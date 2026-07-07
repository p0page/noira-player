# v0.1 Source-Only Baseline

这是 v0.1 播放质量评测体系的第一份可版本化 baseline artifact。

生成命令：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- materialize-baseline-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-source-only\reports --source-revision efa9246 --player-core-version NextGenEmby.Core --build-configuration Debug --output docs\qa\baselines\v0.1-source-only\materialized-baseline-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-source-only\reports --output docs\qa\baselines\v0.1-source-only\report-set-validation.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-source-only\reports --output docs\qa\baselines\v0.1-source-only\report-analysis-summary.json
```

结果摘要：

- manifest case 覆盖：9/9。
- report-set validation：`isValid = false`。
- 缺报告错误：0。
- matched case 数量：1，来自 `local/missing-file-error-handling` 的一等 `result = error` envelope。
- 缺失 telemetry 错误：67。
- 缺失 telemetry failure class：全部为 `insufficient instrumentation`。
- report-analysis decision：`fix-report-analysis`。
- report-analysis blockedReportCount：8。
- report-analysis risk：`high`。

边界：

- 这不是实际播放采集结果。
- 生成报告只包含 source/track/environment 级可构造证据。
- 每个报告都带有 `source-only: playback execution was not run by this command` limitation。
- 这份 baseline 的用途是验证 manifest、report serialization、model analysis、report-set validation 和缺失证据分类可以闭环。
- 后续真实 App/native 播放采集器完成后，应以真实 `PlaybackQualityRunResult` envelope 替换 source-only baseline。
