# v0.1 Native Harness Skip Baseline

这是 v0.1 播放质量评测体系的 native-harness 缺口归档。

生成命令：

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- materialize-native-harness-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-native-harness-skip\reports --source-revision working-tree-source-color-metadata-v0.1 --player-core-version NextGenEmby.Core --build-configuration Debug --output docs\qa\baselines\v0.1-native-harness-skip\materialized-native-harness-summary.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-report-set --manifest docs\qa\playback-quality-reference-manifest.example.json --reports-dir docs\qa\baselines\v0.1-native-harness-skip\reports --output docs\qa\baselines\v0.1-native-harness-skip\report-set-validation.json
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- analyze-report-set --reports-dir docs\qa\baselines\v0.1-native-harness-skip\reports --output docs\qa\baselines\v0.1-native-harness-skip\report-analysis-summary.json
```

当前结果：

- 9/9 reference case 生成标准 `PlaybackQualityRunResult` envelope。
- `validate-report-set` 为 `isValid = true`，`matchedCaseCount = 9`，`errors = []`。
- 所有 report 都是 `result = skip`，`skip.code = native-harness.not-implemented`。
- modelAnalysis analyzerVersion：5。
- `analyze-report-set` 输出 `action = collect-comparable-evidence`、`decision = collect-comparable-evidence`、`risk = high`、`confidence.level = weak`、`skippedReportCount = 9`。
- `targetFailureAreas` 和 rank-1 `nextActions[0].failureArea` 指向 `evidence-collection`。

边界：

这份归档不打开 native playback graph，不解码真实媒体，不产生 frame timing、buffering、A/V sync、display 或 color 质量证据。它只证明当前评测体系能把“真实 native 采集器还没实现”表达为可验证、可版本化、可由模型消费的标准 skip report-set。模型不能用它优化播放 Core 行为；下一步应实现或接入真实 native playback collector。
