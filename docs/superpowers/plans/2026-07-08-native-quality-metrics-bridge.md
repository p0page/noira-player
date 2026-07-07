# Native Quality Metrics Bridge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the existing WinRT native `QualityMetrics()` snapshot to the Core playback-quality report path without changing playback behavior.

**Architecture:** `NativePlaybackEngine.idl` already exposes `NativePlaybackQualityMetrics QualityMetrics()`. `NativeDirectXPlaybackBackend` already delegates to `IPlaybackQualityMetricsProvider` when the wrapped engine implements it. The smallest correct change is to make `WinRtNativePlaybackEngine` implement `IPlaybackQualityMetricsProvider` and map WinRT metrics into `PlaybackQualityMetricsSnapshot`.

**Tech Stack:** C# `NextGenEmby.App`, C# `NextGenEmby.Core`, existing xUnit Core/App source-contract tests.

## Global Constraints

- This is instrumentation/testability only; do not change playback decisions, thresholds, source selection, native graph behavior, or evaluation pass/fail rules.
- Keep the existing `PlaybackQuality` report contract and `quality-run` commands.
- Do not add dependencies.
- Do not commit private Emby credentials, private media URLs, or machine-local test paths.

---

### Task 1: Add WinRT Native Metrics Mapping

**Files:**
- Modify: `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`
- Test: `tests/NextGenEmby.Core.Tests/PlaybackQuality/NativeQualityMetricsBridgeContractTests.cs`

**Interfaces:**
- Consumes: `NextGenEmby.Native.NativePlaybackEngine.QualityMetrics()`
- Produces: `bool IPlaybackQualityMetricsProvider.TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics)`

- [x] **Step 1: Write the failing test**

Use a source-contract test because this repository currently has no dedicated App test project and `NextGenEmby.App` targets UWP. The test checks that `WinRtNativePlaybackEngine` implements `IPlaybackQualityMetricsProvider`, calls `_engine.QualityMetrics()`, and maps every `NativePlaybackQualityMetrics` field to the Core snapshot.

- [x] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~NativeQualityMetricsBridgeContractTests
```

Expected: FAIL because metrics provider support or mapping does not exist yet.

- [x] **Step 3: Implement the minimal bridge**

Change `WinRtNativePlaybackEngine` to implement `IPlaybackQualityMetricsProvider`, call `_engine.QualityMetrics()`, map all native fields to `PlaybackQualityMetricsSnapshot`, and return `false` only when native returns no object or throws.

- [x] **Step 4: Run narrow tests**

Run the same test command.

Expected: PASS.

- [x] **Step 5: Run playback-quality regression tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQuality
```

Expected: PASS.

### Task 2: Document Evidence Boundary

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Modify as needed: `docs/qa/software-playback-quality-metric-contract.md`

**Interfaces:**
- Consumes: the Task 1 bridge behavior.
- Produces: documentation that models can distinguish native graph metrics from deterministic probe telemetry.

- [x] **Step 1: Add decision note**

Record that WinRT native `QualityMetrics()` is now bridged into Core through `IPlaybackQualityMetricsProvider`.

- [x] **Step 2: Add status note**

Record that this enables real App/native playback sessions to feed runtime metrics into the report collector, but does not itself run a native playback harness or prove HDMI/display correctness.

- [x] **Step 3: Run docs-sensitive checks**

Run:

```powershell
rg -n "<private-host>|<private-password>|<private-user>|<private-device>" --glob '!docs/qa/private/**' --glob '!tools/quality-run/private/**' --glob '!*.local.json' --glob '!*.private.json' --glob '!*.secrets.json'
```

Expected: no output.

### Task 3: Verify and Commit

**Files:**
- Stage only files changed for this bridge.

- [x] **Step 1: Run full available quality gate**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1
```

Expected: PASS, or record the exact failing command and reason.

Result: `run-playback-core-checks.ps1` was not directly applicable because its App diff guard intentionally rejects all `src/NextGenEmby.App` changes, while this task is an App native-adapter instrumentation bridge. Equivalent available checks were run manually: Core playback tests, playback-quality tests, CLI build/smoke, manifest scripts, native software tests, native build, secret scan, and diff check. A full solution App build reached native build successfully but failed in the existing XAML compiler environment before generated `*.g.i.cs` files were available.

- [x] **Step 2: Inspect diff**

Run:

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors; only intentional files changed.

- [ ] **Step 3: Commit**

Run:

```powershell
git add src\NextGenEmby.App\Playback\WinRtNativePlaybackEngine.cs tests\NextGenEmby.Core.Tests\PlaybackQuality\NativeQualityMetricsBridgeContractTests.cs docs\STATUS.md docs\DECISIONS.md docs\qa\software-playback-quality-metric-contract.md docs\superpowers\plans\2026-07-08-native-quality-metrics-bridge.md
git commit -m "feat: bridge native playback quality metrics"
```
