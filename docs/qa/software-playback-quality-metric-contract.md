# Software Playback Quality Metric Contract

The primary consumer of `quality-run` reports is an automated model or agent, not a human reading an on-screen debug panel. Reports must therefore provide enough context for the model to identify likely failure areas and choose a next investigation step.

## Contract Principles

1. Do not emit a single quality score in phase 1.
2. Always emit raw observed metrics.
3. Always emit threshold comparisons when expected values exist.
4. Always preserve failure reasons as explicit strings.
5. Always include metric limitations so the model does not infer unsupported conclusions.
6. Always separate startup/network, frame pacing, A/V sync, buffering, and color pipeline signals.
7. Never hide one class of failure behind another. HDR output mismatch remains a failure even if frame pacing is good.
8. Always record display refresh evidence when diagnosing cadence problems. 23.976fps and 24fps sources can look subtly wrong even when no frames are obviously dropped.

## Required Top-Level Fields

- `schemaVersion`: JSON schema version.
- `metricVersion`: semantic version for metric meaning, initially `software-quality-v1`.
- `runId`: stable case identifier.
- `result`: `pass`, `fail`, or `observed`.
- `failureReasons`: exact reasons used for pass/fail.
- `analysis`: model-facing triage hints.
- `limitations`: facts the report cannot prove.
- `source`, `startup`, `timing`, `sync`, `buffers`, `colorPipeline`, `display`: raw metric sections.

## Analysis Hints

`analysis.primaryFailureArea` must be one of:

- `none`
- `startup`
- `frame-pacing`
- `av-sync`
- `buffering`
- `color-pipeline`
- `unsupported-source`
- `unknown`

`analysis.suggestedNextAction` should be concrete, for example:

- `Inspect HDR display switch and DXGI color-space mapping.`
- `Inspect frame pacing wait/drop thresholds around PlaybackFramePacing.`
- `Inspect demux/network stalls before changing render pacing.`
- `Inspect audio renderer clock and queued buffer depth.`

`analysis.relevantSignals` lists the signals that caused the conclusion.

`analysis.ignoredSignals` lists signals deliberately not used for the conclusion, for example startup duration when only frame pacing is being evaluated.

## Model Analysis Output

`PlaybackQualityReportAnalyzer` turns a full report into a compact model-facing JSON object. It does not replace the raw report; it highlights the fields an automated agent should inspect first.

The analyzer output must include:

- `primaryFailureArea` copied from report analysis;
- `failureAreas` containing every failed check area, not only the primary area;
- `failureReasons` copied from the report;
- `failedChecks` with each failed check's signal, expected value, actual value, and message;
- `evidenceSignals` listing report fields used as evidence;
- `missingEvidence` listing critical unset fields that make diagnosis weaker;
- `limitations` copied from the report.

This keeps model iteration grounded in evidence. For example, a report can identify `color-pipeline` as primary while still preserving `frame-pacing` as a secondary failure area.

## Limitations

Every report must include these phase-1 limitations:

- `software-only: does not verify actual HDMI InfoFrame output`
- `software-only: does not verify display panel EOTF, luminance, or color accuracy`
- `software-only: does not detect TV-side tone mapping`
- `internal-timing: frame intervals are measured in the player, not by an external photodiode or HDMI capture device`

## Metric Semantics

`droppedVideoFrames` means the player decoded or held a frame but intentionally discarded it because it was too late or during seek preroll.

`videoStarvedPasses` means the render loop wanted video but `VideoDecoder.TryReadFrame()` did not provide one while audio still had queued data.

`audioStarvedPasses` means the render loop could not get video and audio had no queued data, which usually points to demux/network/decode starvation rather than pure frame pacing.

`maxFrameGapMs` is the largest interval between player-side rendered/presented frames. It is not a display-device measurement.

`expectedFrameDurationMs` is derived from `source.frameRate` as `1000 / frameRate` when a usable frame rate exists. It lets the model compare frame gaps and render interval percentiles against the source cadence instead of reading them as isolated numbers.

`audioVideoDriftMsP95` is the p95 absolute difference between video frame PTS and XAudio-derived clock at render decision time.

`actualHdrOutput` is derived from native display status and swapchain state. It is not a direct HDMI analyzer reading.

`display.refreshRateHz` is the software-observed display mode refresh rate, sourced from native HDMI display status when available. `PlaybackRefreshRatePolicy` treats 1x, 2x, and 2.5x cadence as acceptable with a 0.15Hz tolerance, matching the native Xbox display-mode selection policy. For example, 23.976fps can match 23.976Hz, 24Hz, 47.952Hz, or 59.94Hz/60Hz, while 25fps should prefer a 50Hz-compatible mode.

`PlaybackQualityReportMapper.ApplySource` is the canonical Core mapping from `PlaybackDescriptor` into `source`. `PlaybackQualityReportMapper.ApplyDisplayStatus` maps `PlaybackDisplayStatus` into `display` and `colorPipeline`. `PlaybackQualityReportMapper.ApplyMetrics` maps playback metrics snapshots into `timing`, `sync`, and `buffers`. App or harness code should use these mappers instead of copying report fields manually.

## Failure Area Priority

When multiple failures occur, choose `analysis.primaryFailureArea` by this order:

1. `unsupported-source`
2. `color-pipeline`
3. `startup`
4. `buffering`
5. `av-sync`
6. `frame-pacing`
7. `unknown`

This priority is intentional. A model should not optimize frame pacing when the actual problem is that the source is unsupported or HDR output is wrong.
