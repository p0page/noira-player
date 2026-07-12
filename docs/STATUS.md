# 当前状态

播放质量评测体系正在推进 v0.1，目标是先把评测做成可信裁判，而不是优化播放效果。

## 2026-07-12 更新：正式 baseline 必须重新绑定 manifest expected

v10 跨 case 审计发现，native manifest runner 直接把 captured report 放入正式 `reports`，没有经过现有 materializer 重新绑定 manifest expected。结果是私有 audio-switch manifest 明明声明 `maxStartupDurationMs=7000`，报告 expected 却只剩 helper 自带的 `requireValidatedConversion=true`；8535ms 启动、3279ms 最大帧间隔和 35 个 seek-preroll drop 因此仍显示 pass。该问题归类为 eval harness bug，v10 不能作为调优裁判。

baseline 编排现在把 runner 原始输出隔离到 `captured-reports`，根据 runner summary 的实际 case 集生成 `executed-core-manifest`，再通过 `materialize-native-harness-report-set` 绑定完整 expected、按当前规则重新评测后写入正式 `reports`。quarantine 仍只存在于 unified manifest，不生成假 skip。测试覆盖 expected 传递、错误 source locator 不被 materializer 修正、以及选中 case 缺报告必须失败。下一步以干净提交生成 v11，并以 v11 的真实 fail/unsupported 结果选择 Core 候选。

## 2026-07-12 更新：实际媒体身份不再由 URL 冒充，网络恢复进入统一 native 基线

评测器此前把 `openedSourceHash` 实际计算为去掉 `PlaySessionId` 后的 URL 哈希，因此它经常与 `sourceLocatorHash` 等价，不能证明 helper 真正打开了 manifest 声明的媒体。这是评测可信度缺陷，不是播放器优化成果。现在 native-headless 与 App-hosted 都在真实采样完成后，根据实际解析到的 container、duration、视频属性、色彩元数据和全部视频/音频/字幕轨生成 `observed-media-signature-v1`；execution 同时记录 `openedSourceHashKind`。strict validator 与 baseline/candidate comparator 会拒绝类型缺失、旧 URL 语义或类型不一致的报告，旧 baseline 必须重建，不能静默混比。

确定性网络恢复 case 也从 gate 内部孤立检查提升为统一 native report-set 的正式 stable case。manifest 使用固定 `local-fault://network-reconnect-pause-resume` 关联测试意图，运行时 source map 才把它解析到随机 localhost 端口；临时端口不再改变 locator identity。最新统一集合为 11 个 case，strict validation 记录 completed 11、rendered 11，analysis 消费 11 份真实 native 报告。网络 case 实际观察到第二次非零 Range 请求、137 帧呈现、`result=pass`，且 locator hash 与 observed media hash 明确不同。

验证：Core 全量 904/904；32 阶段 playback-core gate 全部通过；native-headless smoke 从构建 helper、真实播放、故障注入、materialize、strict validate 到 analyze 全链路通过；Debug x64 Native AOT/UWP Publish 成功且无 AOT/trimming blocker。

已基于提交 `9067de1` 生成 ignored 的 `playback-evidence-v10-observed-media.local` 正式 baseline：统一 manifest 共 29 个 case，其中 24 个 stable/challenge 均真实执行并产生报告，5 个 quarantine 明确缺席；strict validation 的 executionValid 为 true。结果为 23 pass、1 unsupported（公开 DV Profile 5）、0 fail、0 error、0 missing；24 份 opened report 全部使用 `observed-media-signature-v1`，0 类型缺失、0 locator alias。私有凭据只经进程环境变量注入，baseline 文件中敏感值扫描命中为 0。旧 baseline 不得用于候选改善结论。

完整 App 也已注册并用公开 SDR case 生成首份新格式 App-hosted 报告：`jellyfin/sdr-hevc-main10-1080p60-3m` 为 pass/completed，runner 与 evidence level 均为 app-hosted，解码/呈现 147 帧，conversion validated，startup 3315.578ms，max gap 518.9512ms；opened media signature 类型正确且不与 locator alias。该结果未调整任何 expected 或阈值。下一步可以在 v10 baseline 上进入小步 Core/native candidate 调优。

## 2026-07-12 更新：拒绝 Matroska 跳过流探测候选，修复 seek 与导出证据丢失

后续已补齐真实 App timeline 的 6 项 instrumentation。native metrics/WinRT/Core 现在贯通 container start、video stream start、demux seek target 和 seek 后首个实际呈现位置；App 在完整采样窗口结束、Stop 之前记录 post-seek position 与是否继续推进。同一“一战再战” full-probe case 最终得到：seek target 与 demux target 均为 `631000000` ticks，首帧 `633550000` ticks，seek error `255ms`，随后位置 `698610000` ticks，`postSeekAdvanced=true`，137 帧实际呈现。6 个 `SeekTimelineEvidence missing` 已全部消失，唯一失败为远端总启动 `20224.607ms > 7000ms`，timeline 与 startup 现在可以独立归因。

针对真实 App 启动瓶颈，曾实现一个受限候选：仅在 Matroska 头已包含完整主视频、全部音轨、全部字幕轨和 duration 时跳过 `avformat_find_stream_info`，内嵌封面不作为可播放视频轨。候选在“一战再战”中保留 2 条视频、2 条音频、5 条 PGS 字幕，音轨切换成功，PGS 切换解码 25 个 cue 并完成 128 次合成；但两轮 timeline 的 seek 误差为 624ms、2012ms，后一轮 15 秒仅呈现 3 帧。恢复完整探测后，同一远端 case 仍出现 964ms seek 误差和仅呈现 9 帧，说明远端 seek/preroll 还有独立问题，但候选没有证明稳定收益，最终已撤回，继续采用 Kodi/VLC 一样的完整 FFmpeg 流探测。

本轮同时修复两个独立评测器缺陷。`Seek()` 过去会清空 native open、FFmpeg open 和 stream-info timing，导致执行过 seek 的报告缺少启动分解；现在 seek 只清运行期样本并保留 open timing。App-hosted gate 过去只等待文件名出现，异步 JSON 尚未完整落盘时可能在 `Resolve-Path`/导出阶段失败；现在必须成功读取并解析完整 JSON 才继续。最终 full-probe timeline 报告在真实 seek 后仍记录 `native.open=30465.833ms`、`find_stream_info=1530ms/status=measured`，导出和分析正常完成。

验证：Core 全量 898/898、纯 C++ 指标测试、32 项 playback-core gate、Debug x64 Native AOT/UWP Publish 均通过。“哈姆奈特”HDR manifest 当前发生源漂移：预期 4K DV/HDR10 源实际打开为 1080p HDR10，报告以 source mismatch 拒绝；该结果归类为样本/服务环境问题，不作为 HDR Core 通过证据。下一步应修复 timeline 报告缺失的 demux/post-seek 字段，并把远端 seek 后低呈现率拆成可归因的 demux、preroll、网络等待证据。

## 2026-07-12 更新：native open 已拆为可归因子阶段

`native.open` 现在保留为只计一次的父阶段，内部结构化记录 FFmpeg `open_input`、`find_stream_info`、native decoder/renderer/首帧初始化和 host dispatch overhead。模型分析会分别输出主导子项、子项合计、未归因和重叠时长；App-hosted 冻结 snapshot 同时补齐此前遗漏的硬解/软解帧计数。三个原生计时值已贯通 FfmpegMediaSource、PlaybackGraph metrics、WinRT IDL/runtimeclass、App provider 和 Core report，不再依赖人工解析日志。

真实私有 Emby 长暂停 case 的一次采样总启动 10497ms，因超过既有 7000ms 标准而诚实 `fail`。其中 `native.open=8939.509ms`，子项为 `open_input=3987ms`、`find_stream_info=4726ms`、native 初始化/首帧 108ms、host dispatch 119ms，子项缺口和重叠均为 0。硬解帧 494、软解帧 0；播放阶段 max gap 67ms、render P95 48.3ms、A/V drift P95 13ms。证据表明启动波动主要在 FFmpeg 网络打开和流探测，不应通过调整解码或帧节奏策略解决。

## 2026-07-12 更新：App 启动已分段归因，暂停不再污染帧节奏

App-hosted 报告新增可扩展的 `startup.stages`，当前覆盖 App/会话准备、Emby PlaybackInfo、源选择、native surface、native open 调度和 native open。模型分析同时输出主导阶段、已归因耗时和未归因耗时。真实私有 Emby 长暂停 case 中，总启动 5393ms 被完整归因，未归因 0ms；主导项为 `native.open=4209ms`，PlaybackInfo 为 658ms。复跑时 `native.open=3332ms`，原生日志进一步显示其中 FFmpeg `avformat_open_input=2843ms`、`avformat_find_stream_info=266ms`，剩余约 223ms 为 decoder、renderer、首帧和线程启动。

同一 case 还暴露出评测器把 10 秒主动暂停计入 `maxFrameGapMs=10054ms`。根因是 `PlaybackGraph` 在 pause/resume 边界保留了上一帧 present 时间。当前实现只断开 presentation interval continuity，不清空既有 histogram，也不改变播放时钟或解码策略。完整 App 前后对比中，暂停恢复、position 前进、渲染帧增长和 A/V drift P95=13ms 均保持成功；render interval 样本量保持 492/488，错误的 10054ms 最大间隔降至 53ms。Core 全量测试 892/892、纯 C++ 连续性测试和 Debug x64 Native AOT/UWP Publish 均通过。

## 2026-07-11 更新：baseline/candidate 跨执行证据比较已被阻断

`PlaybackQualityRunComparator` 现在先验证执行证据可比性，再计算任何改善或回退。两侧必须具有完整的真实播放 execution、相同 evidence level、相同 runner、相同匿名 source locator；若两侧均成功打开媒体，还必须具有相同 opened source hash。`completed` 的 pass/fail 报告还必须证明 source open、native graph、demux、decoder 和 playback sample 均真实发生。任一条件不满足时，比较固定输出 `insufficient-evidence`，不再让纸面 telemetry 变化产生 improvement/regression。

CLI smoke、Core comparator/suite 测试和 candidate 编排测试已覆盖 orchestration/native 混比、locator 不同、实际打开源不同及执行证据不完整。candidate 脚本端到端测试还证明：即使候选的帧间隔数字明显更好，只要 opened source hash 不同，suite 仍必须拒绝采纳。正式 baseline 已在上一提交停止生成 core-probe actual；probe 仅保留为 evaluator self-test。

## 2026-07-11 更新：长暂停后的 HTTP 断线已改为有界恢复

完整 App 日志确认：播放约 15 秒后暂停约 5 分半，恢复 276ms 后 `av_read_frame` 返回 `I/O error`，旧播放器立即进入 `Failed`。当前 FFmpeg HTTP/HTTPS 输入已启用有界重连：最多 3 次，单次延迟不超过 2 秒、累计延迟不超过 6 秒；本地文件不受影响，耗尽后仍保留原始失败。

本轮新增独立的 native 长暂停场景，并建立本地可控 Range 故障服务器。相同 30 秒 H.264/AAC 样本在 3,145,728 字节处强制 RST 时，`main` 旧 helper 出现 partial/corrupt packet、`playbackFailed=1`、退出 2；候选明确输出 FFmpeg `Will reconnect`，发起第二个 Range 请求，继续解码并以 `playbackFailed=0`、退出 0 完成。该故障注入现已接入 native-headless gate，整套脚本最终输出 `native network reconnect smoke passed` 与 `native-headless-harness smoke ok`。

私有 Emby 同源验证中，候选在约 12.8 秒处暂停 360 秒，恢复后持续推进到约 42.8 秒并解码 1285 帧，没有进入 `Failed`。Core 全量测试通过，Debug x64 完整构建通过；Native AOT AppX layout 已重新生成、注册并启动为 `0.1.0.279`，运行路径指向当前修复 worktree。尚未在新 App UI 中再次等待 5 分半复跑同一人工操作，但 Core 断线恢复已有确定性旧版/候选软件证据，且完整 App 已携带并启动新 native DLL。

## 2026-07-11 更新：播放修复已完成完整 App 构建与 App-hosted 实播

提交 `061be2c` 之后不再只以 App-free helper 作为结论。本轮从干净 Native/App 输出重新执行 Debug x64 Native AOT Publish，修复了现代 App 项目在 `BuildNoiraWebClient` 执行前检查 `dist/index.html`、导致 `WebCode` 被静默漏出 AppX recipe 的目标顺序错误；注册脚本现在也强制验证 `WebCode\index.html`。修复后的完整 AppX layout 包含 Web、Core、Native、FFmpeg 和 AOT executable，重新注册后成功启动、恢复本地 Emby 会话并加载真实媒体库。

完整 App 实播中，“一战再战”的 `1080p / 15 Mbps, HEVC + DDP7.1` 源持续推进超过 1 分钟；“哈姆奈特”的 `4K / 24 Mbps, HEVC - HDR + TrueHD7.1` 源从 0 开始持续推进。后者日志中音频与视频时钟按墙钟速度推进且长期保持在毫秒级附近，多次真实出现 video send/receive 双 `EAGAIN`，均在第 1 次有界重试恢复，没有进入 `Failed`，因此主体慢放和立即崩溃问题已在 App-hosted 路径得到验证。

剩余风险：哈姆奈特实播中观察到一次约 6 秒没有新视频呈现、随后丢弃积压视频帧追上仍在推进的音频时钟。当前证据更接近网络/供帧瞬时停顿，而不是旧的 TrueHD 主时钟极慢；仍需作为独立 buffering/cadence case 采集，不应被本轮结论掩盖。本轮验证是 Windows App-hosted，仍不替代 Xbox/HDMI 实机验证。

## 2026-07-11 更新：合并 main 后修复真实 HEVC/TrueHD 慢放与双 EAGAIN

当前调优分支已通过合并提交 `3427469` 纳入本地 `main` 的 FFmpeg 8.1.2、VS2026/.NET 10、WebView hybrid 和 UI 线程修复。合并只在 `NativePlaybackGraphDecouplingContractTests.cs` 产生冲突，最终保留两边契约，11 个相关测试通过。对比确认 `VideoDecoder.cpp` 在合并前两侧完全一致，因此本次故障不是当前调优分支已经修复而 `main` 遗漏；两边都包含相同的致命双 `EAGAIN` 分支。

本机真实日志确认了两个独立根因。哈姆奈特从非零位置恢复时，TrueHD 解码帧可能缺少时间戳且每帧只有约 40 samples；旧代码把缺失时间戳重置为 0，并让每个小帧占用一个 XAudio buffer，导致视频已到约 2.5 秒而音频主时钟每约 2.4 秒只推进 10ms。现在 `AudioFrameTimeline` 从 seek 锚点连续合成缺失时间戳，`AudioBufferAccumulator` 把小帧合并到至少 20ms 后提交，并在 flush/stop/流尾正确清理或排出。候选 App-free 实播的 23.976fps render interval P50 约 `41.9ms`，A/V drift P95 `13ms`，不再复现极慢推进。

另一个确定性 4K HEVC/EAC3 私有源会在 16 帧、媒体位置约 0.642 秒时触发 D3D11VA `avcodec_send_packet(EAGAIN)` 与 `avcodec_receive_frame(EAGAIN)` 同时发生。现在视频和音频解码器都保留原包并做最多 4 次有界恢复，任何送包或收帧进展都会清零计数，耗尽仍以明确诊断失败。相同源从 helper 退出 2、`playbackFailed=1` 变为退出 0、125 rendered frames、媒体位置约 5.002 秒；随后三轮重复均为 126 decoded / 125 rendered / `playbackFailed=0`。一次重复的交互 seek 仍失败，应作为单独的交互/网络 case，不覆盖主体播放结论。

本次也确认此前评测器不足以充当真实解码回归门禁：主要 native case 使用 320x180、3–6 秒、AAC/H.264 或简单生成媒体；公开 HEVC manifest 很多只做计划/报告形状验证；helper 未订阅 render-thread failure；整段最多三次重试会隐藏首轮失败。现在 helper 把 `PlaybackGraphState::Failed` 转成 `playbackFailed=1` 和非零退出，native smoke 不再静默整段重试，三个纯 native 测试已进入统一 Core 检查计划。仍需后续把私有 Emby case 的媒体推进率、最低帧数和恢复计数正式写入本地 manifest/report-set，而不是只依赖人工命令输出。

## 2026-07-11 更新：post-present audio deadline 合并候选判为 mixed，不采纳

在 `486c969` evidence baseline 上，候选 `3419ea7` 尝试合并成功 A/V present 后的固定 `5ms` render-loop wait 与下一帧 audio-aware wait：当前帧具有音频时钟时，成功 present 后立即进入下一轮检查；无音频时钟和失败路径保持原有 `5ms` 等待。完整 App-free gate 通过，覆盖 457 个 Core tests、CLI/headless/质量工具测试、native helper/frame-pacing/render-loop/seek/subtitle/display/offscreen 测试和 Native Debug x64 build。

同一 24-case manifest 的正式 candidate 和三轮 repeat 均通过 `24/24` validation，分别位于 `docs\qa\private\candidates\playback-core-tuning-coalesced-audio-deadline-3419ea7-24case.local\` 与 `docs\qa\private\repeats\playback-core-tuning-coalesced-audio-deadline-3419ea7-native-repeat.local\`；comparison 位于 `docs\qa\private\comparisons\playback-core-tuning-coalesced-audio-deadline-3419ea7-vs-486c969.local\`。自动 suite 输出 `1 improved / 0 regressed / 0 mixed / 23 unchanged` 和 `accept-candidate`，唯一 improved case 是 `local/native-headless-hdr10-60`。该 case 没有音频时钟，候选分支不会执行，因而这项改善不能与代码改动建立因果关系，只能视为采样波动。

真正命中候选路径的 `local/native-headless-av-smoke` 被自动分类为 unchanged，但三轮 repeat 显示 mixed 结果：render P95 从 baseline 的 `37.2281-38.9675ms` 上升到 candidate 的 `39.7261-40.4073ms`，常态 cadence 持续变差；P99/max 则从 `38.4134-48.2608ms` 收窄到 `39.9364-40.6431ms`，candidate 的 22 个 group 也从 baseline 的 21 stable / 1 unstable 变为 22 stable。A/V drift P95/P99 均保持 `10ms`，audio-ahead final delta P95/P99 保持 `10ms`，video/audio starvation 保持 `0`；seek 误差仍为 `0ms`，音轨切换、暂停/播放中字幕切换及字幕关闭均完成，color/DXGI 证据保持一致。end-to-present 仍低于 `0.65ms`，没有把尾部转移到 render/Present 阶段。

人工因果审计结论为 `mixed / reject`：候选用更差且更偏离 `33.333ms` 目标的 P95 换取较窄的 P99 尾部，且自动 improved 信号来自未命中代码路径的 video-only case，不足以采纳。评测规则、manifest 和 expected 均未修改。后续不应基于该候选继续叠加调参；需要回到 `486c969` 行为，进一步解释为何去掉 post-present `5ms` 后 render pass/audio-ahead wait pass 明显增多，并寻找同时改善 P95 与 P99 的 deadline/clock scheduling 方案。

后续预筛又排除了两个方向。Kodi Win10/Xbox 共用 DXGI 路径会调用 `IDXGIDevice1::SetMaximumFrameLatency(1)`，但当前 offscreen composition device 在未改代码时已读回 `1`，且 headless `Present` P95 约 `0.06ms`、不承担真实 compositor VSync 阻塞，因此额外设置在当前软件闭环中没有行为差异。另一个未提交的 XAudio processing-pass 事件唤醒候选保留原 timer deadline，但允许音频处理量子提前唤醒；单次 A/V 预筛即把 audio wait pass 从 baseline 约 `112` 增到 `415`、passes-per-episode P50/P95 增到 `5/22`、render P95 变为 `40.1589ms`，因此未晋升为 24-case candidate，源码已撤回。

## 2026-07-11 更新：audio-ahead wait 返回到成功 present 的分段证据已闭环

提交 `486c969` 新增 `timing.audioAheadWaitEndToPresentSampleCount` 与 `timing.audioAheadWaitEndToPresentMsP50/P95/P99/Max`。采样从当前 generation 的 audio-ahead wait 返回开始，到下一次成功 `Render + Present` 完成为止；不包含 wait 本身，但包含 graph mutex 重新取得、decode/audio refill、clock 检查、字幕/渲染与 Present。字段已贯通 native、WinRT、Core report/analyzer/comparator、native-headless 严格 parser、repeat stability 和 App quality-run clone。旧报告缺字段时 repeat summary 保持 null，不会被补成 `0ms`。

同一 24-case manifest 的首轮 report-set 和三轮 repeat 均通过 24/24 validation，分别位于 `docs\qa\private\candidates\playback-core-tuning-post-audio-wait-evidence-486c969-24case.local\` 与 `docs\qa\private\repeats\playback-core-tuning-post-audio-wait-evidence-486c969-native-repeat.local\`。repeat 为 22 个 group 中 21 stable、1 unstable，唯一不稳定项仍是 `local/native-headless-av-smoke`。该 case 的 end-to-present P95 仅为 `0.4756-0.5202ms`，spread `0.0446ms`；P99/max 为 `0.5008-0.6418ms`，spread `0.141ms`。与此同时 render P99/max expected-error spread 仍为 `9.8474ms`，不稳定信号仍集中在 render interval 与 audio-ahead 后 render interval P99/max。

这说明当前尾部不在 wait 返回后的 mutex、decode refill、字幕、render 或 Present 阶段。A/V case 的 episode wait target P95 约 `30.3-31.3ms`，episode wait duration P95 约 `31.3-32.7ms`，而成功 present 后 render loop 还会先执行固定 `5ms` 默认等待，再为下一帧计算精确 audio-ahead wait。下一项小步结构性候选应合并这两个定时阶段：成功 A/V present 后立即检查下一帧并直接安排 audio-aware deadline，避免继续做 lead、线程优先级或音频游标外推常量试验。

与旧 `6b936f9` 报告的自动 comparison 为 0 improved、2 regressed、22 unchanged、24 partial confidence，并输出 `reject-candidate`。两个单次 regression 是 A/V smoke 与 HDR10-60 的 cadence 波动；新增字段全部为 candidate-only signal。由于 `486c969` 不改变播放策略，且三轮 repeat 仍显示同一个已知 A/V unstable group，该自动结果不能解释为行为回退或质量改善，只能说明旧报告不具备新字段且单次 timing 不适合采纳判断。完整 `run-playback-core-checks.ps1` 已通过，覆盖 456 个 Core tests、native-headless、评测脚本、原生 helper/offscreen tests 和 Native Debug x64 build。

## 2026-07-11 更新：render thread above-normal 候选不采纳

在字幕重同步 accepted baseline 之后，本轮针对 `local/native-headless-av-smoke` 的 audio-ahead 后 render P99/max 尾部尝试了线程级调度候选。Kodi 固定提交 `f0232910490189b97717bc5d309aec2e5751d6d3` 的 Windows 层支持 `THREAD_PRIORITY_ABOVE_NORMAL`，DXVA 路径还会临时提升进程优先级；本项目选择了更窄的 `RenderThreadPriorityScope`，仅在 `PlaybackGraph::RenderLoop` 生命周期内提升当前线程并在退出时恢复。候选提交为隔离分支上的 `4ecb74441baf67cdf61aa784fea4321b7e4766d8`，未合入 accepted 主线。

候选 report-set、三轮 repeat 和 comparison 分别位于 `docs\qa\private\candidates\playback-core-tuning-render-thread-priority-4ecb744-24case.local\`、`docs\qa\private\repeats\playback-core-tuning-render-thread-priority-4ecb744-native-repeat.local\` 和 `docs\qa\private\comparisons\playback-core-tuning-render-thread-priority-4ecb744-24case.local\`。每轮均通过 24/24 validation。自动 comparison 为 24 unchanged、0 improved、0 regressed、0 mixed，24 strong confidence、`no-change / low risk`。

repeat 证据不支持采纳：baseline 与 candidate 都是 21/22 stable，唯一 unstable group 都是 A/V smoke；candidate 的 render P99/max expected-error spread 为 `9.4009ms`，没有改善 baseline 的 `9.3927ms`，render P95 spread 反而从 `0.8821ms` 扩大到 `5.2124ms`，audio-ahead episode oversleep P99 spread 从 `0.5482ms` 扩大到 `3.8839ms`。A/V drift、final delta、seek、字幕和 starvation 没有退化，但目标 tail volatility 未改善且波动面扩大，因此拒绝候选。

正式候选前还筛掉了一个未提交的音频游标插值预实验：它把 audio-ahead pass P95 从约 13 次降到 2 次，但把原始 A/V drift 从 `10ms` 提高到 `16.67ms`，本质上形成额外提前呈现。该试验未进入 full manifest，不得当作可采纳候选。下一步应先补 wait 返回到实际 present 之间的分段延迟证据，区分 OS 恢复延迟、graph mutex/CPU 工作和 audio cursor 量化，而不是继续调整 lead、wait cap、游标外推或线程优先级。

## 2026-07-11 更新：字幕当前点重同步已用严格证据协议重新验证

本轮废弃此前基于 `27a7c1d` / `fcd194a` 生成的字幕重同步采纳证据，并在修正 seek 落点、native 指标必填校验、原始字段 presence、真实音轨选择观测和字幕切换事务恢复后重新生成报告。旧 report-set 仍可用于追溯，但不得再作为当前采纳依据。

新的 no-resync 控制组为提交 `32a974dbb081766f3807185b4b019de540f664d9`，报告位于 `docs\qa\private\baselines\playback-core-tuning-no-resync-control-32a974d-24case.local\`；当前候选为提交 `6b936f98e948d5a9bc055e955b4045d5e94880f3`，报告、三轮重复和 comparison 分别位于 `docs\qa\private\candidates\playback-core-tuning-trustworthy-subtitle-resync-6b936f9-24case.local\`、`docs\qa\private\repeats\playback-core-tuning-trustworthy-subtitle-resync-6b936f9-native-repeat.local\` 和 `docs\qa\private\comparisons\playback-core-tuning-trustworthy-subtitle-resync-6b936f9-24case.local\`。这些目录均为 ignored/private artifact，不进入仓库。

控制组、候选和三轮重复都通过 24/24 report-set validation；15 个 Core/Emby case 的 manifest 完全相同，9 个 native case 的 expected 完全相同，生成媒体的 SHA-256 逐项一致。native URI 仅因 worktree 绝对路径不同而不同。控制组结果为 20 pass、1 fail、2 expected error、1 unsupported；候选及三轮重复均为 21 pass、0 fail、2 expected error、1 unsupported，且没有 evidence-collection error。

目标 A/V case 中，控制组两次字幕切换均为 `failed`，cue 数保持 `0 -> 0`；候选和三轮重复的两次切换均为 `completed`，每次 cue 数都增长。四次候选运行的音轨切换、暂停状态下字幕切换、字幕关闭和非零 seek 均完成；seek 的实际位置来自 seek 后首个成功呈现帧，四次误差均为 `0ms`。因此当前点 demux 重定位可以限定采纳为嵌入字幕切换修复。

自动 comparison 为 24 unchanged、0 regressed、0 mixed，并输出 `review-unmatched-signals / medium risk`。原因是控制组的两个失败 `lifecycle.subtitle-switch` check 在候选 pass report 中消失，形成预期的 baseline-only unmatched signal；自动裁判没有把“失败检查消失”直接算作 improvement。人工审计只依据 raw lifecycle、case result、相同媒体哈希和三轮重复作限定采纳，不覆盖该自动结论，也不修改评测规则。

候选 repeat 的 22 个可聚合 group 中 21 stable、1 unstable。唯一 unstable group 仍是 `local/native-headless-av-smoke`：A/V drift P95/P99 与 audio-ahead final delta P95/P99 spread 均为 `0ms`，但 render P99/max expected-error spread 为 `9.3927ms`，不稳定信号集中在 audio-ahead wait 之后的 render interval P99/max。因此本轮不宣称 frame pacing、A/V sync、buffering、HDR 或设备输出已经解决；下一轮应针对该调度尾部设计独立的小步候选。

## 2026-07-10 更新：3ms audio render-start lead 候选已通过 24-case gate

本轮在 `render-after-wait` 诊断基线之后做了一个小步播放策略调整：audio-ahead gating 保留原有 `10ms` A/V 容忍区间，但额外给渲染启动预留 `3ms` lead。也就是说 native 不再等到视频帧只领先音频 `10ms` 才返回 render loop，而是在 `13ms` 边界开始让渲染链路工作；`audioAheadWaitTargetMs` 与实际 wait duration 现在使用同一个 `PlaybackFramePacing::AudioAheadWaitDuration` 计算，避免指标和策略不一致。此前尝试过的 `5ms` lead 候选为 `split-candidate / high risk`，没有保留。

候选 24-case report-set 已生成到 `docs\qa\private\candidates\playback-core-tuning-audio-render-lead-3ms-24case-working.local\`。带 repeat attribution 的 comparison 输出为 `docs\qa\private\comparisons\playback-core-tuning-audio-render-lead-3ms-24case-with-repeat.local\`，suite 结论为 `accept-candidate / keep-candidate`、risk `low`、24/24 strong confidence、2 improved、0 regressed、0 mixed、22 unchanged，manifest case IDs 完全一致。改进 case 为：`local/native-headless-av-smoke` 的 `framePacing.renderIntervalP95ExpectedErrorMs` 从 `6.239667ms` 降到 `4.027867ms`；`local/native-headless-hdr10-60` 的 `framePacing.maxFrameGapExpectedErrorMs` 从 `3.547333ms` 降到 `0.549033ms`。

已补 3 次 candidate repeat：`docs\qa\private\repeats\playback-core-tuning-audio-render-lead-3ms-native-repeat.local\`。旧基线 repeat 为 9 个 native group 中 7 stable、2 unstable，unstable 是 `local/native-headless-av-smoke` 与 `local/native-headless-hdr10-60`；candidate repeat 为 22 个 group 中 21 stable、1 unstable，只剩 `local/native-headless-av-smoke`。A/V smoke 的 P95 常态区间相对旧基线下降：render P95 expected-error 从旧基线约 `6.24-6.67ms` 降到 candidate 约 `3.57-5.07ms`；A/V drift P95/P99 spread 与 finalDeltaAbs P95/P99 spread 仍为 `0ms`。但该 case 仍不能宣称完全解决：candidate repeat 的 A/V P99/max frame gap spread 约 `5.6844ms`，且 unstable signals 包含 `framePacing.renderIntervalP99ExpectedErrorMs`、`framePacing.maxFrameGapExpectedErrorMs`、`timing.audioAheadWaitOversleepMsP95/P99` 与 `timing.renderIntervalAfterAudioAheadWaitMsP99/Max`。

验证：TDD 先让 `FramePacingTests.cpp` 暴露 3ms lead 边界，再实现通过；native-headless smoke 通过；3 次 repeat 每轮 24-case validation 均通过；带 repeat attribution 的 candidate comparison 通过；完整 `tools\quality-run\run-playback-core-checks.ps1` 已通过，覆盖 438 个 Core tests、CLI smoke、native-headless smoke、manifest/report/comparison 脚本测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。下一步不应继续盲调 1-5ms 常量，而应处理 A/V smoke 残留的 P99/max tail 与 audio-ahead 后 render interval 波动。

## 2026-07-10 更新：render interval 已按 preceding wait reason 分桶

本轮在当前 `34a625b` accepted baseline 之后只补诊断证据，不改变播放策略、不放宽评测规则。新增 `timing.renderIntervalAfterAudioAheadWaitSampleCount`、`timing.renderIntervalAfterAudioAheadWaitMsP95/P99/Max`、`timing.renderIntervalAfterNonAudioWaitSampleCount`、`timing.renderIntervalAfterNonAudioWaitMsP95/P99/Max`，用于把每个 render interval 归因到它之前完成的 render-loop wait reason。字段已贯通 native metrics、WinRT metrics、native-headless stdout、Core report、analyzer evidenceSignals、signal catalog、required signal policy、comparison matchedSignals、app quality-run clone 和 cadence stability summary。

关键结论：`local/native-headless-av-smoke` 的长 render interval 后验上几乎完全绑定到 preceding audio-ahead wait。candidate 单次 24-case report-set `docs\qa\private\candidates\playback-core-tuning-render-after-wait-evidence-working-24case.local\` validation 通过，24 reports，native-headless included；与 accepted baseline `docs\qa\private\baselines\playback-core-tuning-video-clock-wait-cap-34a625b-24case.local\` 对比输出到 `docs\qa\private\comparisons\playback-core-tuning-render-after-wait-evidence-vs-34a625b.local\`，结果为 `no-change`，24/24 same case，0 regression。A/V case 中 audio-ahead 分桶约 `45-46` 个样本，P95 约 `39.6-40.0ms`，P99/max 约 `39.7-40.2ms`；non-audio 分桶三次 repeat 均为 `0` 个样本。

已生成 candidate native repeat：`docs\qa\private\repeats\playback-core-tuning-render-after-wait-evidence-working-native-repeat.local\`。repeat summary 显示 9 个 native group 中 7 个 stable、2 个 unstable，unstable 仍为 `local/native-headless-av-smoke` 与 `local/native-headless-hdr10-60`。A/V group 的新分桶证据本身稳定：audio-ahead bucket P95 spread `0.4302ms`，P99 spread `0.503ms`，sample count spread `1`，non-audio bucket sample count spread `0`。该 group 被判 unstable 的原因仍是旧的 episode-level `timing.audioAheadWaitOversleepMsP95/P99` 波动，不是新分桶字段。

验证：新增字段的 Core mapper/analyzer/comparator/bridge targeted tests 通过，native `PlaybackQualityMetricsTests.cpp` 通过，`NativePlaybackGraphDecouplingContractTests` 通过，`run-native-headless-harness-smoke-test.ps1` 通过，`Measure-PlaybackCadenceStability.tests.ps1` 通过。下一步可以基于该证据设计真正的 audio-ahead wait scheduling 候选，但不能把本轮记录为播放质量优化。

## 2026-07-10 更新：main 合并后的 video-clock wait cap 候选已通过 24-case gate

本轮重新核对当前 worktree：`main` 已经是当前分支祖先，没有未解决 merge conflict；合并 main 后的品牌、构建链和 FFmpeg 依赖更新已经包含在当前分支口径内。被打断前尝试过的 audio-ahead wait cap 已撤回，当前保留的行为改动只限于无音轨 software video-clock wait：`PlaybackFramePacing::VideoClockWaitDuration` 最长等待限制为 `10ms`，audio-ahead wait 仍使用原策略。

已用当前代码重新生成 24-case candidate：`docs\qa\private\candidates\playback-core-tuning-video-clock-wait-cap-after-main-merge-24case.local\`。report-set validation 通过，24/24 report matched，native-headless included。与 `907e8d0` baseline `docs\qa\private\baselines\playback-core-tuning-short-interval-907e8d0-24case.local\` 对比输出到 `docs\qa\private\comparisons\playback-core-tuning-video-clock-wait-cap-after-main-merge-24case-with-repeat.local\`，suite 结论为 `accept-candidate / keep-candidate`、risk `low`、1 improved、0 regressed、0 mixed、23 unchanged。

唯一 improved case 是 `local/native-headless-hdr10-60`：`framePacing.renderIntervalP99ExpectedErrorMs` 从 `3.456633ms` 降到 `0.443133ms`，`framePacing.maxFrameGapExpectedErrorMs` 从 `4.507933ms` 降到 `0.495233ms`。这与限制单次 video-clock wait、减少 60fps video-only cadence 尾部 overshoot 的预期一致。

仍需保留的风险：native repeat summary `docs\qa\private\repeats\playback-core-tuning-video-clock-wait-cap-native-repeat.local\summaries\cadence-stability-summary.local.json` 显示 9 个 native group 中 7 个 stable、2 个 unstable，unstable group 仍为 `local/native-headless-av-smoke` 与 `local/native-headless-hdr10-60`。因此本轮可以说 video-clock wait cap 候选按当前 24-case gate 可采纳，但不能说 native cadence 稳定性已经完全解决。最新 smoke 抽样中 `local/native-headless-av-smoke` 的 render P95/P99 仍约 `39.6368/39.804ms`，目标帧长为 `33.333ms`；后续仍应围绕 A/V smoke 的 wait scheduling、oversleep 和 render catch-up 继续诊断。

验证：原生 `FramePacingTests.cpp` 通过，`run-native-headless-harness-smoke-test.ps1` 通过，`git diff --check` 通过；完整 `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.ps1` 也已通过，覆盖 Core 436 个测试、CLI smoke、native-headless smoke、manifest/report/comparison 脚本测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。

## 2026-07-10 更新：audio-ahead wait episode/pass 诊断证据已接入

本轮确认当前调优分支已经包含 `main`，无需再次 merge；主线构建链、依赖库和 FFmpeg 升级内容已在当前分支口径内。随后继续基于 24-case manifest 做播放 Core 诊断能力增强，新增 `timing.audioAheadWaitEpisodeCount`、`timing.audioAheadWaitPassesPerEpisodeP50/P95/P99/Max`。这些字段从 native `PlaybackGraph` 的 audio-ahead wait episode 采集，并贯通 native metrics、native-headless stdout、Core report、analyzer evidence signals、signal catalog、candidate comparison matched signals、WinRT bridge 和 app quality-run clone。

该改动不改变播放策略、不修改 wait 时长、不放宽任何 stable/challenge 规则。目标是让模型区分“wait pass 数增加但 episode 数稳定”与“真正出现更多 audio-ahead 等待 episode”。这对解释此前被拒绝的 half-frame wait cap 很关键：那个候选把单次等待拆得更碎，但没有降低 A/V smoke 的 P99/max instability。

验证：完整 `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.ps1` 已通过，覆盖 435 个 Core tests、CLI smoke、native-headless smoke、manifest/report/comparison 脚本测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。生成 24-case candidate `docs\qa\private\candidates\playback-core-tuning-audio-wait-episode-evidence-24case-working.local\`，validation 通过，24 个 report，native-headless included。

与 `907e8d0` baseline 的 comparison 输出为 `docs\qa\private\comparisons\playback-core-tuning-audio-wait-episode-evidence-24case-with-repeat.local\`，suite 仍为 `reject-candidate`：`local/native-headless-av-smoke` 单次 frame-pacing 回退，`local/native-headless-hdr10-60` 单次改善。因为本轮没有行为改动，该结果不能作为性能优化结论。已补 3 次 native repeat：`docs\qa\private\repeats\playback-core-tuning-audio-wait-episode-evidence-working-native-repeat.local\`，27 个 report 中 8/9 native group stable，唯一 unstable 仍是 `local/native-headless-av-smoke`。

repeat attribution 显示：baseline 的 target unstable group 是 `local/native-headless-av-smoke` 与 `local/native-headless-hdr10-60`，candidate 中只剩 `local/native-headless-av-smoke` unstable。A/V smoke 的 finalDeltaAbs P95/P99 spread 与 A/V drift P95/P99 spread 仍为 `0ms`，但 render P99/max spread 约 `3.0481ms`，audioAheadWaitOversleep P95/P99 spread 约 `6.7386/6.7004ms`。当前结论：这次是可保留的诊断字段增强，不是可采纳的播放质量优化；下一步应围绕 A/V smoke 的 wait episode/pass 分布、oversleep 与 render tail 关系设计更细的 per-wait evidence 或结构性 scheduling 候选。

## 2026-07-10 更新：render short-interval / catch-up 证据已补齐

本轮在合并 main 后的当前调优分支上继续工作，补齐了 render interval 的短间隔侧证据：`timing.renderIntervalMsP05`、`timing.minFrameGapMs`、`timing.renderIntervalUnderExpected2MsCount` 和 `timing.renderIntervalUnderExpected4MsCount`。这些字段从 native render interval histogram 在 `Snapshot()` 阶段派生，并贯通 native-headless stdout、Core report、analyzer evidence signals、candidate comparison matched signals、cadence stability summary、WinRT bridge 和 app quality-run clone。

该改动不改变播放 core/native 调度行为，也不新增 pass/fail 阈值。它的作用是让模型判断 P99/max frame gap 尾部是否伴随短间隔补偿：如果长间隔后存在 P05/min 明显低于 expected frame duration，问题可能是 catch-up / pacing compensation；如果只有长间隔而没有短间隔补偿，则更像真实欠帧或持续卡顿。

当前 targeted 验证已经覆盖 native histogram、Core mapper/analyzer/comparator、WinRT bridge contract、cadence stability 工具和 native-headless smoke；完整 `tools\quality-run\run-playback-core-checks.ps1` 也已通过，覆盖 435 个 Core tests、CLI smoke、native-headless smoke、manifest/report/comparison 脚本测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。单次 native-headless smoke 抽样中，`local/native-headless-av-smoke` 的 30fps render P05/min 约为 `26.06/25.91ms`，P99/max 约为 `43.08/43.08ms`，under-expected 计数存在，说明尾部长间隔同时伴随短间隔补偿；`local/native-headless-hdr10-60` 的 P05/min 约为 `15.19/14.73ms`，max 约为 `21.22ms`，短帧补偿较轻。

同时修正了 `Measure-PlaybackCadenceStability.ps1` 的旧报告兼容性：旧 report 缺少 P05/min 时，short-interval spread 必须保持 `null`，不能被 PowerShell 数组转换误当成 `0ms`。后续调优应先用当前提交重新跑 native repeat，再根据同一 manifest 的 baseline/candidate comparison 判断是否需要调整 render scheduling 或 audio-clock gating。

提交 `907e8d0` 后，已生成当前 24-case baseline：`docs\qa\private\baselines\playback-core-tuning-short-interval-907e8d0-24case.local\`，validation 通过，24 个 report，native-headless included，analysis 为 `no-change / low risk`，playback evidence 为 `partial` 且可评估 native software playback。随后完成 3 次 native repeat：`docs\qa\private\repeats\playback-core-tuning-short-interval-907e8d0-native-repeat.local\`。repeat summary 显示 9 个 native group 中 6 个 stable、3 个 unstable：`local/native-headless-av-smoke`、`local/native-headless-hdr10-60`、`local/native-headless-sdr-60`。

`local/native-headless-av-smoke` 的 A/V drift P95/P99 spread 与 finalDeltaAbs P95/P99 spread 均为 `0ms`，但 render P95/P99/max spread 分别约 `3.3244/4.04/4.04ms`，audioAheadWaitOversleep P95/P99 spread 约 `6.316/4.1725ms`，同时 P05/min 短间隔 spread 也明显存在。当前解释是：A/V 最终对齐稳定，但 wait scheduling 与 render catch-up pattern 不稳定；下一步应针对这类结构性证据设计 candidate，而不是再做单纯 audio wait 常量调参。`hdr10-60` 与 `sdr-60` 的 P95 基本稳定，主要是 P99/max 或单个 max-frame-gap outlier；它们更适合作为 render-loop/outlier 统计问题处理。

基于该证据尝试过一个未采纳候选：按源帧率把 audio-ahead 单次 wait 限制为最多半帧，以减少一次长 sleep 的 timer oversleep。TDD 先给 `PlaybackFramePacing::AudioAheadWaitDuration(..., videoFrameRate)` 增加红灯测试，实现后 targeted native frame pacing test 与 native-headless smoke 通过；随后生成 24-case candidate `docs\qa\private\candidates\playback-core-tuning-audio-half-frame-wait-24case-working.local\`，并与 `907e8d0` baseline 对比。comparison 为 `split-candidate / high risk`：A/V smoke 的 P95 expected-error 改善约 `-2.9274ms`，但 P99/max expected-error 回归约 `+2.2549ms`；HDR10-60 单次改善。进一步 3 次 candidate repeat `docs\qa\private\repeats\playback-core-tuning-audio-half-frame-wait-working-native-repeat.local\` 显示目标 A/V smoke 仍 unstable，且 P99/max spread 从 baseline 约 `4.04ms` 扩大到约 `6.7486ms`，audioAheadWaitOversleep P99 spread 也从约 `4.1725ms` 扩大到约 `7.0819ms`。源码已回退；不要把半帧 audio wait cap 当作 accepted 优化。

## 2026-07-10 更新：native A/V smoke 最低渲染帧阈值已收紧

本轮继续基于已合入 main 的当前调优分支工作。排查 `local/native-headless-av-smoke` 时确认：生成样本因字幕 `-shortest` 实际为 `2.5s / 75` 帧，但 native helper 的主播放快照是在 3 秒窗口的一半处采集，因此 report 中约 `46` 个 rendered frames 对 30fps 的 `1.5s` 捕获窗口是合理结果，不是“2.5 秒只渲染 46 帧”的播放 core 欠帧。

同时发现该 A/V case 的 manifest 仍只要求 `minRenderedVideoFrames = 1`，作为稳定裁判过弱。已把 A/V smoke 的最低渲染帧期望提高到 `40`，并在 smoke 脚本中断言 materialized report 必须保留该阈值。TDD 验证先在旧阈值下红灯失败，改为 `40` 后同一 `run-native-headless-harness-smoke-test.ps1` 绿灯通过；当前样本实际 `renderedVideoFrames = 46`、`decodedVideoFrames = 47`、`observedSampleDurationMs = 1533.33`、A/V sync 为 `synced`。

当前结论：本轮没有改变播放 core 行为，只修正 native A/V smoke 的评测强度。后续候选如果导致该 case 严重欠帧，将不再因为 `minRenderedVideoFrames = 1` 而被误判为可接受。

## 2026-07-10 更新：audio-ahead final delta 证据已补齐，当前 A/V instability 不是最终对齐漂移

本轮在当前 main 口径上确认 `main` 已合入当前调优分支，并提交 `2998f61 tools: expose audio wait final delta evidence`。新增 `timing.audioAheadWaitFinalDeltaAbsMsP50/P95/P99/Max`，从 native `PlaybackGraph` 的 audio-ahead wait episode 结束点采集残余 A/V delta 绝对值，并贯通 native-headless stdout、Core report、analyzer evidence signals、signal catalog、comparison matched signals、WinRT quality metrics bridge 和 app quality-run clone。

完整 `tools\quality-run\run-playback-core-checks.ps1` 已通过，覆盖 Core 434 个播放相关测试、CLI smoke、native-headless smoke、manifest/report/comparison 脚本测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。native-headless A/V smoke 的新字段已实际产出：单次样本中 finalDeltaAbs P95/P99/Max 均约 `10ms`。

随后对 `2998f61` 跑了 3 次 native-headless repeat，归档到 `docs\qa\private\repeats\playback-core-tuning-final-delta-2998f61-native-repeat.local\`。更新后的 `Measure-PlaybackCadenceStability.ps1` 现在会聚合 `timing.audioAheadWaitFinalDeltaAbsMsP95/P99` spread，并让 `Compare-PlaybackCoreTuningCandidate.ps1` 透传这些字段。repeat 结果显示 `local/native-headless-av-smoke` 仍为 unstable，但 finalDeltaAbs P95/P99 spread 为 `0ms`，A/V drift P95/P99 spread 也为 `0ms`；不稳定信号来自 frame P99/max gap spread 与 `timing.audioAheadWaitOversleepMsP95/P99` spread。

当前结论：A/V smoke 的当前问题不应优先解释为“等待结束后最终 A/V 对齐漂移”，更像 wait scheduling / render interval tail / audio clock sampling 粒度导致的尾部稳定性问题。下一步继续调优时，应基于同一 repeat/comparison 流程尝试结构性 scheduling 方案，避免继续做 5ms cap、1-2ms early-wake、20ms tolerance 这类已拒绝的简单参数调参。

基于该证据，`PlaybackQualityRunComparator` 已调整 audio-ahead oversleep 的判定语义：在 baseline/candidate 都带有 finalDeltaAbs 证据时，`timing.audioAheadWaitOversleepMsP95/P99` 只有在 finalDeltaAbs 同方向发生显著变化时才进入 improvement/regression；如果 finalDeltaAbs 稳定，oversleep 只作为 matched evidence 保留。这避免模型因为 timer/clock sampling 尾部波动而误采纳或误拒绝 Core 候选。

## 2026-07-10 更新：当前 main 口径已迁移到 24-case，audio-ahead early-wake 候选不采纳

本轮确认 `main` 已经是当前分支祖先，`git merge --ff-only main` 结果为 already up to date。由于主线样本/私有 manifest 已调整，当前可复现口径从旧 54-case 变为 24-case：15 个 core/private case 加 9 个 native-headless case。已基于当前 accepted HEAD `b6307e2` 生成同口径 baseline：`docs\qa\private\candidates\playback-core-tuning-b6307e2-24case.local\`，validation 通过，24/24 report matched，native-headless included。

尝试过三个未采纳的小步 audio-ahead wait 候选，源码均已撤回。5ms cap 候选会把 `local/native-headless-av-smoke` 的 `audioAheadWaitCount` 从 `53` 增到 `243`，并让 render P99/max gap 回归，直接 `reject-candidate`。2ms early-wake 候选结论为 `split-candidate`：`local/native-headless-hdr10-60` 单次 improved，但该 case 没有 audio wait，因此改善不能归因于候选；目标 A/V smoke 则从 frame tail instability 转为 `timing.audioAheadWaitOversleepMsP95/P99` instability。1ms early-wake 候选仍为 `split-candidate`，A/V smoke 的 P95 render 改善被 P99/max gap 和 audio oversleep P95/P99 回归抵消。

为排除单次 native-headless 抖动，已对 `b6307e2` baseline 和 2ms early-wake candidate 各跑 3 次 native repeat。baseline repeat 归档到 `docs\qa\private\repeats\playback-core-tuning-b6307e2-native-repeat.local\`，A/V smoke 与 HDR10-60 都是 unstable：A/V 主要是 frame P99/max spread，HDR10-60 是 max-frame-gap spread。2ms early-wake repeat 归档到 `docs\qa\private\repeats\playback-core-tuning-audio-ahead-early-wake-native-repeat.local\`，HDR10-60 变为 stable，但 A/V smoke 仍 unstable，且 unstable signals 变成 frame P95 加 audio oversleep P95/P99。带 stability attribution 的 comparison 仍是 `split-candidate`，因此不能采纳该策略。

当前结论：不要继续沿 5ms cap、1-2ms early-wake 或简单 audio wait 参数调参。下一步如果继续处理 A/V smoke，应先补更细的 per-wait evidence，例如每次 wait 的 initial gap、planned wait、actual wait、final audio delta、是否跨过 render interval tail，而不是继续只看 episode-level P95/P99；或者转向 render scheduling 结构问题，研究为什么 2.5s/30fps A/V 样本只稳定渲染约 46 帧。

## 2026-07-10 更新：render outlier 证据已补齐，但本轮不是可采纳的播放质量优化

本轮在 `c129249` accepted render-loop timer 基线之后补齐了 native render interval outlier 证据：report 现在会输出 `timing.renderIntervalSampleCount`、`timing.renderIntervalOverExpected2MsCount` 和 `timing.renderIntervalOverExpected4MsCount`，用于区分“P95/P99 常态 cadence”与“少量 max-frame-gap 尾部尖峰”。第一次实现曾在 `RecordRenderIntervalMs` 热路径内计数；随后已修正为只在 `Snapshot()` 阶段按 histogram 和 source frame rate 计算，避免每帧记录时做额外阈值判断。

提交链路：`868d2ba tools: expose render interval outlier evidence` 增加字段透传、report/schema/analyzer/comparison matched signals 和 App WinRT bridge；`0c40e63 tools: compute render outlier evidence off hot path` 把 outlier 计算移出采样热路径。完整 `tools\quality-run\run-playback-core-checks.ps1` 已通过，覆盖 Core 434 个播放相关测试、CLI smoke、native-headless smoke、manifest/report/comparison 脚本测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。

已基于 `0c40e63` 生成 54-case candidate：`docs\qa\private\candidates\playback-core-tuning-render-outlier-evidence-54case-0c40e63.local\`，validation 通过，54/54 report matched，native-headless included。与 `c129249` accepted baseline 的 comparison 输出为 `docs\qa\private\comparisons\playback-core-tuning-render-outlier-evidence-54case-0c40e63.local\`，结论为 `reject-candidate`、0 improved、1 regressed、53 unchanged，唯一目标 case 是 `local/native-headless-av-smoke`。阻断信号是 `timing.audioAheadWaitOversleepMsP95/P99`，从约 `3.9499/4.1270ms` 增至 `7.1612/7.4722ms`。

为避免把单次 native-headless 抖动误判为确定 Core 行为，已对 `0c40e63` 跑 3 次 native repeat 并归档到 `docs\qa\private\repeats\playback-core-tuning-render-outlier-evidence-0c40e63-native-repeat.local\`。随后扩展 `Measure-PlaybackCadenceStability.ps1`，让 repeat summary 除 frame expected-error 外，也输出 `timing.audioAheadWaitOversleepMsP95/P99` 和 `sync.audioVideoDriftMsP95/P99` 的 spread。按新口径，`c129249` baseline 与 `0c40e63` candidate 的 A/V smoke 都是 unstable，但性质不同：baseline 主要是 `audioAheadWaitOversleepMsP99` 尾部波动，P95 oversleep spread 仅约 `0.4578ms`，frame P99/max spread 约 `0.1408ms`；candidate 变成 `audioAheadWaitOversleepMsP95/P99` 与 frame P99/max 同时 unstable，P95 oversleep spread 约 `6.8674ms`，frame P99/max spread 约 `10.5918ms`。

`Compare-PlaybackCoreTuningCandidate.ps1` 现在会在 `cadenceStability.attribution` 中输出 target case 与 baseline/candidate repeat stability 的交集，并保留 A/V oversleep / drift spread 字段。本轮 artifact 明确显示 target case `local/native-headless-av-smoke` 在 baseline 和 candidate repeat 中都 unstable，但 candidate 的 unstable signals 从单一 P99 oversleep 扩大为 frame P99/max 加 oversleep P95/P99。该归因不改变 suite gate，也不放宽阈值；它让模型能区分“既有尾部波动”和“候选把波动扩大到更核心信号”。

当前结论：render outlier 证据和 stability attribution 可以作为诊断/评测能力保留，但 `0c40e63` 不应标记为播放质量优化成功。下一步如果继续调 Core，应优先处理 A/V smoke 的 wait scheduling / audio-clock gating 尾部稳定性，或者先建立更长/重复 A/V 样本的基线；不要因为这轮只加了 evidence 字段，就忽略 comparison 的 reject 结论。

## 2026-07-10 更新：当前 accepted 行为的 native cadence 重复采样已补充

在回退 20ms audio-ahead tolerance 后，已对当前 accepted HEAD `f96aaa8` 运行 3 次 native-headless smoke 并归档到 `docs\qa\private\repeats\playback-core-tuning-current-f96aaa8-native-repeat.local\`。共 27 个 report，`cadence-stability-summary.local.json` 显示 9 个 native case group 中 8 个 stable、1 个 unstable，minimum samples 为 3，materiality 为 `2ms`。

稳定组包括 `local/native-headless-av-smoke`、SDR 23.976/24/60/smoke 和 HDR10 23.976/24/30。A/V smoke 的 expected-error spread 为 P95 `1.1565ms`、P99 `0.4749ms`，说明当前 accepted 行为下该 3 秒 A/V smoke 在本轮采样中没有跨过 `2ms` materiality；这进一步支持不要继续沿 audio-ahead tolerance 方向调参。

唯一 unstable group 是 `local/native-headless-hdr10-60`，P95 expected-error spread 为 `2.8259ms`，P99 spread `0.9622ms`。该样本无音轨、`audioAheadWaitCount = 0`，因此问题不应归到 A/V sync 或 audio-ahead gating。下一步 frame pacing 调优应优先面向 60fps/HDR10 native-headless cadence 的 render scheduling / display cadence 证据，而不是继续改含音轨 A/V 策略。

## 2026-07-10 更新：20ms audio-ahead tolerance 候选不采纳

本轮在 `d687248` evidence baseline 之后尝试了一个小步 native 策略候选：只把含音轨路径的 audio-ahead tolerance 从 10ms 放宽到 20ms，video-only software clock tolerance 保持 10ms。TDD 先更新 `FramePacingTests.cpp`，确认现有 10ms 策略红灯；实现后 targeted native frame pacing test、native-headless smoke 和完整 `run-playback-core-checks.ps1` 均通过。

working 54-case comparison 曾显示 `keep-candidate / accept-candidate`，但 commit-bound candidate `docs\qa\private\candidates\playback-core-tuning-audio-ahead-tolerance-54case-e8cef30.local\` 与 `playback-core-tuning-decode-mode-evidence-54case-d687248.local` 对比后被拒绝：`docs\qa\private\comparisons\playback-core-tuning-audio-ahead-tolerance-54case-e8cef30.local\` 结论为 `reject-candidate`、risk `high`、54/54 可比、0 improved、2 regressed、0 mixed、52 unchanged，blocker 为 `suite.regression`，目标区域 `frame-pacing`。

两个回归 case 是 `local/native-headless-av-smoke` 和 `local/native-headless-hdr10-60`。A/V case 从 baseline P95/P99/max `40.433/46.2755/46.2755ms` 变为 candidate `45.3174/52.3268/52.3268ms`，`audioAheadWaitCount` 从 `67` 增至 `75`，`audioVideoDriftMsP95` 从 `10ms` 增至 `20ms`。HDR10-60 无音轨 case 不应受该策略直接影响，回归更像已知 60fps native-headless 短样本波动，但 A/V case 本身也回归，因此不能采纳。

已通过 `12bea45 Revert "chore: loosen audio ahead pacing tolerance"` 回退该策略。当前 Core/native 行为不包含 20ms audio-ahead tolerance。后续不要继续通过简单放宽 audio-ahead tolerance 解决 A/V smoke jitter；下一步更适合做 repeated/longer A/V sampling 或补充 per-wait final clock delta 证据，再寻找更稳定的 scheduling 策略。

## 2026-07-09 更新：native decode-mode evidence 已进入 report

本轮在合并后的 `905241d` 工程基线上补齐了一项诊断证据：native playback metrics 现在会拆分记录 `hardwareDecodedVideoFrames` 与 `softwareDecodedVideoFrames`，并通过 native-headless helper、Core report mapper、report analyzer、signal catalog、candidate comparison matched signals 和 App WinRT quality metrics bridge 暴露给评测系统。同时补齐 App WinRT bridge 中此前未暴露的 `audioAheadWaitCount` 与 `videoClockWaitCount`，让 App-hosted quality-run 与 native-headless 在 wait reason evidence 上保持一致。

这次改动不改变解码、渲染、A/V sync、wait scheduling、HDR/color 或样本预期，只解决“模型无法判断当前 native report 是硬解路径还是软解路径”的证据缺口。TDD 先覆盖 Core report mapper、native metrics snapshot、WinRT bridge contract、signal catalog/analyzer 和 native-headless smoke 输出，再实现字段透传。

验证结果：`dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter "FullyQualifiedName~PlaybackQualityReportMapperTests|FullyQualifiedName~NativeQualityMetricsBridgeContractTests|FullyQualifiedName~PlaybackQualityReferenceManifestTests|FullyQualifiedName~PlaybackQualityReportAnalyzerTests"` 通过 125/125；native `PlaybackQualityMetricsTests.cpp` 通过；`run-native-headless-harness-smoke-test.ps1` 通过；完整 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1` 通过，覆盖 Core 434 个播放相关测试、CLI smoke、native-headless smoke、manifest/report-set 脚本测试、cadence stability 工具测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。

本地 headless smoke 抽样显示当前两个 smoke case 都走硬解路径：`local/native-headless-sdr-smoke` 与 `local/native-headless-av-smoke` 均为 `decodedVideoFrames = 47`、`hardwareDecodedVideoFrames = 47`、`softwareDecodedVideoFrames = 0`。因此合并后观察到的 A/V smoke 尾部 frame gap 问题，不应优先归因为软解 fallback；下一步调优更应继续集中在 clock/render scheduling、display cadence 和采样稳定性。

已基于提交 `d687248` 生成同 54-case manifest 的 candidate：`docs\qa\private\candidates\playback-core-tuning-decode-mode-evidence-54case-d687248.local\`，validation 通过，54/54 report matched，native-headless included。与新工程基线 `playback-core-tuning-main-modern-54case-905241d.local` 的 comparison 输出在 `docs\qa\private\comparisons\playback-core-tuning-decode-mode-evidence-54case-d687248.local\`，结论为 `keep-candidate / accept-candidate`、risk `low`、54/54 可比、2 improved、0 regressed、0 mixed、52 unchanged。当前 evidence hook 可作为后续调优基线的一部分保留。

## 2026-07-09 更新：已合并 main 的 VS2026 / FFmpeg 更新，调优基线迁移到 905241d

本轮已将 `main` 的构建链和依赖更新合入当前播放 Core 调优分支，merge commit 为 `67fc96e`。合入内容包括 VS2026 / MSBuild 18、.NET 10 现代 UWP、native `v145` toolset、Windows SDK `10.0.26100.0`、C++20、`Microsoft.Windows.CppWinRT 3.0.260520.1`，以及当前 `FFmpegInteropX.UWP.FFmpeg.8.1.2` 依赖链路。

合并后发现 `tools\quality-run\run-playback-core-checks.ps1` 的 native restore/build 仍硬编码 VS2022 `vcvars64.bat` 和旧 `msbuild`，在 `v145` toolset 下失败。已提交 `905241d tools: use modern msbuild for native core gate`，让 native restore/build 通过 `tools\NoiraModernToolchain.ps1` 解析现代 MSBuild。完整门禁 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1` 已通过，覆盖 Core tests、CLI smoke、native-headless smoke、manifest/report-set 脚本测试、cadence stability 工具测试、native helper/frame pacing/render loop/display refresh/offscreen tests 和 native Debug x64 build。

已基于合并后的 `905241d` 生成新的 54-case report-set：`docs\qa\private\candidates\playback-core-tuning-main-modern-54case-905241d.local\`。report-set validation 通过，54/54 report matched，native-headless included。与合并前 54-case baseline `playback-core-tuning-round2-baseline-54case-a0e72c6.local` 对比时，suite 结论为 `reject-candidate`：4 improved、2 regressed、48 unchanged，blocker 为 `suite.regression`，目标区域为 `frame-pacing`。两个回归 case 是 `local/native-headless-av-smoke` 和 `local/native-headless-hdr10-60`。

重复采样显示这不是某个新调优补丁的结论，而是 main/工具链/FFmpeg 合并后的基线迁移风险。旧 baseline 的 3 次重复采样中 `local/native-headless-av-smoke` 和 `local/native-headless-hdr10-60` 为 stable；新 `905241d` 的 3 次 native-headless 重复采样中，这两个 group 均为 unstable：`local/native-headless-av-smoke` 的 P95/P99/max expected-error spread 为 `2.2868ms/3.8023ms/3.8023ms`，`local/native-headless-hdr10-60` 的 P99/max spread 为 `2.3287ms`。单次 comparison 中 A/V smoke 的 P99/max `52.7209ms` 未在 3 次重复采样中复现，但新基线的尾部 cadence 稳定性确实弱于旧基线。

当前结论：`905241d` 是后续继续调优必须面对的新工程基线，但不能把它解释为一次已接受的播放质量优化。后续 candidate 应以 `playback-core-tuning-main-modern-54case-905241d.local` 作为新 baseline 做同 manifest 对比；旧的 pre-merge baseline 只用于迁移诊断，不再作为后续候选是否可采纳的主要参照。下一步应优先定位合并后 native-headless A/V smoke 与 HDR10-60 cadence 不稳定的来源，再做小步 Core/native 调整。

## 2026-07-09 更新：VS2026 / .NET 10 / Native AOT 已成为本地主构建链路

当前仓库主入口已切到 VS2026 / MSBuild 18 / .NET 10 现代 UWP 路径：`NoiraPlayer.sln` 包含 `NoiraPlayer.App.Modern.csproj`、Core、Native、Core tests、playback-quality CLI/headless 工具；旧 solution、旧 UAP app project 和旧 loose deploy helper 已从 active tree 移除。Core、测试和 playback-quality 工具均直接 target `net10.0`，UWP app target 为 `net10.0-windows10.0.26100.0`，`Package.appxmanifest` 保持 `Windows.Universal`、`MinVersion=10.0.19041.0`、`MaxVersionTested=10.0.26100.0`。

Native C++/WinRT 项目已切到 VS2026 C++ toolset `v145`、Windows SDK `10.0.26100.0`、C++20，并显式关闭 legacy C++/WinRT coroutine `/await` 路径和 C++/CX `/ZW`。`Microsoft.Windows.CppWinRT` 已升级到 `3.0.260520.1`，native 编译选项包含 `/utf-8` 以避免 VS2026 生成头在本地 code page 下产生 C4819 警告。`FFmpegInteropX.UWP.FFmpeg` 已确认当前为 `8.1.2`，仍通过 UWP native 依赖链路进入 AppContainer/MSIX 布局。

当前本地验证命令以 `tools\Build-Noira.ps1` 为准：Debug build、Debug/Release page gate、strict playback-quality 和现代 cutover gate 均已通过。最新完整 `CutoverCheck` 报告为 `docs\qa\private\modern-final-local-cutover-check-rerun.local.json`，结果记录 Debug/Release Home 均达到 `semanticEvidenceStatus=ready`、`renderStage=supplemental`、`libraryCount=21`、`rowCount=16`，strict playback-quality 为 `pass`，`sourceStatus=matched`，`runtimeMetricsStatus=captured`，`hasPlaybackSample=true`，`startupDurationMs=2520.2707`，`playbackAttemptCount=1`。

收口过程中曾观察到 public direct-uri smoke 的 `startup.startupDurationMs` 波动：一次 captured report 为 `9283.799ms > 5000ms`，native diagnostics 显示主要耗时来自远端 FFmpeg open/demux（例如 `avformat_open_input=6138ms`、`avformat_find_stream_info=2404ms`）。随后 direct strict `Test-NoiraModernPlaybackQuality.ps1 -SkipBuild` 复跑通过，`startupDurationMs=2877.0173ms`；最新完整 `CutoverCheck` 也已通过，`startupDurationMs=2520.2707ms`。因此该风险被记录为远端 direct-uri startup gate 波动，不是 VS2026/.NET/Native AOT 构建、注册、启动或页面进入失败。

边界：Xbox 真机部署、启动、登录和播放验证仍是下一阶段事项；当前阶段只证明本机 desktop UWP/MSIX/AppContainer 路径、真实登录后 Home 页面、app-hosted playback-quality 和现代工具链闭环。已知 4K HEVC D3D11VA double-EAGAIN decoder 策略问题归播放优化 worktree，不作为 .NET/VS2026 迁移 blocker。

## 2026-07-09 更新：第二轮 54-case baseline 已重建，precise audio tail 候选拒绝

当前新 worktree 和 sibling worktree 中没有保留旧 41-case ignored artifact；为了避免依赖缺失本地产物，本轮用当前 main/Noira/FFmpeg 8.1.2 状态下已存在的 `docs/qa/private/combined-core-reference-manifest.local.json` 重建 54-case 调优基线：`docs/qa/private/candidates/playback-core-tuning-round2-baseline-54case-a0e72c6.local/`。结果为 54/54 report-set validation 通过，native-headless included，analysis `decision = no-change`、risk `low`、`canEvaluateNativePlayback = true`。

目标 A/V native-headless case `local/native-headless-av-smoke` 的 baseline 证据显示问题仍集中在 audio-clock gating / wait scheduling：30fps expected frame duration `33.333ms`，render P50/P95/P99 `33.3276/39.8287/40.6644ms`，audio-ahead wait P50/P95/P99 `23.8121/33.6223/34.0229ms`，audio-ahead oversleep P50/P95/P99 `0.4546/7.5068/7.7553ms`，A/V drift P95 `10ms`，audio/video starvation 均为 `0`。

本轮按 TDD 尝试了一个未提交的 precise audio tail 候选：先给 `RenderLoopWaiterTests.cpp` 增加 `WaitForPrecise(2ms)` 期望并确认红灯失败于缺少 API；随后实现 timer 等大头、`yield` 等 750us 尾段，并只让 audio-ahead wait 使用该路径，video-clock wait 保持不变。targeted `RenderLoopWaiterTests`、`FramePacingTests` 和 native-headless smoke 均通过。

随后生成 candidate：`docs/qa/private/candidates/playback-core-tuning-precise-audio-tail-54case-working.local/`，并与新 baseline 输出 comparison：`docs/qa/private/comparisons/playback-core-tuning-precise-audio-tail-54case-working.local/`。suite 结论为 `reject-candidate` / `reject-candidate`，54/54 可比，4 improved、1 regressed、0 mixed、49 unchanged，blocker 为 `suite.regression`。唯一硬回归是目标 A/V case：`framePacing.renderIntervalP99ExpectedErrorMs` 和 `framePacing.maxFrameGapExpectedErrorMs` 从 `7.331067ms` 增至 `9.434867ms`，delta `+2.1038ms`。

结论：precise audio tail 不能作为当前播放策略保留；代码已回退，当前 Core/native 行为不包含该候选。该实验说明单纯在 audio-ahead wait 尾段 yield 可能降低部分 oversleep 分位，但会放大目标 A/V case 的尾部 frame pacing gap。下一步不应继续围绕尾段 spin/yield 调参，应优先做重复采样/更长 A/V 样本，或寻找能同时降低 oversleep 与 P99/max gap 的更明确调度机制。

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

# 2026-07-09 更新：native video-only cadence 样本改为 5 秒，A/V smoke 仍保持 3 秒

本轮只调整评测 harness 的样本策略，不改变播放器 Core/native 行为：`run-native-headless-harness-smoke-test.ps1` 中 video-only SDR/HDR cadence 样本改为 5 秒，`local/native-headless-av-smoke` 继续保持 3 秒。对应脚本测试已加入 `run-playback-core-checks.tests.ps1`，防止后续把 cadence-only 样本和 A/V smoke 样本时长再次混在一起。

原因：上一轮 accepted 当前基线 `f96aaa8` 的 3 次重复采样显示，9 个 native case 中 8 个稳定，唯一不稳定的是 `local/native-headless-hdr10-60`。该 case 是 60fps video-only HDR cadence 短样本，3 秒样本对 P95/P99 尾部分位数过于敏感。工作区验证把 video-only cadence 样本延长到 5 秒后，重复采样产物 `docs/qa/private/repeats/playback-core-tuning-native-5s-cadence-av3-working-repeat.local/` 显示 HDR10-60 变为稳定：P95 spread 约 `0.5131ms`，P99 spread 约 `0.4566ms`。

边界：这不是播放质量优化，也不证明真实 Xbox/HDMI 输出、HDR 颜色、A/V sync 或主观流畅度改善。A/V smoke 在同一轮 3 次重复采样中仍是不稳定项，P95 spread 约 `2.4487ms`、P99 spread 约 `1.1301ms`，因此它仍是后续 wait scheduling / A/V gating 调优的独立证据目标。

验证：`tools/quality-run/run-playback-core-checks.ps1` 已通过，覆盖 Core tests、quality CLI、native-headless smoke、manifest/report/comparison 测试、native helper tests 和 native build。

提交 `cda19d2` 后已生成新的 commit-bound 54-case report-set：`docs/qa/private/candidates/playback-core-tuning-native-cadence-5s-54case-cda19d2.local/`，validation 通过，包含 54 个 report，native-headless included。与旧 3 秒 cadence baseline `playback-core-tuning-decode-mode-evidence-54case-d687248.local` 的迁移对比输出为 `docs/qa/private/comparisons/playback-core-tuning-native-cadence-5s-54case-cda19d2.local/`，结果为 `reject-candidate`：`local/native-headless-av-smoke` improved，`local/native-headless-hdr10-60` regressed。

该 reject 不作为 Core 播放策略退化结论。原因是 video-only cadence 样本时长从 3 秒改为 5 秒，旧 report-set 与新 report-set 的 P95/P99 tail statistics 不再适合作为同一策略候选的门禁比较。后续真实 Core 调优应以 `playback-core-tuning-native-cadence-5s-54case-cda19d2.local` 作为新的迁移基线。

# 2026-07-09 更新：default render-loop wait 改用 high-resolution timer，60fps cadence 改善

本轮基于新 5 秒 cadence 基线 `playback-core-tuning-native-cadence-5s-54case-cda19d2.local` 做了一个通用 native 调度策略候选：默认 render-loop 5ms wait 不再走 `std::this_thread::sleep_for`，而是和 audio/video-clock wait 一样通过 `RenderLoopWaiter` 使用 high-resolution waitable timer。该改动已提交为 `c129249 chore: use timer for default render loop wait`。

已生成 commit-bound candidate：`docs/qa/private/candidates/playback-core-tuning-default-render-loop-timer-54case-c129249.local/`，comparison 输出为 `docs/qa/private/comparisons/playback-core-tuning-default-render-loop-timer-54case-c129249.local/`。结果：54/54 case 可比，validation 通过，`decision = keep-candidate`，`action = accept-candidate`，3 improved、0 regressed、0 mixed、51 unchanged，strong confidence 54/54。

关键指标：`local/native-headless-hdr10-60` 的 render P95/P99 expected error 从约 `3.1662/4.4410ms` 降到 `0.4094/1.2259ms`；`local/native-headless-sdr-60` 从约 `3.6700/4.3778ms` 降到 `0.4238/0.4950ms`。`local/native-headless-av-smoke` 的 audio-ahead oversleep P95/P99 从约 `7.5806/14.3632ms` 降到 `3.9499/4.1270ms`，但它的 render P95/P99 没有作为 improvement 记录，因此不能宣称 A/V smoke 整体流畅度已经改善。

验证：策略实现按 TDD 添加 native frame pacing test，先红灯失败，再实现通过；`tools/quality-run/run-playback-core-checks.ps1` 已通过。边界：这是纯软件 native-headless 证据，不证明真实 Xbox/HDMI 输出或主观观感已经改善；下一步应优先做重复采样确认 `c129249` 的稳定性，再继续新的播放策略候选。

已完成 `c129249` 的 3 次 native-headless 重复采样，产物为 `docs/qa/private/repeats/playback-core-tuning-default-render-loop-timer-c129249-native-repeat.local/`。结果：27/27 sample 可用，9 个 native group 中 8 个 stable，唯一 unstable group 是 `local/native-headless-hdr10-60`。该 case 的 P95/P99 expected-error spread 已很小，分别约 `0.0541ms` 和 `0.3106ms`；不稳定信号来自一次 max-frame-gap outlier，max-gap expected-error spread 约 `2.8572ms`。因此当前结论是：`c129249` 明显改善 60fps cadence 的常态 P95/P99，但仍不能宣称完全消除偶发 max-frame-gap outlier。

# 2026-07-11 更新：audio-ahead episode oversleep 语义修正完成同期控制验证

本轮以已接受的 `94108ae`（`playback: add audio render start lead`）为基线，调查 24-case native-headless-inclusive repeat 中的 A/V 不稳定。原 commit-bound baseline 共 22 个可重复 group，20 个稳定、2 个不稳定：`local/native-headless-av-smoke` 与 `local/native-headless-hdr10-60`。A/V drift 和 final delta 的 spread 为 `0ms`，异常主要集中在 episode oversleep 与 frame-pacing 尾部。

旧指标使用 `max(episode wall duration - first pass target, 0)`，会混入后续合法 wait、audio-clock stall/量化和 render-loop overhead。最终实现改为 `sum(max(actual pass wait - requested pass wait, 0))`，并累计全部 requested pass target。`RecordAudioAheadWaitPassMs` 返回已计算的正 oversleep，Graph 仅在当前 episode generation 内累加；Seek/reset 后晚到的旧 wait completion 不再污染新 episode。等待策略、容差与丢帧策略未改变。

报告现在显式写入 `timing.audioAheadWaitOversleepSemantics=sum-positive-pass-oversleep-v2`；旧报告缺省为 `episode-wall-minus-first-target-v1`。比较器遇到跨语义数据时不比较 episode target 或 oversleep 数值，只把真实存在相关证据的 case 标为 partial/unmatched；没有相关样本的其他 case 保持 strong。对应单元测试按 TDD 先失败后通过。

最终正式候选与三轮 repeat 均使用同一 24-case manifest，24/24 校验有效并包含 9 个 native-headless case。versioned repeat 为 21/22 stable：HDR10-60 本轮稳定，唯一不稳定的是 A/V smoke；其 frame P95/P99/max-gap spread 为 `2.0331/4.9483/4.9483ms`，新语义 oversleep P95/P99 spread 为 `2.4227/2.1727ms`，A/V drift 与 final delta P95 spread 仍为 `0ms`。

为排除采样窗口漂移，使用未修改的 `94108ae` 在同一时段重新运行三轮控制组。控制组仍为 20/22 stable，且同样只有 A/V 与 HDR10-60 不稳定；A/V frame P95/P99/max-gap spread 为 `2.3786/6.3443/6.3443ms`，HDR10-60 P99/max-gap spread 为 `2.5682/3.0533ms`。这证明正式 comparison 中的尾部尖峰不是该 metrics-only 候选独有。

同期逐轮 comparison 归档于 `docs/qa/private/comparisons/playback-core-tuning-audio-pass-oversleep-versioned-current-window-control.local/`：三轮分别为 `1 improved + 23 unchanged`、`1 improved + 23 unchanged`、`24 unchanged`，均为 0 regression；统一因为 A/V oversleep 语义不同而输出 `review-unmatched-signals`。正式旧 baseline comparison 的 `reject-candidate` 仍保留，目标 case 是 A/V 与 HDR10-60，不修改 manifest、expected、threshold、materiality 或 acceptance rule。

采纳结论：保留实现作为 metrics-semantics 修正，不标记为 accepted playback-quality improvement。当前剩余风险是 Windows 短样本中的 A/V frame P99/max-gap 尾部噪声，以及尚未单独报告的 episode loop overhead；下一轮播放策略候选必须以新语义报告为新基线，不能再跨语义比较 oversleep。

# 2026-07-11 更新：native interaction evidence 固定为新的 24-case baseline 口径

`local/native-headless-av-smoke` 已从仅发现轨道的样本升级为真实交互样本：两个音轨、两个嵌入字幕轨、字幕关闭与 `1s` seek 都会进入 lifecycle evidence。cadence、A/V 与 buffering 指标仍取自交互前 snapshot；这些新增 interaction 不把后续状态混入既有播放采样窗口。

lifecycle 的 `failed`/`error` 现在是 evaluator 的正式失败，而不是被折叠为 harness 成功或缺失证据。parser 对缺失或非法的 interaction stdout 严格报告 `evidence-collection` failure；反之，完整 report 中诚实记录的字幕失败仍属于可采集、可评估的播放证据。

当前旧 Core 行为下，两次 embedded subtitle switch 的 cue count 都是 `0->0`，因此 A/V case 的两次 subtitle-switch 都失败，case result 为 `fail`、failure area 为 `subtitles`，且没有 `evidence-collection` error。它是下一步验证 current-position demux resync 假设的基线，不是已修复的字幕能力。

由于 manifest 已把 A/V case 的交互语义改为上述真实操作，旧 baseline 不可直接用于播放器播放质量改善比较；需要在 evidence-only docs commit 的 clean HEAD 上重新生成包含默认公开 manifest、私有 Emby manifest 与 native-headless 样本的 24-case baseline。本轮不改变 Core/native 播放策略、App/UI、manifest expected、阈值或 case ID，也不把 baseline 迁移描述为播放质量改善。

# 2026-07-11 更新：embedded subtitle current-cue switch 完成 24-case 验证并限定采纳

本轮基线为 `docs/qa/private/baselines/playback-core-tuning-native-interactions-27a7c1d-24case.local/`，对应完整 revision `27a7c1de80c188074d7c55d4ed53ec2f19290a19`；候选为 `docs/qa/private/candidates/playback-core-tuning-subtitle-resync-fcd194a-24case.local/`，对应完整 revision `fcd194a10e643edc6249330746fe893802bc018a`。三轮重复采样位于 `docs/qa/private/repeats/playback-core-tuning-subtitle-resync-fcd194a-native-repeat.local/`，comparison 位于 `docs/qa/private/comparisons/playback-core-tuning-subtitle-resync-fcd194a-24case.local/`。candidate 与每轮 repeat 都以 baseline 保存的 `manifests/core-reference-manifest.local.json` 作为唯一 core manifest 输入，使用 `-NoPrivateManifest`，并包含 native-headless。

baseline 与 candidate 均为 24 reports，report-set validation 均为 24/24 matched、0 error。除 comparison 自带的 case ID 校验外，本轮还按 case ID 建索引，对两个 unified manifest 的 `expected` 做递归 leaf 深比较，并对 `uri` 做区分大小写的完整字符串比较；24 个 case ID、全部 expected leaf 与全部 source URI 均全等，没有 missing、extra 或 duplicate case ID。

目标 A/V case 在 baseline 中为 `fail`：两次 `subtitle-switch` 均为 `failed`，cue count 分别为 `0->0` 和 `0->0`，failure area 为 `subtitles`。candidate 中同一 case 与 model analysis 都为 `pass`，两次切换均为 `completed`，cue count 为 `0->16` 和 `16->32`；`audio-switch`、`subtitle-off`、`seek` 也全部 `completed`，seek error 为 `0ms`，没有 failure reason、failed check、error code 或 `evidence-collection` failure。

三轮 repeat 分别使用 `fcd194a-repeat-1/2/3`，每轮均为 24/24 valid、native-headless included、24 份报告 revision 一致。三轮字幕 cue 增长分别为 `0->16, 16->31`、`0->16, 16->32`、`0->15, 15->30`；每轮 audio switch、subtitle off、seek 都为 `completed`，seek error 都为 `0ms`，A/V case 与 model analysis 都为 `pass`，无 failure/evidence error。

repeat stability 使用 `MinimumSamples=3`、`MaterialityMs=2.0`：93 个扫描到的 report JSON 中有 66 个可用 cadence samples，组成 22 groups；17 stable、5 unstable、0 insufficient。unstable groups 为 A/V smoke、HDR10 23.976、HDR10 60、SDR 23.976 与 SDR smoke。A/V 三轮 render P95 为 `37.1976-37.7035ms`，P99 为 `37.9208-42.0375ms`；其 P95/P99/max-gap expected-error spread 为 `0.5059/4.1167/4.1167ms`。但 A/V drift P95/P99 三轮均为 `10/10ms`，对应 spread 都为 `0ms`；final delta P95/P99 spread 也为 `0ms`，video/audio starved passes 均为 `0/0`，seek error 均为 `0ms`，两条音轨、两条字幕轨、最终音轨 index 2 与字幕关闭状态保持一致。process CPU ratio 范围为 `0.12421406037578431-0.14625977289130998`。

comparison 使用 `-MatchBy run-id` 并显式传入 candidate cadence stability。结果为 24/24 comparable，1 improved、0 regressed、0 mixed、23 unchanged，23 strong confidence、1 partial confidence。A/V case 有 123 个 matched signals，覆盖 A/V sync、cadence、buffering、seek/timeline、track/subtitle、process cost 与 color/DXGI；唯一 unmatched 是 baseline 侧的 `lifecycle.subtitle-switch`，candidate 侧没有 unmatched signal。自动 decision/action 仍为 `review-unmatched-signals`，risk 为 `medium`，active suite gate 因 `suite.partial-evidence` 显示 `blocked`；这不能改写成自动 `accept-candidate`。唯一 suite improvement 来自 HDR10-60 的单轮 frame-pacing 尾部值，而该 case 在 repeat 中为 unstable，因此不采纳为 frame-pacing improvement。

采纳结论：人工审查上述唯一 unmatched 后，只限定采纳 `fcd194a` 的 embedded subtitle current-cue switch 修复。依据是同一 24-case manifest 下目标 lifecycle 从两次 `failed`/case `fail` 变为两次 cue 严格增长的 `completed`/case `pass`，且三轮 repeat 全部重现；同时 0 regression、manifest 全等、validation 无错。自动 comparison 的 `review-unmatched-signals` 与 `medium` risk 仍原样保留。该结论不表示 frame pacing、整体 A/V sync、HDR/color、真实设备输出或 Xbox 播放已经解决；5 个 unstable cadence groups 与 Windows 短样本尾部抖动仍是剩余风险。
# 2026-07-11 更新：长暂停与 HTTP 断线恢复进入正式 native report-set

`local/network-reconnect-pause-resume` 已从只检查 helper stdout 的旁路 smoke 升级为正式 `manifest -> native-headless -> raw report -> materialize -> strict validate` case。确定性 Range 服务器会在首个请求中途断开连接；case 暂停 1 秒后恢复，服务器日志必须出现第二个非零 Range 请求。

native helper 的长暂停模式现在继续输出完整 source、decoded/rendered、timing、buffering、A/V、track 和 color/DXGI 快照，并跳过音轨、字幕和 seek 操作，避免把多个场景混进同一次 attempt。报告 lifecycle 记录暂停前位置、恢复后位置、恢复后的 decoded/rendered 帧数和 `playbackFailed`；无 seek 时不再写入默认 seek target 或错误的 stop position。

本轮正式验证结果：runner `selected=1 / attempted=1 / reports=1 / failed=0`；strict validation 的 structure/execution 均有效，execution coverage 为 opened/decoded/rendered/completed 各 1，且故障服务器观察到 `request=2`。全量 Core tests 为 `840/840`，逐 case runner tests、门禁计划测试和完整 native-headless smoke 均通过。

边界：该 case 证明当前纯软件 native 链路可在确定性 HTTP 断线和暂停后继续播放，不证明任意真实服务器、长达数分钟的暂停、Xbox 网络栈或 App 后台生命周期都已覆盖。私有 Emby 长暂停 case 仍需在 source resolver 完成后加入同一正式流程。

# 2026-07-11 更新：私有 Emby case 已真实进入 native runner

新增独立 `NoiraPlayer.PlaybackQuality.Runner`，从进程环境读取私有 Emby 服务器凭据，复用 `EmbyApiClient.AuthenticateAsync/GetPlaybackInfoAsync` 按 `itemId/mediaSourceId` 解析 direct stream URL。URL 只在 runner 与 headless 子进程内存和参数中存在；manifest、summary 与报告只保存 `emby://` locator、匿名 SHA-256 关联和脱敏错误码。仓库中没有提交服务器地址、账号、密码、真实 direct URL、item ID 或 media source ID；本地 manifest/report-set 继续位于已忽略的 `docs/qa/private`。

已用 ignored manifest 真实执行“一战再战”和“哈姆奈特”的代表性 HDR 源。两者都通过运行时解析打开远端媒体，进入 HEVC 硬件解码并生成 strict validator 可匹配的 native-playback report；观察到的代表性证据分别约为 `decoded 62 / rendered 61`，不是 probe 或 expected 物化。两份 playback report 均诚实为 `FAIL`：当前暴露字幕切换、部分音轨切换、远端启动耗时和离屏 color/DXGI 预期差异；离屏 Windows runner 的 SDR output 不能解释为 Xbox HDR 输出失败。

resolver 失败现在也必须生成标准 `error` report，execution level 为 `orchestration`、`sourceOpenAttempted=false`，因此报告可追踪但仍不能满足 stable/challenge 的 native-playback strict gate。manifest runner 回归结果为 `selected 4 / reports 4 / unresolved 1 / missing 0`，并保持整体非零退出。

修复了另一处证据丢失：native helper 已经输出完整 source/decode/render 遥测后再非零退出时，headless 不再把它重写成“从未打开源”。parser fixture 现在验证同一报告同时保留 `result=error`、`decoded/rendered > 0`、`sourceOpened=true`、`demuxStarted=true` 和 `playbackSampleObserved=true`，用于区分启动失败与播放中途失败。

Task 4 的 Core `483/483`、CLI smoke、manifest/resolver、私有 manifest、parser contract 和完整 native-headless smoke 均已通过。统一 `run-playback-core-checks.ps1` 已继续执行到 Task 5，并按预期在 legacy `New-PlaybackCoreTuningBaseline.ps1` 使用 core-probe 填充 stable/challenge case 后被 strict validator 拒绝；这不是 resolver/native runner 回归。下一步进入正式 baseline 编排替换，彻底停止用 core-probe 填充播放 case。真实 App 中出现的 EAGAIN、暂停恢复 I/O error、timeline/seek 异常仍需在同一 evidence contract 下沉淀为回归 case，最终再做完整 App 编译与代表性 App-hosted 复核。

# 2026-07-12 更新：timeline 归一化、音频预滚与单场景评测已完成真实私有复测

native 时间线现在统一以容器 start time 为逻辑零点：解码帧位置先归一化，seek 时再把逻辑目标恢复为 demux 时间戳；duration 优先采用音视频流时长，再回退容器时长。实现对照了 Kodi `f0232910490189b97717bc5d309aec2e5751d6d3`、VLC `8e476d86cfbbb00833dfd9deb2f5324b074f3ca0` 和 mpv `e5486b96d7d06dd148337899bfdc46bf25101663` 的 start-time/seek 语义。确定性非零 start MPEG-TS 样本以及 resume+seek 样本均已进入 native smoke，报告会记录 container/stream origin、logical duration、demux target、first-presented landing 和 post-seek advancement。

私有 Emby timeline case 暴露了独立的音频预滚 bug：从 60 秒启动时，demux 回退到约 54 秒关键帧，视频会丢弃目标前帧，但音频显式时间戳会覆盖 seek anchor，使音频主时钟回到 54 秒，最终出现 `decoded=142 / rendered=0`。新增 `AudioFramePreroll` 后，目标前的完整音频帧在 decoder 内部持续丢弃，跨越目标的首帧从目标位置发布；同一 case 复测为 `decoded=203 / rendered=61 / submittedAudio=89`，seek 后继续推进，A/V drift P95 曾观察为 `13ms`。

评测器同时修复了两处会妨碍归因的问题。headless 现在会在每个报告旁保存经过 stream URL 脱敏的 helper stdout/stderr，超时和解析失败也必须保留；helper 还实时输出阶段标记，可区分 graph open、采样、seek 和 stop 阻塞。manifest runner 根据 case purpose 选择 `playback`、`timeline`、`interactions` 或 `pause-resume` 单一场景，timeline case 不再夹带音轨/字幕切换。隔离后的同一私有 timeline case 为 `pass`，execution completed，只有一个 seek lifecycle，0 个 audio/subtitle interaction；本次 seek first-presented error 为 `478ms`，低于既有 `500ms` 标准但余量很小，仍是后续调优风险。

验证结果：统一 App-free gate 已覆盖 491 个 Core 测试、CLI/runner、manifest、native smoke、baseline/candidate 脚本、独立 C++ timeline/audio/EAGAIN/display/offscreen 回归和 Native Debug x64 build；完整 Windows UWP App x64 Debug 也已通过官方 `Build-Noira.ps1 -Target Build` 重新编译。上述软件结论仍不证明 Xbox/HDMI 输出；旧的混合 interaction+seek report-set 不应与新单场景报告直接比较。

# 2026-07-11 更新：正式 baseline 已停止使用 core-probe

`New-PlaybackCoreTuningBaseline.ps1` 不再调用 `materialize-core-probe-report-set`。公开、私有和 additional manifest 合并后，所有选中的 stable/challenge case 都先进入 `Invoke-PlaybackQualityManifest.ps1`；baseline 保存 runner summary，并要求 `selectedCaseCount > 0`、`reportCount == selectedCaseCount`、`missingReportCount == 0`，再进行 unified manifest 的 strict validate/analyze。

`-SkipNativeHeadless` 现在只跳过附加的本地生成 cadence/HDR 样本，不跳过 core manifest 播放；使用该开关时必须显式传入 native helper。正常模式会先运行 native-headless smoke，复用其构建的 helper，并继续追加已 strict-valid 的本地 native report-set。

manifest runner 因真实播放 `fail/error` 非零退出时，只要每个 case 都留下完整报告，baseline 会继续交给 strict validator 裁决，并在 summary 中保留 failed attempt warning。缺报告测试确认 `selected 1 / reports 0 / missing 1` 必须终止；一个真实 native execution error report 则可形成 `1/1` strict-valid baseline。播放器失败因此可评测，评测器缺证据不可放行。

CLI 新增 `materialize-evaluator-self-test-report-set` 作为 deterministic core-probe 的明确入口，旧命令仅保留兼容。source-only、core-probe 和未执行 skip 归为 evaluator self-test；当前 strict playback gate 必须拒绝。Task 5 定向验证为 baseline tests 通过、CLI build/smoke 通过、Core `483/483`。下一步是 Task 6：阻止不同 execution level、locator 或 opened source 的 baseline/candidate 产生 improvement/regression。

# 2026-07-12 更新：真实 DV 证据与 23-case 统一基线完成闭环

native 不再在 `VideoDecoder::Open` 成功、失败或 seek 后丢弃已经从 FFmpeg side data 解析出的 Dolby Vision 配置。`PlaybackGraph` 会保留最近一次实际打开的流快照；Profile 5 无 HDR10/HLG fallback 时，helper 输出结构化 unsupported code 与真实 codec、尺寸、帧率、profile、compatibility ID 和 base-layer 证据。headless 将其记录为 `result/execution=unsupported`，不再误报为 helper 崩溃或 instrumentation 缺失。Profile 8.1 则明确记录 `DolbyVisionWithHdr10Fallback`，不再退化为普通 HDR10。

公开 Jellyfin P5 与 P8.1 素材均已真实复测：P5 为 source opened、demux started、decoder unopened 的可归因 unsupported；P8.1 为 profile 8、compatibility ID 1、HDR10 base layer true，并完成 native 播放。私有 Emby 中六个旧 HDR10 case 也由实际流侧证据识别为同类 P8.1 fallback；ignored manifest 已据此修正，不依赖文件名或 Emby 预分类。

最终本地 baseline 为 `docs/qa/private/baselines/playback-evidence-v8-c6cdf08.local/`，绑定 revision `c6cdf08`。core runner 为 `selected 14 / attempted 14 / reports 14 / failed 0 / unresolved 0`；合并 native-headless 后为 23 reports、23 matched、0 validation error，strict validation 有效。结果为 21 pass、1 unsupported、1 fail；唯一 fail 是“一战再战”SDR 源的字幕切换，已准确归因为 `subtitles`，模型分析无缺失证据，并指向 SubtitleDecoder、SubtitleRenderer 与 PlaybackGraph。

评测器同时修正了两处自我误导：lifecycle failure 会沿用检查中已经确定的 tracks/subtitles/timeline/playback-lifecycle，而不再落入 unknown；普通 playback 不再被无条件要求 pause/resume，只有观察到其中一个操作时才要求成对证据，正式 pause-resume case 仍由 manifest required-signal policy 强制。最终 analysis confidence 为 strong 23/23、blocked 0、decision `no-change`、risk low。

验证：Core tests `857/857`、CLI smoke、parser 负向契约、完整 native-headless smoke、网络断线恢复、公开/私有 manifest runner 与 strict report-set validation 均通过。完整 Windows UWP App x64 Debug 已通过 `tools/Build-Noira.ps1 -Target Build`，生成 `NoiraPlayer.Native.dll` 与 `NoiraPlayer.App.dll`。本轮仍不声称 Xbox/HDMI 颜色输出已被软件评测证明；下一步应针对保留下来的字幕切流失败做同 manifest 的小步 candidate 调优。
# 2026-07-12 更新：PGS 位图字幕与单场景评测闭环完成

本轮修复了评测器无法真实覆盖 PGS 字幕切换的问题，并用同一私有 Emby manifest、同一匿名 case、同一媒体源完成旧 Core 与候选 Core 对照。manifest SHA-256 为 `8BBFA43688791988A5702D623A058BD57714BCBABDC748194463B50E25F3C10F`；账号、密码、直链和报告仍只保存在 ignored/local 路径，没有进入仓库。

播放器侧新增 FFmpeg `SUBTITLE_BITMAP` 解码与 PGS indexed palette 转 premultiplied BGRA，保留字幕画布坐标，并通过 Direct2D 合成到视频 backbuffer。旧 Core 基线完整打开源、demux、decoder 和 native graph，发现 5 条字幕轨并选中索引 3，但 cue render count 为 `0->0`，结果 `fail`、failure area `subtitles`。候选 `872d517` 在同一 case 上 cue render count 为 `0->1`、渲染 38 帧，结果 `pass`，没有 missing evidence。

评测契约同时完成以下修正：

- 每个 case 必须显式声明 `playback`、`timeline`、`audio-switch`、`subtitle-switch` 或 `pause-resume`；多个主动执行意图会被 manifest validator 拒绝。
- audio 与 subtitle 切换不再由合并的 `interactions` 场景执行；runner 和 helper 只执行 case 指定的一个动作。
- helper 非零退出且 stdout 无法解析时优先保留 stderr/runtime 根因，不再被二次解析错误覆盖。
- `openedSourceHash` 对 Emby 临时直链忽略已确认的 `PlaySessionId`，但保留 host、path、`MediaSourceId` 等稳定身份。两次对照现在既有相同 `sourceLocatorHash`，也有相同 `openedSourceHash`。
- comparator 对 audio/subtitle interaction 使用对应 lifecycle 结果，只有普通 `playback` case 才比较 frame-pacing 派生指标。最终真实 comparison 为 `comparable / improved / keep-candidate`，改进信号为 `lifecycle.subtitle-switch: resolved`，无回归。

验证：完整 `run-playback-core-checks.ps1` 的 32 个阶段全部通过，包含 510 个定向 Core 测试、native-headless 真解码/渲染、网络恢复、独立音轨/字幕切换、PGS bitmap、DX offscreen 和 native build。最新 comparator/hash 修正完成后，全量 Core 测试 872/872、playback-quality CLI smoke 和完整 Modern UWP Debug x64 App 构建再次通过。

边界：当前只证明 Windows 软件链路能解码并合成该 PGS cue，不证明 Xbox HDR backbuffer 上的字幕 reference white、色彩或性能完全正确；完整 ASS 样式、外置字幕和 subtitle offset 仍未实现。

# 2026-07-12 更新：manifest expected 绑定后的 unsupported 语义已修正

v11 首次把 manifest 的完整 `expected` 绑定到真实 native captured reports 后，严格校验暴露出 evaluator 顺序错误：DV Profile 5 已被实际流证据正确识别为 `unsupported`，却先因源探测耗时超过 `maxStartupDurationMs` 被重判为 `fail`，导致 execution status 与 report result 不一致。

评估顺序现已修正：预期不支持的源仍必须通过生命周期和实际源分类校验，但不执行启动耗时、帧、色彩等可播放源质量阈值。回归测试覆盖“分类正确但探测超过 3 秒”的 DV P5；同一批 v11 captured reports 重新物化后为 24/24 matched、0 validation error，结果为 11 pass、12 fail、1 unsupported。

剩余 12 个 fail 被原样保留：主要是远端启动耗时超限及 headless 环境的 HDR/DXGI 输出与 App/显示预期不一致，私有 timeline case 还包含 `520ms > 500ms` 的 seek 偏差。它们是后续区分播放器问题、环境边界和场景预期适用范围的输入，不构成本轮播放器质量改善。

# 2026-07-12 更新：首套 evaluator-consistent v12 基线已建立

commit-bound v12 位于 ignored 路径 `docs/qa/private/baselines/playback-evidence-v12-evaluator-consistent.local/`，绑定 revision `d44826c`。首轮 6 个公开 Jellyfin case 同时遇到 TLS EOF/I/O error，strict validator 因缺失真实源与播放证据正确拒绝报告集；仅重跑相同 6 个 case 后全部产生真实报告。最终 v12 为 24/24 matched、0 validation error、11 pass、12 fail、1 unsupported，且凭据/服务器地址/token 扫描 0 命中。首次失败和重试摘要均保留在 baseline provenance 中，没有静默覆盖环境故障。

v12 同时揭示 interaction 评测缺口：私有 audio-switch 的生命周期虽为 completed，但实际 `maxFrameGapMs=5319.28`，75 帧解码仅 38 帧呈现；现有 typed contract 只要求目标音轨选中、position 和 submitted audio frames 各自增加，没有记录达到这些条件所需时间。实现计划已写入 `docs/superpowers/plans/2026-07-12-interaction-recovery-evidence.md`。下一步先补结构化 operation/recovery duration 和 manifest-owned threshold，再生成新的规则版本 baseline；不得把它与 v12 跨规则比较成播放质量改善。

# 2026-07-12 更新：v13 交互恢复证据基线完成

音轨和字幕切换现已记录独立的结构化交互证据，包括同步切换调用耗时、从操作开始到播放恢复的总耗时、位置推进以及音频提交或视频呈现增量。manifest 以版本化的 `maxInteractionRecoveryDurationMs` 声明产品级恢复上限；当前 audio-switch 与 subtitle-switch 均为 `2000ms`。缺少、非有限或超过阈值的观测必须失败，不能用最终 `completed` 状态掩盖长时间中断。

ignored v13 基线位于 `docs/qa/private/baselines/playback-evidence-v13-interaction-recovery.local/`，绑定 revision `3373418`。私有 runner 为 `selected 13 / attempted 13 / reports 13 / failed 0 / unresolved 0 / missing 0`；合并本地 native-headless 后共 24 份报告，strict structure/execution validation 为 24/24 matched、0 error，结果为 11 pass、12 fail、1 unsupported。凭据、服务器地址和 token 扫描为 0 命中。

本地生成样本的音轨切换 operation/recovery 约为 `27.67/92.60ms`，字幕切换约为 `0.65/170.95ms`，均通过 2000ms 上限。相同契约首次暴露真实私有源的长中断：音轨切换 operation/recovery 约为 `3286.21/3791.06ms`，PGS 字幕切换约为 `592.89/33973.80ms`，分别归因为 `tracks` 与 `subtitles`。这些是播放器 Core/native 的待优化事实，不是评测器失败；下一阶段必须复用 v13 的同一 manifest 做小步 baseline/candidate 对照。

v12 只保留为引入恢复时延契约前的观察集。v12 与 v13 的 expected schema 不同，不得把两者直接比较为播放器质量改善或退化。完整 `run-playback-core-checks.ps1` 已全绿，包括 555 项定向 Core 测试、真实 native-headless、网络恢复、EAGAIN、seek/timeline、帧节奏、音轨字幕、色彩证据与 Native Debug x64 build。统一入口 `tools/Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64` 也已完整编译 Core、Headless、Native 和 Modern App，生成 `NoiraPlayer.Native.dll` 与 `NoiraPlayer.App.dll`。

质量门执行时发现候选对比测试仍生成旧版 timeline 夹具，缺少 source origin、demux target、first-presented、post-seek position 与 advancement，因而被当前 strict gate 正确拒绝。夹具已补齐真实 timeline 字段，评测规则和阈值没有放宽；独立候选对比测试及随后完整质量门均通过。

# 2026-07-12 更新：交互恢复证据已拆分并定位到远端 seek

v13 把字幕 cue 增长误写入 `interaction.renderedVideoFrameDelta`，并把“首个视频帧恢复”和“字幕 cue 出现”合并为同一个 recovery 条件。这会让模型无法区分播放恢复、字幕可见性和真实视频呈现。v14 契约现分别记录 `recoveryDurationMs`、`cueRenderDurationMs`、真实 `renderedVideoFrameDelta` 与 `subtitleCueRenderCountDelta`；subtitle completed 仍要求真实 cue，但 2000ms 播放恢复 SLO 只消费首个呈现、位置推进和目标轨选择。required-signal policy 同时强制 operation、lock wait、execution、分阶段耗时及场景对应的音频/视频/cue 增量，缺失字段不能通过。

三轮同源私有复测中，audio-switch operation 为 `4523.46-5012.32ms`、recovery 为 `5459.14-6389.63ms`；PGS subtitle-switch operation 为 `1139.93-1306.46ms`、recovery 为 `4481.36-6281.40ms`。锁等待三轮均约为 0ms，推翻了“render loop 长时间持锁是主要瓶颈”的初始假设。进一步分段采样显示：audio-switch 的 `4087.65ms` execution 中 `4048.54ms` 在 video seek，decoder open 仅 `0.14ms`、renderer open `33.13ms`；subtitle-switch 的 `1349.12ms` execution 中 `1349.03ms` 在 video seek，其余阶段均低于 `0.05ms`。当前 Core 问题因此归因为切换未激活轨道时同步执行远端 `av_seek_frame`，不是 codec 或音频设备初始化。

Kodi 本地源码同样在音轨切换后发送 accurate/backward seek；内嵌字幕切换也明确说明 demux 已领先并丢弃当前时段字幕包，因此重新 seek 读取。差异是 Kodi 通过 player messenger/demux 状态机处理，而本项目当前在 graph 切换调用内同步 seek。FFmpeg `multiple_requests=1` 作为单变量候选已用相同私有 case 实测，但该服务器/反代组合出现 `File ended prematurely`，两个 case 都没有有效播放样本；候选已撤销，失败报告保留在 ignored `docs/qa/private/candidates/http-persistent-v14-repeat-1.local/`，不得宣称改善。

验证：完整 `run-playback-core-checks.ps1` 全绿，包含 556 项定向 Core 测试、parser 负向契约、真实 native-headless、网络恢复、独立 C++ 回归与 Native Debug x64 build；统一 `tools/Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64` 也已重新编译完整 Modern App 并生成 `NoiraPlayer.App.dll`。本轮只增强证据并定位根因，没有宣称切流性能已经改善。

# 2026-07-12 更新：有界轨道包缓存消除远端切流同步 seek

native demux 现在会为未激活的音轨和字幕轨保留滚动 packet cache；每条流最多 `4096` 包、`8 MiB`、`30s`。切换时只有缓存覆盖当前播放位置才允许复用；音轨还要求缓存跨过当前位置，字幕要求存在当前位置之前的历史包。覆盖不足或显式关闭缓存时，仍走原有 seek 路径，不把 cache miss 伪装成成功。

同一私有 Emby manifest、同一当前 helper/schema 下完成了禁用与启用缓存各三轮实播。禁用时 audio-switch operation/recovery 为 `2241.46-3920.96ms / 2510.45-4483.36ms`，PGS subtitle-switch 为 `526.81-1864.12ms / 3818.88-9654.07ms`，主要耗时仍是远端 seek。启用时三轮 audio-switch 为 `38.71-40.73ms / 91.01-151.33ms`，PGS 为 `0.60-0.84ms / 157.50-182.40ms`；全部明确记录 `packetCacheEnabled=true`、`packetCacheHit=true`、`seekDurationMs=0`，并保留真实 position、audio、video 和 cue 增量。实际缓存规模稳定为音轨 `176 packets / 180224 bytes / 1.866s`、字幕 `26 packets / 418968 bytes / 1.043s`，远低于上限。

三轮候选 report-set 均为 `2/2 matched`、0 validation error。慢基线前两轮结构有效；第三轮 PGS 虽返回操作结束，但 cue 为 `0->0` 且位置倒退，strict validator 因缺少 position/cue 必要证据正确拒绝为 `1/2 matched`。该样本保留为基线不稳定/证据不足，不能删除、补默认值或算作播放器通过。当前结果证明 Windows App-free native 交互策略改善；完整 Core gate、完整 App 编译和 App-hosted 代表性复核仍是本轮剩余门禁。

# 2026-07-12 更新：切流候选完成完整 App-hosted 证据复核

完整 App 的 `quality-run` 现在能把 native 最近一次切流 phase/cache 快照与 App 单调时钟观测组合为 v14 typed interaction evidence。WinRT metrics 新增 scenario/sequence、lock/execution/quiesce/seek/decoder/renderer、cache hit/size/window，并在 open/stop 清空；App 独立测量 operation、播放 recovery、字幕 cue duration 以及 position/audio/video/cue delta。lifecycle 文本和日志不参与字段生成。

首轮 RED App 实播成功切流但 `interaction` 全空，strict evaluator 正确判为 insufficient instrumentation。桥接后第一次音轨实播又暴露场景错误：descriptor 的 audio index 为 null，而 native 实际已播放默认音轨 1，App 因而重复切到音轨 1并 cache miss，operation/recovery 为 `5006.50/6024.91ms`、seek `4962.11ms`。新增真实 `SelectedAudioStreamIndex` bridge 后，App 按 native 当前轨从 1 切到 2，不再针对具体 case 硬编码目标。

最终 App-hosted audio-switch 为 pass：operation/recovery `46.01/154.91ms`、cache hit、seek `0ms`、缓存 `216 packets / 221184 bytes / 2.293s`，position/audio/video delta 为 `920000 / 30 / 1`。PGS subtitle-switch 的交互本身通过：operation/recovery/cue `0.42/346.51/346.51ms`、cache hit、seek `0ms`，position/video/cue delta 为 `3400000 / 1 / 1`；报告整体仍因冷启动 `37764.07ms > 7000ms` 为 fail，未放宽阈值。

两份完整 App 报告已用 exact 两 case 私有 manifest 重新 materialize，strict structure/execution validation 为 `2/2 matched`、0 error。完整 Core gate 的 32 个阶段全部通过，包含 `562/562` Core 测试和约 240 秒 native-headless smoke；完整 Modern App Debug x64 build 也已生成最新 `NoiraPlayer.Native.dll` 与 `NoiraPlayer.App.dll`。私有凭据、case ID 和报告只保存在 ignored `docs/qa/private/app-hosted`。冷启动网络/stream-info 延迟应作为下一项独立 startup case 调查。
