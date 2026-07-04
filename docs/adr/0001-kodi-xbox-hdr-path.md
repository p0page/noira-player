# ADR 0001：Kodi Xbox HDR 路径与原生播放核心

日期：2026-07-05

## 状态

已接受，作为原生播放核心阶段的研究依据。

## 背景

这个应用只面向 Xbox 上的 Emby 播放。基础阶段先用系统播放器后端验证 UI、Emby API 和播放编排边界。最终产品方向需要一个接近 Kodi 级别的原生后端，用于直连播放 4K HEVC HDR10；第一阶段不假设服务器一定能转码。

本地研究用的 Kodi checkout 是 `xbmc/xbmc` 的 `f0232910490189b97717bc5d309aec2e5751d6d3`，路径是 `.research/kodi-xbox`。该目录被 git 忽略；这份 ADR 记录可长期保留的结论。

## 决策

原生播放核心计划将以 Kodi 的 Win10/UWP/Xbox DirectX 路径作为 HDR 输出行为的主要参考。

基础 app 保持 `IPlaybackBackend` 稳定。这样 `SystemMediaPlaybackBackend` 后续可以被原生 `NativeDirectXPlaybackBackend` 替换，而不需要改 Emby API 解析、登录/session 存储、Xbox Shell 导航或播放编排。

下一阶段的播放后端应是一个 C++/WinRT UWP component，并通过 C# adapter 接入。它需要拥有解码、渲染、显示状态，并向 C# 暴露一个小型状态面：

- HDR 显示能力与当前 HDR 输出状态
- 当前媒体版本、音轨、字幕轨
- 播放位置、时长、暂停、seek、缓冲、结束、错误事件
- HDR 模式切换与失败原因
- 显示状态恢复结果

## Kodi 源码发现

Kodi 将 HDR 工作拆成几个职责，我们应该照这个思路拆。

### 1. Xbox 显示模式与 HDR 切换

Kodi 在 UWP/Xbox 上使用 `Windows.Graphics.Display.Core.HdmiDisplayInformation`，并通过 `HdmiDisplayHdrOption::Eotf2084` 请求 HDR 模式。

相关本地源码：

- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10.cpp:317`
- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10.cpp:320`
- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10.cpp:361`
- `.research/kodi-xbox/xbmc/platform/win32/WIN32Util.cpp:1212`
- `.research/kodi-xbox/xbmc/platform/win32/WIN32Util.cpp:1235`
- `.research/kodi-xbox/xbmc/platform/win32/WIN32Util.cpp:1241`

结论：原生后端不能把 HDR 当成一个普通播放器开关。它需要一个 Xbox 显示模式服务，能够请求当前 HDMI 模式进入 SDR 或 HDR10 EOTF。

### 2. HDR 能力与设置开关

Kodi 通过 windowing 抽象上报 HDR 能力，并用 `HDR_STATUS` 表示 unsupported、off、on、toggle failed。自动切换 HDR 还会被一个 HDR display setting 控制。

相关本地源码：

- `.research/kodi-xbox/xbmc/HDRStatus.h:11`
- `.research/kodi-xbox/xbmc/windowing/WinSystem.h:242`
- `.research/kodi-xbox/xbmc/windowing/WinSystem.h:243`
- `.research/kodi-xbox/xbmc/windowing/WinSystem.cpp:320`
- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10DX.cpp:175`
- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10DX.cpp:179`

结论：我们的原生后端应该暴露明确的 `Unsupported`、`Off`、`On`、`Failed` 状态，不要只用 bool。C# 层应能展示或记录为什么没有进入 HDR。

### 3. Swapchain 与 DXGI HDR 输出

Kodi 在需要 HDR 或 10-bit surface 时使用 10-bit swapchain。它通过 `IDXGISwapChain4::SetHDRMetaData` 设置 HDR10 metadata，并通过 `IDXGISwapChain3::SetColorSpace1` 切换传递函数和色彩空间。

相关本地源码：

- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:636`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:685`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:719`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1293`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1339`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1451`

结论：原生后端必须拥有 DXGI swapchain，并在进入播放前验证支持的 color space。HDR10 使用 `DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020`；恢复 SDR 使用 `DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709`。

### 4. Xbox 专用的 swapchain 保留规则

Kodi 有一个非常关键的 Xbox 专用分支：在 Xbox 上切换 HDR 时，它不会销毁并重建 swapchain，而是在现有 swapchain 上切 color space。源码注释说明，在 Xbox 上重建 swapchain 可能丢失原生 4K 输出质量。

相关本地源码：

- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1357`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1368`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1392`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1397`

结论：Xbox 实现应避免在 HDR/SDR 切换时重建 swapchain，除非后续硬件测试证明这样做安全。这也是我们要参考 Kodi，而不是只看普通桌面 DirectX HDR sample 的主要原因之一。

### 5. 视频 metadata 与 HDR 状态机

Kodi 先从 FFmpeg stream metadata 和 frame side data 判断 HDR 类型，然后渲染器按每个 render buffer 更新 HDR 状态。

相关本地源码：

- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp:2546`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp:2554`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp:2556`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp:2562`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDCodecs/Video/DVDVideoCodecFFmpeg.cpp:1107`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDCodecs/Video/DVDVideoCodecFFmpeg.cpp:1118`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDCodecs/Video/DVDVideoCodecFFmpeg.cpp:1157`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:518`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:586`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:606`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:623`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:669`

结论：Emby `MediaSource` metadata 可以辅助选流，但原生后端仍必须基于实际 stream/frame metadata 再验证是否进入 HDR。渲染器要能根据解码帧在 HDR10、HLG-as-PQ fallback 和 SDR 输出之间切换。

### 6. 显示状态恢复

Kodi 在配置 renderer 时保存初始 HDR 状态，并在播放停止时恢复。如果没有启用 auto-switch，则直接恢复正确的 DXGI color space。

相关本地源码：

- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:144`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:146`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:149`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:155`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:199`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:204`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:212`

结论：我们的原生后端需要在正常停止、播放错误、后端 dispose、app suspend、app resume 时都尝试恢复显示状态。`PlaybackOrchestrator.StopAsync` 仍然是托管侧清理入口，但 native 清理必须自己防御性兜底。

## 影响

原生播放会比 iPlayX 这种系统播放器方案重很多，因为它必须拥有 HEVC 解码、渲染、显示输出循环。这个重量是有意接受的：缺失的核心能力正是 Xbox 上的 HDR、HEVC、音轨和字幕控制。

第一轮 native-core 不追求 Kodi 全功能对齐，只做窄而完整的垂直切片：

- 从 Emby direct-play HTTP 输入
- 一个选中的媒体版本
- 音轨和字幕轨切换
- HEVC Main/Main10 解码路径
- HDR10 输出与 SDR 恢复
- 回调现有 Emby progress 上报
- 记录 HDR mode、DXGI color space、metadata、失败原因的诊断日志

转码仍然不在这个阶段范围内。

## 原生核心验收清单

下一阶段计划必须创建：

- C++/WinRT UWP component project
- 名为 `NativeDirectXPlaybackBackend` 的 C# adapter
- 从 `PlaybackDescriptor` 到 native open options 的桥
- 带明确状态码的 HDR 能力检测
- HDR 进入和退出方法
- 10-bit swapchain 与 DXGI color-space 管理
- HDR10 metadata 传递
- 在 stop、failure、suspend、resume 时恢复显示状态
- 覆盖 1080p H.264 SDR、4K HEVC SDR、4K HEVC HDR10 的 fixture 测试或硬件冒烟脚本

## 参考

- Kodi repository: https://github.com/xbmc/xbmc
- Kodi Xbox HDR10 passthrough PR: https://github.com/xbmc/xbmc/pull/24083
- Kodi 21.3 release notes: https://kodi.tv/article/kodi-21-3-omega-release/
