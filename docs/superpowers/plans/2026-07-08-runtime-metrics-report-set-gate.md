# Runtime Metrics Report-Set Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make playable reference cases require explicit runtime metrics collection-state evidence in report-set validation.

**Architecture:** Reuse `PlaybackQualityRequiredSignalPolicy` as the single source of required report signals. Keep runtime metrics values separate from playback behavior: the gate only requires explicit collection status, provider identity, snapshot presence, and playback-sample presence; analyzer remains responsible for deciding whether unavailable or empty metrics block optimization.

**Tech Stack:** C# / .NET, xUnit, existing `NextGenEmby.Core.PlaybackQuality`, existing `quality-run` baseline commands.

## Global Constraints

- Do not change playback behavior, thresholds, expected behavior, source selection, HDR/DV strategy, or native graph behavior.
- Do not treat `HasPlaybackSample = false` as missing evidence when `runtimeMetrics.status` is explicit; false is valid evidence that the collector did not capture a playable sample.
- Do not require runtime metrics for `error-handling` cases or explicitly unsupported sources.
- Keep source-only baseline invalid only because it lacks real playback telemetry; explicit `runtimeMetrics.status = unavailable` must be accepted as present evidence and blocked later by analysis.
- No private Emby server URL, account, password, personal item IDs, or private paths in committed artifacts.

---

### Task 1: Add Required-Signal Tests For Runtime Metrics Collection State

**Files:**
- Modify: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`

**Interfaces:**
- Consumes: `PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(PlaybackQualityReferenceCase)`
- Produces: tests proving playable cases require `runtimeMetrics.status`, `runtimeMetrics.providerStatus`, `runtimeMetrics.hasSnapshot`, and `runtimeMetrics.hasPlaybackSample`

- [x] **Step 1: Write the failing required-signal test**

Add a test named `RequiredSignalPolicy_Requires_Runtime_Metrics_State_For_Playable_Cases`:

```csharp
[Fact]
public void RequiredSignalPolicy_Requires_Runtime_Metrics_State_For_Playable_Cases()
{
    var referenceCase = CreateCase(
        "runtime-metrics/playable-case",
        tier: 1,
        purpose: "sdr-smoke");

    var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

    Assert.Contains("runtimeMetrics.status", requiredSignals);
    Assert.Contains("runtimeMetrics.providerStatus", requiredSignals);
    Assert.Contains("runtimeMetrics.hasSnapshot", requiredSignals);
    Assert.Contains("runtimeMetrics.hasPlaybackSample", requiredSignals);
}
```

- [x] **Step 2: Write the failing report-set validation test**

Add a test named `ValidateReportSet_Rejects_Playable_Report_Missing_Runtime_Metrics_State`:

```csharp
[Fact]
public void ValidateReportSet_Rejects_Playable_Report_Missing_Runtime_Metrics_State()
{
    var referenceCase = CreateCase(
        "runtime-metrics/missing-state",
        tier: 1,
        purpose: "sdr-smoke");
    referenceCase.Expected.MaxStartupDurationMs = 5000;
    var manifest = new PlaybackQualityReferenceManifest();
    manifest.Cases.Add(referenceCase);

    var report = CreateReport(
        "runtime-metrics/missing-state",
        codec: "hevc",
        width: 3840,
        height: 2160,
        frameRate: 23.976,
        hdrKind: "Hdr10");
    PopulatePlayableRequiredSignalsExceptRuntimeMetrics(report);

    var validation = PlaybackQualityReferenceReportSetValidator.Validate(
        manifest,
        new[] { new PlaybackQualityReferenceReportSetEntry(report) });

    Assert.False(validation.IsValid);
    Assert.Contains(validation.Errors, error =>
        error.Code == "report.requiredSignal.missing" &&
        error.Signal == "runtimeMetrics.status" &&
        error.FailureClass == PlaybackQualityFailureClassification.InsufficientInstrumentation);
    Assert.Contains(validation.Errors, error =>
        error.Code == "report.requiredSignal.missing" &&
        error.Signal == "runtimeMetrics.providerStatus");
    Assert.Contains(validation.Errors, error =>
        error.Code == "report.requiredSignal.missing" &&
        error.Signal == "runtimeMetrics.hasSnapshot");
    Assert.Contains(validation.Errors, error =>
        error.Code == "report.requiredSignal.missing" &&
        error.Signal == "runtimeMetrics.hasPlaybackSample");
}
```

Add a private helper in the same test class if no existing helper covers every non-runtime required signal:

```csharp
private static void PopulatePlayableRequiredSignalsExceptRuntimeMetrics(PlaybackQualityReport report)
{
    report.Source.HasDirectStreamUrl = true;
    report.Source.DirectStreamProtocol = "https";
    report.Startup.StartupDurationMs = 1000;
    report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent { Operation = "load", Status = "completed" });
    report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent { Operation = "play", Status = "completed" });
    report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent { Operation = "pause", Status = "completed" });
    report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent { Operation = "resume", Status = "completed" });
    report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent { Operation = "stop", Status = "completed" });
}
```

- [x] **Step 3: Run focused tests to verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~PlaybackQualityReferenceManifestTests.RequiredSignalPolicy_Requires_Runtime_Metrics_State_For_Playable_Cases|FullyQualifiedName~PlaybackQualityReferenceManifestTests.ValidateReportSet_Rejects_Playable_Report_Missing_Runtime_Metrics_State"
```

Expected: tests fail because runtime metrics collection-state signals are not emitted as required signals for playable cases.

### Task 2: Implement Runtime Metrics Required Signals

**Files:**
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityRequiredSignalPolicy.cs`
- Modify: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`

**Interfaces:**
- Consumes: playable reference case after unsupported-source check
- Produces: required signals `runtimeMetrics.status`, `runtimeMetrics.providerStatus`, `runtimeMetrics.hasSnapshot`, `runtimeMetrics.hasPlaybackSample`

- [x] **Step 1: Add required signals after unsupported-source return**

In `PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals`, after `IsExpectedUnsupportedSource(expected)` returns false and before lifecycle signals, add:

```csharp
AddUnique(requiredSignals, "runtimeMetrics.status");
AddUnique(requiredSignals, "runtimeMetrics.providerStatus");
AddUnique(requiredSignals, "runtimeMetrics.hasSnapshot");
AddUnique(requiredSignals, "runtimeMetrics.hasPlaybackSample");
```

- [x] **Step 2: Keep false values valid evidence**

Do not change `HasReportSignal` for `runtimeMetrics.hasSnapshot` or `runtimeMetrics.hasPlaybackSample`: explicit non-`unknown` `RuntimeMetrics.Status` already means the boolean fields are present evidence.

- [x] **Step 3: Run focused tests to verify GREEN**

Run the same focused `dotnet test` command from Task 1.

Expected: both tests pass.

### Task 3: Refresh Baselines And Documentation

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Modify: `docs/qa/software-playback-quality-metric-contract.md`
- Modify: `docs/qa/baselines/v0.1-*`

**Interfaces:**
- Consumes: updated required-signal policy
- Produces: refreshed validation and analysis artifacts that show source-only/core-probe/native-harness-skip remain semantically correct

- [x] **Step 1: Refresh source-only baseline**

Run:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- materialize-baseline-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-source-only\reports --source-revision working-tree-runtime-metrics-gate-v0.1 --player-core-version NextGenEmby.Core --build-configuration Debug --output docs\qa\baselines\v0.1-source-only\materialized-baseline-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-source-only\reports --output docs\qa\baselines\v0.1-source-only\report-set-validation.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-source-only\reports --output docs\qa\baselines\v0.1-source-only\report-analysis-summary.json
```

Expected: validation exits non-zero because source-only still lacks real playback telemetry, but runtime metrics collection-state signals are present as explicit unavailable evidence.

- [x] **Step 2: Refresh core-probe baseline**

Run:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- materialize-core-probe-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-core-probe\reports --source-revision working-tree-runtime-metrics-gate-v0.1 --player-core-version NextGenEmby.Core --build-configuration Debug --output docs\qa\baselines\v0.1-core-probe\materialized-core-probe-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-core-probe\reports --output docs\qa\baselines\v0.1-core-probe\report-set-validation.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-core-probe\reports --output docs\qa\baselines\v0.1-core-probe\report-analysis-summary.json
```

Expected: validation remains valid, analysis remains `decision = no-change`.

- [x] **Step 3: Refresh native-harness-skip baseline**

Run:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- materialize-native-harness-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-native-harness-skip\reports --source-revision working-tree-runtime-metrics-gate-v0.1 --player-core-version NextGenEmby.Core --build-configuration Debug --output docs\qa\baselines\v0.1-native-harness-skip\materialized-native-harness-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-native-harness-skip\reports --output docs\qa\baselines\v0.1-native-harness-skip\report-set-validation.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-native-harness-skip\reports --output docs\qa\baselines\v0.1-native-harness-skip\report-analysis-summary.json
```

Expected: validation remains valid, analysis remains `collect-comparable-evidence`.

- [x] **Step 4: Update status and decisions**

Record that playable cases now require explicit runtime metrics collection-state evidence. State that unavailable/empty runtime metrics remains accepted as evidence of collector state but blocks optimization in analysis.

### Task 4: Verification And Commit

**Files:**
- No new production files beyond Task 2.

**Interfaces:**
- Consumes: repository working tree after Tasks 1-3
- Produces: committed runtime metrics report-set gate

- [x] **Step 1: Run verification**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore
git diff --check
rg -n "<private-server-host>|<private-password>|<private-account>|<private-xbox-host>|<private-remote-code>" .
```

Expected: checks pass; secret scan has no matches.

- [ ] **Step 2: Commit**

Run:

```powershell
git add docs src tests tools
git commit -m "feat: require runtime metrics collection state"
```
