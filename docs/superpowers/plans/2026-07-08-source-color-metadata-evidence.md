# Source Color Metadata Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 Emby playback-info 中的 raw 色彩元数据进入 `PlaybackQuality` report、model analysis 和 signal catalog，供模型判断 HDR/DV/color 问题时使用。

**Architecture:** 不新建评测框架，只扩展现有 `EmbyMediaStream` -> `PlaybackQualityReportMapper` -> `PlaybackQualityReportAnalyzer` -> `validate/analyze` 证据链。字段只作为 instrumentation/testability，不改变播放行为、源选择、HDR 输出策略、阈值或 pass/fail 规则。

**Tech Stack:** C# / .NET, xUnit, existing `NextGenEmby.Core.PlaybackQuality`, existing `tools/quality-run` CLI smoke scripts.

## Global Constraints

- 继续基于现有 `NextGenEmby.Core` / `PlaybackQuality` / `quality-run` 工作推进，不新建并行框架。
- 本阶段主目标是可信、可复现、可版本化的评测裁判，不追求播放效果提升。
- 私有 Emby 服务地址、账号、密码和个人素材路径不得提交。
- raw 色彩字段必须来自 Emby playback-info 或采集器明确提供的元数据，不从文件名推断。
- 如果修改只是暴露证据，应记录为 instrumentation/testability；不得改变播放策略或阈值。

---

### Task 1: Preserve Emby Raw Color Metadata

**Files:**
- Modify: `tests/NextGenEmby.Core.Tests/Emby/EmbyPlaybackInfoTests.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyMediaStream.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`

**Interfaces:**
- Consumes: Emby playback-info `MediaStreams[].VideoRange`, `ColorPrimaries`, `ColorTransfer`, `ColorSpace`
- Produces: `EmbyMediaStream.VideoRange`, `ColorPrimaries`, `ColorTransfer`, `ColorSpace`

- [ ] **Step 1: Write the failing test**

```csharp
Assert.Equal("HDR10", video.VideoRange);
Assert.Equal("bt2020", video.ColorPrimaries);
Assert.Equal("smpte2084", video.ColorTransfer);
Assert.Equal("bt2020nc", video.ColorSpace);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter FullyQualifiedName~EmbyPlaybackInfoTests`

Expected: FAIL because `EmbyMediaStream` does not expose raw color metadata properties.

- [ ] **Step 3: Write minimal implementation**

```csharp
public string VideoRange { get; set; } = "";
public string ColorPrimaries { get; set; } = "";
public string ColorTransfer { get; set; } = "";
public string ColorSpace { get; set; } = "";
```

Map these fields in `EmbyApiClient.MapMediaSource` from `MediaStreamDto`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter FullyQualifiedName~EmbyPlaybackInfoTests`

Expected: PASS.

### Task 2: Expose Source Color Metadata In Reports

**Files:**
- Modify: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReportMapperTests.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportMapper.cs`

**Interfaces:**
- Consumes: `EmbyMediaStream.VideoRange`, `ColorPrimaries`, `ColorTransfer`, `ColorSpace`
- Produces: `PlaybackQualitySource.VideoRange`, `ColorPrimaries`, `ColorTransfer`, `ColorSpace`

- [ ] **Step 1: Write the failing test**

```csharp
Assert.Equal("HDR10", report.Source.VideoRange);
Assert.Equal("bt2020", report.Source.ColorPrimaries);
Assert.Equal("smpte2084", report.Source.ColorTransfer);
Assert.Equal("bt2020nc", report.Source.ColorSpace);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter FullyQualifiedName~PlaybackQualityReportMapperTests.ApplySource_Copies_Playback_Source_Metadata`

Expected: FAIL because `PlaybackQualitySource` does not expose raw color metadata properties.

- [ ] **Step 3: Write minimal implementation**

Add the four string properties to `PlaybackQualitySource`, and assign them from the selected video stream in `PlaybackQualityReportMapper.ApplySource`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter FullyQualifiedName~PlaybackQualityReportMapperTests.ApplySource_Copies_Playback_Source_Metadata`

Expected: PASS.

### Task 3: Add Model Evidence Signals

**Files:**
- Modify: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReportAnalyzerTests.cs`
- Modify: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportAnalyzer.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualitySignalCatalog.cs`

**Interfaces:**
- Consumes: `PlaybackQualitySource.VideoRange`, `ColorPrimaries`, `ColorTransfer`, `ColorSpace`
- Produces: analyzer version 5; model signals `source.videoRange`, `source.colorPrimaries`, `source.colorTransfer`, `source.colorSpace`

- [ ] **Step 1: Write the failing tests**

```csharp
Assert.Equal(5, PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion);
Assert.Equal("HDR10", analysis.Source.VideoRange);
Assert.Contains("source.videoRange", analysis.Source.Signals);
Assert.Contains("source.colorPrimaries", analysis.EvidenceSignals);
Assert.Contains("source.colorTransfer", PlaybackQualitySignalCatalog.KnownSignals.Select(signal => signal.Signal));
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~PlaybackQualityReportAnalyzerTests|FullyQualifiedName~PlaybackQualityReferenceManifestTests"`

Expected: FAIL because analyzer/source assessment/catalog do not expose these fields yet.

- [ ] **Step 3: Write minimal implementation**

Add the four fields to `PlaybackQualitySourceAssessment`, copy them in `AssessSource`, include them in `AddSourceSignals`, add them to `PlaybackQualitySignalCatalog`, and bump `CurrentAnalyzerVersion` to 5.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~PlaybackQualityReportAnalyzerTests|FullyQualifiedName~PlaybackQualityReferenceManifestTests"`

Expected: PASS.

### Task 4: Refresh Artifacts And Verify

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Modify: `docs/qa/software-playback-quality-metric-contract.md`
- Modify: `docs/qa/baselines/v0.1-*`
- Modify: `tools/quality-run/run-playback-quality-cli-smoke-test.ps1` if handcrafted JSON fixtures need explicit source color metadata.

**Interfaces:**
- Consumes: analyzer version 5 outputs
- Produces: refreshed v0.1 baseline artifacts with no private Emby secrets

- [ ] **Step 1: Refresh baselines**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1`

Expected: PASS; if analyzer version changes invalidate archived baselines, regenerate them with existing quality-run commands rather than hand-editing results.

- [ ] **Step 2: Run full Core test suite**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore`

Expected: PASS.

- [ ] **Step 3: Check for whitespace and private secrets**

Run: `git diff --check`

Expected: exit 0.

Run a private-secret scan using the repository's current secret patterns before committing. The command must not be copied into this plan with literal private hostnames, passwords, one-time codes, or account names.

Expected: no matches.

- [ ] **Step 4: Commit**

```powershell
git add src tests tools docs
git commit -m "feat: report source color metadata evidence"
```

Expected: commit created on `codex/playback-core-quality-isolated`.
