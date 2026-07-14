# 视频渲染阶段诊断

`compare-render-phases` 用于比较同一批真实 native 播放报告中的 VideoProcessor CPU 阶段。它是阶段级诊断器，不是整体播放候选裁判。

## 运行方式

```powershell
dotnet run --project tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj -- compare-render-phases `
  --manifest <精确的目标-manifest.json> `
  --baseline-dir <baseline-run-1-reports> `
  --candidate-dir <candidate-run-1-reports> `
  --baseline-dir <baseline-run-2-reports> `
  --candidate-dir <candidate-run-2-reports> `
  --output <render-phase-diagnostic.json>
```

`--baseline-dir` 与 `--candidate-dir` 必须数量相同，并按重复轮次成对出现。目录可以包含 manifest 之外的其他报告；命令只选择 manifest 声明的 case，但每轮所选报告仍必须通过严格 report-set validation。manifest case 缺失、重复或证据无效都会阻断该轮，不能静默跳过。

## 可比较性条件

每一对报告必须同时满足：

- 实际打开媒体的 `observed-media-signature-v2` 相同，并由比较器根据报告中的 native observation 独立重算；两侧填入相同伪造 hash 也会被拒绝；
- 颜色期望策略相同；
- baseline 与 candidate 有完整且不同的构建身份；
- build configuration、collector version 和 native runner 相同；
- 两侧都完成 native playback，并真实观察到播放样本；
- VideoProcessor 的 setup、view/target、clear 和 blit 各至少有 30 个样本，且各自计数与该侧 VideoProcessor 帧数一致；
- 启用 post-process 时，其样本数与该侧 post-process 帧数一致且至少为 30；未启用时 0 帧、0 样本合法。

不要求 baseline 与 candidate 的总帧数接近。固定观察窗口内的帧数可能正是候选影响的结果；只要两侧样本均足量且内部计数自洽，就应保留该差异，而不是用帧数比例反向遮蔽性能变化。

## 输出语义

顶层输出固定包含 `diagnosticScope = video-render-phases` 和 `decisionAuthority = none`。只有所有预期 case 与重复轮次都可比时，`status` 才是 `complete`。

每个 comparison 保存：

- 匿名 opened-source 身份、颜色期望、构建和执行上下文；
- direct-copy、VideoProcessor、BGRA 与 post-process 帧数；
- 各阶段样本数与 transport read/seek wait 上下文；
- total render、setup、view/target、clear、blit、post-process 的 P50、P95、P99、Max；
- 每个指标的原值、绝对差、候选/基线比值、百分比和 `lower` / `higher` / `unchanged` 方向。

顶层 `caseSummaries` 按 manifest 顺序保留全部 case，并对每个 signal 汇总预期/可比重复数、lower/higher/unchanged 次数，以及 baseline、candidate、absolute delta 的 min、median、max。只有预期轮次全部可比且方向完全一致时，才会输出 `consistent-lower`、`consistent-higher` 或 `consistent-unchanged`；方向不一致为 `mixed`，缺少任一轮为 `insufficient-evidence`。汇总不删除逐轮 comparison，也不计算分数。

诊断器不设置“足够小就忽略”的性能阈值，也不计算总分。一个阶段降低、另一个阶段升高时结果应为 `mixed`。transport wait 只作为解释上下文，不阻断阶段 CPU 计时；整体播放是否可接受仍必须由同版本的完整 report analysis、candidate evaluation 和必要的 App-hosted 复核决定。

## 当前证据

首次正式回放使用同一精确 manifest 的 3 轮、每轮 5 个真实 native case，得到 `15/15 comparable`、0 blocker。新命令读取的 setup P95 与旧手工分析表 `15/15` 逐值一致；5 个 case 的 setup P95 汇总均为 `3/3 consistent-lower`，但各 comparison 因其他阶段的细微上下浮动保留为 `mixed`。该结果支持“setup 缓存确实改变了 CPU 阶段耗时”，不支持“整体播放质量已通过”或“Xbox/GPU/HDR 输出已验证”。
