# Demux 读取错误恢复设计与验证

## 目标

在 FFmpeg 内建 HTTP reconnect 已耗尽后，为可恢复的 demux 读取错误提供有界恢复；同时确保失败、重试、恢复和最终致命错误都进入正式 v0.9 报告。该策略不得改变 timeline、轨道、字幕、颜色或准确 seek 语义。

## 根因

真实 App 日志证明 `av_read_frame` 会在长暂停或网络中断后返回 I/O error。单纯在同一个 FFmpeg protocol context 上重复调用 `av_read_frame` 无法恢复：内建 reconnect 预算耗尽后，context 会保持终止状态。MOV/MP4 还可能把底层连接截断映射为 partial file 或 EOF，隐藏原始 I/O error。

## 实现

`FfmpegMediaSource` 对 HTTP(S) 使用稳定的外层 custom AVIO，外层由 demuxer 持有；`HttpMediaInput` 管理可替换的内层 FFmpeg HTTP AVIO。

1. 正常读取和 seek 由外层回调转发给内层 AVIO，并记录真实调用次数、等待时间和 seek distance。
2. 内层读取在已知文件大小之前返回 EOF 时，记录 pending transport error，禁止把截断冒充正常 EOF。
3. 读取策略判定错误可恢复后，在当前 byte offset 重新打开内层 HTTP AVIO。
4. 重开成功后重置外层 buffer、error 和 EOF 状态，并调用 `avformat_flush`；外层 context 和 demuxer 不重建。
5. 有效 packet 到达后结束本次错误 episode，累计一次 recovery；预算耗尽则保留原始 FFmpeg error 作为 fatal。
6. `NOIRAPLAYER_QA_DISABLE_DEMUX_READ_RECOVERY=1` 仅用于同 revision baseline，不改变 FFmpeg 内建 reconnect 或正式 expected。

## 评测合同

正式报告必须包含：

- `readErrorCount`
- `readRetryCount`
- `readRecoveryCount`
- `maxConsecutiveReadErrors`
- `lastReadErrorCode`
- `fatalReadErrorCode`
- `lastReadRecoveryDurationMs`

manifest runner 必须把完整 reference case 绑定到每次 headless 执行。原始 captured report 自身就必须拥有 `category`、`severity`、`stability`、`purpose` 和 `expected`，不能依赖后续 materialize 才补上裁判规则。runner summary 同时区分执行结果和报告结果，分别统计 pass、fail、error、skip、unsupported 和 unknown。

App-hosted quality-run 如果 native 启动进入 `Failed` 且没有 playback descriptor，必须导出包含原始失败消息的 error report；不得写成 `capture-skipped`。

## 确定性故障场景

`local/demux-read-error-recovery-after-pause` 使用本地 Range server：

1. 第一次请求发送固定前缀并等待 pause marker 后强制断开。
2. 后续两次请求在响应 body 前立即 reset，用于耗尽 FFmpeg 内建 reconnect。
3. Core 恢复重开后的下一次 Range 请求成功。
4. server 日志、helper stdout/stderr、raw report、materialized report、manifest 和 run metadata 一并归档。

## 同版本对比

归档目录：`artifacts/quality-run/demux-read-recovery-v0.9/`。

- baseline：`result=error`、`execution=failed`、errors=1、retries=0、recoveries=0、fatal=-5、rendered=55。
- candidate：`result=pass`、`execution=completed`、errors=1、retries=1、recoveries=1、fatal=0、rendered=148。
- comparator：case 为 `improved / keep-candidate`，改善信号为 `execution.outcome`。
- suite：保留 `partial-evidence` 和 `review-unmatched-signals`；单 case 不能支持全语料或播放器整体改善结论。

## 回归结果

- Core 测试：1047/1047 通过。
- 完整 native-headless：14/14 pass，14/14 completed，14/14 有真实播放 sample。
- 私有 Emby manifest：2/2 pass；一战再战为 SDR HEVC，哈姆奈特由 native 实际识别为 DV Profile 8 + HDR10 fallback。
- 完整 Debug x64 Native AOT Publish 成功。
- 新包 App-hosted 一战再战完成播放采样：rendered=256、A/V drift P95=13ms、无 starvation、无读错误；严格结果仍因 startup 11.865s 超过 10s 阈值而 fail，阈值未放宽。

## 未完成风险

- 当前 candidate 结论只覆盖确定性 demux 恢复场景；仍需用同 manifest 做重复采样，确认恢复时延和连接行为稳定。
- 私有 App-hosted 启动时间存在明显网络波动，应作为独立 startup 调优问题处理，不能与 demux 恢复混为一个候选。
- PC 软件证据不证明 HDMI HDR 输出正确；本阶段不据此声明 Xbox HDR 实机等价。
