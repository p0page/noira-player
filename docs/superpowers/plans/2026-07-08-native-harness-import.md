# Native Harness Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the existing playback-quality CLI so native/App-hosted playback evidence can be imported into the current `PlaybackQualityRunResult` report-set path instead of being trapped outside the evaluator.

**Architecture:** Keep `materialize-native-harness-report-set` as the single native harness command. With no captured input it continues to produce explicit skip envelopes; with `--captured-reports-dir` it reads per-case raw reports or envelopes, refreshes model analysis with the current analyzer, stamps/merges run environment metadata, and writes the normalized report-set to `--reports-dir`.

**Tech Stack:** C# / .NET 9 CLI, existing `NextGenEmby.Core.PlaybackQuality` DTOs, PowerShell smoke test, JSON report-set validation.

## Global Constraints

- Do not create a parallel evaluation framework.
- Do not open native playback graph in this CLI change.
- Do not fabricate runtime metrics, timing, buffering, A/V sync, display, or color evidence.
- Missing captured reports must be explicit skip evidence, not missing files in the report-set.
- Private Emby URLs, credentials, item ids, media source ids, and local private report paths must not be committed.
- Any imported evidence must remain analyzable by existing `validate-report-set`, `analyze-report-set`, `compare-suite`, and `evaluate-candidate`.

---

### Task 1: Add Captured Native Report Import Mode

**Files:**
- Modify: `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`
- Test: `tools/quality-run/run-playback-quality-cli-smoke-test.ps1`

**Interfaces:**
- Consumes: existing `ReadPlaybackQualityReportEnvelope(string path, string fallbackRunId)`, `AnalyzeReport(PlaybackQualityReportEnvelope envelope)`, `PlaybackQualityRunResult`, `PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult`.
- Produces: `materialize-native-harness-report-set --captured-reports-dir <dir>`, where reports are found using the same relative path as `GetRunIdComparisonRelativePath(caseId)`.

- [ ] **Step 1: Write failing smoke coverage**

Add a smoke case that:

```powershell
dotnet $cliDll `
    materialize-native-harness-report-set `
    --manifest $nativeHarnessManifestPath `
    --captured-reports-dir $nativeHarnessCapturedDir `
    --reports-dir $nativeHarnessImportedDir `
    --source-revision smoke-native-harness-import-revision `
    --player-core-version smoke-core `
    --build-configuration Debug `
    --output $nativeHarnessImportedSummaryPath
```

Expected assertions:

```powershell
$importedReport.report.result -eq 'pass'
$importedReport.report.environment.sourceRevision -eq 'smoke-native-harness-import-revision'
$importedReport.report.runtimeMetrics.providerStatus -eq 'native-winrt:returned-snapshot'
$importedReport.modelAnalysis.evidenceSignals -contains 'runtimeMetrics.providerStatus'
$importedReport.modelAnalysis.evidenceSignals -contains 'timing.renderedVideoFrames'
$importedSummary.limitations -contains 'native-harness: imported captured playback evidence; CLI did not open native playback graph'
```

- [ ] **Step 2: Run smoke and verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
```

Expected: FAIL with unknown option `--captured-reports-dir`.

- [ ] **Step 3: Implement option parsing**

Add `CapturedReportsDirectory` to the existing materialize report-set options type and parse:

```csharp
case "--captured-reports-dir":
    options.CapturedReportsDirectory = ReadValue(args, ref index, arg);
    break;
```

- [ ] **Step 4: Implement import behavior**

In `MaterializeNativeHarnessReportSet`, when `CapturedReportsDirectory` is non-empty:

```csharp
var imported = TryReadCapturedNativeHarnessResult(
    options.CapturedReportsDirectory,
    referenceCase,
    output.Environment,
    out var result);
```

If found, write the normalized `PlaybackQualityRunResult`. If missing, write `ComposeSkipRunResult` with:

```csharp
Code = "native-harness.capture-missing",
Reason = "Native harness captured report was not found for this manifest case.",
Operation = "materialize-native-harness-import",
FailureClass = "insufficient instrumentation",
FailureArea = "evidence-collection",
IsExpected = false,
IsRetriable = true
```

- [ ] **Step 5: Run smoke and verify it passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
```

Expected: `playback-quality-cli smoke ok`.

- [ ] **Step 6: Commit**

```powershell
git add tools/NextGenEmby.PlaybackQuality.Cli/Program.cs tools/quality-run/run-playback-quality-cli-smoke-test.ps1 docs/superpowers/plans/2026-07-08-native-harness-import.md
git commit -m "feat: import native harness playback reports"
```

### Task 2: Document Import Boundary

**Files:**
- Modify: `docs/qa/playback-core-quality-validation.md`
- Modify: `docs/qa/software-playback-quality-metric-contract.md`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Consumes: Task 1 CLI behavior.
- Produces: documented command and explicit boundary that import mode normalizes evidence but does not itself execute playback.

- [ ] **Step 1: Document command usage**

Add the import form:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- materialize-native-harness-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --captured-reports-dir docs\qa\private\native-captured.local --reports-dir docs\qa\private\native-normalized.local --source-revision <git-sha-or-working-tree-id> --player-core-version <core-version> --build-configuration Debug --output docs\qa\private\native-normalized-summary.local.json
```

- [ ] **Step 2: Document boundary**

State that import mode:

```text
does not open native playback graph;
does not validate HDMI/display output;
does not fabricate missing metrics;
converts missing captured reports to explicit skip envelopes;
refreshes modelAnalysis with the current evaluator.
```

- [ ] **Step 3: Run focused validation**

Run:

```powershell
dotnet build tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
rg -n "<private-server>|<private-password>|<private-user>|<private-device-address>" .
```

Expected: build pass, smoke pass, secret scan no matches.

- [ ] **Step 4: Commit**

```powershell
git add docs/qa/playback-core-quality-validation.md docs/qa/software-playback-quality-metric-contract.md docs/STATUS.md docs/DECISIONS.md
git commit -m "docs: document native harness report import"
```

## Self-Review

- Spec coverage: This plan advances the v0.1 goal by making real playback evidence consumable by the existing report-set path, while keeping the current skip behavior for absent native collector output.
- Placeholder scan: No placeholders or unbounded implementation tasks remain.
- Type consistency: The new CLI option is scoped to the existing materialize report-set options and uses existing report envelope/materialization types.
