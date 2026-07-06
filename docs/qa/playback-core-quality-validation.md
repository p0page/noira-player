# Playback Core Quality Validation

This document defines the App-free validation path for playback quality work.

Use this path when another worktree is actively changing Xbox UI or App interaction code. It validates playback-related Core and Native code without building or packaging the UWP App.

Reference media sources and suggested case tiers are tracked in [playback-quality-reference-corpus.md](playback-quality-reference-corpus.md).

## Command

```powershell
tools\quality-run\run-playback-core-checks.ps1
```

## Scope

The command emits `scope = playback-core`, plus `includedRoots` and `excludedRoots` fields so automated model runs can verify that the run is isolated from App interaction work.

Before running tests or builds, the command also runs an App diff guard. The guard fails if the current worktree, index, or playback-quality branch diff contains changes under `src/NextGenEmby.App`. This keeps playback Core evaluation independent from parallel Xbox UI/App worktrees.

The command validates:

- playback-core validation plan structure, including the invariant that App/MSIX build steps are excluded;
- playback-specific Core tests selected by `coreTestFilter`, including playback quality DTOs, report composer, evaluator, analyzer, command parsing, playback policies, backend diagnostics, stream-launch decisions, and Emby playback progress/session behavior;
- App-free playback quality comparison CLI build and JSON smoke test;
- Core refresh-rate cadence policy tests that mirror the native Xbox display-mode selection ratios;
- standalone native playback quality metrics helper;
- standalone native display refresh cadence policy helper;
- standalone native display refresh snapshot normalization helper;
- native playback component build.

The command deliberately excludes:

- `NextGenEmby.App.csproj`;
- XAML interaction work;
- unrelated Core interaction/focus policy tests;
- App package generation;
- MSIX packaging.

Use an App package build only when validating Xbox integration or a change that directly touches App/XAML behavior.

## Validate Reference Manifest

Use the App-free CLI to validate a playback reference corpus manifest before using it in automated runs:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-manifest --manifest docs\qa\playback-quality-reference-manifest.example.json --output manifest-validation.json
```

The command emits `isValid`, `caseCount`, `tiers`, `purposes`, and structured `errors`. Invalid manifests return a non-zero exit code so automation can stop before collecting misleading playback evidence.

## Compare Reports

Use the App-free CLI when an automated model run needs to compare two serialized playback quality reports without building the Xbox App:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- compare --baseline baseline.json --candidate candidate.json --output comparison.json
```

The `compare` and `compare-suite` commands accept either a raw `PlaybackQualityReport` JSON file or a `PlaybackQualityRunResult` envelope with a top-level `report` property.

For iterative optimization loops, pass previous comparison JSON files to enable repeated-unchanged stall protection:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- compare --baseline baseline.json --candidate candidate.json --previous previous-comparison.json --stall-threshold 2 --output comparison.json
```

When a candidate Core change is validated across multiple samples, summarize all comparison JSON files before deciding whether to keep the change:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- summarize --comparison comparison-a.json --comparison comparison-b.json --output suite.json
```

If an automated run already has baseline and candidate report directories, compare the matching report files and produce the suite in one command:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- compare-suite --baseline-dir baseline-reports --candidate-dir candidate-reports --comparisons-dir comparisons --output suite.json
```

`compare-suite` matches reports by relative `*.json` path. Missing or extra files fail the command so the model does not optimize from an incomplete sample set. `--comparisons-dir` is optional and writes each individual comparison using the same relative path as the report. `--previous-comparisons-dir` may point at a previous comparison directory with the same relative paths so repeated-unchanged stall protection works in batch runs; missing previous files for newly added cases are allowed. Generated comparisons include `caseId = <relative report path>`, and the suite emits a compact `cases` list so model loops can locate the exact sample behind a suite-level action.

The suite summary is conservative: any regression blocks acceptance, weak evidence requires more comparable reports, and partial evidence requires unmatched-signal review.

## Model-Facing Output

Playback quality reports are optimized for model/agent consumption:

- raw metrics remain available in `timing`, `sync`, `buffers`, `colorPipeline`, and `display`;
- `checks` contains structured threshold comparisons;
- `analysis.primaryFailureArea` identifies the first area to investigate;
- `PlaybackQualityReportComposer` is the App-free entry point that combines source, display, metrics, expected thresholds, evaluation, and model analysis in one call;
- `modelAnalysis.cadence` exposes the source/display refresh relationship, nearest 1x/2x/2.5x target, Hz delta, and tolerance used for frame cadence diagnosis;
- `analysis.relevantSignals` names the exact report fields that triggered the conclusion;
- `PlaybackQualityReportAnalyzer` emits a model-facing analysis JSON with primary and secondary failure areas, failed check expected/actual values, evidence signals, missing evidence, and software-only limitations;
- `triageSteps` ranks blocker and failure investigation steps so automated model runs can decide whether to collect missing evidence or edit playback Core first;
- `sample` includes observed/minimum sample duration and additional required rendered frames when frame-rate evidence is available;
- `PlaybackQualityRunComparator` compares baseline and candidate reports after a Core change and classifies the run as improved, regressed, mixed, unchanged, or insufficient evidence, including baseline/candidate run IDs, comparability checks, strong/partial/weak confidence, direct optimization action/risk, optional repeated-unchanged stall protection, matched/unmatched signal coverage, unmatched candidate failures, no-matching-signal evidence gaps, and a machine-readable keep/reject/split/collect-evidence decision;
- `display.refreshRateHz` is treated as required evidence for diagnosing 23.976fps/24fps cadence issues and is exposed by native display status when HDMI mode data is available;
- `limitations` prevents the model from inferring hardware facts that pure software telemetry cannot prove.
