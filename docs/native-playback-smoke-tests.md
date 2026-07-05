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
- Emby 服务端能看到进度更新。

## 当前实现限制

- 当前 native 解码器已通过共享 `FfmpegMediaSource` 打开 FFmpeg format context，能读视频/音频 packet 并接收 `AVFrame` 元数据；视频路径会在 codec 支持时尝试使用 D3D11VA hardware device context。
- 当前 `AudioDecoder` 已能通过 `swresample` 产出 48 kHz stereo float PCM，`AudioRenderer` 已能把 PCM 提交给 XAudio2 source voice；该链路尚未经过 Local Machine 或 Xbox 实机听音验证。
- 当前原地音轨切换会在当前位置重开目标音轨的 audio decoder/source voice；该链路也尚未经过实机验证。
- 当前 renderer 能清黑、present、复制同尺寸同格式 texture，并能尝试用 D3D11 video processor 呈现 FFmpeg D3D11VA texture slice；该路径尚未经过 Local Machine 或 Xbox 实机验证。
- HDR10 metadata side-data 已能映射到 DXGI HDR10 metadata；BT.2020/PQ video processor 色彩空间设置和 tone mapping 策略尚未补齐。
- 当前已有后台 render loop，并会根据 XAudio2 音频时钟暂存早到的视频帧、丢弃明显落后的视频帧；阈值和观感仍需实机校准。
- 当前 `CurrentPositionTicks()` 已优先使用 XAudio2 `SamplesPlayed` 推导的初步音频时钟，seek 会清空旧音频 buffer；subtitle renderer 已有 DirectWrite 文本叠加出口，但还没有 FFmpeg 字幕解码和 cue 调度。
- 当前 Playback 页仍是手动 URL demo，真实 Emby 条目驱动和 HTTP 进度上报尚未完成。

## 测试结果

| 文件 | 直连播放 | 视频 | 音频 | 字幕 | HDR 状态 | Emby 进度 | 备注 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1080p H.264 SDR | 未测 | 未测 | 未测 | 未测 | 未测 | 未测 | |
| 4K HEVC SDR | 未测 | 未测 | 未测 | 未测 | 未测 | 未测 | |
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
src\NextGenEmby.App\AppPackages\NextGenEmby.App_0.1.0.0_x64_Debug_Test
```

## 待你实机填写

- Xbox Device Portal 部署是否成功：
- 首次启动是否成功：
- 是否能进入 Playback 页：
- Native surface 是否清黑且不崩溃：
- HDR10 显示状态实测结果：
- 失败日志或截图：
