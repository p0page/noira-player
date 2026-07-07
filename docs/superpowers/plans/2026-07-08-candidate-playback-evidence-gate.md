# Candidate Playback Evidence Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `evaluate-candidate` block before suite comparison when baseline or candidate reports do not contain native/App software playback evidence.

**Architecture:** Reuse `ReportAnalysisSummary.PlaybackEvidence` as the source of truth. Add two candidate evidence gates, `baseline-playback-evidence` and `candidate-playback-evidence`, after report-analysis gates and before suite comparison. This does not change `analyze-report-set`, report-set validation, thresholds, analyzer results, or playback behavior.

**Tech Stack:** C# / .NET 9 CLI, existing `NextGenEmby.PlaybackQuality.Cli`, existing PowerShell smoke tests, existing v0.1 docs/baselines.

## Global Constraints

- Do not change playback behavior, native graph behavior, thresholds, expected behavior, report-set validation, analyzer blockers, or comparison scoring.
- Do not treat `source-only` or `core-probe` evidence as enough for native playback candidate acceptance.
- Treat imported `native-winrt:*` evidence as native/App software playback evidence only for fields present in reports.
- Do not introduce a new evaluation framework or large dependency.
- Do not commit private Emby server URL, account, password, personal item IDs, or private paths.

---

### Task 1: Add RED Smoke Coverage

**Files:**
- Modify: `tools/quality-run/run-playback-quality-cli-smoke-test.ps1`

**Interfaces:**
- Consumes: `evaluate-candidate` output JSON
- Produces: failing smoke assertions for `baseline-playback-evidence` active gate when core-probe is used as candidate evidence

- [x] **Step 1: Add temp paths**

Add:

```powershell
$coreProbeCandidateEvaluationPath = Join-Path $tempRoot 'core-probe-candidate-evaluation.json'
$coreProbeCandidateEvaluationComparisonsDir = Join-Path $tempRoot 'core-probe-candidate-evaluation-comparisons'
$archivedCoreProbeReportsDir = Join-Path $repoRoot 'docs\qa\baselines\v0.1-core-probe\reports'
```

- [x] **Step 2: Add core-probe evaluate-candidate blocker assertion**

After `$coreProbeAnalysis` assertions, run:

```powershell
Push-Location $repoRoot
try {
    dotnet $cliDll `
        evaluate-candidate `
        --manifest $exampleManifestPath `
        --baseline-dir $archivedCoreProbeReportsDir `
        --candidate-dir $archivedCoreProbeReportsDir `
        --match-by run-id `
        --comparisons-dir $coreProbeCandidateEvaluationComparisonsDir `
        --output $coreProbeCandidateEvaluationPath
    if ($LASTEXITCODE -eq 0) {
        throw 'Expected core-probe candidate evaluation to reject non-native playback evidence.'
    }
}
finally {
    Pop-Location
}

$coreProbeCandidateEvaluation = Get-Content -Raw -LiteralPath $coreProbeCandidateEvaluationPath | ConvertFrom-Json
if ($coreProbeCandidateEvaluation.activeGate.name -ne 'baseline-playback-evidence' -or
    $coreProbeCandidateEvaluation.activeGate.status -ne 'blocked' -or
    -not ($coreProbeCandidateEvaluation.activeGate.blockers -contains 'baseline-playback-evidence.insufficient')) {
    throw 'Expected core-probe candidate evaluation to block at baseline playback evidence gate.'
}

if (Test-Path -LiteralPath $coreProbeCandidateEvaluationComparisonsDir) {
    throw 'Expected core-probe playback-evidence gate to skip comparison output.'
}
```

- [x] **Step 3: Update existing successful candidate fixtures**

For existing smoke fixtures that are expected to reach suite comparison and accept/reject candidates based on playback metrics, change runtime metrics provider status from `smoke-provider:returned-snapshot` to `native-winrt:returned-snapshot`.

- [x] **Step 4: Run smoke test and verify RED**

Run:

```powershell
dotnet build tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj --no-restore
powershell -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
```

Result: the first RED run failed at the expected candidate-evaluation path, then the narrow temp core-probe manifest was replaced with the archived full core-probe report-set so the test exercises the playback-evidence gate rather than manifest coverage.

### Task 2: Implement Playback Evidence Gates

**Files:**
- Modify: `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`

**Interfaces:**
- Consumes: `ReportAnalysisSummary.PlaybackEvidence`
- Produces: `CandidateEvaluationGate` entries named `baseline-playback-evidence` and `candidate-playback-evidence`

- [x] **Step 1: Create helper**

Add:

```csharp
private static CandidateEvaluationGate CreatePlaybackEvidenceGate(
    string name,
    ReportAnalysisSummary summary)
{
    var label = name.Replace("-", " ", StringComparison.Ordinal);
    var gate = new CandidateEvaluationGate
    {
        Name = name,
        Status = summary.PlaybackEvidence.CanEvaluateNativePlayback ? "pass" : "blocked",
        Action = summary.PlaybackEvidence.CanEvaluateNativePlayback
            ? "continue"
            : "collect-comparable-evidence",
        Summary = summary.PlaybackEvidence.CanEvaluateNativePlayback
            ? label + " has native/App software playback evidence"
            : label + " lacks native/App software playback evidence"
    };

    CopyValues(summary.EvidenceSources, gate.Signals);
    CopyValues(summary.Limitations, gate.SuggestedNextActions);
    CopyValues(summary.PlaybackEvidence.Reasons, gate.SuggestedNextActions);

    if (gate.Status == "blocked")
    {
        AddUnique(gate.Blockers, name + ".insufficient");
        PlaybackQualityCodeTargetCatalog.AddForFailureArea(
            gate.CodeTargets,
            "evidence-collection");
        MarkWeakGateConfidence(
            gate,
            label + " is not sufficient for native playback candidate comparison");
    }

    return gate;
}
```

- [x] **Step 2: Wire gates into `RunEvaluateCandidate`**

After report-analysis gates and before adding suite gate, create:

```csharp
var baselinePlaybackEvidenceGate = CreatePlaybackEvidenceGate(
    "baseline-playback-evidence",
    baselineReportAnalysis);
if (baselinePlaybackEvidenceGate.Status == "blocked")
{
    AddUnique(evaluation.Blockers, "baseline-playback-evidence.insufficient");
    CopyValues(baselinePlaybackEvidenceGate.Blockers, evaluation.Blockers);
}

var candidatePlaybackEvidenceGate = CreatePlaybackEvidenceGate(
    "candidate-playback-evidence",
    candidateReportAnalysis);
if (candidatePlaybackEvidenceGate.Status == "blocked")
{
    AddUnique(evaluation.Blockers, "candidate-playback-evidence.insufficient");
    CopyValues(candidatePlaybackEvidenceGate.Blockers, evaluation.Blockers);
}
```

Add both gates after `candidate-report-analysis` in `evaluation.EvidenceGates`.

- [x] **Step 3: Run smoke test and verify GREEN**

Run:

```powershell
dotnet build tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj --no-restore
powershell -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
```

Result: smoke test passed after adding the two playback evidence gates and updating successful fixtures to use `native-winrt:returned-snapshot`.

### Task 3: Update Docs

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Modify: `docs/qa/software-playback-quality-metric-contract.md`
- Modify: `docs/qa/playback-core-quality-validation.md`

**Interfaces:**
- Consumes: new evaluate-candidate evidence gate behavior
- Produces: docs stating source-only/core-probe evidence cannot pass native playback candidate gates

- [x] **Step 1: Document gate semantics**

State that `evaluate-candidate` now includes `baseline-playback-evidence` and `candidate-playback-evidence` gates. These gates require `playbackEvidence.canEvaluateNativePlayback = true` before suite comparison.

### Task 4: Verification And Commit

**Files:**
- No additional production files.

**Interfaces:**
- Consumes: repository state after Tasks 1-3
- Produces: committed playback-evidence candidate gate

- [x] **Step 1: Run verification**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.ps1
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore
git diff --check
rg -n "<private-server-host>|<private-password>|<private-account>|<private-xbox-host>|<private-remote-code>" .
```

Expected: checks pass; secret scan has no matches for real private values.

Result:

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.ps1` passed after the final formatting fix.
- `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore` passed: 548/548.
- `git diff --check` passed after removing a trailing blank line in the smoke script.
- Real private-value scan using the actual known host/password/account/code patterns returned no matches.

- [x] **Step 2: Commit**

Run:

```powershell
git add docs tools
git commit -m "feat: gate candidate evaluation on playback evidence"
```

Result: committed with message `feat: gate candidate evaluation on playback evidence`.
