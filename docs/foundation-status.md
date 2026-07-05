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
- Debug x64 MSIX 测试包已生成：`src\NextGenEmby.App\AppPackages\NextGenEmby.App_<version>_x64_Debug_Test\NextGenEmby.App_<version>_x64_Debug.msix`，包内已确认包含 FFmpeg 运行时 DLL。
- Windows 本机已完成 Debug x64 MSIX 侧载验证：使用 `CN=NextGenEmby` 临时开发证书签名，导入本机级 `LocalMachine\Root` / `LocalMachine\TrustedPeople`，开启 `AllowDevelopmentWithoutDevLicense` 和 `AllowAllTrustedApps`，并通过 `Add-AppDevPackage.ps1` 安装成功。
- Windows 本机已能启动应用：`shell:AppsFolder\NextGenEmby.App_h8qjz0sr1sg4m!App` 可打开窗口，`ApplicationFrameHost` 窗口标题为 `Next Gen Xbox Emby`，`NextGenEmby.App` 进程存在。
- Windows 本机已使用真实 Emby 服务器完成 Login -> Home -> 媒体详情 -> Playback 冒烟；首页最新条目、PlaybackInfo、媒体源选择器、播放状态、停止状态均正常。
- Windows 本机已确认真实条目 `第 40 集` 的 4K HEVC/AAC 直连源可以出画面并播放；同一条目的 1080p AVC/AAC 源切换后仍可见画面；暂停和恢复状态正常。
- 已修复本机“有声音没画面”的直接原因：native surface 之前可能在 `SwapChainPanel` 尚未布局时附着，导致创建 `1x1` swapchain；现在播放前等待 surface 加载，并在原生层使用 1280x720 作为零尺寸兜底。
- `VideoDecoder` 已加入 BGRA 软件帧回退，`VideoRenderer` 会在 D3D11 texture/video processor 路径没有成功呈现时尝试把软件帧绘制到 backbuffer。
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
tar -tf 'src\NextGenEmby.App\AppPackages\NextGenEmby.App_<version>_x64_Debug_Test\NextGenEmby.App_<version>_x64_Debug.msix' | Select-String -Pattern 'avcodec|avdevice|avfilter|avformat|avutil|swresample|swscale'
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
powershell -NoProfile -ExecutionPolicy Bypass -File 'src\NextGenEmby.App\AppPackages\NextGenEmby.App_<version>_x64_Debug_Test\Add-AppDevPackage.ps1' -Force -SkipLoggingTelemetry
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

- 还没有部署到 Xbox 硬件。
- HDR10 输出、电视 HDR 模式切换、HDR 停止后的 SDR 恢复、Xbox 上 HEVC Main10/D3D11VA/P010/NV12 性能仍需 Xbox 实机验证。
- A/V sync 阈值还需要更长样本校准；当前只做了 Windows 本机短时播放、切源、暂停、恢复、停止冒烟。
- 多音轨真实样本、真实字幕样本、PGS/bitmap 字幕、完整 ASS 样式和外置字幕 URL 尚未验证。
- start/progress/stop HTTP 上报已发送，但当前测试服务器的 session 回读接口未完成确认；服务端播放记录效果仍需在 Emby 管理端或可用 session API 中复核。

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

## Xbox Fluent UI 页面交互改造状态

日期：2026-07-05

本轮已完成并通过代码审查的页面交互改造：

- Shell 已从桌面式 `NavigationView` 改为 Xbox/TV 优先的顶部导航，包含 Home、Movies、TV、Search、Settings 入口。
- Home 已接入真实 Emby 数据，支持继续观看、最近添加、电影/剧集库入口、详情入口和播放入口。
- Movies / TV Library 已改为真实媒体网格，支持刷新、排序、过滤、手柄可聚焦卡片和详情跳转。
- Media Details 已加载完整条目、播放源版本、音轨摘要、字幕摘要和剧集数据，并把选中的 `MediaSourceId` 传入播放页。
- Playback 已改为默认全屏视频，控制层改为隐藏式 OSD；版本、音轨、字幕和信息面板移动到 More 抽屉。
- 播放页已接入 Xbox 手柄路径：A 显示 OSD/确认 seek preview，B 取消 seek preview/关闭 More/关闭 OSD/返回，Menu 打开 More，D-pad 左右即时 seek，左摇杆左右进入可取消 seek preview。
- 左摇杆 seek preview 已实现误触保护：目标位置先预览，A 应用，B 取消，短暂无操作后自动提交；播放未 ready 或命令忙碌时不会把页面打成 Failed。
- Search 已接入 Emby 搜索，支持 Movie / Series / Episode 结果、空输入/未登录/无结果状态和详情跳转。

本轮自动验证：

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

结果：

```text
Passed: 86
Failed: 0
Skipped: 0
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

最新 Debug x64 MSIX 已提升包版本、重新签名并安装到本机：

```powershell
& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe' sign /fd SHA256 /sha1 6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0 'src\NextGenEmby.App\AppPackages\NextGenEmby.App_0.1.0.9_x64_Debug_Test\NextGenEmby.App_0.1.0.9_x64_Debug.msix'
```

结果：

```text
Successfully signed.
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'src\NextGenEmby.App\AppPackages\NextGenEmby.App_0.1.0.9_x64_Debug_Test\Add-AppDevPackage.ps1' -Force -SkipLoggingTelemetry
Get-AppxPackage NextGenEmby.App | Select-Object Name,Version,PackageFamilyName
```

结果：

```text
Success: Your app was successfully installed.
NextGenEmby.App 0.1.0.9 NextGenEmby.App_h8qjz0sr1sg4m
```

本轮追加修复：

- 修复 Shell 标题和 Home/Movies/TV/Search 导航在桌面窗口和 TV 安全区下挤压重叠的问题：Shell header 改为标题/设置按钮一行、主导航一行，并让导航在横向滚动容器中左对齐。
- 新增 `IconButtonStyle`，用于 Settings、Refresh 等纯图标按钮，显式设置深色底、边框、白色图标和焦点框；同时补充 `AutomationProperties.Name`，避免 UIA/无障碍树里出现空名称按钮。
- 修复 Playback 仍嵌在普通页面 chrome 下的问题：进入 `PlaybackPage` 时隐藏 Shell header，并让 `ContentFrame` 跨满根布局两行；离开播放页时恢复 Shell。
- 播放页底部控制按钮补充 UIA 名称：Pause、Resume、Seek back 10 seconds、Seek forward 30 seconds、More、Stop、Info。
- 抽出 `PlaybackOverlayInputPolicy` 并补单元测试，统一 Gamepad A/B/Menu 与桌面 fallback 的 OSD 决策：Space/Enter/鼠标点击显示 OSD，M 打开 More，Escape 按状态取消 seek preview、关闭 More、隐藏 OSD 或返回。

本轮 Windows 本机交互 smoke：

- 使用真实 Emby session 启动 0.1.0.9，窗口标题为 `Next Gen Xbox Emby`，`ApplicationFrameHost` 和 `NextGenEmby.App` 均响应。
- 通过 UI Automation 依次打开 Home、Movies、TV、Search、Settings：Movies 和 TV 均返回 `100 items`，Search 和 Settings 能正常进入。
- 通过 UI Automation 从 Movies 网格打开第一部影片详情页；详情页显示标题、播放按钮、版本列表、音轨摘要和字幕摘要。
- 通过详情页 Play 进入播放页；播放状态为 `Playing`，且播放页控件树中不再出现 Home/Movies/TV/Search/Settings，确认播放页已脱离 Shell chrome。
- 使用 DPI 修正后的窗口截图验证：Home header、Settings/Refresh 图标按钮、Movies 网格、详情页、播放页全屏画面和 More 抽屉均可见且没有明显重叠。
- Playback More 抽屉可打开，`SourceBox`、`AudioStreamBox`、`SubtitleStreamBox`、`InfoButton` 均存在；当前测试片源无字幕，`SubtitleStreamBox` 正常禁用并显示 Off。
- Windows 本机短时播放能看到真实画面；本轮结束时已通过 UI Automation 调用 Stop。
- 已用 computer-use 重新执行 Home -> Movies -> Details -> Playback -> More：截图确认真实播放画面、Shell 隐藏、OSD 显示、More 抽屉、Source/Audio/Subtitles/Info 均可见；最后通过 computer-use 点击 Stop，状态回到 `Stopped`。
- 已检测到真实 XInput 手柄连接：`XInputGetState(0)` 返回成功，PnP 中存在 `Xbox One Elite Controller` 和 `XINPUT compatible HID device`。当前自动化工具不能直接注入 XInput 按键，真实 GamepadA/Menu/D-pad 操作仍需人手按键或 Xbox 实机验证。

本轮追加修复（0.1.0.11）：

- Home 页的 Continue watching 数据源已改为 Emby Resume 端点：`/Users/{UserId}/Items/Resume`；新增 `GetResumeItemsAsync` 单元测试，覆盖 URL、query 和 `UserData.PlaybackPositionTicks` 映射。
- 已用真实 Emby 服务器直接验证 Resume 端点：返回 17 条 resume 项，第一条带有播放进度 ticks 和百分比；验证输出未记录 token。
- 详情页补充本页级 `GamepadB` / `Escape` 返回处理，避免焦点或 Frame 边界吃掉 B 键后无法退出详情。
- 播放页 native surface 不再在 `ActualWidth/ActualHeight == 0` 时假就绪；现在会等真实尺寸，并在 `SwapChainPanel.SizeChanged` 后按新尺寸重新 attach。
- DXGI swapchain 创建时已按 `SwapChainPanel.CompositionScaleX/Y` 创建物理像素缓冲，并设置 inverse matrix transform，减少高 DPI / 4K 屏下的软缩放。
- D3D11 video processor 和 BGRA fallback 都改为等比 contain 绘制，先清黑底，再把视频居中 letterbox，不再把源画面强行拉伸到整个 backbuffer。
- D3D11 video processor 明确设置 SDR 输入/输出色彩空间：输入按 full/limited range 与 BT.709/BT.601 矩阵，输出为 full-range RGB；BGRA software fallback 也通过 `sws_setColorspaceDetails` 显式设置 range 和 colorspace。
- `NativePlaybackOpenRequest` 新增 `IsHdr`，托管侧从 `EmbyMediaSource.IsHdr` 传入 native；native engine 不再对所有片源无条件 `EnterHdr10()`，SDR 片源会恢复初始 HDR 状态并保持 SDR swapchain color space。这是当前“非 HDR 片源高光溢出”的主要修复点。
- 自动验证已通过：`dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj` 通过 87/87；`MSBuild NextGenXboxEmby.sln /p:Configuration=Debug /p:Platform=x64` 通过；0.1.0.11 Debug x64 MSIX 已签名并本机安装成功。
- computer-use 启动 0.1.0.10 时命中一个 Windows UAC 提示：`Omega-x64.exe` 请求提权。该提示属于系统安全确认，不能由自动化代点；用户手动处理后已继续完成本机 UI 复验。最终已安装包版本为 0.1.0.11。
- UAC 处理后的 computer-use 复验已完成：Home 的 `Continue watching` hero 和横向行均显示 Resume 返回的 `"Friends"`；Movies 库显示 `100 items`；从 Movies 第一张卡进入详情后，`Escape` 成功返回 Movies，验证详情页本页级返回处理有效。
- 播放复验已完成：从 `"Friends"` 详情页 Resume 进入播放，播放页无 Home/Movies/TV/Search/Settings shell chrome，状态为 `Playing`；OSD 隐藏后画面按 16:9 居中显示，上下黑边正常，没有再被强制拉伸到窗口；可见高光不再呈现此前那种 SDR 被推到 HDR 的整体溢出。当前样本自身含快速运动与平台水印，清晰度仍需更多片源继续复核。
- Stop 复验已完成：Stop 按钮在主窗口目标正确绑定后可点击，状态回到 `Stopped`。此前 click/Enter/Space 未触发是因为 UI Automation 目标被 tooltip 的 `PopupHost` 临时抢走，不是应用 Stop 按钮失效。

仍需 Xbox / 本机后续验证：

- Windows 本机：补更多真实多音轨/字幕样本、长时间播放、seek preview 的真实手柄实操。
- Xbox 实机：4K 安全区、手柄焦点路径、HDR10 输出、HEVC Main10、P010/NV12 渲染性能、HDR 停止后的 SDR 恢复、Dev Mode 部署体验。

## 返回键与手柄输入自动化状态（0.1.0.15）

日期：2026-07-05

本轮针对“详情页和播放页不能稳定按 B 返回”的问题做了拆分修复：

- 详情页现在把 `GamepadB`、`Escape`、`GoBack` 视为同一个返回动作；进入影片详情后按 B / Escape 只返回上一页，不再卡在详情页。
- Shell 全局返回逻辑新增 `GlobalBackInputPolicy`：如果子页面已经处理过返回键，Shell 不再二次 `GoBack`。这修复了详情页处理 Escape 后又被 Shell 再退一级的问题。
- 播放页按键处理从 XAML `AddHandler(..., handledEventsToo: true)` 改为 `Window.Current.CoreWindow.KeyDown`，避免焦点落在原生 `SwapChainPanel` 或播放停止状态时吃掉返回键。
- 播放页停止或失败后，B / Escape / GoBack 会退出播放页；播放中仍保持原有 OSD 语义：先取消 seek preview，再关闭 More，再隐藏 OSD，OSD 已隐藏时才返回。
- 桌面自动化侧用 `Escape` / `GoBack` 覆盖同一套策略；真实 `GamepadB` 仍走同一套代码路径。当前 Windows 自动化环境没有虚拟 XInput 驱动，不能直接注入真正的手柄 B 键事件，但可以用同语义的键盘返回事件做稳定回归测试。

新增自动测试：

- `PlaybackOverlayInputPolicyTests.Cancel_Goes_Back_When_Back_Should_Exit_Playback_Page`
- `PlaybackOverlayInputPolicyTests.Cancel_Closes_More_Before_Exiting_Playback_Page`
- `GlobalBackInputPolicyTests.Does_Not_Go_Back_When_Event_Was_Already_Handled`
- `GlobalBackInputPolicyTests.Goes_Back_For_Unhandled_Back_Key_When_Frame_Can_Go_Back`

本轮验证结果：

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore
```

结果：

```text
Passed: 91
Failed: 0
Skipped: 0
```

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /p:UseSharedCompilation=false
```

结果：

```text
Build succeeded.
0 warnings
0 errors
```

最新 Debug x64 MSIX 已提升到 `0.1.0.15`，重新签名并安装到本机。

computer-use 本机交互复验：

- Home -> Movies -> 第一张影片卡 -> Details 后，按 Escape 返回 Movies；没有再发生 Shell 二次返回到 Home。
- Movies -> `"Friends"` 详情 -> Resume/Play -> 播放页后，按 Escape 返回 `"Friends"` 详情页；播放页返回没有被 Shell chrome 或原生视频 surface 吃掉。
- Stop 按钮在 Windows 自动化里仍容易只触发 tooltip 而不是按钮 invoke；停止态返回由单元策略覆盖，且 0.1.0.14 的本机复验曾覆盖 Stop -> `Stopped` -> Escape 返回详情。0.1.0.15 相比 0.1.0.14 仅升级调试包版本以绕开同版本 MSIX 覆盖限制，返回逻辑代码未变。

后续如果要更接近真实手柄自动化，需要安装虚拟手柄驱动或引入专门的 XInput 注入工具；目前代码层面已经把真实 `GamepadB` 和可自动化的 `Escape` / `GoBack` 收敛到同一条返回策略。
