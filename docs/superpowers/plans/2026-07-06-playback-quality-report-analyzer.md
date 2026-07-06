# Playback Quality Report Analyzer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不依赖 App 打包的前提下，为播放质量报告增加模型可消费的诊断摘要、次要失败分区和缺失证据清单。

**Architecture:** 分析器只依赖 `NextGenEmby.Core.PlaybackQuality` DTO，读取已经生成的 `PlaybackQualityReport`，输出新的 Core DTO。它不启动播放器、不访问 Emby、不构建 UWP App，因此可以和交互 worktree 并行工作。

**Tech Stack:** C# netstandard2.0, xUnit, PowerShell core/native validation script.

---

### Task 1: Core Report Analyzer

**Files:**
- Create: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportAnalyzer.cs`
- Create: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReportAnalyzerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Analyze_Preserves_Secondary_Failure_Areas_For_Model_Triage()
{
    var report = new PlaybackQualityReport
    {
        RunId = "hdr-and-pacing",
        Result = "fail",
        Analysis = new PlaybackQualityAnalysis
        {
            PrimaryFailureArea = "color-pipeline",
            SuggestedNextAction = "Inspect HDR display switch and DXGI color-space mapping."
        }
    };
    report.Checks.Add(new PlaybackQualityCheck { Status = "fail", FailureArea = "color-pipeline", Signal = "colorPipeline.actualHdrOutput" });
    report.Checks.Add(new PlaybackQualityCheck { Status = "fail", FailureArea = "frame-pacing", Signal = "timing.maxFrameGapMs" });

    var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

    Assert.Equal("color-pipeline", analysis.PrimaryFailureArea);
    Assert.Equal(new[] { "color-pipeline", "frame-pacing" }, analysis.FailureAreas);
    Assert.Contains("timing.maxFrameGapMs", analysis.EvidenceSignals);
}

[Fact]
public void Analyze_Reports_Missing_Evidence_For_Unset_Critical_Signals()
{
    var report = new PlaybackQualityReport { RunId = "missing-evidence", Result = "observed" };

    var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

    Assert.Contains("source.codec", analysis.MissingEvidence);
    Assert.Contains("timing.renderedVideoFrames", analysis.MissingEvidence);
    Assert.Contains("colorPipeline.dxgiInput", analysis.MissingEvidence);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter PlaybackQualityReportAnalyzerTests -v minimal`

Expected: compile failure because `PlaybackQualityReportAnalyzer` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `PlaybackQualityReportAnalyzer` with immutable report fields:

```csharp
public sealed class PlaybackQualityModelAnalysis
{
    public string RunId { get; set; } = "";
    public string Result { get; set; } = "";
    public string PrimaryFailureArea { get; set; } = "none";
    public string SuggestedNextAction { get; set; } = "";
    public List<string> FailureAreas { get; } = new List<string>();
    public List<string> EvidenceSignals { get; } = new List<string>();
    public List<string> MissingEvidence { get; } = new List<string>();
    public List<string> Limitations { get; } = new List<string>();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter PlaybackQualityReportAnalyzerTests -v minimal`

Expected: analyzer tests pass.

### Task 2: App-Free Validation Coverage

**Files:**
- Modify: `docs/qa/playback-core-quality-validation.md`

- [ ] **Step 1: Document analyzer in the App-free quality path**

Add one bullet under model-facing output explaining that report analyzer output lists secondary failure areas and missing evidence for model-driven iteration.

- [ ] **Step 2: Run full App-free checks**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1`

Expected: Core tests, native helper test, native restore, and native build pass without building `NextGenEmby.App.csproj`.
