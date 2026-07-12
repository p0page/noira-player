# In-Session Seek Replay Cache Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为会话内短距离回退建立有界压缩包 replay cache，并用 v0.3 同 manifest baseline/candidate 证明它降低 seek recovery 且不损害准确性。

**Architecture:** 新的 `FfmpegSeekReplayCache` 只管理激活流的 `AVPacket` 历史、关键帧覆盖判断和 replay queue 克隆；`FfmpegMediaSource` 负责观察真实 demux 包并在 seek 时选择 cache 或 FFmpeg fallback。完整实现先以默认关闭提交，重建 v0.3 baseline；candidate 只切换默认开关。

**Tech Stack:** C++20、FFmpeg 8.1.2、C++/WinRT、C#/.NET 10、PowerShell quality runner、xUnit。

## Global Constraints

- 总缓存最多 `48 MiB`、`12s`、`32768` 包。
- cache miss 必须执行现有准确 `av_seek_frame(..., AVSEEK_FLAG_BACKWARD)`。
- 私有 Emby 身份、token、item/source ID 和直链不得进入提交。
- baseline/candidate 使用同一 v0.3 manifest、相同 case ID 和 expected；不得放宽 `2000ms` recovery 或 `500ms` landing 标准。
- 冷 resume、服务端 remux、转码和持久缓存不在本计划范围。

---

### Task 1: Replay Cache 纯状态机

**Files:**
- Create: `src/NoiraPlayer.Native/Media/FfmpegSeekReplayCache.h`
- Create: `src/NoiraPlayer.Native/Media/FfmpegSeekReplayCache.cpp`
- Create: `tests/NoiraPlayer.Native.Tests/FfmpegSeekReplayCacheTests.cpp`
- Modify: `tests/NoiraPlayer.Native.Tests/NoiraPlayer.Native.Tests.vcxproj`

**Interfaces:**
- Consumes: `AVPacket`, normalized packet position ticks, stream index, keyframe flag, active stream set。
- Produces: `ObservePacket(...)`, `TryBuildReplay(...)`, `Clear()`, `Snapshot()`；命中结果包含 per-stream cloned packets、包数、字节、窗口和 fallback reason。

- [ ] 写 RED：连续视频关键帧与音频包覆盖目标时命中；缺关键帧、缺轨、窗口外、超限裁剪时 miss。
- [ ] 运行独立 C++ test，确认因类型不存在失败。
- [ ] 实现 AVPacket RAII clone、全局 sequence、关键帧边界裁剪和 coverage 判定。
- [ ] 运行测试，确认命中包顺序、内存释放和三个上限全部通过。
- [ ] 提交 `feat: add bounded seek replay cache state`。

### Task 2: FfmpegMediaSource 与 PlaybackGraph 接入，默认关闭

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.h`
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.cpp`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.h`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.cpp`
- Modify: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`

**Interfaces:**
- Produces: `FfmpegSeekReplaySnapshot TryPrepareSeekReplay(...)`；`PlaybackGraphSeekTiming` 返回 enabled/hit/cache size/window/fallback 和 FFmpeg seek duration。

- [ ] 写 RED：cache disabled 必走 FFmpeg seek；完整覆盖命中时不得调用 seek callback；miss 时必须调用一次 fallback。
- [ ] 在 `TryReadPacket` 的真实 `av_read_frame` 成功边界观察包，重放 queue 读取时不重复观察。
- [ ] `PlaybackGraph::Seek` 命中后 flush decoder、保留 preroll target 并 `RenderNextFrame`；miss 保持原路径。
- [ ] native helper 新增 `--enable-seek-packet-cache`，但默认关闭。
- [ ] 运行 replay、timeline、audio preroll、EAGAIN 和 native-headless smoke。
- [ ] 提交 `feat: integrate disabled seek replay cache`。

### Task 3: v0.3 结构化证据

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/PlaybackQualityMetrics.h`
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.idl`
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.cpp`
- Modify: `src/NoiraPlayer.App/Playback/WinRtNativePlaybackEngine.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityRequiredSignalPolicy.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualitySignalCatalog.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityRunComparator.cs`
- Modify: `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`
- Modify: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/*`

**Interfaces:**
- Produces: 六个 `position.seekPacketCache*`/`seekFallbackReason` 字段和 `playback-quality-v0.3`。

- [ ] 写 RED：timeline required signals、parser 缺字段拒绝、model analysis、comparison matched signals、App bridge 均要求 cache 证据。
- [ ] 贯通 native snapshot、WinRT、App、helper、strict parser、report、analyzer、comparator。
- [ ] 将 evaluation version 升级为 `playback-quality-v0.3`，只更新当前契约，不改历史归档。
- [ ] 运行 Core 全量、CLI smoke、native-headless smoke 和完整 App build。
- [ ] 提交 `feat: attribute seek replay cache evidence`。

### Task 4: 默认关闭的 v0.3 Baseline

**Files:**
- Local only: `docs/qa/private/manifests/in-session-seek-v03.local.json`
- Local only: `docs/qa/private/in-session-seek-v03-baseline-repeat-*.local/`
- Modify: `docs/STATUS.md`

- [ ] 从同一私有 timeline case 生成 v0.3 manifest，保留 `2000ms` recovery 与 `500ms` landing。
- [ ] 连续三轮真实 native 播放，要求 cache enabled/hit 均 false、fallback reason 为 disabled、strict 1/1 matched。
- [ ] 归档 startup、operation、recovery、landing、post-seek、错误与网络日志；失败轮不得覆盖。
- [ ] 提交 `docs: record v0.3 disabled seek cache baseline`。

### Task 5: 单变量 Candidate 与比较

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.h` 或对应 open-request default
- Local only: `docs/qa/private/in-session-seek-v03-candidate-repeat-*.local/`
- Local only: `docs/qa/private/comparisons/in-session-seek-v03.local/`

- [ ] 写 RED：普通 App/native open 默认启用，测试显式关闭仍可形成 fallback control。
- [ ] 只切换默认值，不改缓存算法、manifest 或阈值。
- [ ] 运行三轮同 manifest candidate；要求三轮 hit、recovery `<2000ms`、landing `<=500ms`、post-seek 继续推进。
- [ ] 运行 candidate comparison 和 repeat stability，检查 A/V sync、buffering、tracks/subtitles、color/DXGI 无回退。
- [ ] 若不满足采纳标准，恢复默认关闭并保留报告；若满足，提交 `feat: enable bounded seek replay cache`。

### Task 6: 完整 App 复核

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Local only: `docs/qa/private/app-hosted/in-session-seek-v03.local/`

- [ ] 运行 `tools/quality-run/run-playback-core-checks.ps1 -AppDiffBase main`。
- [ ] 运行 `tools/Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64`。
- [ ] 使用同一私有 timeline case 执行 App-hosted quality-run，确认 cache hit、首帧恢复、落点和继续播放。
- [ ] strict materialize/validate App 报告，并与 App-free candidate 对照。
- [ ] 更新状态、决策、剩余风险和冷 resume 非目标边界，提交文档。
