# 播放场景证据链状态（2026-07-12）

## 本轮结论

评测器已不再允许缺失场景身份的 manifest 或报告进入有效 report-set。每个 case 必须显式声明 `executionRequirement.scenario`，runner、native helper、App-hosted capture 和最终报告必须使用同一场景。

旧的 `playback-evidence-v8-c6cdf08.local` 仍保留历史价值，但按当前规则重新校验时，23 份报告全部因 `report.execution.scenario.mismatch` 失效。它们不能继续充当当前基线。

## 已完成

- 公开 reference manifest 和私有 manifest 模板显式声明场景；缺失或未知场景会使 manifest validation 失败。
- CLI、manifest runner、native-headless 和相关 smoke fixture 已迁移到显式场景契约，不再依赖静默的 `playback` 默认值。
- App-hosted 暂停恢复不再以 API 调用成功为成功：必须观察暂停期位置基本冻结、恢复后位置继续前进且渲染帧增加。
- App-hosted 音轨切换必须同时观察目标音轨选中、时间线继续前进、音频提交帧增加。
- native helper 既有的音轨、字幕、seek、暂停恢复证据规则保持严格，没有放宽阈值。

## 真实报告

`playback-evidence-v9-scenario-contract.local` 使用当前契约逐案执行公开和私有 active case：

- 13 个 case 被选择、尝试并生成 13 份报告；无缺失、无 source resolution 失败。
- strict report-set validation 有效，13 个 case 全部匹配 manifest 与 scenario。
- 12 个 case 为 `pass`；公开 DV Profile 5 case 按当前能力诚实报告为 `unsupported`。
- 私有音轨切换、PGS 字幕切换和 timeline case 都执行了真实 native 交互。

《哈姆奈特》另有 3 个 active case 独立执行并通过 strict validation：HDR 输出、HDR 强制 SDR、4K 23.976 cadence。20 秒采集中未复现慢速播放或双 EAGAIN 致命退出。23.976 case 的 render interval P95/P99 约为 49-51 ms，仍高于理想帧间隔 41.7 ms，应保留为后续 frame pacing 调优信号。

## App-hosted 复核

完整 Debug x64 Native AOT/UWP Publish 成功。私有 `audio-switch` case 随后通过完整 App 注册和启动路径执行：

- `execution.runner = app-hosted`，`execution.scenario = audio-switch`；
- 目标/选中音轨均为 1；
- position 从 21.70 秒前进到 24.09 秒；
- submitted audio frames 从 79 增长到 98；
- decoded/rendered video frames 为 387/332。

音轨切换操作本身成功，但报告整体仍为 `fail`：冷启动 10160.668 ms 超过 manifest 的 7000 ms 上限，主失败区域为 `startup`。该阈值未调整。

## 自动验证

`tools/quality-run/run-playback-core-checks.ps1` 的 32 个阶段全部通过，包括 529 个 Core 测试、CLI/runner、真实 native-headless、网络重连、音轨字幕、timeline、双 EAGAIN 恢复、DirectX offscreen 和 native build。

## 剩余风险

- 本轮没有在 Xbox 上重新验证显示输出；HDR 实机结论沿用此前阶段，不由这批软件报告替代。
- App-hosted 暂停恢复的加强规则已有单元/构建验证，但还需要代表性长暂停 App-hosted 报告。
- 《哈姆奈特》23.976 的帧间隔仍有可量化的优化空间，不能因 case 当前为 `pass` 而忽略。
- App 冷启动超过既定阈值，需要单独归因网络、PlaybackInfo、demux probe 与宿主启动耗时，不能混入交互成功判定。
