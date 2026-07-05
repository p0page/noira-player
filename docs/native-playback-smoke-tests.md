# 原生播放冒烟测试

日期：2026-07-05

## 设备信息

- Xbox 型号：
- Xbox OS 版本：
- 电视/显示器型号：
- HDMI 模式：
- Xbox 设置中是否启用 HDR：
- 显示设备是否确认进入 HDR 模式：

## 测试文件

1. 1080p H.264 SDR，AAC 或 AC3 音频，无字幕
2. 4K HEVC SDR，AAC 或 AC3 音频，无字幕
3. 4K HEVC Main10 HDR10，AC3 或 EAC3 音频，无字幕
4. 4K HEVC Main10 HDR10，至少两条音轨
5. 4K HEVC Main10 HDR10，至少一条文本字幕

## 通用检查项

- 应用可以启动。
- 登录成功。
- 能进入媒体库并打开目标条目。
- PlaybackInfo 能拿到媒体源、直连 URL、音轨和字幕轨。
- 播放启动时走 native backend。
- 播放启动后没有触发 Emby 服务端转码。
- 暂停、恢复、快退、快进、停止可用。
- 切换媒体源后能从当前位置继续。
- 切换音轨后选择的是目标音轨。
- 字幕能启用、切换、关闭。
- HDR10 文件能让显示设备进入 HDR 输出。
- SDR 文件播放后能恢复 SDR 输出。
- HDR 播放停止后能恢复进入播放前的显示状态。
- 应用挂起/恢复后不会把显示设备留在错误 HDR 状态。
- Emby 服务端能看到播放开始、进度更新和停止记录。

## 当前实现限制

- 当前 native 解码器已通过共享 `FfmpegMediaSource` 打开 FFmpeg format context，能读视频/音频 packet 并接收 `AVFrame` 元数据；视频路径会在 codec 支持时尝试使用 D3D11VA hardware device context。
- 当前 `AudioDecoder` 已能通过 `swresample` 产出 48 kHz stereo float PCM，`AudioRenderer` 已能把 PCM 提交给 XAudio2 source voice；Windows 本机真实 Emby 片源已确认有声音，Xbox 仍需实机听音验证。
- 当前原地音轨切换会在当前位置重开目标音轨的 audio decoder/source voice；该链路仍需带多音轨样本实机验证。
- 当前 renderer 能清黑、present、复制同尺寸同格式 texture，并能尝试用 D3D11 video processor 呈现 FFmpeg D3D11VA texture slice；同时已加入 BGRA 软件帧回退，以覆盖 D3D11VA/video processor 未成功呈现时的可见画面。
- HDR10 metadata side-data 已能映射到 DXGI HDR10 metadata；BT.2020/PQ video processor 色彩空间设置和 tone mapping 策略尚未补齐。
- 当前已有后台 render loop，并会根据 XAudio2 音频时钟暂存早到的视频帧、丢弃明显落后的视频帧；阈值和观感仍需实机校准。
- 当前 `CurrentPositionTicks()` 已优先使用 XAudio2 `SamplesPlayed` 推导的初步音频时钟，seek 会清空旧音频 buffer；subtitle decoder 已能把 FFmpeg 文本/ASS 字幕 cue 接到 DirectWrite 文本叠加出口，但还没有真实字幕样本、PGS 图形字幕或完整 ASS 样式验证。
- 当前 Playback 页既保留手动 URL 测试入口，也能从 Home 页经媒体详情页拿到真实 Emby itemId、自动拉 PlaybackInfo 并开播；媒体源、音轨、字幕选择器已接到 orchestrator；start/progress/stop 上报已接到 Emby session API，但还没有实机确认服务器端播放记录效果。

## Windows 本机冒烟记录

- Debug x64 solution build 通过，0 个警告、0 个错误。
- Core 单元测试通过：60 个通过，0 个失败，0 个跳过。
- Debug x64 MSIX 已确认包含 FFmpeg runtime DLL。
- Windows 本机已完成 MSIX 签名、开发人员模式开启、`Add-AppDevPackage.ps1` 安装和首次启动。
- 当前已确认应用窗口能打开，窗口标题为 `Next Gen Xbox Emby`。
- 已在 Windows 本机使用真实 Emby 服务器完成 Login -> Home -> 媒体详情 -> Playback 冒烟；首页最新条目、PlaybackInfo、媒体源选择器、播放页状态均正常。
- 已验证真实条目 `第 40 集` 的 4K HEVC/AAC 直连源可以出画面并播放；同一条目的 1080p AVC/AAC 源切换后仍有画面；暂停、恢复、停止可用。
- 这次修复了“有声音没画面”：`SwapChainPanel` 之前在页面尚未布局时附着，原生层可能创建 `1x1` swapchain；现在播放前会等待原生表面加载，并把零尺寸兜底改为 1280x720。
- HDR10 输出、电视 HDR 模式切换、Xbox HEVC Main10 性能和手柄十英尺体验必须在 Xbox 实机上验证。

## 测试结果

| 文件 | 直连播放 | 视频 | 音频 | 字幕 | HDR 状态 | Emby 进度 | 备注 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1080p H.264/AVC | 通过 | 通过 | 通过 | 无字幕源 | 未测 | 已发送上报，服务端回读未确认 | Windows 本机，真实 Emby `第 40 集` 第二媒体源，`1080p / 1.6 Mbps, AVC • AAC · 1920x1080` |
| 4K HEVC | 通过 | 通过 | 通过 | 无字幕源 | 未测 | 已发送上报，服务端回读未确认 | Windows 本机，真实 Emby `第 40 集` 第一媒体源，`4K / 3.9 Mbps, HEVC • AAC · 3840x2160` |
| 4K HEVC HDR10 | 未测 | 未测 | 未测 | 未测 | 未测 | 未测 | |
| HDR10 多音轨 | 未测 | 未测 | 未测 | 未测 | 未测 | 未测 | |
| HDR10 字幕 | 未测 | 未测 | 未测 | 未测 | 未测 | 未测 | |

## Debug x64 侧载包

生成命令：

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never
```

预期输出目录：

```text
src\NextGenEmby.App\AppPackages\NextGenEmby.App_<version>_x64_Debug_Test
```

## 待你实机填写

- Xbox Device Portal 部署是否成功：
- 首次启动是否成功：
- 是否能进入 Playback 页：
- Native surface 是否清黑且不崩溃：
- HDR10 显示状态实测结果：
- 失败日志或截图：
