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
6. Dolby Vision Profile 8.1 + HDR10 base layer，HEVC Main10
7. Dolby Vision Profile 5，纯 DV 无 HDR10/HLG base layer
8. HDR10+ + Dolby Vision hybrid，HEVC Main10

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
- HDR10 文件播放页 Info 面板中应显示 `R10G10B10A2_UNORM`、`RGB_FULL_G2084_NONE_P2020` 和 `DXGI conversion validated`。
- SDR 文件播放页 Info 面板中应显示 SDR swapchain color space，且不应请求 HDR 输出。
- DV Profile 8.1 文件应显示 `HDR10 fallback from Dolby Vision`，不应出现 Dolby Vision 输出状态。
- DV Profile 5 文件应显示 `Dolby Vision unsupported`，不应被初始播放源自动选中。
- SDR 文件播放后能恢复 SDR 输出。
- HDR 播放停止后能恢复进入播放前的显示状态。
- 应用挂起/恢复后不会把显示设备留在错误 HDR 状态。
- Emby 服务端能看到播放开始、进度更新和停止记录。

## 当前实现限制

- 当前 native 解码器已通过共享 `FfmpegMediaSource` 打开 FFmpeg format context，能读视频/音频 packet 并接收 `AVFrame` 元数据；视频路径会在 codec 支持时尝试使用 D3D11VA hardware device context。
- 当前 `AudioDecoder` 已能通过 `swresample` 产出 48 kHz stereo float PCM，`AudioRenderer` 已能把 PCM 提交给 XAudio2 source voice；Windows 本机真实 Emby 片源已确认有声音，Xbox 仍需实机听音验证。
- 当前原地音轨切换会在当前位置重开目标音轨的 audio decoder/source voice；该链路仍需带多音轨样本实机验证。
- 当前 renderer 能清黑、present、复制同尺寸同格式 texture，并能尝试用 D3D11 video processor 呈现 FFmpeg D3D11VA texture slice；同时已加入 BGRA 软件帧回退，以覆盖 D3D11VA/video processor 未成功呈现时的可见画面。
- HDR10 metadata side-data 已能映射到 DXGI HDR10 metadata；FFmpeg frame 的 primaries/transfer/matrix/range/bit-depth 已传入 renderer，并通过 `ID3D11VideoContext1` 设置 DXGI video processor input/output color space。
- Swapchain 现在优先创建 `R10G10B10A2_UNORM`，失败时回退 `B8G8R8A8_UNORM`；播放页 Info 会显示实际格式和 color space。
- Dolby Vision 不做 DV 输出；Profile 8.1/HLG 兼容源按 HDR10/HLG fallback 处理，Profile 5 按不支持直放处理。
- 当前已有后台 render loop，并会根据 XAudio2 音频时钟暂存早到的视频帧、丢弃明显落后的视频帧；阈值和观感仍需实机校准。
- 当前 `CurrentPositionTicks()` 已优先使用 XAudio2 `SamplesPlayed` 推导的初步音频时钟，seek 会清空旧音频 buffer；subtitle decoder 已能把 FFmpeg 文本/ASS 字幕 cue 接到 DirectWrite 文本叠加出口，但还没有真实字幕样本、PGS 图形字幕或完整 ASS 样式验证。
- 当前 Playback 页既保留手动 URL 测试入口，也能从 Home 页经媒体详情页拿到真实 Emby itemId、自动拉 PlaybackInfo 并开播；媒体源、音轨、字幕选择器已接到 orchestrator；start/progress/stop 上报已接到 Emby session API，但还没有实机确认服务器端播放记录效果。

## Windows 本机冒烟记录

- Debug x64 solution build 通过，0 个警告、0 个错误。
- Core 单元测试通过：123 个通过，0 个失败，0 个跳过。
- Debug x64 MSIX 已确认包含 FFmpeg runtime DLL。
- Windows 本机已完成 MSIX 签名、开发人员模式开启、`Add-AppDevPackage.ps1` 安装和首次启动。
- 当前已确认应用窗口能打开，窗口标题为 `Noira`。
- 已在 Windows 本机使用真实 Emby 服务器完成 Login -> Home -> 媒体详情 -> Playback 冒烟；首页最新条目、PlaybackInfo、媒体源选择器、播放页状态均正常。
- 已验证真实条目 `第 40 集` 的 4K HEVC/AAC 直连源可以出画面并播放；同一条目的 1080p AVC/AAC 源切换后仍有画面；暂停、恢复、停止可用。
- 这次修复了“有声音没画面”：`SwapChainPanel` 之前在页面尚未布局时附着，原生层可能创建 `1x1` swapchain；现在播放前会等待原生表面加载，并把零尺寸兜底改为 1280x720。
- HDR10 输出、电视 HDR 模式切换、Xbox HEVC Main10 性能已在 Xbox 实机验证；v41 已补齐 HDR/SDR 的 DXGI input/output color space、`CheckVideoProcessorFormatConversion()` 诊断，以及 HDR10/PQ -> SDR 的第一版 Kodi-style Hable shader tone mapping。手柄十英尺体验、更多真实样本、HLG/DV fallback 和字幕/音轨组合仍需继续验证。

## Xbox 实机冒烟记录

- Debug x64 v38 MSIX 已成功部署到 Xbox Device Portal。
- HDR10 源启动时电视侧出现 HDR 切换黑屏，native 日志显示 `RequestSetCurrentDisplayModeAsync HDR get success`。
- HDR10 源 native status 显示 `hdr=On active=True`、`R10G10B10A2_UNORM`、`RGB_FULL_G2084_NONE_P2020`。
- HDR 源播放后切换到 SDR 源，native 日志显示 `RequestSetCurrentDisplayModeAsync SDR get success`，最终 `hdr=Off active=False`、`RGB_FULL_G22_NONE_P709`。
- Debug x64 v39 MSIX 已成功部署到 Xbox Device Portal，且确认启动的是 `NoiraPlayer.App_0.1.0.39_x64__h8qjz0sr1sg4m`。
- HDR10 源 `4K / 19 Mbps, HEVC - HDR10` 实测最终状态为 `hdr=On active=True`、`swap=R10G10B10A2_UNORM`、`color=RGB_FULL_G2084_NONE_P2020`、`vpIn=YCBCR_STUDIO_G2084_TOPLEFT_P2020`、`vpOut=RGB_FULL_G2084_NONE_P2020`、`vpStatus=validated`。
- SDR 源 `4K / 18 Mbps, HEVC` 实测最终状态为 `hdr=Off active=False`、`color=RGB_FULL_G22_NONE_P709`、`vpIn=YCBCR_STUDIO_G22_LEFT_P709`、`vpOut=RGB_FULL_G22_NONE_P709`、`vpStatus=validated`。
- v39 仍不能宣称完整 Kodi 等价：HDR -> SDR tone mapping、DV fallback 真源、更多音轨/字幕/帧率组合仍按 `docs/kodi-color-pipeline-comparison.md` 继续补齐。
- Debug x64 v41 MSIX 已成功部署到 Xbox Device Portal，且确认启动的是 `NoiraPlayer.App_0.1.0.41_x64__h8qjz0sr1sg4m`。
- HDR10 源 `4K / 19 Mbps, HEVC - HDR10` 正常 HDR 输出回归通过：最终 `hdr=On active=True`、`color=RGB_FULL_G2084_NONE_P2020`、`vpIn=YCBCR_STUDIO_G2084_TOPLEFT_P2020`、`vpOut=RGB_FULL_G2084_NONE_P2020`、`vpStatus=validated`，日志为 `C:\tmp\nextgen-v41-hdr-normal-diagnostics.log`。
- HDR10 源强制 SDR 输出诊断通过：最终 `hdr=Off active=False`、`color=RGB_FULL_G22_NONE_P709`、`vpIn=YCBCR_STUDIO_G22_TOPLEFT_P2020`、`vpOut=RGB_FULL_G22_NONE_P709`、`vpStatus=validated;tone-mapped-hable`，日志为 `C:\tmp\nextgen-v41-hdr-force-sdr-tone-mapping-diagnostics.log`。
- SDR 源 `4K / 18 Mbps, HEVC` 回归通过：最终 `hdr=Off active=False`、`color=RGB_FULL_G22_NONE_P709`、`vpIn=YCBCR_STUDIO_G22_LEFT_P709`、`vpOut=RGB_FULL_G22_NONE_P709`、`vpStatus=validated`，日志为 `C:\tmp\nextgen-v41-sdr-diagnostics.log`。
- v41 仍不能宣称完整 Kodi 等价：HLG->PQ、HLG->SDR、DV fallback 真源、更多音轨/字幕/帧率组合和 tone mapping 可调策略仍按 `docs/kodi-color-pipeline-comparison.md` 继续补齐。

## 测试结果

| 文件 | 直连播放 | 视频 | 音频 | 字幕 | HDR 状态 | Emby 进度 | 备注 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1080p H.264/AVC | 通过 | 通过 | 通过 | 无字幕源 | 未测 | 已发送上报，服务端回读未确认 | Windows 本机，真实 Emby `第 40 集` 第二媒体源，`1080p / 1.6 Mbps, AVC • AAC · 1920x1080` |
| 4K HEVC | 通过 | 通过 | 通过 | 无字幕源 | 未测 | 已发送上报，服务端回读未确认 | Windows 本机，真实 Emby `第 40 集` 第一媒体源，`4K / 3.9 Mbps, HEVC • AAC · 3840x2160` |
| 4K HEVC HDR10 | 通过 | 通过 | 初步通过 | 未测 | 通过 | 未测 | Xbox v41，`4K / 19 Mbps, HEVC - HDR10`，native status 为 `R10G10B10A2_UNORM` + `RGB_FULL_G2084_NONE_P2020`，DXGI conversion 为 `YCBCR_STUDIO_G2084_TOPLEFT_P2020 -> RGB_FULL_G2084_NONE_P2020` 且 `validated` |
| 4K HEVC HDR10 -> SDR tone mapping | 通过 | 通过 | 初步通过 | 未测 | SDR + tone mapped | 未测 | Xbox v41 Debug 强制 SDR 诊断，DXGI conversion 为 `YCBCR_STUDIO_G22_TOPLEFT_P2020 -> RGB_FULL_G22_NONE_P709`，shader 后 `vpStatus=validated;tone-mapped-hable` |
| 4K HEVC SDR | 通过 | 通过 | 初步通过 | 未测 | SDR | 未测 | Xbox v41，`4K / 18 Mbps, HEVC`，native status 为 `RGB_FULL_G22_NONE_P709`，DXGI conversion 为 `YCBCR_STUDIO_G22_LEFT_P709 -> RGB_FULL_G22_NONE_P709` 且 `validated` |
| HDR10 多音轨 | 未测 | 未测 | 未测 | 未测 | 未测 | 未测 | |
| HDR10 字幕 | 未测 | 未测 | 未测 | 未测 | 未测 | 未测 | |
| DV Profile 8.1 + HDR10 base | 未测 | 未测 | 未测 | 未测 | HDR10 fallback | 未测 | 不应进入 DV 输出；Info 应显示 HDR10 fallback 与 DXGI conversion validated |
| DV Profile 5 | 不支持直放 | N/A | N/A | N/A | Unsupported | N/A | 应提示或选择其它版本，不应静默按 HDR10 播放 |
| HDR10+ + DV hybrid | 未测 | 未测 | 未测 | 未测 | HDR10 fallback | 未测 | 不应黑屏；优先验证实际 DXGI color space |

## Debug x64 侧载包

生成命令：

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NoiraPlayer.App\NoiraPlayer.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never
```

预期输出目录：

```text
src\NoiraPlayer.App\AppPackages\NoiraPlayer.App_<version>_x64_Debug_Test
```

## 待你实机填写

- Xbox Device Portal 部署是否成功：
- 首次启动是否成功：
- 是否能进入 Playback 页：
- Native surface 是否清黑且不崩溃：
- HDR10 显示状态实测结果：
- 失败日志或截图：
