# 独立 D3D11 解码管线实施计划

## 目标

在不改变 HDR、音频主时钟、seek、切轨和字幕语义的前提下，将 D3D11VA 解码从 App 呈现 device 的 immediate context 中分离。解码 worker 在同一 DXGI adapter 的独立 device 上生产有界视频帧，通过共享 texture 和 fence 交给现有 render thread，目标是让 FFmpeg `avcodec_send_packet` 与上一帧的渲染/呈现重叠，而不是继续串行相加。

## 已有证据

- v0.13 三个公开 1080p60 HEVC case 的 packet read、receive frame、frame materialize、render 和 present 都不是主耗时。
- `avcodec_send_packet` P50 约 `12-13ms`，P95 约 `15-19ms`，与固定 render-loop wait 串行后导致 30 秒窗口只能推进约 `26-27.5s`。
- 同一 device 上的 worker 会竞争 D3D immediate context；单帧确认只能消除竞争，不能形成并行。
- `extra_hw_frames = 4` 没有稳定缩短 send-packet，已撤回。
- Kodi Xbox DXVA 使用独立 decoder context、共享 decoder surface，并在可用时用共享 fence 同步；不共享时退回显式 copy。

## 强制边界

1. 队列容量固定为 3；不允许按码率或运行时长无界增长。
2. 独立 decoder device 必须来自 App device 的同一 DXGI adapter。
3. FFmpeg decoder texture 必须显式带共享资源标志；render device 只打开共享资源，不直接使用另一个 device 的 COM texture。
4. 每帧携带单调递增 fence value；render context 必须在读取共享 surface 前等待对应 fence。
5. 若独立 device、共享 texture 或 fence 任一能力不可用，整条异步路径关闭并回退到当前同步路径；禁止半异步、无同步共享。
6. seek、stop、音轨切换和字幕切换必须先停止 worker、清空旧 generation，再 flush/close decoder。
7. HDR metadata、DXGI input/output color space、tone mapping 和 display refresh 决策保持在现有 render thread。
8. 报告必须记录实际采用的 decode device mode、共享同步 mode、队列深度/等待/underrun；不能仅凭配置宣称异步路径生效。

## 实施顺序

### 1. 能力验证

- 新增真实 D3D11 测试：同 adapter 创建第二 device；创建共享 NV12/P010 array texture；在 App device 打开；创建并共享 fence；由 decoder context signal、render context wait。
- 若本机驱动不支持某格式，报告明确 unsupported，不把 capability 缺失当成播放器 pass。

### 2. 共享帧桥

- 新增只负责 device/texture/fence ownership 的小型 native 组件。
- 输入 decoder texture + array index，输出 render-device texture + array index + fence value。
- 同一 decoder texture 只打开共享 handle 一次并缓存；Close/seek generation reset 时释放缓存。

### 3. FFmpeg 独立 device

- `VideoDecoder::Open` 尝试创建同 adapter decoder device，并给 `AVD3D11VADeviceContext` 设置共享 texture flags。
- 只有共享帧桥和 fence 全部初始化成功时才使用独立 device，否则回退现有 App device。
- 每个 decoded frame 在交给队列前 signal fence，并携带明确 mode 证据。

### 4. Worker 接入

- 复用现有 `VideoDecodeWorker` 与容量 3 的 generation-aware queue。
- render thread 只消费 ready frame、等待 fence、执行现有时钟/drop/HDR/render/present。
- worker failure、EOS、stop 和 generation reset 使用已有有界状态，不跨线程抛异常。

### 5. 评测与接受条件

- 固定 v0.13 manifest、30 秒窗口、90 秒 attempt timeout，不修改 expected 和阈值。
- 首轮使用同三个公开 1080p60 case；至少重复三次 SDR 和 HDR。
- 候选必须同时满足：send-packet 不再阻塞 render thread；三项媒体推进覆盖请求窗口；P95/P99/max cadence 无显著回归；无新增 dropped/starvation/failure。
- 通过后再跑完整 native smoke、私有 Emby 代表 case、完整 Modern App 编译和 App-hosted 播放；最后才安排 Xbox HDR 实机复核。
- 任一关键条件不满足，撤回行为代码，保留 capability test、v0.13 证据和拒绝结论。
