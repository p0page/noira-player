# Native Interaction Evidence And Subtitle Resync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把现有 A/V native-headless case 升级为真实双音轨、双字幕、非零 seek 行为案例，并修复嵌入字幕中途切换后无法取得当前 cue 的 Core 问题。

**Architecture:** 原有 cadence/A/V 快照继续在交互前采集；helper 在快照后执行真实切轨、字幕切换/关闭和非零 seek，并把每项结果映射成 lifecycle event。先提交 evidence-only 版本并生成旧 Core baseline，再在 `PlaybackGraph` 中按 Kodi 的嵌入字幕原则执行当前点 demux 重定位，最后用同一 manifest 生成 candidate/repeat/comparison。

**Tech Stack:** C++20、FFmpeg 8.1.2、DirectX/XAudio2、.NET 8、PowerShell、xUnit。

## Global Constraints

- 不修改 App/UI，不使用 Xbox、HDMI 或显示器作为完成证据。
- 保持 24 个 case ID 不变；manifest 语义升级后必须重新生成 baseline。
- cadence/A/V/buffering 指标来自交互前快照。
- 不放宽现有 250ms seek error、A/V drift 或 frame-pacing 标准。
- helper 单项操作失败时仍要生成完整报告。
- baseline 和 candidate 必须使用同一个 materialized manifest。

---

### Task 1: 让失败的 lifecycle 操作成为正式失败

**Files:**
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityEvaluatorTests.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityEvaluator.cs`

**Interfaces:**
- Consumes: `PlaybackQualityLifecycle.Events`
- Produces: `PlaybackQualityCheck`，signal 为 `lifecycle.<operation>`，失败区域由 operation 映射。

- [ ] **Step 1: 写 evaluator 红灯测试**

在 evaluator tests 中构造带有效 expected 的 report，加入：

```csharp
report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
{
    Operation = "subtitle-switch",
    Status = "failed",
    PositionTicks = 30_000_000,
    Message = "no subtitle cue was rendered after switching"
});
```

断言 `Evaluate` 后 `Result == "fail"`，存在 signal `lifecycle.subtitle-switch`、failure area `subtitles` 的 failed check，并保留原 message。再分别覆盖 `audio-switch -> tracks`、`seek -> timeline`；`completed` 不产生失败。

- [ ] **Step 2: 运行红灯测试**

Run:

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityEvaluatorTests
```

Expected: 新测试 FAIL，因为 evaluator 尚未检查 lifecycle status。

- [ ] **Step 3: 实现最小 lifecycle gate**

在阈值检查之后、`AssignFailureClasses` 之前调用：

```csharp
CheckFailedLifecycleOperations(report);
```

新增私有方法，遍历 status 为 `failed` 或 `error` 的 event，按以下规则产生 check：

```csharp
private static string GetLifecycleFailureArea(string operation) => operation switch
{
    "audio-switch" => "tracks",
    "subtitle-switch" or "subtitle-off" => "subtitles",
    "seek" => "timeline",
    _ => "playback-lifecycle"
};
```

message 优先使用 event message，否则使用稳定的默认说明。不要把 `skipped` 当 fail。

- [ ] **Step 4: 运行 targeted 与全 Core tests**

Run:

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityEvaluatorTests
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj
```

Expected: PASS。

---

### Task 2: 增加真实字幕绘制与选择状态 testability hook

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/SubtitleRenderer.h`
- Modify: `src/NoiraPlayer.Native/Media/SubtitleRenderer.cpp`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.h`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.cpp`
- Modify: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`

**Interfaces:**
- Produces: `uint64_t PlaybackGraph::SubtitleCueRenderCount() const noexcept`
- Produces: `std::optional<int32_t> PlaybackGraph::SelectedSubtitleStreamIndex() const noexcept`，只转发 renderer 已有选择状态，不在 Graph 复制第二份 selected index。
- Internal: `bool SubtitleRenderer::RenderAt(int64_t positionTicks)`，只有 `DrawTextOverlay` 成功时返回 true。

- [ ] **Step 1: 先让 helper 引用尚不存在的 hook**

在 native helper 的输出中加入 `subtitleCueRenderCount` 和 selected subtitle index，并在最终 assert 前读取两个 API。

- [ ] **Step 2: 编译并确认红灯**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-native-headless-harness-smoke-test.ps1
```

Expected: helper 编译失败，指出两个 API 尚不存在。

- [ ] **Step 3: 实现最小 hook**

让 `SubtitleRenderer::RenderAt` 在 cue 有效时返回 `m_deviceResources.DrawTextOverlay(m_textCue)`，其余路径返回 false；新增只读 `SelectedStreamIndex()` 返回 renderer 已有的 `m_selectedSubtitleStreamIndex`。`PlaybackGraph::UpdateSubtitleCue` 在 `RenderAt` 返回 true 时递增 `m_subtitleCueRenderCount`；在 open/stop/runtime reset 时归零。`PlaybackGraph::SelectedSubtitleStreamIndex()` 在持锁后转发 renderer getter，不新增重复选择状态。

两个 getter 必须持有 `m_graphMutex`，不得暴露可写状态。

- [ ] **Step 4: 运行 helper smoke**

Run 同 Step 2。

Expected: helper 重新编译并运行；此时只是 hook 可用，尚未要求真实切换成功。

---

### Task 3: 升级 A/V 样本并执行真实交互

**Files:**
- Modify: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- Modify: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`
- Modify: `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`

**Interfaces:**
- Helper stdout adds: `audioSwitchStatus`、`audioSwitchStreamIndex`、`audioSwitchPositionBeforeTicks`、`audioSwitchPositionAfterTicks`、`subtitleSwitch1Status`、`subtitleSwitch2Status`、`subtitleOffStatus`、对应 stream index/cue count、`seekTargetPositionTicks`、`seekActualPositionTicks`、`postSeekPlaybackPositionTicks`。
- C# parser produces lifecycle events and final selected stream state.

- [ ] **Step 1: 先收紧 smoke 断言并确认红灯**

要求 A/V report：

```powershell
$nativeAvReport.report.tracks.audioTrackCount -eq 2
$nativeAvReport.report.tracks.subtitleTrackCount -eq 2
$nativeAvReport.report.position.seekTargetPositionTicks -eq 10000000
$nativeAvReport.report.lifecycle.events.operation -contains 'audio-switch'
$nativeAvReport.report.lifecycle.events.operation -contains 'subtitle-switch'
$nativeAvReport.report.lifecycle.events.operation -contains 'subtitle-off'
```

并要求对应 event status 为 `completed`。运行 smoke，Expected: FAIL，旧样本只有单音轨/单字幕且无切换 lifecycle。

- [ ] **Step 2: 生成双音轨双字幕 6 秒样本**

在 `New-NativePlaybackAvSample` 中增加第二个 sine 输入和第二个 SRT 输入；映射 1 video、2 audio、2 subtitle，设置 audio/subtitle language 和 default disposition。移除会把样本截到旧字幕长度的隐式依赖，显式使用 6 秒 cue 和 `-t 6`。

- [ ] **Step 3: helper 执行并记录操作，不因单项失败退出**

交互前保留原 `playbackSnapshot`。从 `SourceTrackSnapshots()` 找到第二条 audio 和两条 subtitle。每项操作使用独立 `try/catch` 填写 status：

```cpp
graph.SwitchAudioStream(alternateAudioIndex);
std::this_thread::sleep_for(500ms);
audioSwitchCompleted = graph.CurrentPositionTicks() > positionBefore &&
    graph.QualityMetricsSnapshot().SubmittedAudioFrames > submittedBefore;
```

字幕切换以 `SubtitleCueRenderCount()` 的增长为成功条件；字幕关闭以 `SelectedSubtitleStreamIndex() == std::nullopt` 为成功条件。最后执行 `graph.Seek(10'000'000)`，并确认 post-seek position 大于 immediate landing position。

- [ ] **Step 4: C# parser 映射真实 lifecycle**

扩展 `NativeHeadlessHelperResult` 保存每个 interaction outcome。只在 attempted 时添加 lifecycle event，status 使用 helper 返回的 `completed`/`failed`，message 包含 stream index、前后位置或 cue count。`CreateDescriptor` 使用最终 selected audio/subtitle index。

- [ ] **Step 5: 运行 smoke，确认旧 Core 的预期红灯形态**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-native-headless-harness-smoke-test.ps1
```

Expected: 样本和报告完整生成；audio switch、subtitle off、1s seek 完成；至少一个 subtitle switch lifecycle 为 `failed`，A/V case result 为 `fail`，错误原因指向没有真实 cue overlay，而不是 evidence-collection。

---

### Task 4: 固定 evidence-only commit 与新 manifest baseline

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

- [ ] **Step 1: 记录旧 Core 的真实结果**

中文记录新 manifest 语义、旧 artifact 不可混用、各 operation 状态、subtitle switch 根因假设和 Kodi 对照位置。不得把 evaluator/helper 增强描述为播放质量改善。

- [ ] **Step 2: 运行完整检查**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.ps1
git diff --check
```

Expected: 全部通过；smoke 脚本应把“已知 subtitle failure 被诚实报告”视为 harness PASS。

- [ ] **Step 3: 提交 evidence-only 版本**

```powershell
git add src tests tools docs/STATUS.md docs/DECISIONS.md
git commit -m "tools: exercise native playback interactions"
```

- [ ] **Step 4: 生成 commit-bound baseline**

使用 `New-PlaybackCoreTuningBaseline.ps1` 的现有私有 manifest 参数生成新 24-case baseline，确认：manifest validation 通过、nativeHeadless included、A/V case subtitle lifecycle 为 fail、sourceRevision 等于 evidence-only commit。

---

### Task 5: 以当前点重定位修复嵌入字幕切换

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.cpp`
- Modify: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`

**Interfaces:**
- Consumes: `PlaybackGraph::SwitchSubtitleStream(std::optional<int32_t>)`
- Produces: 切换新嵌入字幕后，当前时间覆盖 cue 能被 decoder 重新读取并绘制。

- [ ] **Step 1: 把 smoke 中 subtitle completed 断言作为 Core 红灯**

运行 evidence-only commit 的 smoke，确认失败仅来自 subtitle cue 未绘制，保存输出作为 TDD red evidence。

- [ ] **Step 2: 实现最小 current-position resync**

在 enable subtitle 分支注册新 decoder 后执行：

```cpp
auto const resumePositionTicks = m_positionTicks;
m_pendingVideoFrame.reset();
ResetAudioAheadWait();
ResetVideoClock();
m_audioRenderer.Flush();
m_videoDecoder.Seek(resumePositionTicks);
m_audioDecoder.Flush(resumePositionTicks);
m_subtitleDecoder.Flush();
SetVideoPrerollTarget(resumePositionTicks);
m_subtitleRenderer.SwitchStream(*subtitleStreamIndex);
```

disable 分支不 seek。保持 `m_paused` 和 audio started/paused 状态不变，最后通知 render loop。

- [ ] **Step 3: 运行 targeted smoke 并确认绿灯**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-native-headless-harness-smoke-test.ps1
```

Expected: 两次 subtitle switch 都有 cue overlay 增量，全部 interaction lifecycle completed，非零 seek 误差 <= 250ms。

- [ ] **Step 4: 运行完整检查并提交 candidate**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.ps1
git diff --check
git add src tests docs/STATUS.md docs/DECISIONS.md
git commit -m "playback: resync embedded subtitles on switch"
```

Expected: 全部通过。

---

### Task 6: 同 manifest candidate、repeat 与采纳结论

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Private ignored artifacts: `docs/qa/private/baselines/*.local`、`candidates/*.local`、`repeats/*.local`、`comparisons/*.local`

- [ ] **Step 1: 从 candidate commit 生成 report-set**

使用 Task 4 baseline 内保存的同一 materialized manifest。验证 case IDs、expected 和 source URI 完全一致。

- [ ] **Step 2: 运行至少 3 次 native repeat**

聚合 A/V interaction 状态、seek error、cadence、A/V drift、buffering 和 frame tail。interaction 不允许只在单次运行偶然通过。

- [ ] **Step 3: 生成 baseline/candidate comparison**

确认报告明确列出 improved/regressed/mixed/unchanged、matched/unmatched signals、repeat attribution、风险和 recommendation。若 stable case 有新增回归，拒绝 candidate 并保留证据。

- [ ] **Step 4: 文档化并提交结论**

只有同 manifest comparison 和 repeat 均支持时，才记录“字幕切换修复可采纳”；不要把这项结论扩展为 frame pacing 已解决。更新下一步为 Kodi 式 presentation scheduler 的结构性候选。

- [ ] **Step 5: 最终验证**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.ps1
git status --short --branch
```

Expected: checks PASS；仓库只有明确记录的改动，私有 artifact 保持 ignored。
