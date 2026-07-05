# 原生依赖记录

日期：2026-07-05

## FFmpeg

用途：

- 解析 Emby direct-play HTTP/HTTPS 媒体流。
- 分离容器中的视频、音频、字幕流。
- 解码 HEVC Main / Main10，并在后续阶段接入 D3D11VA 硬件解码。
- 提取 HDR10 metadata，交给 DXGI swapchain 和视频渲染器。

当前先采用 NuGet 包 `FFmpegInteropX.FFmpegUWP`，版本固定为 `5.1.100`。这个包面向 Windows 10 UWP app，包含 FFmpeg 5.1.1 UWP 构建产物、头文件、lib、运行时 DLL 和 license 文件。NuGet 页面显示它兼容 `native` / `UAP 10.0`，包 license expression 为 `LGPL-2.1-or-later AND Zlib AND MIT`，最后更新时间为 2022-09-24。

来源：

- `FFmpegInteropX.FFmpegUWP` NuGet: <https://www.nuget.org/packages/FFmpegInteropX.FFmpegUWP/>
- `FFmpegInteropX.Desktop.FFmpeg` README / release notes: <https://www.nuget.org/packages/FFmpegInteropX.Desktop.FFmpeg/>
- FFmpeg 官方 MSVC 构建说明：<https://ffmpeg.org/platform.html>

已接入的库：

- `avformat`
- `avcodec`
- `avdevice`
- `avfilter`
- `avutil`
- `swresample`
- `swscale`
- Windows SDK `xaudio2.lib`

MSBuild 接入方式：

- `src/NextGenEmby.Native/packages.config` 固定 `FFmpegInteropX.FFmpegUWP` 版本。
- `src/NextGenEmby.Native/NextGenEmby.Native.vcxproj` 导入 `packages\FFmpegInteropX.FFmpegUWP.5.1.100\build\native\FFmpegInteropX.FFmpegUWP.targets`。
- 该 targets 会自动加入 `include` 路径、`runtimes\win10-$(PlatformTarget)\native` lib 路径、FFmpeg linker inputs，并把对应架构 DLL 加入 copy-local。
- Debug x64 构建已确认使用 `runtimes\win10-x64\native`，并把 `avcodec-59.dll`、`avdevice-59.dll`、`avfilter-8.dll`、`avformat-59.dll`、`avutil-57.dll`、`swresample-4.dll`、`swscale-6.dll` 复制到 native 输出目录。

仓库策略：

- 不提交 `src/NextGenEmby.Native/packages/` 下还原出来的 NuGet 二进制。
- 只提交 `packages.config`、`.vcxproj` 接入点、源码里的 FFmpeg probe 边界和依赖说明。
- 如果后续需要修改 FFmpeg 构建参数，再改为自建 UWP FFmpeg，并把构建脚本、校验和、源码版本和 configure 命令放进仓库。

当前状态：

- `FfmpegMediaSource` 已能调用 `avformat_open_input`、`avformat_find_stream_info` 和 `av_find_best_stream`，并集中管理 FFmpeg `AVFormatContext` 生命周期、seek 和按已注册流缓存 packet。
- `VideoDecoder` 已改为消费共享 `FfmpegMediaSource`，并负责 `avcodec_alloc_context3`、`avcodec_parameters_to_context` 和 `avcodec_open2` 建立视频 `AVCodecContext` 生命周期。
- `PlaybackGraph` 会把 native D3D11 device/context 传入 `VideoDecoder`；当 codec 声明支持 `AV_HWDEVICE_TYPE_D3D11VA` 时，`VideoDecoder` 会尝试创建 FFmpeg D3D11VA `AVHWDeviceContext` 并通过 `get_format` 选择硬件像素格式。
- `VideoDecoder::TryReadFrame()` 会通过 `FfmpegMediaSource` 读取视频 packet，并调用 `avcodec_send_packet` / `avcodec_receive_frame` 生成包含宽高、DXGI 像素格式、HDR transfer 类型和 position ticks 的 `DecodedVideoFrame` 元数据。
- `AudioDecoder` 已能从共享 `FfmpegMediaSource` 选择音频流、打开 FFmpeg audio decoder，并通过 `swresample` 统一转换为 48 kHz stereo float PCM；`DecodedAudioFrame` 会携带 sample rate、channel count、sample count、sample format、position ticks 和 PCM bytes。
- 当 FFmpeg 返回 `AV_PIX_FMT_D3D11` frame 时，`DecodedVideoFrame` 会携带 `ID3D11Texture2D` 和 texture array slice index；`VideoRenderer` 会在同格式 copy 失败后尝试用 D3D11 video processor blit 到 swapchain backbuffer。
- 当 FFmpeg frame 带有 `AV_FRAME_DATA_MASTERING_DISPLAY_METADATA` / `AV_FRAME_DATA_CONTENT_LIGHT_LEVEL` side-data 且 transfer 为 PQ 时，`VideoDecoder` 会映射为 `DXGI_HDR_METADATA_HDR10`。映射单位遵循 Microsoft 文档：色度坐标乘 50000，最大母版亮度为整 nits，最小母版亮度为 1/10000 nit，MaxCLL/MaxFALL 为 nits。
- `PlaybackGraph` 已有后台 render loop，会优先用 XAudio2 音频时钟决定视频帧节奏：视频帧早于音频超过容差时暂存等待，落后音频超过容差时丢弃有限数量的视频帧追赶。
- `PlaybackGraph` 的后台 loop 会在 EOF 时上报 `Stopped`，在 native 解码/渲染异常时上报 `Failed`；通知在 graph mutex 外触发，避免托管 wrapper 查询当前位置时形成死锁。
- UWP `HomePage` 已能加载 Emby 最新条目并导航到 `PlaybackPage`；`PlaybackPage` 会根据导航 itemId 拉取 PlaybackInfo、启动 `PlaybackOrchestrator`，并在开始、暂停、恢复、seek、停止和定时 tick 时调用 `EmbyApiClient.ReportProgressAsync`。
- `PlaybackPage` 的媒体源、音轨、字幕选择器会从 `PlaybackDescriptor` / `EmbyMediaSource.Streams` 填充，并把用户选择映射到 orchestrator 的切流/切音轨/切字幕方法。
- `NativePlaybackEngine` 已暴露原地音轨切换、字幕切换和禁用字幕方法；音轨切换会在当前位置重开 `AudioDecoder` / XAudio2 source voice，并通过 `FfmpegMediaSource::UnregisterStream` 释放旧音轨 packet queue；字幕切换会关闭旧 `SubtitleDecoder`、清掉旧 cue，并在目标流上重开文本字幕解码。
- `AudioRenderer` 已能创建 XAudio2 engine、mastering voice 和 source voice，并维护小型 PCM buffer queue；XAudio2 buffer-end callback 会释放已提交 buffer 的生命周期引用。
- `AudioRenderer` 会用首个提交 PCM buffer 的 `PositionTicks` 作为时钟基准，并通过 `IXAudio2SourceVoice::GetState().SamplesPlayed` 换算当前播放位置；`PlaybackGraph.CurrentPositionTicks()` 会优先返回该音频时钟，seek 时会 flush source buffers 并重置基准。
- 当前音频路径已经能把 FFmpeg audio frame 转成 PCM 并提交给 XAudio2；视频 render cadence 已有第一版音频时钟等待/丢帧策略，但阈值、画面观感和听音效果还没有实机验证。
- `SubtitleDecoder` 已能打开 FFmpeg subtitle stream，使用 `avcodec_decode_subtitle2` 解码已排队字幕 packet，并把 `SUBTITLE_TEXT` / `SUBTITLE_ASS` 规范化成文本 cue；为了不让字幕链路单独向前扫 demux，当前只消费 `FfmpegMediaSource::TryReadQueuedPacket` 暴露的已排队 packet。
- `SubtitleRenderer` 已持有 `DxDeviceResources` 并能把文本 cue 绘制到 swapchain backbuffer；`DxDeviceResources` 通过 Direct2D/DirectWrite 画白字和阴影，播放图会在视频帧和字幕都写入 backbuffer 后统一 `Present()`。
- 当前字幕路径还没有 PGS/bitmap 图形字幕、完整 ASS 样式渲染、外置字幕 URL 下载或字幕 offset；ASS 目前只做基础字段剥离、换行和 override tag 去除。
- `VideoDecoder` 会在失败路径和 `Close()` 中释放 FFmpeg context；`PlaybackGraph.Open` 失败时会回滚已打开的边界状态。
- 当前还没有实机验证 video processor 对 FFmpeg D3D11VA frame 的呈现效果，也还没有补齐 BT.2020/PQ video processor 色彩空间设置和 tone mapping 策略。
- 下一步继续做 Local Machine / Xbox 冒烟、A/V sync 阈值校准、P010/NV12 颜色链路、真实字幕样本和 PGS/ASS 完整渲染策略。
