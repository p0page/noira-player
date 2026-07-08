# 技术决策

## 2026-07-08: native color evidence 必须来自 helper/source snapshot 与 DXGI runtime observation

决策：App-free native helper 的颜色证据分两层采集：source raw color metadata 来自 FFmpeg 打开的实际流 snapshot，DXGI input/output 与 conversion status 来自 `DxDeviceResources` 在渲染路径中的 runtime observation。`PlaybackQuality.Headless` 只负责解析 helper 输出并写入标准 report，不从 manifest expected、文件名、case id 或预分类结果倒填 actual color evidence。

原因：HDR/DV/SDR 判断最容易被文件名和预期值带偏。评测系统的职责是暴露 native playback 当前实际看到了什么、映射到了什么 DXGI color space、是否完成了 video processor conversion validation，而不是让报告看起来符合预期。本地 stable SDR 样本也必须在 bitstream 中真实写入 bt709 metadata；本轮选择 ffmpeg `setparams`，因为直接输出侧 color flags 在当前环境下只写出了 matrix，primaries/transfer 会变成 unknown。

影响：`NativePlaybackGraphHeadlessSmokeTests.exe` 现在输出 `sourceVideoRange/sourceColorPrimaries/sourceColorTransfer/sourceColorSpace` 和 `dxgiInput/dxgiOutput/conversionStatus/isVideoProcessorColorSpaceValidated`；C# headless harness 会把它们映射到 `report.source` 与 `report.colorPipeline`。stable native smoke 会失败在真实字段缺失，而不是靠 manifest expected 通过。

边界：`ObserveVideoColorMapping` 是 instrumentation hook，不改变 HDR、tone mapping、frame pacing、A/V sync 或渲染策略。当前只验证本地 SDR bt709 样本的软件链路；HDR10/HLG/DV、真实显示输出和更复杂色彩转换仍需要后续 evidence。

## 2026-07-08: 无音轨样本不能产生 A/V sync evidence

决策：当 report 的轨道发现明确显示有视频但没有音轨时，`PlaybackQualityReportAnalyzer` 将 `modelAnalysis.avSync.status` 标记为 `not-applicable`，不再把单独的视频位置或全 0 drift counters 输出为 `sync.*` evidence signals。native-headless smoke 固化该约束：本地 video-only SDR 样本的集合级 `capabilityCoverage.av-sync` 不能是 `evidence-present`。

原因：A/V sync 需要音频时钟和视频时钟之间的关系。video-only 样本可以验证加载、解码、渲染、seek、frame timing 和 color pipeline，但不能评价音画同步。把 `audioClockTicks = 0`、`videoPositionTicks > 0` 或全 0 drift 当成同步证据，会误导模型把“没有音轨”理解为“同步良好”。

影响：真实带音轨 report 仍会按已有 `sync.audioClockTicks`、`sync.videoPositionTicks` 和 `sync.audioVideoDriftMs*` 进入 A/V sync analysis；没有轨道发现信息的历史 fixture 也保持兼容。只有明确 video-only 的 report 会被标记为不适用，集合级 coverage 变成 `not-observed`。

边界：这只是评测器证据归类修正，不改变播放器、native graph、音频渲染、frame pacing 或 A/V sync 策略。后续仍需要带音轨 stable/challenge 样本来建立真正的 A/V sync evidence。

## 2026-07-08: native stream snapshot 作为 tracks/A-V sync evidence 的入口

决策：`FfmpegMediaSource` 新增 stream snapshot，枚举实际 FFmpeg stream 的 index、kind、codec、language、channel layout、channels、default/forced 和视频帧率；`PlaybackGraph` 只读暴露该 snapshot；native helper 将其输出为 `sourceTrackCount` 与 `trackN*` key-value；`PlaybackQuality.Headless` 再映射为 `EmbyMediaStream` 进入标准 report。

原因：之前 headless report 只从 best video snapshot 构造一个 video stream，导致真实音轨即使已经被 native graph 解码并提交到 audio renderer，也不会进入 `tracks.audioTrackCount`、`selectedAudioStreamIndex` 或 A/V sync required signal。评测系统需要从 native source snapshot 采集轨道发现证据，而不是由 C# harness 假设只有一条视频轨。

影响：native-headless smoke 新增 `local/native-headless-av-smoke` challenge case，使用本地生成的 bt709 SDR + AAC 样本。该 case 现在能在 captured/materialized/validated/analyzed 链路中保留 audio track、submitted audio frames、queued audio buffers、audio/video clock ticks 与 drift percentile。`capabilityCoverage.av-sync` 和 `capabilityCoverage.buffering` 因此可以基于真实 native/software report 标记为 `evidence-present`。

边界：stream snapshot 是采集证据，不改变源选择、音轨选择、字幕选择、解码、渲染、同步或缓冲策略。当前 challenge 覆盖一条 AAC 音轨和一条 mov_text 字幕轨的发现证据；多音轨切换、字幕选择和字幕渲染仍需后续 case。

## 2026-07-08: App-free native helper 作为第一条真实软件播放采集路径

决策：`tools/NextGenEmby.PlaybackQuality.Headless` 保留默认 skip/blocker 模式，同时新增 `--native-helper-exe`。当传入 native helper exe 时，C# harness 负责调用 helper、解析 key=value metrics、组合 `PlaybackDescriptor`、`PlaybackQualityLifecycle`、`PlaybackQualityPosition` 和 `native-headless:returned-snapshot` metrics provider，再输出标准 `PlaybackQualityRunResult`。`run-native-headless-harness-smoke-test.ps1` 负责在本机编译 helper、补齐 FFmpegInteropX UWP DLL 与 `vcruntime140_app.dll`，并用本地生成的声明样本跑完整 captured import / validate / analyze 链路；默认 skip/blocker 路径仍保留公开 Jellyfin direct-uri 作为命令契约输入。

原因：目标要求摆脱 UWP App 启动、打包、部署和 UI，获得第一套可复现的真实播放软件证据。直接让 C# 工具链接 C++/WinRT 组件仍会被 UWP projection 影响；把 native `PlaybackGraph` helper 做成独立 exe，可以先在 desktop/headless 进程里验证 FFmpeg open、D3D offscreen swapchain、decode/render metrics 和生命周期，再复用现有 C# report 契约，避免新建平行评测框架。

影响：`native-headless` report 现在有两种明确语义：没有 helper 时是结构化 skip，不得算作 playback evidence；传入 helper 且成功返回 snapshot 时是 App-free native/software playback evidence，`analyze-report-set` 会把集合级 `playbackEvidence.canEvaluateNativePlayback` 判为 `true`。helper 会从 FFmpeg 实际 source snapshot 输出 codec、尺寸、帧率和 HDR kind，不能从 manifest expected 或文件名倒填 actual。

边界：这仍是纯软件层证据，不验证 HDMI InfoFrame、电视 EOTF、真实 HDR 亮度或主观观感。当前 helper 已经暴露首个 SDR 样本的 DXGI input/output color space，但还没有覆盖 HDR/DV 复杂样本、display refresh snapshot、真实音轨/字幕轨发现和更稳定的 A/V sync 证据，因此 report 可以进入评测链路，但优化播放 core 前仍应继续补齐这些 instrumentation。

## 2026-07-08: App-free surface blocker 收窄为 graph host/lifecycle blocker

决策：新增独立 native smoke `DxDeviceResourcesOffscreenTests.cpp`，直接在桌面进程中创建 DirectX composition swapchain，并验证 `ClearToBlack()` 与 `Present()` 成功。为此给 `DxDeviceResources` 增加只读查询 `HasRenderTarget()`，并把该 smoke 接入 `run-playback-core-checks.ps1`。`native-headless` runner 的结构化 limitation 同步改为说明 offscreen swapchain 已通过 smoke，剩余 blocker 是缺少 native `PlaybackGraph` host 和 lifecycle bridge。

原因：此前 App-free native harness 的 blocker 描述把 UWP projection、`SwapChainPanel` surface 和 graph host 耦合混在一起。当前目标需要逐层拆开真实阻塞点，避免模型继续围绕已经可验证的 surface 问题打转。composition swapchain 能在无 `SwapChainPanel` 的进程中创建后，下一步应集中在如何把 `PlaybackGraph` 以 App-free 方式实例化、打开 direct-uri/local sample，并把 lifecycle/metrics 写回现有 report-set 契约。

影响：评测链路现在有一个更细的 native surface contract：如果后续 helper 连 offscreen render target 都建不起来，`native-dx-offscreen-test` 会先失败；如果它能建 surface 但不能打开媒体，问题应归到 graph host/linkage/lifecycle，而不是泛化为 XAML surface 缺失。

边界：该 smoke 本身不代表真实 native playback evidence，不改变播放策略、解码、渲染算法、HDR/color 处理或 metrics 阈值。真实播放证据由 `native-headless` helper 路径产生；offscreen smoke 只负责保护无 `SwapChainPanel` render target 的底层前提。

## 2026-07-08: rendered frame metrics 必须以 render 和 present 成功为前提

决策：`VideoRenderer::Render` 从 `void` 改为返回 `bool`，表示当前帧是否成功写入 back buffer；`PlaybackGraph::RenderNextFrame` 只有在 render 成功且 `DxDeviceResources::Present()` 成功时，才更新 render interval、`m_renderedVideoFrameCount` 和 `PlaybackQualityMetrics.RenderedVideoFrames`。

原因：App-free/headless 路线会遇到无 swapchain、无 surface 或 present 不可用的情况。此前 renderer 失败被吞掉，graph 仍会把解码到的帧计为 rendered frame，可能让模型把“真实渲染证据”与“仅解码/推进帧”混在一起。评测系统需要诚实暴露缺 surface instrumentation，而不是产生漂亮但错误的 frame pacing 证据。

影响：当前 UWP surface 正常时行为应保持一致；无 surface 或 present 失败时，decoded frame 可以继续反映解码进度，但 rendered frame 和 render interval 不会被伪造。新增 contract test 固化该约束。

边界：这不改变 HDR 策略、帧选择、音频同步、drop 策略或渲染算法；只是修正 metrics 采样条件。真实 App-free native playback evidence 仍需要后续 native host/render-surface 解耦。

## 2026-07-08: PlaybackGraph 使用普通 native open request

决策：`PlaybackGraph::Open` 改为接收 `PlaybackGraphOpenRequest`，该 struct 定义在 `PlaybackGraph.h`，包含 direct stream URL、start position、音轨/字幕选择和 video frame rate。`NativePlaybackEngine` 继续保留 WinRT/UWP public API，并在 adapter 层把 `NativePlaybackOpenRequest` 转换成 `PlaybackGraphOpenRequest`。

原因：App-free playback harness 的主要 blocker 是 native graph 内核和 WinRT/UWP projection、`SwapChainPanel` surface host 纠缠。先把 open request 从 WinRT runtimeclass 解出来，可以让后续 headless/Win32/native host 在不依赖 IDL runtimeclass 的情况下复用 graph 参数契约。这个变化比直接引入新 native host 小，风险集中在 adapter 转换层。

影响：新增 contract test 防止 `PlaybackGraph.h` 重新 include `NativePlaybackEngine.g.h`，并要求 `NativePlaybackEngine.cpp` 显式执行 `CreatePlaybackGraphOpenRequest(request)` 后再调用 `m_graph->Open(graphRequest)`。Native build 已验证该重构不破坏当前 C++/WinRT 组件编译。

边界：这不改变播放行为、不改变解码、渲染、HDR、音频或 metrics 采集。App-free headless harness 仍然输出 native linkage blocker；下一步仍需要 graph host/render-surface 抽象，才能真实 open direct-uri/local sample。

## 2026-07-08: App-free headless harness 先输出结构化 native linkage blocker

决策：新增 `tools/NextGenEmby.PlaybackQuality.Headless` 作为 App-free captured report producer。当前版本只生成标准 `PlaybackQualityRunResult` skip envelope，skip code 为 `native-headless.native-link-blocked`，并通过 `materialize-native-harness-report-set` 的 captured import 路径进入现有 report-set 链路。该 provider 当前不得写入 `native-headless:returned-snapshot`。

原因：现有 `NextGenEmby.Native` 是 Windows Store C++/WinRT dynamic library，IDL 公开入口包含 `AttachSurface(Windows.UI.Xaml.Controls.SwapChainPanel)`，播放入口通过 WinRT/UWP projection 暴露。虽然 `DxDeviceResources` 在没有 swapchain 时多处会返回 false 而不是直接崩溃，`PlaybackGraph` 公开复用仍受 WinRT runtimeclass、UWP component activation、FFmpeg UWP linkage 和 surface host 边界限制。直接用外部 ffmpeg 或只拼 JSON 会让模型误以为已经获得真实 native playback evidence。

影响：现在可以用一个 App-free 命令产出可导入、可校验、可分析的 headless report，并把真实 blocker 暴露给模型。下一步真实 native/software evidence 需要先抽出 native graph host 或 render-surface/playback-surface abstraction，再让 harness 调用 `PlaybackGraph` 实际 open direct-uri/local sample。

边界：该决策不优化播放效果，不改变 native graph 行为，不证明 HDR、颜色、帧率或 A/V sync。skip-only report 必须继续被 `analyze-report-set` 判定为缺 native playback evidence，不能进入 candidate playback evidence gate。

## 2026-07-08: App-free native evidence provider 身份进入 playbackEvidence 判断

决策：`analyze-report-set` 的 native/software playback evidence 判断从单一 `native-winrt:*` 扩展为 provider catalog：`native-winrt:*`、`native-headless:*` 和 `native-win32-harness:*`。这些 provider 会让集合级 `playbackEvidence.scope = native-software`、`status = available`、`canEvaluateNativePlayback = true`，前提是 report-set 没有混入 core-probe/source-only/skip-only 证据。

原因：下一阶段目标是摆脱 UWP App 启动、打包、部署和 UI，转向 App-free native/software playback harness。如果 analyzer 只承认 `native-winrt:*`，后续独立 headless 或 Win32 harness 即使产出同等结构的 runtime metrics，也会被误判为证据不足。

影响：CLI smoke 覆盖了 `native-headless:returned-snapshot` 的 imported report-set 分析路径。`evaluate-candidate` 仍复用 `playbackEvidence.canEvaluateNativePlayback`，因此未来 App-free harness 产出的 report-set 可以进入候选比较门禁。

边界：这只是 evidence provider 身份分类，不实现 harness，不打开 native playback graph，不改变阈值、expected behavior、report-set validation 或播放行为。`native-headless:*` 不得用于包装 source-only、core-probe 或外部播放器报告；只有真实 App-free harness 采集到的软件播放证据才应使用该身份。

## 2026-07-08: candidate evaluation 要求 native/App 软件播放证据

决策：`evaluate-candidate` 在 report-analysis gate 之后新增 `baseline-playback-evidence` 和 `candidate-playback-evidence`。两个 gate 复用 `ReportAnalysisSummary.PlaybackEvidence.CanEvaluateNativePlayback` 作为唯一判据；任一侧没有 native/App 软件播放证据时，candidate evaluation 停在对应 gate，顶层 blockers 写入 `baseline-playback-evidence.insufficient` 或 `candidate-playback-evidence.insufficient`，并跳过 suite comparison。

原因：`core-probe` 已经能证明播放器 core orchestration、生命周期事件和报告链路闭合，但它不打开 native playback graph，也不解码真实媒体。此前只要 report-set 和 report-analysis 都通过，`evaluate-candidate` 仍可能继续进入 suite，把 core-probe 的 deterministic telemetry 误用于候选播放质量判断。新增 gate 让模型在进入 before/after 比较前必须先确认 baseline 和 candidate 都有可比较的 native/App 软件播放证据。

影响：source-only 和 core-probe report-set 不能通过 playback evidence gate；导入 `native-winrt:*` captured report 可以作为本阶段纯软件闭环里的 native/App playback evidence。CLI smoke 覆盖了 core-probe 被阻断和 native-winrt fixture 通过两条路径。

边界：这是候选评估门禁变更，不改变播放行为、native graph、report-set validation、analyzer 输出、阈值、expected behavior、comparison scoring 或 suite 决策逻辑。`native/App 软件播放证据` 仍不等于硬件显示输出验证。

## 2026-07-08: report-analysis summary 增加 playbackEvidence 范围判断

决策：`analyze-report-set` 输出的 `ReportAnalysisSummary` 新增 `playbackEvidence`。该对象包含 `scope`、`status`、`canEvaluateNativePlayback`、`canEvaluateOrchestration` 和 `reasons[]`，从集合级 `evidenceSources[]`、`limitations[]` 与 skip 计数派生，不改变既有分析结果。

原因：`core-probe` 对 orchestrator 和报告链路是有效软件证据，但不能证明真实 native decode/render、frame pacing、A/V sync 或 color 管线。仅靠 `decision = no-change` / `risk = low` 或 capability `evidence-present`，自动化模型仍可能过度信任 deterministic probe。`playbackEvidence` 把证据范围显式编码成机器字段，减少模型二次推断。

影响：source-only baseline 的 `playbackEvidence.scope = source-only`、`status = missing`；core-probe baseline 的 `scope = orchestration-only`、`status = limited`、`canEvaluateNativePlayback = false`；native-harness-skip baseline 的 `scope = none`、`status = missing`。CLI smoke 还覆盖了导入 `native-winrt:*` captured report 时 `scope = native-software`、`status = available`。

边界：这是模型消费契约增强，不改变播放行为、native graph、report-set validation、analysis decision、risk、candidate gate、阈值或 expected behavior。`native-software` 仍只表示软件层 captured evidence，可用于本阶段纯软件闭环；它不证明 HDMI InfoFrame、显示器 EOTF 或人工观感。

## 2026-07-08: report-analysis summary 聚合证据来源和限制

决策：`analyze-report-set` 输出的 `ReportAnalysisSummary` 新增 `evidenceSources[]` 和 `limitations[]`。`evidenceSources[]` 聚合非 `unknown` 的 `report.runtimeMetrics.providerStatus`；`limitations[]` 聚合每个 report 的 limitation 字符串。

原因：单 report 已经包含 runtime provider 和 limitation，但自动化模型通常先读集合级 summary 决定下一步。如果 summary 只显示 `decision = no-change` 或 capability `evidence-present`，模型需要额外展开所有 case 才能知道证据来自 deterministic `core-probe`、source-only materializer、native-winrt captured evidence，还是 skip/import 路径。集合级聚合能减少误把 probe 指标当成真实 native 播放质量证据的风险。

影响：三套 v0.1 baseline 的 `report-analysis-summary.json` 已刷新。source-only summary 暴露 `evidenceSources = ["source-only"]`；core-probe summary 暴露 `["core-probe:returned-snapshot"]`；native-harness-skip 不伪造 runtime evidence source，但会聚合 skip/native-harness limitation。

边界：这是模型消费 JSON 的可解释性增强，不改变播放行为、native graph、阈值、expected behavior、report-set validation、analysis decision 或候选评估规则。`unknown` provider status 不进入 `evidenceSources[]`，避免把缺省状态当成证据来源。

## 2026-07-08: playable report-set 要求 runtime metrics 采集状态

决策：`PlaybackQualityRequiredSignalPolicy` 对非 error、非明确 unsupported 的可播放 case 新增 required signals：`runtimeMetrics.status`、`runtimeMetrics.providerStatus`、`runtimeMetrics.hasSnapshot` 和 `runtimeMetrics.hasPlaybackSample`。`validate-report-set` 会把缺少这些信号的报告归类为 `insufficient instrumentation`。

原因：runtime counters 只有在知道采集器状态时才可解释。此前 analyzer 已能把 `unavailable` 和 `empty-snapshot` 阻断为 evidence-collection 问题，但 report-set gate 没有强制外部 captured report 声明采集状态；手写或第三方 report 可能带了 timing/buffer 数字，却没有说明这些数字来自真实 provider、空 snapshot，还是默认值。

影响：source-only baseline 继续显式写入 `runtimeMetrics.status = unavailable` 与 `providerStatus = source-only`，因此新增 gate 不把 source-only 误判为缺字段；core-probe baseline 继续通过，并以 `core-probe:returned-snapshot` 标记 deterministic provider；native-harness-skip 仍只要求 skip 证据，不要求 runtime playback sample。

边界：这是 report-set evaluation contract 变更，不改变播放行为、native graph、阈值、expected behavior、case 分类或 pass/fail 标准。`HasSnapshot = false` 或 `HasPlaybackSample = false` 是有效采集状态证据，不是缺字段；这些状态是否阻断优化由 `modelAnalysis.optimizationGate` 处理。

## 2026-07-08: manifest 明确声明的 source raw color metadata 进入 required-signal gate

决策：`PlaybackQualityExpected` 新增 `VideoRange`、`ColorPrimaries`、`ColorTransfer` 和 `ColorSpace`。当 reference manifest 写出对应 expected 值时，`PlaybackQualityEvaluator` 会比较 `report.source.videoRange`、`source.colorPrimaries`、`source.colorTransfer` 和 `source.colorSpace`，`PlaybackQualityRequiredSignalPolicy` 也会要求这些信号存在。未声明的字段不进入 required-signal gate。

原因：上一轮只把 raw source color metadata 暴露为可选 evidence signal，模型仍无法区分“manifest 没有要求该字段”和“采集器应该提供但缺失”。HDR/DV 相关优化需要先确认输入流的 range/primaries/transfer/matrix 来自 playback-info 或真实解析路径，而不是从文件名、`HdrKind` 或策略分类反推。把 manifest 明确声明的字段纳入 gate，可以让缺采集、采集错误和播放输出问题分开归因。

影响：私有 Emby manifest 生成器会从 playback-info 视频流字段写入这些 expected 值；公开示例 manifest 和 v0.1 source-only/core-probe/native-harness-skip baseline 已刷新。source-only/core-probe 的 synthetic source 只复制 manifest 显式 expected 值用于评测链路验证，不代表真实 App/native collector 已解析到这些字段。

边界：这是 evaluation contract/testability 变更，不改变播放行为、源选择、HDR/DV 策略、DXGI conversion、阈值、case expected behavior 或 pass/fail 标准。评测器仍不得从文件名、显示标题、`HdrKind`、Dolby Vision profile 分类或播放策略推导 raw source color metadata。

## 2026-07-08: analyzer version 升级到 5 并暴露 source raw color metadata

决策：`PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion` 从 4 升级到 5。`EmbyMediaStream` 新增 `VideoRange`、`ColorPrimaries`、`ColorTransfer` 和 `ColorSpace`，`EmbyApiClient` 从 playback-info `MediaStreams[]` 映射，`PlaybackQualityReportMapper` 从视频流透传到 `PlaybackQualitySource`，`PlaybackQualityReportAnalyzer` 输出 `source.videoRange`、`source.colorPrimaries`、`source.colorTransfer` 和 `source.colorSpace` evidence signals，`PlaybackQualitySignalCatalog` 将这些信号登记为正式 report signals。

原因：v0.1 覆盖能力要求色彩元数据包括 primaries、transfer、matrix/range。此前 Core 已用这些字段辅助 HDR/DV 分类，但 report 只暴露分类后的 `HdrKind` 和播放策略，模型无法判断 raw input color metadata 是否来自服务端/采集器，也无法区分“采集缺口”和“颜色管线判断错误”。

边界：这是 instrumentation/testability 和报告契约变更，不改变播放行为、源选择、HDR/DV 策略、DXGI color conversion、阈值或 pass/fail 规则。评测器不得从文件名、`HdrKind`、`DisplayTitle` 或 profile 分类反推 raw color 字段。后续 manifest gate 已把明确声明的 expected raw source color 字段纳入 required-signal 检查；未声明字段仍保持可选。

## 2026-07-08: analyzer version 升级到 4 并要求音频声道数证据

决策：`PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion` 从 3 升级到 4。`EmbyMediaStream` 和 `PlaybackQualityTrack` 新增 `Channels`，`EmbyApiClient` 从 playback-info `MediaStreams[].Channels` 映射，`PlaybackQualityReportMapper` 透传到 report，`PlaybackQualityReportAnalyzer` 输出 `tracks.audio.channels` evidence signal。track/subtitle purpose 的 required-signal policy 现在要求 `tracks.audio.channels`。

原因：v0.1 覆盖能力要求媒体能力识别包含音频 codec/channel/layout。此前报告有 `tracks.audio.channelLayout` 和 `displayTitle`，但没有保留服务端明确给出的声道数，模型只能从文本推断 5.1/7.1。声道数应作为独立 telemetry，缺失时报告为 instrumentation 缺口。

边界：这是 instrumentation/testability 和报告契约变更，不改变播放行为、音轨选择、解码、转码、阈值或 pass/fail 规则。评测器不得从 `ChannelLayout`、`DisplayTitle` 或文件名推断 `Channels`；只有采集器/服务端明确提供的正数才算 `tracks.audio.channels` 证据。

## 2026-07-08: analyzer version 升级到 3 并要求 direct stream locator 证据

决策：`PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion` 从 2 升级到 3。`PlaybackQualitySource` 和 `PlaybackQualityModelAnalysis.Source` 新增 `hasDirectStreamUrl` 与 `directStreamProtocol`，`PlaybackQualityReportMapper` 只从 `EmbyMediaSource.DirectStreamUrl` 提取非敏感 locator evidence：是否存在 direct stream URL，以及 URL scheme/protocol；不写入完整 URL、query string、token 或个人服务地址。非 error case 的 required-signal policy 现在要求 `source.hasDirectStreamUrl` 和 `source.directStreamProtocol`。

原因：v0.1 覆盖能力包含媒体加载、本地/远端 URL、Emby item/stream 信息解析，以及源选择/协议诊断。此前 report 只保留 source id、codec、container 等媒体属性，模型无法判断采集器是否拿到了实际直连流定位信息，也无法区分协议/source-selection 证据缺失与播放质量问题。只记录 protocol 可以提供诊断入口，同时避免把私有 Emby URL 或 token 写入报告。

边界：这是 instrumentation/testability 和报告契约变更，不改变播放行为、源选择策略、转码策略、HDR/DV 策略、阈值或 pass/fail 规则。缺少这两个 locator 信号会被 report-set gate 归类为 `insufficient instrumentation`，不应被解释为播放器 core 播放质量回归。

## 2026-07-08: 模型消费输出统一暴露 evaluationVersion

决策：manifest validation、report-set validation、single comparison、comparison suite、run plan、materialized report-set summary、report-analysis summary 和 candidate evaluation 输出都暴露 `evaluationVersion = playback-quality-v0.1`。

原因：这些 JSON 都会被模型直接消费。仅有 `schemaVersion` 只能说明 JSON shape，不能说明当前评测合同版本；模型在比较 baseline/candidate 或读取历史 artifact 时需要先确认评测合同一致。

边界：这只扩展 JSON 契约，不改变播放器行为、case 分类、阈值、expected behavior、pass/fail 判断或比较算法。

## 2026-07-08: report-set gate 要求播放器身份

决策：`validate-report-set` 会要求每个 report 包含 `environment.playerCoreVersion` 和 `environment.sourceRevision`，缺失、null 或空白时输出 `report.environment.missing`，并归类为 `insufficient instrumentation`。`plan-runs` 会把这两个字段列入 `evidenceRequirements`，让采集器在运行前就知道最终 report-set 的身份要求。

原因：v0.1 的报告要用于版本化比较和模型后续优化。如果 report 没有播放器版本和源码修订，模型无法判断证据来自哪个 core/native 版本，也无法可靠做前后对比。

边界：这只要求最终可比较 report-set 的环境身份完整，不改变播放器行为、阈值或 expected behavior。`collectorVersion` 和 `buildConfiguration` 仍作为有用环境信号记录，但本轮不作为 report-set 必填项。

## 2026-07-08: report-set gate 校验 failureArea 枚举

决策：`PlaybackQualityCodeTargetCatalog` 暴露 `KnownFailureAreas` / `IsKnownFailureArea`，`validate-report-set` 会拒绝未知的 `analysis.primaryFailureArea`、`error.failureArea`、`skip.failureArea` 和 `checks.failureArea`，并输出 `report.failureArea.invalid`。

原因：failure area 是后续模型定位代码和规划优化的核心索引。任意字符串或 typo 会让模型把问题导向不存在的模块，或者绕过已有 code target catalog。

边界：这只校验 report-set 契约，不改变播放器行为、阈值或 expected behavior。未知 failureArea 被归类为 `evaluation harness bug`，表示采集器、手写 fixture 或 evaluator 输出不符合当前 area catalog。

## 2026-07-08: report-set gate 校验 report result 枚举

决策：新增 `PlaybackQualityReportResult` 作为 v0.1 报告结果枚举，`validate-report-set` 会拒绝未知的 `report.result`，合法值固定为 `pass`、`fail`、`skip`、`unsupported`、`error`。

原因：目标报告的消费对象是模型。`observed` 或临时状态如果进入 report-set，会让模型无法可靠区分“已裁决结果”和“中间采集状态”。report-set gate 必须只接受 v0.1 明确声明的最终结果状态。

边界：这只收紧 report-set 契约，不改变 `PlaybackQualityReport` 的默认构造值，也不改变播放器行为、阈值或 expected behavior。未知 result 被归类为 `evaluation harness bug`。

## 2026-07-08: report-set gate 校验 failureClass 枚举

决策：`PlaybackQualityFailureClassification` 暴露统一的 `KnownFailureClasses` / `IsKnown`，`validate-report-set` 会拒绝 report 中未知的 `error.failureClass`、`skip.failureClass` 和 `checks.failureClass`，并输出 `report.failureClass.invalid`。

原因：v0.1 报告的消费对象是模型。failure class 如果可以任意拼写，模型会把同一种责任归因拆成多类，或者把 typo 当成新类别，后续自动优化会被带偏。report-set gate 应在进入 compare/evaluate 前阻断这类契约错误。

边界：这只校验评测报告契约，不改变播放行为、case expected behavior、阈值或 failure area 判断。未知 failureClass 被归类为 `evaluation harness bug`，表示采集器、手写报告或 evaluator 输出不符合当前枚举。

## 2026-07-08: analyzer version 升级到 2

决策：`PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion` 从 1 升级到 2，并刷新 v0.1 source-only、core-probe 和 native-harness-skip 归档 artifact。

原因：`modelAnalysis.source.signals` 和集合级 `capabilityCoverage` 新增了 `source.hasChapterMetadata`，且 `chapterCount` 从非 nullable 计数改为 nullable evidence。旧 envelope 的 `modelAnalysis` 不能继续被视为同一契约下的可复用分析，否则自动化模型会漏读章节 metadata presence 语义。

边界：这是 report/analyzer 契约版本变更，不改变播放行为、阈值、case expected behavior 或 pass/fail 规则。刷新后的 source-only baseline 仍然因为缺运行时 telemetry 而保持失败；native-harness-skip 仍然只表达采集器未实现；core-probe 仍然只是 App-free/in-process 诊断评测。

## 2026-07-08: DEBUG App-hosted quality-run 作为真实播放采集入口

决策：复用现有 `DevelopmentNavigationCommand route = quality-run`，在 DEBUG App 中将其分发到 `PlaybackPage`。播放成功后，App 在命令指定的采集窗口内主动执行 pause、resume、seek、stop，读取当前 `PlaybackDescriptor`、`IPlaybackBackendDiagnostics.DisplayStatus` 和 `IPlaybackQualityMetricsProvider` metrics，通过 `PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult` 生成标准 `PlaybackQualityRunResult` envelope，并写入 App LocalFolder 的 `quality-run/captured/<runId>.json`。seek target、actual position 和 seek error 会作为 position evidence 在 evaluator 运行前写入 report。如果打开媒体或播放命令执行阶段失败，App 会通过 `ComposeErrorRunResult` 写入标准 error envelope。CLI 和 App 共享 `PlaybackQualityCapturedReportPath.GetReportRelativePath`，保证 App captured report 可以被 `materialize-native-harness-report-set --captured-reports-dir` 按同一 run-id 相对路径导入。

原因：v0.1 已经有 source-only、core-probe、native-harness skip 和 captured-report import，但缺少真实 App/native 播放会话产出 raw report 的入口。完全独立 App-free native harness 仍然成本较高；DEBUG App-hosted collector 是当前最小可行路径，因为它复用已经能登录、拿 Emby playback-info、创建 native graph、读 native metrics 的现有 App 播放链路，同时不新建并行评测框架。

边界：这是 instrumentation/testability 变更，不改变普通播放策略、source selection、HDR/DV 策略、阈值、expected behavior 或 pass/fail 规则。它也不验证 HDMI InfoFrame、显示器 EOTF 或肉眼颜色准确性。自动 pause/resume/seek/stop 只在 DEBUG `quality-run` 采集路径执行，用于证明生命周期操作和 position telemetry 能进入报告；不能把该序列当成普通用户播放行为。

影响：`run-playback-core-checks.ps1` 的 App diff guard allowlist 扩展为精确允许 DEBUG quality-run 接线文件：`Playback/WinRtNativePlaybackEngine.cs`、`Navigation/PlaybackLaunchRequest.cs`、`MainPage.xaml.cs`、`Views/PlaybackPage.xaml.cs`。这不是允许 UI 或交互工作进入本阶段；XAML、App project、manifest/package 和未列入 App 文件仍被阻断。

## 2026-07-08: HTTP/HTTPS direct-uri case 可生成 App-hosted quality-run 命令

决策：`plan-runs` 对 HTTP/HTTPS `direct-uri` reference case 也生成 `devCommand.route = quality-run`，并把 `referenceCase.uri` 写入 `devCommand.streamUrl`。`DevelopmentNavigationCommand` 对 `quality-run` 改为要求 `itemId` 或 `streamUrl` 二选一；DEBUG App 收到只有 `streamUrl` 的 `quality-run` 时，使用 direct stream native playback 路径启动，并复用同一套 App-hosted capture、error envelope 和 report export/import 机制。

原因：v0.1 需要第一套不依赖私人 Emby 服务的 native/App 软件播放证据。公开 Jellyfin 样本已经进入 reference manifest，但此前 `plan-runs` 只给有 `itemId` 的 case 输出 App `quality-run` command，导致公开样本只能停留在 source/probe 层，不能被 App-hosted collector 实际打开。

边界：这是采集入口和调度能力，不是播放策略优化。direct-uri 路径不从 URL、文件名或 manifest expected 反推真实源元数据；如果 App/native 当前没有解析出 codec、HDR/color、轨道或字幕证据，report-set validation 和 analyzer 应继续把这些缺口归类为 instrumentation/metadata 缺失。`devCommand.streamUrl` 只在本地 App command 和 private/local artifact 中使用；提交仓库时仍不得包含私人服务地址、账号、密码或个人素材路径。

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

# 2026-07-08: headless display refresh 只作为软件 policy snapshot

决策：`native-headless` report 中的 `display.refreshRateHz` 来自 native `HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot`，用于软件闭环里的 cadence 分析；它不声明 HDMI 输出、系统显示模式或电视面板真的切换到了该刷新率。report 必须保留 limitation：`native-headless: display refresh is a software policy snapshot; HDMI/display output is not verified`。

原因：当前目标是脱离 Xbox/显示器形成可复现的软件评测证据。缺少 refresh evidence 会让 frame-pacing/cadence 一直停在缺证据状态；但在 headless 环境中伪装成真实显示器输出同样会误导后续模型。因此只输出“策略选择结果”，并在 limitation 中明确硬件不可验证。

影响：`run-native-headless-harness-smoke-test.ps1` 的 SDR/HDR10 矩阵可以让模型消费 source frame rate、display refresh policy、cadence ratio、frame interval、drop/wait/starvation 和 DXGI mapping。后续 Core 调优可以基于这些软件证据比较变化，但任何 HDMI/HDR 输出结论仍需独立硬件验证。

边界：该决策不改变播放策略、不改变实际刷新率切换、不改变 HDR/tone mapping/frame pacing 算法，只增加 instrumentation 和 report-set 覆盖。

# 2026-07-08: SDR/HDR10 frame-rate 矩阵使用本地生成样本

决策：native-headless smoke 使用 FFmpeg 本地生成 SDR 23.976/24/30/60fps 和 HDR10 24fps 样本，所有 source color、HDR kind、frame rate 和 DXGI mapping 都必须来自 native helper 实际解析与 runtime observation，不从文件名、case id 或 manifest expected 倒填 actual evidence。

原因：评测系统的消费者是模型，错误的 actual evidence 会把后续调优带偏。本地生成样本可复现、无私有服务器依赖、不会把账号或媒体库信息写入仓库，也能稳定覆盖目标中的 SDR/HDR10 与常见帧率组合。

影响：最新 native-headless report-set 有 9 个 native/software playback reports，SDR 和 HDR10 都覆盖 23.976/24/30/60fps，`frame-pacing` coverage 为 `evidence-present`，4 个 HDR10 case 都进入 `color` coverage。生成产物只保存在 ignored 的 `artifacts/` 下。

边界：本地样本短小且是合成画面，只能证明解析、DXGI mapping、cadence 和 timing evidence 链路可用；不能代表真实影视素材的观感、码率压力、字幕渲染复杂度或硬件 HDR 输出。

# 2026-07-08: baseline/candidate 对比必须携带一致的 build identity

决策：播放器 Core 调优不直接基于单次 report 做结论，而是通过 `New-PlaybackCoreTuningBaseline.ps1` 生成 baseline/candidate report-set，再用 `Compare-PlaybackCoreTuningCandidate.ps1` 调用 `evaluate-candidate --match-by run-id` 进行同一 manifest 下的对比。

原因：调优证据的消费对象是模型。模型必须能确认 baseline 和 candidate 来自不同 build identity，且报告集合匹配同一批 case。没有 native/App software playback evidence 时，比较应被门禁拦截为 `collect-comparable-evidence`，不能伪装成可接受的调优结果。

影响：`run-native-headless-harness-smoke-test.ps1` 现在接收 `PlayerCoreVersion`、`SourceRevision` 和 `BuildConfiguration`，并在 materialized native report 中写入这些值。baseline 编排脚本会把本轮 source revision 传给 native-headless，避免 native report 在 candidate 对比中触发 `suite.environment-same-build`。

边界：这不改变播放器行为、阈值、expected behavior 或 pass/fail 规则。当前 full no-op candidate 对比结果为 `decision = no-change`，说明评测链路可用，但没有 measured improvement；因此不能据此声明播放器 Core 已被优化。

# 2026-07-08: video-only native 播放使用视频时钟限速

决策：当 native `PlaybackGraph` 没有可用 audio clock 时，使用第一帧 PTS 与 `steady_clock` 建立 video clock。后续视频帧如果相对该时钟过早，会保留 pending frame 并等待，而不是立即渲染。

原因：baseline/candidate 调优目标要求所有 Core/native 策略调整都必须有同一 manifest 对比证据。baseline 暴露了 video-only 23.976/24/30fps 样本的渲染间隔 P95 接近 16ms，说明无音轨路径被 render loop / Present 节奏驱动，低帧率内容实际被过快输出。已有 audio clock 路径只覆盖含音轨样本，不能保护 video-only 样本。

影响：`PlaybackFramePacing.ShouldWaitForVideoClock` 成为无 audio clock 路径的等待判定；`PlaybackGraph` 在 resume 或切回 audio clock 时重置 video clock，避免旧时钟污染后续播放。candidate 对比结果为 41 个 case 可比，5 个 frame-pacing improvement，0 regression。

边界：该策略只解决“无 audio clock 时视频过快输出”的软件 pacing 问题。不证明硬件刷新率切换、HDR 输出、A/V sync 或真实素材主观观感已经正确；含音轨播放仍优先使用 audio clock。

# 2026-07-08: cadence 评测同时检查过慢和过快

决策：`PlaybackQualityEvaluator` 在 matched display refresh case 中新增 `RenderIntervalMsP95Cadence` 检查：当存在可用源帧率、期望帧时长和至少 2 帧渲染样本时，`timing.renderIntervalMsP95` 必须不低于源帧时长的 75%。

原因：此前 evaluator 已有 max render interval 检查，可以发现明显卡顿/过慢，但无法发现 24fps 内容以接近 60Hz loop 速度输出的“过快 cadence”。这会让模型误以为 low-frame-rate video-only case 通过了 frame-pacing。

影响：同一 raw/native report 在导入或 compare 时会被当前 evaluator 规则重新评估，历史 captured report 的 stale checks 不再掩盖新规则发现的问题。

边界：75% 是最低节奏守卫，不是最终画质标准。它用于识别明显 under-paced/too-fast 输出；更精细的 frame pacing、jitter、display cadence 和硬件输出仍需要后续更严格的样本与指标。

# 2026-07-08: frame-ratio candidate 对比按可接受区间判断

决策：`PlaybackQualityRunComparator` 对 `framePacing.renderIntervalP95FrameRatio`、`renderIntervalP99FrameRatio` 和 `maxFrameGapFrameRatio` 使用 `0.75..1.5` 可接受区间比较。baseline 在区间外而 candidate 进入区间时记为 improvement；反向记为 regression；都在区间内不因为数值轻微变化制造噪音。

原因：frame-ratio 信号不是单纯 lower-is-better。对于低帧率内容，被过快渲染时 ratio 会显著低于 1；candidate 增大并靠近 1 是改善，而不是回退。此前 lower-is-better 规则会把这类正确变化误判为 mixed/regression。

影响：提交绑定候选 `playback-core-tuning-video-clock-61fecb3.local` 在同一 manifest 对比下为可采纳候选：`accept-candidate`，5 个 improvement，0 regression。该候选使用归档 baseline 的 `core-reference-manifest.local.json` 作为固定输入，避免当前私有 manifest 变化影响同 manifest 比较。

边界：该规则只改变 candidate comparison 对派生 frame-ratio 的解释，不放宽 stable case expected behavior，也不删除任何失败。单报告 evaluator 仍独立输出 pass/fail checks。

# 2026-07-08: track/subtitle evidence 进入 candidate comparison

决策：`PlaybackQualityRunComparator` 在 baseline/candidate comparison 中加入 track/subtitle 稳定证据比较。两个报告都有轨道证据时，比较轨道数量、选中流、字幕关闭状态、音轨 codec/channels、字幕 codec/language/default/forced/external 等信号；相同则进入 `coverage.matchedSignals`，不同则作为对应 `tracks` 或 `subtitles` regression 暴露。

原因：report-set analysis 已能证明 track/subtitle evidence 存在，但同一 manifest 的 candidate comparison 之前没有把这些信号写入 matched comparison。调优目标要求模型能在同一 manifest 下同时看到 cadence、frame pacing、A/V sync、buffering、seek/timeline、track/subtitle 和 color/DXGI 证据的对比；track/subtitle 不能只停留在集合级 coverage。

影响：`playback-core-tuning-video-clock-61fecb3.local` 重新对比后仍为 `accept-candidate`，41 个 comparison、5 个 improvement、0 regression、0 mixed；comparison JSON 中已出现 `tracks.subtitleTrackCount`、`tracks.isSubtitleDisabled`、`tracks.audio.codec`、`tracks.subtitles.codec` 等 matched signals。

边界：这是 comparison evidence 补齐，不改变样本预期、不放宽 stable 标准、不改变播放器行为，也不把轨道变化自动解释成改善。轨道/字幕证据变化默认需要模型审查，避免播放策略调优意外改变源发现或选择状态。

# 2026-07-08: seek/timeline comparison 使用 seek landing 证据，不使用 post-seek 播放位置

决策：native-headless helper 在执行 `Seek(0)` 后必须立即采样 seek landing position，并用该值写入 `seekActualPositionTicks`。如果后续继续播放以观察 seek 后稳定性，该后续播放 position 不能再作为 `position.actualPositionTicks` 或 `position.seekPositionErrorMs` 的来源。

原因：`position.seekPositionErrorMs` 的语义是“seek 落点相对目标位置的误差”，不是“seek 后又播放一段时间后的当前进度”。此前 helper 在 seek 后继续播放 1.5 秒再采样，导致 `seekTargetPositionTicks = 0` 但 `actualPositionTicks = 15000000`，模型会把正常的 post-seek playback progress 误判成 timeline/seek 缺陷。

影响：`run-native-headless-harness-smoke-test.ps1` 增加 A/V report 的 immediate seek evidence 断言；`PlaybackQualityRunComparator` 现在会把 `position.seekPositionErrorMs` 放进 candidate comparison 的 matched signals，并按 lower-is-better 记录 `timeline` improvement/regression。以 `playback-core-tuning-video-clock-61fecb3.local` 为 baseline，新 candidate `playback-core-tuning-seek-evidence-working.local` 在同一 41-case manifest 下为 `accept-candidate`，9 个 timeline improvement，0 regression。

边界：这是评测证据语义修正，不改变播放器 seek 行为、不改变 stable case expected、不降低阈值，也不证明真实设备上的 seek 体验已经完整正确。后续如果要评价 seek 后播放稳定性，应新增独立信号，而不是复用 seek landing error。

# 2026-07-08: runtime timing/sync/buffering telemetry 进入 candidate comparison matched signals

决策：`PlaybackQualityRunComparator` 在 baseline 和 candidate 都包含 runtime playback evidence 时，把 frame timing、A/V sync 和 buffering telemetry 暴露到 `coverage.matchedSignals`。这些信号包括 render interval P50/P95/P99、max frame gap、video ahead waits、audio/video clocks、drift percentiles、submitted/queued audio buffers 和 starvation counters。

原因：第二轮 Core 调优的消费对象是模型。模型不能只知道 report-set 层面“有 evidence”，还需要在逐 case comparison 中看到同一 manifest 下 candidate 是否保留了 timing、sync、buffering 这些目标能力证据。否则含音轨 native-headless/Emby case 的关键证据仍要人工展开单个 report 才能确认，容易漏掉回归或缺证据。

影响：`local/native-headless-av-smoke` comparison 现在同时暴露 frame pacing jitter、buffering、A/V sync、seek/timeline、track/subtitle 和 color/DXGI matched signals。重新比较 `playback-core-tuning-video-clock-61fecb3.local` 与 `playback-core-tuning-seek-evidence-16ba684.local` 后仍为 41 case 可比、`accept-candidate`、9 improvement、0 regression、0 mixed。

边界：这些 runtime telemetry 在没有显式 threshold 或 failure delta 时只证明 evidence 可比，不自动解释成 improvement 或 regression，避免短样本采样噪音驱动 candidate 接受/拒绝。真正的播放策略调优仍需要同一 manifest 下的明确前后差异或失败阈值。

# 2026-07-08: Present duration 作为 swapchain/vsync blocking evidence

决策：native/headless 播放质量报告新增 `timing.presentDurationMsP50/P95/P99/Max`，记录 `DxDeviceResources.Present()` 调用耗时。该信号进入 signal catalog、单报告 `modelAnalysis.evidenceSignals`、native-headless smoke 断言和 candidate comparison `coverage.matchedSignals`。

原因：第二轮 Core 调优需要区分 frame pacing jitter 是来自解码/调度/render loop，还是来自 swapchain `Present()`/vsync blocking。此前只有 render interval 和 max frame gap，模型无法判断等待发生在 Present 前还是 Present 内部。

影响：后续同 manifest baseline/candidate 对比会在逐 case matched signals 中暴露 Present duration evidence。它能辅助解释 cadence、frame pacing 和 A/V sync 异常，但不改变现有 pass/fail 阈值。

边界：`presentDurationMs*` 当前只作为诊断 evidence，不自动解释成 improvement 或 regression；短样本中的数值波动不能单独驱动 candidate 接受或拒绝。
# 2026-07-08: audio-ahead wait duration 作为 Present 前等待诊断证据

决策：native/headless 播放质量报告新增 `timing.audioAheadWaitDurationMsP50/P95/P99/Max`，专门记录 pending video frame 因音频 clock 尚未追上而停留在 `ShouldWaitForAudio` 路径的等待时长。该信号进入 report schema、signal catalog、model analysis evidence signals、native-headless smoke 断言和 candidate comparison matched signals。

原因：`presentDurationMs*` 已经显示 A/V smoke 的 jitter 不在 `DxDeviceResources.Present()` 内部，但仍缺少证据区分 audio-clock gating、render loop sleep、decode starvation 等 Present 前因素。`audioAheadWaitDurationMs*` 让模型可以直接看到“视频等音频”这段等待的分布。

影响：同一 41-case manifest 的 `playback-core-tuning-audio-wait-evidence-41case-f7f7315.local` comparison 为 `no-change`，不会被解释成 quality improvement。该信号目前只作为诊断和 matched evidence，不自动驱动 improvement/regression。

边界：不要用该信号替代 A/V sync 正确性判断，也不要因为它存在就放宽 frame pacing 或 drift 阈值。它只说明等待发生在哪里、等待多久；是否需要改播放策略仍必须由同 manifest candidate comparison 证明。

# 2026-07-08: RenderLoopWait 1ms 实验不采纳

决策：不采纳把 `PlaybackFramePacing::RenderLoopWait()` 从 5ms 降到 1ms 的策略调整。本轮 TDD 验证了该改动可以通过 native frame pacing 单测，但 native-headless A/V smoke 没有改善 `renderIntervalMsP95`，并且 `audioAheadWaitDurationMsP99/Max` 变差，因此已回退。

原因：A/V smoke 中 `audioAheadWaitDurationMsP50≈15.6ms`、`P95≈31ms` 的形态更像底层 sleep/timer 调度粒度，而不是 constexpr 等待值本身。单纯把 sleep 请求从 5ms 改到 1ms 不能保证实际唤醒粒度改善。

影响：下一步调优应聚焦高精度 wait primitive、wait scheduling、或 render loop 结构，而不是继续调小固定 sleep 常量。任何候选实现都必须先保留现有 A/V sync、buffering、seek/timeline、track/subtitle 和 color/DXGI evidence，并通过同一 41-case manifest 的 baseline/candidate comparison。

边界：这不是结论性地证明 Windows timer quantum 是唯一根因；它只排除了“把 5ms 改 1ms 就能改善”的低成本假设。后续需要更强证据或更小心的候选实现。

# 2026-07-08: near-threshold audio catch-up wait 实验不采纳

决策：不采纳“只在音频即将追上视频阈值时把 render loop wait 从 5ms 降到 1ms”的策略调整。实验实现为 `PlaybackFramePacing::AudioCatchUpRenderLoopWait`：远离阈值保持默认 5ms；当 `framePosition - audioPosition - VideoAheadTolerance <= 6ms` 时使用 1ms，并由 `PlaybackGraph` 的 `ShouldWaitForAudio` 分支传递到下一轮 render loop sleep。

原因：该假设通过了 TDD targeted native test 和 native UWP Debug x64 build，但同一 41-case manifest comparison 不支持采纳。`playback-core-tuning-audio-catchup-wait-41case-working.local` 相对 `playback-core-tuning-audio-wait-evidence-41case-f7f7315.local` 的结果为 `decision = no-change`，41/41 unchanged。关键 A/V case 中 render P95/P99 从 `47.0978/47.6411ms` 变为 `47.1216/47.7064ms`，audio-ahead wait P95/P99/Max 从 `31.2524/31.2728/31.2728ms` 变为 `31.4748/38.0209/38.0209ms`。

影响：实验代码已回退，不改变当前播放器 Core/native 行为。该结果排除了“只在阈值附近缩短 sleep”这个低成本候选；它也说明 1ms wait 本身并不能可靠降低当前 A/V smoke 的等待抖动。

边界：该实验仍是短样本 native-headless 纯软件证据，不代表真实 Xbox/HDMI 输出体验。下一步不应继续调整固定 sleep 常量或阈值窗口；应先补充 wait target、oversleep delta、wait reason 等诊断信号，或设计可复用且低开销的等待 primitive，再用同一 41-case manifest 做 baseline/candidate comparison。

# 2026-07-08: audio-ahead wait target/oversleep 作为等待调度诊断证据

决策：native/headless 播放质量报告新增 `timing.audioAheadWaitTargetMsP50/P95/P99/Max` 和 `timing.audioAheadWaitOversleepMsP50/P95/P99/Max`。`target` 表示 pending video frame 进入 `ShouldWaitForAudio` 时，按当前 audio-ahead tolerance 计算还需要等待音频追上的理论剩余时间；`oversleep` 表示实际等待时长超过该目标的部分。

原因：此前 `audioAheadWaitDurationMs*` 只能说明“视频等音频等了多久”，但无法区分等待来自合理策略目标，还是底层 wait/sleep 唤醒过粗导致的过冲。A/V smoke 的新证据显示 target P95 约 23.3ms，而 oversleep P95 约 16.9ms，因此下一步调优应优先验证 wait primitive / wait scheduling，而不是继续盲调固定 sleep 常量或 audio-ahead 阈值。

影响：这些字段进入 native metrics、WinRT bridge、Core report schema、signal catalog、model analysis evidence signals、headless parser、native-headless smoke 断言和 candidate comparison matched signals。同一 41-case manifest 的 `playback-core-tuning-audio-wait-target-evidence-41case-8e13b26.local` comparison 为 `decision = no-change`，不会被解释成 quality improvement。

边界：`target/oversleep` 当前只作为诊断和模型可消费 evidence，不自动驱动 improvement/regression，也不替代 A/V sync 正确性判断。任何等待策略候选仍必须生成同 manifest baseline/candidate comparison，并明确记录 improved/regressed/mixed 与剩余风险。

# 2026-07-08: audio target precise wait 候选暂不采纳

决策：不采纳本轮 yield-based audio target precise wait 策略。该策略在 `ShouldWaitForAudio` 分支用 `framePosition - audioPosition - VideoAheadTolerance` 计算下一轮 render loop 的等待目标，并用 yield loop 等待到目标时间，避免固定 5ms sleep 多轮叠加。

原因：同一 41-case manifest comparison 的 suite 结果为 `decision = no-change`，0 improved / 0 regressed / 0 mixed。目标 A/V case 的诊断信号确实改善，`renderIntervalMsP95` 从约 47.5ms 降到 38.0ms，`audioAheadWaitOversleepMsP95` 从约 16.9ms 降到 6.8ms；但这些信号当前没有明确 improvement 规则，且 yield loop 的 CPU 成本没有被当前评测覆盖。

影响：实验代码回退，不改变当前播放器 Core/native 行为。该结果保留为下一步设计低开销高精度 wait primitive 的证据：方向上应继续围绕 audio target / oversleep，而不是继续调整固定 sleep 常量。

边界：不要把本轮诊断改善解释为已采纳优化。除非后续补齐 CPU/调度开销证据，或引入可证明低开销的等待 primitive，并再次通过同 manifest baseline/candidate comparison，否则该策略不进入主线。

# 2026-07-08: high-resolution waitable timer 作为低开销 audio target wait primitive 保留

决策：保留 `38ae764` 引入的 `RenderLoopWaiter` 候选实现。它在 `ShouldWaitForAudio` 路径按 `AudioAheadWaitDuration` 计算下一轮 render loop 的目标等待时间，并优先使用 Windows high-resolution waitable timer；不可用时退回普通 waitable timer，再退回 `sleep_for`。这替代了上一轮不采纳的 yield-based precise wait，避免 busy/yield loop。

原因：同一 41-case manifest 对比 `playback-core-tuning-audio-wait-target-evidence-41case-8e13b26.local` 与 `playback-core-tuning-highres-wait-41case-38ae764.local` 的结果为 41/41 可比、baseline/candidate validation 均通过、`manifest.sameCaseIds = true`、`decision = no-change`、0 improved、0 regressed、0 mixed、41 strong confidence。目标 A/V case 的诊断信号改善：`renderIntervalMsP95` 约 `47.5ms -> 41.0ms`，`audioAheadWaitDurationMsP95` 约 `31.3ms -> 27.7ms`，`audioAheadWaitOversleepMsP95` 约 `16.9ms -> 7.6ms`；`audioVideoDriftMsP95` 保持 `10ms`，音轨/字幕、buffering、seek/timeline 和 color/DXGI matched signals 均保留。

影响：该实现作为低开销等待 primitive 进入当前 Core/native 主线，但不把本轮结果声明为播放质量 improvement。当前 evaluator 仍把这些 timing/oversleep 信号作为 matched diagnostic evidence，suite 决策仍为 `no-change`。后续调优可以基于该 primitive 继续补齐 CPU/调度开销、wait reason 或更长 A/V 样本证据。

边界：该结论只来自 native-headless 纯软件短样本，不证明 Xbox/HDMI 输出、真实显示刷新、HDR 观感或长时间播放稳定性。`videoAheadWaitCount` 从 51 增加到 71，`audioVideoDriftMsP50` 从约 3.3ms 增至 6.7ms，虽然 P95 未退化，但仍属于剩余风险；后续不能仅凭本轮结果声称流畅度已改善。

# 2026-07-08: native helper process-cost 作为 wait/scheduling 调优证据

决策：native-headless helper report 需要记录 helper 进程级别的 wall-clock、CPU time 和 CPU utilization ratio，并作为 `runtimeMetrics.processWallClockMs`、`runtimeMetrics.processCpuTimeMs`、`runtimeMetrics.processCpuUtilizationRatio` 进入 report schema、model analysis evidence signals、required signal policy 和 candidate comparison。

原因：前几轮 wait/scheduling 调优已经能观察到 render interval、audio-ahead target 和 oversleep 的变化，但仍缺少 CPU/调度开销证据。尤其是 yield-based precise wait 和 high-resolution waitable timer 这类候选，不能只看 frame pacing 诊断信号，还必须知道是否引入明显 CPU 成本。

实现边界：

- `tools/NextGenEmby.PlaybackQuality.Headless` 在 helper 进程退出后读取 `Stopwatch.Elapsed` 和 `Process.TotalProcessorTime`，计算 `cpu / wall-clock`。
- process-cost 信号只在数值大于 0 时进入 `modelAnalysis.evidenceSignals`。
- comparator 只有在 baseline 和 candidate 都有 process-cost 证据时才把这些信号加入 matched signals，避免把旧 schema baseline 的缺失字段误解为改善或退化。
- 该变更不改变播放器行为、播放策略、evaluator 阈值、case expected behavior 或 pass/fail 规则。

当前 `b292019` 与 `38ae764` 的 41-case comparison 结果为 `decision = no-change`。由于 `38ae764` baseline 没有 process-cost 字段，本轮不能给出 CPU 成本前后结论；下一轮以 `b292019` 之后的 report-set 为 baseline 时，才可以比较 process-cost 是否发生变化。
