# Playback Core Quality Validation

This document defines the App-free validation path for playback quality work.

Use this path when another worktree is actively changing Xbox UI or App interaction code. It validates playback-related Core and Native code without building or packaging the UWP App.

## Command

```powershell
tools\quality-run\run-playback-core-checks.ps1
```

## Scope

The command validates:

- Core playback quality DTOs, evaluator, command parsing, and playback policy tests;
- Core refresh-rate cadence policy tests that mirror the native Xbox display-mode selection ratios;
- standalone native playback quality metrics helper;
- native playback component build.

The command deliberately excludes:

- `NextGenEmby.App.csproj`;
- XAML interaction work;
- App package generation;
- MSIX packaging.

Use an App package build only when validating Xbox integration or a change that directly touches App/XAML behavior.

## Model-Facing Output

Playback quality reports are optimized for model/agent consumption:

- raw metrics remain available in `timing`, `sync`, `buffers`, `colorPipeline`, and `display`;
- `checks` contains structured threshold comparisons;
- `analysis.primaryFailureArea` identifies the first area to investigate;
- `analysis.relevantSignals` names the exact report fields that triggered the conclusion;
- `PlaybackQualityReportAnalyzer` emits a model-facing analysis JSON with primary and secondary failure areas, failed check expected/actual values, evidence signals, missing evidence, and software-only limitations;
- `display.refreshRateHz` is treated as required evidence for diagnosing 23.976fps/24fps cadence issues;
- `limitations` prevents the model from inferring hardware facts that pure software telemetry cannot prove.
