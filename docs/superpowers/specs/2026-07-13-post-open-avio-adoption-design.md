# FFmpeg 打开后接管 AVIO 的候选设计

## 背景

v0.9 通过稳定外层 custom AVIO 和可替换内层 HTTP AVIO 恢复播放期断流，但真实私有 Emby 样本显示，双层 AVIO 从 `avformat_open_input` 之前就介入会放大远端启动波动。

新增的回调分账证明：`AVSEEK_SIZE` 不是瓶颈。同一“一战再战”Matroska 源中，open-input 的尺寸查询耗时低于 `0.001ms`，真正的等待来自一次约 `18.65GB` 的前向 data seek 和一次等量回向 data seek。FFmpeg 8.1.2 的 Matroska demuxer在 `matroska_execute_seekhead` 中读取 SeekHead 指向的非 Cues 元数据，再恢复原位置；Kodi 的 custom IO 也保留 FFmpeg seek 语义。

因此不采用以下伪优化：

- 删除 `AVSEEK_SIZE`；
- 把 HTTP 输入伪装为不可 seek；
- 跳过 Matroska SeekHead、附件、章节或标签；
- 放宽 startup 门限；
- 把准确 timeline/track 发现换成更快但不完整的结果。

## 候选方案

保留当前 v0.9 路径作为 baseline，增加默认关闭的“post-open adoption”候选：

1. `avformat_open_input` 使用 FFmpeg 内建 AVIO 完成协议打开和容器 `read_header`；
2. open-input 结束后，不关闭或重开现有 `formatContext->pb`；
3. `HttpMediaInput` 原子接管该 AVIO 作为内层输入，创建稳定外层 `avio_alloc_context`，并把外层 logical position 对齐到内层当前位置；
4. `avformat_find_stream_info`、startup seek、首帧和正常播放均经稳定外层执行；
5. 播放期 I/O error 仍只替换内层 HTTP AVIO，保持 demuxer、轨道、timeline 和外层 context 不变；
6. 任何接管失败都显式终止候选，不静默回退成另一条路径。

这个方案复用 FFmpeg 已建立的 HTTP 状态和缓冲，不实现新的 WinHTTP/Range 调度器，也不把 Kodi 的完整文件缓存系统搬进项目。

## 所有权与失败边界

- 接管必须是事务性的：外层分配成功前，`AVFormatContext` 继续拥有原 `pb`；成功后设置 `AVFMT_FLAG_CUSTOM_IO`，由 `HttpMediaInput` 关闭外层和被接管的内层。
- 外层 `pos` 必须从 `avio_tell(inner)` 初始化；不能从 0 开始伪造 timeline。
- open-input 阶段仍由 FFmpeg 内建 reconnect 负责；从接管完成起，v0.9 的 bounded reopen 负责播放期恢复。
- close、失败清理和重复 close 必须覆盖，不允许 double-close 或悬空 `formatContext->pb`。

## 评测要求

候选只允许由环境开关切换，使用同一构建、同一 manifest 和同一规则完成：

- 私有“一战再战”启动 baseline/candidate 各至少三轮；
- 确定性 demux 断流恢复 baseline/candidate 各三轮，故障必须真实触发；
- duration/timeline、seek、音轨、字幕、颜色和帧节奏不得回归；
- callback provider 必须按阶段诚实报告：open-input 为 builtin/unavailable，接管后的阶段为 instrumented/measured；在报告契约支持混合 provider 前，不得把单一 provider 冒充整个启动链路；
- 最终候选需通过完整 Core/native gate、完整 App Native AOT Publish 和代表性 App-hosted 播放。

若启动没有稳定改善，或断流恢复、轨道发现、timeline/seek 任一回归，则保留当前 v0.9 路径并拒绝候选。
