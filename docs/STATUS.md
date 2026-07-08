# 当前状态

播放质量评测体系正在推进 v0.1，目标是先把评测做成可信裁判，而不是优化播放效果。

## 2026-07-08 更新：App-free native-headless helper 已产生真实 native/software playback evidence

`tools/NextGenEmby.PlaybackQuality.Headless` 现在支持 `--native-helper-exe`。传入由 smoke 编译出的 `NativePlaybackGraphHeadlessSmokeTests.exe` 时，headless harness 会在不启动、不打包、不部署 UWP App 的情况下调用 native helper，打开本地生成的声明样本，执行最小生命周期 `load/play/pause/resume/seek/stop`，解析 native metrics，并输出标准 `PlaybackQualityRunResult`。

`tools/quality-run/run-native-headless-harness-smoke-test.ps1` 已覆盖两条路径：不传 helper 时继续输出结构化 `native-headless.native-link-blocked` skip，传 helper 时编译/运行 App-free native `PlaybackGraph` helper，生成 captured report，再走 `materialize-native-harness-report-set -> validate-report-set -> analyze-report-set`。最新 smoke 中 `analyze-report-set` 识别到 `evidenceSources = ["native-headless:returned-snapshot"]`，`playbackEvidence.scope = native-software`，`canEvaluateNativePlayback = true`。

当前真实 helper report 仍会诚实暴露缺口：它能采集 decoded/rendered frames、render intervals、source codec/width/height/frameRate、生命周期和 runtime metrics provider；但仍缺 `colorPipeline.dxgiInput`、`display.refreshRateHz` 等更深 instrumentation，且不验证 HDMI/显示器/HDR 观感。该 report 可作为软件播放证据进入评测链路，但还不能作为“颜色、HDR、帧率或 A/V sync 已优化”的证明。

文档化运行命令：单项 smoke 使用 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-native-headless-harness-smoke-test.ps1`；本阶段完整门禁使用 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1`。stable smoke 使用本地生成样本来避免公网波动；公开 Jellyfin direct-uri 已作为 challenge 手动验证过，可真实打开并被 materialize / validate / analyze 消费，但不放进稳定门禁。

## 2026-07-08 更新：offscreen DirectX composition swapchain 已通过 native smoke

新增 `tests/NextGenEmby.Native.Tests/DxDeviceResourcesOffscreenTests.cpp`，并把 `native-dx-offscreen-test` 纳入 `tools/quality-run/run-playback-core-checks.ps1`。该测试在不创建 UWP `SwapChainPanel` 的桌面进程里调用 `DxDeviceResources::CreateSwapChain(16, 16, false)`，随后验证 `HasRenderTarget()`、`ClearToBlack()` 和 `Present()`。

这一步把 App-free playback 的 surface blocker 收窄了：当前证据显示 composition swapchain/offscreen render target 本身可以在独立 native smoke 中创建和 present。后续真实 helper 已经基于这个前提打开本地样本、驱动 `PlaybackGraph` 生命周期，并把 metrics 写成 `PlaybackQualityRunResult`；如果未来该层回归，`native-dx-offscreen-test` 会先暴露 render target 前提失败。

边界：这仍不是“真实播放证据”。该 smoke 不打开媒体、不解码、不运行 `PlaybackGraph`、不产生 frame pacing / A/V sync / color pipeline runtime evidence，也不证明 HDR、颜色或显示输出正确。

## 2026-07-08 更新：RenderedVideoFrames 不再在 surface render/present 失败时累加

`VideoRenderer::Render` 现在返回是否真的把当前帧写入 back buffer，`PlaybackGraph::RenderNextFrame` 只有在 `Render(...)` 和 `Present()` 都成功时才记录 render interval 并累加 `RenderedVideoFrames`。这避免未来 headless/no-surface runner 只要成功解码就误报“已渲染帧”。

边界：这不是播放效果优化，也不代表 headless runner 已经能真实打开媒体；它只是让 native metrics 在缺失 surface 或 present 失败时更诚实地区分 decoded frame 与 rendered frame。

## 2026-07-08 更新：PlaybackGraph open request 已脱离 WinRT runtimeclass

`PlaybackGraph::Open` 现在接收普通 native `PlaybackGraphOpenRequest`，不再直接接收 `NextGenEmby::Native::NativePlaybackOpenRequest`。`NativePlaybackEngine` 负责把 WinRT/UWP request 转换成 graph request，再调用 `m_graph->Open(graphRequest)`。新增 `NativePlaybackGraphDecouplingContractTests` 防止 `PlaybackGraph.h` 重新 include `NativePlaybackEngine.g.h` 或把 WinRT runtimeclass 暴露回 graph open 参数。

边界：这一步只是把 WinRT runtimeclass 依赖从 native graph 内核往 adapter 层外推，降低后续 App-free native host 的耦合；它还没有创建 desktop/headless native host，也没有让 headless harness 真实打开 `PlaybackGraph`。

## 2026-07-08 更新：App-free native-headless harness 入口和结构化 blocker

新增 `tools/NextGenEmby.PlaybackQuality.Headless`，可以在不启动、不打包、不部署 UWP App 的情况下，用公开 direct-uri 输入生成标准 `PlaybackQualityRunResult` captured report。新增 smoke `tools/quality-run/run-native-headless-harness-smoke-test.ps1` 覆盖 `captured report -> materialize-native-harness-report-set import -> validate-report-set -> analyze-report-set`，并已纳入 `run-playback-core-checks.ps1` 的 App-free 验证计划。

当前该 harness 不会伪造 `native-headless:returned-snapshot`，也不会把 skip-only report 伪装成 native playback evidence。它输出 `native-headless.native-link-blocked` 结构化 skip，明确记录当前 blocker：`NextGenEmby.Native` 仍是 Windows Store C++/WinRT 组件，公开播放入口通过 UWP projection 暴露，surface API 绑定 `SwapChainPanel`；真实 App-free native open 需要先补一个 native graph host 或 render-surface 抽象。

边界：这一步证明的是 App-free report 入口、导入、校验和分析链路已经能消费 headless runner 产物；它仍不打开真实 native graph，不解码，不渲染，不产生 frame pacing / A/V sync / color pipeline 证据，因此 `analyze-report-set` 必须保持 `playbackEvidence.canEvaluateNativePlayback = false`。

## 2026-07-08 更新：App-free native evidence provider 身份进入 playbackEvidence 判断

`analyze-report-set` 的 `playbackEvidence` 判断现在不再只识别 `native-winrt:*`，也会把 `native-headless:*` 和 `native-win32-harness:*` 视为 native/software playback evidence provider。CLI smoke 新增了 `native-headless:returned-snapshot` 变体，确认这类 App-free provider 会输出 `playbackEvidence.scope = native-software`、`status = available` 和 `canEvaluateNativePlayback = true`。

边界：这是 evidence classification 变更，不打开媒体、不运行 native graph、不解码、不渲染，也不证明 HDR、颜色、帧率或 A/V sync 正确。`native-headless:*` 只有在后续真实 App-free harness 写入 captured report 时才代表对应软件播放证据；不能用 source-only、core-probe 或外部工具报告冒充该 provider。

## 2026-07-08 更新：公开 direct-uri case 可进入 App-hosted quality-run

`plan-runs` 现在会为 HTTP/HTTPS `direct-uri` case 生成 `devCommand.route = quality-run`，并把 manifest `uri` 写入 `devCommand.streamUrl`。DEBUG App 的 `quality-run` 路径现在也接受 `itemId` 或 `streamUrl` 二选一：有 `itemId` 时继续走 Emby item playback；只有 `streamUrl` 时走 direct stream native playback，并复用同一套 App-hosted pause/resume/seek/stop 采集和 `quality-run/captured/<runId>.json` 报告写入路径。

边界：这是 instrumentation/testability 变更，目的是让公开 Jellyfin 等直链样本可以产出 App/native 软件播放报告，不需要把私人 Emby item 写入仓库。direct-uri 路径不会从文件名推断 HDR/DV/color metadata；如果当前 App/native 链路没有实际解析出 codec、color、track 等源证据，报告应继续暴露为缺 instrumentation，而不是伪装成 pass。

## 2026-07-08 更新：evaluate-candidate 增加 playback evidence 门禁

`evaluate-candidate` 现在会在 `baseline-report-analysis` / `candidate-report-analysis` 之后、`suite` 比较之前增加 `baseline-playback-evidence` 和 `candidate-playback-evidence` 两个门禁。它们直接读取 `analyze-report-set` 输出的 `playbackEvidence.canEvaluateNativePlayback`：只有 baseline 和 candidate 都包含 native/App 软件播放证据时，才允许进入 before/after suite 比较。

source-only baseline 和 core-probe baseline 仍然有价值，但不能再被误用成 native playback candidate evidence。source-only 会被归类为缺播放证据，core-probe 会被归类为 orchestration-only；这两类 report-set 在候选评测中会停在 playback evidence gate，且不会产出 comparison 目录。导入的 `native-winrt:*` captured report 仍可通过该门禁，但只代表软件层 App/native playback evidence，不证明 HDMI、显示器 EOTF 或人工观感。

边界：这是 candidate evaluation 契约增强，不改变播放行为、native graph、report-set validation、analyzer decision、阈值、expected behavior、comparison scoring 或候选采纳规则。它只防止模型在证据范围不足时继续做播放 core 优化判断。

## 2026-07-08 更新：report-analysis summary 增加 playbackEvidence 范围判断

`analyze-report-set` 的集合级 JSON 现在会输出 `playbackEvidence`。它从 `evidenceSources[]`、`limitations[]` 和 skip 数量派生当前报告集的播放证据范围：source-only baseline 会标记为 `scope = source-only` / `status = missing`；core-probe baseline 会标记为 `scope = orchestration-only` / `status = limited`；native-harness skip 会标记为 `scope = none` / `status = missing`；导入的 `native-winrt:*` captured report 会标记为 `scope = native-software` / `status = available`。

这让模型不需要只靠 `decision`、`risk` 或 capability `evidence-present` 判断证据强度。特别是 core-probe 仍可作为 App-free orchestrator 回归守卫，但 `playbackEvidence.canEvaluateNativePlayback = false` 会明确阻止它被误当成真实 native decode/render、frame pacing、A/V sync 或 color 管线证据。

## 2026-07-08 更新：report-analysis summary 聚合证据来源和限制

`analyze-report-set` 的集合级 JSON 现在会输出 `evidenceSources[]` 和 `limitations[]`。`evidenceSources[]` 聚合每个报告里明确的 `runtimeMetrics.providerStatus`，例如 `source-only` 或 `core-probe:returned-snapshot`；默认 `unknown` 不会被当成证据来源。`limitations[]` 聚合 report-level limitation，模型不需要展开每个 case 就能看到当前报告集是否来自 source-only、core-probe、native-harness import/skip 或其他受限采集路径。

边界：这是模型消费契约增强，不改变播放行为、native graph、阈值、expected behavior、report-set pass/fail 或候选采纳规则。`core-probe:returned-snapshot` 仍只证明 deterministic in-process probe 产生了软件诊断指标，不证明真实 native decode/render、HDMI 输出、颜色准确性或 A/V sync 真实表现。

## 2026-07-08 更新：runtime metrics 采集状态进入 playable report-set gate

`PlaybackQualityRequiredSignalPolicy` 现在会对非 error、非明确 unsupported 的可播放 case 要求 `runtimeMetrics.status`、`runtimeMetrics.providerStatus`、`runtimeMetrics.hasSnapshot` 和 `runtimeMetrics.hasPlaybackSample`。这让 report-set validation 可以在比较或候选评测前识别“报告缺少 runtime metrics 采集状态”这一类 instrumentation 缺口。

`HasSnapshot = false` 或 `HasPlaybackSample = false` 不是缺字段；只要 `runtimeMetrics.status` 明确为 `unavailable`、`empty-snapshot` 或 `captured`，这些布尔值就算作采集状态证据。是否允许模型优化播放 core 仍由 `modelAnalysis.optimizationGate` 决定：`unavailable` 和 `empty-snapshot` 会继续作为 evidence-collection blocker。

边界：这是 report-set evaluation contract 变更，不改变播放行为、native graph、阈值、expected behavior 或 pass/fail 标准。error-handling case 和明确 unsupported source 不要求 runtime playback sample。

## 2026-07-08 更新：source raw color expectation 进入 manifest/report-set gate

`PlaybackQualityExpected` 现在可以声明 `videoRange`、`colorPrimaries`、`colorTransfer` 和 `colorSpace`。当 reference manifest 明确写出这些 expected 值时，`PlaybackQualityEvaluator` 会逐项比较 `report.source` 的实际值，`PlaybackQualityRequiredSignalPolicy` 也会把对应的 `source.*` 字段加入 required signals；如果 manifest 没有写出这些字段，则不会强制要求采集器伪造。

私有 Emby manifest 生成器现在会从 playback-info 的视频流字段透传这些 raw source color metadata，公开示例 manifest 和三套 baseline 已刷新。source-only/core-probe 只会把 manifest 中明确声明的 expected 值写入 synthetic diagnostic source，用于验证评测链路；真实 App/native collector 后续仍必须从服务端或实际解析路径提供这些字段。

边界：这是 evaluation contract/testability 变更，不改变播放行为、源选择、HDR/DV 策略、DXGI conversion、阈值或 pass/fail 标准。评测器仍不得从文件名、`HdrKind`、`DisplayTitle` 或 profile 分类反推出 raw color metadata。

## 2026-07-08 更新：source raw color metadata 进入可选证据链

`PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion` 已升级到 5。`EmbyMediaStream` 现在保留 Emby playback-info 的 `MediaStreams[].VideoRange`、`ColorPrimaries`、`ColorTransfer` 和 `ColorSpace`，`PlaybackQualityReportMapper` 会把选中视频源的这些 raw color metadata 透传到 `report.source`，`PlaybackQualityReportAnalyzer` 会输出 `source.videoRange`、`source.colorPrimaries`、`source.colorTransfer` 和 `source.colorSpace` evidence signals。

边界：这是 instrumentation/testability 和报告契约变更，不改变播放行为、源选择、HDR/DV 策略、DXGI color conversion、阈值或 pass/fail 规则。评测器不得从文件名、`HdrKind`、`DisplayTitle` 或 profile 分类反推这些 raw color 字段；只有服务端或采集器明确提供的值才算证据。后续的 source raw color expectation gate 已把 manifest 明确声明的字段纳入 required-signal 检查；未声明字段仍保持可选。

## 2026-07-08 更新：音频声道数进入轨道证据链

`PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion` 已升级到 4。`EmbyMediaStream` 和 `PlaybackQualityTrack` 现在保留 Emby playback-info 的 `MediaStreams[].Channels`，`PlaybackQualityReportMapper` 会把音轨声道数透传到 report，`PlaybackQualityReportAnalyzer` 会输出 `tracks.audio.channels` evidence signal。track/subtitle purpose 的 `requiredSignals` 现在要求 `tracks.audio.channels`；缺失时应归类为 `insufficient instrumentation`，不能从 `ChannelLayout` 或 `DisplayTitle` 推断。

边界：这是 instrumentation/testability 和报告契约变更，不改变播放行为、音轨选择、解码、转码、阈值或 pass/fail 规则。诊断样本和 baseline 会显式写入声道数，用于让模型区分“服务端/采集器提供了声道数证据”和“只有文本 layout/title 可读”。

## 2026-07-08 更新：direct stream locator 进入 source 证据链

`PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion` 已升级到 3。`report.source` 和 `modelAnalysis.source` 现在暴露 `hasDirectStreamUrl` 与 `directStreamProtocol`，只记录是否存在 direct stream URL 和协议/scheme，不记录完整 URL、query string、token 或个人服务地址。非 error case 的 `requiredSignals` 现在要求 `source.hasDirectStreamUrl` 和 `source.directStreamProtocol`；缺失时会作为 `insufficient instrumentation` 阻断 report-set，而不是被解释为播放质量问题。

边界：这是 instrumentation/testability 与报告契约变更，不改变播放行为、源选择策略、转码策略、HDR/DV 策略或 pass/fail 阈值。该版本对应 analyzer version 3；当前最新 analyzer version 见上方音频声道数更新。source-only baseline 仍用于证明缺运行时 telemetry 时会被如实 gate，结论没有因为新增 locator 证据而被放宽。

## 2026-07-08 更新：模型消费输出统一带 evaluationVersion

manifest validation、report-set validation、single comparison、comparison suite、run plan、materialized report-set summary、report-analysis summary 和 candidate evaluation 现在都会输出 `evaluationVersion = playback-quality-v0.1`。

边界：这是 JSON 契约版本信号，不改变播放器行为、case 分类、阈值或比较算法。模型应同时检查 `schemaVersion` 和 `evaluationVersion`：前者描述 JSON shape，后者描述评测合同。

## 2026-07-08 更新：player identity 进入 report-set gate

`validate-report-set` 现在要求每个 report 携带 `environment.playerCoreVersion` 和 `environment.sourceRevision`。缺失、null 或空白值会输出 `report.environment.missing`，并归类为 `insufficient instrumentation`。`plan-runs` 也会在 `evidenceRequirements` 中要求每个采集报告带上这两个身份字段。

边界：这是最终 report-set 的可追溯性要求，不改变播放器行为。它保证后续模型进行 baseline/candidate 比较时，能知道证据来自哪个播放器版本和源码修订。

## 2026-07-08 更新：failureArea 枚举进入 report-set gate

`validate-report-set` 现在会校验 `analysis.primaryFailureArea`、`error.failureArea`、`skip.failureArea` 和 `checks.failureArea` 是否属于当前 area catalog。未知值会输出 `report.failureArea.invalid`，并归类为 `evaluation harness bug`。

边界：这是报告契约校验，不改变播放器行为或 pass/fail 阈值。它保证模型消费的 failure area 能稳定映射到已有诊断模块和 code target catalog。

## 2026-07-08 更新：report result 枚举进入 report-set gate

`validate-report-set` 现在会校验 `report.result` 是否属于 v0.1 最终结果枚举：`pass`、`fail`、`skip`、`unsupported`、`error`。未知值会输出 `report.result.invalid`，并归类为 `evaluation harness bug`。

边界：这是 report-set 契约校验，不改变播放器行为、阈值或 expected behavior。`PlaybackQualityReport` 的默认 `observed` 仍可作为中间采集状态存在，但不能进入最终可比较 report-set。

## 2026-07-08 更新：failureClass 枚举进入 report-set gate

`validate-report-set` 现在会校验 `error.failureClass`、`skip.failureClass` 和 `checks.failureClass` 是否属于当前枚举：`player-core bug`、`unsupported by current MVP`、`evaluation harness bug`、`sample issue`、`environment issue`、`external service/protocol issue`、`insufficient instrumentation`、`ambiguous expectation`、`flaky / nondeterministic`、`needs human confirmation`。未知值会输出 `report.failureClass.invalid`，并归类为 `evaluation harness bug`。

边界：这是报告契约校验，不改变播放器行为或 pass/fail 阈值。它防止 typo 或临时分类进入模型消费链路。

## 2026-07-08 更新：analyzer version 2 与 baseline 刷新

`PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion` 此前升级到 2，用于标记 `source.hasChapterMetadata`、nullable `source.chapterCount` 和章节 metadata presence 语义进入模型分析契约。对应 baseline 已刷新过；当前最新 analyzer version 见上方音频声道数更新。

边界：这不是播放效果优化，也不改变阈值、expected behavior 或 pass/fail 规则。source-only baseline 仍用于暴露缺运行时 telemetry；native-harness-skip 仍表达真实 native collector 尚未实现；core-probe 仍是 App-free/in-process 的 core orchestration 诊断路径，不代表真实解码、显示或 A/V sync 效果。

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
- `PlaybackQualityReportAnalyzer` 会输出 `tracks.video.isExternal`、`tracks.video.isDefault`、`tracks.video.isForced`、`tracks.audio.isExternal`、`tracks.audio.channels`、`tracks.audio.isDefault`、`tracks.audio.isForced`、`tracks.subtitles.isExternal`、`tracks.subtitles.isDefault` 和 `tracks.subtitles.isForced` evidence signals。
- `PlaybackQualityRequiredSignalPolicy` 现在把 track/subtitle purpose 的 audio channels 和 external/default/forced 当成 required signals；缺失时应先归类为 telemetry 缺口。
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
- `docs/qa/baselines/v0.1-source-only/` 已归档 source-only baseline：9/9 case 有报告；DV Profile 5 预期 `unsupported` case 和 `error-handling` case 匹配，其余 7 个播放 case 因 104 个缺失 telemetry 失败，全部归类为 `insufficient instrumentation`。
- reference manifest case 已支持 `stable`、`challenge`、`quarantine` 分类，并在 validation、report-set status 和 run plan 中保留。
- reference manifest case 已支持 `severity` 和 `stability`，并在 validation、report-set status、run plan 和 baseline summary 中保留。
- `PlaybackQualityRunResult` envelope 已输出 `caseMetadata`，单个报告可直接暴露 case id、category、severity 和 stability。
- report-set validation errors 已输出 `failureClass`，可区分缺 telemetry、缺报告、重复/额外报告和 source metadata mismatch。
- App-free 验证命令为 `tools\quality-run\run-playback-core-checks.ps1`，当前结果为 pass。
- 本轮新增 tracks/subtitles telemetry：报告会记录视频轨、音轨、字幕轨数量、音频声道数、当前选中音轨/字幕轨、字幕关闭状态和轨道明细。
- reference manifest coverage 现在要求 `tracks` 和 `subtitles` purpose；默认公开 manifest 与私有 Emby manifest 生成脚本已同步更新。
- Core 已有可选 runtime metrics provider 和 runtime evidence collector，可把 backend display diagnostics、native metrics snapshot、startup 和 environment 合成为标准 report envelope；App 侧 WinRT native adapter 现在也会把 native `QualityMetrics()` 映射进该 provider 路径，并通过 `native-winrt:*` provider status 标明证据来源。
- error-handling 已进入 report、analyzer、required signal policy、signal catalog、code target catalog 和 core-probe 路径；错误样本会报告为 `result = error`，而不是伪装成播放质量失败。
- `skip` 已进入 report、analyzer、signal catalog 和 runtime evidence collector 路径；当前评测器或 MVP 明确跳过的能力可以报告为 `result = skip`，并保留 `skip.*` 结构化原因，不再被误报为普通播放 telemetry 缺失。
- `source.container`、`source.bitrate` 和 `source.durationTicks` 已从 `EmbyMediaSource` 进入 report、model analysis、signal catalog 和 required-signal presence 检查，模型可以在 source metadata 层判断容器、码率和时长证据。
- `source.hasDirectStreamUrl` 和 `source.directStreamProtocol` 已从 `EmbyMediaSource.DirectStreamUrl` 进入 report、model analysis、signal catalog 和 required-signal presence 检查；报告只保留非敏感 locator evidence，不保留完整 URL。
- `source.hasChapterMetadata`、`source.chapterCount` 和 `source.chapters[]` 已从 Emby playback-info 的 media source chapters 进入 report、model analysis、signal catalog、required-signal presence 检查和 `metadata-duration` capability coverage；服务端未返回 chapters 字段、明确返回空章节列表、返回章节明细三种情况会被区分，不把缺字段伪装成 0 章。
- `runtimeMetrics.status`、`runtimeMetrics.providerStatus`、`runtimeMetrics.reason`、`runtimeMetrics.hasSnapshot` 和 `runtimeMetrics.hasPlaybackSample` 已进入 report、model analysis、signal catalog 和 required-signal presence 检查；source-only baseline 会明确标记 `providerStatus = source-only`，模型可以先判断 runtime metrics 采集是否真实可用。
- `modelAnalysis` 已输出 `expectedBehavior` / `actualBehavior` 摘要，模型不需要只从分散的 `checks[].expected` / `checks[].actual` 推断 case 行为差异。
- `PlaybackQualityRunResult` envelope 已输出 `evaluationVersion = playback-quality-v0.1`，`modelAnalysis` 已输出 `primaryFailureClass`，便于模型直接判断报告契约版本和失败责任分类。
- `analyze-report-set` 的每个 case summary 已透传 `expectedBehavior`、`actualBehavior`、`primaryFailureClass` 和 `primaryFailureArea`，模型读取集合级报告时不必先展开单个 report envelope 才能定位主要差异。
- `analyze-report-set` 已输出集合级 `capabilityCoverage`，模型可以直接看到各 v0.1 能力的 evidence-present、partial、missing-evidence、blocked 或 not-observed 状态。

## 当前缺口

- 当前 readiness audit 见 `docs/qa/playback-quality-v0.1-readiness-audit.md`。结论是：v0.1 裁判框架和证据门禁已经基本成型，但尚不能宣称完成，因为还没有可复现的 native/App 软件播放 report-set 作为真实 playback evidence baseline。
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
