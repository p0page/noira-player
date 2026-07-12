# Interaction Recovery Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让音轨和字幕切换报告结构化记录操作耗时与恢复耗时，并由 manifest 显式阈值判定，避免“最终恢复即 completed”掩盖数秒播放中断。

**Architecture:** native helper 在单场景交互前后使用 steady clock 记录 `operationDurationMs` 与 `recoveryDurationMs`，headless parser 将其写入独立的 interaction evidence，而不是 lifecycle message。evaluator 只在 manifest 提供 `maxInteractionRecoveryDurationMs` 时应用阈值；普通 playback cadence 不消费该信号。Kodi 的音轨切换会关闭/重开音频流并发送同步 accurate seek，因此本项目把切流中断归入 interaction recovery，不归入普通 frame pacing。

**Tech Stack:** C++17 native helper、.NET 10/C# report model、PowerShell quality runner、xUnit/native contract tests。

## Global Constraints

- 所有 case 必须真实进入 native 播放链路，message 文本和 expected 不得冒充观测值。
- 不修改既有 startup、seek、frame-pacing 阈值来获得通过。
- 私有 Emby locator、凭据和 direct URL 不进入提交内容。
- 每个行为按 TDD 先红后绿；最终使用同一 manifest 生成 baseline/candidate。

---

### Task 1: Versioned report and manifest contract

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityExpected.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReferenceManifest.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityEvaluatorTests.cs`

**Interfaces:**
- Produces: `PlaybackQualityInteractionEvidence` with scenario, operation duration, recovery duration, position delta and submitted-audio-frame delta.
- Produces: nullable `PlaybackQualityExpected.MaxInteractionRecoveryDurationMs`.

- [ ] **Step 1: Write failing manifest and evaluator tests**

Add tests proving a positive finite threshold is accepted, zero/NaN is rejected, a measured recovery above the threshold fails in `tracks` or `subtitles`, and missing measured evidence cannot pass.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test tests/NoiraPlayer.Core.Tests/NoiraPlayer.Core.Tests.csproj --filter "FullyQualifiedName~PlaybackQualityReferenceManifestTests|FullyQualifiedName~PlaybackQualityEvaluatorTests"`

- [ ] **Step 3: Add the minimal typed contract and evaluator check**

Keep the threshold opt-in and scenario-scoped. Do not parse lifecycle message text.

- [ ] **Step 4: Run focused tests and verify GREEN**

- [ ] **Step 5: Commit the contract**

Commit message: `feat: evaluate interaction recovery evidence`

### Task 2: Native measurement and parser preservation

**Files:**
- Modify: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`
- Modify: `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`
- Modify: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`

**Interfaces:**
- Consumes: Task 1 interaction evidence model.
- Produces: finite non-negative operation/recovery durations from steady-clock observations.

- [ ] **Step 1: Add parser fixtures that fail when attempted interaction timing is missing, negative or non-finite**

- [ ] **Step 2: Run parser contracts and verify RED**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-native-headless-harness-smoke-test.ps1`

- [ ] **Step 3: Measure and emit native interaction timing**

Measure synchronous switch call separately from the bounded wait for selected stream, position advancement and submitted/rendered progress. Preserve both values in the standard report.

- [ ] **Step 4: Run native smoke and verify GREEN**

- [ ] **Step 5: Commit native evidence**

Commit message: `feat: capture interaction recovery timing`

### Task 3: Versioned interaction baseline and App gate

**Files:**
- Modify: `tools/quality-run/New-PrivateEmbyReferenceManifest.ps1`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Consumes: Task 1 threshold and Task 2 measured evidence.
- Produces: commit-bound interaction-evidence baseline for later Core candidates.

- [ ] **Step 1: Add explicit interaction recovery thresholds to generated interaction cases**

Use one documented threshold per scenario class; do not add generic frame/drop limits to seek-based interaction windows.

- [ ] **Step 2: Regenerate ignored private manifest and a new commit-bound baseline with the same case IDs and source locators**

- [ ] **Step 3: Validate the new baseline and preserve v12 as the pre-contract observation set**

Require 24/24 strict validation and report any newly exposed fail without changing the threshold. Do not compare v12 and the new baseline as playback-quality improvement because their expected schemas differ. A later Core strategy candidate must reuse the new baseline's exact manifest.

- [ ] **Step 4: Run full Core/native gate and full App build**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-playback-core-checks.ps1`

Run the repository's documented full Debug x64 App build command and retain its result in `docs/STATUS.md`.

- [ ] **Step 5: Commit docs and final contract**

Commit message: `docs: record interaction recovery baseline`

## Self-Review

- Scope is limited to evaluator honesty and interaction recovery evidence; no playback strategy change is bundled.
- Thresholds remain manifest-owned and versioned; no observed failure can silently rewrite them.
- Typed fields replace message parsing, and native steady-clock values replace synthetic evidence.
- Seek preroll drops remain separate from switch recovery duration, avoiding false frame-pacing conclusions.
