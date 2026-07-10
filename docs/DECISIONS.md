# 技术决策

## 2026-07-10: 3ms audio render-start lead 作为当前 24-case 可采纳候选保留

决策：在 audio-ahead gating 中保留原有 `PlaybackFramePacing::VideoAheadToleranceTicks = 100000`，并新增 `PlaybackFramePacing::AudioAheadRenderStartLeadTicks = 30000`。`ShouldWaitForAudio` 与 `AudioAheadWaitDuration` 都使用同一个 `10ms + 3ms` 边界；`PlaybackGraph` 的 `audioAheadWaitTargetMs` 也改为直接记录同一个 wait duration。当前不采纳 `5ms` lead，也不恢复此前被拒绝的 audio wait cap / early-wake / half-frame wait cap。

原因：`render-after-wait` 证据显示 A/V smoke 的长 render interval 与 preceding audio-ahead wait 直接相关。3ms lead 的目标不是扩大 A/V 同步容忍到一个新阈值，而是给 render/present/scheduler 成本留出启动空间。当前 24-case comparison 输出 `accept-candidate / keep-candidate`、2 improved、0 regression：A/V smoke 的 P95 render expected-error 下降，HDR10-60 的 max frame gap expected-error 下降；A/V drift 与 finalDeltaAbs repeat spread 仍为 `0ms`。

影响：后续播放 Core 调优可以把 3ms render-start lead 作为当前 accepted 行为继续推进。比较候选时必须继续同时看 single-run suite、native repeat stability、A/V drift/finalDelta、audio-ahead wait pass/episode、render interval after audio-ahead wait、buffering、seek/timeline、tracks/subtitles 和 color/DXGI evidence。

边界：该决策不宣称 A/V smoke 完全稳定。candidate repeat 仍显示 A/V P99/max tail 不稳定，且 unstable signals 包含 audio-ahead 后 render interval P99/Max 与 episode-level oversleep。后续不能因为本轮 24-case gate 通过而放宽评测规则，也不能继续把 1-5ms 常量试验当成主要方向；下一步应处理残留 tail volatility。

## 2026-07-10: render interval preceding-wait 分桶作为 A/V 调度诊断证据保留

决策：保留 `timing.renderIntervalAfterAudioAheadWait*` 与 `timing.renderIntervalAfterNonAudioWait*` 字段，按每个 render interval 之前完成的 render-loop wait reason 分桶。该字段组只作为诊断 evidence，不改变 `PlaybackFramePacing` 策略，不新增 pass/fail 阈值，不作为播放质量 improvement 自动判定依据。

原因：当前 A/V smoke 的长短帧模式不能再只靠 render P95/P99、audio-ahead wait oversleep 或 final delta 判断。新 evidence 显示，A/V case 的 render interval P95/P99/max 与 audio-ahead preceding wait 直接重合，non-audio 分桶为 0；同时 repeat 中新分桶 P95/P99 spread 小于 1ms，说明这组证据比旧 episode-level oversleep 更稳定、更适合指导下一轮调度候选。

影响：后续模型可以直接判断长 render interval 是跟 audio-ahead gating、video-clock wait 还是其它 render-loop wait 相邻，而不是靠人工交叉推断。若后续候选要改善 A/V smoke，应优先让 `renderIntervalAfterAudioAheadWaitMsP95/P99/Max` 下降，并同时保持 A/V drift、final delta、buffering、track/subtitle、seek/timeline 和 color/DXGI evidence 不退化。

边界：本决策不宣称流畅度改善，不证明 Xbox/HDMI/HDR 输出质量，也不允许为通过测试而降低阈值。当前 artifacts 的 suite 决策仍为 `no-change`；它们证明证据链更完整，而不是证明播放策略已优化。

## 2026-07-10: video-clock wait cap 作为当前 24-case 可采纳候选保留

决策：无音轨 software video-clock wait 继续使用目标剩余时间等待，但单次 `PlaybackFramePacing::VideoClockWaitDuration` 返回值限制为最多 `10ms`。该限制只作用于 video-clock wait 路径；audio-ahead wait 不加 cap，不采用此前实验过的 audio wait 10ms cap。

原因：合并 main 后重新生成的 24-case candidate `playback-core-tuning-video-clock-wait-cap-after-main-merge-24case.local` 与 `907e8d0` baseline 对比后，suite 输出 `accept-candidate / keep-candidate`：1 improved、0 regressed、23 unchanged。改进 case 为 `local/native-headless-hdr10-60`，P99 render interval expected-error 从 `3.456633ms` 降到 `0.443133ms`，max frame gap expected-error 从 `4.507933ms` 降到 `0.495233ms`。这说明限制单次 video-clock wait 能降低 60fps video-only 短样本的尾部 overshoot，而没有在当前 24-case gate 中引入回归。

影响：后续播放 Core 调优可以把该 video-clock cap 作为当前 accepted 行为的一部分继续推进。若再调整 video-clock pacing，应继续使用同 manifest baseline/candidate comparison，并附带 native repeat stability；不能只看一次 smoke 的 P99/max。

边界：该决策不解决 A/V smoke 的残留 frame-pacing 尾部问题，也不证明真实 Xbox/HDMI/HDR 输出完全正确。native repeat 中 `local/native-headless-av-smoke` 和 `local/native-headless-hdr10-60` 仍可出现不稳定，因此后续仍需把 repeat stability 作为解释证据。audio-ahead wait cap 实验已拒绝，不应混入该 accepted 候选。

## 2026-07-10: audio-ahead wait episode/pass evidence 作为诊断字段保留

决策：保留 `timing.audioAheadWaitEpisodeCount` 与 `timing.audioAheadWaitPassesPerEpisodeP50/P95/P99/Max` 作为 native/Core/app quality report 的诊断证据。该字段组只描述 audio-ahead wait episode 的数量和每个 episode 内被拆成多少 wait pass，不改变 `PlaybackFramePacing` 策略，不新增 pass/fail 阈值，也不把单次 report 中的 pass 数变化直接解释为播放质量改善或回退。

原因：此前 half-frame wait cap 候选会增加 wait polling 频率，但旧报告只能看到 `audioAheadWaitCount` 变多，无法判断是 episode 数变多还是每个 episode 被拆成更多 pass。新的 episode/pass 证据让模型能解释“等待被拆碎”与“实际遇到更多 audio-ahead 情况”之间的区别。当前 24-case comparison 因 native-headless 单次 frame-pacing 波动仍输出 `reject-candidate`，但 3 次 repeat 显示本轮候选没有引入新的行为策略，A/V smoke 的 finalDeltaAbs 与 A/V drift spread 仍为 `0ms`，不稳定集中在 render P99/max 与 audio wait oversleep spread。

影响：后续 A/V smoke 调优应优先同时查看 `audioAheadWaitCount`、`audioAheadWaitEpisodeCount`、`audioAheadWaitPassesPerEpisode*`、oversleep、finalDeltaAbs、A/V drift 和 render interval tail。若一个候选只是增加 pass 数而没有降低 oversleep spread、frame P99/max、buffering 或 A/V drift，不应采纳。若 episode 数增加，则应调查 audio clock sampling、wait loop 退出条件、queued audio 状态和 render scheduling，而不是继续做简单常量调参。

边界：该决策不改变 evaluator gate，不把 repeat attribution 变成自动放行条件，不宣称播放效果改善，不证明真实 Xbox/HDMI 输出质量。candidate/baseline/repeat artifact 仅作为 ignored/private 诊断材料保留。

## 2026-07-10: 不采纳 audio-ahead half-frame wait cap

决策：不保留“按源帧率把 audio-ahead 单次 wait 限制为最多半帧”的候选。当前 Core/native 行为继续使用原有 `PlaybackFramePacing::AudioAheadWaitDuration(framePositionTicks, audioPositionTicks, hasQueuedAudio)` 计算，不新增 frame-rate-aware audio wait cap。

原因：该候选的目标是把一次长 audio-ahead sleep 拆成更频繁的音频时钟 polling，理论上降低 waitable timer oversleep。但 24-case comparison 输出 `split-candidate / high risk`：`local/native-headless-av-smoke` 的 P95 frame-pacing expected-error 改善约 `-2.9274ms`，同时 P99/max expected-error 回归约 `+2.2549ms`。3 次 candidate native repeat 进一步显示目标 A/V smoke 仍 unstable，且 P99/max spread 从 baseline 约 `4.04ms` 扩大到约 `6.7486ms`，audioAheadWaitOversleep P99 spread 从约 `4.1725ms` 扩大到约 `7.0819ms`。这说明该候选没有解决目标 A/V tail instability，只是改变了 wait/catch-up 分布。

影响：后续不要重复尝试“按半帧 cap audio-ahead wait”作为 A/V smoke jitter 修复。若继续处理该问题，应补更直接的 per-wait 证据，或设计能同时降低 P95、P99/max、audio oversleep spread 且不增加 catch-up volatility 的结构性 scheduling 策略。

边界：该拒绝不改变评测阈值、不删除 case、不否定 `907e8d0` 的短间隔证据字段。candidate/baseline/repeat artifact 仅作为 ignored/private 诊断材料保留；源码已回退。

## 2026-07-10: render short-interval evidence 作为诊断字段保留，不作为评分阈值

决策：保留 `timing.renderIntervalMsP05`、`timing.minFrameGapMs`、`timing.renderIntervalUnderExpected2MsCount` 和 `timing.renderIntervalUnderExpected4MsCount` 作为 native/Core/app quality report 的诊断证据。该字段组只解释 render interval 分布的短间隔侧和 catch-up 迹象，不新增通过/失败阈值，也不加入 manifest required signals。

原因：此前只看 P95/P99/max 与 over-expected 计数时，模型能看到长尾 frame gap，但无法判断后续是否通过短间隔补偿。成熟播放器调优通常不会针对单个文件写补丁，而是先把 “long gap 是否伴随 catch-up”、“是否有 starvation”、“是否有 A/V drift” 这些证据拆清楚，再调整通用 scheduling 策略。短间隔证据让后续调优能区分真实持续卡顿、短样本尾部波动和可恢复的 pacing compensation。

影响：native-headless、app-hosted quality-run、cadence repeat summary 和 candidate comparison 都可以携带 P05/min/under-count 证据。`Measure-PlaybackCadenceStability.ps1` 必须把旧 report 中缺失的短间隔字段视为 unknown/null，不能把缺失值转换成 `0ms`。后续候选如果只改善 max gap 但破坏短间隔补偿，或只改变 P05/min 而不改善 P95/P99/max、A/V sync 和 buffering，不应被自动解释为播放质量提升。

边界：这些字段不证明外部显示器/HDMI 输出正确，不改变 frame pacing gate，不放宽 stable case 标准，不修改播放 core/native 行为。若未来要把短间隔纳入正式评分，必须另起决策并用历史 report 回放验证。

## 2026-07-10: native A/V smoke 使用 40 帧最低渲染阈值

决策：`local/native-headless-av-smoke` 的 manifest 不再使用占位式 `minRenderedVideoFrames = 1`，改为 `40`，并由 `run-native-headless-harness-smoke-test.ps1` 断言 materialized report 保留该期望。

原因：该样本文件实际为 `2.5s / 75` 帧，但 native helper 在 3 秒测试窗口的一半处采集主播放快照；因此正常报告约 `46` 个 rendered frames，对应约 `1.5s` 的 30fps 播放窗口。`1` 帧阈值无法发现严重欠帧或渲染链路近似失效的问题，作为 stable/challenge A/V 裁判过弱。`40` 帧保留少量调度余量，同时能阻止明显欠帧候选被误判为 pass。

影响：后续播放 core/native 候选如果让 A/V smoke 主播放窗口严重欠帧，会在同一 manifest 下暴露为失败或回归。该决策不修改播放 core，不声称 cadence、A/V sync 或颜色质量改善，只提高评测器对 A/V smoke 的最低样本量要求。

边界：该阈值绑定当前 native helper 的“半窗口主播放快照”设计；如果后续 helper 改为全窗口采样、移除中途 seek，或样本帧率/时长变化，阈值必须随 manifest 语义一起重新评估，不能静默沿用。

## 2026-07-10: audio-ahead final delta 作为证据字段保留，不作为评分阈值

决策：保留 `timing.audioAheadWaitFinalDeltaAbsMsP50/P95/P99/Max` 作为 native/Core/app quality report 的诊断证据，并让 repeat stability summary 与 candidate comparison 透传 `audioAheadWaitFinalDeltaAbsP95/P99SpreadMs`。该字段只解释 audio-ahead wait episode 结束时的残余 A/V delta 绝对值，不新增通过/失败阈值。`PlaybackQualityRunComparator` 在 baseline/candidate 都带有 finalDeltaAbs 证据时，要求 oversleep 的 improvement/regression 必须由 finalDeltaAbs 同方向显著变化佐证；否则 oversleep 只作为 matched evidence 保留。

原因：此前 audio-ahead wait cap / early-wake / tolerance 候选都容易把单次 frame tail 或 oversleep 波动误判为策略效果。新 repeat 证据显示当前 A/V smoke 的 finalDeltaAbs P95/P99 spread 为 `0ms`，而 instability 来自 frame P99/max 和 oversleep P95/P99 spread。因此模型应优先区分“最终对齐残差稳定”和“等待/渲染尾部不稳定”，而不是继续盲调 audio wait 常量。

影响：后续比较报告可以直接携带 finalDeltaAbs stability 证据，模型不需要逐个打开 native report 才能判断 audio-ahead wait 结束点是否稳定。若未来 finalDeltaAbs spread 变大，应调查 audio clock sampling、wait loop 退出条件和 render scheduling；若 finalDeltaAbs 稳定而 frame tail 不稳定，应避免把问题归咎于最终 A/V drift。

边界：该决策不宣称播放质量改善，不放宽 stable case 标准，不把 finalDeltaAbs 当作单独 pass/fail 门槛。它只避免把未被最终残差佐证的 oversleep 波动当成候选胜负依据。

## 2026-07-10: 不采纳 audio-ahead wait cap / early-wake 调参候选

决策：不保留 5ms audio-ahead wait cap、2ms early-wake 或 1ms early-wake 候选。当前 accepted Core 行为继续使用 `b6307e2` 的 audio-ahead wait 计算；本轮所有候选源码已撤回，只保留 ignored/private report artifacts 作为后续模型避免重复尝试的证据。

原因：当前主线样本口径已经变为 24-case，不能继续用旧 54-case artifact 做严格候选判定。基于 `b6307e2` 24-case baseline 对比后，5ms cap 直接回归 A/V smoke，`audioAheadWaitCount` 从 `53` 增到 `243`。2ms early-wake 为 `split-candidate`：video-only HDR10-60 单次 improved 但无法归因，A/V smoke 的 instability 转移到 audio oversleep P95/P99。1ms early-wake 仍为 `split-candidate`，A/V smoke 同时出现 P99/max gap 和 oversleep P95/P99 回归。重复采样进一步显示 baseline 与 candidate 都存在 A/V smoke instability，但 candidate 没有消除目标风险，只改变了不稳定信号形态。

影响：后续不要继续通过“缩短单次 audio wait”或“提前 1-2ms 醒来”这类参数调参尝试修复 A/V smoke。下一步应先补 per-wait 级证据，或调查 render scheduling / audio clock gating 的结构问题。候选比较必须继续使用当前 manifest 口径生成同源 baseline/candidate report-set，并在 native tail case 上附带 repeat stability。

边界：该决策不修改 evaluator 阈值、不删除 case、不把 A/V smoke 或 HDR10-60 quarantine，也不声称当前 Core 已改善。它只拒绝本轮三个策略候选，并把当前 24-case baseline 作为后续调优的新比较起点。

## 2026-07-10: render outlier evidence 保留为诊断增强，不作为播放质量优化采纳

决策：保留 `timing.renderIntervalSampleCount`、`timing.renderIntervalOverExpected2MsCount`、`timing.renderIntervalOverExpected4MsCount` 这组 render interval outlier 证据，并保留 `Compare-PlaybackCoreTuningCandidate.ps1` 的 `cadenceStability.attribution` 输出。但 `0c40e63` 不标记为 accepted playback-quality improvement；它只是 evidence/schema 与模型消费能力增强。

原因：`0c40e63` 相对 `c129249` 的 54-case comparison 为 `reject-candidate`，唯一 regression 是 `local/native-headless-av-smoke` 的 audio-ahead oversleep P95/P99 上升。扩展 repeat summary 后，target case 在 baseline 和 candidate repeat 中都属于 unstable，但 baseline 只暴露 P99 oversleep 尾部波动，candidate 同时暴露 P95 oversleep、P99 oversleep、frame P99 和 max-frame-gap instability。因此不能把本轮拒绝解释为纯 HDR10-60 已知噪声，也不能因为改动主要是 telemetry 就忽略 gate。

影响：后续模型可以直接从 comparison summary 读取 target case 是否命中 baseline/candidate unstable group，并看到 oversleep/drift spread 是否参与 instability，不必手工交叉查询 repeat artifact。该归因只解释证据可信度，不覆盖 `evaluation.decision`，不放宽阈值，不删除 case，也不把 unstable case 自动 quarantine。

边界：render outlier 计数必须在 `Snapshot()` 阶段从已记录 histogram 派生，不能在每帧采样热路径里做额外阈值计算。后续 Core/native 策略调优仍必须生成同 manifest baseline/candidate comparison，并在被 single-run native tail spike 阻断时附带 repeat stability evidence。

## 2026-07-10: 不采纳 20ms audio-ahead tolerance

决策：不保留 `e8cef30` 的 20ms audio-ahead tolerance 策略；该提交已通过 `12bea45` 回退。当前 audio-ahead gating 继续使用原有 10ms tolerance，video-clock tolerance 也保持 10ms。

原因：working comparison 曾显示目标 A/V case 的 P99/max expected-error 改善，但 commit-bound 54-case comparison 结论为 `reject-candidate`。`local/native-headless-av-smoke` 出现真实目标回归：render P95/P99/max 从 `40.433/46.2755/46.2755ms` 升到 `45.3174/52.3268/52.3268ms`，`audioAheadWaitCount` 从 `67` 升到 `75`，drift P95 从 `10ms` 升到 `20ms`。`local/native-headless-hdr10-60` 也触发 frame-pacing regression，虽可能受已知 60fps 短样本波动影响，但 A/V case 已足够拒绝该候选。

影响：后续不要把“放宽 audio-ahead tolerance 到 20ms”当作已验证优化，也不要用 working comparison 覆盖 commit-bound rejection。继续优化 A/V smoke 时，应优先做重复采样、长样本或更细的 per-wait clock delta evidence，再提出更稳定的 scheduling 策略。

边界：该拒绝不改变 evaluator 阈值、不删除 case、不否定 `d687248` decode-mode evidence baseline。它只说明简单放宽 audio-ahead tolerance 不是当前可采纳策略。

## 2026-07-09: native decode-mode 必须作为实际 runtime evidence 暴露

决策：native playback metrics 增加 `hardwareDecodedVideoFrames` 与 `softwareDecodedVideoFrames`，由 `PlaybackGraph` 在成功取得 decoded frame 后根据 frame 是否携带 D3D texture 计数。该证据通过 native-headless helper、Core report、analyzer、signal catalog、comparison matched signals 和 App WinRT quality metrics bridge 暴露；App WinRT bridge 同步补齐 `audioAheadWaitCount` 与 `videoClockWaitCount`。

原因：合并 VS2026 / FFmpeg 8.1.2 后，当前基线的 native-headless A/V smoke 与 HDR10-60 出现尾部 frame pacing stability 风险。没有 decode-mode 证据时，模型无法区分问题是否来自软件解码 fallback、硬解路径下的 render scheduling、clock gating 或采样噪声。decode-mode 计数可以把“是否软解”从猜测变成 report 中可消费的 runtime evidence。

影响：后续调优 report-set 可以直接看到硬解/软解帧数，不需要从文件名、manifest expected、codec 名称或人工观察推断。当前 smoke 抽样显示 `sdr-smoke` 与 `av-smoke` 都是硬解路径，因此这些 case 的尾部 gap 诊断应优先看 scheduling/cadence，而不是先假设软解性能不足。

边界：decode-mode 是诊断证据，不是新的 pass/fail 规则；本决策不改变解码策略、不切换硬解偏好、不放宽 comparison threshold、不修改样本 expected behavior，也不证明真实 Xbox/HDMI/HDR 输出正确。

## 2026-07-09: main 合并后以 905241d 作为新的播放 Core 调优基线

决策：`main` 的 VS2026 / .NET 10 / native `v145` / FFmpeg 8.1.2 构建链更新合入后，后续播放 Core 调优以 `905241d` 生成的 `playback-core-tuning-main-modern-54case-905241d.local` 作为当前工程基线。旧的 pre-merge 54-case baseline 继续保留为迁移诊断材料，但不再作为后续候选是否可采纳的主要比较对象。

原因：新旧基线 comparison 结果为 `reject-candidate`，阻断点是 `local/native-headless-av-smoke` 和 `local/native-headless-hdr10-60` 的 frame-pacing 回归；重复采样进一步显示新 `905241d` 基线在这两个 native-headless group 上存在 cadence stability 风险。这是合并主线后的基线状态，不是某个后续 Core 调优候选造成的回归。继续用旧 baseline 作为主判据会把工具链/依赖迁移风险混进后续策略调优结论。

影响：后续所有 wait scheduling、frame pacing、A/V sync、buffering、seek/timeline、track/subtitle 和 color/DXGI 调整，都必须先从 `905241d` baseline 生成同 manifest candidate comparison。若需要解释与合并前状态的差异，可以单独引用 old-vs-new migration comparison 和 cadence stability summary，但不能把旧 baseline 的 `reject-candidate` 结论当成新候选的直接拒绝原因。

边界：该决策不放宽评测阈值、不删除失败 case、不声称播放质量提升。`905241d` 只是新的工作起点；当前已知风险是 native-headless A/V smoke 与 HDR10-60 的尾部 frame pacing stability 弱于旧基线，下一步调优应优先定位并改善这两个目标。

## 2026-07-09: 播放 Core 门禁 native restore/build 必须使用现代 MSBuild

决策：`tools\quality-run\run-playback-core-checks.ps1` 的 native restore/build 阶段通过 `tools\NoiraModernToolchain.ps1` 解析 MSBuild，不再依赖 VS2022 `vcvars64.bat` 或 PATH 上的旧 `msbuild`。

原因：`main` 已切到 VS2026/MSBuild 18 和 native `v145` toolset。继续用 VS2022/MSBuild 17 会失败在 `MSB8020: 找不到 v145`，这不是播放 Core 问题，而是门禁脚本还停在旧构建链。

影响：干净 worktree 可以用文档化命令运行完整播放 Core gate，native restore/build 与 App 现代构建入口保持一致。脚本测试会阻止该阶段回退到 VS2022 工具链。

边界：这是构建门禁修复，不改变播放器行为、FFmpeg 版本、评测规则或样本预期。

## 2026-07-09: 不采纳 precise audio tail wait 候选

决策：不保留本轮 precise audio tail wait 候选。该候选给 `RenderLoopWaiter` 增加 timer + `yield` 尾段等待，并仅接入 audio-ahead gating；comparison 结果为 `reject-candidate`，因此当前 Core/native 主线保持原有 `RenderLoopWaiter.WaitFor` 行为。

原因：同一 54-case manifest 对比显示，候选虽然让部分 video-only native-headless case 的 frame pacing expected-error 改善，但目标含音轨 A/V case `local/native-headless-av-smoke` 出现强回归：`framePacing.renderIntervalP99ExpectedErrorMs` 与 `framePacing.maxFrameGapExpectedErrorMs` 均增加 `2.1038ms`。当前目标优先级是含音轨 A/V sync 与 frame pacing，不能用 video-only 改善换取 A/V case 尾部 gap 回退。

影响：不要继续沿“尾段 spin/yield”方向做细粒度调参，除非先有重复采样或更长样本证明当前回归是采样噪声。后续 wait scheduling 候选仍必须以同 manifest baseline/candidate comparison 为准，并同时检查 oversleep、render P95/P99/max gap、A/V drift、buffering、track/subtitle 和 process-cost evidence。

边界：这是一次被拒绝的小步策略实验，不改变评测阈值、样本预期或 accepted Core 行为。ignored artifact 保留在 `playback-core-tuning-precise-audio-tail-54case-working.local`，用于后续模型避免重复尝试同一路线。

## 2026-07-09: cadence 重复采样结果作为候选解释证据，不直接改写 comparison 规则

决策：新增 `Measure-PlaybackCadenceStability.ps1` 作为 repeated report 聚合工具。它按 case group 汇总 `framePacing.renderIntervalP95ExpectedErrorMs`、`framePacing.renderIntervalP99ExpectedErrorMs` 和 `framePacing.maxFrameGapExpectedErrorMs` 的 spread，并标记 `stable`、`unstable` 或 `insufficient-samples`。该工具先作为诊断证据使用，不直接覆盖 `evaluate-candidate` 的 suite gate。

原因：当前 native-headless 3 秒短样本存在可重复观察到的单次尾部波动。直接把重复采样工具接成自动放行逻辑，会变相降低 existing comparison gate；但完全没有稳定性归因，又会让模型把无关 candidate 的随机 P99/max spike 当成 Core regression。独立 summary 能让模型看到“候选本身的对比结果”和“该 case 当前采样稳定性”两层证据。

影响：后续 wait scheduling / frame pacing Core 候选仍必须生成同 manifest baseline/candidate comparison；如果 comparison 结果被少数 native-headless cadence case 阻断，应附带 cadence stability summary 解释它是稳定回退、混合结果还是采样不稳定。是否调整 comparator 的 gate，需要单独 TDD 和同一历史 artifact 回放验证，不能静默修改阈值。

补充：`Compare-PlaybackCoreTuningCandidate.ps1` 可以通过显式参数附带 baseline/candidate cadence stability summary。comparison summary 中的 `cadenceStability` 只提供解释证据；`evaluation.decision`、`evaluation.action`、improved/regressed/mixed counts 仍来自原始 suite gate。

边界：重复采样 summary 不是新的 pass 规则，也不是删除或 quarantine case 的依据。它只解决“模型需要更完备信息识别问题”的消费层缺口。

## 2026-07-09: 不采纳 positive-wait clamp，先处理 60fps cadence 单次采样不稳定性

决策：不保留 `92e82e0` 的 audio/video positive wait clamp，也不保留 `dc2bf33` 的 audio-only positive wait clamp。当前 Core/native 主线回到 positive-wait clamp 之前的行为；后续调度候选必须再次从 accepted baseline 生成同 manifest baseline/candidate comparison。

原因：两版候选都无法通过 commit-bound 54-case gate。`92e82e0` 被无音轨 native-headless cadence case 拒绝；`dc2bf33` 虽然 working comparison 为 `keep-candidate`，但 commit-bound comparison 因 `local/native-headless-hdr10-60` 的 `framePacing.renderIntervalP99ExpectedErrorMs` 和 `maxFrameGapExpectedErrorMs` 回退而被拒绝。由于 gate 结论是 reject，不能把候选留作 accepted Core 优化。

补充证据：对当前 helper 重复采样显示，60fps native-headless 短样本的 P99/max gap 有明显单次波动。`hdr10-60` 五次 P99 为 `23.0687ms`、`21.0002ms`、`24.2723ms`、`21.7227ms`、`21.0617ms`；`sdr-60` 五次 P99 为 `26.4203ms`、`20.0507ms`、`21.6333ms`、`21.2512ms`、`21.1417ms`。在当前 `2ms` materiality 下，这种波动会让无关 candidate 随机触发 frame-pacing regression。

影响：不要为了通过当前候选而放宽阈值、删除 case 或降低 expected behavior。下一步应先为 native-headless cadence 建立重复采样/稳定性归因机制，再继续尝试 wait scheduling 或 frame pacing Core 调整。

边界：这不是否定 `framePacing.*ExpectedErrorMs` 规则；它说明单次 3 秒 native-headless 60fps 样本不足以单独作为强拒绝证据。`761800c` video-clock target wait 仍是此前已接受的 Core 候选。

## 2026-07-09: audio-ahead oversleep comparison 以 P95 为决策门禁

决策：`PlaybackQualityRunComparator` 对 `timing.audioAheadWaitOversleepMsP95` 增加 lower-is-better 判定；baseline/candidate 都有正数证据且差异达到 `2ms` 时，oversleep 降低记为 `frame-pacing` improvement，oversleep 升高记为 regression。`timing.audioAheadWaitOversleepMsP99` 不单独作为硬门禁，只在 `P95` 已经达到 material threshold 且方向一致时作为补充 delta 输出。

原因：当前 native-headless A/V smoke 是短样本，`P99` 很容易接近 max，单独一次尾部抖动不应直接否决整个 candidate。P95 更适合作为 v0.1 自动裁判的可操作门禁；P99 仍作为 matched signal 和同方向补充 delta 暴露给模型，避免完全丢失尾部风险。

影响：未提交的 precise-tail wait 实验会被新规则识别为 regression，因为它让 `audioAheadWaitOversleepMsP95` 从 `7.9336ms` 升到 `10.07ms`，并同时拉高 P99。已接受的 54-case video-clock target wait candidate 中，`local/native-headless-av-smoke` 只有 P99 从 `7.7653ms` 升到 `10.787ms`，P95 只从 `7.5337ms` 升到 `7.9336ms`，因此保持 `unchanged`，suite 仍为 `keep-candidate / accept-candidate`。

边界：该规则不改变播放行为、不放宽样本 expected behavior，也不把 oversleep P95 的任何小幅波动解释为质量变化。后续如果引入更长样本或重复采样，可以再把 P99/max 从诊断信号升级为更严格门禁。

## 2026-07-09: frame pacing comparison 按 expected-duration error 判定改善

决策：baseline/candidate 都有 `timing.expectedFrameDurationMs` 和 runtime frame pacing telemetry 时，comparison 增加三个派生信号：`framePacing.renderIntervalP95ExpectedErrorMs`、`framePacing.renderIntervalP99ExpectedErrorMs`、`framePacing.maxFrameGapExpectedErrorMs`。这些信号用 `abs(actual - expectedFrameDurationMs)` 表示离目标帧时长的误差；误差降低是 improvement，误差升高是 regression。

原因：frame pacing 不是 lower-is-better。23.976/24fps 内容的理想 render interval 约 41.7ms，60fps 内容约 16.7ms；过慢和过快都可能是问题。上一轮 video-clock target wait 把多个 native-headless case 从 47-48ms 拉回 42ms 左右，但旧 evaluator 只把这些 runtime telemetry 当作 matched diagnostic signals，无法自动判定候选是否可采纳。

影响：`playback-core-tuning-video-clock-expected-error-54case-5ef58d6.local` 对同一 54-case baseline/candidate reports 重新比较后，suite 从 `no-change` 变为 `keep-candidate / accept-candidate`，8 improved、0 regressed、0 mixed。per-case comparison 会暴露 expected-error derived signals，模型不需要再从 P95/P99 原始值手工推断。

边界：该规则只在 baseline/candidate expected frame duration 一致且 telemetry 为正时生效；误差变化小于 `2ms` 视为短样本波动，不产生 improvement/regression。它不降低 stable case 标准，不改变样本 expected behavior，也不证明真实 Xbox/HDMI/display refresh 输出正确。

## 2026-07-09: video-clock pacing 使用目标剩余时间等待

决策：无音轨路径中的 software video-clock wait 不再使用默认 5ms render loop sleep。`PlaybackFramePacing` 新增 `VideoClockWaitDuration`，按 `framePosition - clockStartPosition - elapsed - VideoAheadTolerance` 计算剩余等待时间；`PlaybackGraph::ShouldWaitForVideoClock` 在需要等待时把该 duration 写入 `m_nextRenderLoopWait`，并使用现有 `RenderLoopWaiter`。

原因：54-case baseline 中无音轨 native-headless cadence case 的 `videoClockWaitCount` 为主要等待原因，23.976/24fps 样本的 render P95/P99 约 48ms，30fps HDR 样本约 47ms，明显像固定 5ms 轮询造成的 oversleep。audio-ahead 分支已经使用 target duration + high-resolution waiter；video-clock 分支继续固定 sleep 会让低帧率和 60fps 无音轨样本产生不必要的 frame pacing jitter。

影响：commit-bound comparison `playback-core-tuning-video-clock-wait-54case-761800c.local` 中，8 个无音轨 native-headless cadence/color case 的 render P95/P99/max gap 全部向 expected frame duration 收敛；suite 仍为 `decision = no-change`，因为当前 evaluator 不自动把 runtime frame pacing telemetry 判为 improvement/regression。该策略作为低风险 native pacing 修正保留。

边界：不改变 `VideoAheadToleranceTicks`、drop tolerance、audio-ahead gating、A/V sync 规则、样本 expected behavior 或 evaluator 阈值。A/V smoke 基本持平，不能据此宣称音画同步改善；该结论也不代表真实 Xbox/HDMI/display refresh 已验证。

## 2026-07-09: main/Noira/FFmpeg 8.1.2 后播放 Core 门禁必须以当前 main 为默认 diff base

决策：`tools\quality-run\run-playback-core-checks.ps1` 的默认 `AppDiffBase` 改为 `origin/main`，不再使用旧的 pre-Noira 固定提交 `94adec5`。脚本计划测试会阻止默认 base 回退到该旧提交。

原因：项目已改名为 Noira / NoiraPlayer，并且 FFmpeg 8.1.2 升级已经合入 main。继续用旧提交作为默认 base，会让干净 main worktree 被 App diff guard 误判为包含大量 App 改名/资源变化，导致文档化默认命令无法运行。

影响：从 main 新建的 playback-core worktree 可以直接运行 `run-playback-core-checks.ps1`。分支上的 App 改动仍会通过 `origin/main...HEAD` 被 guard 检出；允许列表仍只限 DEBUG quality-run/native metrics 接线文件。

边界：这是评测门禁基线修复，不放宽 App-free 边界，不允许 UI/XAML/project/package 改动进入播放 Core 调优阶段。

## 2026-07-09: native restore 必须先于 native-headless smoke

决策：`run-playback-core-checks.ps1` 中 `native-restore` 提前到 `native-headless-harness-smoke-test` 和后续 native compile checks 之前执行。

原因：新 worktree 不会自动带上 ignored 的 `src\NoiraPlayer.Native\packages` 目录。FFmpeg 8.1.2 native-headless smoke 需要从 NuGet package 目录复制 DLL 并链接 import libs；如果 restore 排在 smoke 之后，干净 worktree 会失败在缺 package 目录，而不是失败在真正的播放 Core 问题。

影响：完整门禁现在能在干净 main worktree 中先 restore `FFmpegInteropX.UWP.FFmpeg.8.1.2`，再运行 native-headless helper、native tests 和 native Debug x64 build。

边界：这不改变播放器行为、FFmpeg 版本、评测阈值或样本预期；只是修正门禁前置依赖顺序。

## 2026-07-09: force-SDR 和显式零渲染帧必须作为可消费 evidence

决策：reference case 的 `forceSdrOutput` 必须从 manifest/reference case 传入 `PlaybackQualityReportRequest` 并写入 `report.colorPipeline.forceSdrOutput`；native-headless CLI 新增 `--force-sdr-output`，smoke 覆盖该入口。`PlaybackQualityReportAnalyzer` 只有在 `timing.renderedVideoFrames` signal 未出现时，才把零渲染帧归为 missing evidence；如果 collector 明确上报了 `0`，它应作为失败/样本不足证据保留。

原因：force-SDR 是颜色管线评测的重要 expected/runtime 证据，不能只存在于 manifest 或 plan 里。零渲染帧同样不是天然“缺证据”：例如 seek/timeline 后 native helper 明确上报 rendered frames 为 0 时，模型需要看到这是播放输出失败或样本不足，而不是被 missing-evidence blocker 掩盖。

影响：54-case candidate 中 public、native-headless 和 private Emby force-SDR case 都能输出 `colorPipeline.forceSdrOutput` signal。显式 zero-render report 不再产生 `missingEvidence: timing.renderedVideoFrames`，但仍会保留 `sample.insufficient` 等真实失败信号。

边界：这仍是评测 evidence 语义修正，不是 HDR tone mapping、SDR conversion、frame pacing 或 seek 行为优化。播放器能力是否提升必须继续用同一 manifest baseline/candidate 对比证明。

## 2026-07-09: UI 开发样本使用私有真实 manifest，不再维护 mock fixture route

决策：废弃 `*-fixture`、`details-real-sample` 和 `details-real-bright-sample` route，移除 App/Core 中的 mock fixture 数据链路。UI 开发需要稳定跳转真实页面时，使用 ignored 的 `docs/qa/private/ui-real-samples.local.json` 维护真实 Emby 样本，再通过 `tools/Write-AppUiSampleCommand.ps1` 写入 `LocalState\dev-command.json`。

原因：fixture route 不能反映真实 Emby 数据、真实 artwork、真实 source/audio/subtitle 密度和真实页面状态，继续维护会让 UI 开发在错误数据上收敛。`details-real-*` 自动挑样也把“从服务器选择哪部片”写进代码路径，且容易诱导依赖标题、亮度采样或本机状态；更合适的边界是由本地私有 manifest 明确列出样本。

影响：App active code 只保留真实 route：`home`、`movies`、`tv`、`search`、`details`、`photo`、`playback`、`quality-run` 等。`details`、`photo`、`playback` 必须由 dev-command 提供真实 `itemId`；`quality-run` 必须提供 `itemId` 或 `streamUrl`。仓库可提交模板和脚本测试，但不得提交真实 `itemId`、`mediaSourceId`、私服 URL、账号或私有截图。

边界：该决策只治理 UI 开发数据源，不改变播放 core/native 策略，不替代 playback-quality report-set，也不证明 UI 交互已经完成。若需要可重复视觉/交互测试，应先把真实样本规范化到本地 manifest，而不是恢复 mock fixture。

当前权威说明：`docs/qa/ui-development-data-sources.md`。历史文档中的 fixture route 表述只保留为历史证据，不得作为新开发入口。

## 2026-07-08: App 开发期使用 XAML Hot Reload 与 loose file deploy

决策：Noira UWP App 的 Debug 构建显式设置 `<DisableXbfLineInfo>False</DisableXbfLineInfo>` 和 `<UseDotNetNativeToolchain>false</UseDotNetNativeToolchain>`，以保留 Visual Studio XAML Hot Reload 所需信息。新增 `tools/Register-NoiraLooseApp.ps1` 作为本机 loose file deploy 入口：默认 clean/build 后从 `bin\<Platform>\<Configuration>\AppxManifest.xml` 注册 loose layout，`-ValidateOnly` 用于脚本和布局验证。

原因：当前 App/XAML 迭代成本过高，每次小改都完整打包会拖慢交互和视觉修复。Microsoft 支持 UWP/Xbox 的 loose file registration，但它是开发期快速验证手段，不应替代最终包验证。

影响：开发者可用 Visual Studio F5 做 Hot Reload，用 loose registration 快速启动本机 Debug layout；远程 Xbox 可通过网络共享配合 Device Portal 或 `WinAppDeployCmd registerfiles` 注册 loose layout。脚本默认 clean 输出目录，降低改名后旧二进制残留导致误判的概率。

边界：该决策不改变播放器 core/native 行为、不改变播放质量评测规则、不证明真实 Xbox/HDR/HDMI 输出正确。最终验证、分发和质量结论仍以正常 MSIX 包、真机检查和 playback-quality report-set 为准。

## 2026-07-08: 文档入口和历史记录边界集中到 docs/README.md

决策：新增 `docs/README.md` 作为文档入口，集中说明当前权威文档、冻结评测结果、历史 plan/log 和 latest-wins 规则。历史 plan、handoff、smoke log 和 QA run log 可以保留旧项目名、旧路径和旧命令；当前执行前必须优先核对 `docs/README.md` 指向的活文档和现有代码路径。

原因：项目连续经历 UI、Xbox 实机、播放 core 评测、Noira 改名等多条并行工作流，文档中同时存在当前事实、历史执行计划、长 QA 记录和冻结 report-set。继续把所有文档视为同等权威会导致模型误读旧命令或改动不应改动的评测结果。

影响：`docs/qa/baselines/` 在当前整理阶段保持冻结；`docs/plans/` 与 `docs/superpowers/plans/` 默认作为历史输入；新长期事实写入 `docs/STATUS.md`，新技术取舍写入 `docs/DECISIONS.md`，新评测规则写入 `docs/EVAL_PHILOSOPHY.md` 或 metric contract。

边界：这只是文档治理变更，不改变播放 core、评测规则、样本 expected behavior 或 baseline/candidate 结果。历史记录中的旧名称不代表当前命令失效记录被删除；需要复盘时仍可从原文和 git history 追溯。

## 2026-07-08: 项目改名后环境变量前缀同步为 NOIRAPLAYER

决策：播放质量工具链和私有 Emby manifest 生成脚本使用 `NOIRAPLAYER_*` 环境变量前缀，包括 `NOIRAPLAYER_PLAYBACK_QUALITY_COLLECTOR_VERSION`、`NOIRAPLAYER_PLAYER_CORE_VERSION`、`NOIRAPLAYER_SOURCE_REVISION`、`NOIRAPLAYER_BUILD_CONFIGURATION`、`NOIRAPLAYER_QA_SERVER_URL`、`NOIRAPLAYER_QA_USERNAME`、`NOIRAPLAYER_QA_PASSWORD` 和 `NOIRAPLAYER_VCRUNTIME140_APP_PATH`。

原因：代码、项目、包名和用户品牌已经从旧项目名切换到 Noira / NoiraPlayer。继续暴露旧环境变量前缀会让后续自动化、文档和模型目标产生歧义，也会增加私有配置维护成本。

影响：旧 `NEXTGENEMBY_*` 变量不再作为当前文档化接口。需要本地运行私有 Emby manifest 或 playback-quality 工具时，应使用新的 `NOIRAPLAYER_*` 名称。

边界：变量含义没有改变；本轮只改名，不改变私有数据读取、report environment 字段优先级、baseline/candidate 判定逻辑或 QA 样本规范。

## 2026-07-08: wait reason counter 必须拆分并保留零值证据

决策：`timing.videoAheadWaitCount` 继续作为历史兼容的总等待计数，新增 `timing.audioAheadWaitCount` 和 `timing.videoClockWaitCount`。native `PlaybackGraph` 在 `ShouldWaitForAudio` 分支累加 audio-ahead wait，在 `ShouldWaitForVideoClock` 分支累加 video-clock wait；Core report、analyzer、signal catalog、required-signal policy、headless parser 与 comparison matched signals 均消费这两个拆分字段。

原因：前一轮 near-threshold audio catch-up wait 实验被拒绝后，现有 `videoAheadWaitCount` 只能说明“视频帧被延后渲染”，无法区分等待来自音频时钟追赶、软件 video clock pacing，还是两者混合。继续在这个总计数上调 sleep/tolerance 会让模型把不同根因混在一起判断。

影响：当 report 出现 `videoAheadWaitCount > 0` 时，`audioAheadWaitCount` 和 `videoClockWaitCount` 即使为 0 也会作为 evidence signal 暴露。`0` 在这里不是缺失值，而是“该等待原因未发生”的负证据。`local/native-headless-av-smoke` 当前显示 `videoAheadWaitCount = 71`、`audioAheadWaitCount = 71`、`videoClockWaitCount = 0`，因此该样本的等待更应优先分析 audio-clock gating，而不是 video-clock pacing。

边界：这是 instrumentation/testability 变更，不改变播放行为、评测阈值、样本预期或候选采纳规则。41-case comparison 结果为 `decision = no-change`，不应被解释为播放质量改善。当前拆分首先覆盖 native-headless/Core report 路径；如需 App-hosted WinRT quality-run 同等信号，后续应单独扩展对应 bridge。

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

决策：`tools/NoiraPlayer.PlaybackQuality.Headless` 保留默认 skip/blocker 模式，同时新增 `--native-helper-exe`。当传入 native helper exe 时，C# harness 负责调用 helper、解析 key=value metrics、组合 `PlaybackDescriptor`、`PlaybackQualityLifecycle`、`PlaybackQualityPosition` 和 `native-headless:returned-snapshot` metrics provider，再输出标准 `PlaybackQualityRunResult`。`run-native-headless-harness-smoke-test.ps1` 负责在本机编译 helper、补齐 FFmpegInteropX UWP DLL 与 `vcruntime140_app.dll`，并用本地生成的声明样本跑完整 captured import / validate / analyze 链路；默认 skip/blocker 路径仍保留公开 Jellyfin direct-uri 作为命令契约输入。

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

决策：新增 `tools/NoiraPlayer.PlaybackQuality.Headless` 作为 App-free captured report producer。当前版本只生成标准 `PlaybackQualityRunResult` skip envelope，skip code 为 `native-headless.native-link-blocked`，并通过 `materialize-native-harness-report-set` 的 captured import 路径进入现有 report-set 链路。该 provider 当前不得写入 `native-headless:returned-snapshot`。

原因：现有 `NoiraPlayer.Native` 是 Windows Store C++/WinRT dynamic library，IDL 公开入口包含 `AttachSurface(Windows.UI.Xaml.Controls.SwapChainPanel)`，播放入口通过 WinRT/UWP projection 暴露。虽然 `DxDeviceResources` 在没有 swapchain 时多处会返回 false 而不是直接崩溃，`PlaybackGraph` 公开复用仍受 WinRT runtimeclass、UWP component activation、FFmpeg UWP linkage 和 surface host 边界限制。直接用外部 ffmpeg 或只拼 JSON 会让模型误以为已经获得真实 native playback evidence。

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

决策：`tools\quality-run\run-playback-core-checks.ps1` 的 App diff guard 继续保护 `src/NoiraPlayer.App`，但新增精确 allowlist。最初只允许 `src/NoiraPlayer.App/Playback/WinRtNativePlaybackEngine.cs`；随后为了接通 DEBUG App-hosted `quality-run` collector，allowlist 扩展到 `PlaybackLaunchRequest.cs`、`MainPage.xaml.cs` 和 `PlaybackPage.xaml.cs`。Plan 输出会暴露 `appDiffGuard.allowedPaths`，测试会确认 allowlist 没有包含 XAML、App project、manifest/package 或未列入 App 文件。

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

- `tools/NoiraPlayer.PlaybackQuality.Headless` 在 helper 进程退出后读取 `Stopwatch.Elapsed` 和 `Process.TotalProcessorTime`，计算 `cpu / wall-clock`。
- process-cost 信号只在数值大于 0 时进入 `modelAnalysis.evidenceSignals`。
- comparator 只有在 baseline 和 candidate 都有 process-cost 证据时才把这些信号加入 matched signals，避免把旧 schema baseline 的缺失字段误解为改善或退化。
- 该变更不改变播放器行为、播放策略、evaluator 阈值、case expected behavior 或 pass/fail 规则。

当前 `b292019` 与 `38ae764` 的 41-case comparison 结果为 `decision = no-change`。由于 `38ae764` baseline 没有 process-cost 字段，本轮不能给出 CPU 成本前后结论；下一轮以 `b292019` 之后的 report-set 为 baseline 时，才可以比较 process-cost 是否发生变化。

# 2026-07-08: audio prewake margin 候选不进入主线

决策：不采纳 `aa2eddc` 的 audio-ahead prewake margin 策略。该策略在 `AudioAheadWaitDuration` 中为大于 2ms 的等待提前 2ms 唤醒重新采样，试图降低 high-resolution waitable timer 的 oversleep。

原因：虽然 41-case comparison 中没有 suite-level regression，且 `local/native-headless-av-smoke` 的 oversleep P95 和 process CPU ratio 有改善，但 commit-bound 指标没有稳定改善目标 frame pacing：render P95/P99 从 `40.179/40.7123ms` 变为 `40.771/41.1449ms`，audio-ahead wait P95/P99 也从 `31.3399/31.3715ms` 变为 `33.2936/34.6695ms`。同时 `videoAheadWaitCount` 从 `71` 增加到 `91`。这属于 mixed diagnostic/no-change，不足以作为播放策略主线改动。

影响：保留该实验的 candidate/comparison 产物作为反证，不保留代码行为。后续 wait/scheduling 调优不能仅靠固定 prewake margin 继续试参；需要更稳定的重复采样、长样本证据，或更明确地同时改善 render interval、oversleep、A/V drift 和 process cost 的策略。

边界：这不是降低 stable eval 标准，也不是删除测试；相反，本轮因为 commit-bound 同 manifest comparison 不支持采纳而回退了策略代码。

# 2026-07-09: video-only native cadence 样本使用 5 秒，A/V smoke 保持 3 秒

决策：native-headless harness 中的 video-only SDR/HDR cadence 样本统一使用 5 秒；含音频/字幕的 `local/native-headless-av-smoke` 继续使用 3 秒，并通过脚本测试固定这两个显式时长。

原因：3 秒 video-only 60fps 样本对 P95/P99 尾部分位数过敏，容易把短样本采样噪音误判为 cadence 不稳定。5 秒样本提高了 HDR10-60/SDR60 这类 cadence-only case 的统计稳定性。A/V smoke 的主要问题来自 audio-clock gating / wait scheduling，其不稳定性不应通过延长所有样本或放宽阈值被掩盖。

影响：这是评测 harness 可靠性调整，不改变播放器行为、stable case expected behavior、candidate acceptance 规则或 evaluator 阈值。后续基线如果包含 native-headless cadence case，应把样本时长变化视为 eval baseline migration，而不是 Core 调优收益。

决策补充：`cda19d2` 生成的 54-case report-set 是 5 秒 cadence 样本迁移后的新调优基线。旧 3 秒 cadence baseline 与新 5 秒 cadence report-set 的 comparison 即使输出 `reject-candidate`，也只能作为迁移审计结果，不能用来判断播放器 Core 策略退化。后续策略候选必须优先和 `playback-core-tuning-native-cadence-5s-54case-cda19d2.local` 做同 manifest 比较。

# 2026-07-09: default render-loop wait 使用 RenderLoopWaiter

决策：默认 render-loop wait 也使用 `RenderLoopWaiter`，不再只让 audio-ahead/video-clock 等待走 high-resolution waitable timer。`PlaybackFramePacing::ShouldUseRenderLoopTimer` 作为统一策略入口，当前对正数 delay 返回 true。

原因：60fps video-only native-headless case 的 P95/P99 frame interval error 形态符合默认 `sleep_for(5ms)` 在 Windows 上被粗粒度唤醒放大的特征。把默认 wait 纳入同一个低开销 timer primitive 后，同一 54-case manifest 比较显示 HDR10-60 和 SDR60 的 frame pacing error 明显下降，且无 suite-level regression。

边界：该策略不针对某个 case、codec 或 HDR/SDR 分支；它是 render loop 等待 primitive 的统一化。A/V smoke 只证明 audio-ahead oversleep 改善，不能据此声称整体 A/V sync 或主观流畅度已解决。后续如果发现 CPU 成本或真实设备调度问题，应以同 manifest comparison 和 process-cost evidence 重新评估。

重复采样补充：`c129249` 保留为 accepted candidate，但稳定性结论需保守表述。3 次 native-headless repeat 中 8/9 group stable，`local/native-headless-hdr10-60` 仍因一次 max-frame-gap outlier 被判 unstable；P95/P99 cadence 已稳定。因此后续候选应优先区分 percentile cadence 与 rare max-gap outlier，避免因为单次 max-gap 尾部值误判整个 cadence 策略。
