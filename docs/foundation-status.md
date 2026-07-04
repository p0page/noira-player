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
- Core 单元测试通过：53 个通过，0 个失败，0 个跳过。
- `NextGenEmby.Core`、`NextGenEmby.Core.Tests`、`NextGenEmby.App` 均已能进入完整 solution 构建链路。
- Emby 认证、认证请求头、媒体库查询 URL、PlaybackInfo 解析、直连播放 URL、播放进度上报都已有单元测试覆盖。
- 播放编排层已有稳定的托管后端接口：`IPlaybackBackend`、`PlaybackDescriptor`、`PlaybackState`、`PlaybackOrchestrator`。
- 原生播放诊断契约已实现：`PlaybackBackendCapabilities`、`PlaybackDisplayStatus`、`IPlaybackBackendDiagnostics`。
- 托管侧原生播放适配器已实现：`INativePlaybackEngine`、`NativePlaybackOpenRequest`、`NativeDirectXPlaybackBackend`。
- UWP app 源码已经包含 Xbox 优先的 Shell、Login、Home、Playback 页面。
- UWP 登录流程已经接入 `EmbyApiClient`、`ApplicationDataSessionStore`、`ApplicationDataDeviceIdProvider`。
- UWP 播放页已经接入 `NativeDirectXPlaybackBackend` 和 native `SwapChainPanel`，并保留 `SystemMediaPlaybackBackend` 作为临时回退路径。
- C++/WinRT 原生组件已经创建并接入 solution；当前包含 HDR display controller、DXGI swapchain、native playback graph、HTTP input 边界、video decoder stub、video renderer 边界、audio/subtitle renderer 控制边界。
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
Passed: 53
Failed: 0
Skipped: 0
```

## 已解除的阻塞

之前完整 UWP app 构建被本机 Visual Studio/UWP 组件安装状态卡住，不是源码编译错误。

旧 MSBuild 错误：

```text
error MSB3644: Could not find the reference assemblies for .NETCore,Version=v5.0
```

已通过安装 Visual Studio 的 UWP/WinUI/.NET Native 工具链修复。本机现在存在 `.NETCore\v5.0` 引用程序集，solution build 已越过该阶段并成功完成。

## 尚未验证

- 还没有在 Visual Studio 中启动 Local Machine 做手工冒烟测试。
- 还没有部署到 Xbox 硬件。
- FFmpeg/D3D11VA 还没有接入，native decoder 仍是 stub。
- HDR/HEVC 真实视频播放还没有完成；当前已具备 HDR display/DXGI/renderer 边界，但没有真实 HEVC Main10/P010 解码帧。
- XAudio2 音频输出和 DirectWrite 字幕绘制还没有完成；当前只有音轨/字幕控制边界。
- 真实 Emby 条目驱动的播放进度 HTTP 上报还没有接入；当前已能构造 progress request，并能透传 backend position event。

## 原生播放硬件验证

硬件冒烟矩阵已创建：`docs/native-playback-smoke-tests.md`。

当前状态：未在 Xbox 硬件上执行。需要在 Xbox Dev Mode 部署 Debug x64 包后填写实际结果。

## 建议的下一步本机操作

用 Visual Studio 打开 `NextGenXboxEmby.sln` 做手工冒烟测试：

- 启动项目：`NextGenEmby.App`
- 平台：`x64`
- 目标：Local Machine
- 登录页能以深色模式渲染
- 能在 Login、Home、Playback 之间导航
- Playback 页显示黑色视频区域和底部控制层
- 键盘或手柄导航时焦点可见

如果 Local Machine 冒烟通过，再继续 `docs/superpowers/plans/2026-07-05-native-playback-core.md` 的后续任务：接入 FFmpeg UWP/x64 产物、完成 D3D11VA 解码、补齐 NV12/P010 渲染、实现 XAudio2/DirectWrite，并在 Xbox 硬件上执行 `docs/native-playback-smoke-tests.md`。
