# 视频解码 EAGAIN 恢复证据

## 目的

FFmpeg 解码器可能在 `avcodec_send_packet` 返回 `EAGAIN` 后暂时无法立刻产出帧。播放器会保留同一压缩包，先排空输出并进行有界重试。评测系统必须证明该状态是否发生、是否恢复、是否耗尽，不能只记录最终播放结果。

## v0.17 信号

- `timing.videoDecoderSendPacketEagainCount`：`send_packet` 返回 `EAGAIN` 的累计次数。
- `timing.videoDecoderDoubleEagainRetryCount`：同一轮中 `send_packet` 与 `receive_frame` 均无进展时执行的累计重试次数。
- `timing.videoDecoderDoubleEagainRecoveryCount`：出现双 EAGAIN 后重新取得解码进展的累计次数。
- `timing.videoDecoderDoubleEagainExhaustedCount`：有界重试预算耗尽的累计次数。

完成 native 播放的报告必须显式包含四个字段。字段缺失表示 `insufficient instrumentation`，不能按零处理。计数必须满足：重试不大于 send EAGAIN、恢复不大于重试、耗尽不大于 send EAGAIN；违反约束归类为 `evaluation harness bug`。耗尽大于零归类为 `decoder-recovery / player-core bug`。

非 native 报告不会因默认零值获得“恢复未耗尽”的通过证据。只有 native 解码器确实打开，或已经观察到非零恢复活动时，evaluator 才生成该检查。

## 当前实测

2026-07-15 使用同一 v0.17 实现完成以下软件闭环：

| Case | 证据等级 | send EAGAIN | 双 EAGAIN 重试 | 恢复 | 耗尽 | 结果 |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| 私有 Emby SDR 代表片源 | native-playback | 2 | 2 | 2 | 0 | pass |
| 私有 Emby DV8/HDR10 fallback 代表片源 | native-playback | 1 | 1 | 1 | 0 | pass |
| Jellyfin HEVC Main10 公开片源 | app-hosted | 0 | 0 | 0 | 0 | pass |

两条私有片源自然触发了真实 FFmpeg/D3D11 解码路径的双 EAGAIN，并在预算内恢复；这不是合成报告或仅调用状态机单测。公开 App-hosted case 完成完整 App 发布、注册和实际播放，证明 Native、WinRT、App、Core 与报告投影一致。

本轮没有调整重试预算或解码策略，只补齐可观测性、严格校验和归因。耗尽路径由状态机单测和 evaluator 失败测试覆盖；当前真实样本没有自然耗尽，因此不能宣称实播耗尽场景已经复现。

## 评测器防护

- native-headless smoke 使用 worktree 级命名 mutex，禁止并发任务删除或覆盖同一 artifacts 目录。
- report-set reader 只读取播放 report 或 run-result envelope；runner summary、validation 等同目录辅助 JSON 不得冒充报告。
- `decoder-recovery` 是版本化 failure area，并映射到 `VideoDecoder`、恢复状态机和播放图代码位置。
- 私有服务器地址、凭据、item/source id 和报告保留在 ignored/local artifacts，不进入仓库。

## 验证命令

```powershell
tools\quality-run\run-native-headless-harness-smoke-test.ps1
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj
tools\Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64
```

私有 Emby case 通过 `Invoke-PlaybackQualityManifest.ps1` 执行；认证信息只通过本地环境变量提供。
