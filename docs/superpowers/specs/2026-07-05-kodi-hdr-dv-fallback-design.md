# Kodi HDR 与 Dolby Vision Fallback 规范

日期：2026-07-05

## 状态

已接受。后续任何涉及 Xbox 原生播放、HDR、HEVC Main10、颜色空间、DXGI swapchain、Dolby Vision 片源兼容的实现计划，都必须先阅读本规格。

详细代码对照见：`docs/kodi-color-pipeline-comparison.md`。

2026-07-05 v38 更新：Xbox 真机已验证 HDR10 display mode 可以成功切换，HDR 输出状态为 `R10G10B10A2_UNORM` + `RGB_FULL_G2084_NONE_P2020`；HDR 后切 SDR 已验证可以回到 `RGB_FULL_G22_NONE_P709`。这只代表输出层打通，完整 Kodi 等价仍需补齐输入 DXGI color space、conversion validation 和 HDR -> SDR tone mapping。

## 目标

Xbox 端原生播放器以 Kodi Windows/Xbox DirectX 路径为主要参考，实现可靠的 HDR10/PQ 输出，并在遇到 Dolby Vision 片源时安全降级到 HDR10 或 HLG base layer。

本项目不实现 Dolby Vision passthrough。

## 不可变约束

1. 不靠肉眼判断颜色正确性。必须通过代码路径、DXGI color space、swapchain format、HDR display state 和 conversion validation 证明。
2. HDR10 输出必须以 Kodi 的 DXVA/DXGI 路径为参考：完整颜色 metadata、`DXGI_COLOR_SPACE_TYPE` 映射、10-bit swapchain、`CheckVideoProcessorFormatConversion()` 或等价验证。
3. Xbox HDR/SDR 切换时不能按桌面 Windows 思路随意重建 swapchain。Kodi 在 Xbox 上保留同一个 swapchain 并切 color space，是因为重建可能导致 4K 输出质量退化。
4. Windows/Xbox renderer 不设计 Dolby Vision 输出状态。可识别 DV，但最终只进入 SDR、HDR10 或 HLG/HDR10-compatible 路径。
5. Pure Dolby Vision 不允许静默按 HDR10 播放。

## HDR10 等价要求

后续实现必须补齐：

- `DecodedVideoFrame` 保留 primaries、matrix/color space、transfer、range、chroma location、pixel format。
- FFmpeg AV color metadata 到 `DXGI_COLOR_SPACE_TYPE` 的映射，覆盖 BT.709、BT.601、BT.2020 PQ、BT.2020 HLG、full/limited、left/top-left chroma siting。
- `ID3D11VideoContext1::VideoProcessorSetStreamColorSpace1()` 和 `VideoProcessorSetOutputColorSpace1()` 优先路径。
- `ID3D11VideoProcessorEnumerator1::CheckVideoProcessorFormatConversion()` 或等价验证。
- HDR 输出使用 10-bit `DXGI_FORMAT_R10G10B10A2_UNORM` swapchain。
- HDR display 打开且 swapchain 为 10-bit 时，才设置 `DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020`。
- SDR 恢复使用 `DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709`。

已验证的 Xbox 输出层规则：

- App manifest 必须保留 `rescap:Capability Name="hevcPlayback"`。
- HDR display mode 选择以 `HdmiDisplayColorSpace::BT2020` + 同刷新率为核心条件，不把 `IsSmpte2084Supported()` 当硬门槛。
- `RequestSetCurrentDisplayModeAsync()` 在 STA/UI 线程上必须通过 Completed 回调和同步事件等待，不能直接 `.get()`。
- SDR 源播放前必须显式请求 `HdmiDisplayHdrOption::None`，不能把当前 HDR 状态当成可恢复的默认初始状态。
- SwapChainPanel 路径保持 `DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL`；Kodi CoreWindow 的 flip discard 选择不能直接照搬到当前 XAML composition 路径。

## Dolby Vision Fallback 要求

必须新增内部分类：

- `Sdr`
- `Hdr10`
- `Hlg`
- `DolbyVisionWithHdr10Fallback`
- `DolbyVisionWithHlgFallback`
- `DolbyVisionUnsupported`
- `UnknownHdr`

判定规则：

- 存在 `AV_PKT_DATA_DOVI_CONF` 或 Emby metadata 明确包含 Dolby Vision / DV / DOVI 时，进入 DV 分支。
- DV Profile 8 且 base layer 兼容 HDR10 时，走 `DolbyVisionWithHdr10Fallback`。
- DV Profile 8 且 base layer 兼容 HLG 时，走 `DolbyVisionWithHlgFallback`。
- DV Profile 7 且能确认 HDR10 base layer 时，丢弃或忽略 enhancement layer / RPU，尽量保留 HDR10 base layer 播放。
- DV Profile 5 标记为 `DolbyVisionUnsupported`。
- HDR10+ + DV hybrid 不能把 renderer 带入未知状态；优先寻找 HDR10 fallback。

选流优先级：

1. 普通 SDR。
2. 普通 HDR10。
3. HLG。
4. DV with HDR10 fallback。
5. DV with HLG fallback。
6. Pure DV unsupported。

## 诊断要求

媒体详情页、播放页诊断和 dev command 至少展示：

- 原始 HDR 类型。
- DV profile、compatibility id、是否存在 RPU / enhancement layer。
- 实际播放策略，例如 `Native HDR10`、`HLG to PQ`、`HDR10 fallback from Dolby Vision`、`Unsupported pure Dolby Vision`。
- 输入 primaries/matrix/transfer/range/chroma location。
- 输入 DXGI color space、输出 DXGI color space。
- swapchain format。
- HDR display state。
- conversion validation result。

## 验收样本

至少覆盖：

- 普通 4K HEVC HDR10 Main10：HDR10/PQ 输出。
- DV Profile 8.1 + HDR10 base：识别为 DV，实际 HDR10 fallback。
- DV Profile 8.4 + HLG base：识别为 DV，实际 HLG fallback 或 HLG-to-PQ。
- DV Profile 7 FEL/MEL + HDR10 base：不输出 DV，保留 HDR10 base layer，失败时错误明确。
- DV Profile 5：不直放，显示 unsupported。
- HDR10+ + DV hybrid：不黑屏，不进入未知 HDR 状态。

## 后续计划入口

下一份 implementation plan 应以本规格和 `docs/kodi-color-pipeline-comparison.md` 为输入，先实现 metadata 分类与诊断，再实现 Kodi-style DXGI color pipeline，最后接 DV fallback bitstream/filter 行为。
