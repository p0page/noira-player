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

## v0.1 裁判边界

v0.1 可以验证媒体源解析、播放生命周期、seek/resume、轨道发现、字幕选择状态、缓冲、frame timing、A/V sync、颜色元数据和错误处理的软件证据。它不验证最终显示设备画质，也不以总分替代结构化失败诊断。
# 2026-07-07 补充原则

1. `unsupported` 是有效评测结果，不是 pass 的变体。对当前 MVP 明确不支持的源，报告应保留 source 分类证据，并避免要求不会发生的播放、解码、色彩转换或显示输出 telemetry。
2. probe telemetry 必须显式标注来源。`core-probe` 可以证明 player core orchestration 和 report-set 链路闭合，但不能证明真实媒体播放质量。
3. 当报告消费对象是模型时，`missingEvidence` 必须指向下一步真实可行动的证据缺口。不要把 unsupported source 误导成 color-pipeline、frame-pacing 或 A/V sync 缺证据。
4. deterministic probe 数据可以用于验证评测系统本身，但不能用于声称播放器颜色、帧率、缓冲或同步能力提升。
