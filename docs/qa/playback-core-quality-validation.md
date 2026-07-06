# Playback Core Quality Validation

This document defines the App-free validation path for playback quality work.

Use this path when another worktree is actively changing Xbox UI or App interaction code. It validates playback-related Core and Native code without building or packaging the UWP App.

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

## Model-Facing Output

Playback quality reports are optimized for model/agent consumption:

- raw metrics remain available in `timing`, `sync`, `buffers`, `colorPipeline`, and `display`;
- `checks` contains structured threshold comparisons;
- `analysis.primaryFailureArea` identifies the first area to investigate;
- `PlaybackQualityReportComposer` is the App-free entry point that combines source, display, metrics, expected thresholds, evaluation, and model analysis in one call;
- `analysis.relevantSignals` names the exact report fields that triggered the conclusion;
- `PlaybackQualityReportAnalyzer` emits a model-facing analysis JSON with primary and secondary failure areas, failed check expected/actual values, evidence signals, missing evidence, and software-only limitations;
- `triageSteps` ranks blocker and failure investigation steps so automated model runs can decide whether to collect missing evidence or edit playback Core first;
- `sample` includes observed/minimum sample duration and additional required rendered frames when frame-rate evidence is available;
- `PlaybackQualityRunComparator` compares baseline and candidate reports after a Core change and classifies the run as improved, regressed, mixed, unchanged, or insufficient evidence, including baseline/candidate run IDs, comparability checks, matched/unmatched signal coverage, unmatched candidate failures, no-matching-signal evidence gaps, and a machine-readable keep/reject/split/collect-evidence decision;
- `display.refreshRateHz` is treated as required evidence for diagnosing 23.976fps/24fps cadence issues and is exposed by native display status when HDMI mode data is available;
- `limitations` prevents the model from inferring hardware facts that pure software telemetry cannot prove.
