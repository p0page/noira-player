# v0.1 Core Probe Baseline

这是 v0.1 播放质量评测体系的第一份非 source-only 评测归档。

生成命令：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- materialize-core-probe-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-core-probe\reports --source-revision working-tree-track-default-forced --player-core-version NextGenEmby.Core --build-configuration Debug --output docs\qa\baselines\v0.1-core-probe\materialized-core-probe-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-core-probe\reports --output docs\qa\baselines\v0.1-core-probe\report-set-validation.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-core-probe\reports --output docs\qa\baselines\v0.1-core-probe\report-analysis-summary.json
```

结果摘要：

- manifest case 覆盖：9/9。
- report-set validation：`isValid = true`。
- report 数量：9。
- matched case 数量：9。
- validation error 数量：0。
- report-analysis decision：`no-change`。
- report-analysis blockedReportCount：0。
- Dolby Vision Profile 5 case：`status = unsupported`，`primaryFailureArea = unsupported-source`。
- `local/missing-file-error-handling` case：`result = error`，`error.code = core-probe.error-case`，`failureArea = error-handling`。

边界：

- 这是实际 player core 软件评测，不是 source-only 静态报告。
- 它会执行 `PlaybackOrchestrator` 的 load、play、pause、resume、seek、track switch、subtitle switch、diagnostic end-of-stream marker 和 stop 路径，并把这些操作写入 `lifecycle.events[]`。
- 它使用 in-process diagnostic backend，不打开 native playback graph，不解码真实媒体，不访问网络，不验证 HDMI / 显示器输出。
- runtime metrics 的 `providerStatus = core-probe:returned-snapshot`，表示指标来自 deterministic probe，不是 App/native graph。
- startup、display、timing、buffering、A/V sync 指标是 deterministic probe telemetry，只能用于验证评测链路和 core orchestration 证据，不代表真实播放效果。
- `lifecycle.endOfStream` 在 core-probe 中只是 diagnostic marker，不证明真实媒体自然播放到 EOF。
- 后续进入播放效果优化前，仍需要 native graph 或真实媒体采集器提供 decoder、renderer、buffer、frame pacing、A/V sync、color pipeline 的真实软件 telemetry。
