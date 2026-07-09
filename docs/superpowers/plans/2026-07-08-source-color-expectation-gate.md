# Source Color Expectation Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 reference manifest 能声明 raw source color metadata 期望，并让 evaluator/report-set gate 在这些期望存在时检查 `report.source` 中的对应证据。

**Architecture:** 继续复用现有 `PlaybackQualityExpected`、`PlaybackQualityEvaluator`、`PlaybackQualityRequiredSignalPolicy`、manifest factory 和 CLI materializer。只扩展评测契约与 instrumentation/testability，不改变播放行为、HDR/DV 策略、DXGI conversion 或阈值。

**Tech Stack:** C# / .NET, xUnit, existing `NextGenEmby.Core.PlaybackQuality`, existing `quality-run` PowerShell smoke tests.

## Global Constraints

- 不新建并行评测框架。
- 不从文件名、display title、`HdrKind` 或 playback strategy 推断 raw source color metadata。
- 只有 manifest 明确声明 raw source color expectation 时，required-signal gate 才要求对应 `source.*` 信号。
- 私有 Emby 服务地址、账号、密码和个人素材路径不得提交。
- 若 baseline 使用 synthetic source，则只能写入 manifest 明确给出的 source color expectation，不能伪造真实采集。

---

### Task 1: Add Expected Raw Source Color Fields

**Files:**
- Modify: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityExpected.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReferenceManifest.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReferenceCaseReportRequestFactory.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityRuntimeEvidenceCollector.cs`

**Interfaces:**
- Consumes: manifest JSON `expected.videoRange`, `expected.colorPrimaries`, `expected.colorTransfer`, `expected.colorSpace`
- Produces: cloned `PlaybackQualityExpected.VideoRange`, `ColorPrimaries`, `ColorTransfer`, `ColorSpace`

- [x] **Step 1: Write failing tests**

Add assertions that `PlaybackQualityReferenceManifestValidator.Validate` preserves expected source color fields, and that `PlaybackQualityReferenceCaseReportRequestFactory.CreateRequest` clones them.

- [x] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter FullyQualifiedName~PlaybackQualityReferenceManifestTests`

Expected: compile/test failure because `PlaybackQualityExpected` lacks raw source color fields.

- [x] **Step 3: Implement minimal fields and clone paths**

Add string properties to `PlaybackQualityExpected`, then clone them anywhere expected metadata is copied.

- [x] **Step 4: Run tests to verify pass**

Run: same command.

### Task 2: Evaluate Source Color Expectations

**Files:**
- Modify: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityEvaluatorTests.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityEvaluator.cs`

**Interfaces:**
- Consumes: `PlaybackQualityExpected.VideoRange`, `ColorPrimaries`, `ColorTransfer`, `ColorSpace`
- Produces: failed checks `ExpectedVideoRange`, `ExpectedColorPrimaries`, `ExpectedColorTransfer`, `ExpectedColorSpace`

- [x] **Step 1: Write failing evaluator test**

Create a report where expected source color metadata is HDR10/bt2020/PQ/bt2020nc but report source contains SDR/bt709/bt709/bt709. Assert `report.Result = fail`, `PrimaryFailureArea = unsupported-source`, and failed checks use `source.videoRange`, `source.colorPrimaries`, `source.colorTransfer`, `source.colorSpace`.

- [x] **Step 2: Run test to verify failure**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter FullyQualifiedName~PlaybackQualityEvaluatorTests`

Expected: failure because evaluator does not compare raw source color metadata.

- [x] **Step 3: Implement minimal comparisons**

Call existing `CheckExpectedString` for the four source color fields in `CheckExpectedSourceMetadata`.

- [x] **Step 4: Run test to verify pass**

Run: same command.

### Task 3: Require Source Color Signals Only When Expected

**Files:**
- Modify: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityRequiredSignalPolicy.cs`

**Interfaces:**
- Consumes: optional source color expectation fields
- Produces: required signals `source.videoRange`, `source.colorPrimaries`, `source.colorTransfer`, `source.colorSpace` only when corresponding expected fields are non-empty

- [x] **Step 1: Write failing report-set gate tests**

Add one test asserting required signals include source color fields when expected values are set, and one test asserting `validate-report-set` reports `report.requiredSignal.missing` when the report lacks them.

- [x] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter FullyQualifiedName~PlaybackQualityReferenceManifestTests`

Expected: failure because required-signal policy does not know these fields.

- [x] **Step 3: Implement policy**

Add non-empty expected checks to `CreateRequiredSignals`; add `HasRequiredSignal` cases reading `report.Source.VideoRange`, `ColorPrimaries`, `ColorTransfer`, `ColorSpace`.

- [x] **Step 4: Run tests to verify pass**

Run: same command.

### Task 4: Propagate Expectations Into Synthetic Baselines And Tools

**Files:**
- Modify: `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityOrchestratorProbe.cs`
- Modify: `tools/quality-run/New-PrivateEmbyReferenceManifest.ps1`
- Modify: `tools/quality-run/New-PrivateEmbyReferenceManifest.tests.ps1`
- Modify: `docs/qa/playback-quality-reference-manifest.example.json`
- Modify: `docs/qa/baselines/v0.1-*`

**Interfaces:**
- Consumes: expected source color fields
- Produces: synthetic descriptors with explicit source color metadata only when expected fields are present

- [x] **Step 1: Add failing tool tests where existing coverage exists**

Update `New-PrivateEmbyReferenceManifest.tests.ps1` expectations so generated manifest expected blocks include explicit source color fields from playback-info.

- [x] **Step 2: Run focused tests**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\New-PrivateEmbyReferenceManifest.tests.ps1`

Expected: failure until script emits expected source color fields.

- [x] **Step 3: Implement propagation**

Map raw stream fields into manifest expected blocks. In source-only and core-probe descriptor builders, copy expected source color fields into the synthetic video stream so explicit expectations can be satisfied by synthetic baselines without inventing values.

- [x] **Step 4: Refresh baselines and verify**

Run the existing materialize/validate/analyze commands, then run `tools\quality-run\run-playback-core-checks.ps1`.

Expected: source-only remains invalid only for known missing runtime telemetry; core-probe and native-harness-skip remain valid.
