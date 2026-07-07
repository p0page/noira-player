# Report Analysis Playback Evidence Summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an explicit `playbackEvidence` summary to `analyze-report-set` output so automated models can distinguish source-only, core-probe, skip-only, and real native/App software playback evidence.

**Architecture:** Reuse the `ReportAnalysisSummary` built by the existing CLI. Derive `playbackEvidence` from already aggregated `evidenceSources[]`, `limitations[]`, `skippedReportCount`, and report counts; do not create a parallel evaluator and do not alter existing decisions, thresholds, or pass/fail rules.

**Tech Stack:** C# / .NET 9 CLI, existing `NextGenEmby.PlaybackQuality.Cli`, existing PowerShell smoke tests, existing v0.1 baseline artifacts.

## Global Constraints

- Do not change playback behavior, native graph behavior, thresholds, expected behavior, report-set validation, or candidate acceptance rules.
- Do not treat `core-probe` metrics as proof of native decode/render, HDMI output, real frame pacing, color correctness, or A/V sync.
- Treat `native-winrt:*` provider evidence as native/App software playback evidence only for fields present in the report; it still does not prove external display correctness.
- Do not introduce a new evaluation framework or large dependency.
- Do not commit private Emby server URL, account, password, personal item IDs, or private paths.

---

### Task 1: Add Smoke Test Expectations

**Files:**
- Modify: `tools/quality-run/run-playback-quality-cli-smoke-test.ps1`

**Interfaces:**
- Consumes: JSON output from `analyze-report-set`
- Produces: failing expectations for `playbackEvidence.scope`, `status`, and booleans

- [x] **Step 1: Add source-only playback evidence expectation**

After `$materializedBaselineAnalysis` is loaded, assert:

```powershell
if ($materializedBaselineAnalysis.playbackEvidence.scope -ne 'source-only' -or
    $materializedBaselineAnalysis.playbackEvidence.status -ne 'missing' -or
    $materializedBaselineAnalysis.playbackEvidence.canEvaluateNativePlayback -ne $false -or
    $materializedBaselineAnalysis.playbackEvidence.canEvaluateOrchestration -ne $false) {
    throw 'Expected source-only analyze-report-set summary to mark playback evidence as missing source-only evidence.'
}
```

- [x] **Step 2: Add core-probe playback evidence expectation**

After `$coreProbeAnalysis` is loaded, assert:

```powershell
if ($coreProbeAnalysis.playbackEvidence.scope -ne 'orchestration-only' -or
    $coreProbeAnalysis.playbackEvidence.status -ne 'limited' -or
    $coreProbeAnalysis.playbackEvidence.canEvaluateNativePlayback -ne $false -or
    $coreProbeAnalysis.playbackEvidence.canEvaluateOrchestration -ne $true) {
    throw 'Expected core-probe analyze-report-set summary to mark playback evidence as orchestration-only.'
}
```

- [x] **Step 3: Add native-winrt imported playback evidence expectation**

After validating `$nativeHarnessImportedDir`, run `analyze-report-set` to `$nativeHarnessImportedAnalysisPath` and assert:

```powershell
if ($nativeHarnessImportedAnalysis.playbackEvidence.scope -ne 'native-software' -or
    $nativeHarnessImportedAnalysis.playbackEvidence.status -ne 'available' -or
    $nativeHarnessImportedAnalysis.playbackEvidence.canEvaluateNativePlayback -ne $true) {
    throw 'Expected imported native harness analyze-report-set summary to mark native software playback evidence as available.'
}
```

- [x] **Step 4: Run smoke test and verify RED**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
```

Expected: fails because `playbackEvidence` does not exist yet.

### Task 2: Implement Playback Evidence Summary

**Files:**
- Modify: `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`

**Interfaces:**
- Consumes: `ReportAnalysisSummary.EvidenceSources`, `ReportAnalysisSummary.Limitations`, `TotalReportCount`, `SkippedReportCount`
- Produces: `ReportAnalysisSummary.PlaybackEvidence`

- [x] **Step 1: Add DTO**

Add:

```csharp
private sealed class ReportAnalysisPlaybackEvidence
{
    public string Scope { get; set; } = "none";
    public string Status { get; set; } = "missing";
    public bool CanEvaluateNativePlayback { get; set; }
    public bool CanEvaluateOrchestration { get; set; }
    public List<string> Reasons { get; } = new List<string>();
}
```

Add property to `ReportAnalysisSummary`:

```csharp
public ReportAnalysisPlaybackEvidence PlaybackEvidence { get; set; } =
    new ReportAnalysisPlaybackEvidence();
```

- [x] **Step 2: Add derivation helper**

Add helper:

```csharp
private static void AddReportAnalysisPlaybackEvidence(ReportAnalysisSummary summary)
{
    var hasNative = HasEvidenceSourcePrefix(summary, "native-winrt:");
    var hasCoreProbe = HasEvidenceSourcePrefix(summary, "core-probe:");
    var hasSourceOnly = summary.EvidenceSources.Contains("source-only");

    if (hasNative && !hasCoreProbe && !hasSourceOnly && summary.SkippedReportCount == 0)
    {
        summary.PlaybackEvidence.Scope = "native-software";
        summary.PlaybackEvidence.Status = "available";
        summary.PlaybackEvidence.CanEvaluateNativePlayback = true;
        summary.PlaybackEvidence.CanEvaluateOrchestration = true;
        AddUnique(summary.PlaybackEvidence.Reasons, "native-winrt runtime metrics provider evidence is present");
        return;
    }

    if (hasCoreProbe && !hasNative)
    {
        summary.PlaybackEvidence.Scope = "orchestration-only";
        summary.PlaybackEvidence.Status = "limited";
        summary.PlaybackEvidence.CanEvaluateNativePlayback = false;
        summary.PlaybackEvidence.CanEvaluateOrchestration = true;
        AddUnique(summary.PlaybackEvidence.Reasons, "core-probe evidence does not open the native playback graph");
        return;
    }

    if (hasSourceOnly && !hasNative && !hasCoreProbe)
    {
        summary.PlaybackEvidence.Scope = "source-only";
        summary.PlaybackEvidence.Status = "missing";
        AddUnique(summary.PlaybackEvidence.Reasons, "source-only reports do not execute playback");
        return;
    }

    if (summary.SkippedReportCount > 0 && summary.EvidenceSources.Count == 0)
    {
        summary.PlaybackEvidence.Scope = "none";
        summary.PlaybackEvidence.Status = "missing";
        AddUnique(summary.PlaybackEvidence.Reasons, "all playback evidence was skipped or not collected");
        return;
    }

    if (summary.EvidenceSources.Count > 1 || summary.SkippedReportCount > 0)
    {
        summary.PlaybackEvidence.Scope = "mixed";
        summary.PlaybackEvidence.Status = hasNative ? "partial" : "limited";
        summary.PlaybackEvidence.CanEvaluateNativePlayback = hasNative;
        summary.PlaybackEvidence.CanEvaluateOrchestration = hasNative || hasCoreProbe;
        AddUnique(summary.PlaybackEvidence.Reasons, "report set contains mixed playback evidence sources");
    }
}
```

Add prefix helper:

```csharp
private static bool HasEvidenceSourcePrefix(ReportAnalysisSummary summary, string prefix)
{
    foreach (var source in summary.EvidenceSources)
    {
        if (source.StartsWith(prefix, StringComparison.Ordinal))
        {
            return true;
        }
    }

    return false;
}
```

- [x] **Step 3: Call helper**

In `CreateReportAnalysisSummary`, after `AddReportAnalysisCapabilityCoverage(summary);`, call:

```csharp
AddReportAnalysisPlaybackEvidence(summary);
```

- [x] **Step 4: Run smoke test and verify GREEN**

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
- Produces: versioned summaries and docs describing `playbackEvidence`

- [x] **Step 1: Refresh analysis summaries**

Run:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-source-only\reports --output docs\qa\baselines\v0.1-source-only\report-analysis-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-core-probe\reports --output docs\qa\baselines\v0.1-core-probe\report-analysis-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-native-harness-skip\reports --output docs\qa\baselines\v0.1-native-harness-skip\report-analysis-summary.json
```

- [x] **Step 2: Update docs**

Document that `playbackEvidence` is a model-facing scope summary. State that it does not replace `decision`, `capabilityCoverage`, per-case evidence, or report limitations.

### Task 4: Verification And Commit

**Files:**
- No additional production files.

**Interfaces:**
- Consumes: repository state after Tasks 1-3
- Produces: committed playback evidence summary contract

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
git commit -m "feat: summarize playback evidence scope"
```
