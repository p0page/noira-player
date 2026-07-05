# Kodi 颜色管线对照结论

结论：v41 已经在 Xbox 上验证了三条颜色链路。HDR10 直放样本实测为 `YCBCR_STUDIO_G2084_TOPLEFT_P2020 -> RGB_FULL_G2084_NONE_P2020` 且 `vpStatus=validated`；SDR 直放样本实测为 `YCBCR_STUDIO_G22_LEFT_P709 -> RGB_FULL_G22_NONE_P709` 且 `vpStatus=validated`；HDR10 源强制 SDR 输出时按 Kodi 的 DXVA bypass 思路走 `YCBCR_STUDIO_G22_TOPLEFT_P2020 -> RGB_FULL_G22_NONE_P709`，再经过新增 Hable shader tone mapping，最终 `vpStatus=validated;tone-mapped-hable`。这比 v39 的“只识别 HDR->SDR 需要 tone mapping”更进一步，但仍不能宣称完整 Kodi 等价：HLG->PQ、HLG->SDR、DV fallback 真源、更多字幕/音轨/帧率组合和可调 tone mapping 策略仍未补齐。

后续实现的规范入口是 `docs/superpowers/specs/2026-07-05-kodi-dxgi-color-pipeline-v2-design.md` 和 `docs/superpowers/specs/2026-07-05-kodi-hdr-dv-fallback-design.md`。本文件保留 Kodi 代码对照和技术证据。

## 参考代码

- Kodi DXVA video processor：<https://github.com/xbmc/xbmc/blob/master/xbmc/cores/VideoPlayer/VideoRenderers/HwDecRender/DXVAHD.cpp>
- Kodi DXVA conversion 枚举：<https://github.com/xbmc/xbmc/blob/master/xbmc/cores/VideoPlayer/VideoRenderers/HwDecRender/DXVAEnumeratorHD.cpp>
- Kodi DXVA renderer：<https://github.com/xbmc/xbmc/blob/master/xbmc/cores/VideoPlayer/VideoRenderers/RendererDXVA.cpp>
- Kodi DXGI 交换链：<https://github.com/xbmc/xbmc/blob/master/xbmc/rendering/dx/DeviceResources.cpp>
- Kodi Windows renderer HDR/tone mapping 状态：<https://github.com/xbmc/xbmc/blob/master/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp>
- Kodi D3D output shader tone mapping：<https://github.com/xbmc/xbmc/blob/master/system/shaders/output_d3d.fx>
- Kodi Xbox/Win10 HDR 模式切换：<https://github.com/xbmc/xbmc/blob/master/xbmc/windowing/win10/WinSystemWin10.cpp>
- Kodi WinRT async 等待辅助：<https://github.com/xbmc/xbmc/blob/master/xbmc/platform/win10/AsyncHelpers.h>

## v38 Xbox 实测结论

这次成功的关键不是单点修复，而是几件事同时对齐：

1. App manifest 必须声明 `rescap:Capability Name="hevcPlayback"`。

   微软 Xbox UWP 4K/HDR 文档明确要求这个 restricted capability。实际验证中，如果没有它，系统不会按 4K/HDR 视频应用的方式暴露和处理 HEVC/HDR 能力。

2. HDR 模式选择要按 Kodi 的方式看 HDMI display mode，而不是只看当前分辨率。

   当前 Xbox 可能先处在 `1920x1080@59.94 SDR`，但可用 HDR 模式在 `3840x2160@59.94 BT.2020`。如果强制“同分辨率匹配”，就会误判没有 HDR。现在 `HdrDisplayController` 会在同刷新率、BT.2020 模式里优先选同 stereo、同分辨率，其次选最高分辨率。

3. 不把 `IsSmpte2084Supported()` 当硬门槛。

   Kodi 代码里也没有用它作为进入 HDR 的硬条件，并且注释提到当前代码路径下这个 API 有问题。我们的实现改为以 `HdmiDisplayColorSpace::BT2020` 作为模式筛选条件，然后用 `HdmiDisplayHdrOption::Eotf2084` 请求 HDR10。

4. `RequestSetCurrentDisplayModeAsync()` 不能在 UI/STA 线程上直接 `.get()`。

   直接阻塞会导致黑屏后崩溃。现在采用 Kodi 同类做法：在 STA 上挂 `Completed` 回调，用 `Concurrency::event` 等待，再取 `GetResults()`。

5. SDR 播放要显式离开 HDR。

   之前如果 app 已经处在 HDR，`RestoreInitialState()` 可能把 HDR 当成初始状态保留下来。现在 SDR 源播放前调用 `LeaveHdr10()`，用当前 display mode 请求 `HdmiDisplayHdrOption::None`，实测 HDR 源切到 SDR 源后最终状态为 `hdr=Off active=False`。

6. SwapChainPanel 路径保持 `DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL`。

   Kodi CoreWindow 路径在 Xbox 上使用 3 buffers 和 flip discard，但 UWP `CreateSwapChainForComposition`/`SwapChainPanel` 官方要求 flip sequential。当前实现保留 `FLIP_SEQUENTIAL`，改为 3 buffers，并在 HDR 输出时使用 `DXGI_FORMAT_R10G10B10A2_UNORM` + `DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020`。

7. `ProtectionCapabilities` 只能作为诊断信号，不能作为唯一真相。

   实测中，进入 HDR 模式前后 HEVC/4K/HDR 能力报告会变化。它可以帮助诊断系统状态，但是否进入 HDR 仍应以 `HdmiDisplayInformation` 模式切换结果、`DisplayInformation.GetAdvancedColorInfo()`、native playback status 和电视侧 HDR 触发为准。

v38 证据：

- HDR 源日志：`C:\tmp\nextgen-v37-playback-diagnostics.log`，包含 `RequestSetCurrentDisplayModeAsync HDR get success`、`hdr=On active=True`、`swap=R10G10B10A2_UNORM`、`color=RGB_FULL_G2084_NONE_P2020`。
- HDR 后切 SDR 日志：`C:\tmp\nextgen-v38-hdr-then-sdr-diagnostics.log`，包含 `RequestSetCurrentDisplayModeAsync SDR get success`、`Native open end hdr=Off active=False`、`color=RGB_FULL_G22_NONE_P709`。
- 截图：`C:\tmp\nextgen-v38-hdr-then-sdr.png`。

## v39 Xbox 实测结论

v39 补齐了 Kodi-style DXGI 色彩映射和硬件转换验证的第一阶段闭环：

1. `VideoDecoder` 从 FFmpeg frame 继续向 native renderer 传递 primaries、transfer、matrix、range、bit-depth，并新增 chroma location。
2. `DxgiColorSpaceMapper` 按 Kodi `AvToDxgiColorSpace()` 的语义映射 BT.709、BT.601、BT.2020 PQ/HLG、full/limited range 和 `TOPLEFT`/`LEFT` chroma siting。
3. `DxDeviceResources` 在 `ID3D11VideoContext1` 路径上使用 `VideoProcessorSetStreamColorSpace1()` / `VideoProcessorSetOutputColorSpace1()`，并先通过 `ID3D11VideoProcessorEnumerator1::CheckVideoProcessorFormatConversion()` 验证转换组合。
4. BT.2020 PQ 默认按 Kodi 的 UHD-BD 习惯选择 `TOPLEFT`，如果硬件不支持会尝试 `LEFT` alternative；本机 Xbox 上 HDR10 样本的 `TOPLEFT` 直接通过验证。
5. 播放页 Info 和 `playback-diagnostics.log` 已暴露 `vpIn`、`vpOut`、`vpStatus`，不再只靠肉眼判断颜色。

v39 证据：

- HDR10 源日志：`C:\tmp\nextgen-v39-hdr-diagnostics.log`，包含 `Requested source found name=4K / 19 Mbps, HEVC - HDR10`、`RequestSetCurrentDisplayModeAsync HDR get success`、`Native open end hdr=On active=True display=True swap=R10G10B10A2_UNORM color=RGB_FULL_G2084_NONE_P2020 tenBit=True vp=True vpIn=YCBCR_STUDIO_G2084_TOPLEFT_P2020 vpOut=RGB_FULL_G2084_NONE_P2020 vpStatus=validated`。
- HDR10 源截图：`C:\tmp\nextgen-v39-hdr-screenshot.png`。
- SDR 源日志：`C:\tmp\nextgen-v39-sdr-diagnostics.log`，包含 `Requested source found name=4K / 18 Mbps, HEVC`、`RequestSetCurrentDisplayModeAsync SDR get success`、`Native open end hdr=Off active=False display=True swap=R10G10B10A2_UNORM color=RGB_FULL_G22_NONE_P709 tenBit=True vp=True vpIn=YCBCR_STUDIO_G22_LEFT_P709 vpOut=RGB_FULL_G22_NONE_P709 vpStatus=validated`。
- SDR 源截图：`C:\tmp\nextgen-v39-sdr-screenshot.png`。

## v41 Xbox 实测结论

v41 补齐了 HDR10 源输出到 SDR 时的第一版 Kodi-style shader tone mapping：

1. `DxgiColorSpaceMapper` 保留 Kodi 的策略：HDR10/PQ 源目标为 SDR 时，把 video processor 输入 transfer 临时映射成 G22 + BT.2020，以绕开 DXVA 自带 tone mapping，同时标记 `RequiresToneMapping=true`。
2. `DxDeviceResources` 在 `RequiresToneMapping && !outputHdr10` 时不再直接把 video processor 输出写入 swapchain，而是先写入中间纹理，再调用 `HdrToneMappingPass`。
3. `HdrToneMappingPass` 使用 Kodi `output_d3d.fx` 中的 PQ inverse EOTF + Hable tone mapping 公式；峰值亮度优先来自 HDR10 metadata，缺失时使用 Kodi 的 400 nits 默认值。
4. shader 成功后诊断状态从 `validated;requires-tone-mapping` 变为 `validated;tone-mapped-hable`。这表示 HDR->SDR 已经有显式 tone mapping 路径，而不是把错误颜色当成“可接受”。
5. HDR10 直出和 SDR 直出不经过该 shader pass，v41 Xbox 回归均保持 `vpStatus=validated`。

v41 证据：

- HDR10 -> SDR 强制诊断日志：`C:\tmp\nextgen-v41-hdr-force-sdr-tone-mapping-diagnostics.log`，包含 `Force SDR output for diagnostics`、`Native open enter ... isHdr=False`、`Native open end hdr=Off active=False ... vpIn=YCBCR_STUDIO_G22_TOPLEFT_P2020 vpOut=RGB_FULL_G22_NONE_P709 vpStatus=validated;tone-mapped-hable`。
- HDR10 正常直出日志：`C:\tmp\nextgen-v41-hdr-normal-diagnostics.log`，包含 `RequestSetCurrentDisplayModeAsync HDR get success`、`Native open end hdr=On active=True ... color=RGB_FULL_G2084_NONE_P2020 ... vpIn=YCBCR_STUDIO_G2084_TOPLEFT_P2020 vpOut=RGB_FULL_G2084_NONE_P2020 vpStatus=validated`。
- SDR 正常直出日志：`C:\tmp\nextgen-v41-sdr-diagnostics.log`，包含 `Requested source found name=4K / 18 Mbps, HEVC`、`Native open end hdr=Off active=False ... color=RGB_FULL_G22_NONE_P709 ... vpIn=YCBCR_STUDIO_G22_LEFT_P709 vpOut=RGB_FULL_G22_NONE_P709 vpStatus=validated`。

## 关键差异

v39 已经补齐了此前最大的四个差异：

1. 已使用 `ID3D11VideoContext1` 设置精确的 `DXGI_COLOR_SPACE_TYPE`。

   当前 `DxDeviceResources` 会在可用时调用 `VideoProcessorSetStreamColorSpace1` 和 `VideoProcessorSetOutputColorSpace1`，把输入和输出都表达成 DXGI color space。旧 `D3D11_VIDEO_PROCESSOR_COLOR_SPACE` 只保留给 legacy SDR fallback。

2. 已把 FFmpeg 的颜色元数据传入 DXGI color space mapper。

   `VideoColorMetadata` 已包含 primaries、matrix、transfer、range、bit-depth、chroma location 和 DV side-data 检测信号。`DxgiColorSpaceMapper` 会基于这些字段选择类似 `DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_TOPLEFT_P2020` 的输入色彩空间。

3. BT.2020/PQ/HLG 不再被简化成 BT.709 矩阵。

   HDR10 BT.2020 PQ 样本已实测走 `YCBCR_STUDIO_G2084_TOPLEFT_P2020`；SDR BT.709 样本已实测走 `YCBCR_STUDIO_G22_LEFT_P709`。

4. 已验证硬件是否支持某个输入/输出转换。

   当前路径会调用 `ID3D11VideoProcessorEnumerator1::CheckVideoProcessorFormatConversion()`。如果 primary chroma siting 不支持，会尝试 alternative；如果 HDR conversion 无法验证，不再把 legacy path 当作 HDR 正确输出。

仍然存在的关键差异：

5. Kodi 的 HDR 输出依赖 10-bit swapchain，这部分当前已经对齐到第一阶段。

   Kodi 在 HDR 或 10-bit surface 场景下优先使用 `DXGI_FORMAT_R10G10B10A2_UNORM`，并且只有在 swapchain 是 10-bit 且系统 HDR 打开时才把输出视为 HDR。当前 native 后端已经能在 HDR 输出时创建 10-bit swapchain，并设置 `DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020`。但这只说明输出 surface 状态对齐，不代表输入帧颜色空间和 HDR -> SDR 处理已经完整等价。

6. Kodi 对 HDR 源到 SDR 输出有专门策略。

   Kodi 在 BT.2020 + PQ 但目标为 SDR 时，会绕开 DXVA processor 自带 tone mapping，改用自己的 HDR 到 SDR tone mapping 策略。v41 已经补齐 HDR10/PQ -> SDR 的 Hable shader pass，但还没有补齐 Kodi 的多策略可选项、HLG->PQ、HLG->SDR 和更完整的 metadata/debug UI。

## 当前实现里相对正确的部分

- 已经可以在 Xbox 上触发 HDR10 display mode，电视侧会出现切换黑屏，native status 显示 `hdr=On active=True`。
- HDR 输出时 swapchain 已经是 10-bit `R10G10B10A2_UNORM`，color space 为 `RGB_FULL_G2084_NONE_P2020`，HDR10 输入帧会记录为 `YCBCR_STUDIO_G2084_TOPLEFT_P2020` 并经过 conversion validation。
- SDR 源播放前会显式离开 HDR，恢复到 `RGB_FULL_G22_NONE_P709`，SDR BT.709 输入帧会记录为 `YCBCR_STUDIO_G22_LEFT_P709` 并经过 conversion validation。
- Emby 侧 HDR 识别已经补充了 `ColorTransfer`、`ColorPrimaries`、`ColorSpace`，能更早识别 PQ/HLG/BT.2020 源。
- 播放源选择仍可在需要时避开 HDR 源；v41 后 HDR10/PQ -> SDR 已有显式 Hable tone mapping，但 HLG、DV fallback 和更多样本仍需要保守选择策略。

这些是正确方向，但不是完整 Kodi 等价实现。

## Dolby Vision 处理边界

结论：不支持 Dolby Vision passthrough，只做识别和 HDR10/HLG fallback。这个策略和 Kodi Windows/Xbox renderer 的边界一致：Kodi 能识别 Dolby Vision，但 Windows/Xbox 渲染状态只有 SDR、HDR10、HLG，没有 Dolby Vision 输出状态。

Kodi 中可参考的 DV 行为分三层：

1. Demux 层识别 DV。Kodi 在 `DVDDemuxFFmpeg.cpp` 检查 `AV_PKT_DATA_DOVI_CONF`，把流标记为 `StreamHdrType::HDR_TYPE_DOLBYVISION`，并保存 `AVDOVIDecoderConfigurationRecord`，包括 profile、level、RPU、compatibility id 等信息。
2. Android / webOS 平台管线处理 DV。Kodi 的 `videoplayer.convertdovi`、`videoplayer.allowedhdrformats`、`videoplayer.dovizerolevel5` 只在 Android / webOS 等使用 `CBitstreamConverter` 的平台显示。webOS 会设置 `DolbyHdrInfo`，也能移除 DV metadata、移除 HDR10+、把 Profile 7 RPU 转 Profile 8.1，或清零 Level 5 metadata。
3. Windows/Xbox DXVA renderer 不输出 DV。Kodi Windows renderer 的 `HDR_TYPE` 只有 `HDR_NONE_SDR`、`HDR_HDR10`、`HDR_HLG`，`ActualRenderAsHDR()` 只把 HDR10/HLG 视为 HDR 输出。

因此 Xbox 端第一阶段策略：

- DV Profile 8 且 base layer 兼容 HDR10：按 `DolbyVisionWithHdr10Fallback` 处理，实际走 HDR10/PQ 管线。
- DV Profile 8 且 base layer 兼容 HLG：按 `DolbyVisionWithHlgFallback` 处理，实际走 HLG 或 HLG-to-PQ 策略。
- DV Profile 7 且能确认 HDR10 base layer：丢弃或忽略 enhancement layer / RPU，尽量保留 HDR10 base layer 播放。
- DV Profile 5：标记为 `DolbyVisionUnsupported`，不允许静默按 HDR10 播放，避免粉紫、绿屏或黑屏。
- HDR10+ + DV hybrid：优先寻找 HDR10 fallback，不能因为动态 metadata 让 native renderer 进入未知状态。

选流优先级：

1. 普通 SDR。
2. 普通 HDR10。
3. HLG。
4. DV with HDR10 fallback。
5. DV with HLG fallback。
6. Pure DV unsupported。

诊断至少要显示：原始 HDR 类型、DV profile、base layer compatibility id、实际播放策略、是否移除 DV metadata、最终输入/输出 DXGI color space。

## 要做到 Kodi 等价，需要补齐的代码边界

1. 在 `DecodedVideoFrame` 中保留完整颜色元数据：primaries、color space/matrix、transfer、range、chroma location、DXGI/AV pixel format。
2. 增加 Kodi `AvToDxgiColorSpace` 同级别的映射函数，覆盖 BT.709、BT.601、BT.2020 PQ、BT.2020 HLG、full/limited、top-left/left chroma siting。
3. 在 native renderer 中查询 `ID3D11VideoContext1` 和 `ID3D11VideoProcessorEnumerator1`，优先使用 `VideoProcessorSetStreamColorSpace1` / `VideoProcessorSetOutputColorSpace1`。
4. 用 `CheckVideoProcessorFormatConversion()` 验证 conversion，记录输入 CS、输出 CS、输出格式和是否支持。
5. 继续保持 Xbox 上 10-bit swapchain + HDR display 状态成立时才设置 `DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020`，同时避免 UI/SwapChainPanel 路径误用 Kodi CoreWindow 专用的 swap effect。
6. 对 HDR 源 + SDR 输出实现明确 tone mapping，不能让旧 D3D11 video processor 参数隐式决定。v41 已完成 HDR10/PQ -> SDR 的 Hable shader pass；HLG 和更多 tone mapping 策略待补。
7. 增加 DV fallback 分类和诊断：`Sdr`、`Hdr10`、`Hlg`、`DolbyVisionWithHdr10Fallback`、`DolbyVisionWithHlgFallback`、`DolbyVisionUnsupported`、`UnknownHdr`。
8. 在诊断面板暴露：输入 primaries/matrix/transfer/range、输入 DXGI CS、输出 DXGI CS、swapchain format、HDR display 状态、conversion supported 结果。

## 判断标准

后续不能再用“看起来差不多”作为颜色正确性的验收标准。至少要满足：

- SDR BT.709 源：输入 `YCBCR_*_G22_*_P709`，输出 `RGB_*_G22_NONE_P709`。
- HDR10 BT.2020 PQ 源 + HDR 输出：输入 `YCBCR_STUDIO_G2084_*_P2020`，输出 `RGB_FULL_G2084_NONE_P2020`，swapchain 为 `R10G10B10A2_UNORM`。
- HDR10 BT.2020 PQ 源 + SDR 输出：代码中存在明确 tone mapping 路径，不能只依赖旧 `D3D11_VIDEO_PROCESSOR_COLOR_SPACE`。
- DV Profile 8.1 + HDR10 base：识别为 DV，但实际策略为 HDR10 fallback，输出 HDR10/PQ。
- DV Profile 5：识别为 unsupported，不允许静默按 HDR10 direct play。
- 所有硬件转换组合必须经过 `CheckVideoProcessorFormatConversion()` 或等价验证。
