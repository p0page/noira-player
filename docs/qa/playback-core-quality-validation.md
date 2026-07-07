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
- standalone native frame pacing policy helper;
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

## Native 帧节奏策略

当源帧率缺失或无效时，`PlaybackFramePacing` 保留旧的 100ms late-frame drop 阈值。当 Core 已经拿到可用源帧率时，`PlaybackGraph` 会把帧率传给 native pacing，late-frame drop 阈值会按帧率自适应：约 2.5 个源帧，并设置 40ms 下限。这样 23.976/24fps 会接近原有容忍度，同时避免 50/60fps 播放在丢帧追赶前容忍过多晚到帧。

`native-frame-pacing-test` 会在不构建、不打包 App 的情况下编译并运行 `tests/NextGenEmby.Native.Tests/FramePacingTests.cpp`。自动化播放优化应把它视为 Core/native 策略门禁，而不是 Xbox 视觉效果验证。

质量报告会把这条策略暴露为 `timing.framePacingSourceFrameRate`、`timing.lateFrameDropToleranceMs`、`modelAnalysis.framePacing.lateFrameDropToleranceMs` 和 `modelAnalysis.framePacing.lateFrameDropToleranceFrameRatio`。这些字段描述 native pacing 实际采用的策略参数，不代表真实 HDMI 输出或肉眼观感。

## Validate Reference Manifest

Use the App-free CLI to validate a playback reference corpus manifest before using it in automated runs:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-manifest --manifest docs\qa\playback-quality-reference-manifest.example.json --output manifest-validation.json
```

The command emits `schemaVersion = 1`, `isValid`, `caseCount`, `tiers`, `purposes`, `cases`, structured `errors`, and `coverage`. `cases` is a schedulable summary of caseId, uri, tier, purpose, and expected source metadata. Invalid manifests return a non-zero exit code so automation can stop before collecting misleading playback evidence. `isValid = true` only means the manifest can be scheduled; `coverage.status = ready` means the corpus includes the required playback Core risk purposes for broad candidate evaluation: `sdr-smoke`, `hdr-output`, `hdr-force-sdr`, `dv-reject`, `dv-fallback`, `cadence-23.976`, `frame-pacing`, `av-sync`, and `buffering`. If `coverage.status = incomplete`, the model should treat `coverage.missingPurposes` as a sample-corpus gap and avoid over-optimizing Core from a narrow corpus.

当前 `docs/qa/playback-quality-reference-manifest.example.json` 是可调度的默认参考 manifest。它包含 7 个 case：

- Jellyfin SDR HEVC Main10 1080p60 3M：`sdr-smoke`、`av-sync`。
- Jellyfin HDR10 HEVC Main10 1080p60 10M：`hdr-output`。
- 同一个 Jellyfin HDR10 1080p60 10M 源的 force-SDR 变体：`hdr-force-sdr`。
- Jellyfin HDR10 HEVC Main10 4K60 50M：`buffering` 和 4K/60/HDR 解码压力。
- Jellyfin Dolby Vision Profile 5 4K60：`dv-reject`，期望 Core 解析后明确拒播为 `DolbyVisionUnsupported`。
- Jellyfin Dolby Vision Profile 8.1 4K60：`dv-fallback`，期望 Core 解析为 `DolbyVisionWithHdr10Fallback`。
- `local/chimera-23976-hdr10-cadence`：本地 Emby 绑定占位，用于 `cadence-23.976` 和 `frame-pacing`。公开直链还没有稳定验证到 23.976 HDR10 样本，所以这里使用 `emby-item` 调度，而不是提交不可靠 URL。

Jellyfin 公开直链 case 应以 `direct-uri` 调度；本地 23.976 case 应以 `emby-item` 调度并生成 `quality-run` dev command。`tools/quality-run/run-playback-quality-cli-smoke-test.ps1` 会校验这些不变量，防止 example manifest 退化成 coverage 不完整或不可调度的状态。

## 公开素材探测

`validate-manifest` 只能证明 manifest 结构可调度，不能证明公开 URL 当前可访问，也不能证明文件实际 metadata 仍然匹配预期。进入播放优化前，可以用 `ffprobe` 跑公开素材探测：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\Test-PublicReferenceMedia.ps1 -CaseId 'jellyfin/hdr10-hevc-main10-4k60-50m' -OutputPath docs\qa\private\jellyfin-hdr10-4k60-50m-probe.local.json
```

脚本只探测 `http://` 和 `https://` case；`emby://` 或本地私有 case 会以 `status = skipped`、`reason = non-public-uri` 输出。默认输出路径位于 `docs/qa/private/*.local.json`，属于 `.gitignore` 覆盖范围，不应提交。

输出报告面向模型消费：每个公开 case 会给出 `source.codec`、`source.width`、`source.height`、`source.frameRate` 和可由 `ffprobe` 判断的 `source.hdrKind` 检查结果，并保留 `probe.codec/profile/pixelFormat/colorTransfer/colorPrimaries/bitRate` 等原始证据。HDR10 与 SDR 会直接比较；Dolby Vision、动态 metadata 或需要播放器解析的 HDR 分类不会仅凭文件名判定，会输出跳过原因，后续仍应依赖播放 Core 解析后的报告。

当前 Jellyfin 4K60 HDR10 50M 公开样片已在本机验证通过：`ffprobe` 读到 HEVC Main 10、3840x2160、60fps、`bt2020nc`、`smpte2084`、`bt2020`、约 49.2 Mbps。这个结论只说明源文件 metadata 匹配 manifest，不代表 Xbox 已正确输出 HDR，也不代表颜色管线等价于 Kodi。

私有 Emby 服务器可以作为真实库测试源，但不得进入仓库。服务器 URL、用户名、密码、真实 `itemId`、真实 `mediaSourceId`、采集出的私有报告和本地 manifest 应只放在环境变量、系统临时目录或 `.gitignore` 覆盖的路径中，例如 `docs/qa/private/`、`tools/quality-run/private/`、`*.private.json`、`*.local.json`、`*.secrets.json` 或 `.env`。提交前必须用 `rg` 检查真实服务器地址、账号和密码没有出现在 tracked 文件或 diff 中。

可以用本地脚本从私有 Emby 库生成 ignored manifest：

```powershell
$env:NEXTGENEMBY_QA_SERVER_URL = '<private-emby-url>'
$env:NEXTGENEMBY_QA_USERNAME = '<private-user>'
$env:NEXTGENEMBY_QA_PASSWORD = '<private-password>'

powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\New-PrivateEmbyReferenceManifest.ps1 -OutputPath docs\qa\private\emby-reference-manifest.local.json -Limit 1000
```

脚本先分页读取 Movie/Episode item，再对没有 `MediaSources` 的 item 补取 `/Items/{ItemId}/PlaybackInfo`。HDR/Dolby Vision 分类只使用实际 stream metadata，例如 codec、`VideoRange`、`ColorPrimaries`、`ColorTransfer` 和 `ColorSpace`；item 名称、source 名称和 display title 不参与 DV 判定。如果 stream metadata 看起来是 SDR，但名称或标题里出现 DV/DoVi/Dolby Vision 提示，脚本会保守地把该源视为未知 HDR 线索，避免把疑似 DV 源选作 SDR smoke。若要定位特定片源，可以加 `-SearchTerm '<title>'` 输出到另一个 ignored manifest。生成后仍应运行 `validate-manifest`，把 `coverage.missingPurposes` 当作样本缺口，而不是播放 Core 结论。

如果全库扫描慢或会超时，优先生成多个小的 ignored manifest，再合并成一个综合 corpus：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\New-PrivateEmbyReferenceManifest.ps1 -SearchTerm '<title-a>' -OutputPath docs\qa\private\title-a-reference-manifest.local.json -Limit 50
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\New-PrivateEmbyReferenceManifest.ps1 -SearchTerm '<title-b>' -OutputPath docs\qa\private\title-b-reference-manifest.local.json -Limit 50

powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\Merge-ReferenceManifests.ps1 -ManifestPath "docs\qa\playback-quality-reference-manifest.example.json,docs\qa\private\title-a-reference-manifest.local.json,docs\qa\private\title-b-reference-manifest.local.json" -OutputPath docs\qa\private\combined-reference-manifest.local.json -DuplicateCaseIdMode skip

dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-manifest --manifest docs\qa\private\combined-reference-manifest.local.json --output docs\qa\private\combined-reference-manifest-validation.local.json
```

`Merge-ReferenceManifests.ps1` 只拼接 `cases` 并保留输入顺序，默认 `-DuplicateCaseIdMode fail` 会拒绝重复 `caseId`，用于严格检查手写 manifest。私有 Emby 多搜索词采集会自然产生重叠样本，此时应显式使用 `-DuplicateCaseIdMode skip`，保留第一个 case 并跳过后续重复 case，避免把同一个真实片源重复放大为多份证据。这让模型可以把公开 Jellyfin DV 样本、公开 HDR10 压力样本和私有 Emby 真实库样本放进同一个本地评测集，同时仍保证所有私有 locator 和报告留在 `.gitignore` 覆盖路径。当前本机验证过的 `public example + private Emby 搜索结果` 综合 manifest 有 30 个 case，`coverage.status = ready`，覆盖 `sdr-smoke`、`hdr-output`、`hdr-force-sdr`、`dv-reject`、`dv-fallback`、`cadence-23.976`、`frame-pacing`、`av-sync` 和 `buffering`。

验证 manifest 后，可以生成 App-free 采集计划：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- plan-runs --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir baseline-reports --duration 60 --output baseline-run-plan.json
```

`plan-runs` 不执行播放，也不打包 App。它把每个 manifest case 转成标准 `runId`、`sourceUri`、`durationSeconds`、`requiredSignals`、`expected`、`reportRelativePath` 和 `reportPath`，让模型或脚本按同一套 key 采集 `PlaybackQualityRunResult`。生成的报告应写入 plan 里的 `reportPath`，之后再运行 report-set 校验和候选评估。

`requiredSignals` 是模型采集前的 case 级 telemetry checklist。它会根据 `expected` 和 `purpose` 自动列出必须出现在报告里的信号，例如 `source.codec`、`source.hdrKind`、`colorPipeline.actualHdrOutput`、`display.hdrStatus`、`colorPipeline.swapChainFormat`、`colorPipeline.swapChainColorSpace`、`display.refreshRateHz`、`timing.framePacingSourceFrameRate`、`sync.audioVideoDriftMsP95`、`buffers.videoStarvedPasses`。HDR10 输出还会要求 `colorPipeline.isTenBitSwapChain`；如果采集到的值是 `false`，evaluator 会把它判为 `color-pipeline` 失败，避免模型只凭推断的 `actualHdrOutput` 优化颜色管线。如果报告缺这些信号，应先补采集或让 `analyze-report` 明确标记 missing evidence，不要直接根据窄证据修改播放 Core。

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

report-set gate 输出 `schemaVersion = 1`，并按 `report.runId` 匹配 manifest `caseId`，拒绝缺失 case、额外报告、重复 runId、source metadata 不匹配以及 case 级 `requiredSignals` 缺失。缺失 telemetry 会输出为 `report.requiredSignal.missing`，并带上精确的 `signal`、`caseId`、`failureArea`、`suggestedNextAction` 和 `codeTargets`。CLI 会保留 JSON 字段 presence，所以显式写出的 0 counter（例如 `videoStarvedPasses: 0`）会被视为已采集，字段不存在才会被视为缺证据。运行 `compare-suite` 前必须先跑这个 gate；report set 不匹配 manifest 或缺少必要 telemetry 都属于证据采集失败，不是播放 Core 优化证据。

## 单报告分析

当模型只需要诊断一份已经采集好的播放质量报告，而不是比较 baseline/candidate 时，使用 App-free CLI 直接生成模型分析 JSON：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report --report captured-report.json --output report-analysis.json
```

`--report` 可以是 raw `PlaybackQualityReport`，也可以是包含顶层 `report` 字段的 `PlaybackQualityRunResult` envelope。命令会重新运行当前 Core 的 `PlaybackQualityReportAnalyzer`，输出 `failureAreas`、`failedChecks`、`evidenceSignals`、`missingEvidence`、`optimizationGate`、`framePacing` 和 `triageSteps`。如果报告 pin 住颜色输出期望，`missingEvidence` 会标记缺失的 `display.hdrStatus`、swapchain 格式/色彩空间和 HDR10 十位 swapchain 证据。自动化模型应先读取这个分析结果，再决定是补采集证据还是修改播放 Core。

当模型需要快速审计一整个报告目录，但还没有进入 baseline/candidate 比较时，使用目录级分析：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir captured-reports --output report-analysis-summary.json
```

`analyze-report-set` 会读取目录下所有 `*.json`，对 raw report 自动运行当前 Core analyzer；对已有 envelope，如果 `modelAnalysis.runId` 或 `modelAnalysis.result` 缺失，或 `modelAnalysis.analyzerVersion` 不等于当前 `PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion`，也会把该 analysis 视为不可复用并重新生成。输出复用候选评测中的 report-analysis summary，包含 `schemaVersion = 1`、`action`、`risk`、`confidence`、`nextActions`、`totalReportCount`、`analyzedReportCount`、`unavailableReportCount`、`blockedReportCount`、聚合 `blockers`、`signals`、`failureAreas`、`targetFailureAreas`、`targetCaseIds`、`codeTargets`、`suggestedNextActions`，以及每个 case 的 `status`、`blockers`、`signals`、`failureAreas`、`targetFailureAreas`、`codeTargets`、`suggestedNextActions`。模型应先读取 `nextActions[0]`，再用 `action`、`risk` 和 `confidence.level` 判断能否继续优化 Core；rank 1 action 会携带目标 failure area、case、signals、blockers 和 code targets。这一步适合在采集完成后立即判断证据是否足够、下一步应补 telemetry 还是修改播放 Core。

## 候选版本门禁评测

当另一个 worktree 正在修改 Xbox App 交互时，播放核心候选改动应优先走 App-free 门禁，不打包、不启动 UWP App：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- evaluate-candidate --manifest docs\qa\playback-quality-reference-manifest.example.json --baseline-dir baseline-reports --candidate-dir candidate-reports --match-by run-id --comparisons-dir comparisons --output candidate-evaluation.json
```

`evaluate-candidate` 会按固定顺序执行：

- 验证 reference manifest；
- 验证 manifest coverage 是否覆盖播放 Core 候选评测所需风险面；
- 验证 baseline 报告集是否完整覆盖 manifest；
- 验证 candidate 报告集是否完整覆盖 manifest；
- 检查 baseline report envelope 里的 `modelAnalysis.optimizationGate` 是否允许作为比较基线；
- 检查 candidate report envelope 里的 `modelAnalysis.optimizationGate` 是否允许继续优化；
- 只有前六步都有效时，才调用 `compare-suite` 做 before/after 比较；
- 输出一个模型可直接消费的 JSON，包含 `schemaVersion`、`action`、`decision`、`risk`、`blockers`、`activeGate`、`evidenceGates`、`baselineReportAnalysis`、`candidateReportAnalysis`、manifest/report-set 校验结果和 suite 结果。

这条命令默认使用 `--match-by run-id`，因此 manifest `caseId`、baseline `report.runId`、candidate `report.runId` 应保持一致。任何 missing/extra/duplicate/metadata mismatch 都会被视为证据采集失败，而不是播放核心优化结论。自动化模型循环应先修复采集或源选择问题，再根据 suite 结果决定保留、拆分、回退或继续修改播放核心。

`activeGate` 是模型当前应处理的入口：它指向第一个 `status != pass` 的 gate；如果所有前置 gate 都通过，则指向最终 `suite` gate。`evidenceGates` 是完整门禁摘要，按顺序列出 `manifest`、`manifest-coverage`、`baseline-report-set`、`candidate-report-set`、`baseline-report-analysis`、`candidate-report-analysis`、`suite`，每一项都有 `status`、`action`、`risk`、`confidence`、`resultCounts`、`signalSummaries`、`blockers`、`signals`、`failureAreas`、`targetFailureAreas`、`targetCaseIds`、`caseIds`、`codeTargets`、`suggestedNextActions` 和 `nextActions`。当 `manifest-coverage` gate 被阻断时，模型应先补 `signals` 中列出的 missing purposes，不要根据窄样本集修改播放 Core；当 report-set gate 被阻断时，模型应优先根据 `signals`、`caseIds`、`codeTargets` 和 `suggestedNextActions` 修复采集或源选择；当 `baseline-report-analysis` 或 `candidate-report-analysis` 被阻断时，模型应先处理对应 `modelAnalysis.optimizationGate` 给出的 blocker，例如 source mismatch、缺失证据或样本不足，并用 `baselineReportAnalysis.cases` 或 `candidateReportAnalysis.cases` 定位具体 report，同时用 `codeTargets` 和 `suggestedNextActions` 定位采集、源分类或 Core/native 文件及下一步操作；当 suite gate 被阻断时，模型应先读取 `activeGate.environment`、`activeGate.risk`、`activeGate.confidence`、`activeGate.resultCounts`、`activeGate.signalSummaries`、`activeGate.nextActions`、`activeGate.blockers` 中聚合的 `comparison.*` blocker、`activeGate.targetFailureAreas`、`activeGate.targetCaseIds`、`activeGate.codeTargets` 和 `activeGate.suggestedNextActions`，再展开对应 comparison；当 suite gate 为 `skipped` 时，说明还没有进入播放核心 before/after 比较。

`decision` 是 candidate evaluation 的顶层机器决策摘要。模型应优先用它判断是否保留候选，再用 `action`、`risk`、`activeGate` 和 `nextActions` 定位下一步。映射规则是：`accept-candidate` -> `keep-candidate`，`reject-candidate` / `split-candidate` 保持同名，`continue-next-triage-step` -> `no-change`，`collect-comparable-evidence`、`review-unmatched-signals`、`change-optimization-strategy` 保持原动作名。

`activeGate.confidence` 是 gate 级证据强度摘要。suite gate 会聚合 suite 的 `strongCount`、`partialCount`、`weakCount`、`insufficientEvidenceCount` 和整体 `level`；前置 manifest/report-set/report-analysis gate 被阻断时，`level = weak`，表示当前证据还不能支撑播放 Core 优化结论。

`activeGate.resultCounts` 是 suite 级比较结果分布摘要，包含 `totalCount`、`improvedCount`、`regressedCount`、`mixedCount`、`unchangedCount`、`insufficientEvidenceCount` 和 `policyChangeCount`。它只描述 baseline/candidate 指标比较结果；是否可以采纳候选仍必须由 `action`、`blockers`、`risk` 和 `confidence` 决定。前置 gate 和 skipped suite 的 `resultCounts` 保持 0，表示尚未形成播放 Core before/after 比较结论。

`activeGate.signalSummaries` 是 suite 级信号证据摘要，按 signal 和 failure area 聚合 `outcome`、improvement/regression/policy-change 计数、case IDs 和方向。模型可先用它判断具体受影响的播放 Core 信号，再决定是否展开 full suite 或单个 comparison。

`activeGate.nextActions` 是 gate 级结构化执行摘要，每项包含 `rank`、`action`、`risk`、可选 `failureArea`、`caseIds`、`signals`、`reasons`、`blockers` 和 `codeTargets`。suite gate 会复制 suite 的 ranked `nextActions`；前置 gate 和 skipped suite 会从自身的 action/risk/signals/blockers/case/code target 生成 rank 1 动作。模型应优先读取 `activeGate.nextActions[0]`，再决定是否需要展开 `suite.cases` 或单个 comparison。

`evaluate-candidate` 仍会把 raw `PlaybackQualityReport` 视为没有 envelope-level `modelAnalysis`，并在 report-analysis summary 中显示 `status = unavailable`；这让旧报告可以继续走 suite 比较，但自动化采集应优先写入完整 `PlaybackQualityRunResult` envelope。新的 envelope 会带顶层 `schemaVersion = 1`，内部 `report.schemaVersion` 和 `modelAnalysis.analyzerVersion` 仍分别描述原始报告与分析器契约。若 envelope 里存在 `modelAnalysis` 但 `modelAnalysis.runId` / `modelAnalysis.result` 缺失，或 `modelAnalysis.analyzerVersion` 不匹配当前 analyzer，门禁会把它视为不可复用并用当前 Core analyzer 重新生成，然后再决定 report-analysis gate 是否阻断。空对象 `{}` 不能绕过 report-analysis gate。

## Compare Reports

Use the App-free CLI when an automated model run needs to compare two serialized playback quality reports without building the Xbox App:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- compare --baseline baseline.json --candidate candidate.json --output comparison.json
```

The `compare` and `compare-suite` commands accept either a raw `PlaybackQualityReport` JSON file or a `PlaybackQualityRunResult` envelope with a top-level `report` property. Generated comparison JSON includes `schemaVersion = 1`, `environment.status`, baseline/candidate build identity fields, `codeTargets`, and ranked `nextActions`, so model loops can verify whether evidence came from the intended Core revision and locate the next Core/native file before accepting or rejecting a candidate change. If comparison evidence has missing, partial, or same-build identity, it is treated as weak evidence and must collect comparable evidence before accepting the candidate. Per-case comparison blockers use `comparison.environment-evidence-missing`, `comparison.environment-same-build`, `comparison.incompatible-inputs`, `comparison.missing-checks`, or `comparison.no-matched-signals`; suite blockers distinguish missing identity with `suite.environment-evidence-missing` and same-build identity with `suite.environment-same-build`.

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

The suite output includes `schemaVersion`, `decision`, and ranked `nextActions`. Automated model loops should verify `schemaVersion = 1`, then read `decision` plus rank 1 before expanding the full `comparisons` payload; rank 1 carries the suite action, risk, target case IDs, signals, blockers, reasons, and likely Core/native code targets.
It also includes `signalSummaries`, which aggregates improvements and regressions by signal and failure area so a model can identify cross-case playback trends before opening each individual comparison.

`compare-suite` matches reports by relative `*.json` path by default. Manifest-driven runs should prefer `--match-by run-id`, which pairs baseline/candidate reports by `report.runId` and writes `caseId = runId` into generated comparisons:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- compare-suite --baseline-dir baseline-reports --candidate-dir candidate-reports --match-by run-id --comparisons-dir comparisons --output suite.json
```

Missing or extra files fail the command so the model does not optimize from an incomplete sample set. `--comparisons-dir` is optional and writes each individual comparison. `--previous-comparisons-dir` may point at a previous comparison directory with the same matching keys so repeated-unchanged stall protection works in batch runs; missing previous files for newly added cases are allowed. Generated comparisons include `caseId`, and the suite emits a compact `cases` list so model loops can locate the exact sample behind a suite-level action. Each case summary includes `suggestedNextAction`, `reasons`, and `codeTargets`, so the model can understand the case-level action rationale and likely Core/native files before opening the full comparison JSON. Suite-level and case-level `failureAreas` include persisting failures even when the comparison result is unchanged, so stalled or continuing optimization still has a concrete playback Core target. Suite-level `targetFailureAreas` exposes the highest-priority Core target, `targetCaseIds` points to the matching samples, and suite-level `codeTargets` exposes the likely files for the current gate. Automated runs should not infer priority or localization from unordered lists. When the suite is blocked by weak or insufficient evidence and no playback failure area can be chosen yet, `targetFailureAreas` may be empty, but `targetCaseIds` and `codeTargets` still point at the cases and evidence-collection files that need comparable evidence.

The suite summary is conservative: any regression blocks acceptance, weak evidence requires more comparable reports, and partial evidence requires unmatched-signal review.

## Model-Facing Output

Playback quality reports are optimized for model/agent consumption:

- raw metrics remain available in `timing`, `sync`, `buffers`, `colorPipeline`, and `display`;
- `checks` contains structured threshold comparisons;
- `analysis.primaryFailureArea` identifies the first area to investigate;
- `PlaybackQualityReportComposer` is the App-free entry point that combines source, display, metrics, expected thresholds, evaluation, and model analysis in one call;
- `PlaybackQualityReferenceCaseReportRequestFactory` converts a validated reference case and actual playback evidence into a composer request with `runId = caseId`;
- `modelAnalysis.environment` exposes collector version, player core version, source revision, and build configuration so before/after report sets can be tied back to the Core revision that produced them;
- `modelAnalysis.startup`, `modelAnalysis.source`, `modelAnalysis.colorPipeline`, `modelAnalysis.buffering`, and `modelAnalysis.avSync` summarize the raw report into status fields, evidence signals, and failed/mismatched signals so automated optimization can choose the right failure area before editing playback Core;
- `modelAnalysis.cadence` exposes the source/display refresh relationship, nearest 1x/2x/2.5x/3x/4x/5x target, Hz delta, tolerance, fractional 2.5x pulldown marker, and Kodi-style clock-speed adjustment used for frame cadence diagnosis;
- `modelAnalysis.avSync.clockDeltaMs` 和 `driftDirection` 是从 `sync.videoPositionTicks - sync.audioClockTicks` 派生的方向化 A/V sync 诊断；它们帮助模型区分 `video-ahead`、`audio-ahead`、`aligned`，但不属于 `requiredSignals` 采集清单，缺少时应先补 clock telemetry；
- `modelAnalysis.framePacing` exposes both the machine-readable failure pattern and normalized severity fields such as interval-to-frame ratios and dropped-frame percent, so automated optimization can compare stutter evidence across source frame rates;
- `analysis.relevantSignals` names the exact report fields that triggered the conclusion;
- `PlaybackQualityReportAnalyzer` emits a model-facing analysis JSON with primary and secondary failure areas, failed check expected/actual values, evidence signals, missing evidence, and software-only limitations;
- `triageSteps` ranks blocker and failure investigation steps so automated model runs can decide whether to collect missing evidence or edit playback Core first;
- `sample` includes observed/minimum sample duration and additional required rendered frames when frame-rate evidence is available;
- `PlaybackQualityRunComparator` compares baseline and candidate reports after a Core change and classifies the run as improved, regressed, mixed, unchanged, or insufficient evidence, including baseline/candidate run IDs, comparability checks, strong/partial/weak confidence, direct optimization action/risk, suggested next action, reasons, suite/case code targets, optional repeated-unchanged stall protection, matched/unmatched signal coverage, unmatched candidate failures, no-matching-signal evidence gaps, and a machine-readable keep/reject/split/collect-evidence decision;
- `display.refreshRateHz` is treated as required evidence for diagnosing 23.976fps/24fps cadence issues and is exposed by native display status when HDMI mode data is available;
- `limitations` prevents the model from inferring hardware facts that pure software telemetry cannot prove.
