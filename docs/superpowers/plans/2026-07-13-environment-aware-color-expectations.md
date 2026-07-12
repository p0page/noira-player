# Environment-Aware Color Expectations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让播放器软件评测依据真实显示环境选择 manifest 明示的颜色期望，避免把 Kodi 对齐的 HDR-to-SDR fallback 误判为播放器缺陷，同时不降低 HDR 输出门禁。

**Architecture:** 主 `expected` 保持跨环境元数据、性能和 HDR-capable 颜色期望；可选 `expected.sdrDisplayFallback` 仅覆盖颜色字段。Evaluator 根据 `forceSdrOutput`、显示 HDR 能力和主 HDR 输出期望选择唯一 profile，并将选择作为报告证据。规则变更升级为 `playback-quality-v0.4`，不得与 v0.3 结果混作播放器前后对比。

**Tech Stack:** C#/.NET 8 Core tests、PowerShell quality runners、JSON manifests、C++/WinRT native headless/App-hosted evidence。

## Global Constraints

- 不改 native 解码、色彩转换、tone mapping 或 HDR 输出策略。
- 不静默推导 fallback；需要 fallback 而 manifest 未声明时必须 fail。
- `requiredConversionStatus` 按分号 token 精确匹配。
- DXGI 输入允许集合必须由 manifest 显式声明；不得接受任意 P2020 字符串。
- v0.3 与 v0.4 只用于评测规则迁移核对，不声称播放器质量提升。

---

### Task 1: 固化 v0.4 数据契约与验证规则

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityExpected.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReferenceManifest.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportComposer.cs`
- Modify: all `PlaybackQualityExpected` clone sites under `src/NoiraPlayer.Core/PlaybackQuality/`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReportComposerTests.cs`

- [x] 先添加失败测试：合法 fallback 可反序列化/克隆；空对象、嵌套 fallback、缺少关键颜色字段、空 conversion token、重复 DXGI allowed values 均被拒绝。
- [x] 新增专用 `PlaybackQualityColorExpected`，包含 `hdrOutput`、`dxgiInputAnyOf`、`dxgiOutput`、可选 `isTenBitSwapChain`、`requireValidatedConversion`、`requiredConversionStatus`。
- [x] 在 `PlaybackQualityExpected` 增加可选 `SdrDisplayFallback`，并补齐所有深拷贝路径。
- [x] 将 evaluation version 升级为 `playback-quality-v0.4`，同步版本契约测试。
- [x] 运行定向 Core 测试，确认先红后绿。

### Task 2: 实现显式 profile 选择与颜色检查

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityColorExpectationPolicy.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityEvaluator.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityEvaluatorTests.cs`

- [x] 添加失败测试：HDR-capable 选 primary；无 HDR 显示和 force-SDR 选 fallback；缺 fallback 必须 fail。
- [x] 添加失败测试：DXGI 输入仅可匹配 `dxgiInputAnyOf`；conversion status 必须含精确 token；错误输出、位深或 tone mapping 均 fail。
- [x] 实现单一 profile 选择，颜色检查消费选中 profile，其他阈值继续消费主 expected。
- [x] 报告增加 `colorPipeline.expectationProfile`，每次评测生成 observed check，值仅为 `primary` 或 `sdr-display-fallback`。
- [x] 运行 evaluator 定向测试。

### Task 3: 让证据分析与 required-signal 策略理解 profile

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualitySignalCatalog.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityRequiredSignalPolicy.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportAnalyzer.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReportAnalyzerTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityRunComparatorTests.cs`

- [x] 添加失败测试，确保 profile 是必需且可分析的证据，缺失时不能通过 strict validation。
- [x] 注册 `colorPipeline.expectationProfile`，在分析摘要中暴露并作为比较上下文，而非播放器性能投票信号。
- [x] 验证 primary/fallback 报告不会在未说明环境差异时被当作同质颜色结果比较。

### Task 4: 迁移公开、生成和私有 manifest

**Files:**
- Modify: `docs/qa/playback-quality-reference-manifest.example.json`
- Modify: `tools/quality-run/New-PrivateEmbyReferenceManifest.ps1`
- Modify: native generated-manifest builders under `tools/quality-run/`
- Modify: `docs/qa/private/README.md`
- Local only: `docs/qa/private/manifests/interaction-recovery-v13.local.json`

- [x] 为所有 HDR/DV fallback case 添加显式 SDR profile；保留主 HDR 期望与既有性能阈值。
- [x] SDR fallback 声明 G22/P2020 到 G22/P709、8-bit swapchain、`tone-mapped-hable`，并仅允许 native 支持的 LEFT/TOPLEFT 输入集合。
- [x] 更新私有 manifest 生成器和本地私有样本；确认凭据仍只来自进程环境。
- [x] 运行 manifest validation 与生成器测试。

### Task 5: 重建同语料 v0.4 baseline 并审计差异

**Files:**
- Local output: `docs/qa/private/baselines/playback-evidence-v04-current-corpus-*.local/`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

- [x] 用与 v0.3 相同的公开、私有和 native-generated 语料重跑完整 baseline。
- [x] 要求 selected/attempted/reports 一一对应、strict validation 全匹配、无未解释 skip 或缺失报告。
- [x] 逐 case 对照 v0.3/v0.4：只允许环境期望导致的颜色结论变化；startup、seek、subtitle 等原失败必须保留。
- [x] 对仍失败的颜色 case 判断 player bug、harness bug、样本/环境问题或证据不足，不做阈值修饰。

### Task 6: 全量验证与 App-hosted 复核

**Files:**
- Modify as needed: quality-run tests and version assertions under `tools/quality-run/`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

- [x] 运行完整 Core test/build/lint gate 和 native headless gate。
- [x] 完整编译/发布 App，并执行代表性 App-hosted HDR-source 软件报告，验证 SDR 显示环境选择 fallback。
- [x] 若当前没有 HDR-capable 输出环境，明确记录 primary 硬件门禁待验证，不把软件 fallback 证据写成 HDR 输出通过。
- [x] 扫描仓库凭据、确认 git diff/状态，并提交可审查的小步结果。
