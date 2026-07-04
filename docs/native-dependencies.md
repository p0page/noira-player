# 原生依赖记录

日期：2026-07-05

## FFmpeg

用途：

- 解析 Emby direct-play HTTP/HTTPS 媒体流。
- 分离容器中的视频、音频、字幕流。
- 解码 HEVC Main / Main10，并在后续阶段接入 D3D11VA 硬件解码。
- 提取 HDR10 metadata，交给 DXGI swapchain 和视频渲染器。

需要的库：

- `avformat`
- `avcodec`
- `avutil`
- `swresample`
- `swscale`
- `avfilter`：仅在 ASS 字幕渲染委托给 FFmpeg 时需要。

构建要求：

- `x64`
- UWP 可用的 Windows 目标。
- 启用 D3D11VA。
- 启用 `http` 和 `https` 协议。
- 记录 LGPL/GPL 模式、源码来源、提交号或版本号，以及完整 configure 命令。
- 产物需要能被 UWP app package 复制到运行目录。

本地目录约定：

```text
native-deps/
  ffmpeg/
    include/
    lib/x64/
    bin/x64/
    build-notes.md
```

二进制策略：

- 不把大型第三方二进制提交到仓库。
- 可以提交构建脚本、校验和、来源说明和 `build-notes.md`。
- 本地二进制放在被忽略的 `native-deps/ffmpeg/` 下。
- UWP 打包脚本后续负责把运行时 DLL 复制进 app package。

当前状态：

- 2026-07-05：仓库内还没有 `native-deps/ffmpeg/`，因此当前提交只加入 `VideoDecoder` 可编译边界，不链接 FFmpeg。
- `HttpMediaInput` 和 `VideoDecoder` 目前都是验证壳，不会实际发起网络读取或解码。
