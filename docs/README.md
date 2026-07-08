# Noira 文档入口

本目录按“当前事实优先、历史记录可追溯”的原则维护。遇到同一问题的多份文档时，优先级为：

1. 最新提交中的代码、测试和脚本。
2. 本文件列出的当前权威文档。
3. `docs/STATUS.md` 和 `docs/DECISIONS.md` 中较新的日期条目。
4. 历史 plan、handoff、smoke log 和 QA run log。

历史文档可能保留旧项目名、旧路径、旧包名和当时的验证命令；除非正在复盘历史过程，不应把它们当作当前执行指令。

## 当前权威文档

| 领域 | 文档 | 用途 |
| --- | --- | --- |
| 项目状态 | `docs/STATUS.md` | 追加式记录当前阶段、已验证事实、缺口和风险。 |
| 技术决策 | `docs/DECISIONS.md` | 追加式 ADR/decision log，最新日期优先。 |
| 评测原则 | `docs/EVAL_PHILOSOPHY.md` | 播放 core 软件评测的边界、证据原则和模型消费原则。 |
| 播放评测契约 | `docs/qa/software-playback-quality-metric-contract.md` | JSON 报告、failure 分类、model analysis 和 candidate gate 契约。 |
| 播放评测运行 | `docs/qa/playback-core-quality-validation.md` | 本地运行 playback quality 工具链的命令和范围。 |
| 样本规范 | `docs/qa/playback-quality-reference-corpus.md`、`docs/qa/private/README.md`、`docs/qa/public-test-media-catalog.md` | 公开/私有/本地样本维护规范。 |
| 设计系统 | `docs/DESIGN.md` | Noira 当前视觉系统、token、组件和页面规则。 |
| 视觉收敛 | `docs/a3-visual-convergence-rules.md`、`docs/qa/a3-visual-convergence-checklist.md` | A3 阶段视觉目标和截图验收。 |
| 交互 QA | `docs/qa/emby-tv-client-operation-matrix.md`、`docs/qa/design-conformance-checklist.md` | 当前 TV 客户端交互覆盖和设计验收。 |
| 原生播放 | `docs/kodi-color-pipeline-comparison.md`、`docs/native-dependencies.md`、`docs/native-playback-smoke-tests.md` | Kodi 对照、FFmpeg/UWP 依赖、Windows/Xbox 冒烟边界。 |

## 冻结或历史资料

| 路径 | 状态 | 规则 |
| --- | --- | --- |
| `docs/qa/baselines/` | 冻结评测结果 | 当前整理阶段不改内容。只在明确刷新同一 eval 版本的 report-set 时更新。 |
| `docs/qa/private/` 下的 `*.local.*` | 私有本地产物 | 必须保持 ignored，不得提交真实服务器、账号、密码、URL 或本地私有样本路径。 |
| `docs/plans/` | 历史或待执行计划 | 只作为背景输入；执行前必须重新核对当前代码路径、项目名和测试命令。 |
| `docs/superpowers/plans/` | 历史执行计划 | 默认归档，不作为当前开发入口。 |
| `docs/superpowers/specs/` | 历史规格 | 若被当前权威文档引用，可以作为设计依据；否则视为背景材料。 |
| `docs/foundation-status.md` | 早期基础阶段记录 | 已被 `docs/STATUS.md` 取代，只保留历史。 |
| `docs/design-handoff-2026-07-07.md` | 设计交接记录 | 当前设计事实以 `docs/DESIGN.md` 和 A3 文档为准。 |
| `docs/qa/emby-tv-client-keyboard-checklist.md` | 长期 QA run log | 只追加新运行记录；当前覆盖状态优先看 operation matrix 和设计/视觉 checklist。 |

## 维护规则

- 新的长期事实写入对应权威文档；不要把结论只留在 plan 或聊天记录里。
- 新的技术取舍写入 `docs/DECISIONS.md`，需要包含决策、原因、影响和边界。
- 新的阶段状态写入 `docs/STATUS.md`，需要区分“已验证事实”“缺口”“风险”“下一步”。
- 新的评测规则必须同步更新 `docs/EVAL_PHILOSOPHY.md` 或 metric contract；不能只修改阈值或 expected result。
- QA 结果和 baseline 结果必须可追溯到命令、manifest、report-set 路径和 git revision。
- 对历史文档做路径/品牌修正时，只修正会被当前流程直接执行的命令；纯历史日志可以保留原文。
