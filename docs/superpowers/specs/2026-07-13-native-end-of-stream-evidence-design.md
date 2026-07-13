# Native 自然播放结束证据设计

## 目标

把自然播放结束从 manifest 中的声明变成可复现的 native 播放证据。`end-of-stream` case 必须真实打开媒体、解码并排空音视频输出，只有 `PlaybackGraph` 自然进入 `Stopped` 且状态消息为 `Playback ended.` 时，报告才允许包含 `lifecycle.endOfStream`。

## 方案选择

采用独立 `end-of-stream` 执行场景。相比在普通 `playback` 场景中用固定等待推断结束，它能让一次 attempt 只验证一种主动意图；相比根据 duration、最终 position 或进程退出合成 EOF，它保留了 decoder drain、audio queue drain 和 graph 状态转换的真实因果链。

不采用以下方案：

- 普通播放后按固定时长读取状态：容易把尚未排空或卡死误判为结束。
- 根据 duration 与 position 接近度生成 EOF：这是 expected/派生数据，不是实际生命周期证据。

## 数据流

1. manifest 为 case 声明唯一 `executionRequirement.scenario = "end-of-stream"`，purpose 包含 `end-of-stream`。
2. runner 原样把场景传给 native helper，不在 PowerShell 层补造结果。
3. helper 监听 `PlaybackGraph` 状态回调。该场景下不执行固定采样后主动停止，而是在有界超时内等待自然 `Stopped / Playback ended.` 或 `Failed`。
4. helper 在自然结束后、清理 graph 前冻结质量快照，并输出 `endOfStreamAttempted`、`endOfStreamObserved`、`endOfStreamStatus`、`endOfStreamPositionTicks`。
5. headless collector 严格解析这些字段。只有 attempted、observed、completed 三者一致时才写入 `lifecycle.endOfStream`；否则生成诚实的失败或缺失证据。
6. report-set validator 继续以 manifest scenario、report scenario 和 required signal 三方一致为门禁。

## 错误与边界

- helper 收到 `Failed`、超时或没有自然结束消息时，场景失败且不得写入 EOS lifecycle。
- helper 字段缺失、非法或互相矛盾时，collector 必须拒绝报告，不能回退到 duration/position 推断。
- `graph.Stop()` 只用于场景结束后的资源清理，不计作自然 EOF。
- 本阶段不处理长暂停和结构化 source-open failure；它们是后续独立场景，避免一次 attempt 混入多个归因目标。

## 验证

- Core 单元测试覆盖场景枚举、manifest 意图匹配、report scenario 匹配和 required signal。
- headless parser 测试覆盖完整、缺失、矛盾 EOS 字段。
- native helper source contract 锁定自然状态回调与禁止固定时长合成 EOS。
- 本地 5 秒生成媒体作为 stable native case，必须真实执行到 EOS 并产出报告。
- 完整 Core gate 和 Modern App x64 Native AOT Publish 必须通过；App 编译只证明集成无回归，不替代 native EOS 证据。
