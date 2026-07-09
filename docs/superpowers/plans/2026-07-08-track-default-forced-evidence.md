# Track Default Forced Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture `default` and `forced` track metadata as model-readable playback-quality evidence.

**Architecture:** Extend the existing Emby stream model, playback-quality report model, mapper, analyzer, and signal catalog. Keep this as instrumentation/testability only; do not alter playback selection, switching, decoding, rendering, or pass/fail thresholds.

**Tech Stack:** C# / .NET, `NextGenEmby.Core`, xUnit, existing `tools/NextGenEmby.PlaybackQuality.Cli`.

## Global Constraints

- Continue existing `NextGenEmby.Core` / `PlaybackQuality` / `quality-run` work; do not create a parallel evaluation framework.
- Add tests before production code.
- Private Emby service addresses, credentials, and personal media identifiers must not be committed.
- Do not change playback behavior in this task.

---

### Task 1: Emby Stream Metadata Mapping

**Files:**
- Modify: `src/NextGenEmby.Core/Emby/EmbyMediaStream.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyPlaybackInfoTests.cs`

**Interfaces:**
- Produces: `EmbyMediaStream.IsDefault` and `EmbyMediaStream.IsForced` as nullable booleans.
- Consumes: Emby playback-info stream JSON fields `IsDefault` and `IsForced`.

- [x] **Step 1: Write the failing test**

Add assertions to an existing playback-info mapping test that includes:

```json
"IsDefault": true,
"IsForced": true
```

and verifies:

```csharp
Assert.True(stream.IsDefault);
Assert.True(stream.IsForced);
```

- [x] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~EmbyPlaybackInfoTests -v minimal
```

Expected: fail because `IsDefault` / `IsForced` are not exposed.

- [x] **Step 3: Write minimal implementation**

Add nullable properties:

```csharp
public bool? IsDefault { get; set; }
public bool? IsForced { get; set; }
```

and map DTO values into them.

- [x] **Step 4: Run test to verify it passes**

Run the same focused test command. Expected: pass.

### Task 2: Playback Quality Report and Analyzer Evidence

**Files:**
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportMapper.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportAnalyzer.cs`
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualitySignalCatalog.cs`
- Test: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReportMapperTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityReportAnalyzerTests.cs`

**Interfaces:**
- Consumes: `EmbyMediaStream.IsDefault` and `EmbyMediaStream.IsForced`.
- Produces: `PlaybackQualityTrack.IsDefault`, `PlaybackQualityTrack.IsForced`, and evidence signals such as `tracks.audio.isDefault`, `tracks.audio.isForced`, `tracks.subtitles.isDefault`, and `tracks.subtitles.isForced`.

- [x] **Step 1: Write failing mapper and analyzer tests**

Add mapper assertions:

```csharp
Assert.True(audio.IsDefault);
Assert.False(audio.IsForced);
Assert.False(subtitle.IsDefault);
Assert.True(subtitle.IsForced);
```

Add analyzer assertions:

```csharp
Assert.Contains("tracks.audio.isDefault", analysis.EvidenceSignals);
Assert.Contains("tracks.audio.isForced", analysis.EvidenceSignals);
Assert.Contains("tracks.subtitles.isDefault", analysis.EvidenceSignals);
Assert.Contains("tracks.subtitles.isForced", analysis.EvidenceSignals);
```

- [x] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityReportMapperTests -v minimal
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityReportAnalyzerTests -v minimal
```

Expected: fail because the report and analyzer do not expose those fields/signals.

- [x] **Step 3: Write minimal implementation**

Map nullable metadata through report tracks and add evidence signals when the nullable value is present.

- [x] **Step 4: Run tests to verify they pass**

Run the same focused test commands. Expected: pass.

### Task 3: Probe Samples, Docs, Verification, Commit

**Files:**
- Modify: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityOrchestratorProbe.cs`
- Modify: `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Produces: core-probe reports that contain default/forced evidence for representative audio/subtitle tracks.

- [x] **Step 1: Mark diagnostic probe streams**

Set first audio stream `IsDefault = true`, second audio stream `IsDefault = false`, and subtitle stream `IsDefault = false`, `IsForced = false`.

- [x] **Step 2: Add capability coverage signals**

Add default/forced signals to `tracks` and `subtitles` capability definitions so report-set analysis can expose missing coverage.

- [x] **Step 3: Run verification**

Run:

```powershell
dotnet build tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-quality-cli-smoke-test.ps1
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQuality -v minimal
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~EmbyPlaybackInfoTests -v minimal
git diff --check
```

Expected: pass, except existing CRLF warnings from `git diff --check` are acceptable if no new whitespace errors are introduced.

- [x] **Step 4: Commit**

```powershell
git add src\NextGenEmby.Core tests\NextGenEmby.Core.Tests tools\NextGenEmby.PlaybackQuality.Cli docs
git commit -m "feat: capture track default forced evidence"
```
