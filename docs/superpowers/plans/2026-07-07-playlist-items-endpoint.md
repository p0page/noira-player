# Playlist Items Endpoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (or continue inline with the same discipline) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make real Emby playlist browsing use the standard `GET /Playlists/{Id}/Items` endpoint instead of relying on generic `ParentId` item queries.

**Architecture:** Keep the endpoint in `NextGenEmby.Core.Emby.EmbyApiClient` so the data path is testable without UWP. Keep `LibraryPage` as the view router: normal folders and collections continue through `GetItemsAsync`, while playlist child pages call the playlist endpoint. Do not touch native playback or transcoding.

**Tech Stack:** C#, .NET Standard Core library, xUnit source and API tests, UWP XAML code-behind, MSBuild Debug x64, local MSIX keyboard verification.

**Reference:** Emby DEV documents playlist item retrieval as `GET /Playlists/{Id}/Items` with `UserId`, `StartIndex`, `Limit`, and `Fields`.

---

### Task 1: Core Playlist Items API

**Files:**
- Modify: `tests/NextGenEmby.Core.Tests/Emby/EmbyLibraryTests.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`

- [x] **Step 1: Write the failing API test**

Add a test near the playlist add tests:

```csharp
[Fact]
public async Task GetPlaylistItemsAsync_Uses_Playlist_Items_Endpoint_And_Maps_Items()
{
    var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
        HttpStatusCode.OK,
        """
        {
          "Items": [
            {
              "Id": "episode-1",
              "Name": "Queued Episode",
              "Type": "Episode",
              "ImageTags": { "Primary": "primary-tag" },
              "BackdropImageTags": [ "backdrop-tag" ]
            }
          ],
          "TotalRecordCount": 1
        }
        """));
    using var http = new HttpClient(handler);
    var client = CreateClient(http);

    var items = await client.GetPlaylistItemsAsync(Session(userId: "user 1/slash"), "playlist 1/slash", 24);

    var item = Assert.Single(items);
    Assert.Equal("episode-1", item.Id);
    Assert.Equal("Queued Episode", item.Name);
    Assert.Equal("Episode", item.Type);
    Assert.Equal("primary-tag", item.PrimaryImageTag);
    Assert.Equal("backdrop-tag", item.BackdropImageTag);
    Assert.Equal("/Playlists/playlist%201%2Fslash/Items", handler.LastRequest!.RequestUri!.AbsolutePath);
    Assert.Contains("UserId=user%201%2Fslash", handler.LastRequest.RequestUri.Query);
    Assert.Contains("Limit=24", handler.LastRequest.RequestUri.Query);
    Assert.Contains("Fields=Overview%2CProductionYear%2CRunTimeTicks%2CPrimaryImageAspectRatio%2CChildCount%2CUserData", handler.LastRequest.RequestUri.Query);
    Assert.Contains("EnableImages=true", handler.LastRequest.RequestUri.Query);
    Assert.Contains("EnableImageTypes=Primary%2CBackdrop%2CThumb%2CBanner%2CLogo", handler.LastRequest.RequestUri.Query);
    Assert.Contains("ImageTypeLimit=1", handler.LastRequest.RequestUri.Query);
}
```

- [x] **Step 2: Run RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "GetPlaylistItemsAsync_Uses_Playlist_Items_Endpoint_And_Maps_Items" -v minimal
```

Expected: compile fails because `GetPlaylistItemsAsync` does not exist.

- [x] **Step 3: Implement the endpoint**

Add this public method to `EmbyApiClient`, near `GetHomeSectionItemsAsync`:

```csharp
public async Task<IReadOnlyList<EmbyMediaItem>> GetPlaylistItemsAsync(
    EmbySession session,
    string playlistId,
    int limit)
{
    var parameters = new List<string>();
    AddQueryParameter(parameters, "UserId", session.UserId);
    AddQueryParameter(parameters, "Limit", Math.Max(1, limit).ToString());
    AddQueryParameter(parameters, "Fields", ItemListFields);
    AddImageQueryParameters(parameters);

    using var request = new HttpRequestMessage(
        HttpMethod.Get,
        $"Playlists/{EscapeUriComponent(playlistId)}/Items?{string.Join("&", parameters)}");
    EmbyAuthorization.Apply(request, _options, session);

    using var response = await _http.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var dto = JsonSerializer.Deserialize<ItemListDto<ItemDto>>(body, _jsonOptions) ?? new ItemListDto<ItemDto>();
    return (dto.Items ?? new List<ItemDto>()).Select(MapItem).ToList();
}
```

- [x] **Step 4: Run GREEN**

Run the same filtered test. Expected: test passes.

### Task 2: Library Page Uses Playlist Endpoint

**Files:**
- Modify: `tests/NextGenEmby.Core.Tests/Design/LibraryPageSourceTests.cs`
- Modify: `src/NextGenEmby.App/Navigation/LibraryNavigationRequest.cs`
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml.cs`

- [x] **Step 1: Write source regression tests**

Add a test proving the navigation request carries the opened container item type and `LibraryPage` branches playlist child loads to the new endpoint:

```csharp
[Fact]
public void Library_Page_Loads_Playlist_Children_From_Playlist_Items_Endpoint()
{
    var source = ReadAppSource("Views", "LibraryPage.xaml.cs");
    var requestSource = ReadAppSource("Navigation", "LibraryNavigationRequest.cs");

    Assert.Contains("ContainerItemType", requestSource);
    Assert.Contains("item.Type", source);
    Assert.Contains("IsPlaylistRequest(request)", source);
    Assert.Contains("client.GetPlaylistItemsAsync(session, request.ParentId, 100)", source);
}
```

- [x] **Step 2: Run RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "Library_Page_Loads_Playlist_Children_From_Playlist_Items_Endpoint" -v minimal
```

Expected: fails because request metadata and playlist endpoint routing do not exist.

- [x] **Step 3: Add request metadata**

Add a `containerItemType` constructor parameter with default `""`, store it as `ContainerItemType`, and preserve it through `WithDevelopmentFixture`.

- [x] **Step 4: Route playlist child loads**

When `ActivateLibraryItem` opens a browse container, pass `item.Type` into the child `LibraryNavigationRequest`. In `LoadLibraryAsync`, before the generic `GetItemsAsync` branch, call `GetPlaylistItemsAsync` when `IsPlaylistRequest(request)` is true.

- [x] **Step 5: Run GREEN**

Run the filtered source test and the API test.

### Task 3: Verification And QA Evidence

**Files:**
- Modify: `docs/qa/emby-tv-client-operation-matrix.md`
- Modify: `docs/qa/emby-tv-client-keyboard-checklist.md`
- Modify: `docs/superpowers/plans/2026-07-07-playlist-items-endpoint.md`
- Modify: `src/NextGenEmby.App/Package.appxmanifest`

- [x] **Step 1: Bump package version**

Change manifest version from `0.1.0.185` to `0.1.0.186`.

- [x] **Step 2: Run full Core tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [x] **Step 3: Build Debug x64**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /p:UseSharedCompilation=false
```

Expected: `0 warnings`, `0 errors`.

- [x] **Step 4: Keyboard verification**

Install `0.1.0.186`, launch `playlists-fixture`, use only keyboard/controller-mapped input:

```text
Return -> open Weekend Queue
Right -> move item focus
Right -> move item focus again
Escape -> return to Playlists root
```

Expected: fixture behavior stays unchanged while real-server playlist child pages are now protected by the standard endpoint path.

- [x] **Step 5: Update QA docs and commit**

Record test/build/keyboard evidence and commit:

```powershell
git add docs\qa\emby-tv-client-keyboard-checklist.md docs\qa\emby-tv-client-operation-matrix.md docs\superpowers\plans\2026-07-07-playlist-items-endpoint.md src\NextGenEmby.App\Navigation\LibraryNavigationRequest.cs src\NextGenEmby.App\Package.appxmanifest src\NextGenEmby.App\Views\LibraryPage.xaml.cs src\NextGenEmby.Core\Emby\EmbyApiClient.cs tests\NextGenEmby.Core.Tests\Design\LibraryPageSourceTests.cs tests\NextGenEmby.Core.Tests\Emby\EmbyLibraryTests.cs
git commit -m "feat: load playlist items through playlist endpoint"
```
