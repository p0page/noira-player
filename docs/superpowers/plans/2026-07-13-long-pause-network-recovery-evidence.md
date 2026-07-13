# 长暂停网络恢复评测实施计划

1. 为 helper/collector 的暂停后帧增量、实际暂停时长和恢复延迟增加失败测试。
2. 为故障服务器增加 pause marker 握手，并以 source contract 锁定事件顺序。
3. 实现 helper 结构化字段和 collector 严格解析、错误归因。
4. 将原有短重连 stable 与新增 30 秒 challenge 拆成独立 case。
5. 运行真实 native 故障服务器、同 manifest materialize/validate/analysis。
6. 更新状态与决策，执行 Core/native gate 和 Modern App Publish 后提交。
