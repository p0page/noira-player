# 视频渲染阶段证据与资源复用设计

## 目标

先把 `VideoRenderer::Render` 内 11-15ms 的 CPU 提交耗时拆成可归因阶段，再决定是否复用 D3D11 video processor 资源。该工作只服务播放器 Core 软件闭环，不声称验证 GPU 完成时间、HDMI 输出或面板颜色。

## 现状证据

- v0.15 同 revision 下，本地 SDR 60fps 的 video render P95 为 `0.288ms`，本地 HDR10 到 SDR 为 `14.398ms`。
- 公开 1080p 原生 SDR 与 HDR10 到 SDR 的 P95 分别约 `12.924ms` 与 `15.210ms`，说明 Hable shader 不是唯一成本。
- `DxDeviceResources::TryProcessVideoFrameToBackBuffer` 每帧创建 enumerator、processor、input/output view；tone-map 路径还每帧创建 intermediate texture、SRV 和 RTV。
- Kodi 在 processor 打开/重配时保存 enumerator 与 processor，帧循环只创建与当前 surface 绑定的 view。这里借用资源生命周期原则，不复制 Kodi 的完整 DXVA 架构。

## 方案比较

### A. 直接缓存所有资源

改动快，但当前只有总 render 时长，无法证明收益来自哪一项，也难以解释无改善或驱动差异。拒绝。

### B. 先增加阶段证据，再做最小缓存候选

推荐。先测量 processor setup、view/target、clear、blit 和 post-process 的 CPU 调用耗时与路径计数；用默认行为建立同 manifest baseline。只有 setup 明确占主导才缓存 enumerator/processor。证据提交与策略提交分开，可独立回退。

### C. 重写为独立渲染管线对象

长期结构更整洁，但会同时改变资源所有权、颜色状态和设备恢复，超出当前单变量调优边界。暂不采用。

## 证据契约

每次渲染返回 `VideoRenderPhaseSample`，记录路径与非重叠 CPU 提交阶段：

- `processorSetupCpuMs`：获取 video interfaces、创建 enumerator/processor、检查 format conversion 和设置 processor 状态；
- `viewTargetCpuMs`：创建 input view、intermediate texture 和 output view；
- `clearCpuMs`：提交目标清屏；
- `blitCpuMs`：调用 `VideoProcessorBlt`；
- `postProcessCpuMs`：提交 HDR tone-map/HLG shader；
- `directCopyFrames`、`videoProcessorFrames`、`bgraFrames`、`postProcessFrames`：证明实际渲染路径。

报告对每个阶段保存 P50/P95/P99/Max 和 sample count。字段明确是 CPU API 调用/提交耗时，不得解释为 GPU 执行完成时间。完成态 native playback 必须带这些字段；因此 evaluation version 升为 v0.16，旧 v0.15 只作历史诊断，不与 v0.16 candidate 混比。

## 最小缓存候选

第一候选只复用 enumerator 与 processor。cache key 包含输入/输出宽高、输入/输出 DXGI format 和 content usage；key 变化、device 重建或 swapchain 重建立即失效。颜色空间、source/destination rect 和 HDR metadata 仍按帧设置；input/output view 和 tone-map intermediate target 暂不缓存，避免同时改变多个变量。

若第一候选证明 setup 显著下降且无回归，再把 intermediate target 复用作为第二个独立候选。不得在同一 candidate 中关闭 auto processing、删除 clear、修改 tone-map shader或缓存无界 input texture view。

## 验证与采纳

1. 证据版先跑完整 Core/native gate，并生成默认 5ms 的 v0.16 baseline。
2. 同 manifest 至少覆盖公开 HDR10 force-SDR、公开 SDR 1080p60、本地 HDR10/SDR 60fps、私有 DV8 fallback，以及 timeline/track/subtitle/color case。
3. 候选必须无新增 error、unsupported、timeline、track、subtitle 或 color regression。
4. 目标 case 的 setup P95 和 video render P95 必须在重复运行中稳定下降；只多推进一帧或单次总分改善不够。
5. 采纳后运行完整 App build 和代表性 App-hosted 播放；没有策略候选被采纳时不做无意义的 App 实播。

