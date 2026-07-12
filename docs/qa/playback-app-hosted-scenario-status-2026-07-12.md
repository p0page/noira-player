# App-hosted 场景评测与 10-bit PGS 状态

## 本轮结论

评测计划中的 scenario 现在会从 reference manifest 一路传到 run plan、dev-command、PlaybackLaunchRequest 和 App capture。App 不再对所有 case 固定执行“暂停、恢复、seek”，而是只执行当前 case 声明的 playback、timeline、pause-resume、audio-switch 或 subtitle-switch 场景。quality-run 缺少或使用未知 scenario 时会直接拒绝，Emby item 与 direct URI 均可进入同一 native PlaybackPage。

字幕切换的通过条件不再只是轨道元数据或 selected index。报告新增 tracks.subtitleDecodedCueCount 和 tracks.subtitleCueRenderCount，subtitle-switch 必须同时具有真实 lifecycle event、选中轨道和至少一个实际渲染 cue；headless materializer 也会把 helper 的 cue 计数写入结构化报告。

私有 PGS case 首次 App-hosted 运行诚实暴露了“轨道 3 已选中、25 个 cue 已解码、0 个 cue 被渲染”的问题。根因是 App 使用 R10G10B10A2_UNORM 10-bit swap chain，旧实现将 D2D target 直接绑定 backbuffer，返回不支持像素格式。

当前实现参考 Kodi DirectX overlay 原则，将 PGS BGRA 位图缓存为 D3D11 texture，通过 premultiplied-alpha shader 合成；PQ 输出会把字幕 SDR white 映射到 203 nits 后编码为 PQ。最终完整 Debug x64 Native AOT Publish、注册和 App-hosted 复测得到 25 个 decoded cue、169 次 rendered cue，subtitle-switch 为 success，主视频元数据为 1920x1080、23.976fps。

同一 App-hosted 报告整体仍为 fail，唯一剩余失败是服务器冷启动约 12.3 秒超过 manifest 的 7 秒门槛；该阈值未被放宽。全量 playback-core gate 共 32 个阶段全部通过。

## 技术决策

1. App-hosted case 必须按单一 scenario 执行，固定交互脚本不能冒充 case 意图。
2. selected subtitle index 只能证明轨道选择，不能证明字幕显示；decoded cue 与 rendered cue 必须分别报告。
3. 8-bit BGRA swap chain 保留 D2D 字幕路径；10-bit R10G10B10A2 swap chain 使用 D3D texture alpha composition。
4. HDR PQ 字幕按 203 nits SDR white 映射，避免字幕白色被解释为 10000 nits。
5. Emby 主视频元数据必须来自主运动视频，MJPEG、JPEG、PNG 图片附件不能覆盖 width、height、frame rate 或 HDR profile。

## 剩余问题

1. 私有 Emby 冷启动多次超过 7 秒，应作为独立 startup/network case 调查，不能通过放宽字幕 case 的阈值掩盖。
2. 当前软件证据可以证明 cue 解码和 GPU overlay 提交，不证明最终显示设备的字幕亮度、色准或 HDMI 输出。
3. HDR PQ 字幕亮度仍需要后续 Xbox/显示设备观察，但其软件变换规则已经显式且可测试。
