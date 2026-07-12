# App-hosted 交互证据桥接设计

## 背景

当前 native-headless 能记录音轨/字幕切换的 operation、recovery、分阶段耗时和 packet cache 证据，但完整 App-hosted `quality-run` 只记录 lifecycle 文本。真实 App 音轨切换已经成功，报告中的 `interaction` 却为空，严格契约因此正确判为证据不足。App-hosted 复核不能退回解析文本或只检查最终轨道。

## 方案

保持现有 WinRT `IAsyncAction` 切流 API 不变。`NativePlaybackEngine` 保存 `PlaybackGraph` 最近一次切流返回的 typed timing，并通过 `NativePlaybackQualityMetrics` 只读快照暴露。Core `PlaybackQualityMetricsSnapshot` 与 App `WinRtNativePlaybackEngine` 原样映射这些字段。

App 场景执行使用单调时钟记录：

- 调用开始到切流 API 返回的 operation duration；
- 调用开始到位置和音频/视频恢复的 recovery duration；
- 字幕调用开始到真实 cue render 增长的 cue duration；
- 操作前后的 position、submitted audio、rendered video 和 subtitle cue delta。

native 快照提供 lock wait、execution、quiesce、seek、decoder/renderer open、cache enabled/hit/count/bytes/window。App 将两类观测组合为一个 `PlaybackQualityInteractionEvidence`，直接交给现有 report composer。不得从 lifecycle message 或日志反向解析字段。

## 一致性与失败处理

- 每次 open/stop 清空最近交互，避免上一媒体污染下一 case。
- 音轨与字幕快照携带明确 scenario；App 只接受与当前 manifest scenario 相同的最新快照。
- API 抛错、轨道未选中、位置/帧/cue 未推进时仍保留已观测 timing，但 lifecycle 为 failed，不能生成 completed 假证据。
- cache miss、seek 回退和非零 phase 必须原样报告；App 不负责推断或改写 native 策略。
- 评测阈值、expected、required-signal policy 和 2000ms SLO 不变。

## 验收

1. 保留本轮真实 RED：App 播放成功但 `interaction` 为空时，严格评测失败。
2. native 与 C# 映射测试证明所有 phase/cache 字段不丢失，open/stop 后不会复用旧交互。
3. App 场景策略测试证明 audio/subtitle 分别产生正确 delta、duration 和 scenario；缺少必要进展时失败。
4. 用同一私有 manifest 完整 Publish、注册并运行 audio-switch 与 PGS subtitle-switch。
5. 两份 App-hosted 报告必须有真实 playback sample、匹配 source/scenario、完整 typed interaction；materialize 后 strict validate 为 2/2 matched。
6. 完整 Core gate 与完整 App build 通过；私有凭据和报告继续只存在 ignored/local 路径。

## 边界

本设计只补齐 App-hosted 软件证据，不声称证明 Xbox/HDMI 输出，也不处理冷启动网络超时。startup 超限继续作为独立失败保留，不得通过放宽交互 case 阈值消除。
