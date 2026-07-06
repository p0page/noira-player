# Live TV Positive Fixture Route Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add and verify a deterministic positive Live TV route that shows browseable channels, current-program previews, and browse-only playback recovery with keyboard/controller input.

**Architecture:** Keep fixture data in `NextGenEmby.Core.Diagnostics`, add a DEBUG-only `LiveTvNavigationRequest` flag, and reuse the existing `LiveTvPage` two-column TV layout. Channel activation remains browse-only and opens the existing unsupported playback panel; closing that panel must restore focus to the invoking channel.

**Tech Stack:** UWP XAML/C#, `NextGenEmby.Core` diagnostics models, xUnit source/behavior tests, MSBuild Debug x64 MSIX, Computer Use keyboard validation.

---

### Task 1: Live TV Fixture Data Contract

**Files:**
- Create: `src/NextGenEmby.Core/Diagnostics/DevelopmentLiveTvFixture.cs`
- Create: `src/NextGenEmby.Core/Diagnostics/DevelopmentLiveTvFixtureSnapshot.cs`
- Modify: `src/NextGenEmby.Core/Diagnostics/DevelopmentNavigationCommand.cs`
- Test: `tests/NextGenEmby.Core.Tests/Diagnostics/DevelopmentLiveTvFixtureTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Diagnostics/DevelopmentNavigationCommandTests.cs`

- [x] **Step 1: Write failing tests for positive Live TV fixture data**

Add tests proving the fixture exposes multiple channels, current programs, type flags, and packaged artwork URIs:

```csharp
[Fact]
public void Create_Provides_Channels_Current_Programs_And_Artwork()
{
    var fixture = DevelopmentLiveTvFixture.Create();

    Assert.True(fixture.Channels.Count >= 4);
    Assert.Contains(fixture.Channels, channel => channel.Number == "101" && channel.CurrentProgram != null);
    Assert.Contains(fixture.Channels, channel => channel.CurrentProgram != null && channel.CurrentProgram.IsNews);
    Assert.Contains(fixture.Channels, channel => channel.CurrentProgram != null && channel.CurrentProgram.IsSports);
    Assert.NotEmpty(fixture.ArtworkUris);
}
```

Add packaged-asset validation for each channel `Primary` key using `DevelopmentLiveTvFixture.ArtworkKey(channel.Id, "Primary")`.

- [x] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "DevelopmentLiveTvFixture|TryParseJson_Accepts_Guide_Routes" -v minimal
```

Expected: FAIL because `DevelopmentLiveTvFixture` and `livetv-fixture` do not exist.

- [x] **Step 3: Implement minimal fixture and route acceptance**

Create a fixture with at least four `EmbyLiveTvChannel` instances:

- `qa-live-news-24`, number `101`, program `Morning Briefing`, `IsNews = true`.
- `qa-live-cinema`, number `202`, program `Matinee Window`, `IsMovie = true`.
- `qa-live-sports`, number `303`, program `Late Match`, `IsSports = true`.
- `qa-live-kids`, number `404`, program `Saturday Workshop`, `IsKids = true`.

Each channel should have `PrimaryImageTag = "qa"` and a packaged image URI under `ms-appx:///Assets/QaHome/qa-wide-*.png`.

Add `livetv-fixture` to `DevelopmentNavigationCommand.IsSupportedRoute`.

- [x] **Step 4: Verify GREEN for fixture tests**

Run the same filtered command. Expected: all selected tests pass.

### Task 2: Live TV Page Fixture Route And Focus Recovery

**Files:**
- Modify: `src/NextGenEmby.App/Navigation/LiveTvNavigationRequest.cs`
- Modify: `src/NextGenEmby.App/MainPage.xaml.cs`
- Modify: `src/NextGenEmby.App/Views/LiveTvPage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/LiveTvPageSourceTests.cs`

- [x] **Step 1: Write failing source tests for fixture rendering and unsupported focus return**

Add source tests proving:

- `MainPage` handles `case "livetv-fixture"`.
- `LiveTvNavigationRequest` exposes `UseDevelopmentFixture`.
- `LiveTvPage` calls `RenderDevelopmentLiveTvFixture()` when the request uses the fixture.
- `LiveTvPage` creates channel artwork from `DevelopmentLiveTvFixture.ArtworkKey`.
- The unsupported layer stores and restores `_unsupportedReturnFocusTarget`.

- [x] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "LiveTvPageSourceTests|DevelopmentNavigationCommandTests" -v minimal
```

Expected: FAIL because the fixture route and focus-return source paths do not exist.

- [x] **Step 3: Implement DEBUG route and fixture rendering**

Update `LiveTvNavigationRequest` to accept:

```csharp
public LiveTvNavigationRequest(
    string unsupportedChannelName = "",
    bool useDevelopmentFixture = false)
```

Add `UseDevelopmentFixture`.

In `MainPage` DEBUG command handling, add:

```csharp
case "livetv-fixture":
    NavigateTo(typeof(LiveTvPage), new LiveTvNavigationRequest(useDevelopmentFixture: true));
    return;
```

In `LiveTvPage`:

- Import `NextGenEmby.Core.Diagnostics` under `#if DEBUG`.
- Store a `DevelopmentLiveTvFixtureSnapshot?`.
- If `_request.UseDevelopmentFixture`, render channels without session loading.
- Create packaged-image channel buttons for the fixture.
- Set `StatusBlock.Text` to `Fixture Live TV guide`.
- Keep the first channel as the default focus.
- Store `_unsupportedReturnFocusTarget = sender as Button` before showing the unsupported panel.
- On close, focus `_unsupportedReturnFocusTarget` before falling back to first channel.

- [x] **Step 4: Verify GREEN**

Run the same filtered command. Expected: all selected tests pass.

### Task 3: Build, Keyboard Validation, Docs, Commit

**Files:**
- Modify: `src/NextGenEmby.App/Package.appxmanifest`
- Modify: `docs/qa/emby-tv-client-operation-matrix.md`
- Modify: `docs/qa/emby-tv-client-keyboard-checklist.md`
- Modify: this plan checkbox state

- [x] **Step 1: Run all Core tests**

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [x] **Step 2: Build Debug x64 app package**

Increment `Package.appxmanifest` patch version, then run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /p:UseSharedCompilation=false
```

Expected: 0 warnings, 0 errors.

- [x] **Step 3: Sign, install, and keyboard-validate with Computer Use**

Launch `livetv-fixture` through `dev-command.json`. Validate without app-content mouse clicks:

- Initial page shows `Live TV`, `Fixture Live TV guide`, at least four channels, and first channel focused.
- `Down` moves to the second channel and the right-side `Now` preview updates.
- Additional `Down` reaches a sports or kids channel while keeping focus visible.
- `Return` opens `Live TV playback unavailable` for the focused channel.
- `Escape` closes the unsupported layer and restores focus to the same channel.
- `Up` and `Down` still move within the channel list after returning from the layer.

- [x] **Step 4: Update QA docs and commit**

Record the run under `docs/qa/emby-tv-client-keyboard-checklist.md`, update the Live TV rows in `docs/qa/emby-tv-client-operation-matrix.md`, then commit:

```powershell
git add docs src tests
git commit -m "test: add live tv browse fixture route"
```
