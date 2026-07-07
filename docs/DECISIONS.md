# 技术决策

## 2026-07-08: DEBUG App-hosted quality-run 作为真实播放采集入口

决策：复用现有 `DevelopmentNavigationCommand route = quality-run`，在 DEBUG App 中将其分发到 `PlaybackPage`。播放成功后，App 在命令指定的采集窗口内主动执行 pause、resume、seek、stop，读取当前 `PlaybackDescriptor`、`IPlaybackBackendDiagnostics.DisplayStatus` 和 `IPlaybackQualityMetricsProvider` metrics，通过 `PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult` 生成标准 `PlaybackQualityRunResult` envelope，并写入 App LocalFolder 的 `quality-run/captured/<runId>.json`。seek target、actual position 和 seek error 会作为 position evidence 在 evaluator 运行前写入 report。如果打开媒体或播放命令执行阶段失败，App 会通过 `ComposeErrorRunResult` 写入标准 error envelope。CLI 和 App 共享 `PlaybackQualityCapturedReportPath.GetReportRelativePath`，保证 App captured report 可以被 `materialize-native-harness-report-set --captured-reports-dir` 按同一 run-id 相对路径导入。

原因：v0.1 已经有 source-only、core-probe、native-harness skip 和 captured-report import，但缺少真实 App/native 播放会话产出 raw report 的入口。完全独立 App-free native harness 仍然成本较高；DEBUG App-hosted collector 是当前最小可行路径，因为它复用已经能登录、拿 Emby playback-info、创建 native graph、读 native metrics 的现有 App 播放链路，同时不新建并行评测框架。

边界：这是 instrumentation/testability 变更，不改变普通播放策略、source selection、HDR/DV 策略、阈值、expected behavior 或 pass/fail 规则。它也不验证 HDMI InfoFrame、显示器 EOTF 或肉眼颜色准确性。自动 pause/resume/seek/stop 只在 DEBUG `quality-run` 采集路径执行，用于证明生命周期操作和 position telemetry 能进入报告；不能把该序列当成普通用户播放行为。

影响：`run-playback-core-checks.ps1` 的 App diff guard allowlist 扩展为精确允许 DEBUG quality-run 接线文件：`Playback/WinRtNativePlaybackEngine.cs`、`Navigation/PlaybackLaunchRequest.cs`、`MainPage.xaml.cs`、`Views/PlaybackPage.xaml.cs`。这不是允许 UI 或交互工作进入本阶段；XAML、App project、manifest/package 和未列入 App 文件仍被阻断。

## 2026-07-08: playback-core validation guard 精确允许 App playback metrics adapter

决策：`tools\quality-run\run-playback-core-checks.ps1` 的 App diff guard 继续保护 `src/NextGenEmby.App`，但新增精确 allowlist。最初只允许 `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`；随后为了接通 DEBUG App-hosted `quality-run` collector，allowlist 扩展到 `PlaybackLaunchRequest.cs`、`MainPage.xaml.cs` 和 `PlaybackPage.xaml.cs`。Plan 输出会暴露 `appDiffGuard.allowedPaths`，测试会确认 allowlist 没有包含 XAML、App project、manifest/package 或未列入 App 文件。

原因：当前 v0.1 评测链路已经把 `WinRtNativePlaybackEngine` 接入 `IPlaybackQualityMetricsProvider`，这是把真实 App/native 播放 metrics 带入 Core report 的 instrumentation/testability 改动。原 guard 把任何 App diff 都视为 UI/App 交互改动，导致 `run-playback-core-checks.ps1` 在当前合法评测分支上被阻断，反而破坏 v0.1 “有文档化命令可以运行”的完成标准。

边界：这不是允许 App UI 或交互改动进入 playback-core validation。XAML、项目文件、manifest、MSIX/package 相关路径仍被 guard 拦截。allowlist 只覆盖已记录的播放质量 instrumentation 接线；如果未来新增 App-hosted collector 文件或更多 App playback instrumentation，应单独记录决策并扩展测试，而不能泛化放行整个 App/Playback 或 Views 目录。

## 2026-07-08: native harness 命令支持导入 captured report

决策：`materialize-native-harness-report-set` 新增可选参数 `--captured-reports-dir`。不传该参数时，命令保持原有行为，为每个 manifest case 生成 `native-harness.not-implemented` skip envelope。传入该参数时，命令按 `caseId` 的标准相对路径读取 captured raw report 或 envelope，刷新当前 `modelAnalysis`，补入 manifest case metadata，并写入标准 report-set。缺少 captured report 的 case 会生成 `native-harness.capture-missing` skip envelope。

原因：当前 WinRT/native metrics 已经能进入 Core provider，但还缺真实 native playback collector。下一步的 App-hosted 或 native collector 不应绕过现有 v0.1 report-set/validation/analyze/compare 链路，也不应新建并行框架。先把“外部采集结果如何进入标准评测链路”做成稳定命令，可以让后续 collector 只负责采集 raw report，而由 CLI 负责归一化、case metadata、analysis refresh 和 report-set 完整性。

边界：导入模式不打开 native playback graph，不执行播放，不验证 HDMI/display 输出，不补造 timing/buffering/A/V sync/color 证据。它只消费 captured report 已经明确提供的字段；缺失字段仍由 `validate-report-set` 和 analyzer 如实报告为缺证据。命令写入 `native-harness: imported captured playback evidence; CLI did not open native playback graph` limitation，避免自动化模型误用证据来源。

影响：真实 App/native collector 现在有了明确落点：把每个 case 的 raw report 或 envelope 写到 ignored/private captured 目录，再用同一 `materialize-native-harness-report-set` 命令归一化成可版本化 report-set。skip-only native harness baseline 仍保留，用于表示 collector 未实现或未采集。

## 2026-07-08: track default/forced 元数据进入评测证据

决策：`EmbyMediaStream` 和 `PlaybackQualityTrack` 新增 nullable `IsDefault` / `IsForced`，`EmbyApiClient` 从 playback-info `MediaStreams[].IsDefault` / `IsForced` 映射，`PlaybackQualityReportMapper` 透传到 report，`PlaybackQualityReportAnalyzer` 输出 `tracks.video.isExternal`、`tracks.video.isDefault`、`tracks.video.isForced`、`tracks.audio.isExternal`、`tracks.audio.isDefault`、`tracks.audio.isForced`、`tracks.subtitles.isExternal`、`tracks.subtitles.isDefault` 和 `tracks.subtitles.isForced` evidence signals。track/subtitle purpose 的 required signals 现在也要求这些字段。

原因：v0.1 的轨道发现目标不只是知道轨道数量和 codec/language，还要让模型判断内置/外部轨、默认轨、强制字幕和轨道选择链路是否有足够证据。缺少 external/default/forced 时，模型无法区分“轨道明确是内置/非默认/非强制”和“采集器没有暴露该标记”。

边界：这是 instrumentation/testability 变更，不改变音轨/字幕选择、切换、播放、解码、渲染、阈值或 pass/fail 规则。字段使用 nullable bool；只有 Emby 或诊断 probe 明确提供值时才算证据，避免把缺失字段误报成 `false`。

影响：`materialize-baseline-report-set` 和 `PlaybackQualityOrchestratorProbe` 现在会为诊断样本写入明确的 track default/forced 标记。report-set validation 可以把缺失 default/forced 归类为 telemetry 缺口，而不是让模型根据轨道数量猜测。

## 2026-07-08: native-harness 缺口用标准 skip report-set 表达

决策：新增 CLI 命令 `materialize-native-harness-report-set`。在真实 App-free native playback harness 尚未实现前，该命令为 manifest 中每个 case 生成 `PlaybackQualityRunResult` envelope，结果固定为 `report.result = skip`，并写入 `skip.code = native-harness.not-implemented`、`skip.failureClass = insufficient instrumentation`、`skip.failureArea = evidence-collection`。报告和 summary 同时保留 `native-harness: native playback graph was not opened by this command` limitation。

原因：v0.1 需要 report-set 级闭环，但当前还不能在纯软件、App-free 条件下实际打开 native graph 并采集真实播放指标。如果继续让 native-harness 路径缺 report，模型只能看到缺 case 或缺 telemetry，无法判断这是采集器未实现还是播放器 core 失败。标准 skip envelope 可以把“未执行原因明确”变成机器可读事实，同时保留未来真实 harness 替换同一命令路径的空间。

边界：该命令不打开媒体、不执行 native playback、不伪造 timing/buffering/sync/display/color 指标，也不改变播放行为、阈值、expected behavior、case 分类或 pass/fail 规则。`validate-report-set` 对 `result = skip` 只要求 `skip.*` 和 `lifecycle.skip` 证据；缺少真实播放 telemetry 不应被归类为播放器 core bug。

影响：自动化模型现在可以在 native harness 尚未完成时得到完整 report-set，并明确下一步应补采集器而不是优化播放行为。CLI JSON presence collector 也与 Core required-signal policy 对齐，`lifecycle.events[].status = skipped` 会被识别为 `lifecycle.skip`。

补充：`analyze-report-set` 对包含 `result = skip` 的集合必须输出 `skippedReportCount`，并把集合级 action/decision 设为 `collect-comparable-evidence`、risk 设为 `high`、confidence 设为 `weak`。结构化 skip report 通过 validation 只说明“未执行原因已记录”，不说明该 report-set 已经产生可用于播放 Core 优化的真实证据。

## 2026-07-08: WinRT native QualityMetrics 接入 Core 评测 provider

决策：`WinRtNativePlaybackEngine` 实现 `IPlaybackQualityMetricsProvider`，调用已有 WinRT native `NativePlaybackEngine.QualityMetrics()`，并把 `NativePlaybackQualityMetrics` 字段映射为 Core `PlaybackQualityMetricsSnapshot`。`NativeDirectXPlaybackBackend` 已经会在底层 engine 实现 provider 时委托读取 metrics，因此 App/native 播放会话现在能复用 Core runtime evidence collector。新增 `IPlaybackQualityMetricsProviderIdentity`，让 provider status 写成 `native-winrt:returned-snapshot`、`native-winrt:returned-false` 或 `core-probe:returned-snapshot` 这类 `identity:outcome` 格式。

原因：此前 native IDL 已暴露 `QualityMetrics()`，Core backend 也有 provider 委托，但 App adapter 没有实现 `IPlaybackQualityMetricsProvider`。这导致真实 App/native 播放路径不会把 native graph metrics 带入 `PlaybackQualityRuntimeEvidenceCollector`，模型只能看到 provider 缺失或 probe/deterministic telemetry，无法把真实 runtime evidence 接到 v0.1 报告链路。同时，单纯的 `returned-snapshot` 不能说明 snapshot 来自 native graph 还是 core-probe，自动优化容易误用证据。

边界：这是 instrumentation/testability 变更，不改变播放行为、native graph、阈值、expected behavior、case 分类、pass/fail 规则或 baseline。provider 返回空 snapshot 时仍由既有 `runtimeMetrics.empty-snapshot` gate 阻断播放优化；provider 抛异常或 native 不可用时返回 false，并由报告链路归类为 runtime metrics unavailable。

影响：实际 App/native 播放会话具备了把 frame timing、drop/starvation、audio/video clock 和 drift counters 写入 Core report 的通路。v0.1 仍缺独立真实播放采集器；core-probe baseline 仍是 deterministic telemetry，不能因此被解释成真实播放质量证据。

## 2026-07-08: lifecycle evidence 成为一等播放质量信号
决策：`PlaybackQualityReport` 新增 `lifecycle.events[]`，`PlaybackQualityModelAnalysis` 新增 `lifecycle` assessment。每个事件记录 `operation`、`status`、`state`、`positionTicks` 和 `message`；analyzer 会输出 `lifecycle.*` evidence signals。`PlaybackQualityOrchestratorProbe` 会记录 load/play/pause/resume/seek/stop、音轨/字幕切换，以及 `end-of-stream` case 的 diagnostic `lifecycle.endOfStream` marker；error/skip collector 会记录 `lifecycle.error` / `lifecycle.skip`。

原因：此前 core-probe 确实执行了 start、pause、resume、seek 和 stop，但 report 只能通过 startup、position、tracks 等字段间接推断生命周期。v0.1 的报告消费对象是模型，模型需要直接知道生命周期操作是否被观察到、哪些操作缺失、错误是否发生在生命周期中，而不是从分散 telemetry 自行猜。

边界：这是 instrumentation/testability 变更，不改变播放器行为、native graph、阈值、expected behavior 或 pass/fail 规则。可播放 case 的 required signals 现在要求 `lifecycle.load/play/pause/resume/stop`，seek/timeline case 额外要求 `lifecycle.seek`，`end-of-stream` case 额外要求 `lifecycle.endOfStream`；明确 unsupported 的 source 不要求播放生命周期。core-probe 的 `endOfStream` 只是 diagnostic marker，不证明真实媒体自然播放到 EOF；真实 EOF 仍需要 native graph 或真实媒体软件采集器证明。

影响：CLI 的 JSON presence collector 会从 `lifecycle.events[]` 提取 `lifecycle.*`，因此 raw JSON report 也能通过 report-set validation 暴露生命周期证据。`capabilityCoverage.lifecycle` 改为直接聚合 lifecycle 信号，不再用 startup/error/skip operation 作为代理。

## 2026-07-08: report-set capability coverage 进入集合级分析

决策：`analyze-report-set` 的 report-analysis summary 新增 `capabilityCoverage`。它按 v0.1 关键能力聚合 `requiredSignals`、`evidenceSignals`、`missingSignals`、`blockers`、`caseIds`、`suggestedNextActions` 和 capability `status`，状态取值为 `evidence-present`、`partial`、`missing-evidence`、`blocked` 或 `not-observed`。

原因：此前模型读取集合级报告时只能看到全局 `signals`、`failureAreas` 和每个 case 的 blockers。它能定位某个失败 case，但不能快速判断“这组报告是否已经覆盖 runtime metrics、frame pacing、A/V sync、color、tracks/subtitles 等 v0.1 能力”。这会让后续自动优化先花时间展开每个 report，自行拼 capability 覆盖，容易漏掉缺证据项。

边界：该变更只扩展 CLI summary 的诊断索引，不改变单报告 analyzer、evaluator、阈值、expected behavior、case 分类、pass/fail 规则或播放器行为。`capabilityCoverage.status = evidence-present` 只表示报告集里存在相关软件证据，不表示真实播放效果正确；`blocked` 或 `missing-evidence` 表示模型应先补采集或报告证据。

## 2026-07-08: runtime metrics 采集状态进入模型报告

决策：`PlaybackQualityReport` 新增 `runtimeMetrics` section，记录 `status`、`providerStatus`、`reason`、`hasSnapshot` 和 `hasPlaybackSample`。`PlaybackQualityRuntimeEvidenceCollector` 会把 provider 未提供、provider 返回 false、空 snapshot、含播放样本 snapshot 分别写成结构化状态；`PlaybackQualityReportAnalyzer`、signal catalog 和 required-signal presence 检查同步识别 `runtimeMetrics.*`。

原因：此前 `PlaybackQualityMetricsSnapshot` 进入 report 后，模型只能看到 timing/sync/buffer counters 是否为 0 或缺失，无法区分“真实播放采样就是 0”、“provider 没接上”、“provider 返回 false”或“返回了空 snapshot”。这会导致模型把 instrumentation 缺口误判为播放器 core 行为问题，或者把默认 0 当作可用播放证据。

边界：该变更只增加采集状态证据，不改变播放行为、native graph、阈值、expected behavior、case 分类或 pass/fail 规则。`runtimeMetrics.hasSnapshot = true` 不等于真实播放质量可信；模型必须同时检查 `runtimeMetrics.hasPlaybackSample` 和 timing/sync/buffer 具体证据。`status = unknown` 的默认对象不能被当成证据信号。

补充：`materialize-baseline-report-set` 是明确的 source-only materializer，不执行播放、不连接 metrics provider。因此普通播放 case 会写入 `runtimeMetrics.status = unavailable` 和 `providerStatus = source-only`。这不是播放器 core 失败，也不是播放采样结果；它让模型直接识别该 baseline 只能用于 report-shape 和缺证据分类，不能用于播放效果优化。

补充：`PlaybackQualityReportAnalyzer` 的 `optimizationGate` 会把 `runtimeMetrics.status = unavailable` 归为 `runtimeMetrics.unavailable` blocker，把 `empty-snapshot` 归为 `runtimeMetrics.empty-snapshot` blocker。这样即使某个报告其它字段看起来足够，模型也必须先补采集链路，不能用没有 runtime playback sample 的报告调播放 core。

## 2026-07-08: source duration ticks 进入播放质量报告

决策：`EmbyMediaSource` 新增 `RunTimeTicks`，`PlaybackQualitySource` 和 `PlaybackQualityModelAnalysis.Source` 新增 `durationTicks`，`PlaybackQualityReportMapper` 从 `EmbyMediaSource.RunTimeTicks` 映射，signal catalog 和 required-signal presence 检查新增 `source.durationTicks`。

原因：v0.1 覆盖能力要求 metadata/duration 识别。Emby playback-info 的 media source 可以暴露 `RunTimeTicks`，此前 Core 没有把它保留下来，模型无法在 source metadata 层判断实际选中源是否带有可用时长证据。

边界：该变更只透传服务端已提供的 media-source duration，不改变播放行为、源选择策略、阈值、expected behavior 或 pass/fail 规则。服务端未返回 `RunTimeTicks` 时报告保持缺失/0；评测器不得从文件名、采样时长或默认运行时长推断 duration。chapters 已在后续决策中单独接入。

## 2026-07-08: source chapters 进入 metadata 评测证据

决策：`EmbyMediaSource` 新增 `HasChapterMetadata` 与 `Chapters`，`EmbyApiClient` 从 playback-info media source 的 `Chapters[]` 映射 `Name`、`StartPositionTicks` 和 `ImageTag`，并保留 chapters 字段是否被服务端明确返回。`PlaybackQualitySource` 和 `PlaybackQualityModelAnalysis.Source` 新增 `hasChapterMetadata`、nullable `chapterCount` 与 `chapters[]`，`PlaybackQualityReportMapper` 透传，signal catalog、required-signal presence 检查和 `analyze-report-set` 的 `metadata-duration` capability coverage 新增 `source.hasChapterMetadata`、`source.chapterCount`、`source.chapters.startPositionTicks`、`source.chapters.name` 等证据。

原因：v0.1 覆盖能力要求 metadata/duration 识别，章节属于播放 core 后续实现 resume/seek/章节跳转时的重要时间轴元数据。此前状态文档已明确 chapters 未进入 `PlaybackDescriptor` / `EmbyMediaSource` 的播放质量报告路径，模型无法区分“服务端没有章节数据”和“评测器没有记录章节数据”。

边界：这是 metadata instrumentation，不改变播放行为、章节跳转、seek、source selection、阈值或 pass/fail 规则。只有服务端在 playback-info media source 中明确返回 chapters 字段，或采集器明确写入 chapter metadata，才记录章节证据；缺失 chapters 字段、空 chapters 数组和有章节明细是不同状态。评测器不从文件名、时长、采样结果或 UI 文本推断章节。`StartPositionTicks = 0` 是有效章节起点，不能按缺失处理。

## 2026-07-07: source container/bitrate 进入播放质量报告

决策：`PlaybackQualitySource` 和 `PlaybackQualityModelAnalysis.Source` 新增 `container` 与 `bitrate`，`PlaybackQualityReportMapper` 从 `EmbyMediaSource.Container` / `EmbyMediaSource.Bitrate` 映射，signal catalog 和 required-signal presence 检查新增 `source.container` / `source.bitrate`。

原因：v0.1 覆盖能力要求 metadata 和媒体能力识别。容器和码率已经由 Emby playback-info 暴露到 Core，但此前没有进入播放质量报告，模型只能看到 codec、resolution、frame rate 和 HDR 分类，无法判断容器或码率相关 source-selection / protocol 问题。

边界：该变更只接入已有 Core source metadata，不改变播放行为、源选择策略、阈值或 expected behavior。duration 和 chapters 已在后续决策中单独接入；仍不能由评测器伪造服务端未提供的 metadata。

## 2026-07-07: skip 作为一等评测结果

决策：`PlaybackQualityReport` 新增 `skip` section，`PlaybackQualityRuntimeEvidenceCollector` 新增 `ComposeSkipRunResult`。当前评测器、采集器或 MVP 明确不能执行的 case，应生成 `report.result = skip` 的标准 envelope，并保留 `skip.code`、`skip.reason`、`skip.operation`、`skip.failureClass`、`skip.failureArea`、`skip.isExpected` 和 `skip.isRetriable`。

原因：v0.1 目标要求报告状态覆盖 `pass`、`fail`、`skip`、`unsupported` 和 `error`。没有一等 `skip` 时，模型只能把未执行 case 误读为缺 telemetry、失败或人工备注，无法区分“能力边界/采集器边界已知”与“播放器 core 真实播放质量差”。

边界：`skip` 不代表播放成功，也不进入播放质量调参；它只记录为什么没有产生对应播放证据。`PlaybackQualityReportAnalyzer` 对 `result = skip` 只要求结构化 `skip.*` 证据，不要求 source、startup、timing、buffering、A/V sync 或 color telemetry。

## 2026-07-07: report-set case summary 暴露行为摘要和主失败分类

决策：`analyze-report-set` 输出的每个 case summary 透传 `modelAnalysis.expectedBehavior`、`modelAnalysis.actualBehavior`、`modelAnalysis.primaryFailureClass` 和 `modelAnalysis.primaryFailureArea`。

原因：v0.1 的报告消费对象是模型。模型查看集合级报告时，应能直接从 `cases[]` 判断每个 case 的预期、实际、主失败责任和主失败区域；否则需要先展开每个 report envelope 才能做下一步决策，容易遗漏集合级 blockers 与单 case 行为差异之间的关系。

边界：该变更只扩展 CLI JSON summary，不改变播放行为、analyzer 判断、阈值、case 预期或 pass/fail 规则。

## 2026-07-07: run result envelope 暴露 evaluationVersion 和 primaryFailureClass

决策：`PlaybackQualityRunResult` 顶层新增 `evaluationVersion = playback-quality-v0.1`；`PlaybackQualityModelAnalysis` 新增 `primaryFailureClass`，与 `primaryFailureArea` 对称输出。

原因：v0.1 报告需要让模型直接判断评测契约版本、case 元数据、状态、失败区域和失败责任分类。仅有 `schemaVersion`、`metricVersion` 和 `failureClasses[]` 时，模型仍需要自行推断当前报告属于哪个评测阶段，以及哪个 failure class 是优先分类。

边界：该变更只扩展 JSON 报告契约，不改变播放行为、阈值、case 分类或 pass/fail 判断。

## 2026-07-07: modelAnalysis 输出 expected/actual behavior 摘要

决策：`PlaybackQualityModelAnalysis` 新增 `expectedBehavior` 和 `actualBehavior`。普通失败报告从首个 failed check 的 signal、expected 和 actual 生成摘要；`result = error` 的报告从 `error` section 生成摘要。

原因：v0.1 报告消费对象是模型。模型可以展开 `failedChecks` 做细查，但每个 report envelope 也需要一个紧凑的“预期行为/实际行为”摘要，避免下一轮优化先在分散字段中自行猜测 case 差异。

边界：该字段只改变诊断报告契约，不改变播放行为、阈值、expected behavior、case 分类或 pass/fail 判断。

## 2026-07-07: runtime metrics 通过可选 provider 接入评测契约

决策：新增 `IPlaybackQualityMetricsProvider` 和 `PlaybackQualityRuntimeEvidenceCollector`。collector 从 reference case、`PlaybackDescriptor`、backend display diagnostics、metrics snapshot、startup 和 environment 生成标准 `PlaybackQualityRunResult`。`NativeDirectXPlaybackBackend` 只在底层 engine 明确实现 metrics provider 时委托读取 metrics；不支持时返回 false，不伪造零值指标。

原因：native C++ 层已经有 `NativePlaybackEngine.QualityMetrics()`，但 Core 侧缺少稳定、App-free 的采集入口。直接扩展 `IPlaybackBackendDiagnostics` 会破坏 `netstandard2.0` 下的既有实现；可选 provider 能把 instrumentation 增量接入 Core 评测体系，同时避免把缺失 metrics 伪装成有效 0。

影响：

- `PlaybackQualityRuntimeEvidenceCollector` 成为真实播放 harness 和后续 App adapter 写入 report envelope 的优先入口。
- `PlaybackQualityOrchestratorProbe` 已改用同一 collector/provider 路径，避免 probe 和真实采集走两套报告拼装逻辑。
- 当前变更不修改播放行为、不改变阈值、不改变 case 预期。

边界：这一步只完成 Core 侧采集契约。真实 native graph metrics 进入报告，还需要 WinRT App adapter 或独立 harness 实现 `IPlaybackQualityMetricsProvider` 并调用 collector。

## 2026-07-07: error-handling 作为一等评测结果

决策：`PlaybackQualityReport` 新增 `error` section，`PlaybackQualityRuntimeEvidenceCollector` 新增 `ComposeErrorRunResult`。打开失败、缺失文件、取消、超时、native 错误或明确拒播应生成 `report.result = error` 的标准 envelope，并保留稳定 `error.*` 信号。

原因：v0.1 不能只评测“成功进入播放”的路径。模型需要知道失败是播放器 core bug、当前 MVP 不支持、eval harness bug、样本/环境问题、flaky，还是需要人工确认。错误路径如果继续缺少结构化报告，会被误判成 color、frame-pacing、startup 或 telemetry 缺失，导致后续优化方向错误。

影响：

- `error.code`、`error.message`、`error.operation`、`error.exceptionType`、`error.failureClass`、`error.failureArea`、`error.isTerminal` 和 `error.isRetriable` 进入 signal catalog。
- `PlaybackQualityReportAnalyzer` 对 `result = error` 的报告只要求 `error.code` 等错误证据，不要求 source/timing/startup 播放 telemetry。
- reference manifest coverage 把 `error-handling` 纳入 broad Core evaluation 的必需 purpose。
- `PlaybackQualityCodeTargetCatalog` 会把 `error-handling` 指向 orchestrator、collector、native playback graph 和 HTTP media input 等可能位置。

边界：该变更不改变播放器打开、播放、重试或拒播行为，只定义错误如何被评测系统记录和归因。真实 App/native harness 后续仍需把实际错误映射为稳定 error code。

## 2026-07-07: 轨道和字幕先作为评测证据进入 v0.1

决策：在 `PlaybackQualityReport` 中加入 `tracks` section，记录视频轨、音轨、字幕轨数量、选中 stream index、字幕关闭状态和基础轨道明细。

原因：评测目标要求覆盖音轨/字幕轨发现、切换和关闭状态。当前阶段不优化播放行为，但必须让模型能判断证据缺失、轨道发现失败，还是当前 MVP 不支持。

影响：

- `PlaybackQualityReportMapper.ApplySource` 从 `PlaybackDescriptor.MediaSource.Streams` 映射轨道证据。
- `PlaybackQualityReportAnalyzer` 输出 `modelAnalysis.tracks`，并把 `tracks.*` 加入 evidence signals。
- `PlaybackQualityRequiredSignalPolicy` 会为 `tracks`、`subtitles`、`audio-switch`、`subtitle-switch`、`subtitle-off` purpose 生成对应 required signals。
- reference manifest coverage 把 `tracks` 和 `subtitles` 纳入 broad Core evaluation 的必需 purpose。

边界：v0.1 不把字幕视觉渲染正确性当作已验证能力；`tracks.isSubtitleDisabled` 只说明软件状态，不说明屏幕上最终没有字幕。

## 2026-07-07: raw report 可以通过 CLI 归一化为标准 envelope

决策：新增 `materialize-run-result` CLI 命令，读取 raw `PlaybackQualityReport` 或已有 envelope，重新运行当前 `PlaybackQualityReportAnalyzer`，输出 `PlaybackQualityRunResult` envelope。

原因：v0.1 的自动化消费对象是模型。模型应优先读取包含 `report` 和 `modelAnalysis` 的统一 envelope；但早期采集端或手工调试可能只能产出 raw report。归一化命令让这些证据进入同一套 `validate-report-set`、`analyze-report-set`、`compare-suite` 和 `evaluate-candidate` 门禁。

边界：该命令只补齐报告形态和当前 analyzer 输出，不修改播放行为、阈值、expected behavior 或 case 分类。

## 2026-07-07: reference case category 成为 manifest schema 的一等字段

决策：`PlaybackQualityReferenceCase` 新增 `category`，允许值为 `stable`、`challenge`、`quarantine`。旧 manifest 缺失该字段时按 `stable` 处理，避免破坏现有本地样本。

原因：v0.1 目标要求 case 边界清楚。`tier` 描述运行成本或压力等级，`purpose` 描述评测意图，`category` 描述是否可作为稳定裁判、挑战样本或隔离样本，三者不能混用。

影响：`validate-manifest` 聚合 `categories`，`validate-report-set` 的每个 case status 保留 category，`plan-runs` 的每个计划 case 输出 category。当前 category 还不改变门禁算法，只作为模型决策和人工审计的结构化上下文。

## 2026-07-07: reference case severity/stability 成为 manifest schema 字段

决策：`PlaybackQualityReferenceCase` 新增 `severity` 和 `stability`。`severity` 允许 `info`、`low`、`medium`、`high`、`critical`；`stability` 允许 `stable`、`variable`、`flaky`、`unknown`。旧 manifest 缺失字段时按 `medium` / `stable` 处理。

原因：模型消费评测报告时需要区分失败影响程度和样本稳定性。`category` 描述 case 是否进入稳定裁判集，`severity` 描述该 case 失败的影响，`stability` 描述样本或运行条件本身的可信度；三者不能混用。

影响：`validate-manifest` 聚合 `severities` 和 `stabilities`，`validate-report-set` case status、`plan-runs` case 和 source-only baseline summary 都保留这两个字段。当前字段不改变 pass/fail 算法。

## 2026-07-07: run result envelope 输出 caseMetadata

决策：`PlaybackQualityRunResult` 新增 `caseMetadata`，包含 `caseId`、`category`、`severity` 和 `stability`。`PlaybackQualityReferenceCaseReportRequestFactory` 会从 reference case 克隆这些字段，普通 compose 请求缺省时使用 `runId` / `stable` / `medium` / `stable`。

原因：单个 report envelope 必须脱离 report-set summary 也能被模型消费。模型需要在查看单个失败报告时直接知道该 case 的稳定性边界和失败影响，不应额外依赖 manifest join 才能判断优先级。

影响：source-only baseline 的每个 `PlaybackQualityRunResult` envelope 会带 `caseMetadata`。这只改变诊断/报告契约，不改变播放行为或 pass/fail 判断。

## 2026-07-07: materialize-run-result 保留 caseMetadata

决策：CLI 读取 `PlaybackQualityRunResult` envelope 时会解析顶层 `caseMetadata`，并在 `materialize-run-result`、report-set analysis refresh 等归一化路径中保留它。raw report 没有 envelope metadata 时按 `report.runId` / `stable` / `medium` / `stable` 补默认值。

原因：`materialize-run-result` 的用途是把旧报告或 raw report 归一化为模型首选 envelope。如果它丢失 case metadata，模型在单报告层面会失去 severity/stability 上下文，和 v0.1 的报告契约冲突。

影响：这是评测 harness 修复，不改变播放器行为、阈值、expected behavior 或 report pass/fail。

## 2026-07-07: report-set validation errors 输出 failureClass

决策：`PlaybackQualityReferenceReportSetError` 新增 `failureClass`。report-set 层只做保守归因：缺 telemetry 是 `insufficient instrumentation`，缺报告是 `environment issue`，重复或额外报告是 `evaluation harness bug`，source metadata mismatch 是 `external service/protocol issue`，无法判断时使用 `needs human confirmation`。

原因：report-set gate 是模型进入 baseline/candidate 比较前的证据门禁。模型需要知道失败是采集证据不足、运行环境问题、评测器/采集器问题，还是源选择/协议问题；不能把这些前置失败误判为播放器 core 回归。

影响：`validate-report-set` 的 JSON error 同时包含 `failureArea` 和 `failureClass`。该字段不改变 pass/fail 判定，只提高诊断可操作性。

## 2026-07-07: source-only baseline 可物化但不能视为真实播放

决策：新增 `materialize-baseline-report-set` CLI 命令，用 reference manifest 生成一组 `PlaybackQualityRunResult` envelope。该命令只物化 source/track/environment 级可构造证据，不打开媒体、不运行 App、不执行 native 播放。

原因：v0.1 需要一个可版本化 baseline artifact 来验证 manifest、report-set validation、model analysis 和缺失证据分类是否能闭环。在真实播放采集器尚未独立出来前，source-only baseline 可以暴露当前评测链路缺少哪些 telemetry，但不能伪装成播放通过。

影响：生成报告会显式带上 `source-only: playback execution was not run by this command` limitation。后续 `validate-report-set` 应继续因为缺失 display、timing、buffering、sync、startup 或 seek telemetry 而失败，并把这些失败归类为 `insufficient instrumentation`。
# 2026-07-07: core-probe report-set 作为 v0.1 的第一条实际 core 软件评测路径

决策：新增 `PlaybackQualityOrchestratorProbe` 和 CLI 命令 `materialize-core-probe-report-set`。该路径使用 in-process diagnostic backend 驱动 `PlaybackOrchestrator`，执行 load、play、pause、resume、seek、音轨切换、字幕切换、diagnostic end-of-stream marker 和 stop，并生成 `PlaybackQualityRunResult` envelope。

原因：source-only baseline 只能验证 manifest、serialization、report-set validation 和 missing telemetry 分类，不能满足“至少完成一次对当前 player core 的实际评测”。core-probe 至少能真实执行播放器 core 的 orchestration 行为，同时保持 App-free、Xbox-free、hardware-free。

边界：core-probe 不打开 native playback graph，不解码真实媒体，不访问网络，不验证 HDMI / 显示器输出。它输出的 startup、display、timing、buffering、A/V sync 是 deterministic probe telemetry，只能作为评测链路和 core orchestration 的软件证据，不能作为真实播放效果优化依据。

影响：`docs/qa/baselines/v0.1-core-probe/` 已归档 9 个 example manifest case 的 report-set，包含 `error-handling` 错误路径，`validate-report-set` 通过。下一阶段仍需接入 native graph 或真实媒体采集器，替换 probe telemetry。

# 2026-07-07: expected unsupported source 不要求 color conversion telemetry

决策：当 reference expected 明确 `isDirectPlayable = false` 或 `hdrKind = DolbyVisionUnsupported` 时，required signal policy、evaluator 和 analyzer 都把它视为 unsupported source，不再要求 `colorPipeline.actualHdrOutput`、`colorPipeline.dxgiInput`、`colorPipeline.dxgiOutput` 或 `colorPipeline.conversionStatus`。

原因：Dolby Vision Profile 5 这类当前 MVP 不支持的源，正确结论应是 `unsupported` / `unsupported-source`。如果继续要求色彩转换 telemetry，会把“没有进入播放/转换路径”的结果误报成 color-pipeline instrumentation 缺口，误导后续模型去修错误模块。

影响：DV Profile 5 case 现在在 core-probe baseline 中输出 `status = unsupported`，保留 source 分类证据，不携带无关 color missing evidence。可播放的 HDR10、force-SDR、DV Profile 8.1 fallback case 仍然要求 color/display/conversion telemetry。
