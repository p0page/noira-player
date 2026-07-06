# 播放质量参考素材集

这个文档只定义“素材来源和用途”，不把大视频文件放进仓库。目标是给自动化模型循环提供稳定的输入集合，让报告能覆盖 HDR、帧率、码率、DV 降级/拒播、字幕音轨和容器边界。

## 使用原则

1. 素材集不等于客观显示校准。没有 HDMI capture / 光学测量时，软件报告不能证明实际 HDMI InfoFrame、面板 EOTF 或电视侧 tone mapping。
2. 每个样本必须有固定 `caseId`，并记录期望的 codec、frameRate、hdrKind、分辨率、音轨/字幕特征和测试目的。
3. 先用小集合跑快速回归，再用 HDR/高码率/长样本跑优化判断。
4. 模型消费结果时，以 `PlaybackQualityRunResult`、`compare-suite` 的 `cases`、`cadence`、`framePacing` 和 `optimization` 为主，不依赖人工观看结论。

## 推荐素材来源

### Netflix Open Content

链接：
- https://opencontent.netflix.com/
- https://download.opencontent.netflix.com/
- https://netflixtechblog.com/engineers-making-movies-aka-open-source-test-content-f21363ea3781

用途：
- HDR / PQ / P3、4K、23.976fps、59.94fps、高动态范围颜色链路。
- Chimera 提供 DCI 4K 23.98p HDR P3/PQ 和 59.94p HDR P3/PQ，适合验证 Xbox HDR 输出切换、23.976/59.94 cadence 和 10-bit HDR 解码压力。
- Cosmos Laundromat 有 2K 24p HDR P3/PQ MP4，适合较轻的 HDR smoke。
- Meridian/Sparks 等素材可用于 Dolby Vision 或高规格 HDR 研究，但体积和工作流更重，优先作为专项素材，不放进默认 smoke。

建议初始 case：
- `netflix/chimera-4k-2398-hdr-pq`: HDR10/PQ 色彩链路、23.976 cadence、长样本帧节奏。
- `netflix/chimera-4k-5994-hdr-pq`: 59.94 HDR/HFR、解码吞吐和显示刷新匹配。
- `netflix/cosmos-2k-24p-hdr-pq`: 快速 HDR smoke，低于 Chimera 的资源压力。

### Jellyfin Test Videos

链接：
- https://syd1.mirror.jellyfin.org/test-videos/

用途：
- 面向媒体服务器/播放器兼容性，按 SDR、HDR、Dolby Vision、码率、分辨率、codec、bit depth 分类。
- 适合快速覆盖 HEVC Main10、HDR10、DV Profile 5/7/8、SDR fallback、拒播路径和高码率边界。
- 对本项目特别有价值，因为目标协议是 Emby/Jellyfin 相近的媒体服务场景。

建议初始 case：
- `jellyfin/hdr10-hevc-main10-4k`: HDR10 直播放链路。
- `jellyfin/dv-profile5-hevc-4k`: DV Profile 5 不支持时的明确拒播或可解释 fallback。
- `jellyfin/dv-profile8-hevc-4k`: 可降级 HDR10/HLG 的 DV 路径验证。
- `jellyfin/sdr-hevc-4k`: SDR HEVC 基线。

### Kodi Samples

链接：
- https://kodi.wiki/view/Samples

用途：
- Kodi 官方 wiki 按 4K/HDR、3D、codec、framerate、肤色、黑位、字幕、高码率、音频等维度整理样本。
- 适合作为 coverage map，帮助决定还缺哪些 case。

注意：
- 页面中很多链接来自第三方网盘或历史来源，许可、可用性和稳定性不一致。
- 不建议把它作为自动下载源；更适合作为“测试维度清单”。

### Jellyfish Bitrate Test Files

链接：
- https://larmoire.org/jellyfish/

用途：
- 高码率压力测试，常见于 Plex/Jellyfin/Kodi 用户验证网络、解码和缓冲能力。
- 可选 H.264、HEVC、HEVC 10-bit、不同码率和 4K UHD 版本。

建议初始 case：
- `jellyfish/hevc-10bit-4k-120mbps`: 高码率 HEVC 10-bit。
- `jellyfish/hevc-10bit-4k-200mbps`: 更高吞吐压力，只放在 nightly 或手动专项。

注意：
- Jellyfish 不是自然影视内容，适合压测吞吐，不适合判断肤色、tone mapping 或真实运动观感。

### Xiph / Derf Test Media

链接：
- https://media.xiph.org/
- https://media.xiph.org/video/derf/

用途：
- 编码/压缩研究常用测试序列，包含 Big Buck Bunny、Sintel、Tears of Steel、Chimera/El Fuente 派生片段等。
- 适合生成自己的固定 H.264/HEVC/AV1 测试编码，或者做解码正确性和无损参考对比。

建议初始 case：
- `xiph/big-buck-bunny-sdr-reference`: SDR 基线、A/V sync、字幕/音轨可控扩展。
- `xiph/netflix-dinner-scene-4k-10bit`: 真实内容片段的 4K/10-bit 压力。

注意：
- 很多无损序列非常大，不适合作为默认自动化输入。

### FFmpeg FATE Samples

链接：
- https://ffmpeg.org/fate.html
- https://trac.ffmpeg.org/wiki/FATE/AddingATest

用途：
- 小样本解码、demux、filter 回归。适合验证 FFmpeg 相关代码路径、容器边界和异常样本。

注意：
- FATE 样本通常很短，不适合评估帧节奏、缓冲稳定性、长时间 A/V sync 或 HDR 输出体验。
- 应放在 parser/decoder smoke 层，不放在播放质量优化层。

## 建议分层

### Tier 0: CLI / Core Smoke

目的：不启动 App，不依赖 Xbox，不依赖真实大视频。使用合成 report JSON 验证比较器、suite、stall guard、case 定位、cadence 结构。

当前已有：
- `tools/quality-run/run-playback-core-checks.ps1`
- `tools/NextGenEmby.PlaybackQuality.Cli`

### Tier 1: 小文件播放 Smoke

目的：验证打开、首帧、基础 A/V sync、播放进度、音轨/字幕枚举和 SDR 输出。样本应小于几百 MB。

候选：
- Jellyfin SDR/HEVC 小样本。
- Blender/Xiph SDR 片段转码版本。

### Tier 2: HDR / Cadence 核心集

目的：验证 Xbox HDR 切换、DXGI color space、PQ/BT.2020、23.976/24/59.94 cadence、长样本 frame pacing。

候选：
- Netflix Chimera 4K 23.98 HDR P3/PQ。
- Netflix Chimera 4K 59.94 HDR P3/PQ。
- Netflix Cosmos 2K 24p HDR P3/PQ。
- Jellyfin HDR10 HEVC Main10 4K。

### Tier 3: DV / Fallback / Reject

目的：验证 DV 文件实际解析后的分类，而不是文件名预分类；不支持 DV 时应拒播或给出可解释 fallback。

候选：
- Jellyfin DV Profile 5。
- Jellyfin DV Profile 8。
- 用户库里的“喀什恋歌”DV 样本，作为本地专项 case，不进公开 manifest。

### Tier 4: 高码率和网络压力

目的：区分网络/缓冲/解码供给问题和纯 frame pacing 问题。

候选：
- Jellyfish HEVC 10-bit 4K 120Mbps。
- Jellyfish HEVC 10-bit 4K 200Mbps。
- Jellyfin 高码率 HDR10 样本。

## Manifest 校验

参考素材集应使用 `PlaybackQualityReferenceManifest` 描述，并在进入自动化播放前先通过 CLI 校验：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-manifest --manifest docs\qa\playback-quality-reference-manifest.example.json --output manifest-validation.json
```

校验会检查 `caseId` 唯一性、`uri`、`tier`、`purpose`，以及 `expected.codec/width/height/frameRate/hdrKind`。这些字段不完整时，模型不应启动播放优化，因为它无法确认当前样本是不是预期素材。

## 后续落地

下一步适合新增一个不绑定 App 的 manifest 格式，例如：

```json
{
  "caseId": "netflix/chimera-4k-2398-hdr-pq",
  "uri": "https://download.opencontent.netflix.com/...",
  "expected": {
    "codec": "hevc",
    "frameRate": 23.976,
    "hdrKind": "Hdr10",
    "width": 4096,
    "height": 2160
  },
  "purpose": [
    "hdr-output",
    "cadence-23.976",
    "frame-pacing"
  ],
  "tier": 2
}
```

manifest 只负责定义素材和期望；实际播放采集仍应输出 `PlaybackQualityRunResult`，再由 `compare-suite` 汇总。
