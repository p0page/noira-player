# Photos Positive Fixture Route Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add and verify a deterministic positive Photos route that behaves like a TV photo library: browse albums and photos, open a photo in the immersive viewer, and return to the originating grid item with keyboard/controller input.

**Architecture:** Keep fixture data and routing decisions in `NextGenEmby.Core`, reuse the existing `LibraryPage` for the Photos grid, and pass a DEBUG-only local image URI into `PhotoViewerPage` so local validation does not require a saved Emby session. Folder activation should open a nested `LibraryPage` request using the same Photos query and fixture item set.

**Tech Stack:** UWP XAML/C#, `NextGenEmby.Core` C# diagnostics/input policies, xUnit source/behavior tests, MSBuild Debug x64 MSIX, Computer Use keyboard validation.

---

### Task 1: Photos Fixture Data Contract

**Files:**
- Create: `src/NextGenEmby.Core/Diagnostics/DevelopmentPhotosFixture.cs`
- Modify: `src/NextGenEmby.Core/Diagnostics/DevelopmentNavigationCommand.cs`
- Test: `tests/NextGenEmby.Core.Tests/Diagnostics/DevelopmentPhotosFixtureTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Diagnostics/DevelopmentNavigationCommandTests.cs`
- Modify: `src/NextGenEmby.App/NextGenEmby.App.csproj`

- [x] **Step 1: Write failing tests for photos fixture rows**

Add tests proving the fixture exposes root albums/photos, nested folder contents, and packaged artwork keys:

```csharp
[Fact]
public void Create_Items_Contains_Root_Album_And_Photos()
{
    var fixture = DevelopmentPhotosFixture.Create();

    Assert.Contains(fixture.Items, item => item.Id == "fixture-photo-album-night-market" && item.Type == "Folder");
    Assert.Contains(fixture.Items, item => item.Id == "fixture-photo-rooftop" && item.Type == "Photo" && item.ParentId == "");
    Assert.Contains(fixture.ArtworkUris.Keys, key => key == DevelopmentPhotosFixture.ArtworkKey("fixture-photo-rooftop", "Primary"));
}

[Fact]
public void GetItemsForParent_Returns_Nested_Photos_For_Album()
{
    var fixture = DevelopmentPhotosFixture.Create();

    var items = fixture.GetItemsForParent("fixture-photo-album-night-market");

    Assert.All(items, item => Assert.Equal("fixture-photo-album-night-market", item.ParentId));
    Assert.Contains(items, item => item.Id == "fixture-photo-lanterns");
}
```

- [x] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "DevelopmentPhotosFixture|TryParseJson_Accepts_Photos_Fixture_Route" -v minimal
```

Expected: FAIL because `DevelopmentPhotosFixture` and `photos-fixture` do not exist.

- [x] **Step 3: Implement minimal fixture**

Create `DevelopmentPhotosFixture` with:

- `Items`: root `Folder` plus root `Photo` items and nested `Photo` items.
- `ArtworkUris`: `ms-appx:///Assets/QaHome/qa-wide-*.png` and `qa-poster-*.png` values keyed by `ArtworkKey(itemId, "Primary")`.
- `GetItemsForParent(string parentId)`: root items when `parentId` is blank, otherwise children with matching `ParentId`.

Add `photos-fixture` to `DevelopmentNavigationCommand.IsSupportedRoute`.

- [x] **Step 4: Verify GREEN for fixture tests**

Run the same filtered test command. Expected: all selected tests pass.

### Task 2: Folder And Photo Activation Routing

**Files:**
- Modify: `src/NextGenEmby.Core/Input/LibraryItemActivationPolicy.cs`
- Test: `tests/NextGenEmby.Core.Tests/Input/LibraryItemActivationPolicyTests.cs`
- Modify: `src/NextGenEmby.App/Navigation/PhotoViewerNavigationRequest.cs`
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/LibraryPageSourceTests.cs`

- [x] **Step 1: Write failing tests for Folder route and fixture photo URI handoff**

Add a policy test:

```csharp
[Fact]
public void ChooseRoute_Opens_Folder_Browse_For_Folders()
{
    var route = LibraryItemActivationPolicy.ChooseRoute("Folder");

    Assert.Equal(LibraryItemActivationRoute.BrowseFolder, route);
}
```

Add source tests proving `LibraryPage` navigates folders to `LibraryPage` and passes fixture image URIs to `PhotoViewerNavigationRequest` for Photo items.

- [x] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "LibraryItemActivationPolicyTests|LibraryPageSourceTests" -v minimal
```

Expected: FAIL because the policy has no folder route and Photo viewer navigation cannot carry fixture image URIs.

- [x] **Step 3: Implement activation behavior**

Update `LibraryItemActivationRoute` with `BrowseFolder`.

In `LibraryPage.ActivateLibraryItem`:

- `PhotoViewer`: resolve a fixture/local image URI from `request.DevelopmentArtworkUris` using `EmbyArtworkPolicy.SelectPosterArtwork(item, 1920)` and pass it into `PhotoViewerNavigationRequest`.
- `BrowseFolder`: navigate to another `LibraryPage` with the same collection type, include item types, and query, but with `ParentId = item.Id` and the same development item/artwork sets.
- `Details`: keep existing behavior.

Update `PhotoViewerNavigationRequest` to accept optional `developmentImageUri`.

- [x] **Step 4: Verify GREEN**

Run the same filtered command. Expected: all selected tests pass.

### Task 3: Photos Fixture Route And Viewer Image Loading

**Files:**
- Modify: `src/NextGenEmby.App/MainPage.xaml.cs`
- Modify: `src/NextGenEmby.App/Views/PhotoViewerPage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/PhotoViewerSourceTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/LibraryPageSourceTests.cs`

- [x] **Step 1: Write failing source tests for route and local image loading**

Add tests proving:

- `MainPage` handles `photos-fixture`.
- The route creates a `LibraryNavigationRequest("Photos", "photos", "Photo,Folder", ..., new LibraryNavigationQuery(mediaTypes: "Photo", requireItemTypeMatch: true), fixture.Items, fixture.ArtworkUris)`.
- `PhotoViewerPage` checks `DevelopmentImageUri` before loading the saved session.

- [x] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "PhotoViewerSourceTests|LibraryPageSourceTests" -v minimal
```

Expected: FAIL because no source path exists.

- [x] **Step 3: Implement route and local viewer image**

In DEBUG command handling, add:

```csharp
case "photos-fixture":
    var fixture = DevelopmentPhotosFixture.Create();
    NavigateLibrary(new LibraryNavigationRequest(
        "Photos",
        "photos",
        "Photo,Folder",
        "",
        "",
        new LibraryNavigationQuery(mediaTypes: "Photo", requireItemTypeMatch: true),
        fixture.Items,
        fixture.ArtworkUris));
    return;
```

In `PhotoViewerPage_OnLoaded`, if `_request.DevelopmentImageUri` is present, set `PhotoImage.Source` to that URI and return before session loading.

- [x] **Step 4: Verify GREEN**

Run the same filtered command. Expected: all selected tests pass.

### Task 4: Build, Keyboard Validation, Docs, Commit

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

Launch `photos-fixture` through `dev-command.json`. Validate without app-content mouse clicks:

- Initial Library shows `Photos`, a count, root album/photo cards, and first item focused.
- `Return` on the album opens the nested album grid.
- `Down`/`Right` moves through nested photos while focus remains visible.
- `Return` on a photo opens immersive `PhotoViewerPage` and loads the fixture image without `Sign in first`.
- `Escape` returns to the nested grid with focus restored or at least returned to the grid.
- `Escape` again returns to the root Photos grid.

- [x] **Step 4: Update QA docs and commit**

Record the run under `docs/qa/emby-tv-client-keyboard-checklist.md`, update the Photos rows in `docs/qa/emby-tv-client-operation-matrix.md`, then commit:

```powershell
git add docs src tests
git commit -m "test: add photos browse fixture route"
```
