# 当前状态

播放质量评测体系正在推进 v0.1，目标是先把评测做成可信裁判，而不是优化播放效果。

## 2026-07-09 更新：cadence 重复采样 summary 已工具化

新增 `tools/quality-run/Measure-PlaybackCadenceStability.ps1` 和对应脚本测试，用于消费一批 repeated playback report JSON，按 case group 聚合 `framePacing.*ExpectedErrorMs` 的 min/max/spread，并输出机器可读 `playback-cadence-stability-summary`。该工具只做 flake/stability 归因，不改变 `PlaybackQualityRunComparator` 的 accept/reject 规则，也不放宽任何 stable/challenge case 的 expected behavior。

该脚本已接入 `run-playback-core-checks.ps1` 的 `playback-cadence-stability-test` 门禁。测试覆盖两个 group：一个 P99/max expected-error spread 超过 `2ms` 的不稳定组，以及一个低于 materiality 的稳定组，确保 summary 能给模型明确的 `stable` / `unstable` / `insufficient-samples` 归类和 `unstableSignals`。

已用新脚本重新聚合上一轮真实重复采样 artifact：`artifacts/quality-run/repeat-cadence-dc2bf33/cadence-stability-summary.local.json`。结果显示 3/3 group 都是 `unstable`：`local/repeat-av-30` 的 P99 expected-error spread 为 `2.4364ms`，`local/repeat-hdr10-60` 为 `3.2721ms`，`local/repeat-sdr-60` 为 `6.3696ms`。这说明不只是 60fps 无音轨 case，当前 3 秒 native-headless A/V smoke 的尾部 cadence 也可能跨过 `2ms` materiality；后续 Core 调优前应先把重复采样结果作为候选解释证据纳入报告集。

`Compare-PlaybackCoreTuningCandidate.ps1` 现在支持 `-BaselineCadenceStabilityPath` 和 `-CandidateCadenceStabilityPath`，会把 stability summary 摘要写入 `comparison-summary.local.json` 的 `cadenceStability` 节点，同时记录到 `paths`。这让每次 Core 候选对比可以同时暴露 suite gate 结论和重复采样稳定性解释，不需要模型再手动查找额外 artifact。

## 2026-07-09 更新：positive-wait clamp 候选不采纳，60fps native-headless cadence 需要重复采样

本轮尝试了两个小步 native wait 调度候选，均未作为当前 Core 行为保留。第一版把正等待下限同时用于 audio-ahead 和 video-clock，提交为 `92e82e0`，commit-bound 54-case comparison 结果为 `reject-candidate`，回退集中在无音轨 native-headless 60/24fps cadence case。第二版收窄为仅 audio-ahead positive wait clamp，提交为 `dc2bf33`；working comparison 曾得到 `keep-candidate`、1 improved、0 regressed，但 commit-bound comparison 结果为 `reject-candidate`，目标 case 为 `local/native-headless-hdr10-60`。两笔候选已通过 revert 回退，当前主线不保留 positive-wait clamp 行为。

本轮没有放宽 comparison 规则或样本预期。相反，按照 commit-bound comparison 的 gate 结论拒绝候选，并补充了重复采样证据：使用当前 native helper 对 `hdr10-60`、`sdr-60`、`av-30` 各跑 5 次，结果写入 ignored artifact `artifacts/quality-run/repeat-cadence-dc2bf33/repeat-summary.json`。关键观测是 60fps native-headless 短样本的 P99/max gap 波动本身足以跨过当前 2ms materiality：`hdr10-60` P99 在 `21.0002ms..24.2723ms` 间波动，`sdr-60` P99 在 `20.0507ms..26.4203ms` 间波动；相比 expected `16.6667ms`，单次采样可能随机生成 frame-pacing regression。

当前结论：positive-wait clamp 不能作为 accepted Core 优化进入新基线；后续要继续调 frame pacing 时，必须先让 native-headless 60fps cadence comparison 支持重复采样、稳定性标记或独立 flake 归因，否则模型会被单次短样本尾部峰值带偏。该结论不影响此前已接受的 `761800c` video-clock target wait 和 `5ef58d6` expected-error comparison 规则。

## 2026-07-09 更新：audio-ahead oversleep 进入 comparison 判定

本轮不提交播放器 core/native 行为变化，只补齐 comparison 裁判规则：`PlaybackQualityRunComparator` 现在会比较 `timing.audioAheadWaitOversleepMsP95`，当 baseline/candidate 都有正数证据且变化达到 `2ms` 时，oversleep 降低记为 `frame-pacing` improvement，oversleep 升高记为 regression。`P99` 只在 `P95` 已经达到 material threshold 且方向一致时作为补充 delta 进入 comparison；单独的短样本 P99/max 抖动仍保留为 matched signal，不直接否决 candidate。

TDD 记录：新增测试先红灯，当前 comparator 会把 `audioAheadWaitOversleepMsP95 7.9336ms -> 10.07ms` 判为 `unchanged`；实现后 targeted `PlaybackQualityRunComparatorTests` 通过。另有保护测试覆盖 `P99 7.7653ms -> 10.787ms` 但 `P95` 只轻微变化的场景，确保短样本尾部单点不会把 accepted candidate 误判为 regression。

已用同一 54-case baseline/candidate reports 重新生成 ignored local comparison artifact。结果保持 `decision = keep-candidate`、`action = accept-candidate`、risk `low`、54/54 可比、`manifest.sameCaseIds = true`、8 improved、0 regressed、0 mixed、46 unchanged、strong confidence 54/54。

本轮同时验证并拒绝了一个未提交的 native precise-tail wait 实验：该实验让 A/V smoke 的 oversleep P50 下降，但 render P95/P99 和 oversleep P95/P99 变差，因此已回滚，不作为播放策略候选进入主线。完整验证命令 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1` 已通过，覆盖 407 个播放相关 Core 测试、CLI smoke、native-headless smoke、manifest/report-set 脚本测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。

## 2026-07-09 更新：frame pacing expected-error 进入 comparison 判定

已提交 `5ef58d6 tools: score frame pacing expected error`。本轮不改变播放器 core/native 行为，只补齐 comparison 规则：当 baseline/candidate 都有 `timing.expectedFrameDurationMs` 和 runtime frame pacing telemetry 时，比较 `renderIntervalMsP95/P99/maxFrameGapMs` 相对 expected frame duration 的绝对误差，而不是把 frame interval 简单当作 lower-is-better。

规则边界：baseline/candidate 的 expected frame duration 必须一致且为正；误差变化小于 `2ms` 时视为短样本波动，不产生 improvement/regression；达到 materiality threshold 后，误差降低记为 `frame-pacing` improvement，误差升高记为 regression。新增 derived signals：`framePacing.renderIntervalP95ExpectedErrorMs`、`framePacing.renderIntervalP99ExpectedErrorMs`、`framePacing.maxFrameGapExpectedErrorMs`。

TDD 记录：新增 comparator 测试先红灯，当前实现把 `48.098ms -> 42.098ms` 这类“更接近 41.708ms expected”的 runtime cadence 改善判为 `unchanged`；实现后 targeted `PlaybackQualityRunComparatorTests` 通过。完整验证命令 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1` 已通过，覆盖 405 个播放相关 Core 测试、CLI smoke、native-headless smoke、manifest/report-set 脚本测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。

已用同一 54-case baseline/candidate reports 重新生成 evaluator commit 绑定的 comparison：`docs/qa/private/comparisons/playback-core-tuning-video-clock-expected-error-54case-5ef58d6.local/`。结果：54/54 可比、`manifest.sameCaseIds = true`、baseline/candidate validation 均通过、suite `decision = keep-candidate`、`action = accept-candidate`、risk `low`、8 improved、0 regressed、0 mixed、46 unchanged、strong confidence 54/54。

关键结论：上一笔 `761800c` 的 video-clock target wait 候选现在不再只依赖人工读 P95/P99；模型可从 comparison 中直接看到 8 个无音轨 native-headless cadence/color case 的 expected-error improvement，以及 A/V smoke 因变化未达到 2ms materiality threshold 保持 unchanged。剩余风险是该规则仍只覆盖软件 runtime evidence，不验证真实 Xbox/HDMI/display refresh，也不评价硬件端 HDR 输出。

## 2026-07-09 更新：video-clock target wait 改善 native-headless 无音轨 cadence

已提交 `761800c chore: use target wait for video clock pacing`。本轮只做一个小步 native 策略调整：当 `PlaybackGraph` 在无音轨路径等待 software video clock 时，不再退回默认 5ms render loop sleep，而是按 `PlaybackFramePacing::VideoClockWaitDuration` 计算剩余等待时间，并复用现有 high-resolution `RenderLoopWaiter`。

TDD 记录：先在 `FramePacingTests.cpp` 增加 `VideoClockWaitDuration` 期望，确认当前实现红灯失败于缺少 API；实现后 targeted native frame pacing test 和 render loop waiter test 通过。完整验证命令 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1` 已通过，覆盖 404 个播放相关 Core 测试、CLI smoke、native-headless smoke、manifest/report-set 脚本测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。

已生成 commit-bound 54-case candidate：`docs/qa/private/candidates/playback-core-tuning-video-clock-wait-54case-761800c.local/`，并与当前 accepted baseline `playback-core-tuning-main-ffmpeg812-force-sdr-evidence-790caeb.local/` 输出 comparison：`docs/qa/private/comparisons/playback-core-tuning-video-clock-wait-54case-761800c.local/`。结果：54/54 可比、`manifest.sameCaseIds = true`、candidate validation 通过、suite `decision = no-change`、0 improved、0 regressed、0 mixed、54 unchanged、strong confidence 54/54。

关键 runtime 证据：8 个无音轨 native-headless cadence/color case 的 `renderIntervalMsP95/P99/maxFrameGapMs` 全部向 expected frame duration 收敛。典型样本：`local/native-headless-sdr-23976` P95 `48.098ms -> 42.098ms`，P99 `48.111ms -> 42.101ms`；`local/native-headless-sdr-60` P95 `30.024ms -> 20.413ms`，P99 `32.539ms -> 20.917ms`；`local/native-headless-hdr10-30` P95 `47.183ms -> 33.670ms`，P99 `47.613ms -> 33.753ms`。A/V smoke 未使用 video-clock path，整体基本持平：P95 `41.031ms -> 39.577ms`，P99 `41.385ms -> 42.540ms`，process CPU ratio `0.175 -> 0.131`。

边界：当前 evaluator 仍把 frame pacing runtime telemetry 作为 matched diagnostic signals，不自动判定 improvement/regression，因此 suite 结论保持 `no-change`。本轮可采纳为低风险 native video-clock pacing 策略，但不能宣称真实 Xbox/HDMI 输出、HDR 显示、A/V sync 或主观流畅度已经改善。下一步更适合补齐 frame pacing comparison 规则，按“相对 expected frame duration 的误差”而不是单纯 lower-is-better 来机器判定改善/退化。

## 2026-07-09 更新：基于 main/Noira/FFmpeg 8.1.2 重建播放 Core 评测基线

已从 `main` 新建 worktree `codex/playback-core-quality-main-brand-ffmpeg-v2`。当前 main 头为 `d027ed1 merge: FFmpeg 8.1.2 UWP package upgrade`，项目名和品牌名已切到 Noira / NoiraPlayer，native NuGet 包为 `FFmpegInteropX.UWP.FFmpeg.8.1.2`。

本轮先修复评测工具链在新 main 上的两个阻断点：`run-playback-core-checks.ps1` 默认 App diff base 从旧的 pre-Noira 提交改为 `origin/main`；`native-restore` 提前到 native-headless smoke 之前执行，避免新 worktree 中尚未 restore 的 FFmpeg 8.1.2 package 目录导致 smoke 失败。验证命令 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1` 已通过，覆盖 402 个播放相关 Core 测试、CLI smoke、native-headless smoke、manifest/report-set 脚本测试、native helper tests 和 native Debug x64 build。

同时补回旧 worktree 尚未进入 main 的证据修复：reference case 的 `forceSdrOutput` 现在会进入 `PlaybackQualityReportRequest` 和最终 report；`tools/NoiraPlayer.PlaybackQuality.Headless` 新增 `--force-sdr-output`，native-headless smoke 会断言 `colorPipeline.forceSdrOutput`；`PlaybackQualityReportAnalyzer` 不再把“已显式上报的 `timing.renderedVideoFrames = 0`”误判为 missing evidence，而是保留为零帧失败/样本不足证据。

已用 ignored 私有 combined manifest 生成 code commit 绑定的 54-case candidate：`docs/qa/private/candidates/playback-core-tuning-main-ffmpeg812-force-sdr-evidence-790caeb.local/`。结果：54/54 report-set validation 通过，native-headless included，analysis `decision = no-change`、`risk = low`、`canEvaluateNativePlayback = true`。同旧 54-case baseline `playback-core-tuning-noira-main-private54-a89d4ae.local` 对比输出 `docs/qa/private/comparisons/playback-core-tuning-main-ffmpeg812-force-sdr-evidence-790caeb.local/`，结果为 54/54 可比、`manifest.sameCaseIds = true`、0 improved、0 regressed、0 mixed、54 unchanged、strong confidence 54/54。

边界：本轮是新 main/FFmpeg 8.1.2 基线恢复和 evidence 修复，不是播放策略调优。对比结论 `no-change` 不应解释为画质、HDR、A/V sync 或 frame pacing 提升；它只说明当前修复没有在 54-case 软件评测中引入可见回归，并让 force-SDR 与 zero-render 证据可被模型正确消费。

## 2026-07-09 更新：App UI 开发入口从 fixture route 切到私有真实样本 manifest

当前 `*-fixture`、`details-real-sample` 和 `details-real-bright-sample` 开发 route 已退役。App active code 不再携带 mock fixture 数据链路；Home、Library、Details、Search、Live TV、Music、PhotoViewer 和 Playback 的开发入口均回到真实会话、真实 `itemId` 或真实 direct stream。

新增 `tools/Write-AppUiSampleCommand.ps1`，用于从 ignored 的 `docs/qa/private/ui-real-samples.local.json` 选择一个真实 UI 样本，并写入当前 Noira UWP 包的 `LocalState\dev-command.json`。仓库只提交 `docs/qa/private/ui-real-samples.template.json` 和规范；真实 Emby `itemId`、`mediaSourceId`、标题、私有 URL 和账号信息不得提交。

`docs/qa/ui-development-data-sources.md` 是 UI 开发数据源的权威规则。历史文档中的 fixture route 只代表当时验证记录，不作为当前开发入口。

边界：这是 UI 开发数据源治理，不是播放 core 优化，也不改变 playback-quality manifest/report-set 规则。需要可复现 UI 样本时，应维护本地私有 manifest，而不是恢复 mock fixture route。

## 2026-07-08 更新：项目已改名为 Noira / NoiraPlayer

当前普通开发分支已从 worktree 模式迁回 `codex/playback-core-quality-isolated`，并合并 `origin/main`。项目代码、solution、App/Core/Native/test/tool 项目已改名为 `NoiraPlayer.*`，用户可见品牌为 `Noira`。环境变量前缀也已改为 `NOIRAPLAYER_*`。

验证状态：`dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj -v minimal` 通过 780/780；App Debug x64 MSIX 打包通过；playback-core 内部 20 个 core/native/quality 检查逐项通过。`run-playback-core-checks.ps1 -AppDiffBase origin/main` 在本阶段会被 app-diff-guard 阻断，因为项目整体改名会触及 App 目录；这不是 playback core 失败。

文档整理已经建立 `docs/README.md` 作为入口，明确当前权威文档、冻结评测结果、历史 plan/log 的边界。`docs/qa/baselines/` 当前保持冻结，未在本轮整理中改动。

## 2026-07-08 更新：开发调试流程补齐 Hot Reload 和 loose deploy

UWP App 的 Debug x64/x86 构建已显式保留 XAML Hot Reload 所需的 XBF line info，并禁用 Debug 下的 .NET Native toolchain。新增 `tools/Register-NoiraLooseApp.ps1`，用于 clean/build 后注册本机 Debug loose layout，也支持 `-ValidateOnly` 只验证 `AppxManifest.xml` 和注册参数，不改变系统包注册状态。

文档状态：`docs/development-workflow.md` 记录 Visual Studio F5 Hot Reload、本机 loose file deploy、Xbox/远程 loose registration 的使用边界；`README.md` 和 `docs/README.md` 已指向该流程。该流程只用于缩短 App/XAML 开发迭代，不改变 playback-quality report-set、core/native 评测规则或最终 MSIX/真机验证要求。

## 2026-07-08 更新：wait reason evidence 已拆分，A/V jitter 定位更明确

本轮只做 instrumentation/testability，不改变播放策略。`videoAheadWaitCount` 保持为兼容用总计数，同时新增 `audioAheadWaitCount` 与 `videoClockWaitCount`，用于区分 pending video frame 是在等待音频时钟追赶，还是在等待软件 video clock pacing。`PlaybackGraph`、native metrics snapshot、Core report mapper、analyzer、signal catalog、required-signal policy、headless parser、comparison matched signals 和 native-headless smoke 均已接入该拆分。

关键设计点：当 `videoAheadWaitCount > 0` 时，`timing.videoAheadWaitCount`、`timing.audioAheadWaitCount` 和 `timing.videoClockWaitCount` 都会进入 `modelAnalysis.evidenceSignals`。即使 `videoClockWaitCount = 0`，它也必须作为有意义的负证据暴露给模型，避免只记录非零异常造成诊断偏差。

已提交代码：`a2bfdd7 chore: split native wait reason evidence`。验证已通过 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1`，覆盖 389 个 Core 测试、CLI smoke、native-headless smoke、native helper tests、native frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。

已用上一轮 accepted baseline `docs/qa/private/candidates/playback-core-tuning-process-cost-evidence-41case-b292019.local/` 生成同 manifest candidate：`docs/qa/private/candidates/playback-core-tuning-wait-reason-evidence-41case-a2bfdd7.local/`，并输出 comparison：`docs/qa/private/comparisons/playback-core-tuning-wait-reason-evidence-41case-a2bfdd7.local/`。结果为 41/41 case 可比，baseline/candidate validation 均通过，`decision = no-change`，`improved = 0`，`regressed = 0`，`mixed = 0`，`unchanged = 41`，strong confidence 41/41。

关键 A/V 样本证据：`local/native-headless-av-smoke` 中 `videoAheadWaitCount = 71`，`audioAheadWaitCount = 71`，`videoClockWaitCount = 0`，`avSync = synced`，且三个 wait reason signals 均进入 evidence。当前可判断该样本的等待主要来自 audio-clock gating，而不是 software video clock pacing；但这仍不是播放质量优化，只是让后续调优有更可靠的根因信号。

## 2026-07-08 更新：native-headless 首个 color pipeline instrumentation 已进入 report

App-free native helper 现在会从 FFmpeg source snapshot 输出 `source.videoRange`、`source.colorPrimaries`、`source.colorTransfer` 和 `source.colorSpace`，并从 `DxDeviceResources` 输出 `colorPipeline.dxgiInput`、`colorPipeline.dxgiOutput`、`colorPipeline.conversionStatus` 与 `isVideoProcessorColorSpaceValidated`。这些字段已经进入 `PlaybackQuality.Headless` 生成的 `PlaybackQualityRunResult`，并被 `materialize-native-harness-report-set -> validate-report-set -> analyze-report-set` 消费。

`run-native-headless-harness-smoke-test.ps1` 的 stable 本地样本改为用 ffmpeg `setparams=range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709` 写入真实 bitstream metadata，避免用 manifest expected 或文件名冒充 actual source evidence。最新 native smoke 对本地 SDR 样本验证到 `bt709` source metadata、`YCBCR_STUDIO_G22_LEFT_P709` input、`RGB_FULL_G22_NONE_P709` output 和 video processor conversion validation。

边界：这仍然只是首个 SDR 软件证据切片，不验证 HDMI/显示器输出，也不代表 HDR10/HLG/DV、tone mapping、A/V sync 或 frame pacing 已优化。后续仍需补齐更复杂样本、display refresh、轨道/字幕发现、A/V sync 采样和 HDR color pipeline 的同等 evidence。

## 2026-07-08 更新：video-only 样本不再被误判为 A/V sync 证据

`PlaybackQualityReportAnalyzer` 现在会在轨道发现明确显示有视频但没有音轨时，把 `modelAnalysis.avSync.status` 标记为 `not-applicable`，并且不再输出 `sync.*` evidence signals。`analyze-report-set` 因此会把本地 video-only native SDR smoke 的 `av-sync` capability 标记为 `not-observed`，而不是 `evidence-present`。

边界：这不是 A/V sync 优化，也不证明当前播放器同步更好；它只是修正评测器的证据归类，避免把“没有音轨可同步”误读成“音画同步良好”。真正的 A/V sync 评测仍需要带音轨样本和有效 audio/video clock drift evidence。

## 2026-07-08 更新：native-headless challenge 已覆盖音轨、buffering 和 A/V sync 软件证据

`FfmpegMediaSource` 现在会输出 video/audio/subtitle stream snapshots，`PlaybackGraph` 暴露这些 snapshots，native helper stdout 使用 `sourceTrackCount` 和 `trackN*` 字段传给 `PlaybackQuality.Headless`。headless harness 会把这些 native stream snapshots 映射成 `EmbyMediaStream`，因此 report 能保留 `tracks.audioTrackCount`、`tracks.selectedAudioStreamIndex`、音频 codec、channel layout、channels、default/forced 等证据。

`run-native-headless-harness-smoke-test.ps1` 现在包含第二个本地生成 case：`local/native-headless-av-smoke`，category 为 `challenge`。该样本包含 bt709 SDR 视频和 AAC 音轨，native helper 会实际打开 PlaybackGraph，采集 submitted audio frames、queued audio buffers、audio/video clock ticks 和 drift percentile。最新 report-set 中 `tracks`、`buffering`、`av-sync` 和 `color` capability 都能从 native/software evidence 得到消费。

边界：这仍然不是播放策略优化，也不验证硬件输出。A/V sync evidence 来自软件 clock/drift 采样，不能代表外部 HDMI/显示设备测量；subtitle 当前只覆盖带 mov_text 样本的 stream discovery 和 disabled state，仍不证明字幕选择、解码或最终渲染正确。

## 2026-07-08 更新：App-free native-headless helper 已产生真实 native/software playback evidence

`tools/NoiraPlayer.PlaybackQuality.Headless` 现在支持 `--native-helper-exe`。传入由 smoke 编译出的 `NativePlaybackGraphHeadlessSmokeTests.exe` 时，headless harness 会在不启动、不打包、不部署 UWP App 的情况下调用 native helper，打开本地生成的声明样本，执行最小生命周期 `load/play/pause/resume/seek/stop`，解析 native metrics，并输出标准 `PlaybackQualityRunResult`。

`tools/quality-run/run-native-headless-harness-smoke-test.ps1` 已覆盖两条路径：不传 helper 时继续输出结构化 `native-headless.native-link-blocked` skip，传 helper 时编译/运行 App-free native `PlaybackGraph` helper，生成 captured report，再走 `materialize-native-harness-report-set -> validate-report-set -> analyze-report-set`。最新 smoke 中 `analyze-report-set` 识别到 `evidenceSources = ["native-headless:returned-snapshot"]`，`playbackEvidence.scope = native-software`，`canEvaluateNativePlayback = true`。

当前真实 helper report 仍会诚实暴露缺口：它能采集 decoded/rendered frames、render intervals、source codec/width/height/frameRate、首个 SDR source color metadata、DXGI input/output color space、生命周期和 runtime metrics provider；但仍缺 `display.refreshRateHz`、更复杂 HDR/DV 样本、真实轨道/字幕发现和更完整 A/V sync instrumentation，且不验证 HDMI/显示器/HDR 观感。该 report 可作为软件播放证据进入评测链路，但还不能作为“颜色、HDR、帧率或 A/V sync 已优化”的证明。

文档化运行命令：单项 smoke 使用 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-native-headless-harness-smoke-test.ps1`；本阶段完整门禁使用 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1`。stable smoke 使用本地生成样本来避免公网波动；公开 Jellyfin direct-uri 已作为 challenge 手动验证过，可真实打开并被 materialize / validate / analyze 消费，但不放进稳定门禁。

## 2026-07-08 更新：offscreen DirectX composition swapchain 已通过 native smoke

新增 `tests/NoiraPlayer.Native.Tests/DxDeviceResourcesOffscreenTests.cpp`，并把 `native-dx-offscreen-test` 纳入 `tools/quality-run/run-playback-core-checks.ps1`。该测试在不创建 UWP `SwapChainPanel` 的桌面进程里调用 `DxDeviceResources::CreateSwapChain(16, 16, false)`，随后验证 `HasRenderTarget()`、`ClearToBlack()` 和 `Present()`。

这一步把 App-free playback 的 surface blocker 收窄了：当前证据显示 composition swapchain/offscreen render target 本身可以在独立 native smoke 中创建和 present。后续真实 helper 已经基于这个前提打开本地样本、驱动 `PlaybackGraph` 生命周期，并把 metrics 写成 `PlaybackQualityRunResult`；如果未来该层回归，`native-dx-offscreen-test` 会先暴露 render target 前提失败。

边界：这仍不是“真实播放证据”。该 smoke 不打开媒体、不解码、不运行 `PlaybackGraph`、不产生 frame pacing / A/V sync / color pipeline runtime evidence，也不证明 HDR、颜色或显示输出正确。

## 2026-07-08 更新：RenderedVideoFrames 不再在 surface render/present 失败时累加

`VideoRenderer::Render` 现在返回是否真的把当前帧写入 back buffer，`PlaybackGraph::RenderNextFrame` 只有在 `Render(...)` 和 `Present()` 都成功时才记录 render interval 并累加 `RenderedVideoFrames`。这避免未来 headless/no-surface runner 只要成功解码就误报“已渲染帧”。

边界：这不是播放效果优化，也不代表 headless runner 已经能真实打开媒体；它只是让 native metrics 在缺失 surface 或 present 失败时更诚实地区分 decoded frame 与 rendered frame。

## 2026-07-08 更新：PlaybackGraph open request 已脱离 WinRT runtimeclass

`PlaybackGraph::Open` 现在接收普通 native `PlaybackGraphOpenRequest`，不再直接接收 `NoiraPlayer::Native::NativePlaybackOpenRequest`。`NativePlaybackEngine` 负责把 WinRT/UWP request 转换成 graph request，再调用 `m_graph->Open(graphRequest)`。新增 `NativePlaybackGraphDecouplingContractTests` 防止 `PlaybackGraph.h` 重新 include `NativePlaybackEngine.g.h` 或把 WinRT runtimeclass 暴露回 graph open 参数。

边界：这一步只是把 WinRT runtimeclass 依赖从 native graph 内核往 adapter 层外推，降低后续 App-free native host 的耦合；它还没有创建 desktop/headless native host，也没有让 headless harness 真实打开 `PlaybackGraph`。

## 2026-07-08 更新：App-free native-headless harness 入口和结构化 blocker

新增 `tools/NoiraPlayer.PlaybackQuality.Headless`，可以在不启动、不打包、不部署 UWP App 的情况下，用公开 direct-uri 输入生成标准 `PlaybackQualityRunResult` captured report。新增 smoke `tools/quality-run/run-native-headless-harness-smoke-test.ps1` 覆盖 `captured report -> materialize-native-harness-report-set import -> validate-report-set -> analyze-report-set`，并已纳入 `run-playback-core-checks.ps1` 的 App-free 验证计划。

当前该 harness 不会伪造 `native-headless:returned-snapshot`，也不会把 skip-only report 伪装成 native playback evidence。它输出 `native-headless.native-link-blocked` 结构化 skip，明确记录当前 blocker：`NoiraPlayer.Native` 仍是 Windows Store C++/WinRT 组件，公开播放入口通过 UWP projection 暴露，surface API 绑定 `SwapChainPanel`；真实 App-free native open 需要先补一个 native graph host 或 render-surface 抽象。

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

验证状态：`tools\quality-run\run-playback-core-checks.ps1` 已恢复通过。App diff guard 仍保护 `src/NoiraPlayer.App`，但精确允许 DEBUG quality-run 和播放 metrics 采集所需的少量 instrumentation 文件。Plan 输出会暴露 `appDiffGuard.allowedPaths`，自动化模型可检查该例外没有扩展到 XAML、项目文件、manifest/package 或未列入的 App 改动。

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

- 当前 readiness audit 见 `docs/qa/playback-quality-v0.1-readiness-audit.md`。结论需要更新：v0.1 裁判框架和证据门禁已经基本成型，本轮已经补上可复现的 App-free native-headless 本地 report-set；但它还只是 smoke/report-set，不是归档 baseline/candidate。
- 轨道切换目前主要是发现/选择状态证据，尚未证明切换后的 native 播放行为完整正确。
- 字幕 v0.1 已用本地 mov_text 样本验证 stream discovery 和关闭状态，不验证字幕选择、解码或最终视觉渲染正确性。
- duration 和服务端明确返回的 chapters 已能从 Emby playback-info 的 media source 进入播放质量报告；这只覆盖章节元数据识别，不覆盖章节 UI、章节跳转或按章节 seek 行为。
- frame-pacing 仍是 `partial`，主要缺 `display.refreshRateHz`；HDR/HLG/DV 复杂样本、tone mapping、硬件输出和显示器行为仍未纳入纯软件闭环。
- v0.1 尚未完成真实播放采集 baseline/candidate 归档；source-only baseline 仍只证明评测链路闭环和缺失证据分类，不证明播放效果。
- WinRT App adapter 已能接入 native `QualityMetrics()`，独立 native-headless harness 也能产生本地真实播放软件指标；但归档 baseline/candidate 仍需要后续明确版本化产物。
- error-handling 目前能标准化错误 envelope，但真实 App/native harness 仍需要把实际异常、取消、超时和拒播原因映射到稳定 error code。

## 风险

- 缺失 telemetry 应归类为 `insufficient instrumentation`，不能直接当成播放器 bug。
- 私有 Emby 地址、账号、密码、真实 itemId/mediaSourceId 和本地报告只能放在 ignored 路径或环境变量中。
- 当前评测仍是纯软件闭环，不能证明 HDMI InfoFrame、显示器 EOTF 或肉眼颜色准确性。

## 下一步

下一步优先把 native-headless smoke 的产物提升为版本化 report-set baseline/candidate，并继续补 display refresh、HDR 复杂样本、切流/字幕选择行为和 error-handling native case；同时保持 core-probe 作为 App-free orchestration 回归守卫。
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

# 2026-07-08 更新：HDR / display refresh / frame pacing 软件证据闭环

本阶段继续保持“不做播放策略调优”的边界，只补评测证据。`native-headless` helper 现在会从 native `HdrDisplayRefreshRatePolicy` 输出 `displayRefreshRateHz` 和 `displayRefreshPolicy=software-only-cadence-policy`，C# headless harness 会把它写入标准 report 的 `display.refreshRateHz`，并在 limitations 中明确记录：`native-headless: display refresh is a software policy snapshot; HDMI/display output is not verified`。

`tools/quality-run/run-native-headless-harness-smoke-test.ps1` 现在会生成并运行本地样本矩阵：SDR 23.976fps、SDR 24fps、SDR 30fps、SDR 60fps、HDR10 23.976fps、HDR10 24fps、HDR10 30fps、HDR10 60fps，以及一个带 AAC/mov_text 的 A/V challenge 样本。所有样本都通过 native helper 实际打开 PlaybackGraph，进入 `captured -> materialize-native-harness-report-set -> validate-report-set -> analyze-report-set` 链路。

最新 smoke 结果显示 `native-analysis.json` 中 `totalReportCount = 9`，`frame-pacing` capability 为 `evidence-present`，`evidenceCaseCount = 9`，不再缺 `display.refreshRateHz`；`color` capability 覆盖了 4 个 HDR10 帧率 case。HDR10 样本实际解析为 `Hdr10 / HDR10 / bt2020 / smpte2084 / bt2020nc`，DXGI mapping 也从 runtime observation 进入 report。

边界仍然明确：这不是 HDMI、电视 EOTF、HDR InfoFrame 或肉眼颜色正确性的验证；`display.refreshRateHz` 在 headless 中是软件 policy snapshot，不代表系统真的切换了显示器刷新率。当前成果的意义是让模型可以基于可复现 report 判断 source color、DXGI mapping、cadence、frame interval、dropped/wait/starvation 等软件证据是否存在，然后再进入后续 Core 调优。

# 2026-07-08 更新：播放器 Core 调优 baseline/candidate 闭环

本阶段新增第一套本地可复现的播放器 Core 调优 baseline 编排：

- `tools/quality-run/New-PlaybackCoreTuningBaseline.ps1` 可以合并公开 manifest、ignored 私有 Emby manifest 和 native-headless 本地生成样本，输出统一 manifest、reports、validation、analysis、run plan 和 `baseline-summary.local.json`。
- `tools/quality-run/Compare-PlaybackCoreTuningCandidate.ps1` 使用 baseline manifest 校验 baseline/candidate report-set，并通过 `evaluate-candidate --match-by run-id` 生成 candidate evaluation、逐 case comparison 和 `comparison-summary.local.json`。
- native-headless smoke 现在接受并传递 `PlayerCoreVersion`、`SourceRevision` 和 `BuildConfiguration`，避免 baseline/candidate 的 native report 被误判为 same-build。

当前本地私有 baseline 输出位于 ignored 的 `docs/qa/private/baselines/playback-core-tuning-baseline.local/`。本轮完整 baseline 结果为：41 个 report，manifest/report-set validation 通过，native-headless 已包含，playback evidence 为 mixed/partial 且可评估 native software playback。

本轮还生成了 ignored 的 no-op candidate，并用同一 manifest 完成对比：41 个 comparison 全部 strong/unchanged，`decision = no-change`，无 blockers、无 regression、无 measured improvement。该结果说明评测链路已经可以闭合，但当前没有证据支持接受任何 Core/native 播放策略调整；因此本轮没有修改播放器 core/native 行为。

边界：这些输出仍是本地私有/ignored artifact，不提交真实私有 Emby case、itemId、mediaSourceId、URL、账号信息或 captured report。headless display refresh 仍是软件 policy snapshot，不代表 HDMI/display 硬件输出验证。

# 2026-07-08 更新：第一轮 Core/native 小步调优已由 baseline/candidate 采纳

本轮在已归档的 `docs/qa/private/baselines/playback-core-tuning-baseline.local/` 之上，只做了一个有 baseline 证据支撑的小步 native 播放策略调整：当 native `PlaybackGraph` 没有可用 audio clock 时，视频帧不再按 render loop / Present 速度尽快输出，而是用首帧 PTS 建立 video clock，并按帧时间戳等待。

触发原因来自 baseline：video-only native-headless 23.976/24/30fps 样本的 `timing.renderIntervalMsP95` 接近 16ms，明显比源帧时长更快。此前 evaluator 只限制“过慢/间隔过大”，没有识别“低帧率内容被过快渲染”的 cadence 问题。

本轮改动：

- `PlaybackFramePacing` 新增 `ShouldWaitForVideoClock`，`PlaybackGraph` 在无 audio clock 路径使用 wall-clock video PTS pacing。
- `PlaybackQualityEvaluator` 新增 `RenderIntervalMsP95Cadence` 检查：要求 matched display refresh case 的 P95 渲染间隔至少达到源帧时长的 75%。
- `PlaybackQualityRunComparator` 对 frame-ratio 派生信号改为按可接受区间 `0.75..1.5` 判断，而不是简单 lower-is-better，避免把“从过快接近目标帧时长”误判成回退。
- `PlaybackQualityRunComparator` 现在把 track/subtitle 稳定证据纳入 comparison matched signals，包括轨道数量、选中流、字幕关闭状态、音轨 codec/channels 和字幕 codec/language 等，避免这类证据只停留在 report-set analysis 中。
- native harness captured report 导入和 compare 路径会在有 manifest expected 时按当前 evaluator 规则重新评估，避免历史 raw/stale checks 干扰同一 manifest 的 candidate 比较。

已用同一 manifest 生成提交绑定 candidate：`docs/qa/private/candidates/playback-core-tuning-video-clock-61fecb3.local/`，`sourceRevision = 61fecb3`，并输出 ignored 对比：`docs/qa/private/comparisons/playback-core-tuning-video-clock-61fecb3.local/`。该 candidate 使用归档 baseline 的 `core-reference-manifest.local.json` 作为固定输入，避免当前私有 manifest 变化破坏可比性。

对比结论：41 个 case 全部可比，`accept-candidate` / `keep-candidate`，5 个 `frame-pacing` improvement，0 regression，0 mixed。改善集中在 video-only native-headless case：

- `local/native-headless-hdr10-23976`：`RenderIntervalMsP95` 16.752ms -> 48.101ms。
- `local/native-headless-hdr10-24`：16.780ms -> 48.596ms。
- `local/native-headless-hdr10-30`：17.068ms -> 46.731ms。
- `local/native-headless-sdr-23976`：16.308ms -> 47.449ms。
- `local/native-headless-sdr-24`：16.191ms -> 47.665ms。

边界：这是纯软件 native-headless 证据，不证明 Xbox HDMI 输出、HDR InfoFrame、显示器 EOTF 或真实影视素材观感。30fps case 目前落在可接受区间内但仍偏慢，后续应继续用同一 baseline/candidate 机制观察真实 A/V case、含音轨 case、网络媒体和更长样本，不应直接扩大结论。

# 2026-07-08 更新：第二轮调优先修正 seek/timeline 证据语义

本轮进入第二轮纯软件 Core 调优时，先对已接受 candidate 中的真实 native-headless A/V 样本做 root-cause 检查。发现 `local/native-headless-av-smoke` 的 `position.seekTargetPositionTicks = 0`，但 `actualPositionTicks = 15000000`。该现象不是 native `PlaybackGraph.Seek(0)` 失败，而是 native helper 在 `Seek(0)` 之后继续播放剩余 1.5 秒，再把 post-seek playback position 当作 seek landing 写入 `seekActualPositionTicks`。

本轮改动：

- `NativePlaybackGraphHeadlessSmokeTests.cpp` 在 `graph.Seek(0)` 后立即采样 `QualityMetricsSnapshot()`，用立即采样值写入 `seekActualPositionTicks`；继续播放后的 position 仅作为 helper stdout 的 `postSeekPlaybackPositionTicks` 调试信号输出，不再污染 seek landing evidence。
- `run-native-headless-harness-smoke-test.ps1` 增加 A/V materialized report 的 seek evidence 断言，避免该语义回退。
- `PlaybackQualityRunComparator` 增加 `position.seekPositionErrorMs` 的 timeline comparison。baseline/candidate 都有 seek error 证据时，comparison 会把该信号写入 `coverage.matchedSignals`，并按 lower-is-better 记录 timeline improvement/regression。

已生成 ignored candidate：`docs/qa/private/candidates/playback-core-tuning-seek-evidence-working.local/`，以已接受的 `playback-core-tuning-video-clock-61fecb3.local` 作为 baseline 做同 manifest 对比。结果：41 个 case 可比，`accept-candidate` / `keep-candidate`，9 个 `timeline` improvement，0 regression，0 mixed。`local/native-headless-av-smoke` 的 seek error 从 `1500.000ms` 降为 `0.000ms`，同时 A/V sync、audio track、subtitle track 和 DXGI matched signals 仍保留。

边界：这是评测器/native-headless helper 的证据修正，不是播放器 Core/native 播放策略优化。它让后续模型不会被错误 timeline evidence 带偏；下一步仍应继续聚焦真实含音轨样本的 frame pacing jitter、A/V sync、buffering 与更长样本稳定性。

# 2026-07-08 更新：runtime playback evidence 进入 candidate comparison

本轮继续补齐第二轮调优所需的 comparison evidence。此前逐 case comparison 已能看到 check、seek/timeline 和 track/subtitle 证据，但真实含音轨 native-headless A/V case 中的 frame pacing jitter、buffering 和更完整 A/V sync runtime telemetry 仍主要停留在 report 本体，未进入 comparison 的 `coverage.matchedSignals`。

本轮改动：

- `PlaybackQualityRunComparator` 在 baseline 和 candidate 都有 runtime playback evidence 时，把 timing、sync 和 buffering 信号写入 matched signals。
- 新增覆盖信号包括 `timing.renderIntervalMsP95/P99/maxFrameGapMs/videoAheadWaitCount`、`sync.audioClockTicks/videoPositionTicks/audioVideoDriftMsP50/P95/P99/Max`、`buffers.submittedAudioFrames/queuedAudioBuffers/videoStarvedPasses/audioStarvedPasses`。
- 这些无阈值 runtime telemetry 只作为 comparison evidence 暴露，不因为一次采样波动自动制造 improvement/regression；candidate 是否可采纳仍由 checks、明确派生 delta 和既有 gate 判断。

重新用同一 41-case manifest 对比 `playback-core-tuning-video-clock-61fecb3.local` 与 `playback-core-tuning-seek-evidence-16ba684.local`，输出 ignored comparison：`docs/qa/private/comparisons/playback-core-tuning-runtime-evidence-working.local/`。结果仍为 `accept-candidate` / `keep-candidate`，41 case 可比，9 improved，0 regression，0 mixed，strong confidence 41/41。`local/native-headless-av-smoke` 的 matched signals 已同时包含 frame pacing jitter、buffering、A/V sync、seek/timeline、track/subtitle 和 color/DXGI 证据。

边界：这是 comparison artifact 的模型可消费性增强，不改变播放器行为、不改变 evaluator 阈值、不放宽样本预期，也不把 jitter 数值本身解释为已优化。下一步才适合基于这些完整 comparison signals 判断是否需要实际调优含音轨播放的 frame pacing 或采样窗口。

# 2026-07-08 更新：Present duration 证据补齐，jitter 初步定位到 Present 前

本轮在已接受的 `playback-core-tuning-seek-evidence-16ba684.local` 之后补齐 `DxDeviceResources.Present()` 调用耗时证据。新增 `timing.presentDurationMsP50/P95/P99/Max`，并让该信号进入 signal catalog、单报告 `modelAnalysis.evidenceSignals`、native-headless smoke 断言和 candidate comparison `coverage.matchedSignals`。

已用上一轮 accepted candidate 的 41-case core manifest 生成新的 ignored candidate：`docs/qa/private/candidates/playback-core-tuning-present-duration-41case-working.local/`。同 manifest 对比输出为 `docs/qa/private/comparisons/playback-core-tuning-present-duration-41case-working.local/`：41/41 case 可比，candidate validation 通过，`decision = no-change`，0 improvement，0 regression，0 mixed，41 个 strong confidence。

关键证据：`local/native-headless-av-smoke` 的 `timing.renderIntervalMsP95` 约为 47ms，但 `timing.presentDurationMsP95` 约为 0.07ms，说明当前 native-headless A/V jitter 主要不在 swapchain Present/vsync blocking 内，而更可能发生在 Present 前的 render loop、audio-clock gating、decode 或等待路径。

边界：这是诊断证据增强，不是播放策略优化。该 candidate 不应被标记为 accepted quality improvement；下一轮应继续沿同一 41-case manifest，聚焦 Present 前的调度/等待路径，优先补足或调整能解释 `videoAheadWaitCount`、render interval 和 A/V drift 关系的证据或小步策略。
# 2026-07-08 更新：audio-ahead wait duration 证据补齐，A/V jitter 初步定位到 coarse sleep / audio clock gating

本轮在已接受的 `playback-core-tuning-seek-evidence-16ba684.local` 之后补齐 `timing.audioAheadWaitDurationMsP50/P95/P99/Max`。该信号记录 pending video frame 因 `ShouldWaitForAudio` 等待音频时钟追上所花的时间，用来区分“视频渲染/Present 慢”与“Present 前等待音频时钟”。

已用同一 41-case manifest 生成 ignored candidate：`docs/qa/private/candidates/playback-core-tuning-audio-wait-evidence-41case-f7f7315.local/`，并输出 comparison：`docs/qa/private/comparisons/playback-core-tuning-audio-wait-evidence-41case-f7f7315.local/`。结果：41/41 case 可比较，baseline/candidate validation 均通过，`decision = no-change`，0 improved，0 regressed，0 mixed，41 strong confidence。`local/native-headless-av-smoke` 的 matched signals 已包含 `timing.audioAheadWaitDurationMsP50/P95/P99/Max`、`timing.presentDurationMs*`、render interval、A/V drift、buffering、seek/timeline、track/subtitle 和 color/DXGI 证据。

关键 A/V 样本证据：该 case 为 30fps SDR H.264/AAC，software display cadence 为 60Hz。candidate 中 `renderIntervalMsP50/P95/P99` 约为 `31.5/47.1/47.6ms`，`presentDurationMsP95` 约 `0.08ms`，`audioAheadWaitDurationMsP50/P95/P99/Max` 约 `15.6/31.3/31.3/31.3ms`，`videoAheadWaitCount = 52`，`audioVideoDriftMsP95 = 10ms`。这说明当前 jitter 主要不在 swapchain Present，而集中在音频时钟 gating 与 render loop 等待路径。

本轮还做了一个最小策略实验：把 `PlaybackFramePacing::RenderLoopWait()` 从 5ms 降到 1ms。targeted native test 先按 TDD 红灯失败，改实现后绿灯，但 native-headless A/V smoke 未改善：`renderIntervalMsP95` 基本不变，`audioAheadWaitDurationMsP99/Max` 反而变差。因此该实验已回退，不采纳为播放策略调整。

当前结论：不要继续盲目调整 audio ahead tolerance 或固定 sleep 常量。`audioAheadWaitDuration` 的 15.6ms / 31ms 形态更接近 Windows coarse timer quantum，下一步应优先研究并验证更可靠的 render loop 等待 primitive 或 wait scheduling，而不是放宽/收紧 evaluator 阈值。

边界：这是诊断证据增强和一次被拒绝的小实验，不是可采纳的播放质量优化。它不证明真实 Xbox/HDMI 输出、HDR、A/V sync 或主观流畅度已经改善。后续任何等待策略调整仍必须走同一 41-case manifest 的 baseline/candidate comparison，并明确记录 improvement/regression/mixed 与剩余风险。

# 2026-07-08 更新：near-threshold audio catch-up wait 实验不采纳

本轮继续沿已接受的 `playback-core-tuning-audio-wait-evidence-41case-f7f7315.local` 做第二轮纯软件 Core 调优实验。假设是：A/V smoke 中 15ms/31ms 级别的 audio-ahead 等待可能来自固定 5ms sleep 的粗粒度唤醒；如果 pending video frame 只比音频时钟超前少量时间，则改用更短的 1ms wait 可能减少阈值附近 overshoot。

按 TDD 做了最小 native 策略实验：

- `PlaybackFramePacing::AudioCatchUpRenderLoopWait`：远离阈值仍保持默认 5ms；当 `framePosition - audioPosition - VideoAheadTolerance <= 6ms` 时返回 1ms。
- `PlaybackGraph` 在 `ShouldWaitForAudio` 分支把下一轮 render loop wait 改为该动态值。
- targeted native test 先红灯失败，再实现后绿灯；native UWP Debug x64 build 通过。

已生成 ignored candidate：`docs/qa/private/candidates/playback-core-tuning-audio-catchup-wait-41case-working.local/`，并输出 comparison：`docs/qa/private/comparisons/playback-core-tuning-audio-catchup-wait-41case-working.local/`。结果：41/41 case 可比，baseline/candidate validation 均通过，`decision = no-change`，0 improved，0 regressed，0 mixed，41 strong confidence。

关键 A/V case 对比：

- baseline `local/native-headless-av-smoke`：render P50/P95/P99 `31.5218/47.0978/47.6411ms`，audio-ahead wait P50/P95/P99/Max `15.6418/31.2524/31.2728/31.2728ms`，`videoAheadWaitCount = 52`，drift P95 `10ms`。
- candidate：render P50/P95/P99 `31.4879/47.1216/47.7064ms`，audio-ahead wait P50/P95/P99/Max `15.4844/31.4748/38.0209/38.0209ms`，`videoAheadWaitCount = 52`，drift P95 `10ms`。

结论：该候选没有可采纳的质量改善，且 audio-ahead wait P95/P99/Max 变差，decoded/submitted/queued audio 也略降。因此实验代码已回退，不作为播放器 Core/native 策略调整提交。

下一步建议：不要继续围绕固定 sleep 常量或小窗口阈值试错。下一轮应优先补充更能解释调度行为的证据，例如 audio-ahead wait 的目标剩余时间、实际 oversleep delta、render pass 间隔原因分类，或改造为可复用/低开销的等待 primitive 后再做同 manifest candidate comparison。

# 2026-07-08 更新：audio-ahead wait target/oversleep 证据进入 41-case comparison

本轮在已接受的 `playback-core-tuning-audio-wait-evidence-41case-f7f7315.local` 之后补齐 `timing.audioAheadWaitTargetMsP50/P95/P99/Max` 和 `timing.audioAheadWaitOversleepMsP50/P95/P99/Max`。前者记录进入 `ShouldWaitForAudio` 时理论上还需要等音频追上阈值多久；后者记录实际等待时长超过该目标的 delta。

已用同一 41-case manifest 生成 ignored candidate：`docs/qa/private/candidates/playback-core-tuning-audio-wait-target-evidence-41case-8e13b26.local/`，并输出 comparison：`docs/qa/private/comparisons/playback-core-tuning-audio-wait-target-evidence-41case-8e13b26.local/`。结果：41/41 case 可比，baseline/candidate validation 均通过，`manifest.sameCaseIds = true`，`decision = no-change`，0 improved，0 regressed，0 mixed，41 strong confidence。

关键 A/V 样本证据：`local/native-headless-av-smoke` 中 candidate 的 `renderIntervalMsP50/P95/P99` 约为 `31.6/47.5/47.6ms`，`audioAheadWaitDurationMsP50/P95/P99/Max` 约为 `15.6/31.3/40.0/40.0ms`，`audioAheadWaitTargetMsP50/P95/Max` 约为 `13.3/23.3/23.3ms`，`audioAheadWaitOversleepMsP50/P95/Max` 约为 `5.2/16.9/17.8ms`。这说明当前 A/V jitter 的一部分不是单纯策略目标过大，而是实际唤醒相对目标存在明显 oversleep。

边界：这是诊断证据增强，不是播放策略优化；它不改变 evaluator 阈值、不放宽样本预期，也不证明真实 Xbox/HDMI 输出或主观流畅度改善。下一步更适合小步验证等待 primitive / wait scheduling，而不是继续盲调固定 sleep 常量或 audio-ahead 阈值。

# 2026-07-08 更新：audio target precise wait 候选暂不采纳

本轮基于 `audioAheadWaitTargetMs*` / `audioAheadWaitOversleepMs*` 做了一个最小策略候选：当 `PlaybackGraph` 因 `ShouldWaitForAudio` 暂停 pending video frame 时，下一轮 render loop 不再固定 sleep 5ms，而是按当前 frame/audio 差值计算 audio-ahead target，并用 yield-based precise wait 等到目标时间。

该候选按 TDD 增加 `PlaybackFramePacing::AudioAheadWaitDuration` 测试，先红灯失败，再实现后通过；native UWP Debug x64 build 通过；native-headless smoke 通过。随后用同一 41-case manifest 生成 ignored candidate：`docs/qa/private/candidates/playback-core-tuning-audio-target-precise-wait-41case-working.local/`，并输出 comparison：`docs/qa/private/comparisons/playback-core-tuning-audio-target-precise-wait-41case-working.local/`。结果：41/41 case 可比，validation 通过，`decision = no-change`，0 improved，0 regressed，0 mixed，41 strong confidence。

目标 A/V case 的诊断信号有改善：`renderIntervalMsP95` 约 `47.5ms -> 38.0ms`，`audioAheadWaitOversleepMsP50/P95/Max` 约 `5.2/16.9/17.8ms -> 0.1/6.8/6.8ms`，`audioVideoDriftMsP95` 保持 `10ms`，submitted/queued audio 未下降。但 evaluator 当前只把这些信号作为 matched diagnostic evidence，不自动判定 improvement。

结论：该候选证明“按 audio target 等待”方向比继续调固定 sleep 常量更有希望，但暂不采纳。原因是 suite 决策仍为 `no-change`，且 yield-based precise wait 可能引入 CPU 成本，当前评测器还没有 CPU/调度开销证据。实验代码已回退；下一步应优先补充 wait reason / CPU 或低开销高精度 wait primitive，再做同 manifest candidate comparison。

# 2026-07-08 更新：high-resolution waitable timer 候选已进入主线，但仅作为 no-regression 低开销 primitive

本轮把上一轮 yield-based precise wait 改成低开销 waitable timer 候选：新增 `RenderLoopWaiter`，在 audio-ahead gating 时按 `AudioAheadWaitDuration` 使用 high-resolution waitable timer 等待目标时间；不可用时自动回退普通 waitable timer 或 `sleep_for`。该改动已提交为 `38ae764 chore: add high resolution render loop wait candidate`。

已重新生成 commit 绑定的 41-case candidate：`docs/qa/private/candidates/playback-core-tuning-highres-wait-41case-38ae764.local/`。同 manifest 对比输出：`docs/qa/private/comparisons/playback-core-tuning-highres-wait-41case-38ae764.local/`。对比基线为 `playback-core-tuning-audio-wait-target-evidence-41case-8e13b26.local`。

评测结果：41/41 case 可比，baseline/candidate validation 均通过，`manifest.sameCaseIds = true`，`decision = no-change`，0 improved，0 regressed，0 mixed，41 strong confidence。`local/native-headless-av-smoke` 的关键诊断信号为：

- render P50/P95/P99：`31.6/47.5/47.6ms -> 33.7/41.0/41.7ms`。
- audio-ahead wait P50/P95/P99：`15.6/31.3/40.0ms -> 20.3/27.7/30.7ms`。
- audio-ahead oversleep P50/P95/P99：`5.2/16.9/17.8ms -> 0.6/7.6/14.3ms`。
- A/V drift P95 保持 `10ms`；submitted/queued audio 为 `81/11 -> 82/12`；track/subtitle、seek/timeline、buffering 和 color/DXGI matched signals 均保留。

当前结论：该候选可以保留为低开销等待 primitive，但不能宣称播放器质量已提升。原因是 suite 仍为 `no-change`，且还没有 CPU/调度开销、长样本稳定性或真实设备输出证据。剩余风险是 `videoAheadWaitCount` 增加 `51 -> 71`，`audioVideoDriftMsP50` 从约 `3.3ms` 增至 `6.7ms`；后续应补齐 wait reason / CPU evidence 或更长 A/V 样本，再决定是否把 audio-ahead oversleep 纳入明确 improvement/regression 规则。

# 2026-07-08 更新：native helper process-cost 证据已进入 41-case report-set

本轮在 `b292019 chore: expose native helper process cost evidence` 后，重新用已接受的 41-case manifest 生成了 candidate：

- candidate：`docs/qa/private/candidates/playback-core-tuning-process-cost-evidence-41case-b292019.local/`
- comparison：`docs/qa/private/comparisons/playback-core-tuning-process-cost-evidence-41case-b292019.local/`
- baseline：`docs/qa/private/candidates/playback-core-tuning-highres-wait-41case-38ae764.local/`
- 结果：41/41 case 可比，candidate validation 通过，`manifest.sameCaseIds = true`，`decision = no-change`，0 improved / 0 regressed / 0 mixed / 41 unchanged，strong confidence 41/41。

关键 A/V native-headless 样本 `local/native-headless-av-smoke` 中，candidate 已输出新的 process-cost 证据：

- `runtimeMetrics.processWallClockMs = 3476.8989`
- `runtimeMetrics.processCpuTimeMs = 562.5`
- `runtimeMetrics.processCpuUtilizationRatio = 0.16178209840959137`
- `modelAnalysis.evidenceSignals` 已包含 `runtimeMetrics.processWallClockMs`、`runtimeMetrics.processCpuTimeMs` 和 `runtimeMetrics.processCpuUtilizationRatio`。

对比中的播放质量结论保持保守：这次改动是 instrumentation/testability 增强，不是播放策略优化。`local/native-headless-av-smoke` 的 render interval、A/V drift、audio-ahead wait、buffering、seek/timeline、track/subtitle 和 color/DXGI 证据保持可比，但 suite 仍然判定 `no-change`。

重要边界：旧 baseline `38ae764` 没有 process-cost 字段，因此本轮不能判断 high-resolution waitable timer 的 CPU 成本是否改善或退化；它只能证明新的证据链已经可用。下一轮如果要评价 CPU/process-cost 变化，应以 `b292019` 生成的 candidate 作为带 process-cost schema 的新 baseline，再生成后续 candidate 做同 manifest 对比。

# 2026-07-08 更新：audio prewake margin 候选不采纳

本轮尝试了一个最小 native 调度候选：在 `PlaybackFramePacing::AudioAheadWaitDuration` 中，当 audio-ahead target 大于 2ms 时提前 2ms 醒来重新采样，而不是一次性等待到理论边界。TDD 过程为：先修改 `FramePacingTests.cpp` 使当前实现失败，再实现 `AudioAheadPreWakeMarginTicks = 20000` 后通过 targeted native frame pacing test。

提交前完整验证已通过：`tools/quality-run/run-playback-core-checks.ps1` 退出码为 0，覆盖 Core tests、quality CLI、native-headless smoke、candidate comparison script tests 和 native build。随后曾提交为 `aa2eddc chore: prewake audio ahead render wait`。

已生成 commit-bound 41-case candidate 和 comparison：

- candidate：`docs/qa/private/candidates/playback-core-tuning-audio-prewake-margin-41case-aa2eddc.local/`
- comparison：`docs/qa/private/comparisons/playback-core-tuning-audio-prewake-margin-41case-aa2eddc.local/`
- baseline：`docs/qa/private/candidates/playback-core-tuning-process-cost-evidence-41case-b292019.local/`
- 结果：41/41 case 可比，validation 通过，`manifest.sameCaseIds = true`，`decision = no-change`，0 improved / 0 regressed / 0 mixed / 41 unchanged，strong confidence 41/41。

目标 A/V native-headless case 的 commit-bound 指标是 mixed，不支持采纳：

- render P50/P95/P99：`33.2845/40.179/40.7123ms -> 32.9412/40.771/41.1449ms`
- audio-ahead wait P50/P95/P99：`27.0436/31.3399/31.3715ms -> 24.9388/33.2936/34.6695ms`
- audio-ahead oversleep P50/P95/P99：`0.5162/8.0066/8.0382ms -> 0/6.7835/6.9554ms`
- A/V drift P50/P95 保持 `6.6667/10ms`
- process CPU ratio：`0.16178209840959137 -> 0.1522388292376957`
- `videoAheadWaitCount` 增加 `71 -> 91`
- submitted/queued audio 保持 `82/12`

结论：该候选降低了 oversleep 和进程 CPU ratio，但没有稳定改善 render P95/P99，且增加了 audio-ahead wait 次数。因此不作为 accepted candidate 保留；`aa2eddc` 的代码改动已回退。后续不要继续靠固定 prewake margin 调参，应优先补充更稳定的多次采样/长样本证据，或设计能同时降低 oversleep 与 render interval 的更明确调度策略。
