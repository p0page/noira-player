# 评测原则

评测体系的第一职责是诚实暴露事实，不是让当前播放器显得更好。

## 原则

1. 先收集证据，再优化播放器 core。
2. 稳定 case 的标准不能为了通过而降低。
3. 缺失 telemetry 是评测证据问题，默认归类为 `insufficient instrumentation`。
4. 当前 MVP 不支持的能力应标记为 `unsupported`，不能伪装成 pass。
5. 样本、环境或预期不可靠时，case 先进入 `quarantine`。
6. JSON 报告必须优先服务模型消费，失败要能定位到 case、signal、failure area、failure class 和下一步调查方向。
7. 纯软件评测不能声称证明真实显示设备输出、HDMI InfoFrame、面板 EOTF 或肉眼颜色准确性。
8. 播放行为优化和评测规则修改应拆开记录；如果必须同轮发生，需要明确说明边界和前后证据。
9. source color、DXGI color space 和 conversion status 必须来自采集器实际观察到的字段；不得从 manifest expected、文件名、case id 或预分类结果倒填 actual evidence。
10. 无音轨样本不能评价 A/V sync；它最多证明视频链路或 frame timing，不得输出同步良好的结论。
11. track/subtitle evidence 必须来自实际 stream discovery 或明确的播放器状态；不得用单条 fallback video stream 掩盖音轨、字幕轨或选择状态缺失。
12. 正式 baseline/candidate 的每个 stable/challenge case 必须真实进入 native 或 App-hosted 播放链路并产生一一对应报告。source-only、core-probe、expected 物化和未执行 skip 只允许作为 evaluator self-test，strict playback gate 必须拒绝它们。

## v0.1 裁判边界

v0.1 可以验证媒体源解析、播放生命周期、seek/resume、轨道发现、字幕选择状态、缓冲、frame timing、A/V sync、颜色元数据和错误处理的软件证据。它不验证最终显示设备画质，也不以总分替代结构化失败诊断。
# 2026-07-07 补充原则

1. `unsupported` 是有效评测结果，不是 pass 的变体。对当前 MVP 明确不支持的源，报告应保留 source 分类证据，并避免要求不会发生的播放、解码、色彩转换或显示输出 telemetry。
2. probe telemetry 必须显式标注来源。`core-probe` 只用于 evaluator self-test，可以证明 orchestration 和报告分析规则可运行，但不能进入正式 baseline 或证明真实媒体播放质量。
3. 当报告消费对象是模型时，`missingEvidence` 必须指向下一步真实可行动的证据缺口。不要把 unsupported source 误导成 color-pipeline、frame-pacing 或 A/V sync 缺证据。
4. deterministic probe 数据可以用于验证评测系统本身，但不能用于声称播放器颜色、帧率、缓冲或同步能力提升。

# 2026-07-08 补充原则

12. `display.refreshRateHz` 在 headless/native 软件闭环中只能表示“播放器或评测器选择的显示刷新率策略快照”，不能表示 HDMI、系统显示模式或电视面板已经真实切换。报告必须同时给出 limitation，让模型明确哪些结论可以软件闭环，哪些结论需要外部硬件验证。

13. HDR/SDR、frame rate、source color 和 DXGI mapping 的 actual evidence 必须来自实际解析或 runtime observation。manifest expected、文件名、case id、预分类结果只允许表达预期，不允许倒填为 actual。

14. frame cadence 不只检查“是否过慢”，也必须检查“是否过快”。低帧率内容如果以 render loop 或显示刷新节奏过快输出，应被视为 frame-pacing 问题。frame-ratio 派生信号应按接近可接受区间解释，不能简单当作 lower-is-better。

15. 进入调优目标的能力证据不能只停留在 report-set coverage 中；如果目标要求 baseline/candidate 对比某类证据，comparison artifact 必须暴露对应 matched / unmatched / regression 信号，便于模型判断候选是否意外改变了非目标能力。

16. `position.seekPositionErrorMs` 只能表示 seek landing 相对目标位置的误差，不能混入 seek 后继续播放产生的自然进度。若需要评价 seek 后恢复播放的稳定性，应使用独立 timing/buffering/sync 信号，而不是复用 seek landing error。

17. baseline/candidate comparison 应暴露目标能力的 matched signals，即使这些信号当前没有阈值或质量方向。无阈值 runtime telemetry 进入 comparison 的目的首先是证明证据可比；除非有明确规则，否则不要因为短样本数值波动自动判定 improvement 或 regression。

18. 等待链路诊断应区分策略目标和实际唤醒过冲。`audioAheadWaitTargetMs*` 与 `audioAheadWaitOversleepMs*` 可以帮助定位 wait scheduling 问题，但在没有明确阈值和同 manifest 前后对比之前，不应自动解释为 improvement、regression 或 A/V sync 正确性结论。

19. baseline/candidate 只有在执行证据完整、证据等级与 runner 相同、匿名 source locator 相同，且已打开源的匿名摘要相同时才可比较。不可比报告必须输出 `insufficient-evidence`，即使 telemetry 数字看起来改善，也不得产生 improvement 或 regression。
## 单场景与原始证据

一个正式 case 只能代表一个可归因场景。普通播放、timeline/seek、音轨字幕交互、pause/resume 或网络恢复必须分别执行；不能把多个操作串在同一个 helper attempt 中，再用整体 pass/fail 掩盖具体失败来源。

结构化报告不能成为丢弃原始信息的理由。native helper 的 stdout/stderr 必须在脱敏后与报告一起归档，超时和解析失败也必须保留；如果原始输出缺失，评测器只能报告 instrumentation 不足，不能判断播放器通过或失败。
