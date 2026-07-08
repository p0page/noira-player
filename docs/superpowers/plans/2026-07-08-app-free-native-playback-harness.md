# App-Free Native Playback Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first App-free path that can turn real native/software playback evidence into the existing `PlaybackQualityRunResult` report-set flow without launching, packaging, or deploying the UWP App.

**Architecture:** Keep the evaluator contract in `NextGenEmby.Core` and the report-set commands in `tools/NextGenEmby.PlaybackQuality.Cli`. First teach analysis to recognize App-free native evidence provider identities such as `native-headless` and `native-win32-harness`; then add a minimal harness producer that writes captured reports using the existing `PlaybackQualityRuntimeEvidenceCollector`. Native render-surface decoupling is staged behind a small interface so UWP `SwapChainPanel`, Win32 `HWND`, and headless collection can diverge without rewriting `PlaybackGraph`.

**Tech Stack:** C# / .NET 9 for report-set and harness orchestration, existing `NextGenEmby.Core.PlaybackQuality` DTOs, existing C++ native media components for the eventual headless/Win32 producer, PowerShell smoke tests, JSON report-set validation.

## Global Constraints

- Do not use UWP App startup, packaging, installation, or LocalState export as the primary path.
- Do not create a parallel evaluation framework; all outputs must stay consumable by `materialize-native-harness-report-set`, `validate-report-set`, and `analyze-report-set`.
- Do not fabricate playback metrics. Missing measurements must be represented as `missing`, `unsupported`, `skip`, or `insufficient instrumentation`.
- Do not treat `source-only`, `core-probe`, or external media-tool evidence as complete native playback evidence.
- Private Emby URLs, credentials, item ids, media source ids, tokens, and local personal paths must not be committed.
- The first runnable harness may be headless and may honestly report display/color limitations; it must not claim HDMI, panel EOTF, or human-visible HDR correctness.

---

### Task 1: Recognize App-Free Native Evidence Sources

**Files:**
- Modify: `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`
- Modify: `tools/quality-run/run-playback-quality-cli-smoke-test.ps1`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Consumes: existing `analyze-report-set` aggregation, `runtimeMetrics.providerStatus`, and `playbackEvidence`.
- Produces: a centralized provider classification that treats `native-winrt:*`, `native-headless:*`, and `native-win32-harness:*` as native/software playback evidence while preserving limitations.

- [x] **Step 1: Write the failing smoke assertion**

Add a captured-report fixture inside `tools/quality-run/run-playback-quality-cli-smoke-test.ps1` that imports one report with:

```json
"runtimeMetrics": {
  "status": "captured",
  "providerStatus": "native-headless:returned-snapshot",
  "reason": "",
  "hasSnapshot": true,
  "hasPlaybackSample": true
}
```

Then run:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir $nativeHeadlessImportedDir --output $nativeHeadlessAnalysisPath
```

Assert:

```powershell
$nativeHeadlessAnalysis.playbackEvidence.scope -eq 'native-software'
$nativeHeadlessAnalysis.playbackEvidence.canEvaluateNativePlayback -eq $true
$nativeHeadlessAnalysis.evidenceSources -contains 'native-headless:returned-snapshot'
```

- [x] **Step 2: Run smoke and verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
```

Expected: FAIL because `native-headless:returned-snapshot` is not yet recognized as native/software playback evidence.

- [x] **Step 3: Implement provider classification**

In `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`, replace the single `native-winrt:` check in playback evidence summarization with:

```csharp
private static bool HasNativeSoftwarePlaybackEvidence(PlaybackQualityReportSetAnalysisSummary summary)
{
    return HasEvidenceSourcePrefix(summary, "native-winrt:") ||
        HasEvidenceSourcePrefix(summary, "native-headless:") ||
        HasEvidenceSourcePrefix(summary, "native-win32-harness:");
}
```

Use this helper when setting `summary.PlaybackEvidence.Scope = "native-software"`.

- [x] **Step 4: Run smoke and verify GREEN**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
```

Expected: PASS.

- [x] **Step 5: Document boundary**

Update `docs/STATUS.md` and `docs/DECISIONS.md`:

```text
App-free native evidence provider identities now include native-headless and native-win32-harness. This changes evidence classification only; it does not by itself open playback or prove display/HDR output.
```

- [x] **Step 6: Commit**

```powershell
git add tools\NextGenEmby.PlaybackQuality.Cli\Program.cs tools\quality-run\run-playback-quality-cli-smoke-test.ps1 docs\STATUS.md docs\DECISIONS.md docs\superpowers\plans\2026-07-08-app-free-native-playback-harness.md
git commit -m "feat: recognize app-free native playback evidence"
```

### Task 2: Add a Minimal App-Free Captured Report Producer

**Files:**
- Create: `tools/NextGenEmby.PlaybackQuality.Headless/NextGenEmby.PlaybackQuality.Headless.csproj`
- Create: `tools/NextGenEmby.PlaybackQuality.Headless/Program.cs`
- Modify: `NextGenXboxEmby.sln`
- Modify: `tools/quality-run/run-playback-core-checks.ps1`
- Create: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`

**Interfaces:**
- Consumes: `PlaybackQualityReferenceManifest.Load`, `PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult`, `PlaybackQualityCapturedReportPath.GetReportRelativePath`.
- Produces: `quality-run/captured/<case-id>.json` files with real `runtimeMetrics.providerStatus = native-headless:returned-snapshot` when native open succeeds, or a structured error/skip result when App-free native open is still blocked.

- [ ] **Step 1: Write failing smoke test**

Create `tools/quality-run/run-native-headless-harness-smoke-test.ps1` that:

```powershell
$sampleUrl = 'https://repo.jellyfin.org/test-videos/SDR/HEVC%2010bit/Test%20Jellyfin%201080p%20HEVC%2010bit%203M.mp4'
dotnet run --project tools\NextGenEmby.PlaybackQuality.Headless\NextGenEmby.PlaybackQuality.Headless.csproj -- `
  --case-id jellyfin/sdr-hevc-main10-1080p60-3m `
  --stream-url $sampleUrl `
  --duration-seconds 5 `
  --reports-dir $capturedDir
```

Assert that `$capturedDir\jellyfin\sdr-hevc-main10-1080p60-3m.json` exists and contains:

```powershell
$report.report.result -in @('skip', 'error', 'pass')
$report.report.environment.collectorVersion -eq 'native-headless-harness-v0.1'
$report.caseMetadata.caseId -eq 'jellyfin/sdr-hevc-main10-1080p60-3m'
```

- [ ] **Step 2: Run smoke and verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-native-headless-harness-smoke-test.ps1
```

Expected: FAIL because `tools\NextGenEmby.PlaybackQuality.Headless` does not exist.

- [ ] **Step 3: Add the headless project scaffold**

Create `tools/NextGenEmby.PlaybackQuality.Headless/NextGenEmby.PlaybackQuality.Headless.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\NextGenEmby.Core\NextGenEmby.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Implement the minimal command shape without fake playback metrics**

In `Program.cs`, parse `--case-id`, `--stream-url`, `--duration-seconds`, and `--reports-dir`; create a `PlaybackQualityReferenceCase` from the inputs; write a captured `PlaybackQualityRunResult` using `PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult` until real native open is wired:

```csharp
new PlaybackQualitySkip
{
    Code = "native-headless.not-implemented",
    Reason = "App-free native headless playback has a command shape, but real native open is not wired yet.",
    Operation = "native-headless-open",
    FailureClass = "insufficient instrumentation",
    FailureArea = "evidence-collection",
    IsExpected = false,
    IsRetriable = true
}
```

Do not emit `runtimeMetrics.status = captured` or `native-headless:returned-snapshot` until real native/software playback has opened the sample.

The first implementation must clearly add a report limitation:

```text
native-headless: command shape exists, but real App-free native open is not wired yet
```

- [ ] **Step 5: Run smoke and verify GREEN**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-native-headless-harness-smoke-test.ps1
```

Expected: PASS, with a structured `skip` report. This is not native playback evidence yet.

- [ ] **Step 6: Commit**

```powershell
git add tools\NextGenEmby.PlaybackQuality.Headless tools\quality-run\run-native-headless-harness-smoke-test.ps1 NextGenXboxEmby.sln
git commit -m "feat: add app-free headless playback report producer"
```

### Task 3: Replace Contract Producer With Real Native/Open Evidence

**Files:**
- Modify: `tools/NextGenEmby.PlaybackQuality.Headless/Program.cs`
- Modify: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- Modify: `docs/qa/playback-core-quality-validation.md`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Consumes: Task 2 command shape and report output path.
- Produces: a harness run that really opens the sample and reports either decoded software evidence or a structured `insufficient instrumentation` skip/error if native decode is unavailable.

- [ ] **Step 1: Write failing assertion for real-open evidence**

Update smoke to reject the Task 2 limitation:

```powershell
if ($report.report.limitations -contains 'native-headless: initial producer is a report-path contract harness and does not yet decode through NextGenEmby.Native') {
    throw 'Headless harness must now produce real native/software open evidence.'
}
```

Assert one of:

```powershell
$report.report.runtimeMetrics.providerStatus -eq 'native-headless:returned-snapshot'
$report.report.error.failureClass -eq 'insufficient instrumentation'
```

- [ ] **Step 2: Run smoke and verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-native-headless-harness-smoke-test.ps1
```

Expected: FAIL because Task 2 still emits the contract limitation.

- [ ] **Step 3: Implement the smallest real-open path**

Prefer project-native code. If direct `NextGenEmby.Native` reuse is blocked by Windows Store/AppContainer linkage, add a structured `skip` with:

```csharp
Code = "native-headless.native-link-blocked",
FailureClass = "insufficient instrumentation",
FailureArea = "evidence-collection",
Reason = "Current NextGenEmby.Native build is a Windows Store C++/WinRT component bound to SwapChainPanel; App-free headless native decode requires render-surface/linkage decoupling."
```

Do not silently fall back to external `ffmpeg` as player evidence.

- [ ] **Step 4: Document the actual result**

If real-open succeeds, document the command and captured report-set. If it is blocked by native linkage, document the exact blocker and the next render-surface decoupling task:

```text
PlaybackGraph currently constructs VideoRenderer and SubtitleRenderer with DxDeviceResources, and DxDeviceResources owns SwapChainPanel attachment. Headless native decode needs a render target abstraction before the same graph can run outside UWP.
```

- [ ] **Step 5: Run verification**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQuality -v minimal
dotnet build tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-native-headless-harness-smoke-test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
git diff --check
```

- [ ] **Step 6: Commit**

```powershell
git add tools\NextGenEmby.PlaybackQuality.Headless tools\quality-run\run-native-headless-harness-smoke-test.ps1 docs\qa\playback-core-quality-validation.md docs\STATUS.md docs\DECISIONS.md
git commit -m "feat: probe app-free native playback evidence"
```

## Self-Review

- Spec coverage: The plan keeps the current evaluator framework, removes UWP App startup from the main path, introduces App-free provider identities, and stages the real native/open evidence blocker honestly.
- Placeholder scan: No `TBD`/`TODO` placeholders remain; Task 3 explicitly handles the likely native linkage blocker as structured evidence instead of pretending success.
- Type consistency: Provider identities are consistent across smoke tests, analyzer classification, and report producer: `native-headless` and `native-win32-harness`.
