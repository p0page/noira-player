# 环境感知的颜色期望设计

## 背景

`playback-quality-v0.3` 的当前 corpus baseline 在 24 份真实报告中得到 10 个 fail，其中 8 个主要来自 HDR 素材在 offscreen/headless 环境中的颜色期望不适用。报告已经证明源为 `smpte2084/bt2020`，但无 HDR 显示时播放器按 Kodi 的 DXVA bypass 策略使用 G22/P2020 video-processor 输入，再由 Hable shader 完成 PQ 到 SDR 的 tone mapping。manifest 仍无条件要求 HDR10 输出、10-bit swapchain 和 G2084/P2020，因此把正确的 SDR fallback 误归为 `player-core bug`。

本设计只修正评测环境适用性，不改变播放器颜色策略，不降低 HDR-capable 环境的标准。

## 方案比较

### 方案 A：evaluator 隐式推导 fallback 期望

根据 `display.isHdrDisplayAvailable=false` 自动把 HDR expected 改成 SDR。实现最小，但规则藏在 evaluator 中，manifest 无法完整描述期望，模型也难以追踪规则变化。拒绝。

### 方案 B：软件环境直接 skip HDR case

可以避免假 fail，但会丢失已经可闭环的 HDR 解码、BT.2020 矩阵、DXVA bypass 和 tone-mapping 证据，也会让 buffering、DV fallback 等复合 case 无法评测。拒绝。

### 方案 C：manifest 显式声明 SDR 显示 fallback

保留主颜色期望，并为需要自适应输出的 case 增加可选 `expected.sdrDisplayFallback`。evaluator 根据报告中的显示能力和 force-SDR 状态选择唯一 profile，并把选择写入报告/check 证据。采用此方案。

## 数据契约

`PlaybackQualityExpected` 新增可选对象：

```json
{
  "sdrDisplayFallback": {
    "hdrOutput": "Sdr",
    "dxgiInput": "YCBCR_STUDIO_G22_TOPLEFT_P2020",
    "dxgiOutput": "RGB_FULL_G22_NONE_P709",
    "isTenBitSwapChain": false,
    "requireValidatedConversion": true,
    "requiredConversionStatus": "tone-mapped-hable"
  }
}
```

主 expected 继续用于 HDR 输出可用且未强制 SDR 的运行。fallback 仅在以下任一条件成立时选择：

1. `colorPipeline.forceSdrOutput=true`；
2. `display.isHdrDisplayAvailable=false` 且主期望要求 HDR10 输出。

fallback 对象不允许再嵌套 fallback，也不承载 codec、尺寸、帧率、生命周期或性能阈值；这些仍由主 expected 统一所有环境。

## 评估规则

1. 每份报告只选择一个颜色 profile：`primary` 或 `sdr-display-fallback`。
2. 选择 fallback 时，颜色 checks 消费 fallback 字段；source、startup、timing、sync、buffering、track、subtitle 和 timeline 仍消费主 expected。
3. 主期望要求 HDR10，但环境需要 fallback 且 manifest 未声明 fallback 时，报告必须 fail，错误为缺少环境适用期望，不能自动推导或 skip。
4. `requiredConversionStatus` 使用精确的分号 token 匹配，`tone-mapped-hable` 不能被 `requires-tone-mapping` 代替。
5. evaluator 生成 `colorPipeline.expectationProfile` observed check，并由 analyzer 暴露；模型必须能看出软件报告验证的是 SDR fallback，而不是真实 HDR 输出。
6. HDR-capable App-hosted 报告继续选择 `primary`，既有 HDR10、10-bit 和 PQ/P2020 标准不变。
7. force-SDR case 无论显示器能力如何都选择 fallback。

## Manifest 调整

公开和私有 HDR/DV fallback case 在主 expected 下补充相应的 `sdrDisplayFallback`。公开 4K buffering case保留 buffering 阈值，同时显式描述无 HDR 显示时的颜色 fallback，避免无关颜色环境阻塞 buffering 结论。

本地生成 HDR case 也使用同一数据契约；不得通过删除 HDR expected、改成 SDR 素材或放宽阈值制造 pass。

## 验证

1. 单元测试先证明：同一 expected 在 HDR-capable 报告中选择 primary，在无 HDR 显示或 force-SDR 报告中选择 fallback。
2. 缺 fallback、错误 DXGI input/output、未完成 tone mapping、错误 swapchain 位深均必须产生 fail。
3. 重跑当前 24-case corpus，要求 24/24 manifest/report 对应和 strict validation 保持有效。
4. 公开/私有 HDR 软件报告只能据 fallback checks 改变颜色结论；startup 等原有失败必须保留。
5. 最后用 HDR-capable App-hosted/Xbox 报告确认 primary 路径未被 fallback 掩盖；若当前阶段无硬件环境，只能声明该门禁仍待硬件验证，不能声称 HDR 输出通过。

## 边界

本设计不验证 HDMI InfoFrame、电视 EOTF、面板亮度或色准，也不修改 DV 支持范围、tone-map 算法、HDR 输出切换和 Xbox 显示代码。它只保证软件评测器按报告中已经观测到的输出环境使用 manifest 明示的颜色期望。
