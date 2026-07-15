# 有界视频解码队列实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将视频解码从呈现循环中解耦为容量固定的异步队列，解决同步 D3D11VA 解码耗时与固定 render-loop wait 叠加造成的 60fps 慢放，同时保持 seek、暂停恢复、切轨、字幕、HDR 和错误恢复语义。

**Architecture:** 参考 Kodi `CVideoPlayerVideo -> CRenderManager` 的生产者/有界缓冲/消费者边界，但不移植完整 RenderManager。`VideoDecodeWorker` 独占 `VideoDecoder`，向最多 3 帧的 generation-aware 队列写入；现有 render thread 继续负责音频主时钟、丢帧、字幕、颜色映射和 `Present`。`FfmpegMediaSource` 只在真实 demux/packet queue/seek 入口串行化，D3D11 immediate context 开启 multithread protection。

**Tech Stack:** C++20、C++/WinRT、FFmpeg 8.1.2、D3D11、现有 native-headless helper 与 playback-quality-v0.12。

## Global Constraints

- 队列容量固定为 3，不允许按码率、分辨率或运行时间无界增长。
- producer failure、EOS、stop 和 generation reset 必须有明确状态；不得用空队列冒充 EOS。
- seek、startup resume、音轨切换和字幕切换前停止 worker，清空旧 generation，完成 decoder/media-source 操作后再启动。
- 不改变音频主时钟、HDR/DXGI 映射、字幕选择和 Emby 源选择策略。
- baseline/candidate 使用相同 manifest、30 秒窗口和 90 秒 attempt timeout；`SamplePlaybackRate` 不得放宽。
- 只有完整 native smoke、公开 60fps 重复、私有 Emby 代表 case 和 Modern App 构建均通过后才接受候选。

---

### Task 1: 有界 generation 队列

**Files:**
- Create: `src/NoiraPlayer.Native/Media/VideoFrameQueue.h`
- Create: `tests/NoiraPlayer.Native.Tests/VideoFrameQueueTests.cpp`
- Modify: `tools/quality-run/run-playback-core-checks.ps1`

**Interfaces:**
- Produces: `VideoFrameQueue<T, Capacity>`，提供 `Reset()`、`Stop()`、`Push(generation, value)`、`TryPop(generation)`、`MarkEndOfStream(generation)`、`Fail(generation, exception_ptr)` 和 snapshot。

- [ ] 先写测试，覆盖容量 3、producer 阻塞后由 pop 唤醒、reset 丢弃旧 generation、stop 唤醒、EOS 与空队列区分、异常传播。
- [ ] 用现有 MSVC native test 命令确认因缺少 `VideoFrameQueue.h` 红灯。
- [ ] 仅用 mutex、condition_variable 和 deque 实现队列；所有 wait 都包含 stop/generation predicate。
- [ ] 运行 `VideoFrameQueueTests.exe`，确认全部通过并加入 App-free gate。

### Task 2: 共享 demux 与 D3D 并发边界

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.h`
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.cpp`
- Modify: `src/NoiraPlayer.Native/DxDeviceResources.cpp`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativePlaybackGraphDecouplingContractTests.cs`

**Interfaces:**
- Produces: `m_demuxMutex` 保护 `TryReadPacket`、`TryReadQueuedPacket`、`Seek` 及 packet/replay queue 状态；D3D immediate context 的 `ID3D11Multithread::SetMultithreadProtected(TRUE)`。

- [ ] 先写 source-contract 测试，要求 demux 入口共享同一把锁，并要求 device 创建后启用 multithread protection。
- [ ] 运行 focused Core tests，确认缺少锁和 D3D protection 红灯。
- [ ] 实现最小锁边界；不得在 worker 存活时调用 register/unregister/open/close。
- [ ] 运行 focused tests、native media-source tests 和 native Debug x64 build。

### Task 3: 视频解码 worker

**Files:**
- Create: `src/NoiraPlayer.Native/Media/VideoDecodeWorker.h`
- Create: `src/NoiraPlayer.Native/Media/VideoDecodeWorker.cpp`
- Create: `tests/NoiraPlayer.Native.Tests/VideoDecodeWorkerTests.cpp`
- Modify: `src/NoiraPlayer.Native/NoiraPlayer.Native.vcxproj`

**Interfaces:**
- Consumes: `VideoDecoder::TryReadFrame()` 与 `VideoFrameQueue<QueuedVideoFrame, 3>`。
- Produces: `Start(generation)`、`Stop()`、`Reset(generation)`、`TryPop(generation)`；`QueuedVideoFrame` 同时携带 frame 和 decode duration。

- [ ] 先用注入 decode callback 的真实线程测试覆盖三帧上限、EOS、异常、stop 和 generation reset。
- [ ] 确认缺少 worker API 红灯。
- [ ] 实现 worker，异常只存入队列并唤醒消费者，不跨线程抛出。
- [ ] 运行 worker/queue tests 并加入 App-free gate。

### Task 4: PlaybackGraph 接入

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.h`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.cpp`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativePlaybackGraphDecouplingContractTests.cs`
- Modify: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`

**Interfaces:**
- Render thread 从 worker 队列消费；worker 负责 decode duration，render thread 负责 metrics 合并、clock/drop/render/present。

- [ ] 先写 contract 和 real-helper 断言：`TryReadFrame()` 不得再位于 `RenderNextFrame()`；seek/stop/switch 必须 stop-reset-restart；报告必须包含 queue depth、producer wait 和 underrun 证据。
- [ ] 确认现有同步图红灯。
- [ ] 接入 worker，所有 decoder flush/seek/close 前先 stop；EOS 仅在 worker EOS 且队列为空时成立；worker failure 转成现有 Failed 状态。
- [ ] 运行 queue/worker/frame-pacing tests、完整 native-headless smoke 和 Core tests。

### Task 5: 同版本候选判定与 App 复核

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Produces: 同 manifest baseline/candidate/repeat 报告及 accept/reject 结论。

- [ ] 用三个公开 1080p60 case 各跑 30 秒；比较 media progress、wall clock、decode/render/present、P50/P95/P99/max、queue depth/underrun、transport/demux。
- [ ] 至少重复 HDR 与 SDR 各 3 次；任何 SamplePlaybackRate fail、长尾显著回归或 worker failure 都拒绝候选。
- [ ] 用私有 Emby 的“一战再战”“哈姆奈特”运行代表 case，凭据只从 ignored runtime source map/环境变量读取。
- [ ] 运行完整 `run-playback-core-checks.ps1` 与 `Build-NoiraModernUwp.ps1 -Target Build`。
- [ ] 在中文 STATUS/DECISIONS 记录证据和 Kodi 对照；接受则提交 Core/native 改动，拒绝则撤回行为代码但保留评测与报告结论。
