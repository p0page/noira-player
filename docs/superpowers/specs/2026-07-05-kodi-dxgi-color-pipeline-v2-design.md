# Kodi DXGI 颜色管线 v2 设计

日期：2026-07-05

## 背景

v38 已在 Xbox 真机验证 HDR10 display mode 切换和 10-bit HDR swapchain 输出：HDR 源最终为 `R10G10B10A2_UNORM` + `RGB_FULL_G2084_NONE_P2020`，HDR 后切 SDR 能回到 `RGB_FULL_G22_NONE_P709`。但这只证明输出层打通，不能证明输入帧颜色空间、硬件转换和 HDR->SDR 处理已经与 Kodi 等价。

本设计以本地拉取的 Kodi `master` 源码为参考：

- `xbmc/cores/VideoPlayer/VideoRenderers/HwDecRender/DXVAEnumeratorHD.cpp`
- `xbmc/cores/VideoPlayer/VideoRenderers/HwDecRender/DXVAHD.cpp`
- `xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp`
- `xbmc/rendering/dx/DeviceResources.cpp`
- `xbmc/windowing/win10/WinSystemWin10.cpp`

## Kodi 对照结论

1. Kodi 的核心不是“看到 HDR 就设置 swapchain PQ”，而是先把 FFmpeg 的 `primaries / matrix / transfer / range / chroma location` 映射到 `DXGI_COLOR_SPACE_TYPE`。
2. Kodi 的 `AvToDxgiColorSpace()` 对 BT.2020/PQ 有两个关键约束：
   - PQ + YCbCr full range 没有可用 DXGI 枚举，返回 `DXGI_COLOR_SPACE_CUSTOM`，表示不能证明转换。
   - UHD-BD / HLG 默认 chroma siting 是 `TOPLEFT`，如果 conversion 不支持，再尝试 `LEFT`。
3. Kodi 在 `SupportedConversions()` 里先做映射例外：
   - HDR10 PQ 源输出 SDR 时，把输入 transfer 临时视为 BT.709，绕开 DXVA processor 自带 tone mapping，再由自己的 shader 做 tone mapping。
   - HLG 输出 HDR 时，把输入 transfer 改成 PQ，因为 Windows/Xbox 没有 HLG passthrough 输出状态。
   - HLG 输出 SDR 时，把输入 transfer 改成 BT.709。
4. Kodi 使用 `ID3D11VideoProcessorEnumerator1::CheckVideoProcessorFormatConversion()` 验证输入格式、输入 color space、输出格式、输出 color space 的组合，验证失败不应被当成颜色正确。
5. Kodi Windows/Xbox renderer 的输出 HDR 类型只有 SDR、HDR10、HLG。Dolby Vision 只做识别和 fallback，不存在 DV 输出状态。

## 本阶段目标

实现“可证明的 DXGI 颜色处理层”，但不在这一阶段实现完整 shader tone mapping。

必须做到：

- `DecodedVideoFrame` 携带 `ChromaLocation`。
- `DxgiColorSpaceMapper` 按 Kodi `AvToDxgiColorSpace()` 语义处理 BT.709、BT.601、BT.2020 PQ、BT.2020 HLG、full/limited、left/top-left chroma siting。
- HDR10 PQ full-range YCbCr 返回不支持，不伪装为可转换。
- BT.2020 PQ/HLG 默认 `TOPLEFT`，conversion 不支持时尝试 `LEFT`。
- 每次 video processor 渲染记录输入 DXGI color space、输出 DXGI color space、转换是否验证、失败原因。
- HDR 输出只在 display HDR active、10-bit swapchain、HDR color space 设置成功时启用。
- HDR 源输出 SDR 时标记 `RequiresToneMapping` / `HdrToSdrToneMappingMissing`，不宣称颜色等价。

明确不做：

- 不实现 Dolby Vision passthrough。
- 不引入 libdovi。
- 不在这一阶段写完整 GPU shader tone mapper。
- 不为了桌面 Windows 优化重构 Xbox 的 SwapChainPanel 路径。

## 组件设计

### `VideoColorMetadata`

扩展 native metadata：

- `ColorPrimaries`
- `ColorTransfer`
- `ColorSpace`
- `ColorRange`
- `ChromaLocation`
- `BitsPerChannel`
- `HasDolbyVisionMetadata`

`VideoDecoder` 从 `AVFrame` 填充这些字段。`ChromaLocation` 使用 `frame->chroma_location`，缺省值保留 `AVCHROMA_LOC_UNSPECIFIED`。

### `DxgiColorSpaceMapper`

新增结构：

- `DxgiVideoColorSpaceMapping`
  - `InputColorSpace`
  - `OutputColorSpace`
  - `AlternativeInputColorSpace`
  - `HasAlternativeInputColorSpace`
  - `IsSupported`
  - `IsHdr10`
  - `IsHlg`
  - `NeedsTenBitOutput`
  - `RequiresToneMapping`
  - `Reason`

映射策略：

- RGB：按 full/studio、BT.2020/BT.709、PQ/G22 映射。
- BT.2020 + PQ：
  - full range：`IsSupported=false`，原因 `No DXGI full-range PQ YCbCr color space`。
  - limited range：`TOPLEFT` -> `YCBCR_STUDIO_G2084_TOPLEFT_P2020`，`LEFT` -> `YCBCR_STUDIO_G2084_LEFT_P2020`。
  - unspecified 默认 `TOPLEFT`，alternative 为 `LEFT`。
- BT.2020 + HLG：
  - HDR 输出：按 Kodi 语义把 input transfer 当 PQ，用 HDR10/PQ 输出。
  - SDR 输出：把 input transfer 当 G22，标记 `RequiresToneMapping=true`。
- BT.2020 + SDR transfer：按 G22 P2020 映射。
- BT.601：按 `YCBCR_*_G22_LEFT_P601`。
- JPEG/SMPTE170M 特例：full range 可映射到 `YCBCR_FULL_G22_NONE_P709_X601`，limited range 不声明支持。
- 默认 HDTV：P709 left。

### `DxDeviceResources`

`TryProcessVideoFrameToBackBuffer()` 改为：

1. 生成 mapping。
2. 如果 mapping 不支持，记录诊断并返回 false。
3. 使用 `CheckVideoProcessorFormatConversion()` 验证 primary input color space。
4. 如果失败且有 alternative input color space，验证 alternative。
5. 验证成功后使用 `VideoProcessorSetStreamColorSpace1()` 和 `VideoProcessorSetOutputColorSpace1()`。
6. 记录最后一次 input/output color space、conversion status、失败原因。

如果 `ID3D11VideoContext1` 或 `ID3D11VideoProcessorEnumerator1` 不存在：

- SDR 可以走 legacy color space，但诊断必须显示 `legacy-unvalidated`。
- HDR 不走 legacy，不宣称 HDR 正确。

### 诊断

`NativePlaybackStatus` 和 C# `PlaybackDisplayStatus` 增加：

- `VideoProcessorInputColorSpace`
- `VideoProcessorOutputColorSpace`
- `VideoProcessorConversionStatus`

播放页 Info 和诊断日志显示这些字段。验收时不能只看电视画面，要看：

- HDR10 源 + HDR 输出：`YCBCR_STUDIO_G2084_*_P2020 -> RGB_FULL_G2084_NONE_P2020`，conversion validated。
- SDR 源：`YCBCR_*_G22_*_P709/P601 -> RGB_FULL_G22_NONE_P709`。
- HDR 源 + SDR 输出：显示需要 tone mapping，不把它标为 Kodi 等价。

## 执行顺序

1. 写 C# 诊断模型测试，锁定新增字段能从 backend 传到 `PlaybackDisplayStatus`。
2. 扩展 native IDL/status 和 C# 映射。
3. 扩展 `VideoColorMetadata` 的 chroma location。
4. 改写 `DxgiColorSpaceMapper`。
5. 改写 conversion validation，加入 alternative chroma siting。
6. 构建并部署 Xbox，验证 HDR10 样本仍能进入 HDR，且日志显示 input/output DXGI color space 和 validated。

## 执行结果

v39 已按本设计完成第一阶段落地：

- C# 诊断模型、native WinRT status、播放页 Info 与日志均已携带 `VideoProcessorInputColorSpace`、`VideoProcessorOutputColorSpace`、`VideoProcessorConversionStatus`。
- `VideoColorMetadata` 已从 FFmpeg frame 携带 `ChromaLocation`。
- `DxgiColorSpaceMapper` 已按 Kodi 语义处理 BT.709、BT.601、BT.2020 PQ/HLG、full/limited range、`TOPLEFT`/`LEFT` chroma siting、PQ full-range YCbCr 不支持、HDR->SDR 需要 tone mapping 标记。
- `DxDeviceResources` 已在 `ID3D11VideoContext1` 路径上调用 `CheckVideoProcessorFormatConversion()`，primary 失败时尝试 alternative input color space，并记录最终状态。
- Xbox v39 HDR10 源实测：`YCBCR_STUDIO_G2084_TOPLEFT_P2020 -> RGB_FULL_G2084_NONE_P2020`，`vpStatus=validated`，`hdr=On active=True`。
- Xbox v39 SDR 源实测：`YCBCR_STUDIO_G22_LEFT_P709 -> RGB_FULL_G22_NONE_P709`，`vpStatus=validated`，`hdr=Off active=False`。

v41 在本设计基础上继续补齐了 HDR10/PQ -> SDR 的第一版 shader tone mapping：

- 保留 Kodi DXVA bypass 语义：HDR10/PQ 源目标为 SDR 时，video processor 输入 transfer 临时映射为 G22 + BT.2020，以避免 DXVA 自带 tone mapping。
- 新增 `HdrToneMappingPass`，让 video processor 先输出到中间纹理，再使用 Kodi `output_d3d.fx` 同源的 PQ inverse EOTF + Hable tone mapping 公式输出到 SDR swapchain。
- Xbox v41 HDR10 源强制 SDR 诊断实测：`YCBCR_STUDIO_G22_TOPLEFT_P2020 -> RGB_FULL_G22_NONE_P709`，最终 `vpStatus=validated;tone-mapped-hable`，日志为 `C:\tmp\nextgen-v41-hdr-force-sdr-tone-mapping-diagnostics.log`。
- Xbox v41 HDR10 正常 HDR 输出回归仍为 `YCBCR_STUDIO_G2084_TOPLEFT_P2020 -> RGB_FULL_G2084_NONE_P2020`，`vpStatus=validated`，`hdr=On active=True`。
- Xbox v41 SDR 输出回归仍为 `YCBCR_STUDIO_G22_LEFT_P709 -> RGB_FULL_G22_NONE_P709`，`vpStatus=validated`，`hdr=Off active=False`。

仍不宣称完整 Kodi 等价：HLG->PQ、HLG->SDR、DV fallback 真源、更多音轨/字幕/帧率组合、tone mapping 可调策略和 native mapper 独立 C++ 单元测试工程仍未补齐。

## 风险

- 当前没有 native 单元测试工程，C++ 映射只能通过构建、诊断日志和 Xbox 实机验证闭环。后续如果这块继续扩张，应建立一个 native test project，把 mapper 从 WinRT/D3D runtime 里进一步隔离出来。
- HDR->SDR tone mapping 本阶段只做显式识别和防误报。真正补齐需要 shader path 或复用现有 shader/filter 架构。
- Xbox `SwapChainPanel` 路径必须保留 `FLIP_SEQUENTIAL`，不能照搬 Kodi CoreWindow 的 `FLIP_DISCARD`。
