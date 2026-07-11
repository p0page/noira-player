# 播放评测真实执行证据契约实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让正式播放器 baseline 的每个 stable/challenge case 都由真实 native/App 播放 attempt 支撑，并从结构上阻止 core-probe 或合成 actual 冒充播放证据。

**Architecture:** 在 manifest 和 report 中增加显式 execution requirement/evidence，由 strict validator 交叉验证证据等级、源关联、执行阶段与现有 runtime metrics。新增 manifest runner 逐 case 调用 native-headless，正式 baseline 停止物化 core-probe 报告；probe 保留为独立 evaluator-self-test。

**Tech Stack:** C#/.NET、System.Text.Json、xUnit、PowerShell、C++/WinRT native helper、FFmpeg、现有 NoiraPlayer playback-quality CLI/headless 工具。

## Global Constraints

- 文档和面向用户的说明使用中文；代码标识符保持现有英文风格。
- 不修改播放质量阈值来使当前 Core 通过。
- `expected` 不得填充 actual execution/source/runtime evidence。
- 不把完整 URL、token、服务器地址、用户名或密码写入报告或 tracked 文件。
- 不自动从 native runner 回退到 core-probe。
- 行为变更必须遵循 RED-GREEN-REFACTOR，并保留失败测试输出证据。
- 本阶段只宣称纯软件闭环，不宣称验证 HDMI、电视面板或真实 HDR 输出。

---

### Task 1: 定义 execution requirement/evidence 与严格 validator

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReferenceManifest.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Create: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityExecutionEvidence.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReferenceReportSetValidator.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityJsonContext.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`

**Interfaces:**
- Produces: `PlaybackQualityExecutionRequirement.MinimumEvidenceLevel`
- Produces: `PlaybackQualityExecutionEvidence` and `PlaybackQualityEvidenceLevel`
- Produces: strict `StructureValid`, `ExecutionValid`, `IsValid` and execution coverage counters

- [x] 写失败测试：native-playback case 使用 core-probe/pass report 时 `ExecutionValid == false`。
- [x] 运行该测试，确认它因当前 validator 仍返回 valid 而失败。
- [x] 写最小 execution DTO、等级比较和 validator 门禁。
- [x] 运行测试，确认 probe 无法满足 native-playback。
- [x] 继续以失败测试覆盖：缺 attemptId、locator hash 不匹配、无 playback sample、quarantine 缺席、真实 error attempt。
- [x] 运行 PlaybackQualityReferenceManifestTests 全组测试并修复契约预期。
- [x] 提交 `feat: enforce playback execution evidence`。

### Task 2: 让 probe/headless/App producer 产生明确证据

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityOrchestratorProbe.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityRuntimeEvidenceCollector.cs`
- Modify: `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`
- Modify: `src/NoiraPlayer.App/Views/PlaybackPage.xaml.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReportComposerTests.cs`
- Test: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`

**Interfaces:**
- Consumes: Task 1 execution DTO
- Produces: `orchestration`、`native-playback`、`app-hosted` execution evidence

- [x] 写失败测试：core-probe 报告必须标记 orchestration 且所有媒体执行阶段为 false。
- [x] 写失败测试：native helper 成功报告必须带 source hash、native graph/demux/decoder/sample evidence。
- [x] 写失败测试：native helper 非零退出仍必须保留 attempt 和 failed execution evidence。
- [x] 实现 producer 映射，不从 expected 推断执行状态。
- [x] 运行 Core 和 native-headless parser/smoke tests，并编译 Modern App x64 Debug。
- [x] 提交 `feat: capture playback execution provenance`。

### Task 3: 建立逐 case 的 manifest native runner

**Files:**
- Create: `tools/quality-run/NativeHeadlessHarness.psm1`
- Create: `tools/quality-run/Invoke-PlaybackQualityManifest.ps1`
- Create: `tools/quality-run/Invoke-PlaybackQualityManifest.tests.ps1`
- Modify: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- Modify: `tools/quality-run/run-playback-core-checks.ps1`
- Modify: `tools/quality-run/run-playback-core-checks.tests.ps1`

**Interfaces:**
- Consumes: manifest v2 和 `--native-helper-exe`
- Produces: 每个 selected case 一个 raw `PlaybackQualityRunResult`

- [x] 写失败测试：两个 direct URI case 必须产生两次不同 runner invocation，不能只生成计划。
- [x] 写失败测试：第一项失败后第二项仍执行，且两项各有报告。
- [x] 抽取逐 case helper invoke 模块，并让现有 smoke 通过 manifest runner 执行正式网络恢复 case。
- [x] 实现 manifest filter、逐 case 调度、无 fallback、summary 计数和非零退出规则。
- [x] 使用本地 SDR/HDR 小样本运行 runner，并通过 strict materialize/validate。
- [x] 提交 `feat: execute playback manifests with native runner`。

### Task 4: 支持 ignored 私有 Emby case 解析

**Files:**
- Create: `tools/NoiraPlayer.PlaybackQuality.Runner/NoiraPlayer.PlaybackQuality.Runner.csproj`
- Create: `tools/NoiraPlayer.PlaybackQuality.Runner/Program.cs`
- Create: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualitySourceFingerprintTests.cs`
- Modify: `tools/quality-run/Invoke-PlaybackQualityManifest.ps1`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: `NOIRAPLAYER_QA_SERVER_URL`、`NOIRAPLAYER_QA_USERNAME`、`NOIRAPLAYER_QA_PASSWORD`
- Consumes: manifest `itemId/mediaSourceId`
- Produces: 仅在进程内存在的 direct stream URL，以及报告中的匿名 source hashes

- [x] 写失败测试：Emby locator 缺凭据时生成结构化 error，不回退到 probe。
- [x] 写失败测试：指定 mediaSourceId 必须解析到对应 source，输出不得包含 token/URL。
- [x] 复用 `EmbyApiClient.AuthenticateAsync/GetPlaybackInfoAsync`，不复制 URL 组装规则。
- [x] 把 direct stream URL 通过进程参数传给 headless，仅持久化 hash。
- [x] 用脱敏 fixture 完成自动测试，再用 ignored 私有 manifest 实跑。
- [x] 提交 `feat: resolve private Emby playback cases`。

### Task 5: 替换正式 baseline 编排并隔离 probe

**Files:**
- Modify: `tools/quality-run/New-PlaybackCoreTuningBaseline.ps1`
- Modify: `tools/quality-run/New-PlaybackCoreTuningBaseline.tests.ps1`
- Modify: `tools/NoiraPlayer.PlaybackQuality.Cli/Program.cs`
- Modify: `tools/quality-run/run-playback-quality-cli-smoke-test.ps1`
- Modify: `docs/EVAL_PHILOSOPHY.md`
- Modify: `docs/qa/playback-core-quality-validation.md`

**Interfaces:**
- Consumes: Task 3/4 manifest runner report-set
- Produces: strict v2 baseline；独立 evaluator-self-test probe report-set

- [x] 写失败测试：baseline 脚本不得调用 `materialize-core-probe-report-set` 填充播放 case。
- [x] 写失败测试：缺任一 stable/challenge raw report 时 baseline 失败。
- [x] 改为调用 manifest runner，再 strict validate/analyze。
- [x] 将 probe smoke 和归档说明迁为 evaluator-self-test。
- [x] 运行 baseline 脚本测试和 CLI smoke。
- [x] 提交 `fix: require real playback in tuning baselines`。

### Task 6: 阻止跨证据、跨源 candidate 比较

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityRunComparator.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityComparisonSuiteAggregator.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityRunComparatorTests.cs`
- Modify: `tools/quality-run/Compare-PlaybackCoreTuningCandidate.ps1`
- Test: `tools/quality-run/Compare-PlaybackCoreTuningCandidate.tests.ps1`

**Interfaces:**
- Consumes: baseline/candidate execution evidence
- Produces: evidence/source mismatch 时 `insufficient-evidence`

- [ ] 写失败测试：orchestration baseline 与 native candidate 不可比较。
- [ ] 写失败测试：source locator/opened source hash 不同不可比较。
- [ ] 写失败测试：任一侧 execution evidence 不完整不可产生 improvement/regression。
- [ ] 实现 comparability gate，并保留现有指标比较算法。
- [ ] 运行 comparator 和 candidate script tests。
- [ ] 提交 `fix: compare only equivalent playback executions`。

### Task 7: 建立 timeline/seek 真实回归 case 并对照成熟播放器

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityEvaluator.cs`
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.h`
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.cpp`
- Modify: `src/NoiraPlayer.Native/Media/VideoDecoder.cpp`
- Modify: `src/NoiraPlayer.Native/Media/AudioDecoder.cpp`
- Test: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`
- Test: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Produces: container/stream timeline origin、logical duration、seek demux timestamp、first-presented landing 和 post-seek advancement 证据
- Consumes: Task 3 的真实 manifest runner

- [ ] 固定并记录 Kodi、VLC、mpv 参考源码 commit，比较 start-time normalization、seek target、landing 和 duration 语义。
- [ ] 写失败测试：生成一个非零 start time 素材，确认当前逻辑 position/seek landing 出现偏移。
- [ ] 写失败测试：seek 报告必须同时包含 requested、demux target、first presented 和 post-seek advancing。
- [ ] 在 demux/native 层建立统一 timeline origin；不要在 Slider 或 App 中用素材特例补偿。
- [ ] 用零 start time、非零 start time、resume+seek 三类样本验证 RED-GREEN。
- [ ] 使用真实 Emby 问题素材复测进度显示和 seek，不把 UI 目标值当作成功证据。
- [ ] 提交 `fix: normalize playback timeline and seek landing`。

### Task 8: 生成真实 baseline 并重审既有优化

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Modify: `docs/qa/playback-core-quality-validation.md`
- Local ignored output: `docs/qa/private/baselines/playback-evidence-v2-*.local/`

**Interfaces:**
- Consumes: 固定 manifest v2 和当前 HEAD
- Produces: public/private report-set、validation、analysis 和后续 candidate 基线

- [ ] 用 Jellyfin HEVC/HDR/4K URI 运行 direct native cases。
- [ ] 用 ignored manifest 运行“一战再战”和“哈姆奈特”。
- [ ] 检查 declared/attempted/opened/decoded/rendered/completed/failed 计数和每项原始报告。
- [ ] 对 probe 污染的旧结论标记 legacy，不删除历史证据。
- [ ] 使用 `tools/quality-run/run-playback-core-checks.ps1` 完成全量 App-free 验证。
- [ ] 完整重新编译 App，并对代表性 SDR/HDR/复杂音轨 case 做 App-hosted 抽样复核。
- [ ] 更新状态、决策和剩余风险，提交 `docs: establish trustworthy playback baseline`。
