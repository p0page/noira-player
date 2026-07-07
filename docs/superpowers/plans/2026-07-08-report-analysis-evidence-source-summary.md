# Report Analysis Evidence Source Summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add machine-readable evidence source and limitation aggregation to `analyze-report-set` summaries.

**Architecture:** Keep single-report evidence in existing `PlaybackQualityRunResult.report` fields. The CLI report-set summary will aggregate distinct `report.runtimeMetrics.providerStatus` values and distinct `report.limitations[]` strings so models can tell `source-only`, `core-probe`, `native-winrt`, and native-harness skip evidence apart at collection level without expanding every report.

**Tech Stack:** C# / .NET 9 CLI, existing `NextGenEmby.PlaybackQuality.Cli`, existing PowerShell smoke tests, existing v0.1 baseline artifacts.

## Global Constraints

- Do not change playback behavior, thresholds, expected behavior, source selection, HDR/DV strategy, native graph behavior, or report-set pass/fail rules.
- Do not treat `core-probe` runtime metrics as proof of real native decode/render quality.
- Do not introduce a new evaluation framework or large dependency.
- Do not commit private Emby server URL, account, password, personal item IDs, or private paths.

---

### Task 1: Add Smoke Test Expectations

**Files:**
- Modify: `tools/quality-run/run-playback-quality-cli-smoke-test.ps1`

**Interfaces:**
- Consumes: JSON output from `analyze-report-set`
- Produces: failing smoke expectations for summary-level `evidenceSources` and `limitations`

- [x] **Step 1: Add source-only summary expectation**

After reading `$materializedBaselineAnalysis`, assert:

```powershell
if (-not ($materializedBaselineAnalysis.evidenceSources -contains 'source-only')) {
    throw 'Expected source-only analyze-report-set summary to expose source-only evidence source.'
}

if (-not ($materializedBaselineAnalysis.limitations -contains 'source-only: playback execution was not run by this command')) {
    throw 'Expected source-only analyze-report-set summary to expose source-only limitation.'
}
```

- [x] **Step 2: Add core-probe summary expectation**

After reading `$coreProbeAnalysis`, assert:

```powershell
if (-not ($coreProbeAnalysis.evidenceSources -contains 'core-probe:returned-snapshot')) {
    throw 'Expected core-probe analyze-report-set summary to expose core-probe evidence source.'
}

if (-not ($coreProbeAnalysis.limitations -contains 'core-probe: diagnostic backend does not decode media or verify display output')) {
    throw 'Expected core-probe analyze-report-set summary to expose core-probe limitation.'
}
```

- [x] **Step 3: Run smoke test and verify RED**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
```

Expected: fails because `ReportAnalysisSummary` does not yet output `evidenceSources` and `limitations`.

### Task 2: Aggregate Evidence Sources And Limitations

**Files:**
- Modify: `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`

**Interfaces:**
- Consumes: `PlaybackQualityReport.RuntimeMetrics.ProviderStatus` and `PlaybackQualityReport.Limitations`
- Produces: `ReportAnalysisSummary.EvidenceSources` and `ReportAnalysisSummary.Limitations`

- [x] **Step 1: Add summary properties**

Add these properties to `ReportAnalysisSummary`:

```csharp
public List<string> EvidenceSources { get; } = new List<string>();
public List<string> Limitations { get; } = new List<string>();
```

- [x] **Step 2: Aggregate per-report fields**

In `CreateReportAnalysisSummary`, before model-analysis null handling, add:

```csharp
AddReportAnalysisEvidenceSource(summary, envelope.Report);
CopyValues(envelope.Report.Limitations, summary.Limitations);
```

Add helper:

```csharp
private static void AddReportAnalysisEvidenceSource(
    ReportAnalysisSummary summary,
    PlaybackQualityReport report)
{
    var providerStatus = report.RuntimeMetrics.ProviderStatus;
    if (!string.IsNullOrWhiteSpace(providerStatus))
    {
        AddUnique(summary.EvidenceSources, providerStatus);
    }
}
```

- [x] **Step 3: Run smoke test and verify GREEN**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
```

Expected: smoke test passes.

### Task 3: Refresh Baselines And Docs

**Files:**
- Modify: `docs/qa/baselines/v0.1-source-only/report-analysis-summary.json`
- Modify: `docs/qa/baselines/v0.1-core-probe/report-analysis-summary.json`
- Modify: `docs/qa/baselines/v0.1-native-harness-skip/report-analysis-summary.json`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Modify: `docs/qa/software-playback-quality-metric-contract.md`

**Interfaces:**
- Consumes: updated CLI `analyze-report-set`
- Produces: versioned summary artifacts and docs describing summary-level evidence source semantics

- [x] **Step 1: Refresh analysis summaries**

Run:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-source-only\reports --output docs\qa\baselines\v0.1-source-only\report-analysis-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-core-probe\reports --output docs\qa\baselines\v0.1-core-probe\report-analysis-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-native-harness-skip\reports --output docs\qa\baselines\v0.1-native-harness-skip\report-analysis-summary.json
```

- [x] **Step 2: Update docs**

Document that `analyze-report-set.evidenceSources[]` aggregates runtime metrics provider identities and `limitations[]` aggregates report-level limitations. State that this is for model triage only and does not change playback behavior, pass/fail thresholds, or candidate acceptance rules.

### Task 4: Verification And Commit

**Files:**
- No additional production files.

**Interfaces:**
- Consumes: repository state after Tasks 1-3
- Produces: committed evidence-source summary contract

- [x] **Step 1: Run verification**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.ps1
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore
git diff --check
rg -n "<private-server-host>|<private-password>|<private-account>|<private-xbox-host>|<private-remote-code>" .
```

Expected: checks pass; secret scan has no matches for real private values.

- [x] **Step 2: Commit**

Run:

```powershell
git add docs tools
git commit -m "feat: summarize playback evidence sources"
```
