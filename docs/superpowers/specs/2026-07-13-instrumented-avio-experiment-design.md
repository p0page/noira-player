# 可观测 AVIO 实验路径设计

## 目标

为远端媒体输入建立一条默认关闭、可单独启用、可机器归因的 FFmpeg custom AVIO 实验路径。它首先回答 FFmpeg 启动阶段实际发生了多少次 read/seek、这些调用分别等待多久，以及固定网络往返是否是私有 Emby 启动超时的主要原因；在证据成立前不替换生产输入路径。

## 已有证据

v0.5 同一私有 manifest 的 6 个失败 case 全部只失败于 `startup.startupDurationMs`，其余播放、解码、渲染、A/V sync、轨道和色彩证据均完成。两个从 60 秒位置开始的 case 还分别执行了准确 backward seek 和约 18.4 MB 首帧预滚读取。

当前 FFmpeg 内建 HTTP 路径的 `avformat_open_input` 通常只读取 95-210 KB，却耗时约 4.8-5.2 秒。使用同一服务器、同一进程中的 `HttpClient` 进行不落盘的 Range 测量时，256 KB 冷请求首包约 1.9 秒，复用会话后约 0.53 秒，3 MB 总传输约 1.4-1.5 秒。这支持“连接/请求往返成本显著”的假设，但还不能证明 FFmpeg 内部具体发起了多少次请求。

Kodi、mpv 和 VLC 均在 FFmpeg demux 前保有自己的输入/stream 层，并通过 custom AVIO 把 read/seek 提供给 FFmpeg；它们没有依赖全局降低 probe 或改成不准确 seek 来解决该问题。

## 方案选择

### 采用：FFmpeg 内层 AVIO + 可观测转发层

仅在显式启用实验开关时，先用 `avio_open2` 按现有 URL、interrupt callback 和 HTTP reconnect options 打开内层 AVIO，再用 `avio_alloc_context` 创建外层 custom AVIO。外层 callback 只负责把 read/seek 转发给内层，同时记录调用次数、返回字节、等待时间、seek 距离和错误。

优点是依赖不变、UWP/原生 helper 都能沿用 FFmpeg 8，并能先验证 custom AVIO 边界和指标契约。代价是多一层缓冲可能改变读取粒度，因此这条路径是一个真实候选，不得伪装成“纯 instrumentation”。

### 暂不采用：直接引入 libcurl 或 WinRT HTTP 输入层

这更接近 Kodi/mpv 的终态，但会同时引入连接管理、Range、缓存、取消、重连和 UWP API 兼容问题，无法在一个变量下判断收益来源。只有可观测 AVIO 证明请求/seek 模式值得替换后，才另立设计。

### 拒绝：继续尝试 FFmpeg HTTP option 或弱化 probe/seek

`multiple_requests=1` 已在同一私有 Emby/反代链路产生 premature EOF。全局跳过 `avformat_find_stream_info` 会损害音轨、字幕和元数据发现；改成非 backward seek 或删除预滚会牺牲准确落点。以上都不作为本轮候选。

## 架构与数据流

扩展仓库中现有但目前只负责 URL 校验的 `HttpMediaInput`，使它成为聚焦于所有权和计数的 native 单元，负责：

1. 持有内层 `AVIOContext`、外层 `AVIOContext` 和 callback state；
2. 使用现有 HTTP options 打开内层输入；
3. 将 `read_packet`、`seek` 和 `AVSEEK_SIZE` 转发给内层；
4. 使用单调时钟累计 read/seek 调用次数、成功字节、等待时间、绝对 seek 距离和最后错误；
5. 在 close 时按确定顺序释放 format context、外层 buffer/context、内层 AVIO 和 state。

`FfmpegMediaSource` 保留现有默认 `avformat_open_input` 路径。只有 `NOIRAPLAYER_NATIVE_INSTRUMENTED_AVIO=1` 且输入为 HTTP/HTTPS 时启用实验路径；本地文件和默认 App 行为不变。实验路径若无法打开或执行，不允许静默回退到默认路径，因为回退会使 report 无法判断实际执行了哪条输入链路。

这些计数表示 FFmpeg demux 对外层 custom AVIO 发起的 callback 调用，以及 callback 在内层 AVIO 中等待的总时间；它们不等价于底层 HTTP request/Range 次数。没有自持 HTTP backend 或协议级 hook 时，报告不得把 `transportReadCalls` 命名或解释为 HTTP 请求数。

打开 format 后，现有 `avformat_find_stream_info`、准确 startup seek、解码和渲染逻辑完全复用。每个 startup component 记录 phase-local 的：

- `transportProvider`：`ffmpeg-builtin` 或 `instrumented-ffmpeg-avio`；
- `transportCallEvidenceStatus`：`unavailable` 或 `measured`；
- `transportReadCalls`、`transportSeekCalls`；
- `transportReadWaitMs`、`transportSeekWaitMs`；
- `transportSeekDistanceBytes`；
- 现有 `transportBytes` 和 `packetPayloadBytes`。

默认路径不能观测 callback 次数，其状态必须为 `unavailable`，不能用零冒充没有 read/seek。实验路径的字段必须完整存在，解析器缺任一字段即拒绝报告。评测契约升级为 `playback-quality-v0.6`。

## 错误与安全边界

- FFmpeg 负错误码原样向上传播并保留 operation；callback 不吞掉 EOF、EAGAIN、cancel 或网络错误。
- 计数器使用饱和差值；回退或溢出必须写入不含 URL 的 native diagnostic。
- 日志和报告禁止记录 URL、query、token、服务器、用户名、路径或响应 header。
- feature flag、provider 和 evidence status 必须进入 report，确保 baseline/candidate 可归因。
- 本轮不改变 7 秒门限、manifest expected、probe、seek、reconnect、缓存或解码策略。

## 验证与完成标准

1. 用本地可 seek 输入证明 custom AVIO read/seek/size 转发、所有权和错误传播正确；用 source contract 锁住 UWP 可编译边界。
2. 生成 v0.6 默认路径 baseline，所有 callback 计数明确为 `unavailable`，而不是零。
3. 用同一 manifest、同一构建只切换 feature flag 生成实验 candidate；所有 stable/challenge case 仍真实进入 native graph。
4. 比较启动阶段调用次数、等待时间、字节和最终播放结果；任何 EOF、轨道缺失、seek 误差或播放退化都判候选失败。
5. 只有多个代表性公开/私有 case 在相同规则下稳定改善，才考虑让 custom AVIO 成为后续 HTTP 输入层基础；本轮不得直接默认启用。
6. 完成 focused tests、完整 Core/native gate、Native AOT publish、完整 App build，并做一个代表性 App-hosted report。私有凭据和报告继续只存在 ignored 本地路径。
