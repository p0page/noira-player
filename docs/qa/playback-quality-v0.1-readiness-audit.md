# v0.1 播放质量评测就绪度审计

日期：2026-07-08

范围：当前 `NextGenEmby.Core` / `PlaybackQuality` / `quality-run` 评测体系。本审计只判断软件评测裁判是否就绪，不判断播放效果是否提升。

## 当前结论

v0.1 尚未完成。

评测框架、报告契约、source-only baseline、core-probe baseline、native-harness skip baseline 和 candidate gate 已经基本就位。当前主要阻塞是：还没有一套可复现的 native/App 软件播放 report-set，能够真正打开播放路径并产生可比较 runtime evidence，同时不依赖 Xbox、显示器输出或人工肉眼观察。

## 完成标准状态

| 要求 | 状态 | 证据 | 说明 |
| --- | --- | --- | --- |
| 有文档化命令可以运行播放质量检查 | 已满足 | `tools\quality-run\run-playback-core-checks.ps1` | 覆盖 Core 播放测试、CLI smoke、manifest 工具、native 测试、native restore/build 和 App diff guard。 |
| 至少完成一次当前 player core 实际评测 | 有范围限制地满足 | `docs\qa\baselines\v0.1-core-probe\` | 通过 diagnostic backend 执行 `PlaybackOrchestrator`。可证明 orchestration、生命周期、轨道/字幕状态、报告结构和 analyzer 链路；不能证明 native 解码/渲染质量。 |
| source-only bootstrap baseline 存在 | 有范围限制地满足 | `docs\qa\baselines\v0.1-source-only\` | 可验证 manifest、report-set、required-signal 和缺 telemetry 分类；不是播放证据。 |
| native/App evidence 导入路径存在 | 部分满足 | `materialize-native-harness-report-set --captured-reports-dir` | 可以导入 captured reports 并归一化成 v0.1 report-set；它本身不执行播放采集。 |
| native/App 采集入口存在 | 部分满足 | DEBUG App `quality-run` 路径、HTTP/HTTPS direct-uri `streamUrl` command 和 `Export-AppQualityRunReports.ps1` | App-hosted collector 可以打开 Emby item 或公开 direct-uri 并写 LocalFolder 报告，但目前没有已提交的公开 native/App captured baseline。 |
| candidate evaluation 防止弱证据误用 | 已满足 | `baseline-playback-evidence` / `candidate-playback-evidence` gates | `source-only` 和 `core-probe` 不能作为 native playback evidence 进入 suite comparison。 |
| `stable` / `challenge` / `quarantine` 边界存在 | 已满足 | `docs\qa\playback-quality-reference-manifest.example.json` 与 validation 输出 | category、severity、stability 已贯穿 planning、report、validation 和 summary。 |
| JSON 报告可供模型消费 | 基本满足 | `evaluationVersion`、`caseMetadata`、`modelAnalysis`、`capabilityCoverage`、`playbackEvidence`、`evidenceGates`、`nextActions` | 当前 JSON 契约已经暴露 failure class、failure area、证据来源、blocker、code target 和下一步动作。 |
| 当前 fail / skip / unsupported 被诚实记录 | 已满足 | source-only / core-probe / native-harness-skip baselines | 缺 runtime evidence 归类为 `insufficient instrumentation`；DV Profile 5 是 `unsupported`；native harness 缺失是 `skip`。 |
| native playback quality 可以做 before/after 比较 | 未满足 | 无 | 至少需要一套 native/App 软件播放 baseline 和一套 candidate report-set，且两者有可比较 runtime evidence。 |

## 能力覆盖说明

当前已有稳定软件证据覆盖：

- manifest validation 和 report-set coverage。
- source metadata、direct stream locator evidence、显式提供时的 raw source color metadata。
- duration、chapters、codec、resolution、frame rate、HDR 分类 metadata。
- 轨道和字幕发现状态，包括可用时的 default / forced / external 标记。
- core-probe 生命周期：load、play、pause、resume、seek、stop、diagnostic end-of-stream、error。
- 结构化 error、skip、unsupported、failure class 和 failure area。
- runtime metrics provider identity 与采集状态。

仍未被 native/App 软件证据证明：

- 真实播放中的 decoder / rendered frame counters。
- 真实 frame pacing、dropped / duplicated frames 和 timestamp 连续性。
- 真实 buffering / stall / recovery 行为。
- 真实 A/V sync 和 seek 后同步恢复。
- native playback graph 产生的真实 color pipeline 输出。
- 音轨/字幕切换后的 native 行为。
- 字幕最终视觉渲染正确性；这仍不属于 v0.1 强制范围。

## 下一步最合适目标

产出第一套基于公开样本、可复现的 native/App 软件播放 report-set：

1. 使用现有 reference manifest 和公开 HTTP/HTTPS sample case。
2. 用 `plan-runs` 生成带 `streamUrl` 的 public direct-uri `quality-run` command，并运行 App-hosted DEBUG `quality-run`，或实现等价的最小 native/App 软件 harness。
3. 将 captured reports 导出到 ignored/private 工作目录。
4. 用 `materialize-native-harness-report-set --captured-reports-dir` 导入。
5. 运行 `validate-report-set`、`analyze-report-set` 和 `evaluate-candidate`。
6. 只提交去敏后的文档、脚本和公开 artifact；不要提交私有服务器信息或个人素材路径。

完成这个下一步后，至少一套公开 native/App 软件 report-set 的 `playbackEvidence.canEvaluateNativePlayback` 应从 false 变为 true。
