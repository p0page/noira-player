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
- `startup` with command/start timestamps, startup duration, evidence signals, and failed startup signals;
- `source` with parsed source status, HDR/Dolby Vision playback strategy, source evidence signals, and source mismatch signals;
- `colorPipeline` with actual HDR output, swapchain/DXGI observations, conversion status, evidence signals, and color mismatch signals;
- `buffering` with submitted/queued audio buffer evidence, video/audio starvation counters, and failed buffering signals;
- `avSync` with audio/video clock evidence, drift percentiles, max drift, and failed A/V sync signals;
- `sample` with `status`, rendered frame count, expected minimum rendered frames, derived sample durations, additional required frames, and a reason;
- `cadence` with source frame rate, display refresh rate, nearest supported 1x/2x/2.5x/3x/4x/5x target, delta, tolerance, status, and evidence signals;
- `optimizationGate` with a machine-readable decision on whether this run is usable for playback Core optimization;
- `framePacing` with a machine-readable failure pattern, normalized severity fields, and the signals that caused it;
- `triageSteps` with ranked, machine-readable next investigation steps;
- `failureAreas` containing every failed check area, not only the primary area;
- `failureReasons` copied from the report;
- `failedChecks` with each failed check's signal, expected value, actual value, and message;
- `investigationHints` with failure-area-specific suggested actions, Core/native code targets, and signals to inspect next;
- `evidenceSignals` listing report fields used as evidence;
- `missingEvidence` listing critical unset fields that make diagnosis weaker;
- `limitations` copied from the report.

This keeps model iteration grounded in evidence. For example, a report can identify `color-pipeline` as primary while still preserving `frame-pacing` as a secondary failure area.
`startup.status` must be checked before interpreting frame pacing or buffering evidence. `slow` means startup thresholds failed and the model should inspect Emby request latency, source open, demux initialization, and first-frame readiness before tuning render pacing; `missing-evidence` means startup timing telemetry is absent; `ready` means startup telemetry exists and no startup threshold failed.
`source.status` must be checked before optimizing playback timing or color conversion. `mismatch` means the report selected or parsed a different source than the reference case expected; `unsupported` means Core classified the source as not directly playable, for example unsupported Dolby Vision; `missing-evidence` means the run did not capture enough source metadata; `matched` means parsed source metadata is available and no `unsupported-source` check failed.
`colorPipeline.status` must be checked before changing HDR conversion code. `mismatch` means one or more `color-pipeline` checks failed, such as HDR output, DXGI input/output, or conversion validation; `missing-evidence` means the run did not capture usable color telemetry; `matched` means color telemetry is available and no color-pipeline check failed.
`buffering.status` must be checked before tuning frame pacing. `starved` means buffering thresholds failed and the model should inspect demux/decode/network/audio queue supply before changing frame drop or render timing; `observed-starvation` means starvation counters were non-zero but no buffering threshold failed; `missing-evidence` means supply telemetry is absent; `stable` means buffering telemetry exists and no starvation failure was reported.
`avSync.status` must be checked before changing audio clock or frame pacing logic. `drift` means A/V sync thresholds failed; `missing-evidence` means clock/drift telemetry is absent; `synced` means sync telemetry exists and no A/V sync threshold failed.
`sample.status` must be checked before changing playback timing logic. `insufficient` means the run did not render enough frames to support frame-pacing optimization from that run alone.
`sample.observedSampleDurationMs`, `sample.minimumSampleDurationMs`, and `sample.additionalRenderedFramesRequired` must be derived when frame-rate evidence exists. These fields let an automated model choose the next capture duration instead of guessing from raw frame counts.
`cadence.status` must be checked for frame-pacing work involving 23.976fps, 24fps, 25fps, 50fps, or 60fps sources. `matched` means the software-observed display refresh is within the same 0.15Hz tolerance used by `PlaybackRefreshRatePolicy`; `mismatch` means the report should inspect refresh selection before timing thresholds. `cadence.bestMultiplier`, `cadence.bestTargetRefreshRateHz`, and `cadence.refreshDeltaHz` let the model see whether the display is nearest to the supported 1x, 2x, 2.5x, 3x, 4x, or 5x target without parsing human text.
`optimizationGate.canOptimizePlaybackCore` must be `true` only when the report failed, the sample is sufficient, required evidence is present, source metadata is not mismatched/unsupported, and at least one failed area is available. If it is `false`, automated optimization must address `optimizationGate.blockers` and `optimizationGate.blockerSignals` first. When multiple playback failure areas are present, `optimizationGate.targetFailureAreas` must expose only the highest-priority failure area so a model does not tune frame pacing while source, color, startup, buffering, or A/V sync failures are still the better first edit target.
`framePacing.pattern` must classify frame pacing failures before changing native timing logic. Supported phase-1 values are `not-applicable`, `sample-insufficient`, `refresh-mismatch`, `starvation-driven`, `sustained-jitter`, `tail-jitter`, `dropped-frames`, `isolated-gap`, and `unknown`. `sample-insufficient` means the run did not render enough frames to support timing-threshold diagnosis; the model should collect a longer rendered-frame sample or repair startup/render readiness before tuning frame pacing. `starvation-driven` means frame pacing failed while video or audio starvation also failed; the model should inspect demux/decode/network supply before tuning wait/drop thresholds. `framePacing.expectedFrameDurationMs`, `renderIntervalP95FrameRatio`, `renderIntervalP99FrameRatio`, `maxFrameGapFrameRatio`, and `droppedVideoFramePercent` are derived from raw timing telemetry so a model can compare severity across 23.976fps, 24fps, 50fps, and 60fps reports without manually normalizing milliseconds.
`triageSteps` must be ordered by `rank`. Steps with `kind = blocker` come before playback optimization steps when `optimizationGate.status` is `blocked`; otherwise failure steps follow the failure-area priority order below. Each step must include the failure area, suggested action, signals, and code targets so an automated model can choose the next edit or evidence-collection action without inferring priority from prose.
`investigationHints` must be structured for automated consumers. The values should point to playback Core/native areas such as `PlaybackRefreshRatePolicy`, `FramePacing`, `PlaybackGraph`, `DxgiColorSpaceMapper`, or native quality metrics, rather than App/XAML interaction code. For `frame-pacing`, hint targets should follow `framePacing.pattern`; `starvation-driven` must point at demux/decode/network/audio queue depth before timing threshold changes.
When `missingEvidence` is non-empty, analyzer output must also include an `evidence-collection` investigation hint, even if primary playback failures are already present.
When a frame-pacing check fails, analyzer evidence must include `timing.expectedFrameDurationMs` if it is present so the model can compare observed gaps against the source cadence.
When `expected.maxStartupDurationMs` is set, missing startup timing must be reported as `startup.startupDurationMs` so the model does not optimize frame pacing before startup evidence exists.

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

`maxFrameGapMs` is the largest interval between player-side rendered/presented frames. It is not a display-device measurement. When `expected.maxFrameGapMs` is supplied, the metric must be present and positive; a default zero value is treated as missing evidence and a frame-pacing failure.

`renderIntervalMsP95` and `renderIntervalMsP99` measure player-side render interval percentiles. `expected.maxRenderIntervalMsP95` and `expected.maxRenderIntervalMsP99` are frame-pacing thresholds intended to catch repeated micro-stutter that a single maximum gap may not explain well. When either threshold is supplied, the matching interval metric must be present and positive; a default zero value is treated as missing evidence and a frame-pacing failure.

`startup.startupDurationMs` measures time from playback command acceptance to playback-start readiness as observed by the app or harness. When `expected.maxStartupDurationMs` is supplied, exceeding it or omitting the startup duration is a `startup` failure and should be investigated before frame pacing or color changes.

`expected.frameRate` validates that the actual selected media source reports the intended cadence. A mismatch is an `unsupported-source` failure because the model should inspect media source selection or source metadata before tuning render pacing.
`expected.codec`, `expected.width`, `expected.height`, and `expected.hdrKind` validate that the actual parsed media source matches the intended sample. Mismatches are `unsupported-source` failures and must be fixed or recollected before interpreting frame pacing, HDR output, or A/V sync metrics.

`source.hdrPlaybackStrategy`, `source.isHdr`, `source.isDirectPlayable`, `source.isDolbyVision`, `source.dolbyVisionProfile`, `source.dolbyVisionCompatibilityId`, `source.hasHdr10BaseLayer`, and `source.hasHlgBaseLayer` describe the parsed HDR/Dolby Vision playback strategy selected by Core. These fields are model-facing evidence for source classification and fallback diagnosis. They do not replace `expected.hdrKind` pass/fail checks; they explain why a selected source is direct playable, HDR10/HLG fallback, or unsupported so the model does not tune frame pacing when the actual issue is Dolby Vision classification or media-source selection.

The matching optional `expected.hdrPlaybackStrategy`, `expected.isHdr`, `expected.isDirectPlayable`, `expected.isDolbyVision`, `expected.dolbyVisionProfile`, `expected.dolbyVisionCompatibilityId`, `expected.hasHdr10BaseLayer`, and `expected.hasHlgBaseLayer` fields allow reference cases to pin the intended source strategy. When present, mismatches are `unsupported-source` failures. This is intended for DV Profile 5 reject cases, DV Profile 8 HDR10 fallback cases, and other samples where `hdrKind` alone is too coarse for automated Core optimization.

`PlaybackQualityReferenceManifest` is the machine-readable contract for reference media cases. Each case must include a stable `caseId`, `uri`, `tier`, at least one `purpose`, and expected source metadata. `PlaybackQualityReferenceManifestValidator` emits `isValid`, case count, tiers, purposes, schedulable case summaries, structured errors, and a `coverage` summary. `isValid` means the manifest can be scheduled; `coverage.status` tells the model whether the corpus covers the required playback Core risk areas for broad candidate evaluation. Required purposes are `sdr-smoke`, `hdr-output`, `hdr-force-sdr`, `dv-reject`, `dv-fallback`, `cadence-23.976`, `frame-pacing`, `av-sync`, and `buffering`. Automated playback loops should run the CLI `validate-manifest` command before collecting reports so missing sample metadata or corpus coverage gaps do not produce misleading optimization data.

`plan-runs` converts a validated manifest into a model-readable capture plan without building or packaging the Xbox App. Each planned case contains `caseId`, `runId`, `sourceUri`, `durationSeconds`, `captureMode`, `expected`, `reportRelativePath`, and `reportPath`. The model should treat `runId` as the stable comparison key and write each captured `PlaybackQualityRunResult` envelope to `reportPath` before running `validate-report-set` or `evaluate-candidate`. `--purpose` and `--max-tier` can restrict the plan for fast iteration; output `filters` records the applied subset, `caseCount` is the planned subset size, and `manifestValidation.caseCount` remains the full manifest size. `captureMode = direct-uri` means the sample is addressed only by `uri`. `captureMode = emby-item` means the manifest supplied `itemId`; the plan also emits a `devCommand` with `route = quality-run`, `itemId`, optional `mediaSourceId`, `startPositionTicks`, `forceSdrOutput`, `runId`, duration, and expected metadata. These capture locator fields do not replace source validation; the captured report must still match `expected.codec`, `expected.width`, `expected.height`, `expected.frameRate`, and `expected.hdrKind`.

`PlaybackQualityReferenceCaseReportRequestFactory.CreateRequest` converts a validated reference case and actual `PlaybackDescriptor` evidence into a `PlaybackQualityReportRequest`. It sets `runId = caseId`, clones the case `expected` metadata, and disables default expected generation so report evaluation is tied to the manifest case instead of inferred from the selected source. This is the preferred bridge from manifest-driven sample scheduling into `PlaybackQualityReportComposer`.

`PlaybackQualityReferenceReportSetValidator` validates captured report coverage against a reference manifest before baseline/candidate comparison. It matches `report.runId` to manifest `caseId` and emits structured failures for missing reports, extra reports, duplicate run IDs, and source metadata mismatches, including optional HDR/Dolby Vision source strategy fields when the manifest supplies them. Automated loops must treat an invalid report set as evidence collection failure, not as playback Core regression or improvement.

`expected.minRenderedVideoFrames` validates that a run produced enough video frames to be meaningful. A report with too few rendered frames is a `frame-pacing` failure because frame gap, drift, and buffer signals cannot be trusted without a real rendered sample.

`expectedFrameDurationMs` is derived from `source.frameRate` as `1000 / frameRate` when a usable frame rate exists. It lets the model compare frame gaps and render interval percentiles against the source cadence instead of reading them as isolated numbers.

`audioVideoDriftMsP95` is the p95 absolute difference between video frame PTS and XAudio-derived clock at render decision time. When `expected.maxAudioVideoDriftMsP95` is supplied, the drift metric must be present and positive; a default zero value is treated as missing evidence and an A/V sync failure.

`actualHdrOutput` is derived from native display status and swapchain state. It is not a direct HDMI analyzer reading.

When color expectations are supplied, the matching color-pipeline observations must be present. `expected.hdrOutput` requires `colorPipeline.actualHdrOutput`; `expected.dxgiInput` requires `colorPipeline.dxgiInput`; `expected.dxgiOutput` requires `colorPipeline.dxgiOutput`; `expected.requireValidatedConversion` requires `colorPipeline.conversionStatus`. Missing fields must fail with explicit missing-telemetry reasons and be reported as missing evidence so the model can distinguish absent telemetry from a real mismatch.

`display.refreshRateHz` is the software-observed display mode refresh rate, sourced from native HDMI display status when available. `PlaybackRefreshRatePolicy` treats 1x, 2x, 2.5x, 3x, 4x, and 5x cadence as acceptable with a 0.15Hz tolerance, matching the native Xbox display-mode selection policy. For example, 23.976fps can match 23.976Hz, 24Hz, 47.952Hz, 59.94Hz/60Hz, or 119.88Hz/120Hz, while 25fps can match 50Hz or 100Hz-compatible modes. `modelAnalysis.cadence.clockSpeedMultiplier`, `clockSpeedAdjustmentPercent`, and `isClockSpeedAdjustmentRequired` expose the Kodi-style clock-speed adjustment implied by the selected display mode; for example, 23.976fps on 24Hz or 60Hz is cadence-compatible but requires about +0.1% speed adjustment, while 23.976fps on 59.94Hz does not.

`PlaybackQualityReportComposer.Compose` is the canonical Core entry point for playback quality capture. It accepts a `PlaybackQualityReportRequest` containing optional `PlaybackDescriptor`, `PlaybackDisplayStatus`, `PlaybackQualityMetricsSnapshot`, `PlaybackQualityStartup`, and `PlaybackQualityExpected` evidence, then returns both the evaluated `PlaybackQualityReport` and compact `PlaybackQualityModelAnalysis`.

`PlaybackQualityExpectedFactory.CreateDefault` derives a repeatable default threshold profile from `PlaybackDescriptor`. It sets source frame rate, HDR output expectation, canonical DXGI color-space expectations for verified SDR and HDR10-style paths, minimum rendered sample size, dropped/starved frame limits, A/V drift p95, display-refresh matching, and cadence thresholds derived from source frame duration. Current defaults use a 5-second sample window, `maxFrameGapMs = frameDuration * 2.5`, `maxRenderIntervalMsP95 = frameDuration * 1.25`, and `maxRenderIntervalMsP99 = frameDuration * 2.0`. SDR defaults expect `YCBCR_STUDIO_G22_LEFT_P709 -> RGB_FULL_G22_NONE_P709`. HDR10 and Dolby Vision with HDR10 fallback defaults expect `YCBCR_STUDIO_G2084_TOPLEFT_P2020 -> RGB_FULL_G2084_NONE_P2020`. HLG and Dolby Vision with HLG fallback leave DXGI expectations unset until those paths are verified. If source frame rate is unusable, cadence thresholds and display-refresh matching are left unset.

`PlaybackQualityReportRequest.UseDefaultExpectedWhenMissing` lets App or harness callers ask the composer to use `PlaybackQualityExpectedFactory.CreateDefault` when they supply a descriptor but no explicit expected thresholds. Explicit `Expected` values always win.

`PlaybackQualityReportSerializer.Serialize(PlaybackQualityRunResult)` writes a single JSON envelope with `report` and `modelAnalysis`. Automated runs should prefer this envelope when handing evidence to a model, while still allowing separate report or analysis JSON for debugging.

`PlaybackQualityRunComparator.Compare` compares a baseline report and candidate report after a playback Core change. It emits `baselineRunId`, `candidateRunId`, `comparability`, `confidence`, `optimization`, `coverage`, `result = improved`, `regressed`, `mixed`, `unchanged`, or `insufficient-evidence`, plus signal-level `improvements` and `regressions`. `comparability.status = incompatible` must force `result = insufficient-evidence` before signal deltas are interpreted. Phase-1 comparability rejects explicit mismatches in `source.itemId`, `source.mediaSourceId`, `source.frameRate`, `source.hdrKind`, and `metricVersion`; missing values are not treated as mismatches by themselves. `coverage` must include baseline/candidate check counts, matched check count, matched signals, and unmatched baseline/candidate signals so a model can judge how broad the comparison evidence is. Matched signals may include analyzer-derived frame-pacing severity signals even though `matchedCheckCount` only counts original report checks.

`confidence.level` tells an automated optimization loop how much to trust the before/after comparison result without deriving that judgment from prose. Supported values are `strong`, `partial`, and `weak`. `strong` means all comparison checks matched between baseline and candidate. `partial` means at least one signal matched, but unmatched baseline/candidate checks or unstable signal keys are present; a model may use the result as directional evidence but should inspect `confidence.signals` and `coverage` before keeping broad Core changes. `weak` means the comparison is insufficient evidence, usually because the inputs are incompatible or no check signals matched; a model should collect comparable evidence before optimizing or reverting playback Core code. `confidence.reasons` records the machine-readable rationale.

`optimization` is the direct machine-action summary for automated playback Core iteration. `optimization.action` can be `accept-candidate`, `reject-candidate`, `split-candidate`, `continue-next-triage-step`, `review-unmatched-signals`, `isolate-candidate-regression`, `change-optimization-strategy`, or `collect-comparable-evidence`. `optimization.risk` is `low`, `medium`, or `high`; weak evidence is always high risk, partial evidence is medium risk unless it still lacks comparable evidence, and mixed changes are never low risk even when the matched signals are strong. `optimization.reasons`, `optimization.blockers`, `optimization.signals`, and `optimization.failureAreas` explain why the action was selected. Automated loops should prefer `optimization.action` for the next operation, then inspect `decision`, `confidence`, and `coverage` for the evidence trail. In particular, `partial` confidence with an improved result maps to `review-unmatched-signals`, not direct acceptance.

When an automated loop has previous comparison results, it should pass them through `PlaybackQualityComparisonContext`. If the current and immediately previous comparisons are unchanged for the same persisting failure area, and the configured `stallComparisonCountThreshold` is reached, the comparator overrides the optimization action to `change-optimization-strategy`, sets `risk = high`, adds `iteration.stalled` to blockers, and records the stalled failure areas and matched signals. This prevents a model from repeatedly making small Core edits that leave the same playback problem unchanged.

The comparator also emits a machine-readable `decision`: `keep-candidate`, `reject-candidate`, `split-candidate`, `collect-comparable-evidence`, or `no-change`. The comparator treats failed numeric threshold checks as smaller-is-better by default, except minimum-style signals such as `timing.renderedVideoFrames`, where larger values are improvements. If no check signal can be matched between reports, the result must be `insufficient-evidence`, not `unchanged`. A failing candidate check that has no baseline counterpart is a regression with `direction = candidate-only-failure`. When the comparable reports include frame-pacing failures, the comparator also derives lower-is-better deltas for `framePacing.renderIntervalP95FrameRatio`, `framePacing.renderIntervalP99FrameRatio`, `framePacing.maxFrameGapFrameRatio`, and, only when frame-count evidence exists on both sides, `framePacing.droppedVideoFramePercent`. Automated optimization loops should use both `optimization` and the underlying `confidence`/`decision` evidence after each Core change before deciding whether to keep, revise, split, revert, or collect more evidence.

`PlaybackQualityReportSerializer.Serialize(PlaybackQualityRunComparison)` writes the comparison JSON with camelCase field names. Automated runs should prefer the serialized comparison object when handing before/after evidence to a model so the model can trace which two run IDs were compared and whether they were comparable.

`tools/NextGenEmby.PlaybackQuality.Cli` 是 App-free 的命令行入口，用来从已序列化的报告文件生成模型可消费的分析和比较 JSON。`analyze-report` 命令要求传入 `--report`，输入可以是 raw `PlaybackQualityReport`，也可以是顶层包含 `report` 字段的 `PlaybackQualityRunResult` envelope；命令会重新运行 `PlaybackQualityReportAnalyzer`，并把 `PlaybackQualityModelAnalysis` 写到 stdout 或 `--output`。当自动化模型只需要诊断一次采集结果、判断是补证据还是修改播放 Core 时，应优先使用这个入口。`analyze-report-set` 命令要求传入 `--reports-dir`，会分析目录下所有 raw report 或 envelope；如果 envelope 里的 `modelAnalysis.runId` 或 `modelAnalysis.result` 缺失，该 analysis 必须被视为不完整并重新生成。命令输出与候选评测共用的 report-analysis summary；summary 必须聚合 `blockers`、optimization gate signals、analyzer evidence signals、`failureAreas`、最高优先级 `targetFailureAreas` 和对应 `targetCaseIds`，每个 case 也必须保留 `blockers`、`signals`、`failureAreas` 和 `targetFailureAreas`。模型应先读取 summary 级 `blockers` 判断能否优化 Core，再用 `targetFailureAreas` 和 `targetCaseIds` 定位样本。`compare` 命令要求传入 `--baseline` 和 `--candidate`，支持重复传入 `--previous` 做停滞检测，支持 `--stall-threshold`，并输出到 stdout 或 `--output`。这些输入同样可以是 raw `PlaybackQualityReport` 或包含顶层 `report` 的 `PlaybackQualityRunResult` envelope。CLI 是连接已采集 quality-run artifact 和自动化模型优化循环的首选桥接方式，因为它不需要临时自定义代码，也不会构建或打包 Xbox App。

`PlaybackQualityComparisonSuiteAggregator.Summarize` combines multiple comparison JSON objects into a suite-level gate for multi-sample playback optimization. The suite emits comparison counts, confidence counts, `action`, `risk`, `reasons`, `blockers`, `signals`, `failureAreas`, `targetFailureAreas`, `targetCaseIds`, `codeTargets`, and ranked `nextActions`. Regression and mixed results block automatic acceptance across the whole suite. Weak or insufficient evidence blocks acceptance until comparable reports are collected. Partial evidence maps to unmatched-signal review. Only a suite with at least one strong improvement and no blocking comparison can emit `action = accept-candidate`.
The suite also emits `cases`, a compact per-comparison summary containing `caseId`, run IDs, result, decision, action, risk, confidence, `suggestedNextAction`, reasons, signals, failure areas, blockers, and `codeTargets`. `failureAreas` must include resolved, new, and persisting failure areas; unchanged comparisons with persisting failures still surface their matched signals so a model can continue optimizing the correct playback Core area. `suggestedNextAction`, `reasons`, and `codeTargets` must preserve the case-level action rationale and likely Core/native files so automated consumers can localize the sample and understand the next operation before expanding the full `comparisons` payload.
`nextActions` is the model-first execution summary. Each item must include `rank`, `action`, `risk`, optional `failureArea`, `caseIds`, `signals`, `reasons`, `blockers`, and `codeTargets`. Rank 1 mirrors the suite-level gate and should be read before expanding `cases` or full `comparisons`; for evidence blockers it points to cases needing recollection, and for regressions it points to the highest-priority regressed failure area and likely Core/native files.
`targetFailureAreas` contains at most the highest-priority suite-level failure area and is empty when the suite action accepts the candidate. `targetCaseIds` contains the cases whose summaries include that target area. If the suite is blocked by weak or insufficient evidence and no playback failure area can be selected yet, `targetFailureAreas` may be empty, but `targetCaseIds` must still point at the cases that need comparable evidence. Automated loops should read these fields before changing code so they do not tune frame pacing while color, startup, buffering, A/V sync, source evidence, or missing comparable evidence is the higher-priority blocker.

The CLI `summarize` command reads one or more comparison JSON files and writes the same suite summary:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- summarize --comparison comparison-a.json --comparison comparison-b.json --output suite.json
```

The CLI `compare-suite` command is the preferred batch bridge for model optimization loops that collect report directories. It recursively matches baseline and candidate `*.json` reports by relative path, compares each pair, writes an aggregate suite, and optionally writes the individual comparisons:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- compare-suite --baseline-dir baseline-reports --candidate-dir candidate-reports --comparisons-dir comparisons --output suite.json
```

The command fails on missing or extra report files instead of silently dropping cases. Automated consumers should treat that as an evidence-collection blocker, not as a playback Core regression. `--match-by run-id` pairs reports by `report.runId` instead of relative file path and is preferred after manifest/report-set validation. `--previous-comparisons-dir` can be supplied to load one previous comparison per matching key, enabling the same repeated-unchanged stall protection used by single-report `compare` runs. A missing previous comparison for a current report is allowed and means that case has no history yet.
For `compare-suite`, each generated comparison receives `caseId = <relative report path>` by default or `caseId = <runId>` with `--match-by run-id`, and the suite `cases` list repeats that value so model loops can map a failure back to the original sample file.

## 候选评测门禁

`evaluate-candidate` 是模型优化播放核心时优先使用的高层 CLI 门禁。它把 manifest 校验、manifest coverage 校验、baseline 报告集校验、candidate 报告集校验、candidate report-analysis 校验和 suite 比较合并成一个 JSON 输出，避免模型在证据不完整、样本覆盖过窄或候选报告自身不可优化时误判“候选版本变好或变坏”。

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- evaluate-candidate --manifest reference-manifest.json --baseline-dir baseline-reports --candidate-dir candidate-reports --match-by run-id --comparisons-dir comparisons --output candidate-evaluation.json
```

输出字段语义：

- `manifestValidation`：reference manifest 是否可调度，包含 case、tier、purpose 和 expected source metadata 校验。
- `baselineReportSetValidation`：baseline 是否完整覆盖 manifest，并且实际源元数据是否匹配预期。
- `candidateReportSetValidation`：candidate 是否完整覆盖 manifest，并且实际源元数据是否匹配预期。
- `baselineReportAnalysis` / `candidateReportAnalysis`：baseline 和 candidate 每个 report 的 `modelAnalysis.optimizationGate` 摘要，包含 report 数量、已分析数量、缺失 analysis 数量、blocked 数量、聚合 `blockers`、`signals`、`failureAreas`、最高优先级 `targetFailureAreas`、对应 `targetCaseIds`，以及每个 case 的 `status`、`blockers`、`signals`、`failureAreas` 和 `targetFailureAreas`。
- `suite`：只有 manifest、manifest coverage、report-set、baseline report-analysis 和 candidate report-analysis 都有效时才产生有效 before/after 比较结果。
- `activeGate`：模型当前应处理的 gate。它是第一个 `status != pass` 的 gate；如果全部通过，则指向最终 `suite` gate。
- `evidenceGates`：模型优先读取的门禁摘要，按 `manifest`、`manifest-coverage`、`baseline-report-set`、`candidate-report-set`、`baseline-report-analysis`、`candidate-report-analysis`、`suite` 顺序给出 `status`、`action`、`blockers`、`signals`、`failureAreas`、`targetFailureAreas`、`targetCaseIds` 和 `caseIds`。
- `action`、`risk`、`reasons`、`blockers`：给模型直接使用的下一步决策摘要。

自动化模型循环必须按以下规则消费结果：

- 先读取 `activeGate`。需要完整审计时再读取 `evidenceGates`；`activeGate` 总是从同一列表派生，避免模型重复推断当前入口。
- 如果 `blockers` 包含 manifest、manifest coverage 或 report-set 问题，先修复样本集、证据采集、源选择、报告 runId 或 source metadata，不要修改播放核心。`manifest-coverage.incomplete` 表示样本目的覆盖不足，模型应先补 `activeGate.signals` 中列出的 missing purposes。
- 如果 `baseline-report-analysis` 或 `candidate-report-analysis` 被阻断，优先读取该 gate 的 `blockers`、`signals`、`targetFailureAreas` 和 `caseIds`。这些字段来自对应 envelope 的 `modelAnalysis.optimizationGate`，表示报告自身还不能作为播放核心优化依据，例如 source mismatch、缺失证据或样本不足。此时 suite 会被跳过。
- 如果 `activeGate.name = suite` 且被阻断，优先读取 `activeGate.targetFailureAreas` 和 `activeGate.targetCaseIds`，再按需展开 `suite.cases` 或单 case comparison。
- 需要定位具体 report 时，读取 `baselineReportAnalysis.cases` 或 `candidateReportAnalysis.cases`。raw `PlaybackQualityReport` 没有 envelope-level `modelAnalysis` 时会显示 `status = unavailable`；自动化采集应优先写入 `PlaybackQualityRunResult` envelope。如果 envelope 存在 `modelAnalysis` 但 `modelAnalysis.runId` 或 `modelAnalysis.result` 缺失，`evaluate-candidate` 必须用当前 Core analyzer 重新生成 analysis；空 `{}` 不得被视为有效 report-analysis 证据。
- 只有当 `action = accept-candidate` 且 `risk = low` 时，候选播放核心改动才可以被自动保留。
- `review-unmatched-signals`、`collect-comparable-evidence`、`change-optimization-strategy` 等动作都不是通过信号，模型应继续采集或缩小改动范围。
- `--comparisons-dir` 产出的单 case comparison 是定位问题样本的入口；模型应优先读取对应 case 的 comparison，再决定下一次修改。

`PlaybackQualityReportMapper.ApplySource` is the lower-level Core mapping from `PlaybackDescriptor` into `source`. `PlaybackQualityReportMapper.ApplyDisplayStatus` maps `PlaybackDisplayStatus` into `display` and `colorPipeline`. `PlaybackQualityReportMapper.ApplyMetrics` maps playback metrics snapshots into `timing`, `sync`, and `buffers`. App or harness code should prefer the composer and use these mappers only when it needs lower-level control.

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
