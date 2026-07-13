# 长暂停网络恢复评测设计

## 目标

把“播放一段时间、暂停较久、连接在暂停期间失效、恢复后继续解码和呈现”变成可复现的 native challenge case。该 case 用来暴露 `av_read_frame` I/O error、错误的恢复归因和恢复后无画面等问题，不以修改播放器策略或降低阈值为目标。

## 当前盲区

现有 `local/network-reconnect-pause-resume` 同时承担 buffering、短暂停和网络重连，暂停仅 1 秒。故障服务器在首次响应达到字节阈值后立即断开，因此重连可能发生在 pause 之前；报告也只记录累计帧数，不能证明 resume 之后真的继续呈现。

## 设计

1. 保留现有短重连 stable case，用于确认 FFmpeg Range reconnect 基础链路。
2. 新增独立 `challenge` case，使用 `pause-resume` 场景和至少 30 秒暂停。
3. helper 在实际调用 `graph.Pause()` 后原子创建测试标记文件；故障服务器只有观察到该标记后才重置首个连接，并在日志中记录顺序。
4. helper 输出暂停前后 decoded/rendered frame 计数、实际暂停毫秒数、恢复首个有效进展耗时、位置和 graph failure。collector 必须严格解析并拒绝缺失或矛盾证据。
5. 只有位置前进、decoded 与 rendered 均在 resume 后增加、未发生 graph failure，且暂停时长达到 manifest 请求值时，resume 才能标记 completed。
6. HTTP I/O failure 在 `pause-resume` 场景中归因到 `resume`，保留原始 FFmpeg 操作和错误文本；不得把网络故障静默改写成 player-core pass。

## 边界

- 该 case 证明软件层 native graph、FFmpeg HTTP reconnect 和生命周期恢复，不证明 Xbox、HDMI 或真实公网稳定性。
- 测试标记仅存在于 native helper 和本地故障服务器，不进入产品播放接口。
- challenge 失败可以进入报告，但不能阻止 stable corpus 自身成立；同一 manifest 的 baseline/candidate 仍必须保持相同故障脚本与暂停时长。

## 完成标准

- 故障服务器日志证明 `pause marker observed` 先于连接重置和第二次非零 Range 请求。
- materialized report 明确包含 30 秒暂停及 resume 后 decoded/rendered 增量和恢复延迟。
- 缺字段、累计帧冒充增量、暂停不足、resume 后无进展和 I/O failure 均有失败测试。
- Core/native gate 与完整 App 编译通过；不声称完成 App-hosted 或 Xbox 实机验证。
