# 会话内 Seek Replay Cache 设计

## 目标

在不牺牲准确 seek 的前提下，消除短距离回退时重复执行远端 `av_seek_frame` 的数秒阻塞。当前同源三轮 v0.2 baseline 的 seek recovery 为 `4858.98-6476.15ms`，落点误差均为 `22ms`；本设计只处理已经在当前会话中读取过的数据，不处理冷 resume。

## 行为边界

- 只缓存当前已激活音频、视频和字幕流的压缩包，不缓存解码帧。
- 总缓存上限 `48 MiB`、时间窗口上限 `12s`、包数上限 `32768`。
- 命中必须找到目标之前的视频关键帧，并证明所有已激活轨道从重放起点到当前 demux 位置连续覆盖。
- 命中时把克隆包前置到现有 per-stream queue，flush decoder 并保留现有目标前丢帧逻辑；底层 demux 保持在原来的前向位置，重放结束后继续读取。
- 任一轨道覆盖不足、缺少视频关键帧、目标不在窗口内或缓存关闭时，原样执行 `av_seek_frame(..., AVSEEK_FLAG_BACKWARD)`。
- cache miss、分配失败或不支持的包不得改变 seek 正确性；只允许回退原路径，不允许伪命中。
- 不增加持久缓存、服务端 remux、转码或冷启动优化。

## 结构

新增 `FfmpegSeekReplayCache`，独立拥有 `AVPacket` 克隆和全局读取序号。`FfmpegMediaSource` 在 `av_read_frame` 成功后观察激活流包；`PlaybackGraph::Seek` 先请求 cache replay，只有 miss 才调用现有 demux seek。缓存按视频关键帧边界裁剪，避免保留无法独立解码的半段 GOP。

命中后每条流的 replay queue 都按原读取顺序生成，并插入该流现有 pending queue 之前。重放包不会再次写入 history，避免循环增长；底层 demux 新读到的包仍按现有逻辑排入尚未追上的其他轨道队尾。

## 证据契约

evaluation version 升级为 `playback-quality-v0.3`。timeline report 必须新增：

- `position.seekPacketCacheEnabled`
- `position.seekPacketCacheHit`
- `position.seekPacketCachePacketCount`
- `position.seekPacketCacheBytes`
- `position.seekPacketCacheWindowDurationTicks`
- `position.seekFallbackReason`

这些字段必须来自 native seek snapshot，不从耗时、日志或 expected 推断。baseline/candidate comparator 把 enabled/hit/fallback 作为上下文信号，把 operation/recovery 作为 lower-is-better 结果信号。

## 版本与评测

1. instrumentation revision 包含完整缓存实现和 v0.3 证据，但默认关闭；用同一私有 manifest 生成三轮 baseline。
2. candidate revision 只把默认开关改为开启；用同一 manifest 生成三轮 candidate。
3. 采纳要求三轮 candidate cache hit，seek recovery 低于 `2000ms`，落点误差不高于 `500ms`，post-seek 持续推进；同时不得回退 A/V sync、buffering、轨道字幕、颜色或普通 playback。
4. 任一 cache miss 必须显示 fallback reason，并保持原准确 seek 行为；不得删除失败轮或放宽阈值。
5. 关键结论最后通过完整 App 构建和 App-hosted timeline capture 复核。

## 参考原则

mpv 的 seekable demux cache 以缓存时间范围、视频关键帧和 selected stream reader head 决定是否在缓存内 seek，并在缓存耗尽后恢复到原 demux range。本项目只提炼该原则，不复制 mpv 的多 range、磁盘缓存和后台 demux 线程架构。Kodi/VLC 的准确 seek 语义继续作为 fallback：向后定位关键帧并丢弃目标前帧。
