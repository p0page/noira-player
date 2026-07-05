# 基础阶段状态

日期：2026-07-05

## 分支

`codex/xbox-emby-foundation`

## 已验证

- 已安装 Visual Studio 2022 Community，路径为 `C:\Program Files\Microsoft Visual Studio\2022\Community`，并且可以启动。
- MSBuild 可用，路径为 `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`。
- 已通过 Visual Studio Installer 补齐 UWP/WinUI 工具链，包括 `Microsoft.VisualStudio.Workload.Universal`、`.NET Native 和 .NET Standard`、`通用 Windows 平台工具`、`C++ (v143) 通用 Windows 平台工具`、Windows 11 SDK `10.0.22621.0`。
- `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v5.0` 已存在，之前阻塞 UWP C# 项目的 `.NETCore,Version=v5.0` 引用程序集缺失问题已解除。
- Solution MSBuild 已通过：`NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64` 结果为 0 个警告、0 个错误。
- Core 单元测试通过：60 个通过，0 个失败，0 个跳过。
- `NextGenEmby.Core`、`NextGenEmby.Core.Tests`、`NextGenEmby.App` 均已能进入完整 solution 构建链路。
- Emby 认证、认证请求头、媒体库查询 URL、PlaybackInfo 解析、直连播放 URL、播放 session start/progress/stop 上报都已有单元测试覆盖。
- 播放编排层已有稳定的托管后端接口：`IPlaybackBackend`、`PlaybackDescriptor`、`PlaybackState`、`PlaybackOrchestrator`。
- 原生播放诊断契约已实现：`PlaybackBackendCapabilities`、`PlaybackDisplayStatus`、`IPlaybackBackendDiagnostics`。
- 托管侧原生播放适配器已实现：`INativePlaybackEngine`、`NativePlaybackOpenRequest`、`NativeDirectXPlaybackBackend`。
- UWP app 源码已经包含 Xbox 优先的 Shell、Login、Home、MediaDetails、Playback 页面。
- UWP 登录流程已经接入 `EmbyApiClient`、`ApplicationDataSessionStore`、`ApplicationDataDeviceIdProvider`。
- UWP 播放页已经接入 `NativeDirectXPlaybackBackend` 和 native `SwapChainPanel`，并保留 `SystemMediaPlaybackBackend` 作为临时回退路径。
- Home 页已能用已保存 session 拉取 Emby 最新条目，条目点击会先进入媒体详情页，再把 itemId 带入 Playback 页；Playback 页收到真实条目后会拉 PlaybackInfo、选择第一个 media source 启动播放，开播调用 `/Sessions/Playing`，暂停/恢复/seek/切流/定时调用 progress API，停止按钮、页面卸载和原生 EOF 调用 `/Sessions/Playing/Stopped`。
- Playback 页已提供媒体源、音轨、字幕三个 Xbox 可聚焦选择器，分别调用 `SwitchMediaSourceAsync`、`SwitchAudioStreamAsync`、`SwitchSubtitleStreamAsync`，并发送对应 progress event。
- C++/WinRT 原生组件已经创建并接入 solution；当前包含 HDR display controller、DXGI swapchain、native playback graph、HTTP input 边界、FFmpeg format/codec probe 边界、video renderer 边界、audio/subtitle renderer 控制边界。
- `FFmpegInteropX.FFmpegUWP` `5.1.100` 已作为 NuGet native 依赖接入，Debug x64 native 构建会链接 FFmpeg lib 并复制对应运行时 DLL。
- `VideoDecoder` 已能从 native D3D11 device/context 创建 FFmpeg D3D11VA hardware device context，并在 codec 支持时选择硬件像素格式。
- `VideoDecoder` 已能把 FFmpeg D3D11 frame 的 texture 和 array slice index 带给 renderer；`DxDeviceResources` 已有 D3D11 video processor blit 路径用于尝试呈现 NV12/P010 frame。
- `VideoDecoder` 已能把 FFmpeg HDR10 mastering display / content light side-data 映射为 `DXGI_HDR_METADATA_HDR10` 并交给 renderer 设置到 swapchain。
- `FfmpegMediaSource` 已成为共享 demux 边界，负责打开 direct-play URL、查找流、按已注册流缓存 packet；`VideoDecoder` 已改为从共享 source 读取 packet，为音视频同步做准备。
- `AudioDecoder` 已能选择音频流、打开 FFmpeg audio decoder，并解出包含采样率、声道数、样本数、sample format 和 position ticks 的音频 frame 元数据。
- `AudioDecoder` 会把音频 frame 通过 `swresample` 转成 48 kHz stereo float PCM；`AudioRenderer` 已建立 XAudio2 source voice、小型 PCM buffer queue、buffer-end 回收和基于 `SamplesPlayed` 的初步音频时钟。
- `PlaybackGraph` 已有临时后台 render loop，可持续拉取视频帧；`CurrentPositionTicks()` 会优先读取 XAudio2 音频时钟，视频帧会在明显早于音频时暂存等待、明显落后音频时丢帧追赶。
- `PlaybackGraph` 后台 loop 已能把 EOF/异常转换为 native `Stopped`/`Failed` 事件，由 `NativePlaybackEngine` 继续透传给托管播放编排层。
- 托管 `PlaybackOrchestrator` 已能在 backend 支持时原地切换音轨/字幕；`NativeDirectXPlaybackBackend`、UWP wrapper 和 C++/WinRT runtime 已接通音轨切换、字幕切换和禁用字幕的控制面。
- Native 音轨切换已能关闭旧 audio decoder/source voice、释放旧音轨 packet queue，并在当前位置重开目标音轨的 FFmpeg decoder 和 XAudio2 source voice。
- `AudioRenderer` 已接入 XAudio2 engine / mastering voice 生命周期，播放打开时会创建音频设备并在 start/pause/resume/stop 时控制 engine；seek 时会清空旧 source buffer 并重置音频时钟基准。
- `SubtitleDecoder` 已接入 FFmpeg subtitle stream 的文本 cue 解码边界，能从共享 demux 中取已排队字幕 packet，并把 `SUBTITLE_TEXT` / `SUBTITLE_ASS` 文本 cue 喂给 DirectWrite overlay；PGS/bitmap 字幕、完整 ASS 样式和外置字幕 URL 尚未接入。
- Debug x64 MSIX 测试包已生成：`src\NextGenEmby.App\AppPackages\NextGenEmby.App_0.1.0.0_x64_Debug_Test\NextGenEmby.App_0.1.0.0_x64_Debug.msix`，包内已确认包含 FFmpeg 运行时 DLL。
- Windows 本机已完成 Debug x64 MSIX 侧载验证：使用 `CN=NextGenEmby` 临时开发证书签名，导入本机级 `LocalMachine\Root` / `LocalMachine\TrustedPeople`，开启 `AllowDevelopmentWithoutDevLicense` 和 `AllowAllTrustedApps`，并通过 `Add-AppDevPackage.ps1` 安装成功。
- Windows 本机已能启动应用：`shell:AppsFolder\NextGenEmby.App_h8qjz0sr1sg4m!App` 可打开窗口，`ApplicationFrameHost` 窗口标题为 `Next Gen Xbox Emby`，`NextGenEmby.App` 进程存在。
- Kodi HDR 研究路径已记录在 ADR 0001。

## 验证命令

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -products Microsoft.VisualStudio.Product.Community -requires Microsoft.VisualStudio.Workload.Universal Microsoft.VisualStudio.ComponentGroup.UWP.NetCoreAndStandard Microsoft.VisualStudio.ComponentGroup.UWP.Support -format json
```

结果：

```text
Visual Studio Community 2022 17.14.34 matched the required UWP components.
isComplete: true
isLaunchable: true
isRebootRequired: false
```

```powershell
Get-ChildItem 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore'
```

结果：

```text
v4.5
v5.0
```

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

结果：

```text
Build succeeded.
0 warnings
0 errors
```

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

结果：

```text
Passed: 60
Failed: 0
Skipped: 0
```

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never
```

结果：

```text
Build succeeded.
0 warnings
0 errors
```

```powershell
tar -tf 'src\NextGenEmby.App\AppPackages\NextGenEmby.App_0.1.0.0_x64_Debug_Test\NextGenEmby.App_0.1.0.0_x64_Debug.msix' | Select-String -Pattern 'avcodec|avdevice|avfilter|avformat|avutil|swresample|swscale'
```

结果：

```text
avcodec-59.dll
avdevice-59.dll
avfilter-8.dll
avformat-59.dll
avutil-57.dll
swresample-4.dll
swscale-6.dll
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'src\NextGenEmby.App\AppPackages\NextGenEmby.App_0.1.0.0_x64_Debug_Test\Add-AppDevPackage.ps1' -Force -SkipLoggingTelemetry
```

结果：

```text
Success: Your app was successfully installed.
PackageFamilyName: NextGenEmby.App_h8qjz0sr1sg4m
```

## 已解除的阻塞

之前完整 UWP app 构建被本机 Visual Studio/UWP 组件安装状态卡住，不是源码编译错误。

旧 MSBuild 错误：

```text
error MSB3644: Could not find the reference assemblies for .NETCore,Version=v5.0
```

已通过安装 Visual Studio 的 UWP/WinUI/.NET Native 工具链修复。本机现在存在 `.NETCore\v5.0` 引用程序集，solution build 已越过该阶段并成功完成。

MSIX 本机侧载还额外需要签名和开发人员模式。当前本机已用临时 `CN=NextGenEmby` 开发证书签名测试包，并启用 `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock` 下的 `AllowDevelopmentWithoutDevLicense=1` 与 `AllowAllTrustedApps=1`。

## 尚未验证

- 还没有在本机完成真实 Emby 登录后的 Home -> 媒体详情 -> Playback 真实媒体播放手工验证；应用已能启动，但仍需要在 UI 中输入服务器和账号。
- 还没有部署到 Xbox 硬件。
- FFmpeg UWP 产物已接入 native build，`FfmpegMediaSource` 已能初始化 `AVFormatContext`，`VideoDecoder` 已能初始化视频 `AVCodecContext`、读取视频 packet / 接收 `AVFrame`，并把 D3D11VA texture slice 交给 renderer；真实画面呈现还没有经过 Local Machine 或 Xbox 实机确认。
- HDR/HEVC 真实视频播放还没有完成；当前已具备 HDR display/DXGI/renderer 边界、HDR10 metadata 映射、临时 render loop 和 P010/NV12 video processor 尝试路径，但色彩空间细节、A/V sync 和真实 HEVC Main10/P010 播放效果尚未验证。
- XAudio2 source voice、PCM buffer queue、FFmpeg `swresample`、初步音频时钟和第一版视频等待/丢帧策略已接入，但 A/V sync 阈值、真实听音和画面效果还没有经过 Local Machine 或 Xbox 实机验证；DirectWrite 字幕叠加和 FFmpeg 文本字幕 cue 解码已接入，但图形字幕、完整 ASS 样式和实机字幕显示还没有验证。
- 真实 Emby 条目驱动的播放入口、媒体详情页、媒体源/音轨/字幕选择 UI、start/progress/stop HTTP 上报 glue 已接入；服务器端播放记录效果还没有经过 Local Machine 或 Xbox 实机确认。

## 原生播放硬件验证

硬件冒烟矩阵已创建：`docs/native-playback-smoke-tests.md`。

当前状态：未在 Xbox 硬件上执行。需要在 Xbox Dev Mode 部署 Debug x64 包后填写实际结果。

## 验证边界

Windows 本机可以继续验证：

- UWP app 构建、签名、安装、启动。
- Login -> Home -> 媒体详情 -> Playback 导航与焦点。
- Emby 登录、latest items、PlaybackInfo、直连 URL、start/progress/stop 上报。
- 普通 SDR/HEVC 播放、音轨切换、字幕切换、seek/pause/resume/stop、基础 A/V sync。

必须上 Xbox 实机验证：

- 真实 HDR10 输出、电视 HDR 模式切换、HDR 停止后的 SDR 恢复。
- Xbox 上 HEVC Main10/D3D11VA/P010/NV12 渲染性能和稳定性。
- 手柄十英尺交互、TV 安全区、Dev Mode 部署体验。
- Xbox 正常模式或小范围分发路线。

## 建议的下一步本机操作

应用已经安装到本机，可以直接从开始菜单启动 `Next Gen Xbox Emby`，或用下面命令启动：

```powershell
Start-Process 'shell:AppsFolder\NextGenEmby.App_h8qjz0sr1sg4m!App'
```

接下来做手工冒烟测试：

- 登录页能以深色模式渲染
- 能在 Login、Home、媒体详情、Playback 之间导航
- Playback 页显示黑色视频区域和底部控制层
- 键盘或手柄导航时焦点可见

如果 Local Machine 冒烟通过，再继续 `docs/superpowers/plans/2026-07-05-native-playback-core.md` 的后续任务：补齐 HDR/PQ 色彩空间、PTS/音频时钟同步、实现 XAudio2/DirectWrite，并在 Xbox 硬件上执行 `docs/native-playback-smoke-tests.md`。
