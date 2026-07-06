# Home Sections Artwork Rail Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make server-configured Emby home sections behave like first-class TV category entrances with dedicated wide artwork, instead of relying on child item posters as the primary visual.

**Architecture:** Keep Emby DTO parsing and artwork choice in `NextGenEmby.Core`, then let `HomePage` consume the same semantic artwork candidates already used by libraries. Add a deterministic DEBUG fixture route so the controller path can be validated locally without a saved Emby session.

**Tech Stack:** UWP XAML/C#, `NextGenEmby.Core` C# policies/models, xUnit tests, MSBuild Debug x64 MSIX, Computer Use keyboard validation.

---

### Task 1: Emby Section Artwork Data Contract

**Files:**
- Modify: `src/NextGenEmby.Core/Emby/EmbyHomeSection.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyLibraryTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyArtworkPolicyTests.cs`

- [x] **Step 1: Write failing tests for HomeSections image query and mapping**

Add a test that returns a HomeSections payload with image fields on the section itself and verifies the client preserves them:

```csharp
[Fact]
public async Task GetHomeSectionsAsync_Requests_And_Maps_Section_Artwork()
{
    HttpRequestMessage? request = null;
    using var http = new HttpClient(new TestHttpMessageHandler(req =>
    {
        request = req;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            [
              {
                "Id": "sec-hot-movies",
                "Name": "Hot Movies",
                "CollectionType": "movies",
                "ImageTags": { "Thumb": "section-thumb", "Banner": "section-banner" },
                "BackdropImageTags": [ "section-backdrop" ],
                "ParentThumbItemId": "section-thumb-owner",
                "ParentBackdropItemId": "section-backdrop-owner",
                "ParentBannerItemId": "section-banner-owner",
                "ParentItem": {
                  "Id": "fallback-parent",
                  "Name": "Fallback",
                  "Type": "CollectionFolder",
                  "ImageTags": { "Primary": "fallback-primary" }
                }
              }
            ]
            """)
        };
    }));
    var client = CreateClient(http);

    var sections = await client.GetHomeSectionsAsync(CreateSession());

    Assert.Contains("EnableImages=true", request!.RequestUri!.Query);
    Assert.Contains("EnableImageTypes=Primary%2CBackdrop%2CThumb%2CBanner%2CLogo", request.RequestUri.Query);
    var section = Assert.Single(sections);
    Assert.Equal("section-thumb", section.ThumbImageTag);
    Assert.Equal("section-thumb-owner", section.ThumbImageItemId);
    Assert.Equal("section-backdrop", section.BackdropImageTag);
    Assert.Equal("section-backdrop-owner", section.BackdropImageItemId);
    Assert.Equal("section-banner", section.BannerImageTag);
    Assert.Equal("section-banner-owner", section.BannerImageItemId);
}
```

- [x] **Step 2: Run the targeted test and verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "GetHomeSectionsAsync_Requests_And_Maps_Section_Artwork" -v minimal
```

Expected: FAIL because `GetHomeSectionsAsync` does not request images and `EmbyHomeSection` does not expose section-level image fields.

- [x] **Step 3: Implement minimal section artwork mapping**

Add image tag and item-id properties to `EmbyHomeSection`, add HomeSections image query parameters, extend `HomeSectionDto`, and map section-level `Thumb`, `Backdrop`, `Banner`, `Primary`, and `Logo`.

- [x] **Step 4: Add artwork policy test for section-owned artwork priority**

Add:

```csharp
[Fact]
public void SelectHomeSectionWideArtwork_Prefers_Section_Artwork_Before_Parent_Item()
{
    AssertCandidate(
        "section-thumb-owner",
        "Thumb",
        900,
        EmbyArtworkPolicy.SelectHomeSectionWideArtwork(new EmbyHomeSection
        {
            Id = "sec-hot-movies",
            ThumbImageTag = "section-thumb",
            ThumbImageItemId = "section-thumb-owner",
            ParentItem = new EmbyMediaItem
            {
                Id = "fallback-parent",
                ThumbImageTag = "fallback-thumb",
                ThumbImageItemId = "fallback-thumb-owner"
            }
        }, 900));
}
```

- [x] **Step 5: Verify RED, then update `EmbyArtworkPolicy.SelectHomeSectionWideArtwork`**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "SelectHomeSectionWideArtwork_Prefers_Section_Artwork_Before_Parent_Item" -v minimal
```

Expected RED: returned candidate uses the parent item. GREEN: select section `Thumb`, `Backdrop`, `Banner`, `Primary` before falling back to `ParentItem`.

### Task 2: Home Section Entrance Rail

**Files:**
- Modify: `src/NextGenEmby.App/Views/HomePage.xaml`
- Modify: `src/NextGenEmby.App/Views/HomePage.xaml.cs`
- Modify: `src/NextGenEmby.Core/Diagnostics/DevelopmentHomeFixture.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/HomeAccessibilitySourceTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Diagnostics/DevelopmentHomeFixtureTests.cs`

- [x] **Step 1: Write failing source tests for a dedicated section rail**

Add assertions that Home has a `HomeSectionsPanel`, a section title such as `Server sections`, and section buttons use section-owned artwork through `EmbyArtworkPolicy.SelectHomeSectionWideArtwork(section, maxWidth)` rather than reconstructing only `ParentItem`.

- [x] **Step 2: Run targeted source tests and verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "Home" -v minimal
```

Expected: FAIL because Home sections are currently appended to `LibrariesPanel`.

- [x] **Step 3: Implement the rail**

Add a `Server sections` horizontal rail between `Media Libraries` and `StatusBlock`. Render configured HomeSections into that rail with `CreateHomeSectionButton(EmbyHomeSection section, IReadOnlyList<EmbyMediaItem> items)`, keep automation names, and keep `_libraryButtons` as the shared top-rail focus collection so existing Home D-pad policy still works.

- [x] **Step 4: Verify home fixture artwork covers section-level keys**

Update `DevelopmentHomeFixture` configured row data to carry section-level `Thumb` image tags. Add a test that checks `fixture.ConfiguredRows` exposes section-owned artwork and that packaged QA assets exist for those section IDs.

### Task 3: Verification, Docs, Commit

**Files:**
- Modify: `docs/qa/emby-tv-client-operation-matrix.md`
- Modify: `docs/qa/emby-tv-client-keyboard-checklist.md`
- Modify: `src/NextGenEmby.App/Package.appxmanifest`

- [x] **Step 1: Run all Core tests**

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [x] **Step 2: Build, sign, install**

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /p:UseSharedCompilation=false
```

Expected: Debug x64 MSIX builds with 0 errors. Increment the app manifest patch version before install if package identity content changed.

- [x] **Step 3: Keyboard-only Computer Use validation**

Use `dev-command.json` route `home-fixture`. Validate:

- Initial focus lands on Hero `Play`.
- `Down` reaches the first Media Library card.
- `Right` moves across library cards, then dedicated server-section cards with visible wide artwork.
- `Return` on a server-section card opens the matching Library route.
- `Escape` returns Home to the originating section card.
- No app-content mouse clicks are used.

- [x] **Step 4: Update QA docs and commit**

Record the run under `docs/qa/emby-tv-client-keyboard-checklist.md`, update Home matrix evidence, then commit:

```powershell
git add docs src tests
git commit -m "feat: add home section artwork rail"
```
