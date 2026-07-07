# 当前状态

播放质量评测体系正在推进 v0.1，目标是先把评测做成可信裁判，而不是优化播放效果。

## 已完成

- `PlaybackQuality` 报告已覆盖 source、startup、position、timing、sync、buffers、colorPipeline、display 等核心软件信号。
- `PlaybackQualityReportAnalyzer` 已输出模型可消费的 failure area、failure class、evidence、missing evidence、triage steps 和 optimization gate。
- reference manifest、report-set validation、analyze-report、compare、compare-suite、evaluate-candidate、plan-runs 已形成 App-free 闭环。
- `materialize-run-result` 已可把 raw report 或旧 envelope 归一化为包含当前 `modelAnalysis` 的 `PlaybackQualityRunResult` envelope。
- `materialize-baseline-report-set` 已可从 reference manifest 生成 source-only baseline envelope，用于建立可版本化 baseline artifact 并暴露缺失 telemetry。
- `docs/qa/baselines/v0.1-source-only/` 已归档第一份 source-only baseline：8/8 case 有报告，report-set validation 失败于 68 个缺失 telemetry，全部归类为 `insufficient instrumentation`。
- reference manifest case 已支持 `stable`、`challenge`、`quarantine` 分类，并在 validation、report-set status 和 run plan 中保留。
- reference manifest case 已支持 `severity` 和 `stability`，并在 validation、report-set status、run plan 和 baseline summary 中保留。
- `PlaybackQualityRunResult` envelope 已输出 `caseMetadata`，单个报告可直接暴露 case id、category、severity 和 stability。
- report-set validation errors 已输出 `failureClass`，可区分缺 telemetry、缺报告、重复/额外报告和 source metadata mismatch。
- App-free 验证命令为 `tools\quality-run\run-playback-core-checks.ps1`，当前结果为 pass。
- 本轮新增 tracks/subtitles telemetry：报告会记录视频轨、音轨、字幕轨数量、当前选中音轨/字幕轨、字幕关闭状态和轨道明细。
- reference manifest coverage 现在要求 `tracks` 和 `subtitles` purpose；默认公开 manifest 与私有 Emby manifest 生成脚本已同步更新。

## 当前缺口

- 轨道切换目前主要是发现/选择状态证据，尚未证明切换后的 native 播放行为完整正确。
- 字幕 v0.1 只验证识别、选择和关闭状态，不验证最终视觉渲染正确性。
- 缓冲、frame timing、A/V sync 和颜色信号仍依赖当前 native instrumentation 的覆盖度，后续需要继续补强证据质量。
- v0.1 尚未完成真实播放采集 baseline/candidate；当前 source-only baseline 只能证明评测链路闭环和缺失证据分类，不证明播放效果。

## 风险

- 缺失 telemetry 应归类为 `insufficient instrumentation`，不能直接当成播放器 bug。
- 私有 Emby 地址、账号、密码、真实 itemId/mediaSourceId 和本地报告只能放在 ignored 路径或环境变量中。
- 当前评测仍是纯软件闭环，不能证明 HDMI InfoFrame、显示器 EOTF 或肉眼颜色准确性。

## 下一步

先归档一次 source-only baseline report-set、report-set validation 和 report-analysis summary，确认 JSON 可被模型消费；随后优先补真实 App/native 采集器，把 source-only baseline 替换为实际播放 evidence。
