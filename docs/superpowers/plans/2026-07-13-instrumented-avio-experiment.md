# 可观测 AVIO 实验路径实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不改变默认播放路径的前提下，建立可计数的 FFmpeg custom AVIO 实验路径，并用同一 manifest 判断远端启动等待是否来自 read/seek 往返。

**Architecture:** 扩展现有 `HttpMediaInput`，让它在 opt-in 模式下拥有 `avio_open2` 内层输入与 `avio_alloc_context` 外层转发输入；`FfmpegMediaSource` 负责 feature flag、format context 接入和 phase snapshot。指标沿现有 Native/WinRT/App/Core 通道进入 v0.6 report，默认路径明确报告 callback 证据不可用。

**Tech Stack:** FFmpeg 8.1.2、C++20、C++/WinRT UWP、C#/.NET、xUnit、PowerShell native-headless runner。

## Global Constraints

- `main` 的 soft-violet `docs/DESIGN.md` 已进入当前分支，播放器评测改动不得覆盖该设计。
- 默认 App、HTTP、本地文件、probe、准确 backward seek、reconnect、decode/render 行为保持不变。
- `NOIRAPLAYER_NATIVE_INSTRUMENTED_AVIO=1` 是唯一实验入口；失败不得静默 fallback。
- URL、query、token、服务器、用户名和响应 header 不得进入 tracked 文件、日志或 report。
- baseline/candidate 使用同一 manifest、同一规则和同一构建，只改变 feature flag。

---

### Task 1: HttpMediaInput custom AVIO 所有权与转发

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/HttpMediaInput.h`
- Modify: `src/NoiraPlayer.Native/Media/HttpMediaInput.cpp`
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.h`
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.cpp`
- Create: `tests/NoiraPlayer.Native.Tests/HttpMediaInputTests.cpp`
- Modify: `tools/quality-run/run-playback-core-checks.ps1`
- Test: `tests/NoiraPlayer.Core.Tests/Design/NativeFfmpegDiagnosticsContractTests.cs`

**Interfaces:**
- Produce: `HttpMediaInput::Open(source, options, interruptCallback)`、`Attach(AVFormatContext*)`、`Snapshot()`、`Close()`。
- Produce: `HttpMediaInputSnapshot { Provider, EvidenceAvailable, ReadCalls, SeekCalls, ReadWaitMs, SeekWaitMs, SeekDistanceBytes, BytesRead, LastError }`。
- Consume: 现有 HTTP reconnect `AVDictionary` 和 `AVIOInterruptCB`。

- [ ] **Step 1: 写失败测试**

  新增 native 测试，用本地临时文件经 `avio_open2` 打开内层输入，断言外层 `AVIOContext` 可执行 read、`AVSEEK_SIZE`、absolute seek，且 snapshot 的 read/seek 次数、字节和等待时间有确定值；再断言重复 `Close()` 安全。新增 source-contract 测试，锁住 feature flag、`AVFMT_FLAG_CUSTOM_IO`、禁止 fallback 和 URL 不进入 diagnostic。

- [ ] **Step 2: 运行 focused tests 并确认失败**

  Run: `dotnet test tests/NoiraPlayer.Core.Tests/NoiraPlayer.Core.Tests.csproj --filter NativeFfmpegDiagnosticsContractTests`

  Run: 新增到 `run-playback-core-checks.ps1` 的独立 `HttpMediaInputTests.exe` 编译命令。

  Expected: 测试因 custom AVIO API 和 feature flag 分支不存在而失败。

- [ ] **Step 3: 实现最小转发层**

  `HttpMediaInput` 使用固定 32 KiB 外层 buffer。`ReadCallback` 调用 `avio_read`，`SeekCallback` 对 `AVSEEK_SIZE` 调用 `avio_size`、其余调用 `avio_seek`；每次 callback 用 `steady_clock` 累计等待。成功 seek 以 callback 前后 logical position 的绝对差累计距离。FFmpeg 负错误码原样返回。

- [ ] **Step 4: 接入 FfmpegMediaSource opt-in 分支**

  仅 HTTP/HTTPS 且环境变量严格等于 `1` 时创建成员 `HttpMediaInput`；先打开内层 AVIO，再把外层 context 赋给 `formatContext->pb` 并设置 `AVFMT_FLAG_CUSTOM_IO`。默认分支继续执行现有 `avformat_open_input`。close 顺序必须先 `avformat_close_input`，再释放外层与内层 AVIO。

- [ ] **Step 5: 运行 focused tests 并提交**

  Expected: native 转发测试、source-contract 测试、Native Debug x64 build 通过。

  Commit: `feat: add instrumented AVIO experiment path`

### Task 2: Phase-local callback 指标与 v0.6 report

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.h`
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.cpp`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackQualityMetrics.h`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.cpp`
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.idl`
- Modify: `src/NoiraPlayer.Native/NativePlaybackQualityMetrics.h`
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.cpp`
- Modify: `src/NoiraPlayer.App/Playback/WinRtNativePlaybackEngine.cs`
- Modify: `src/NoiraPlayer.App/Views/PlaybackPage.xaml.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityMetricsSnapshot.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityStartupEvidence.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportComposer.cs`
- Modify: `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`
- Test: `tests/NoiraPlayer.Native.Tests/PlaybackQualityMetricsTests.cpp`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativeQualityMetricsBridgeContractTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/AppHostedQualityCaptureContractTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityStartupEvidenceTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReportComposerTests.cs`

**Interfaces:**
- Produce: 每个 startup component 的 `TransportProvider`、`TransportCallEvidenceStatus`、`TransportReadCalls`、`TransportSeekCalls`、`TransportReadWaitMs`、`TransportSeekWaitMs`、`TransportSeekDistanceBytes`。
- Produce: evaluation version `playback-quality-v0.6`。

- [ ] **Step 1: 写 phase delta、bridge 和 report 失败测试**

  明确验证 open-input、find-stream-info、startup-seek、first-frame 四段只记录各自 delta。默认 provider 的 call 字段必须是 JSON `null` 且状态为 `unavailable`；实验 provider 必须全部为非负数且状态为 `measured`。

- [ ] **Step 2: 运行 focused tests 并确认失败**

  Run: `dotnet test tests/NoiraPlayer.Core.Tests/NoiraPlayer.Core.Tests.csproj --filter "PlaybackQualityStartupEvidenceTests|NativeQualityMetricsBridgeContractTests|AppHostedQualityCaptureContractTests|PlaybackQualityReportComposerTests"`

  Expected: 新字段和 v0.6 契约尚不存在。

- [ ] **Step 3: 贯通 native snapshot 与 WinRT/App/Core bridge**

  Native 使用总量 snapshot 的饱和差值形成 phase delta；增加显式 availability/provider，而不是用零推断。按仓库现有 MIDL/build 流程生成 projection，不手改生成文件作为源。

- [ ] **Step 4: 实现 report 与严格 helper parser**

  parser 对 `measured` 要求所有 callback 字段存在，对 `unavailable` 要求它们显式为 null；provider/status 缺失、矛盾或负等待时间均拒绝。现有 transport/payload 字节语义保持不变。

- [ ] **Step 5: 运行 focused tests、完整 Core tests 并提交**

  Expected: focused tests、完整 Core tests、native metrics tests、Native Debug x64 build 通过。

  Commit: `feat: report AVIO callback evidence`

### Task 3: 同 manifest baseline/candidate 实跑

**Files:**
- Keep ignored: `docs/qa/private/baselines/*v06*.local/`
- Keep ignored: `docs/qa/private/candidates/*instrumented-avio*.local/`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Consume: v0.6 默认与实验 provider report。
- Produce: commit-bound baseline/candidate 和下一步明确决策。

- [ ] **Step 1: 生成默认路径 v0.6 baseline**

  使用当前统一公开/私有 manifest，凭据只放当前进程环境。验证 selected/attempted/reported 一致、stable/challenge 均真实进入 native graph、strict validator 通过。

- [ ] **Step 2: 生成实验路径 candidate**

  使用完全相同命令和 manifest，仅设置 `NOIRAPLAYER_NATIVE_INSTRUMENTED_AVIO=1`。若发生 EOF、轨道缺失、seek 误差、播放样本缺失或 parser rejection，保留失败产物并停止性能结论。

- [ ] **Step 3: 比较证据**

  对每个 case 比较 result、startup phase duration/bytes、read/seek calls/waits、timeline、track/subtitle、buffering、A/V sync 和 color。网络波动必须通过重复 case 区分，不能用单次更快宣称改善。

- [ ] **Step 4: 记录决策并提交**

  若候选稳定且无功能退化，将其定义为下一轮连接复用输入层的基础；否则记录失败原因并保留默认路径。不得放宽 7 秒门限或删除 case。

  Commit: `docs: record instrumented AVIO comparison`

### Task 4: 完整 App 验证

**Files:**
- Modify if evidence changes: `docs/STATUS.md`
- Modify if decision changes: `docs/DECISIONS.md`

**Interfaces:**
- Consume: Task 3 通过的软件候选。
- Produce: 完整 App 构建与代表性 App-hosted v0.6 证据。

- [ ] **Step 1: 运行完整 gate**

  Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-playback-core-checks.ps1 -AppDiffBase main`

  Expected: 所有阶段通过。

- [ ] **Step 2: 完成 Native AOT publish 和 Modern App Debug x64 build**

  使用仓库文档化命令，不复用旧 staging layout。

- [ ] **Step 3: 运行一个代表性 App-hosted report**

  report 必须为 v0.6，source revision 与当前提交一致，runtime sample 存在，provider/status/callback 字段符合所选路径。

- [ ] **Step 4: 安全扫描与最终提交**

  对 tracked 文件扫描私有服务器、用户名、密码、token、resolved URL 和 item/media source id；命中数必须为零。更新中文状态文档并提交。
