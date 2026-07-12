# App-hosted Interaction Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让完整 Windows App 的 audio-switch 与 subtitle-switch 报告产生和 native-headless 同语义的 typed interaction evidence，并通过同一 manifest 的严格校验。

**Architecture:** `PlaybackGraph` 继续返回现有 `PlaybackGraphSwitchTiming`；`NativePlaybackEngine` 保存最近一次切流快照并通过 WinRT quality metrics 暴露。App 用单调时钟和操作前后 metrics 计算 operation/recovery/cue/delta，再与 native phase/cache 快照组合后传给现有 report composer。

**Tech Stack:** C++20/C++/WinRT、MIDL 3.0、.NET 10/C#、PowerShell、xUnit、Windows Modern UWP。

## Global Constraints

- 不改变切流行为、manifest expected、required-signal policy 或 `2000ms` SLO。
- 不解析 lifecycle message、日志或 stdout 来补 typed evidence。
- 每次 open/stop 清空最近切流快照，scenario 不匹配时拒绝消费。
- 私有 Emby 凭据、item ID、media source ID 和报告只保存在 ignored/local 路径。

---

### Task 1: Native 最近切流快照

**Files:**
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.idl`
- Modify: `src/NoiraPlayer.Native/NativePlaybackQualityMetrics.h`
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.h`
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.cpp`
- Test: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`

**Interfaces:**
- Consumes: `PlaybackGraphSwitchTiming PlaybackGraph::SwitchAudioStream(...)` / `SwitchSubtitleStream(...)`
- Produces: WinRT metrics fields `LastInteractionScenario`, `LastInteractionSequence`, phase durations and packet-cache evidence.

- [ ] **Step 1: Write a failing source/native test**

Assert that the engine assigns the graph return value for audio/subtitle switches and resets the snapshot during open/stop; assert the IDL exposes every v14 native field.

- [ ] **Step 2: Run the native/source test and verify RED**

Run the focused source contract plus Native Debug x64 build. Expected: missing properties or missing assignments.

- [ ] **Step 3: Implement the snapshot**

Store a scenario-tagged, monotonically sequenced `PlaybackGraphSwitchTiming` under the existing engine mutex. Copy it into `NativePlaybackQualityMetrics`; use empty scenario and sequence zero before any interaction.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the same source/native tests and Native build. Expected: exit code 0.

### Task 2: Core and WinRT mapping

**Files:**
- Modify: `src/NoiraPlayer.Core/Playback/IPlaybackQualityMetricsProvider.cs`
- Modify: `src/NoiraPlayer.App/Playback/WinRtNativePlaybackEngine.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityEvaluatorTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/Design/ModernPlaybackSourceTests.cs` or the existing WinRT source-contract test.

**Interfaces:**
- Consumes: Task 1 WinRT fields.
- Produces: nullable `PlaybackQualityMetricsSnapshot.LastInteraction*` properties preserving zero-valued phase durations and false cache flags.

- [ ] **Step 1: Add failing mapping/contract assertions**

Require every native property to map by name and require Core snapshot cloning/composition to retain explicit zero and false values.

- [ ] **Step 2: Run focused Core tests and verify RED**

Run `dotnet test` with playback-quality and modern playback source filters. Expected: missing mapped fields.

- [ ] **Step 3: Add snapshot properties and direct mapping**

Add one property per v14 phase/cache signal plus scenario and sequence; map directly without defaults that confuse “not observed” with zero.

- [ ] **Step 4: Run focused tests and verify GREEN**

Expected: all selected tests pass with zero failures.

### Task 3: App 场景证据组合

**Files:**
- Modify: `src/NoiraPlayer.App/Views/PlaybackPage.xaml.cs`
- Prefer Create: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityInteractionCapture.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityInteractionCaptureTests.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityRuntimeEvidenceCollector.cs`

**Interfaces:**
- Consumes: operation before/after snapshots, native last-interaction snapshot, monotonic elapsed durations.
- Produces: `PlaybackQualityInteractionEvidence` passed explicitly to `ComposeRunResult`/`CreateRequest`.

- [ ] **Step 1: Add deterministic failing policy tests**

Cover audio success, subtitle success, cache miss with nonzero seek, scenario mismatch, missing metrics, no position/audio/video/cue progress, and operation exception. Tests must assert exact typed fields rather than lifecycle text.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: interaction capture type/API does not exist.

- [ ] **Step 3: Implement deterministic capture model**

Use a small Core value/policy type to combine snapshots and elapsed values. In `PlaybackPage`, start `Stopwatch` before calling orchestrator, record operation elapsed on return, poll only for scenario-specific recovery, and save the resulting evidence for the final report request.

- [ ] **Step 4: Pass interaction into the report composer**

Extend runtime collector overloads with optional `PlaybackQualityInteractionEvidence interaction = null`; assign `request.Interaction = interaction`. Existing callers remain source-compatible.

- [ ] **Step 5: Run focused tests and verify GREEN**

Expected: all deterministic capture and report serialization tests pass.

### Task 4: App-hosted 双 case 闭环

**Files:**
- Modify: `tools/Test-NoiraModernPlaybackQuality.ps1` only if strict materialize/validate orchestration is missing.
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Local only: `docs/qa/private/app-hosted/**`

**Interfaces:**
- Consumes: exact two-case private manifest and committed source revision.
- Produces: two App-hosted captured reports, normalized report-set, validation summary and model-readable comparison evidence.

- [ ] **Step 1: Run audio-switch App-hosted case**

Publish/register the complete App, execute the exact manifest case, export the report, and assert native-playback sample plus complete audio interaction evidence.

- [ ] **Step 2: Run PGS subtitle-switch App-hosted case**

Repeat with the exact subtitle case and require real rendered-video and cue deltas.

- [ ] **Step 3: Materialize and strictly validate both reports**

Run `materialize-native-harness-report-set` then `validate-report-set`; expected `2/2 matched`, no missing required signals. Startup failure may remain a quality failure, but structure/execution evidence must be valid.

- [ ] **Step 4: Run complete gates**

Run `tools/quality-run/run-playback-core-checks.ps1` and `tools/Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64`; expected exit code 0.

- [ ] **Step 5: Audit and commit**

Run `git diff --check`, tracked credential scan, and worktree status. Update Chinese docs with measured evidence and remaining risks, then commit without private artifacts.
