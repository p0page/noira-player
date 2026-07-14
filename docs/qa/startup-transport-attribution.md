# 启动传输归因

## 目的

启动超时可能来自播放器处理，也可能来自 HTTP read/seek 等外部等待。评测器必须向模型提供可复核的因果线索，避免模型把一次网络波动优化进 Core，同时不能用“网络慢”替播放器 I/O 策略免责。

## v0.18 规则

1. 仅统计 `transportCallEvidenceStatus=measured` 的启动组件。
2. 汇总这些组件的 read wait 与 seek wait，保留 provider、调用次数和 seek distance。
3. 启动未超过门限时输出 `within-threshold`，不作失败归因。
4. 启动超限且实测等待不小于超时量、同时占启动时长至少 25% 时，输出 `transport-wait-dominant`，失败分类为测试样本或环境问题。
5. 实测等待不足以解释超时时，输出 `startup-processing-dominant`，失败仍归播放器 Core。
6. 没有实测传输证据时输出 `transport-evidence-unavailable`，不得猜测环境归因。

## 模型消费

优先读取 `startup.transportAttribution`、`startup.transportAttributionReason`、`startup.transportWaitDurationMs`、`startup.transportWaitRatio` 和 `startup.transportMeasuredComponentCount`，再检查 `startup.components` 的逐阶段原始值。归因标签不是总分，也不能覆盖 report 的实际 pass/fail、生命周期或其他失败。

一次 `transport-wait-dominant` 只说明本轮启动超时不能可靠地用于修改 Core。若同一源的多轮报告持续出现相同阶段、provider、调用模式、seek distance 和等待成本，可建立单变量缓存、Range 或 seek 策略候选；候选仍必须与同一 manifest、相同评价版本和相同运行参数比较。

## 边界

- 软件评测无法证明 Xbox、HDMI 或显示设备行为。
- read/seek wait 包含远端服务器、网络和 Core 发起 I/O 的共同结果，不能单凭总等待判定责任方。
- 25% 是防止极小等待触发环境归因的保守门槛；不得为让某个 case 通过而静默修改。
- v0.17 与 v0.18 的启动失败语义不同，不可直接做 baseline/candidate 裁决。
