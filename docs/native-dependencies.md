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

- `VideoDecoder` 已能调用 `avformat_open_input`、`avformat_find_stream_info`、`av_find_best_stream`、`avcodec_alloc_context3`、`avcodec_parameters_to_context` 和 `avcodec_open2`，建立 FFmpeg `AVFormatContext` / `AVCodecContext` 生命周期。
- `PlaybackGraph` 会把 native D3D11 device/context 传入 `VideoDecoder`；当 codec 声明支持 `AV_HWDEVICE_TYPE_D3D11VA` 时，`VideoDecoder` 会尝试创建 FFmpeg D3D11VA `AVHWDeviceContext` 并通过 `get_format` 选择硬件像素格式。
- `VideoDecoder::TryReadFrame()` 已接入 `av_read_frame` / `avcodec_send_packet` / `avcodec_receive_frame`，能读取视频 packet 并生成包含宽高、DXGI 像素格式、HDR transfer 类型和 position ticks 的 `DecodedVideoFrame` 元数据。
- 当 FFmpeg 返回 `AV_PIX_FMT_D3D11` frame 时，`DecodedVideoFrame` 会携带 `ID3D11Texture2D` 和 texture array slice index；`VideoRenderer` 会在同格式 copy 失败后尝试用 D3D11 video processor blit 到 swapchain backbuffer。
- 当 FFmpeg frame 带有 `AV_FRAME_DATA_MASTERING_DISPLAY_METADATA` / `AV_FRAME_DATA_CONTENT_LIGHT_LEVEL` side-data 且 transfer 为 PQ 时，`VideoDecoder` 会映射为 `DXGI_HDR_METADATA_HDR10`。映射单位遵循 Microsoft 文档：色度坐标乘 50000，最大母版亮度为整 nits，最小母版亮度为 1/10000 nit，MaxCLL/MaxFALL 为 nits。
- `PlaybackGraph` 已有临时 render loop，会在后台线程以固定 cadence 拉取并呈现视频帧；这只是 video smoke path，还不是基于音频时钟或 PTS 的 A/V sync。
- `VideoDecoder` 会在失败路径和 `Close()` 中释放 FFmpeg context；`PlaybackGraph.Open` 失败时会回滚已打开的边界状态。
- 当前还没有实机验证 video processor 对 FFmpeg D3D11VA frame 的呈现效果，也还没有补齐 BT.2020/PQ video processor 色彩空间设置和 tone mapping 策略。
- 下一步继续做 Local Machine / Xbox 冒烟、PTS/音频时钟同步、P010/NV12 颜色链路、音频样本和字幕 cue。
