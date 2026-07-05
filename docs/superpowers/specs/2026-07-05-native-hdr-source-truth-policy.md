# Native HDR/DV 真实来源策略

日期：2026-07-05

状态：已接受。后续任何涉及 Xbox 原生播放、HDR 输出切换、Dolby Vision fallback、媒体源选择、播放页版本切换的任务，都必须遵守本策略。

## 核心原则

HDR/DV 决策不能依赖文件名、媒体源名称、展示标题、人工拼接的 label 或任何给人看的字符串。

这些字符串只允许用于 UI 展示和诊断，不允许用于：

- 自动选择或自动切换媒体源；
- 禁用某个媒体源；
- 判断是否进入 Xbox HDR10 输出模式；
- 判断 Dolby Vision profile；
- 判断是否拒绝播放当前源。

## 可信输入

可信输入必须来自解复用、解码或服务端提供的结构化媒体元数据：

- FFmpeg `AVStream` / `AVCodecParameters` 的 `color_trc`、`color_primaries`、`color_space`、`color_range`；
- FFmpeg stream side-data，例如 `av_stream_get_side_data(..., AV_PKT_DATA_DOVI_CONF, ...)`；
- FFmpeg packet side-data，例如 `av_packet_get_side_data(..., AV_PKT_DATA_DOVI_CONF, ...)`；
- FFmpeg frame side-data，例如 HDR10 mastering metadata、content light metadata、DOVI metadata；
- Emby `MediaStreams` 中的结构化字段，例如 `VideoRange`、`ColorTransfer`、`ColorPrimaries`、`ColorSpace`、`Codec`。

Emby 结构化字段可以作为 UI 诊断和打开前的弱提示，但不能覆盖 native 实际解析结果，不能直接触发 Xbox HDR 输出切换。

## Kodi 对照

Kodi 的 Windows/Xbox 路径不是按文件名判断 HDR。

关键链路是：

- `DVDDemuxFFmpeg::DetermineHdrType()` 从 FFmpeg coded side-data 和 color transfer 判断 `StreamHdrType`；
- Dolby Vision 使用 `AV_PKT_DATA_DOVI_CONF` 保存 `AVDOVIDecoderConfigurationRecord`；
- `DVDVideoCodecFFmpeg` 把 codec context 或 demux hints 落到每帧 `VideoPicture` 的色彩字段；
- `CRendererBase::Configure()` 会用首个 `VideoPicture` 的 `color_primaries` 与 `color_transfer` 做自动 HDR 显示切换预判，这里的 `picture` 仍然是 demux/decoder 产生的结构化视频信息，不是文件名或媒体源 label；
- `CRendererBase::ProcessHDR()` 在后续 render buffer 上继续按 BT.2020 + PQ/HLG/SDR 做 HDR10、HLG 或 SDR 输出切换与 metadata 更新；
- Win10/Xbox windowing 根据 renderer 的 HDR 输出状态调用 `HdmiDisplayInformation.RequestSetCurrentDisplayModeAsync(..., HdmiDisplayHdrOption::Eotf2084)`；
- DXGI swapchain 再设置 `SetHDRMetaData()` 和 `SetColorSpace1(DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020)`。

因此，“hint”不是文件名 hint，而是 demux/decoder 解析出的结构化流信息。

本地 Kodi 对照文件：

- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp`
- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10.cpp`

## 当前项目策略

媒体源策略：

- 不自动换源；
- 用户选中的源必须先尝试打开；
- 如果 native 实际解析证明当前源不支持，例如 Dolby Vision Profile 5 且没有 HDR10/HLG fallback，则当前源播放失败并给出明确错误；
- 不因为另一个源看起来更安全就静默切过去。

Core 层：

- `HdrPlaybackProfileClassifier` 不读取 `MediaSource.Name` 或 `MediaStream.DisplayTitle` 做 HDR/DV 判定；
- `HdrPlaybackProfile` 在 UI 中只能作为提示和诊断；
- `IsDirectPlayable` 不能再用于禁用 UI 或阻止 orchestrator 启动播放；
- `NativePlaybackOpenRequest` 不再包含 `IsHdr`，C# 层没有 API 可以向 native 传入 HDR 启动 hint。

App/WinRT 桥接层：

- `WinRtNativePlaybackEngine` 不再向 native request 设置 `IsHdr`；
- 诊断日志不再记录上层 `isHdr` 启动参数；
- 打开请求只传播放源、起播位置、音轨、字幕和帧率。

Native 层：

- `VideoDecoder` 优先解析 FFmpeg stream side-data 的 `AV_PKT_DATA_DOVI_CONF`；
- 如果 stream side-data 不存在，继续解析 packet side-data 的 `AV_PKT_DATA_DOVI_CONF`；
- 解析到 DV 配置后必须记录 profile、level、RPU、EL、BL、compatibility id；
- Profile 5 且 compatibility id 为 0 时，不允许静默按 HDR10 或 SDR 播放，应明确失败；
- `PlaybackGraph` 在第一帧解码后，基于 `DecodedVideoFrame.HdrKind` 和 10-bit swapchain 能力决定是否请求 HDR 输出；
- 第一帧无论 HDR/SDR 都必须做一次显示输出决策，防止上一个播放源留下 HDR 输出状态；
- 后续只有 HDR/SDR desired 状态变化时才再次请求显示输出切换；
- `VideoRenderer` 只有在显示器 HDR 输出已经 active 且帧为 HDR10/HLG 时才设置 HDR10/PQ swapchain color space；
- HLG 按 Kodi 方案输出到 HDR10/PQ：使用 BT.2100 推荐参考 HDR10 metadata，并走 HLG -> PQ shader 转换。

## 后续目标

下一阶段仍需要继续接近 Kodi：

- 补齐更完整的输入 DXGI color space 验证；
- 对 HDR10 -> SDR tone mapping 做更多实片验证；
- 对 DV Profile 8 fallback 做更细的 native 侧确认；
- 对 DV Profile 5 保持明确拒绝，除非后续实现可靠 tone mapping 或平台解码支持。
