# Demux 读流错误恢复与证据设计

## 背景

真实 App 曾在暂停后恢复时终止，错误为 `av_read_frame failed: I/O error`。现有 v0.8 长暂停 case 只证明 FFmpeg HTTP protocol 在一次连接重置后能透明 reconnect；因为 `av_read_frame` 最终仍返回成功，报告无法回答 Core 是否见过错误、是否重试、是否恢复，也无法覆盖 FFmpeg 内建重试耗尽后的路径。

当前 `FfmpegMediaSource::TryReadPacket` 只区分 `AVERROR_EOF` 与其他负值：EOF 返回结束，其他负值立即抛出并终止 render loop。该行为还会把 `EAGAIN` 和 `EINTR` 当成致命错误。

## 参考实现与结论

- Kodi `f0232910490189b97717bc5d309aec2e5751d6d3`：`CDVDDemuxFFmpeg::ReadInternal` 将 `EAGAIN/EINTR` 转为非 EOF 的空包，其他读错才 flush。
- mpv `e5486b96d7d06dd148337899bfdc46bf25101663`：`demux_lavf_read_packet` 对非 EOF 读错最多连续重试 10 次，成功读包后清零；取消操作不重试。
- VLC `8e476d86cfbbb00833dfd9deb2f5324b074f3ca0`：`EAGAIN` 返回继续，其他读错结束 demux。
- FFmpeg HTTP reconnect 只覆盖协议层声明的条件和预算；预算耗尽会向 `av_read_frame` 返回错误。提高 reconnect 数字不能替代 Core 对返回值的有界策略与证据。

## 方案比较

### 方案 A：只增加 FFmpeg reconnect 预算

改动最小，但仍无法观察 Core 层错误，永久故障会拉长无证据等待，也不能修复 `EAGAIN/EINTR` 被误判。拒绝。

### 方案 B：在 media source 内做有界连续重试并贯通证据

保留当前同步 decoder API。EOF 永不重试；取消/关闭永不重试；`EAGAIN/EINTR` 视为暂时无数据；HTTP(S) 的其他非 EOF 读错最多连续重试 10 次，读到有效包后清零。每次错误、重试、恢复和最终失败都有结构化计数。采用。

### 方案 C：重构 demux、decoder、render loop 为三态异步管线

长期上更完整，但会同时改变 packet、decoder EOF 和 frame scheduling 边界，难以用当前真实故障做单变量验证。当前阶段不采用；若有界 media-source 策略仍无法覆盖实际服务端行为，再单独设计。

## Core 行为

1. `AVERROR_EOF` 仍是唯一自然结束信号，不计入读错。
2. 用户 stop/close 引发的 interrupt 不进入恢复预算，也不能报告为网络故障。
3. `EAGAIN/EINTR` 可重试；HTTP(S) 的其他非 EOF 错误也可在同一包读取中有界重试；本地文件的非瞬时 I/O 错误保持致命。
4. 最大连续重试数固定为 10，与本地 mpv 参考一致。有效 packet 到达后，连续错误计数清零并记一次 recovery。
5. 预算耗尽时抛出最后一次原始 FFmpeg 错误，不能改写成 EOF、卡住或伪造恢复。
6. 重试只解决 demux read 返回错误，不执行 close/reopen/seek，不改变 timeline、轨道、颜色或 decoder 状态。

## 结构化证据

native、WinRT、Core snapshot、headless parser、App-hosted mapper 和正式 report 贯通以下字段：

- `readErrorCount`
- `readRetryCount`
- `readRecoveryCount`
- `maxConsecutiveReadErrors`
- `lastReadErrorCode`
- `fatalReadErrorCode`
- `lastReadRecoveryDurationMs`

正式 report 使用独立 `readRecovery` 对象。manifest 使用 `expected.readRecovery` 明示是否要求恢复、最少错误/恢复次数和最大重试数；不得仅凭 case 名、purpose 或服务器日志推导通过。评测版本升级为 `playback-quality-v0.9`，旧版本不得与 v0.9 直接比较。

## 确定性场景

新增单一 `pause-resume` challenge：暂停 marker 出现后，故障 Range server 连续重置足够多的连接，使 FFmpeg 内建 reconnect 预算耗尽并至少向 Core 返回一次 `EIO`；下一次 Core 读重试允许建立 Range 请求并继续发送。case 只有在以下证据同时成立时通过：

- server 顺序为 pause marker、连续 reset、恢复 Range 请求；
- report 记录至少一次 read error、retry 和 recovery，且无 fatal error；
- resume 后 position、decoded frame、rendered frame 均推进；
- manifest、runtime source map、实际执行与 report 的 case identity 一致。

永久故障由 policy 单元测试和 helper 负向测试证明预算耗尽并保留原始错误；它不混入要求全绿的正式 corpus，也不冒充成功播放报告。

## 验证门禁

1. RED：策略测试、parser/mapper/evaluator/validator 测试及确定性 challenge 在实现前失败。
2. GREEN：同一故障参数下 candidate 恢复，且 report 满足 `expected.readRecovery`。
3. 现有透明 reconnect、长暂停、EOS、seek、轨道、字幕、颜色和 cadence case 不退化。
4. 运行完整 Core/native gate、全量 Core 测试、Native Debug x64 build 和 Modern UWP Debug x64 Native AOT Publish。
5. 私有 Emby 只做同一 manifest 的多轮观测；凭据和 token 仅经进程环境变量进入临时目录，不写入仓库。
