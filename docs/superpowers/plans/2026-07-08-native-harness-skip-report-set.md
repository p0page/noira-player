# Native Harness Skip Report Set Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a documented `materialize-native-harness-report-set` CLI path that emits standard skip reports for native playback cases until a real App-free native playback harness exists.

**Architecture:** Reuse `PlaybackQualityReferenceManifest`, `PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult`, report-set validation, and existing `MaterializedBaselineReportSet` summary shape. The command must not open media, run native playback, fabricate runtime metrics, or create a parallel evaluation framework; it records the current native-harness gap as machine-readable evidence.

**Tech Stack:** C# `tools/NextGenEmby.PlaybackQuality.Cli`, existing `NextGenEmby.Core.PlaybackQuality` report types, PowerShell smoke tests.

## Global Constraints

- Do not change playback behavior, source selection, native graph behavior, thresholds, expected behavior, or pass/fail rules.
- Do not emit fake timing, buffering, sync, display, or color metrics.
- Every generated report must be a `PlaybackQualityRunResult` envelope with manifest case metadata preserved.
- Generated reports must use `report.result = skip`, `skip.failureClass = insufficient instrumentation`, and `skip.failureArea = evidence-collection`.
- Keep private Emby credentials, private URLs, and local personal paths out of repository files and test fixtures.

---

### Task 1: Add CLI Smoke Test Coverage

**Files:**
- Modify: `tools/quality-run/run-playback-quality-cli-smoke-test.ps1`

**Interfaces:**
- Consumes: `playback-quality materialize-native-harness-report-set --manifest <manifest> --reports-dir <dir> --source-revision <revision> --player-core-version <version> --build-configuration <config> --output <summary.json>`
- Produces: a failing smoke test before command implementation.

- [x] **Step 1: Add a minimal reference manifest and output paths**

Add temp paths near the existing core-probe smoke setup:

```powershell
$nativeHarnessManifestPath = Join-Path $tempRoot 'native-harness-reference-manifest.json'
$nativeHarnessDir = Join-Path $tempRoot 'native-harness-report-set'
$nativeHarnessSummaryPath = Join-Path $tempRoot 'native-harness-summary.json'
$nativeHarnessValidationPath = Join-Path $tempRoot 'native-harness-validation.json'
```

- [x] **Step 2: Write the failing command assertion**

Create a one-case manifest and run:

```powershell
& dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- `
    materialize-native-harness-report-set `
    --manifest $nativeHarnessManifestPath `
    --reports-dir $nativeHarnessDir `
    --source-revision smoke-native-harness-revision `
    --player-core-version NextGenEmby.Core.Tests `
    --build-configuration Debug `
    --output $nativeHarnessSummaryPath
```

Expected RED: command exits non-zero with `Unknown command: materialize-native-harness-report-set`.

- [x] **Step 3: Assert generated report shape**

After implementation, the smoke test must assert:

```powershell
$nativeHarnessReport = Get-Content -Raw $nativeHarnessReportPath | ConvertFrom-Json
if ($nativeHarnessReport.report.result -ne 'skip' -or
    $nativeHarnessReport.report.skip.code -ne 'native-harness.not-implemented' -or
    $nativeHarnessReport.report.skip.failureClass -ne 'insufficient instrumentation' -or
    $nativeHarnessReport.report.skip.failureArea -ne 'evidence-collection' -or
    $nativeHarnessReport.caseMetadata.caseId -ne 'local/native-harness-sdr-smoke') {
    throw 'Expected native harness materialization to write a standard skip envelope.'
}
```

Also run `validate-report-set` against the generated reports and require `isValid = true`.

### Task 2: Implement CLI Command

**Files:**
- Modify: `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`

**Interfaces:**
- Consumes: same options as `materialize-core-probe-report-set`.
- Produces: one skip `PlaybackQualityRunResult` envelope per validated manifest case.

- [x] **Step 1: Add command dispatch and usage**

Add `materialize-native-harness-report-set` to `Main` and `WriteUsage`.

- [x] **Step 2: Reuse materialize report-set option parsing**

Use `ParseMaterializeReportSetOptions(args, "materialize-native-harness-report-set")`.

- [x] **Step 3: Add materializer**

Implement `MaterializeNativeHarnessReportSet(validation, options)`:

```csharp
var result = PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult(
    referenceCase,
    new PlaybackQualitySkip
    {
        Code = "native-harness.not-implemented",
        Reason = "Native playback harness is not implemented in this software-only evaluator yet.",
        Operation = "materialize-native-harness",
        FailureClass = "insufficient instrumentation",
        FailureArea = "evidence-collection",
        IsExpected = true,
        IsRetriable = true
    },
    output.Environment);
```

Add report and model-analysis limitations:

```csharp
"native-harness: native playback graph was not opened by this command"
"native-harness: report is a standard skip envelope for the missing real native collector"
```

- [x] **Step 4: Use native-harness environment**

Add `CreateNativeHarnessEnvironment` with default `CollectorVersion = materialize-native-harness-report-set-v1`.

### Task 3: Document and Verify

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Modify: `docs/qa/software-playback-quality-metric-contract.md`
- Modify: `docs/qa/playback-core-quality-validation.md`
- Modify: `docs/superpowers/plans/2026-07-08-native-harness-skip-report-set.md`

**Interfaces:**
- Consumes: implemented command.
- Produces: documented v0.1 evidence boundary for native harness.

- [x] **Step 1: Document command and boundary**

State that this command is not native playback evaluation yet. It makes the missing harness machine-readable and keeps the future command path stable.

- [x] **Step 2: Run verification**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQuality -v minimal
dotnet build tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -v minimal
rg -n "<private-host>|<private-password>|<private-user>|<private-device>" --glob '!docs/qa/private/**' --glob '!tools/quality-run/private/**' --glob '!*.local.json' --glob '!*.private.json' --glob '!*.secrets.json'
git diff --check
```

Expected: smoke test, unit tests, and CLI build pass; secret scan returns no repository secrets; diff check has no whitespace errors.

- [ ] **Step 3: Commit**

Run:

```powershell
git add tools\NextGenEmby.PlaybackQuality.Cli\Program.cs tools\quality-run\run-playback-quality-cli-smoke-test.ps1 docs\STATUS.md docs\DECISIONS.md docs\qa\software-playback-quality-metric-contract.md docs\qa\playback-core-quality-validation.md docs\superpowers\plans\2026-07-08-native-harness-skip-report-set.md
git commit -m "feat: report native harness evidence gap"
```
