# 显式时间线 Seek 契约设计

## 背景

现有 `timeline` 场景虽然真实进入 native 播放链路，但目标固定为 `startPositionTicks + 1 秒`。这只能证明一次很短的 seek 被调用，无法可靠发现进度条映射错误、远距离拖动无效或错误 duration 导致的定位偏差。

## 选择

采用 manifest 显式声明绝对逻辑时间 `seekTargetPositionTicks`：

- 不再由 runner 或 helper 隐式生成目标。
- timeline case 必须声明非负、与起播位置不同的目标。
- runner、headless harness、native helper 和最终 report 必须保持同一个目标值；任何一层不一致都使运行失败。
- 私有 Emby 生成器仅在媒体时长足以避开片头片尾时创建 timeline case，并根据真实 `RunTimeTicks` 生成可复现的远距离目标。
- native 仍使用绝对逻辑时间执行后向关键帧 seek，解码预滚后以首个实际呈现帧计算落点误差。

相对偏移会继续掩盖大跨度 seek 问题；运行时按百分比推导则会把错误 duration 反过来当成目标依据，因此不采用。

## 成熟实现参考

- Kodi 的播放器消息携带绝对时间，demuxer 以 `SeekTime` 定位并返回实际起点。
- VLC 在已知 duration 时优先把位置换算为时间，最终通过 `DEMUX_SET_TIME` 和 `av_seek_frame(..., AVSEEK_FLAG_BACKWARD)` 执行。
- mpv 将 UI 百分比先换算成绝对 PTS，再通过 absolute seek 和 high-resolution seek 丢弃目标前数据。

本项目只吸收共同原则：UI 百分比不是 Core 契约；Core 接收绝对逻辑时间，demux 回退到可解码点，最终以呈现帧而不是调用返回值证明 seek 生效。

## 证据与失败处理

timeline report 必须同时记录 requested start、显式 target、demux target、首个呈现位置、seek 后继续推进、操作耗时和恢复耗时。manifest 目标与实际执行目标不一致属于 harness bug；首帧误差超限或播放不再推进属于 player-core bug；网络竞争只污染耗时归因，不改变位置正确性结论。

第一阶段只补远距离 forward seek。backward seek 和多次连续 seek 后续用独立 case 增加，避免一个 case 混合多个主动意图。
