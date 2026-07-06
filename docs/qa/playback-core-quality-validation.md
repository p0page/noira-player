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

The command emits `isValid`, `caseCount`, `tiers`, `purposes`, `cases`, and structured `errors`. `cases` is a schedulable summary of caseId, uri, tier, purpose, and expected source metadata. Invalid manifests return a non-zero exit code so automation can stop before collecting misleading playback evidence.

验证 manifest 后，可以生成 App-free 采集计划：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- plan-runs --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir baseline-reports --duration 60 --output baseline-run-plan.json
```

`plan-runs` 不执行播放，也不打包 App。它把每个 manifest case 转成标准 `runId`、`sourceUri`、`durationSeconds`、`expected`、`reportRelativePath` 和 `reportPath`，让模型或脚本按同一套 key 采集 `PlaybackQualityRunResult`。生成的报告应写入 plan 里的 `reportPath`，之后再运行 report-set 校验和候选评估。

快速迭代时可以只计划一个子集，例如只跑 tier 2 以内的 HDR case：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- plan-runs --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir baseline-reports --duration 60 --purpose hdr-output --max-tier 2 --output hdr-smoke-run-plan.json
```

输出里的 `filters` 会记录实际使用的 `purposes` 和 `maxTier`；`caseCount` 表示过滤后的计划 case 数，完整 manifest 总数仍在 `manifestValidation.caseCount`。

采集计划支持两种模式：

- `direct-uri`：manifest 只有 `uri`，适合公开测试视频或本地直链样本。
- `emby-item`：manifest 额外提供 `itemId`，可选 `mediaSourceId`、`startPositionTicks`、`forceSdrOutput`。计划会输出 `devCommand`，其中 `route = quality-run`，可作为 Xbox/dev-command 采集入口的标准输入。

After reports are captured, validate that the report set covers the manifest before comparing candidates:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir captured-reports --output report-set-validation.json
```

The report-set gate matches `report.runId` to manifest `caseId`, rejects missing cases, extra reports, duplicate run IDs, and source metadata mismatches. Run this gate before `compare-suite`; a report set that does not match the manifest is evidence-collection failure, not playback Core optimization evidence.

## 单报告分析

当模型只需要诊断一份已经采集好的播放质量报告，而不是比较 baseline/candidate 时，使用 App-free CLI 直接生成模型分析 JSON：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report --report captured-report.json --output report-analysis.json
```

`--report` 可以是 raw `PlaybackQualityReport`，也可以是包含顶层 `report` 字段的 `PlaybackQualityRunResult` envelope。命令会重新运行当前 Core 的 `PlaybackQualityReportAnalyzer`，输出 `failureAreas`、`failedChecks`、`evidenceSignals`、`missingEvidence`、`optimizationGate`、`framePacing` 和 `triageSteps`。自动化模型应先读取这个分析结果，再决定是补采集证据还是修改播放 Core。

## 候选版本门禁评测

当另一个 worktree 正在修改 Xbox App 交互时，播放核心候选改动应优先走 App-free 门禁，不打包、不启动 UWP App：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- evaluate-candidate --manifest docs\qa\playback-quality-reference-manifest.example.json --baseline-dir baseline-reports --candidate-dir candidate-reports --match-by run-id --comparisons-dir comparisons --output candidate-evaluation.json
```

`evaluate-candidate` 会按固定顺序执行：

- 验证 reference manifest；
- 验证 baseline 报告集是否完整覆盖 manifest；
- 验证 candidate 报告集是否完整覆盖 manifest；
- 检查 baseline report envelope 里的 `modelAnalysis.optimizationGate` 是否允许作为比较基线；
- 检查 candidate report envelope 里的 `modelAnalysis.optimizationGate` 是否允许继续优化；
- 只有前五步都有效时，才调用 `compare-suite` 做 before/after 比较；
- 输出一个模型可直接消费的 JSON，包含 `action`、`risk`、`blockers`、`activeGate`、`evidenceGates`、`baselineReportAnalysis`、`candidateReportAnalysis`、manifest/report-set 校验结果和 suite 结果。

这条命令默认使用 `--match-by run-id`，因此 manifest `caseId`、baseline `report.runId`、candidate `report.runId` 应保持一致。任何 missing/extra/duplicate/metadata mismatch 都会被视为证据采集失败，而不是播放核心优化结论。自动化模型循环应先修复采集或源选择问题，再根据 suite 结果决定保留、拆分、回退或继续修改播放核心。

`activeGate` 是模型当前应处理的入口：它指向第一个 `status != pass` 的 gate；如果所有前置 gate 都通过，则指向最终 `suite` gate。`evidenceGates` 是完整门禁摘要，按顺序列出 `manifest`、`baseline-report-set`、`candidate-report-set`、`baseline-report-analysis`、`candidate-report-analysis`、`suite`，每一项都有 `status`、`action`、`blockers`、`signals`、`failureAreas`、`targetFailureAreas`、`targetCaseIds` 和 `caseIds`。当 report-set gate 被阻断时，模型应优先根据 `signals` 和 `caseIds` 修复采集或源选择；当 `baseline-report-analysis` 或 `candidate-report-analysis` 被阻断时，模型应先处理对应 `modelAnalysis.optimizationGate` 给出的 blocker，例如 source mismatch、缺失证据或样本不足，并用 `baselineReportAnalysis.cases` 或 `candidateReportAnalysis.cases` 定位具体 report；当 suite gate 被阻断时，模型应先读取 `activeGate.targetFailureAreas` 和 `activeGate.targetCaseIds`，再展开对应 comparison；当 suite gate 为 `skipped` 时，说明还没有进入播放核心 before/after 比较。

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

`compare-suite` matches reports by relative `*.json` path by default. Manifest-driven runs should prefer `--match-by run-id`, which pairs baseline/candidate reports by `report.runId` and writes `caseId = runId` into generated comparisons:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- compare-suite --baseline-dir baseline-reports --candidate-dir candidate-reports --match-by run-id --comparisons-dir comparisons --output suite.json
```

Missing or extra files fail the command so the model does not optimize from an incomplete sample set. `--comparisons-dir` is optional and writes each individual comparison. `--previous-comparisons-dir` may point at a previous comparison directory with the same matching keys so repeated-unchanged stall protection works in batch runs; missing previous files for newly added cases are allowed. Generated comparisons include `caseId`, and the suite emits a compact `cases` list so model loops can locate the exact sample behind a suite-level action. Suite-level and case-level `failureAreas` include persisting failures even when the comparison result is unchanged, so stalled or continuing optimization still has a concrete playback Core target. Suite-level `targetFailureAreas` exposes the highest-priority Core target, and `targetCaseIds` points to the matching samples, so automated runs do not need to infer priority or localization from unordered lists.

The suite summary is conservative: any regression blocks acceptance, weak evidence requires more comparable reports, and partial evidence requires unmatched-signal review.

## Model-Facing Output

Playback quality reports are optimized for model/agent consumption:

- raw metrics remain available in `timing`, `sync`, `buffers`, `colorPipeline`, and `display`;
- `checks` contains structured threshold comparisons;
- `analysis.primaryFailureArea` identifies the first area to investigate;
- `PlaybackQualityReportComposer` is the App-free entry point that combines source, display, metrics, expected thresholds, evaluation, and model analysis in one call;
- `PlaybackQualityReferenceCaseReportRequestFactory` converts a validated reference case and actual playback evidence into a composer request with `runId = caseId`;
- `modelAnalysis.startup`, `modelAnalysis.source`, `modelAnalysis.colorPipeline`, `modelAnalysis.buffering`, and `modelAnalysis.avSync` summarize the raw report into status fields, evidence signals, and failed/mismatched signals so automated optimization can choose the right failure area before editing playback Core;
- `modelAnalysis.cadence` exposes the source/display refresh relationship, nearest 1x/2x/2.5x/3x/4x/5x target, Hz delta, and tolerance used for frame cadence diagnosis;
- `analysis.relevantSignals` names the exact report fields that triggered the conclusion;
- `PlaybackQualityReportAnalyzer` emits a model-facing analysis JSON with primary and secondary failure areas, failed check expected/actual values, evidence signals, missing evidence, and software-only limitations;
- `triageSteps` ranks blocker and failure investigation steps so automated model runs can decide whether to collect missing evidence or edit playback Core first;
- `sample` includes observed/minimum sample duration and additional required rendered frames when frame-rate evidence is available;
- `PlaybackQualityRunComparator` compares baseline and candidate reports after a Core change and classifies the run as improved, regressed, mixed, unchanged, or insufficient evidence, including baseline/candidate run IDs, comparability checks, strong/partial/weak confidence, direct optimization action/risk, optional repeated-unchanged stall protection, matched/unmatched signal coverage, unmatched candidate failures, no-matching-signal evidence gaps, and a machine-readable keep/reject/split/collect-evidence decision;
- `display.refreshRateHz` is treated as required evidence for diagnosing 23.976fps/24fps cadence issues and is exposed by native display status when HDMI mode data is available;
- `limitations` prevents the model from inferring hardware facts that pure software telemetry cannot prove.
