# 当前状态

播放质量评测体系正在推进 v0.1，目标是先把评测做成可信裁判，而不是优化播放效果。

## 2026-07-08 更新：DEBUG App-hosted quality-run 采集入口已接通

`DevelopmentNavigationCommand` 的 `route = quality-run` 现在不再只停留在解析层：

- DEBUG App 会把 `quality-run` dev-command 分发到 `PlaybackPage`，复用普通 item playback 路径打开 Emby item/media source。
- `PlaybackLaunchRequest` 现在保留 `qualityRunId`、采集窗口秒数、expected thresholds 和 command 接收时间。
- 播放成功后，`PlaybackPage` 会在采集窗口结束时读取当前 `PlaybackDescriptor`、backend display diagnostics 和 `native-winrt` metrics provider，并用 `PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult` 生成标准 `PlaybackQualityRunResult` envelope。
- App-hosted `quality-run` 现在会在采集窗口内主动执行 pause、resume、seek、stop，写入对应 lifecycle evidence，并把 seek target、actual position 和 seek error 作为 position evidence 交给 Core evaluator。
- 如果 DEBUG `quality-run` 在打开媒体或播放命令执行阶段抛异常，App 会写入标准 `ComposeErrorRunResult` envelope，避免 report-set 因缺少 captured report 而失去 case 级错误证据。
- App 侧 captured report 会写入 LocalFolder 下 `quality-run/captured/<runId>.json`；相对路径规则复用 Core 的 `PlaybackQualityCapturedReportPath`，与 CLI `materialize-native-harness-report-set --captured-reports-dir` 导入路径一致。
- 新增 `tools\quality-run\Export-AppQualityRunReports.ps1`，可从 Windows App LocalState 导出 `quality-run/captured` reports 到 ignored/private 目录，并保留 report-set 相对路径，方便后续用 `--captured-reports-dir` 导入。
- `PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult` 现在可接收 captured lifecycle 和 position evidence；App-hosted capture 会记录 `load`、`play`、`pause`、`resume`、`seek`、`stop` lifecycle 事件。

边界：这是 DEBUG-only App-hosted instrumentation/testability，不是 App 交互验证，也不是硬件 HDR/display 验证。它不会优化播放策略、改变阈值、修改 expected behavior，也不会证明颜色、帧率或 A/V sync 已正确；它只是让真实 App/native 播放会话能产出可导入现有 v0.1 链路的 raw evidence。

验证状态：新增 `PlaybackQualityCaptureContractTests` 和 `AppHostedQualityCaptureContractTests`，`dotnet test --filter FullyQualifiedName~PlaybackQuality` 已通过；`tools\quality-run\run-playback-core-checks.ps1` 已通过；Debug x64 UWP App 在执行 restore 后也已通过 MSBuild 编译。`run-playback-core-checks.ps1` 的 App diff guard 现在精确允许本次 DEBUG quality-run 接线所需的 App instrumentation 文件：`Playback/WinRtNativePlaybackEngine.cs`、`Navigation/PlaybackLaunchRequest.cs`、`MainPage.xaml.cs`、`Views/PlaybackPage.xaml.cs`；仍禁止 XAML、项目文件、manifest/package 和未列入的 App 改动。

## 2026-07-08 更新：native harness 已支持导入外部采集报告

`materialize-native-harness-report-set` 现在支持 `--captured-reports-dir`：

- 如果传入 captured report 目录，CLI 会按 manifest case 的标准 run-id 相对路径读取 raw `PlaybackQualityReport` 或已有 `PlaybackQualityRunResult` envelope。
- 导入报告会被归一化为当前标准 envelope，补入 manifest `caseMetadata`，用当前 analyzer 刷新 `modelAnalysis`，并写入 `--reports-dir`。
- `--source-revision`、`--player-core-version` 和 `--build-configuration` 会作为本轮归一化环境元数据写入 report；captured report 里已有的 collector version 会在未显式传入 `--collector-version` 时保留。
- 如果某个 manifest case 缺少 captured report，会生成 `report.result = skip`、`skip.code = native-harness.capture-missing`、`failureClass = insufficient instrumentation` 的标准 skip envelope，而不是让 report-set 缺 case。
- 导入模式会显式写入 `native-harness: imported captured playback evidence; CLI did not open native playback graph` limitation，避免模型误以为 CLI 自己执行了 native graph。

边界：这一步仍不实现真实 native 播放采集器，也不打开媒体、不解码、不验证 HDMI/display 输出、不伪造 runtime metrics。它解决的是“真实 App/native collector 一旦产出 report，如何进入现有 v0.1 report-set/validation/analyze/compare 链路”的缺口。

验证状态：`tools\quality-run\run-playback-core-checks.ps1` 已恢复通过。App diff guard 仍保护 `src/NextGenEmby.App`，但精确允许 DEBUG quality-run 和播放 metrics 采集所需的少量 instrumentation 文件。Plan 输出会暴露 `appDiffGuard.allowedPaths`，自动化模型可检查该例外没有扩展到 XAML、项目文件、manifest/package 或未列入的 App 改动。

## 2026-07-08 更新：track external/default/forced 元数据成为模型证据

已补齐轨道 external/default/forced 诊断链路：

- `EmbyMediaStream` 和 `PlaybackQualityTrack` 现在保留 nullable `IsDefault` / `IsForced`。
- `EmbyApiClient` 会从 Emby playback-info 的 `MediaStreams[].IsDefault` / `IsForced` 映射这些字段。
- `PlaybackQualityReportMapper` 会把 video/audio/subtitle 轨道的 external/default/forced 透传到 report。
- `PlaybackQualityReportAnalyzer` 会输出 `tracks.video.isExternal`、`tracks.video.isDefault`、`tracks.video.isForced`、`tracks.audio.isExternal`、`tracks.audio.isDefault`、`tracks.audio.isForced`、`tracks.subtitles.isExternal`、`tracks.subtitles.isDefault` 和 `tracks.subtitles.isForced` evidence signals。
- `PlaybackQualityRequiredSignalPolicy` 现在把 track/subtitle purpose 的 external/default/forced 当成 required signals；缺失时应先归类为 telemetry 缺口。
- source-only materializer 和 core-probe diagnostic source 会为诊断轨道写入明确 default/forced 值，避免 baseline 因测试样本本身缺字段而无法覆盖该证据链。

边界：这仍是 instrumentation/testability，不改变播放行为、轨道选择策略、音轨/字幕切换、字幕渲染、阈值或 pass/fail 规则。nullable 字段为 `null` 时表示采集器没有拿到证据，不能被模型解释成明确 `false`。

## 2026-07-08 更新：native-harness 缺口可物化为标准 skip 报告

已新增 `materialize-native-harness-report-set`：

- 该命令读取 reference manifest，并为每个 case 生成标准 `PlaybackQualityRunResult` envelope。
- 每个报告明确写入 `report.result = skip`、`skip.code = native-harness.not-implemented`、`skip.failureClass = insufficient instrumentation` 和 `skip.failureArea = evidence-collection`。
- 报告和 summary 都会带上 native-harness limitation，说明该命令没有打开 native playback graph，也没有执行真实媒体播放。
- `validate-report-set` 现在会把这类标准 skip envelope 当作有效的“未执行但原因明确”的评测证据，只要求 `skip.*` 和 `lifecycle.skip`，不再错误要求 source/timing/buffering/A/V sync/color playback telemetry。
- CLI JSON presence collector 已与 Core required-signal policy 对齐：`lifecycle.events[].status = skipped` 会被识别为 `lifecycle.skip`。
- `analyze-report-set` 现在会把包含 skip report 的集合判定为 `collect-comparable-evidence` / `risk = high` / `confidence.level = weak`，并通过 `skippedReportCount`、`result.skip` blocker 和 `evidence-collection` target 告诉模型应补真实采集器。
- 已归档 `docs/qa/baselines/v0.1-native-harness-skip/`：9/9 case 生成 skip envelope，`validate-report-set` 有效，`analyze-report-set` 指向高风险 evidence-collection。

边界：这不是 native 播放评测 harness，也不证明真实解码、渲染、帧率、缓冲、A/V sync 或色彩链路正确。它的价值是把“真实 native 采集器还没实现”这个缺口变成可版本化、可验证、可由模型消费的报告，而不是伪造指标或让 report-set 缺 case。

## 2026-07-08 更新：WinRT native metrics 接入 Core provider

已把 App 侧 `WinRtNativePlaybackEngine` 接入 `IPlaybackQualityMetricsProvider`：

- `WinRtNativePlaybackEngine.TryGetQualityMetrics` 会读取 native `NativePlaybackEngine.QualityMetrics()`。
- WinRT `NativePlaybackQualityMetrics` 的 render、decode、drop、buffer、clock、frame interval 和 A/V drift 字段会映射为 Core `PlaybackQualityMetricsSnapshot`。
- `NativeDirectXPlaybackBackend` 既有的 provider 委托路径现在可以在实际 App/native 播放会话中拿到 native graph metrics，而不是总是落到 provider 缺失。
- runtime metrics provider 现在会输出带身份的 `providerStatus`，例如 `native-winrt:returned-snapshot` 或 `core-probe:returned-snapshot`，避免模型把 deterministic probe telemetry 当成真实 native graph 证据。
- 这一步只接通已有诊断数据，不改变播放行为、native graph、阈值、source selection、pass/fail 规则或 baseline 规则。

边界：这还不是独立 native 播放评测 harness，也不证明 HDMI 输出、显示器 EOTF、HDR 肉眼效果或真实样本播放质量正确。它只是让真实 App/native 播放会话产生的 runtime metrics 能进入现有 `PlaybackQualityRuntimeEvidenceCollector` 和 JSON 报告链路。下一步仍需要一个 App-free 或最小 App-hosted 的真实播放采集器，实际打开样本并输出 `PlaybackQualityRunResult`。

## 2026-07-08 更新：结构化播放生命周期证据

已新增 `PlaybackQualityReport.lifecycle` 和 `modelAnalysis.lifecycle`：
- report 会保存 `lifecycle.events[]`，记录 `operation`、`status`、`state`、`positionTicks` 和 `message`。
- core-probe 现在会记录 `load`、`play`、`pause`、`resume`、`seek`、`stop`，音轨/字幕切换也会作为生命周期事件保存。
- `end-of-stream` 已进入 reference manifest required purpose 和 required-signal policy；core-probe 可输出 `lifecycle.endOfStream` diagnostic marker。
- error/skip 路径会输出对应的 lifecycle event，例如 `lifecycle.error` 或 `lifecycle.skip`。
- `PlaybackQualityReportAnalyzer` 会把生命周期事件转换为 `lifecycle.*` evidence signals，并在可播放报告缺少 `load/play/pause/resume/stop` 时标记 missing evidence。
- `validate-report-set` 的 required-signal policy 已要求可播放 case 提供生命周期证据；明确 unsupported 的 source 不要求播放生命周期。
- CLI JSON presence collector 已能从 `lifecycle.events[]` 提取 `lifecycle.*` 信号，手写 raw JSON report 也能被 report-set validation 正确识别。

这一步属于 instrumentation/testability，不改变播放器播放行为、阈值或 pass/fail 规则。它补齐的是 v0.1 “播放生命周期：load、play、pause、resume、stop、end-of-stream、error” 的机器可读证据入口；core-probe 的 `endOfStream` 只是 diagnostic marker，不证明真实媒体自然播放到 EOF。

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
- `docs/qa/baselines/v0.1-source-only/` 已归档 source-only baseline：9/9 case 有报告；DV Profile 5 预期 `unsupported` case 和 `error-handling` case 匹配，其余 7 个播放 case 因 103 个缺失 telemetry 失败，全部归类为 `insufficient instrumentation`。
- reference manifest case 已支持 `stable`、`challenge`、`quarantine` 分类，并在 validation、report-set status 和 run plan 中保留。
- reference manifest case 已支持 `severity` 和 `stability`，并在 validation、report-set status、run plan 和 baseline summary 中保留。
- `PlaybackQualityRunResult` envelope 已输出 `caseMetadata`，单个报告可直接暴露 case id、category、severity 和 stability。
- report-set validation errors 已输出 `failureClass`，可区分缺 telemetry、缺报告、重复/额外报告和 source metadata mismatch。
- App-free 验证命令为 `tools\quality-run\run-playback-core-checks.ps1`，当前结果为 pass。
- 本轮新增 tracks/subtitles telemetry：报告会记录视频轨、音轨、字幕轨数量、当前选中音轨/字幕轨、字幕关闭状态和轨道明细。
- reference manifest coverage 现在要求 `tracks` 和 `subtitles` purpose；默认公开 manifest 与私有 Emby manifest 生成脚本已同步更新。
- Core 已有可选 runtime metrics provider 和 runtime evidence collector，可把 backend display diagnostics、native metrics snapshot、startup 和 environment 合成为标准 report envelope；App 侧 WinRT native adapter 现在也会把 native `QualityMetrics()` 映射进该 provider 路径，并通过 `native-winrt:*` provider status 标明证据来源。
- error-handling 已进入 report、analyzer、required signal policy、signal catalog、code target catalog 和 core-probe 路径；错误样本会报告为 `result = error`，而不是伪装成播放质量失败。
- `skip` 已进入 report、analyzer、signal catalog 和 runtime evidence collector 路径；当前评测器或 MVP 明确跳过的能力可以报告为 `result = skip`，并保留 `skip.*` 结构化原因，不再被误报为普通播放 telemetry 缺失。
- `source.container`、`source.bitrate` 和 `source.durationTicks` 已从 `EmbyMediaSource` 进入 report、model analysis、signal catalog 和 required-signal presence 检查，模型可以在 source metadata 层判断容器、码率和时长证据。
- `source.chapterCount` 和 `source.chapters[]` 已从 Emby playback-info 的 media source chapters 进入 report、model analysis、signal catalog、required-signal presence 检查和 `metadata-duration` capability coverage；服务端未返回 chapters 时不伪造章节证据。
- `runtimeMetrics.status`、`runtimeMetrics.providerStatus`、`runtimeMetrics.reason`、`runtimeMetrics.hasSnapshot` 和 `runtimeMetrics.hasPlaybackSample` 已进入 report、model analysis、signal catalog 和 required-signal presence 检查；source-only baseline 会明确标记 `providerStatus = source-only`，模型可以先判断 runtime metrics 采集是否真实可用。
- `modelAnalysis` 已输出 `expectedBehavior` / `actualBehavior` 摘要，模型不需要只从分散的 `checks[].expected` / `checks[].actual` 推断 case 行为差异。
- `PlaybackQualityRunResult` envelope 已输出 `evaluationVersion = playback-quality-v0.1`，`modelAnalysis` 已输出 `primaryFailureClass`，便于模型直接判断报告契约版本和失败责任分类。
- `analyze-report-set` 的每个 case summary 已透传 `expectedBehavior`、`actualBehavior`、`primaryFailureClass` 和 `primaryFailureArea`，模型读取集合级报告时不必先展开单个 report envelope 才能定位主要差异。
- `analyze-report-set` 已输出集合级 `capabilityCoverage`，模型可以直接看到各 v0.1 能力的 evidence-present、partial、missing-evidence、blocked 或 not-observed 状态。

## 当前缺口

- 轨道切换目前主要是发现/选择状态证据，尚未证明切换后的 native 播放行为完整正确。
- 字幕 v0.1 只验证识别、选择和关闭状态，不验证最终视觉渲染正确性。
- duration 和服务端明确返回的 chapters 已能从 Emby playback-info 的 media source 进入播放质量报告；这只覆盖章节元数据识别，不覆盖章节 UI、章节跳转或按章节 seek 行为。
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

当前已经新增 `materialize-core-probe-report-set`，可以在不启动 App、不打包 UWP、不依赖 Xbox 或显示器的情况下，驱动 `PlaybackOrchestrator` 走 load、play、pause、resume、seek、track switch、subtitle switch、diagnostic end-of-stream marker 和 stop 路径，并生成标准 `PlaybackQualityRunResult` envelope。

已归档 `docs/qa/baselines/v0.1-core-probe/`：

- 9/9 reference case 生成 report。
- `validate-report-set` 结果为 `isValid = true`，`matchedCaseCount = 9`，error 数量为 0。
- `analyze-report-set` 结果为 `decision = no-change`，`blockedReportCount = 0`。
- Dolby Vision Profile 5 case 被标记为 `unsupported` / `unsupported-source`，不再误报为 color-pipeline 缺证据。
- `local/missing-file-error-handling` case 被标记为 `result = error` / `error-handling`，证明错误路径能进入模型可消费的一等报告。

边界仍需明确：core-probe 是实际 player core 软件评测，但它使用 in-process diagnostic backend，不打开 native playback graph，不解码真实媒体，不验证 HDMI / 显示器输出。它证明评测链路、case metadata、required signals、orchestrator 生命周期和模型报告结构已经闭合；它不证明真实播放质量、颜色准确性、帧率稳定性或 A/V sync 真实表现。

下一步应补 native graph 或真实媒体软件采集器，让 frame timing、decoder/rendered frames、buffering、A/V sync、color pipeline 从真实播放路径产生，而不是 deterministic probe telemetry。
