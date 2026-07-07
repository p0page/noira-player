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

## v0.1 裁判边界

v0.1 可以验证媒体源解析、播放生命周期、seek/resume、轨道发现、字幕选择状态、缓冲、frame timing、A/V sync、颜色元数据和错误处理的软件证据。它不验证最终显示设备画质，也不以总分替代结构化失败诊断。
