# Collections Playlists Positive Fixture Route Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add and verify deterministic positive browsing routes for Collections and Playlists, treating `BoxSet` and `Playlist` as couch-browse containers instead of dead-end detail cards.

**Architecture:** Keep the surface on the existing `LibraryPage` grid so sort/filter, TV safe area, artwork cards, nested navigation, and focus restoration stay consistent. Add one diagnostics fixture in `NextGenEmby.Core.Diagnostics`, route `collections-fixture` and `playlists-fixture` through DEBUG startup commands, and update `LibraryItemActivationPolicy` so `BoxSet` and `Playlist` browse child items like folders.

**Tech Stack:** UWP XAML/C#, `NextGenEmby.Core` diagnostics models, xUnit behavior/source tests, MSBuild Debug x64 MSIX, Computer Use keyboard validation.

---

### Task 1: Organization Fixture And Routes

**Files:**
- Create: `src/NextGenEmby.Core/Diagnostics/DevelopmentLibraryOrganizationFixture.cs`
- Create: `src/NextGenEmby.Core/Diagnostics/DevelopmentLibraryOrganizationFixtureSnapshot.cs`
- Modify: `src/NextGenEmby.Core/Diagnostics/DevelopmentNavigationCommand.cs`
- Modify: `src/NextGenEmby.App/MainPage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Diagnostics/DevelopmentLibraryOrganizationFixtureTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Diagnostics/DevelopmentNavigationCommandTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/LibraryPageSourceTests.cs`

- [x] **Step 1: Write failing fixture and route tests**

Add tests proving the fixture contains root collections/playlists, child media with `ParentId`, and packaged artwork:

```csharp
var fixture = DevelopmentLibraryOrganizationFixture.Create();

Assert.Contains(fixture.Items, item => item.Id == "fixture-collection-signal" && item.Type == "BoxSet");
Assert.Contains(fixture.Items, item => item.Id == "fixture-playlist-weekend" && item.Type == "Playlist");
Assert.Contains(fixture.GetItemsForParent("fixture-collection-signal"), item => item.Type == "Movie");
Assert.Contains(fixture.GetItemsForParent("fixture-playlist-weekend"), item => item.Type == "Episode");
```

Extend `DevelopmentNavigationCommandTests.TryParseJson_Accepts_Guide_Routes` with:

```csharp
[InlineData("Collections-Fixture", "collections-fixture")]
[InlineData("Playlists-Fixture", "playlists-fixture")]
```

Extend `LibraryPageSourceTests` with source assertions that `MainPage` routes both fixture commands through `DevelopmentLibraryOrganizationFixture.Create()` and passes `fixture.Items` plus `fixture.ArtworkUris` into `LibraryNavigationRequest`.

- [x] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "DevelopmentLibraryOrganizationFixtureTests|DevelopmentNavigationCommandTests|LibraryPageSourceTests" -v minimal
```

Expected: FAIL because `DevelopmentLibraryOrganizationFixture`, `collections-fixture`, and `playlists-fixture` do not exist.

- [x] **Step 3: Implement fixture and DEBUG routes**

Create a fixture with:

- root collection `fixture-collection-signal`, name `Signal Archives`, type `BoxSet`, `ParentId = ""`, `ChildCount = 4`, `Thumb` artwork;
- root collection `fixture-collection-city`, name `City Nights`, type `BoxSet`, `ParentId = ""`, `ChildCount = 3`, `Thumb` artwork;
- root playlist `fixture-playlist-weekend`, name `Weekend Queue`, type `Playlist`, `ParentId = ""`, `ChildCount = 5`, `Thumb` artwork;
- root playlist `fixture-playlist-documentary`, name `Documentary Stack`, type `Playlist`, `ParentId = ""`, `ChildCount = 3`, `Thumb` artwork;
- child movies/episodes whose `ParentId` points to those root ids and whose `Primary`, `Backdrop`, and `Thumb` artwork use `ms-appx:///Assets/QaHome/qa-*.png`.

Add `collections-fixture` and `playlists-fixture` to supported DEBUG routes. In `MainPage.RunDevelopmentCommand`, route them to `LibraryPage` with collection type `boxsets` or `playlists`, include item type `BoxSet` or `Playlist`, `requireItemTypeMatch: true`, and fixture items/artwork.

- [x] **Step 4: Verify GREEN**

Run the same filtered command. Expected: all selected tests pass.

### Task 2: Browse Activation For BoxSet And Playlist

**Files:**
- Modify: `src/NextGenEmby.Core/Input/LibraryItemActivationPolicy.cs`
- Test: `tests/NextGenEmby.Core.Tests/Input/LibraryItemActivationPolicyTests.cs`

- [x] **Step 1: Write failing activation tests**

Change the current standard-media theory so `BoxSet` and `Playlist` are no longer expected to open Details. Add explicit tests:

```csharp
[Theory]
[InlineData("BoxSet")]
[InlineData("Playlist")]
public void ChooseRoute_Opens_Organization_Items_As_Browse_Folders(string itemType)
{
    var route = LibraryItemActivationPolicy.ChooseRoute(itemType);

    Assert.Equal(LibraryItemActivationRoute.BrowseFolder, route);
}
```

- [x] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "LibraryItemActivationPolicyTests" -v minimal
```

Expected: FAIL because `BoxSet` and `Playlist` still route to Details.

- [x] **Step 3: Implement minimal activation policy**

Update `ChooseRoute` so `Photo` opens `PhotoViewer`, and `Folder`, `BoxSet`, and `Playlist` open `BrowseFolder`. Keep unknown item types routing to Details.

- [x] **Step 4: Verify GREEN**

Run the same activation-policy command. Expected: all selected tests pass.

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

Launch `collections-fixture` through `dev-command.json`. Validate without app-content mouse clicks:

- initial page shows `Collections`, at least two BoxSet cards, and first collection focused;
- `Return` opens the focused collection as a nested Library grid with child movies;
- `Right` moves across child media while focus remains visible;
- `Escape` returns to root Collections with focus restored to the originating collection;
- launch `playlists-fixture`, open a playlist, move across child episodes/movies, and `Escape` returns focus to the originating playlist.

- [x] **Step 4: Update QA docs and commit**

Record the run in `docs/qa/emby-tv-client-keyboard-checklist.md`, update the Collections/Playlists rows in `docs/qa/emby-tv-client-operation-matrix.md`, mark this plan complete, then commit:

```powershell
git add docs src tests
git commit -m "test: add collections playlists fixture routes"
```
