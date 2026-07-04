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
- 只提交 `packages.config`、`.vcxproj` 接入点、源码里的最小链接哨兵和依赖说明。
- 如果后续需要修改 FFmpeg 构建参数，再改为自建 UWP FFmpeg，并把构建脚本、校验和、源码版本和 configure 命令放进仓库。

当前状态：

- `VideoDecoder` 当前只调用 `avformat_version()` 作为链接哨兵，证明头文件和 `avformat.lib` 已进入 native build。
- `HttpMediaInput` 和 `VideoDecoder` 仍是验证壳，不会实际发起网络读取、demux 或 decode。
- 下一步才会把 `VideoDecoder` 改成真正的 FFmpeg `AVFormatContext` / `AVCodecContext` 生命周期，并继续接 D3D11VA、P010/NV12 渲染、音频样本和字幕 cue。
