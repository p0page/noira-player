# 当前状态

播放质量评测体系正在推进 v0.1，目标是先把评测做成可信裁判，而不是优化播放效果。

## 2026-07-08 更新：结构化播放生命周期证据

已新增 `PlaybackQualityReport.lifecycle` 和 `modelAnalysis.lifecycle`：
- report 会保存 `lifecycle.events[]`，记录 `operation`、`status`、`state`、`positionTicks` 和 `message`。
- core-probe 现在会记录 `load`、`play`、`pause`、`resume`、`seek`、`stop`，音轨/字幕切换也会作为生命周期事件保存。
- error/skip 路径会输出对应的 lifecycle event，例如 `lifecycle.error` 或 `lifecycle.skip`。
- `PlaybackQualityReportAnalyzer` 会把生命周期事件转换为 `lifecycle.*` evidence signals，并在可播放报告缺少 `load/play/pause/resume/stop` 时标记 missing evidence。
- `validate-report-set` 的 required-signal policy 已要求可播放 case 提供生命周期证据；明确 unsupported 的 source 不要求播放生命周期。
- CLI JSON presence collector 已能从 `lifecycle.events[]` 提取 `lifecycle.*` 信号，手写 raw JSON report 也能被 report-set validation 正确识别。

这一步属于 instrumentation/testability，不改变播放器播放行为、阈值或 pass/fail 规则。它补齐的是 v0.1 “播放生命周期：load、play、pause、resume、stop、error” 的机器可读证据入口；`endOfStream` 仍是后续真实播放 harness 需要补的能力。

## 2026-07-08 更新：report-set capability coverage 摘要

已新增 `analyze-report-set` 的集合级 `capabilityCoverage` 输出：

- summary 会按 v0.1 关键能力聚合 evidence / missing / blocked 状态，例如 metadata、source-capability、runtime-metrics、frame-pacing、A/V sync、buffering、color、tracks、subtitles、error-handling。
- 每个 capability 会输出 `status`、`requiredSignals`、`evidenceSignals`、`missingSignals`、`blockers`、`caseIds` 和 `suggestedNextActions`，方便模型先判断当前报告集能不能支撑某类 core 优化。
- `runtime-metrics` 在没有 `runtimeMetrics.status` 证据时会明确标记为 `missing-evidence`，避免模型只凭 timing/buffer 默认值推断真实播放采样存在。
- 这一步只扩展 report-analysis summary，不改变播放器行为、阈值、expected behavior、case 分类或 pass/fail 规则。

## 2026-07-08 更新：runtime metrics 采集状态信号

已新增 runtime metrics 采集状态的结构化报告：

- `PlaybackQualityReport.runtimeMetrics` 记录 `status`、`providerStatus`、`reason`、`hasSnapshot` 和 `hasPlaybackSample`。
- `PlaybackQualityRuntimeEvidenceCollector` 会区分 metrics provider 未提供、provider 返回 false、provider 返回空 snapshot、provider 返回含播放样本的 snapshot。
- `PlaybackQualityReportAnalyzer` 会把 `runtimeMetrics.*` 输出到模型可消费的 `modelAnalysis.runtimeMetrics` 和 evidence signals。
- 未知默认值不会被当作采集证据；只有明确的 runtime metrics 状态才会输出 `runtimeMetrics.hasSnapshot` / `runtimeMetrics.hasPlaybackSample` 证据信号。
- `materialize-baseline-report-set` 的 source-only 普通播放报告会显式写入 `runtimeMetrics.status = unavailable`、`providerStatus = source-only`，不再让模型从默认 `unknown` 推断采集器没有运行。
- `optimizationGate` 会在 `runtimeMetrics.status = unavailable` 或 `empty-snapshot` 时加入 runtime metrics blocker，防止模型从无播放样本的报告直接优化播放 core。

这一步仍属于 instrumentation/testability，不改变播放行为、阈值、expected behavior 或 pass/fail 规则。它解决的问题是让模型区分“真实播放指标缺失”和“采集器已经明确报告没有指标”，避免把默认 0 counters 当成有效播放样本。

## 2026-07-07 更新：runtime evidence collector

已新增 Core 侧的 runtime evidence collector：

- `IPlaybackQualityMetricsProvider` 作为可选 metrics instrumentation 接口，不修改既有 `IPlaybackBackendDiagnostics` 契约，避免破坏当前 App adapter。
- `PlaybackQualityRuntimeEvidenceCollector` 可从 reference case、`PlaybackDescriptor`、backend display diagnostics、metrics snapshot、startup 和 environment 生成标准 `PlaybackQualityRunResult`。
- `NativeDirectXPlaybackBackend` 会在底层 native engine 实现 `IPlaybackQualityMetricsProvider` 时委托读取 metrics；如果底层不支持，不伪造 metrics。
- `PlaybackQualityOrchestratorProbe` 已改为通过同一 collector/provider 路径生成报告。

这一步属于 instrumentation/testability，不是播放行为优化。它把 native 已有的 `QualityMetrics()` 能力接到 Core 评测契约的入口处；后续仍需要让实际 WinRT App adapter 或独立 harness 实现该 provider，才能把真实 native graph metrics 写入采集报告。

## 2026-07-07 更新：error-handling 报告路径

已补齐 Core 侧的错误报告 envelope：

- `PlaybackQualityRuntimeEvidenceCollector.ComposeErrorRunResult` 可把打开失败、取消、超时、缺失文件、native 错误或明确拒播写成标准 `PlaybackQualityRunResult`。
- `PlaybackQualityReport` 现在有 `error` section，保留 `code`、`message`、`operation`、`exceptionType`、`failureClass`、`failureArea`、`isTerminal` 和 `isRetriable`。
- `PlaybackQualityReportAnalyzer` 会把 `error.*` 作为证据输出，并把这类问题定位到 `error-handling`，不再要求 source/timing/startup 等播放 telemetry。
- reference manifest coverage 现在要求 `error-handling` purpose，避免 v0.1 只覆盖成功播放路径。

这一步仍然属于评测和诊断能力，不是播放行为优化。它的价值是让模型区分“播放器 core 真的播放质量差”和“样本打不开、输入异常、当前能力不支持或采集环境失败”。

## 已完成

- `PlaybackQuality` 报告已覆盖 source、startup、position、timing、sync、buffers、colorPipeline、display 等核心软件信号。
- `PlaybackQualityReportAnalyzer` 已输出模型可消费的 failure area、failure class、evidence、missing evidence、triage steps 和 optimization gate。
- reference manifest、report-set validation、analyze-report、compare、compare-suite、evaluate-candidate、plan-runs 已形成 App-free 闭环。
- `materialize-run-result` 已可把 raw report 或旧 envelope 归一化为包含当前 `modelAnalysis` 的 `PlaybackQualityRunResult` envelope。
- `materialize-run-result` 会保留已有 envelope 的 `caseMetadata`；raw report 会补默认 case metadata。
- `materialize-baseline-report-set` 已可从 reference manifest 生成 source-only baseline envelope，用于建立可版本化 baseline artifact 并暴露缺失 telemetry。
- `docs/qa/baselines/v0.1-source-only/` 已归档 source-only baseline：9/9 case 有报告；`error-handling` case 以一等 `result = error` envelope 匹配，其余 8 个播放 case 因 67 个缺失 telemetry 失败，全部归类为 `insufficient instrumentation`。
- reference manifest case 已支持 `stable`、`challenge`、`quarantine` 分类，并在 validation、report-set status 和 run plan 中保留。
- reference manifest case 已支持 `severity` 和 `stability`，并在 validation、report-set status、run plan 和 baseline summary 中保留。
- `PlaybackQualityRunResult` envelope 已输出 `caseMetadata`，单个报告可直接暴露 case id、category、severity 和 stability。
- report-set validation errors 已输出 `failureClass`，可区分缺 telemetry、缺报告、重复/额外报告和 source metadata mismatch。
- App-free 验证命令为 `tools\quality-run\run-playback-core-checks.ps1`，当前结果为 pass。
- 本轮新增 tracks/subtitles telemetry：报告会记录视频轨、音轨、字幕轨数量、当前选中音轨/字幕轨、字幕关闭状态和轨道明细。
- reference manifest coverage 现在要求 `tracks` 和 `subtitles` purpose；默认公开 manifest 与私有 Emby manifest 生成脚本已同步更新。
- Core 已有可选 runtime metrics provider 和 runtime evidence collector，可把 backend display diagnostics、native metrics snapshot、startup 和 environment 合成为标准 report envelope。
- error-handling 已进入 report、analyzer、required signal policy、signal catalog、code target catalog 和 core-probe 路径；错误样本会报告为 `result = error`，而不是伪装成播放质量失败。
- `skip` 已进入 report、analyzer、signal catalog 和 runtime evidence collector 路径；当前评测器或 MVP 明确跳过的能力可以报告为 `result = skip`，并保留 `skip.*` 结构化原因，不再被误报为普通播放 telemetry 缺失。
- `source.container`、`source.bitrate` 和 `source.durationTicks` 已从 `EmbyMediaSource` 进入 report、model analysis、signal catalog 和 required-signal presence 检查，模型可以在 source metadata 层判断容器、码率和时长证据。
- `runtimeMetrics.status`、`runtimeMetrics.providerStatus`、`runtimeMetrics.reason`、`runtimeMetrics.hasSnapshot` 和 `runtimeMetrics.hasPlaybackSample` 已进入 report、model analysis、signal catalog 和 required-signal presence 检查；source-only baseline 会明确标记 `providerStatus = source-only`，模型可以先判断 runtime metrics 采集是否真实可用。
- `modelAnalysis` 已输出 `expectedBehavior` / `actualBehavior` 摘要，模型不需要只从分散的 `checks[].expected` / `checks[].actual` 推断 case 行为差异。
- `PlaybackQualityRunResult` envelope 已输出 `evaluationVersion = playback-quality-v0.1`，`modelAnalysis` 已输出 `primaryFailureClass`，便于模型直接判断报告契约版本和失败责任分类。
- `analyze-report-set` 的每个 case summary 已透传 `expectedBehavior`、`actualBehavior`、`primaryFailureClass` 和 `primaryFailureArea`，模型读取集合级报告时不必先展开单个 report envelope 才能定位主要差异。
- `analyze-report-set` 已输出集合级 `capabilityCoverage`，模型可以直接看到各 v0.1 能力的 evidence-present、partial、missing-evidence、blocked 或 not-observed 状态。

## 当前缺口

- 轨道切换目前主要是发现/选择状态证据，尚未证明切换后的 native 播放行为完整正确。
- 字幕 v0.1 只验证识别、选择和关闭状态，不验证最终视觉渲染正确性。
- duration 已经能从 Emby playback-info 的 media source `RunTimeTicks` 进入播放质量报告；chapters 仍未进入 `PlaybackDescriptor` / `EmbyMediaSource` 的播放质量报告路径，当前不能声称 v0.1 已覆盖章节识别。
- 缓冲、frame timing、A/V sync 和颜色信号仍依赖当前 native instrumentation 的覆盖度，后续需要继续补强证据质量。
- v0.1 尚未完成真实播放采集 baseline/candidate；当前 source-only baseline 只能证明评测链路闭环和缺失证据分类，不证明播放效果。
- WinRT App adapter 或独立真实播放 harness 尚未把 native `QualityMetrics()` 接入真实采集 baseline；当前 core-probe 仍是 deterministic probe telemetry。新增 `runtimeMetrics.*` 只能说明采集状态，不证明 native graph 已经产生真实播放指标。
- error-handling 目前能标准化错误 envelope，但真实 App/native harness 仍需要把实际异常、取消、超时和拒播原因映射到稳定 error code。

## 风险

- 缺失 telemetry 应归类为 `insufficient instrumentation`，不能直接当成播放器 bug。
- 私有 Emby 地址、账号、密码、真实 itemId/mediaSourceId 和本地报告只能放在 ignored 路径或环境变量中。
- 当前评测仍是纯软件闭环，不能证明 HDMI InfoFrame、显示器 EOTF 或肉眼颜色准确性。

## 下一步

优先补真实 App/native 或 native-graph 软件采集器，把 source-only baseline 中的缺失 telemetry 替换为实际播放 evidence；同时保持 core-probe 作为 App-free orchestration 回归守卫。
# 2026-07-07 更新：v0.1 core-probe 评测闭环

当前已经新增 `materialize-core-probe-report-set`，可以在不启动 App、不打包 UWP、不依赖 Xbox 或显示器的情况下，驱动 `PlaybackOrchestrator` 走 start、pause、resume、seek、track switch、subtitle switch 和 stop 路径，并生成标准 `PlaybackQualityRunResult` envelope。

已归档 `docs/qa/baselines/v0.1-core-probe/`：

- 9/9 reference case 生成 report。
- `validate-report-set` 结果为 `isValid = true`，`matchedCaseCount = 9`，error 数量为 0。
- `analyze-report-set` 结果为 `decision = no-change`，`blockedReportCount = 0`。
- Dolby Vision Profile 5 case 被标记为 `unsupported` / `unsupported-source`，不再误报为 color-pipeline 缺证据。
- `local/missing-file-error-handling` case 被标记为 `result = error` / `error-handling`，证明错误路径能进入模型可消费的一等报告。

边界仍需明确：core-probe 是实际 player core 软件评测，但它使用 in-process diagnostic backend，不打开 native playback graph，不解码真实媒体，不验证 HDMI / 显示器输出。它证明评测链路、case metadata、required signals、orchestrator 生命周期和模型报告结构已经闭合；它不证明真实播放质量、颜色准确性、帧率稳定性或 A/V sync 真实表现。

下一步应补 native graph 或真实媒体软件采集器，让 frame timing、decoder/rendered frames、buffering、A/V sync、color pipeline 从真实播放路径产生，而不是 deterministic probe telemetry。
