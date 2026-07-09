# App-Hosted Playback Quality Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the existing DEBUG `quality-run` development route to a minimal App-hosted native playback capture path that writes model-consumable playback quality evidence.

**Architecture:** Keep the durable contract in `NextGenEmby.Core`: run-id to report-path mapping and descriptor-to-reference-case creation. Keep App changes thin: route `quality-run` into `PlaybackPage`, wait the requested capture window, compose a `PlaybackQualityRunResult` from the current descriptor, backend diagnostics, and native metrics provider, then write it under the app local data folder for later CLI import.

**Tech Stack:** C# / .NET Standard Core library, UWP App DEBUG command path, existing `PlaybackQuality` report serializer, existing `materialize-native-harness-report-set --captured-reports-dir` importer.

## Global Constraints

- Do not create a parallel evaluation framework.
- Do not change player behavior to improve playback results in this slice.
- Treat all changes as instrumentation/testability unless separately documented.
- Do not commit private Emby server addresses, credentials, item ids, media-source ids, or personal paths.
- Do not depend on Xbox, display output, or human visual judgement for verification.
- Do not lower stable case standards or modify expected behavior to make reports pass.

---

### Task 1: Core Capture Contract

**Files:**
- Create: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityCapturedReportPath.cs`
- Create: `src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityCaptureReferenceCaseFactory.cs`
- Test: `tests/NextGenEmby.Core.Tests/PlaybackQuality/PlaybackQualityCaptureContractTests.cs`

**Interfaces:**
- Produces: `PlaybackQualityCapturedReportPath.GetReportRelativePath(string runId): string`
- Produces: `PlaybackQualityCaptureReferenceCaseFactory.Create(string runId, PlaybackDescriptor descriptor, PlaybackQualityExpected? expected, string category = "stable", string severity = "medium", string stability = "stable"): PlaybackQualityReferenceCase`

- [x] **Step 1: Write failing tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityCaptureContractTests -v minimal
```

Expected: compile failure because `PlaybackQualityCapturedReportPath` and `PlaybackQualityCaptureReferenceCaseFactory` do not exist.

- [x] **Step 2: Implement minimal Core contract**

Add the path helper and reference-case factory. The path helper must reject empty, absolute, and traversal-style run IDs, while preserving `local/foo -> local/foo.json` compatibility.

- [x] **Step 3: Verify Core contract**

Run the same filtered test command. Expected: all `PlaybackQualityCaptureContractTests` pass.

### Task 2: App DEBUG Quality-Run Routing

**Files:**
- Modify: `src/NextGenEmby.App/Navigation/PlaybackLaunchRequest.cs`
- Modify: `src/NextGenEmby.App/MainPage.xaml.cs`
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`
- Modify if needed: `tools/quality-run/run-playback-core-checks.ps1`
- Modify if needed: `tools/quality-run/run-playback-core-checks.tests.ps1`

**Interfaces:**
- Consumes: `DevelopmentNavigationCommand.Route == "quality-run"`
- Consumes: `PlaybackQualityCapturedReportPath.GetReportRelativePath`
- Consumes: `PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult`
- Produces: captured report JSON under app local data `quality-run/captured/<runId>.json`

- [x] **Step 1: Extend launch request**

Add optional `QualityRunId`, `QualityRunDurationSeconds`, `QualityExpected`, and `QualityCommandReceivedAtUtc` values to `PlaybackLaunchRequest`, with `IsQualityRun` returning true when `QualityRunId` is non-empty.

- [x] **Step 2: Route `quality-run`**

In `MainPage.RunDevelopmentCommand`, add `case "quality-run"` and navigate to `PlaybackPage` with the same playback fields plus quality-run fields from the parsed command.

- [x] **Step 3: Capture after playback window**

In `PlaybackPage.StartItemPlaybackAsync`, after `_orchestrator.StartAsync` succeeds and Emby playback-start reporting is sent, schedule a DEBUG-only background task. The task waits `QualityRunDurationSeconds`, reads `CurrentDescriptor`, backend diagnostics, native metrics provider, startup timing, and environment metadata, then serializes `PlaybackQualityRunResult`.

- [x] **Step 4: Write app-local report**

Write the JSON envelope to app local data under `quality-run/captured/<relative path from PlaybackQualityCapturedReportPath>`. Also update `dev-command-result.txt` with the captured relative path or structured failure so remote automation can find the output.

- [x] **Step 5: Preserve guard boundary**

If App files beyond the current allowed instrumentation file are modified, update `run-playback-core-checks.ps1` and its tests so only explicit playback quality instrumentation files are allowed, not App UI, XAML, project/package, or interaction files.

### Task 3: Verification and Documentation

**Files:**
- Modify: `tools/NextGenEmby.PlaybackQuality.Cli/Program.cs`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Modify: `docs/qa/playback-core-quality-validation.md`
- Modify: `docs/qa/software-playback-quality-metric-contract.md`

**Interfaces:**
- Consumes: `PlaybackQualityCapturedReportPath.GetReportRelativePath`
- Produces: docs explaining App-hosted capture output and import command

- [x] **Step 1: Reuse Core path helper in CLI**

Replace the CLI-local run-id path helper with `PlaybackQualityCapturedReportPath.GetReportRelativePath` so App capture and CLI import use the same key.

- [x] **Step 2: Document the capture flow**

Document that `quality-run` is DEBUG App-hosted evidence collection, not App UI validation and not hardware/display validation. Document the import command using `--captured-reports-dir` without private credentials.

- [x] **Step 3: Run verification**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQuality -v minimal
dotnet build tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1
rg -n "<private-server-host>|<private-password>|<private-account>|<private-xbox-host>|<private-remote-code>" .
```

Expected: tests/build/checks pass; secret scan has no matches.

Also run the App compile check after restore:

```powershell
cmd /c '"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul && msbuild src\NextGenEmby.App\NextGenEmby.App.csproj /t:Restore /p:Configuration=Debug /p:Platform=x64 /v:minimal'
cmd /c '"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul && msbuild src\NextGenEmby.App\NextGenEmby.App.csproj /p:Configuration=Debug /p:Platform=x64 /m /v:minimal'
```

Expected: restore and Debug x64 App build pass; generated AppPackages remain ignored.
