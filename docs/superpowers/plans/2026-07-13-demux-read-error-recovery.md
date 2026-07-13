# Demux Read Error Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 FFmpeg 内建 reconnect 预算耗尽后暴露给 Core 的暂时读错可以有界恢复，并用真实 native challenge 和正式 report 证明恢复或预算耗尽。

**Architecture:** 在 `FfmpegMediaSource` 内保留同步读取边界，引入独立、纯状态的 bounded retry policy；成功 packet 清零连续错误并形成 recovery。读错遥测贯通 native snapshot、WinRT、Core、headless/App-hosted report；确定性 Range server 先逼出一次 `EIO`，再允许下一次 Core retry 恢复。

**Tech Stack:** C++20、FFmpeg 8.1.2 UWP、C++/WinRT、C#/.NET 10、PowerShell、xUnit、Native AOT UWP。

## Global Constraints

- `AVERROR_EOF` 永不重试，stop/close interrupt 永不计作网络恢复。
- `EAGAIN/EINTR` 可重试；其他非 EOF 错误只对 HTTP(S) 使用最多 10 次连续重试。
- 有效 packet 到达后重置连续计数；预算耗尽保留最后一个原始 FFmpeg code。
- `NOIRAPLAYER_QA_DISABLE_DEMUX_READ_RECOVERY=1` 只关闭 Core retry，用于同 revision、同 v0.9 manifest 的 baseline/candidate；产品默认不设置。
- 不执行 close/reopen/seek，不改变 timeline、轨道、字幕、颜色或 decoder 状态。
- 评测版本升级为 `playback-quality-v0.9`；manifest expected、实际执行和 report 必须一一对应。
- 私有 Emby 地址、账号、token、runtime source map 和 report 不得进入 Git。

---

### Task 1: 纯 demux 读错恢复策略

**Files:**
- Create: `src/NoiraPlayer.Native/Media/FfmpegReadRecovery.h`
- Create: `tests/NoiraPlayer.Native.Tests/FfmpegReadRecoveryTests.cpp`
- Modify: `tests/NoiraPlayer.Native.Tests/NoiraPlayer.Native.Tests.vcxproj`
- Modify: `tests/NoiraPlayer.Native.Tests/NoiraPlayer.Native.Tests.vcxproj.filters`
- Modify: `tools/quality-run/run-playback-core-checks.ps1`

**Interfaces:**
- Produces: `FfmpegReadRecoveryState::ObserveError(...)`, `RecordPacketRecovered(...)` 和 `Snapshot()`。
- Snapshot fields: `ReadErrorCount`, `ReadRetryCount`, `ReadRecoveryCount`, `MaxConsecutiveReadErrors`, `LastReadErrorCode`, `FatalReadErrorCode`, `LastReadRecoveryDurationMs`。

- [ ] **Step 1: 写失败测试**

覆盖 EOF 不重试、interrupt 不重试、本地 EIO 致命、HTTP EIO 前 10 次重试/第 11 次致命、EAGAIN 可重试、成功 packet 形成一次 recovery 并清零、下一 episode 独立计数。

```cpp
FfmpegReadRecoveryState state;
for (int retry = 0; retry < 10; ++retry)
  assert(state.ObserveError(-5, false, false, true, false) == FfmpegReadDisposition::Retry);
assert(state.ObserveError(-5, false, false, true, false) == FfmpegReadDisposition::Fatal);
assert(state.Snapshot().FatalReadErrorCode == -5);
```

- [ ] **Step 2: 运行测试并确认 RED**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-playback-core-checks.ps1 -TargetFilter FfmpegReadRecoveryTests`

Expected: FAIL，因为 `FfmpegReadRecovery.h` 尚不存在或测试 target 尚未注册。

- [ ] **Step 3: 实现最小纯状态策略**

```cpp
enum class FfmpegReadDisposition { Retry, EndOfStream, Fatal };

class FfmpegReadRecoveryState
{
public:
  static constexpr uint32_t MaxConsecutiveRetries = 10;
  FfmpegReadDisposition ObserveError(
      int errorCode, bool endOfStream, bool transient,
      bool httpSource, bool interrupted) noexcept;
  void RecordPacketRecovered(double recoveryDurationMs) noexcept;
  FfmpegReadRecoverySnapshot Snapshot() const noexcept;
};
```

- [ ] **Step 4: 运行新增 native 测试并确认 GREEN**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-playback-core-checks.ps1 -TargetFilter FfmpegReadRecoveryTests`

Expected: PASS，且永久 EIO 在第 11 个连续错误转为 `Fatal`。

- [ ] **Step 5: 提交**

```powershell
git add src/NoiraPlayer.Native/Media/FfmpegReadRecovery.h tests/NoiraPlayer.Native.Tests/FfmpegReadRecoveryTests.cpp tests/NoiraPlayer.Native.Tests/NoiraPlayer.Native.Tests.vcxproj tests/NoiraPlayer.Native.Tests/NoiraPlayer.Native.Tests.vcxproj.filters tools/quality-run/run-playback-core-checks.ps1
git commit -m "test: define bounded demux read recovery policy"
```

### Task 2: 将策略接入真实 FFmpeg 读取和 native metrics

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.h`
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.cpp`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackQualityMetrics.h`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.cpp`
- Modify: `tests/NoiraPlayer.Native.Tests/PlaybackQualityMetricsTests.cpp`
- Modify: `tests/NoiraPlayer.Core.Tests/Design/NativeFfmpegDiagnosticsContractTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `FfmpegReadRecoveryState`。
- Produces: `FfmpegReadTimingSnapshot::Recovery` 与 `PlaybackQualityMetricsSnapshot` 的七个读错恢复字段。

- [ ] **Step 1: 先扩展 snapshot 测试并确认 RED**

```cpp
metrics.ReadErrorCount = 2;
metrics.ReadRetryCount = 2;
metrics.ReadRecoveryCount = 1;
auto snapshot = metrics.Snapshot();
assert(snapshot.ReadRecoveryCount == 1);
```

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-playback-core-checks.ps1 -TargetFilter PlaybackQualityMetricsTests`

Expected: FAIL，字段尚不存在。

- [ ] **Step 2: 接入 `TryReadPacket`**

`av_read_frame` 负值先交给 policy；`Retry` 记录 diagnostic 后继续同一读取循环；`EndOfStream` 返回 false；`Fatal` 用最后 error 调用 `CreateFfmpegError`。成功 packet 调用 `RecordPacketRecovered`，并把首次 error 到恢复 packet 的 wall time 写入 snapshot。

```cpp
auto disposition = m_readRecovery.ObserveError(
    readResult,
    readResult == AVERROR_EOF,
    readResult == AVERROR(EAGAIN) || readResult == AVERROR(EINTR),
    m_isHttpSource,
    m_interruptRequested.load(std::memory_order_acquire));
if (disposition == FfmpegReadDisposition::Retry)
  continue;
```

HTTP Core retry 由 `NOIRAPLAYER_QA_DISABLE_DEMUX_READ_RECOVERY` 精确关闭；关闭时仍记录 error/fatal telemetry，不关闭 FFmpeg 内建 reconnect。source contract 测试必须证明该变量只出现在 native policy 和评测脚本中。

- [ ] **Step 3: 在 `QualityMetricsSnapshot()` 动态合并 read snapshot**

`PlaybackGraph` 每次采样时从 `m_mediaSource.ReadTimingSnapshot()` 复制七个字段；open/seek 的 metrics reset 不得抹掉同一播放会话累计读错。

- [ ] **Step 4: 运行 native 测试**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-playback-core-checks.ps1 -TargetFilter PlaybackQualityMetricsTests`

Expected: PASS。

- [ ] **Step 5: 提交**

```powershell
git add src/NoiraPlayer.Native/Media/FfmpegMediaSource.h src/NoiraPlayer.Native/Media/FfmpegMediaSource.cpp src/NoiraPlayer.Native/Media/PlaybackQualityMetrics.h src/NoiraPlayer.Native/Media/PlaybackGraph.cpp tests/NoiraPlayer.Native.Tests/PlaybackQualityMetricsTests.cpp tests/NoiraPlayer.Core.Tests/Design/NativeFfmpegDiagnosticsContractTests.cs
git commit -m "fix: retry transient demux read errors"
```

### Task 3: 贯通 WinRT、Core 与 App-hosted 证据

**Files:**
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.idl`
- Modify: `src/NoiraPlayer.Native/NativePlaybackQualityMetrics.h`
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.cpp`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityMetricsSnapshot.cs`
- Modify: `src/NoiraPlayer.App/Playback/WinRtNativePlaybackEngine.cs`
- Modify: `src/NoiraPlayer.App/Views/PlaybackPage.xaml.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativeQualityMetricsBridgeContractTests.cs`

**Interfaces:**
- Produces: App 与 headless 共用的 `PlaybackQualityMetricsSnapshot.Read*` 字段，不通过日志文本映射。

- [ ] **Step 1: 在 bridge contract 测试加入七个属性并确认 RED**

```csharp
Assert.Contains("ReadRecoveryCount", idlSource, StringComparison.Ordinal);
Assert.Contains("ReadRecoveryCount = nativeMetrics.ReadRecoveryCount", appBridgeSource, StringComparison.Ordinal);
```

Run: `dotnet test tests/NoiraPlayer.Core.Tests/NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~NativeQualityMetricsBridgeContractTests`

Expected: FAIL。

- [ ] **Step 2: 按相同命名逐层映射七个字段**

IDL 使用 `UInt64`/`Int32`/`Double`；无 fatal error 用 `0`，因为 FFmpeg error code 均为负值。`PlaybackPage` 冻结 snapshot 时必须复制这些字段，避免 App-hosted 报告丢失。

- [ ] **Step 3: 运行 bridge 测试和完整 Core 编译**

Run: `dotnet test tests/NoiraPlayer.Core.Tests/NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~NativeQualityMetricsBridgeContractTests`

Expected: PASS。

- [ ] **Step 4: 提交**

```powershell
git add src/NoiraPlayer.Native/NativePlaybackEngine.idl src/NoiraPlayer.Native/NativePlaybackQualityMetrics.h src/NoiraPlayer.Native/NativePlaybackEngine.cpp src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityMetricsSnapshot.cs src/NoiraPlayer.App/Playback/WinRtNativePlaybackEngine.cs src/NoiraPlayer.App/Views/PlaybackPage.xaml.cs tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativeQualityMetricsBridgeContractTests.cs
git commit -m "feat: bridge demux read recovery metrics"
```

### Task 4: 正式 report 与 manifest expected 合同

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityExpected.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportMapper.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityEvaluator.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityRequiredSignalPolicy.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReferenceManifest.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReferenceReportSetValidator.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityRunComparator.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportComposer.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityEvaluatorTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityRuntimeEvidenceCollectorTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityRunComparatorTests.cs`

**Interfaces:**
- Produces: `PlaybackQualityReadRecoveryExpected`、`PlaybackQualityReadRecovery`、`report.ReadRecovery`。
- Manifest properties: `required`, `minReadErrors`, `minRecoveries`, `maxRetries`。

- [ ] **Step 1: 写 evaluator/validator RED 测试**

要求 required case 在对象缺失、error=0、recovery=0、retry 超限、fatal 非零时失败；完整且恢复时通过。普通 case 允许全零，但字段仍由 v0.9 report 保留。

```csharp
report.Expected!.ReadRecovery = new PlaybackQualityReadRecoveryExpected
{
    Required = true,
    MinReadErrors = 1,
    MinRecoveries = 1,
    MaxRetries = 10
};
report.ReadRecovery.ReadErrorCount = 1;
report.ReadRecovery.ReadRetryCount = 1;
report.ReadRecovery.ReadRecoveryCount = 1;
```

- [ ] **Step 2: 实现模型、mapper、evaluator 与 strict validation**

新增 checks 使用 `readRecovery.*` signal；fatal error 归因 `buffering/network-io`，缺字段归因 `evidence-collection`。Comparator 仅在 evaluation version 和 expected profile 相同后比较 recovery count、retry count 与 duration。

- [ ] **Step 3: 把 evaluation version 全部升级为 v0.9**

修改 composer、CLI smoke fixtures、baseline/candidate tests 中的精确版本；禁止兼容性代码把 v0.8 静默重写为 v0.9。

- [ ] **Step 4: 运行目标 Core 测试**

Run: `dotnet test tests/NoiraPlayer.Core.Tests/NoiraPlayer.Core.Tests.csproj --filter "FullyQualifiedName~PlaybackQualityEvaluatorTests|FullyQualifiedName~PlaybackQualityReferenceManifestTests|FullyQualifiedName~PlaybackQualityRuntimeEvidenceCollectorTests|FullyQualifiedName~PlaybackQualityRunComparatorTests"`

Expected: PASS。

- [ ] **Step 5: 提交**

```powershell
git add src/NoiraPlayer.Core/PlaybackQuality tests/NoiraPlayer.Core.Tests/PlaybackQuality tools/NoiraPlayer.PlaybackQuality.Cli tools/quality-run
git commit -m "feat: version demux read recovery evidence"
```

### Task 5: Headless parser 与确定性 EIO challenge

**Files:**
- Modify: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`
- Modify: `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`
- Modify: `tools/quality-run/Start-FaultingRangeMediaServer.ps1`
- Modify: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativePlaybackGraphDecouplingContractTests.cs`

**Interfaces:**
- Server parameters: `ResetRequestCount`, `ImmediateResetFromRequest`。
- New case id: `local/demux-read-error-recovery-after-pause`。
- Smoke parameters: `-DemuxReadRecoveryOnly` 与 `-ExpectDemuxReadRecoveryFailure`，用于单独生成可比较的 baseline/candidate，不让预期失败污染全量全绿 gate。

- [ ] **Step 1: 写 source-contract 与 parser RED 测试**

断言 helper 输出七字段、parser 缺任一字段拒绝、server 支持连续 reset、新 challenge manifest 明示 `expected.readRecovery`。

- [ ] **Step 2: 输出并严格解析 native 字段**

helper 从 `QualityMetricsSnapshot()` 输出七字段；headless parser 用 required finite/nonnegative parser 读取计数和 duration，error code 允许 `<= 0`，正数或缺失拒绝。

- [ ] **Step 3: 实现故障服务器模式**

request 1 在 pause marker 后 reset；request 2 到 `ResetRequestCount` 在响应 body 前立即 RST；后续 request 正常 Range 发送。日志必须逐个写 `request=N forcedReset=True` 和最终非零 `rangeStart`。

- [ ] **Step 4: 在同一 v0.9 revision 生成关闭策略的 baseline**

Run: `$env:NOIRAPLAYER_QA_DISABLE_DEMUX_READ_RECOVERY='1'; powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-native-headless-harness-smoke-test.ps1 -DemuxReadRecoveryOnly -ExpectDemuxReadRecoveryFailure -SourceRevision demux-read-recovery-disabled-v0.9`

Expected: 新 challenge 生成真实 native error report，保留 `av_read_frame failed: I/O error` 和 fatal code；它不得被计为 completed/rendered pass。清除变量后用同一 manifest/fault 参数生成 candidate。

- [ ] **Step 5: 验证永久错误负例**

让 server 对 helper 生命周期内所有请求 reset，断言 helper 非零退出、stderr 保留 `av_read_frame` 与原始 FFmpeg error，并且 retry 次数不超过 10；该负例不写入全绿正式 corpus。

- [ ] **Step 6: 运行完整 native smoke**

Run: `Remove-Item Env:NOIRAPLAYER_QA_DISABLE_DEMUX_READ_RECOVERY -ErrorAction SilentlyContinue; powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-native-headless-harness-smoke-test.ps1 -DemuxReadRecoveryOnly -SourceRevision demux-read-recovery-enabled-v0.9`

Expected: candidate 是 1 份真实 materialized pass report，新 case 至少一次 error/retry/recovery、fatal=0，server 顺序证据成立。随后不带 scope 参数运行完整 smoke，得到 14 份真实 report，strict validation 的 completed/rendered 均为 14；用同一 v0.9 manifest 比较 Task 5 Step 4 的 baseline 与本次 candidate，结论为可比较且恢复缺陷消失。

- [ ] **Step 7: 提交**

```powershell
git add tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs tools/quality-run/Start-FaultingRangeMediaServer.ps1 tools/quality-run/run-native-headless-harness-smoke-test.ps1 tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativePlaybackGraphDecouplingContractTests.cs
git commit -m "test: add deterministic demux read recovery challenge"
```

### Task 6: 完整验证、私有重复观测和文档

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

- [ ] **Step 1: 运行全量 Core/native gate**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-playback-core-checks.ps1 -AppDiffBase main`

Expected: 全部阶段 PASS，包含 14-case 真实 native corpus 和所有 native 单测。

- [ ] **Step 2: 运行全量 Core tests**

Run: `dotnet test tests/NoiraPlayer.Core.Tests/NoiraPlayer.Core.Tests.csproj`

Expected: 0 failed。

- [ ] **Step 3: 用同一私有 Emby manifest 做至少三轮长暂停观测**

凭据仅放进进程环境变量；每轮使用同一 manifest/evaluation version，记录 pass/fail、read error/retry/recovery、pause recovery、position/decoded/rendered delta。若未复现真实 EIO，只能声称确定性 case 修复，不能声称私有服务问题已消失。

- [ ] **Step 4: 完整 App Publish**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-NoiraModernUwp.ps1 -Configuration Debug -Platform x64 -Target Publish`

Expected: Modern UWP Debug x64 Native AOT publish 成功。

- [ ] **Step 5: 更新中文状态与决策**

记录 v0.9 版本边界、三套参考源码 commit、确定性 EIO baseline/candidate、永久失败边界、私有观测结果和未验证风险。

- [ ] **Step 6: 最终检查并提交**

Run: `git diff --check`

Run: `rg -n "c1\.zdz\.plus|975245|api_key=|AccessToken" docs src tests tools`

Expected: 无敏感值命中；worktree 仅包含本轮文档变更。

```powershell
git add docs/STATUS.md docs/DECISIONS.md
git commit -m "docs: record demux read recovery evidence"
```
