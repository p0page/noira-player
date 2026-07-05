# Xbox Fluent UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把现有 Xbox Emby 客户端改造成可日用的 Xbox Fluent 媒体中心雏形，补齐 Home、Library、详情页、播放 OSD、切版本/音轨/字幕和 seek 安全交互。

**Architecture:** 继续保留 Core/App 分层：Emby 协议、查询模型和 seek 状态机放在 `NextGenEmby.Core`，用 xUnit 覆盖；UWP XAML App 只负责 Xbox 电视端 Shell、焦点、页面状态和播放器 UI。播放核心继续沿用当前 `PlaybackOrchestrator` 与 native backend，改造重点是把现有调试面板能力迁移到全屏 OSD 和 More 抽屉。

**Tech Stack:** UWP XAML, WinUI 2.8.7, C#, .NET Standard 2.0 Core library, .NET 9 xUnit tests, Visual Studio 2022 MSBuild, Xbox gamepad/remote input, Emby HTTP API.

---

## Scope Check

本计划实现 `docs/superpowers/specs/2026-07-05-xbox-fluent-ui-redesign-design.md` 的 UI/交互基础版本。它是一个纵向可运行切片：真实 Emby 内容进入 Home/Library/Detail，播放页变成真全屏 OSD，并保留当前已经工作的 direct/native 播放链路。

本计划不实现新的 HDR/native playback core，不做转码质量选择，不做在线字幕搜索，不做 Xbox 真机认证提交。

## File Structure

新增或修改的文件：

- `src/NextGenEmby.Core/Emby/EmbyLibraryView.cs`：Emby 用户视图库模型。
- `src/NextGenEmby.Core/Emby/EmbyItemsQuery.cs`：统一描述 Library、搜索、季/集查询参数。
- `src/NextGenEmby.Core/Emby/EmbyUserData.cs`：播放进度、已观看、续播位置模型。
- `src/NextGenEmby.Core/Emby/EmbyMediaItem.cs`：扩展详情页和剧集字段。
- `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`：新增 Views、Items、Item、Search、Children 查询。
- `src/NextGenEmby.Core/Playback/SeekPreviewSession.cs`：可测试的 seek 预览提交状态机。
- `tests/NextGenEmby.Core.Tests/Emby/EmbyLibraryTests.cs`：扩展 Emby library/detail/search URL 与解析测试。
- `tests/NextGenEmby.Core.Tests/Playback/SeekPreviewSessionTests.cs`：seek 预览提交/取消/死区测试。
- `src/NextGenEmby.App/App.xaml`：更新 Fluent TV 色彩、焦点和控件基础样式。
- `src/NextGenEmby.App/MainPage.xaml` 和 `.cs`：替换常驻左侧 `NavigationView` 为轻量 Xbox TV Shell。
- `src/NextGenEmby.App/Navigation/LibraryNavigationRequest.cs`：Library 页面导航参数。
- `src/NextGenEmby.App/Navigation/MediaDetailsNavigationRequest.cs`：详情页导航参数。
- `src/NextGenEmby.App/Views/HomePage.xaml` 和 `.cs`：Home Hub、继续观看、最近添加、Movies/TV 入口。
- `src/NextGenEmby.App/Views/LibraryPage.xaml` 和 `.cs`：电影/剧集网格、排序、筛选、分页加载。
- `src/NextGenEmby.App/Views/MediaDetailsPage.xaml` 和 `.cs`：完整详情、版本/音轨/字幕摘要、季/集列表。
- `src/NextGenEmby.App/Views/PlaybackPage.xaml` 和 `.cs`：真全屏播放、OSD、More 抽屉、seek 预览提交。
- `src/NextGenEmby.App/Views/SearchPage.xaml` 和 `.cs`：可见搜索入口。
- `src/NextGenEmby.App/Views/SettingsPage.xaml` 和 `.cs`：服务器、播放偏好、诊断入口基础页。
- `src/NextGenEmby.App/NextGenEmby.App.csproj`：注册新增 XAML 页面和 C# 文件。
- `docs/foundation-status.md`：记录本轮 UI redesign 本机验证结果。

---

### Task 1: Extend Emby Library and Detail API

**Files:**
- Create: `src/NextGenEmby.Core/Emby/EmbyLibraryView.cs`
- Create: `src/NextGenEmby.Core/Emby/EmbyItemsQuery.cs`
- Create: `src/NextGenEmby.Core/Emby/EmbyUserData.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyMediaItem.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyLibraryTests.cs`

- [ ] **Step 1: Write failing API tests**

Append these tests to `tests/NextGenEmby.Core.Tests/Emby/EmbyLibraryTests.cs`:

```csharp
[Fact]
public async Task GetUserViewsAsync_Parses_Movie_And_Tv_Libraries()
{
    var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
        HttpStatusCode.OK,
        """
        {
          "Items": [
            { "Id": "movies", "Name": "Movies", "CollectionType": "movies" },
            { "Id": "tv", "Name": "TV Shows", "CollectionType": "tvshows" }
          ],
          "TotalRecordCount": 2
        }
        """));
    using var http = new HttpClient(handler);
    var client = CreateClient(http);

    var views = await client.GetUserViewsAsync(Session());

    Assert.Collection(
        views,
        view =>
        {
            Assert.Equal("movies", view.Id);
            Assert.Equal("Movies", view.Name);
            Assert.Equal("movies", view.CollectionType);
        },
        view =>
        {
            Assert.Equal("tv", view.Id);
            Assert.Equal("TV Shows", view.Name);
            Assert.Equal("tvshows", view.CollectionType);
        });
    Assert.Equal("/Users/user-1/Views", handler.LastRequest!.RequestUri!.AbsolutePath);
}

[Fact]
public async Task GetItemsAsync_Builds_Library_Query_And_Parses_UserData()
{
    var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
        HttpStatusCode.OK,
        """
        {
          "Items": [
            {
              "Id": "movie-1",
              "Name": "Movie One",
              "Type": "Movie",
              "ProductionYear": 2024,
              "RunTimeTicks": 72000000000,
              "UserData": {
                "Played": false,
                "PlaybackPositionTicks": 1230000000,
                "PlayedPercentage": 17.5
              }
            }
          ],
          "TotalRecordCount": 1
        }
        """));
    using var http = new HttpClient(handler);
    var client = CreateClient(http);

    var items = await client.GetItemsAsync(Session(), new EmbyItemsQuery
    {
        ParentId = "movies",
        IncludeItemTypes = "Movie",
        SortBy = "SortName",
        SortOrder = "Ascending",
        StartIndex = 20,
        Limit = 40,
        Recursive = true,
        Filters = "IsNotFolder"
    });

    var item = Assert.Single(items);
    Assert.Equal("movie-1", item.Id);
    Assert.False(item.UserData.Played);
    Assert.Equal(1230000000, item.UserData.PlaybackPositionTicks);
    Assert.Equal(17.5, item.UserData.PlayedPercentage);
    Assert.Equal("/Users/user-1/Items", handler.LastRequest!.RequestUri!.AbsolutePath);
    Assert.Contains("ParentId=movies", handler.LastRequest.RequestUri.Query);
    Assert.Contains("IncludeItemTypes=Movie", handler.LastRequest.RequestUri.Query);
    Assert.Contains("StartIndex=20", handler.LastRequest.RequestUri.Query);
    Assert.Contains("Limit=40", handler.LastRequest.RequestUri.Query);
}

[Fact]
public async Task GetItemAsync_And_GetChildrenAsync_Parse_Detail_And_Episodes()
{
    var calls = 0;
    var handler = new TestHttpMessageHandler(request =>
    {
        calls++;
        if (request.RequestUri!.AbsolutePath == "/Users/user-1/Items/series-1")
        {
            return TestHttpMessageHandler.Json(
                HttpStatusCode.OK,
                """
                {
                  "Id": "series-1",
                  "Name": "Series One",
                  "Type": "Series",
                  "Overview": "A show.",
                  "ChildCount": 1
                }
                """);
        }

        return TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "episode-1",
                  "Name": "Pilot",
                  "Type": "Episode",
                  "IndexNumber": 1,
                  "ParentIndexNumber": 1
                }
              ],
              "TotalRecordCount": 1
            }
            """);
    });
    using var http = new HttpClient(handler);
    var client = CreateClient(http);

    var detail = await client.GetItemAsync(Session(), "series-1");
    var episodes = await client.GetChildrenAsync(Session(), "season-1", "Episode");

    Assert.Equal("Series One", detail.Name);
    Assert.Equal(1, detail.ChildCount);
    var episode = Assert.Single(episodes);
    Assert.Equal(1, episode.ParentIndexNumber);
    Assert.Equal(1, episode.IndexNumber);
    Assert.Equal(2, calls);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "GetUserViewsAsync|GetItemsAsync|GetItemAsync" -v minimal
```

Expected: build fails because `EmbyLibraryView`, `EmbyItemsQuery`, `EmbyUserData`, and new `EmbyApiClient` methods do not exist.

- [ ] **Step 3: Add Emby models**

Create `src/NextGenEmby.Core/Emby/EmbyLibraryView.cs`:

```csharp
namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyLibraryView
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string CollectionType { get; set; } = "";

        public bool IsMovieLibrary => CollectionType == "movies";
        public bool IsTvLibrary => CollectionType == "tvshows";
    }
}
```

Create `src/NextGenEmby.Core/Emby/EmbyItemsQuery.cs`:

```csharp
namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyItemsQuery
    {
        public string ParentId { get; set; } = "";
        public string IncludeItemTypes { get; set; } = "";
        public string SearchTerm { get; set; } = "";
        public string SortBy { get; set; } = "SortName";
        public string SortOrder { get; set; } = "Ascending";
        public string Filters { get; set; } = "";
        public int StartIndex { get; set; }
        public int Limit { get; set; } = 50;
        public bool Recursive { get; set; } = true;
    }
}
```

Create `src/NextGenEmby.Core/Emby/EmbyUserData.cs`:

```csharp
namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyUserData
    {
        public bool Played { get; set; }
        public long PlaybackPositionTicks { get; set; }
        public double? PlayedPercentage { get; set; }
    }
}
```

Modify `src/NextGenEmby.Core/Emby/EmbyMediaItem.cs` so the class contains these additional properties:

```csharp
public string ParentId { get; set; } = "";
public string SeriesId { get; set; } = "";
public int? IndexNumber { get; set; }
public int? ParentIndexNumber { get; set; }
public int? ChildCount { get; set; }
public EmbyUserData UserData { get; set; } = new EmbyUserData();
```

- [ ] **Step 4: Add EmbyApiClient query methods**

Modify `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`:

Add public methods after `GetLatestItemsAsync`:

```csharp
public async Task<IReadOnlyList<EmbyLibraryView>> GetUserViewsAsync(EmbySession session)
{
    using var request = new HttpRequestMessage(
        HttpMethod.Get,
        $"Users/{EscapeUriComponent(session.UserId)}/Views");
    EmbyAuthorization.Apply(request, _options, session);

    using var response = await _http.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var dto = JsonSerializer.Deserialize<ItemListDto<ViewDto>>(body, _jsonOptions) ?? new ItemListDto<ViewDto>();
    return (dto.Items ?? new List<ViewDto>()).Select(view => new EmbyLibraryView
    {
        Id = view.Id ?? "",
        Name = view.Name ?? "",
        CollectionType = view.CollectionType ?? ""
    }).ToList();
}

public Task<IReadOnlyList<EmbyMediaItem>> GetChildrenAsync(
    EmbySession session,
    string parentId,
    string includeItemTypes)
{
    return GetItemsAsync(session, new EmbyItemsQuery
    {
        ParentId = parentId,
        IncludeItemTypes = includeItemTypes,
        Recursive = false,
        SortBy = "SortName",
        SortOrder = "Ascending",
        Limit = 100
    });
}

public async Task<IReadOnlyList<EmbyMediaItem>> GetItemsAsync(
    EmbySession session,
    EmbyItemsQuery query)
{
    if (query == null)
    {
        throw new ArgumentNullException(nameof(query));
    }

    var path = BuildItemsPath(session, query);
    using var request = new HttpRequestMessage(HttpMethod.Get, path);
    EmbyAuthorization.Apply(request, _options, session);

    using var response = await _http.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var dto = JsonSerializer.Deserialize<ItemListDto<ItemDto>>(body, _jsonOptions) ?? new ItemListDto<ItemDto>();
    return (dto.Items ?? new List<ItemDto>()).Select(MapItem).ToList();
}

public Task<IReadOnlyList<EmbyMediaItem>> SearchItemsAsync(
    EmbySession session,
    string searchTerm,
    string includeItemTypes)
{
    return GetItemsAsync(session, new EmbyItemsQuery
    {
        IncludeItemTypes = includeItemTypes,
        SearchTerm = searchTerm,
        SortBy = "SortName",
        SortOrder = "Ascending",
        Limit = 50
    });
}

public async Task<EmbyMediaItem> GetItemAsync(EmbySession session, string itemId)
{
    using var request = new HttpRequestMessage(
        HttpMethod.Get,
        $"Users/{EscapeUriComponent(session.UserId)}/Items/{EscapeUriComponent(itemId)}?Fields=Overview,ProductionYear,RunTimeTicks,ChildCount,MediaSources,UserData");
    EmbyAuthorization.Apply(request, _options, session);

    using var response = await _http.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var dto = JsonSerializer.Deserialize<ItemDto>(body, _jsonOptions)
        ?? throw new InvalidOperationException("Emby item response was empty.");
    return MapItem(dto);
}
```

Add helper before `MapItem`:

```csharp
private static string BuildItemsPath(EmbySession session, EmbyItemsQuery query)
{
    var parameters = new List<string>
    {
        "Fields=Overview,ProductionYear,RunTimeTicks,PrimaryImageAspectRatio,ChildCount,UserData",
        "StartIndex=" + Math.Max(0, query.StartIndex),
        "Limit=" + Math.Max(1, query.Limit),
        "Recursive=" + (query.Recursive ? "true" : "false")
    };

    if (!string.IsNullOrWhiteSpace(query.ParentId))
    {
        parameters.Add("ParentId=" + EscapeUriComponent(query.ParentId));
    }

    if (!string.IsNullOrWhiteSpace(query.IncludeItemTypes))
    {
        parameters.Add("IncludeItemTypes=" + EscapeUriComponent(query.IncludeItemTypes));
    }

    if (!string.IsNullOrWhiteSpace(query.SearchTerm))
    {
        parameters.Add("SearchTerm=" + EscapeUriComponent(query.SearchTerm));
    }

    if (!string.IsNullOrWhiteSpace(query.SortBy))
    {
        parameters.Add("SortBy=" + EscapeUriComponent(query.SortBy));
    }

    if (!string.IsNullOrWhiteSpace(query.SortOrder))
    {
        parameters.Add("SortOrder=" + EscapeUriComponent(query.SortOrder));
    }

    if (!string.IsNullOrWhiteSpace(query.Filters))
    {
        parameters.Add("Filters=" + EscapeUriComponent(query.Filters));
    }

    return $"Users/{EscapeUriComponent(session.UserId)}/Items?" + string.Join("&", parameters);
}
```

Extend `MapItem` object initialization:

```csharp
ParentId = item.ParentId ?? "",
SeriesId = item.SeriesId ?? "",
IndexNumber = item.IndexNumber,
ParentIndexNumber = item.ParentIndexNumber,
ChildCount = item.ChildCount,
UserData = item.UserData == null
    ? new EmbyUserData()
    : new EmbyUserData
    {
        Played = item.UserData.Played,
        PlaybackPositionTicks = item.UserData.PlaybackPositionTicks,
        PlayedPercentage = item.UserData.PlayedPercentage
    }
```

Add DTOs near existing DTO classes:

```csharp
private sealed class ItemListDto<T>
{
    public List<T> Items { get; set; } = new List<T>();
    public int TotalRecordCount { get; set; }
}

private sealed class ViewDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CollectionType { get; set; } = "";
}

private sealed class UserDataDto
{
    public bool Played { get; set; }
    public long PlaybackPositionTicks { get; set; }
    public double? PlayedPercentage { get; set; }
}
```

Extend `ItemDto`:

```csharp
public string ParentId { get; set; } = "";
public string SeriesId { get; set; } = "";
public int? IndexNumber { get; set; }
public int? ParentIndexNumber { get; set; }
public int? ChildCount { get; set; }
public UserDataDto UserData { get; set; } = new UserDataDto();
```

- [ ] **Step 5: Run library API tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "EmbyLibraryTests" -v minimal
```

Expected: all `EmbyLibraryTests` pass.

- [ ] **Step 6: Run all Core tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

Run:

```powershell
git add src\NextGenEmby.Core tests\NextGenEmby.Core.Tests
git commit -m "feat: add Emby library and detail queries"
```

Expected: commit succeeds.

---

### Task 2: Add Seek Preview State Machine

**Files:**
- Create: `src/NextGenEmby.Core/Playback/SeekPreviewSession.cs`
- Create: `tests/NextGenEmby.Core.Tests/Playback/SeekPreviewSessionTests.cs`

- [ ] **Step 1: Write failing seek preview tests**

Create `tests/NextGenEmby.Core.Tests/Playback/SeekPreviewSessionTests.cs`:

```csharp
using System;
using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class SeekPreviewSessionTests
{
    [Fact]
    public void Begin_Ignores_Thumbstick_Input_Below_DeadZone()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);

        var changed = session.BeginFromThumbstick(1000, 0.2, TimeSpan.FromSeconds(10));

        Assert.False(changed);
        Assert.False(session.IsActive);
        Assert.Equal(0, session.TargetTicks);
    }

    [Fact]
    public void Move_Updates_Target_And_Reset_Deadline()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);

        Assert.True(session.BeginFromThumbstick(1000, 0.9, TimeSpan.FromSeconds(10)));
        Assert.Equal(1000, session.OriginalTicks);
        Assert.Equal(6000, session.TargetTicks);
        Assert.Equal(TimeSpan.FromSeconds(11.8), session.AutoCommitAt);

        session.MoveBy(TimeSpan.FromSeconds(-3), TimeSpan.FromSeconds(11));

        Assert.Equal(3000, session.TargetTicks);
        Assert.Equal(TimeSpan.FromSeconds(12.8), session.AutoCommitAt);
    }

    [Fact]
    public void AutoCommit_Cancels_Tiny_Drift_Without_Explicit_A()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(10000, TimeSpan.Zero);
        session.MoveBy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));

        var decision = session.DecideTimeout(TimeSpan.FromSeconds(2.8));

        Assert.Equal(SeekPreviewDecisionKind.Cancel, decision.Kind);
        Assert.Equal(10000, decision.PositionTicks);
        Assert.False(session.IsActive);
    }

    [Fact]
    public void Confirm_Commits_Target_Immediately()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(10000, TimeSpan.Zero);
        session.MoveBy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));

        var decision = session.Confirm();

        Assert.Equal(SeekPreviewDecisionKind.Commit, decision.Kind);
        Assert.Equal(40000, decision.PositionTicks);
        Assert.False(session.IsActive);
    }

    [Fact]
    public void Cancel_Returns_Original_Position()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(10000, TimeSpan.Zero);
        session.MoveBy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));

        var decision = session.Cancel();

        Assert.Equal(SeekPreviewDecisionKind.Cancel, decision.Kind);
        Assert.Equal(10000, decision.PositionTicks);
        Assert.False(session.IsActive);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter SeekPreviewSessionTests -v minimal
```

Expected: build fails because `SeekPreviewSession` and `SeekPreviewDecision` do not exist.

- [ ] **Step 3: Implement seek preview state machine**

Create `src/NextGenEmby.Core/Playback/SeekPreviewSession.cs`:

```csharp
using System;

namespace NextGenEmby.Core.Playback
{
    public enum SeekPreviewDecisionKind
    {
        None,
        Commit,
        Cancel
    }

    public readonly struct SeekPreviewDecision
    {
        public static readonly SeekPreviewDecision None = new SeekPreviewDecision(SeekPreviewDecisionKind.None, 0);

        public SeekPreviewDecision(SeekPreviewDecisionKind kind, long positionTicks)
        {
            Kind = kind;
            PositionTicks = positionTicks;
        }

        public SeekPreviewDecisionKind Kind { get; }

        public long PositionTicks { get; }
    }

    public sealed class SeekPreviewSession
    {
        private readonly TimeSpan _autoCommitDelay;
        private readonly TimeSpan _tinyDriftThreshold;
        private readonly double _thumbstickDeadZone;

        public SeekPreviewSession(
            TimeSpan autoCommitDelay,
            TimeSpan tinyDriftThreshold,
            double thumbstickDeadZone)
        {
            _autoCommitDelay = autoCommitDelay;
            _tinyDriftThreshold = tinyDriftThreshold;
            _thumbstickDeadZone = thumbstickDeadZone;
        }

        public bool IsActive { get; private set; }

        public long OriginalTicks { get; private set; }

        public long TargetTicks { get; private set; }

        public TimeSpan AutoCommitAt { get; private set; }

        public void Begin(long currentTicks, TimeSpan now)
        {
            IsActive = true;
            OriginalTicks = Math.Max(0, currentTicks);
            TargetTicks = OriginalTicks;
            AutoCommitAt = now + _autoCommitDelay;
        }

        public bool BeginFromThumbstick(long currentTicks, double horizontalValue, TimeSpan now)
        {
            if (Math.Abs(horizontalValue) < _thumbstickDeadZone)
            {
                return false;
            }

            Begin(currentTicks, now);
            MoveBy(TimeSpan.FromSeconds(horizontalValue > 0 ? 5 : -5), now);
            return true;
        }

        public void MoveBy(TimeSpan delta, TimeSpan now)
        {
            if (!IsActive)
            {
                Begin(0, now);
            }

            TargetTicks = Math.Max(0, TargetTicks + delta.Ticks);
            AutoCommitAt = now + _autoCommitDelay;
        }

        public SeekPreviewDecision Confirm()
        {
            if (!IsActive)
            {
                return SeekPreviewDecision.None;
            }

            var target = TargetTicks;
            Clear();
            return new SeekPreviewDecision(SeekPreviewDecisionKind.Commit, target);
        }

        public SeekPreviewDecision Cancel()
        {
            if (!IsActive)
            {
                return SeekPreviewDecision.None;
            }

            var original = OriginalTicks;
            Clear();
            return new SeekPreviewDecision(SeekPreviewDecisionKind.Cancel, original);
        }

        public SeekPreviewDecision DecideTimeout(TimeSpan now)
        {
            if (!IsActive || now < AutoCommitAt)
            {
                return SeekPreviewDecision.None;
            }

            var delta = TimeSpan.FromTicks(Math.Abs(TargetTicks - OriginalTicks));
            if (delta < _tinyDriftThreshold)
            {
                return Cancel();
            }

            return Confirm();
        }

        private void Clear()
        {
            IsActive = false;
        }
    }
}
```

- [ ] **Step 4: Run seek tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter SeekPreviewSessionTests -v minimal
```

Expected: all `SeekPreviewSessionTests` pass.

- [ ] **Step 5: Run all Core tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src\NextGenEmby.Core tests\NextGenEmby.Core.Tests
git commit -m "feat: add cancellable seek preview state"
```

Expected: commit succeeds.

---

### Task 3: Replace Desktop NavigationView With TV Shell

**Files:**
- Modify: `src/NextGenEmby.App/App.xaml`
- Modify: `src/NextGenEmby.App/MainPage.xaml`
- Modify: `src/NextGenEmby.App/MainPage.xaml.cs`
- Create: `src/NextGenEmby.App/Navigation/LibraryNavigationRequest.cs`
- Create: `src/NextGenEmby.App/Navigation/MediaDetailsNavigationRequest.cs`
- Create: `src/NextGenEmby.App/Views/LibraryPage.xaml`
- Create: `src/NextGenEmby.App/Views/LibraryPage.xaml.cs`
- Create: `src/NextGenEmby.App/Views/SearchPage.xaml`
- Create: `src/NextGenEmby.App/Views/SearchPage.xaml.cs`
- Create: `src/NextGenEmby.App/Views/SettingsPage.xaml`
- Create: `src/NextGenEmby.App/Views/SettingsPage.xaml.cs`
- Modify: `src/NextGenEmby.App/NextGenEmby.App.csproj`

- [ ] **Step 1: Update app visual resources**

Modify `src/NextGenEmby.App/App.xaml` resource colors:

```xml
<Color x:Key="AppBackgroundColor">#070A0E</Color>
<Color x:Key="AppSurfaceColor">#111923</Color>
<Color x:Key="AppRaisedSurfaceColor">#172231</Color>
<Color x:Key="AppAccentColor">#00A6D6</Color>
<Color x:Key="AppActionColor">#66D17A</Color>
<Color x:Key="AppMutedTextColor">#A9B8C8</Color>
```

Add brush:

```xml
<SolidColorBrush x:Key="AppActionBrush" Color="{StaticResource AppActionColor}" />
```

Keep `SystemControlFocusVisualPrimaryBrush` and `SystemControlFocusVisualSecondaryBrush`; they are the Reveal Focus-style high contrast ring.

- [ ] **Step 2: Create navigation request records**

Create `src/NextGenEmby.App/Navigation/LibraryNavigationRequest.cs`:

```csharp
namespace NextGenEmby.App.Navigation
{
    internal sealed class LibraryNavigationRequest
    {
        public LibraryNavigationRequest(string title, string collectionType, string includeItemTypes)
        {
            Title = title ?? "";
            CollectionType = collectionType ?? "";
            IncludeItemTypes = includeItemTypes ?? "";
        }

        public string Title { get; }
        public string CollectionType { get; }
        public string IncludeItemTypes { get; }

        public bool IsMovies => CollectionType == "movies";
        public bool IsTv => CollectionType == "tvshows";
    }
}
```

Create `src/NextGenEmby.App/Navigation/MediaDetailsNavigationRequest.cs`:

```csharp
namespace NextGenEmby.App.Navigation
{
    internal sealed class MediaDetailsNavigationRequest
    {
        public MediaDetailsNavigationRequest(string itemId, string itemName = "")
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
        }

        public string ItemId { get; }
        public string ItemName { get; }
    }
}
```

- [ ] **Step 3: Replace MainPage XAML**

Replace `src/NextGenEmby.App/MainPage.xaml` content with:

```xml
<Page
    x:Class="NextGenEmby.App.MainPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="{StaticResource AppBackgroundBrush}">
    <Grid Background="{StaticResource AppBackgroundBrush}">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <Grid
            Grid.Row="0"
            Margin="56,34,56,18"
            ColumnSpacing="24">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>

            <TextBlock
                Grid.Column="0"
                Text="Next Gen Xbox Emby"
                FontSize="24"
                FontWeight="SemiBold"
                VerticalAlignment="Center" />

            <StackPanel
                Grid.Column="1"
                Orientation="Horizontal"
                HorizontalAlignment="Center"
                Spacing="12">
                <Button x:Name="HomeButton" Content="Home" MinWidth="130" Click="Home_OnClick" />
                <Button x:Name="MoviesButton" Content="Movies" MinWidth="130" Click="Movies_OnClick" />
                <Button x:Name="TvButton" Content="TV" MinWidth="130" Click="Tv_OnClick" />
                <Button x:Name="SearchButton" Content="Search" MinWidth="130" Click="Search_OnClick" />
            </StackPanel>

            <Button
                x:Name="SettingsButton"
                Grid.Column="2"
                Width="64"
                Height="64"
                MinWidth="64"
                Padding="0"
                Click="Settings_OnClick"
                ToolTipService.ToolTip="Settings">
                <SymbolIcon Symbol="Setting" />
            </Button>
        </Grid>

        <Frame
            x:Name="ContentFrame"
            Grid.Row="1" />
    </Grid>
</Page>
```

- [ ] **Step 4: Replace MainPage code-behind**

Replace `src/NextGenEmby.App/MainPage.xaml.cs` with:

```csharp
using System;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Storage;
using NextGenEmby.App.Views;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App
{
    public sealed partial class MainPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();

        public MainPage()
        {
            InitializeComponent();
            NavigateLogin();
            Loaded += MainPage_OnLoaded;
            KeyDown += MainPage_OnKeyDown;
        }

        private async void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainPage_OnLoaded;
            try
            {
                var session = await _sessionStore.LoadAsync();
                if (session != null)
                {
                    NavigateHome();
                }
            }
            catch
            {
            }
        }

        private void MainPage_OnKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.GamepadY)
            {
                NavigateTo(typeof(SearchPage));
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.GamepadB && ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
                e.Handled = true;
            }
        }

        private void Home_OnClick(object sender, RoutedEventArgs e) => NavigateHome();

        private void Movies_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateTo(typeof(LibraryPage), new LibraryNavigationRequest("Movies", "movies", "Movie"));
        }

        private void Tv_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateTo(typeof(LibraryPage), new LibraryNavigationRequest("TV", "tvshows", "Series"));
        }

        private void Search_OnClick(object sender, RoutedEventArgs e) => NavigateTo(typeof(SearchPage));

        private void Settings_OnClick(object sender, RoutedEventArgs e) => NavigateTo(typeof(SettingsPage));

        public void NavigateHome() => NavigateTo(typeof(HomePage));

        private void NavigateLogin() => NavigateTo(typeof(LoginPage));

        private void NavigateTo(Type pageType, object parameter = null)
        {
            if (parameter == null && ContentFrame.CurrentSourcePageType == pageType)
            {
                return;
            }

            ContentFrame.Navigate(pageType, parameter);
        }
    }
}
```

- [ ] **Step 5: Add Search and Settings stubs**

Create `src/NextGenEmby.App/Views/SearchPage.xaml`:

```xml
<Page
    x:Class="NextGenEmby.App.Views.SearchPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="{StaticResource AppBackgroundBrush}">
    <Grid Margin="72,48">
        <StackPanel Spacing="20">
            <TextBlock Text="Search" FontSize="46" FontWeight="SemiBold" />
            <TextBox x:Name="SearchBox" Header="Search movies and TV" PlaceholderText="Title" />
            <Button Content="Search" Click="Search_OnClick" />
            <TextBlock x:Name="StatusBlock" Foreground="{StaticResource AppMutedTextBrush}" FontSize="20" />
            <StackPanel x:Name="ResultsPanel" Spacing="12" />
        </StackPanel>
    </Grid>
</Page>
```

Create `src/NextGenEmby.App/Views/SearchPage.xaml.cs`:

```csharp
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class SearchPage : Page
    {
        public SearchPage()
        {
            InitializeComponent();
        }

        private void Search_OnClick(object sender, RoutedEventArgs e)
        {
            StatusBlock.Text = "Search implementation follows after Library loading is in place.";
        }
    }
}
```

Create `src/NextGenEmby.App/Views/SettingsPage.xaml`:

```xml
<Page
    x:Class="NextGenEmby.App.Views.SettingsPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="{StaticResource AppBackgroundBrush}">
    <Grid Margin="72,48">
        <StackPanel Spacing="22" MaxWidth="900">
            <TextBlock Text="Settings" FontSize="46" FontWeight="SemiBold" />
            <TextBlock x:Name="AccountBlock" FontSize="22" Foreground="{StaticResource AppMutedTextBrush}" />
            <CheckBox x:Name="EnableThumbstickSeekBox" Content="Enable thumbstick seek preview" IsChecked="True" FontSize="22" />
            <TextBlock Text="Seek preview auto-commits after 1.8 seconds. Press B to cancel before it commits." FontSize="18" Foreground="{StaticResource AppMutedTextBrush}" TextWrapping="Wrap" />
        </StackPanel>
    </Grid>
</Page>
```

Create `src/NextGenEmby.App/Views/SettingsPage.xaml.cs`:

```csharp
using NextGenEmby.App.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_OnLoaded;
        }

        private async void SettingsPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SettingsPage_OnLoaded;
            var session = await _sessionStore.LoadAsync();
            AccountBlock.Text = session == null
                ? "Not signed in."
                : session.UserName + " / " + session.ServerUrl;
        }
    }
}
```

- [ ] **Step 6: Add LibraryPage stub**

Create `src/NextGenEmby.App/Views/LibraryPage.xaml`:

```xml
<Page
    x:Class="NextGenEmby.App.Views.LibraryPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="{StaticResource AppBackgroundBrush}">
    <Grid Margin="72,48">
        <StackPanel Spacing="18">
            <TextBlock x:Name="TitleBlock" Text="Library" FontSize="46" FontWeight="SemiBold" />
            <TextBlock x:Name="StatusBlock" Text="Library loading is added in Task 5." FontSize="20" Foreground="{StaticResource AppMutedTextBrush}" />
            <GridView x:Name="ItemsGrid" SelectionMode="None" IsItemClickEnabled="True" />
        </StackPanel>
    </Grid>
</Page>
```

Create `src/NextGenEmby.App/Views/LibraryPage.xaml.cs`:

```csharp
using NextGenEmby.App.Navigation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var request = e.Parameter as LibraryNavigationRequest;
            TitleBlock.Text = request == null ? "Library" : request.Title;
        }
    }
}
```

- [ ] **Step 7: Register files in UWP project**

Modify `src/NextGenEmby.App/NextGenEmby.App.csproj` compile item group:

```xml
<Compile Include="Navigation\LibraryNavigationRequest.cs" />
<Compile Include="Navigation\MediaDetailsNavigationRequest.cs" />
<Compile Include="Views\LibraryPage.xaml.cs">
  <DependentUpon>LibraryPage.xaml</DependentUpon>
</Compile>
<Compile Include="Views\SearchPage.xaml.cs">
  <DependentUpon>SearchPage.xaml</DependentUpon>
</Compile>
<Compile Include="Views\SettingsPage.xaml.cs">
  <DependentUpon>SettingsPage.xaml</DependentUpon>
</Compile>
```

Modify page item group:

```xml
<Page Include="Views\LibraryPage.xaml">
  <Generator>MSBuild:Compile</Generator>
  <SubType>Designer</SubType>
</Page>
<Page Include="Views\SearchPage.xaml">
  <Generator>MSBuild:Compile</Generator>
  <SubType>Designer</SubType>
</Page>
<Page Include="Views\SettingsPage.xaml">
  <Generator>MSBuild:Compile</Generator>
  <SubType>Designer</SubType>
</Page>
```

- [ ] **Step 8: Build**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 9: Commit**

Run:

```powershell
git add src\NextGenEmby.App
git commit -m "feat: add Xbox TV shell"
```

Expected: commit succeeds.

---

### Task 4: Build Home Hub and Library Entry Points

**Files:**
- Modify: `src/NextGenEmby.App/Views/HomePage.xaml`
- Modify: `src/NextGenEmby.App/Views/HomePage.xaml.cs`
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml`
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml.cs`
- Modify: `src/NextGenEmby.App/NextGenEmby.App.csproj`

- [ ] **Step 1: Replace HomePage XAML**

Replace `src/NextGenEmby.App/Views/HomePage.xaml` with:

```xml
<Page
    x:Class="NextGenEmby.App.Views.HomePage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="{StaticResource AppBackgroundBrush}">
    <ScrollViewer VerticalScrollBarVisibility="Hidden">
        <StackPanel Margin="72,42,72,64" Spacing="30">
            <Border MinHeight="260" Background="{StaticResource AppSurfaceBrush}" CornerRadius="8" Padding="28">
                <Grid ColumnSpacing="24">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="220" />
                    </Grid.ColumnDefinitions>
                    <StackPanel Spacing="12" VerticalAlignment="Center">
                        <TextBlock Text="Continue watching" FontSize="18" Foreground="{StaticResource AppMutedTextBrush}" />
                        <TextBlock x:Name="HeroTitleBlock" Text="Loading..." FontSize="42" FontWeight="SemiBold" TextWrapping="WrapWholeWords" />
                        <TextBlock x:Name="HeroMetaBlock" FontSize="20" Foreground="{StaticResource AppMutedTextBrush}" />
                        <StackPanel Orientation="Horizontal" Spacing="12">
                            <Button x:Name="HeroPlayButton" Content="Play" Click="HeroPlay_OnClick" Background="{StaticResource AppActionBrush}" />
                            <Button x:Name="HeroDetailsButton" Content="Details" Click="HeroDetails_OnClick" />
                        </StackPanel>
                    </StackPanel>
                    <Border Grid.Column="1" Background="{StaticResource AppRaisedSurfaceBrush}" CornerRadius="8">
                        <Image x:Name="HeroPosterImage" Stretch="UniformToFill" />
                    </Border>
                </Grid>
            </Border>

            <Grid ColumnSpacing="18">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="280" />
                    <ColumnDefinition Width="280" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <Button x:Name="MoviesLibraryButton" Grid.Column="0" Height="150" Click="MoviesLibrary_OnClick">
                    <StackPanel VerticalAlignment="Center">
                        <TextBlock Text="Movies" FontSize="30" FontWeight="SemiBold" />
                        <TextBlock Text="Open movie library" FontSize="17" Foreground="{StaticResource AppMutedTextBrush}" />
                    </StackPanel>
                </Button>
                <Button x:Name="TvLibraryButton" Grid.Column="1" Height="150" Click="TvLibrary_OnClick">
                    <StackPanel VerticalAlignment="Center">
                        <TextBlock Text="TV" FontSize="30" FontWeight="SemiBold" />
                        <TextBlock Text="Open series library" FontSize="17" Foreground="{StaticResource AppMutedTextBrush}" />
                    </StackPanel>
                </Button>
                <Button x:Name="RefreshButton" Grid.Column="3" Width="64" Height="64" MinWidth="64" Padding="0" Click="Refresh_OnClick">
                    <SymbolIcon Symbol="Refresh" />
                </Button>
            </Grid>

            <TextBlock x:Name="StatusBlock" FontSize="20" Foreground="{StaticResource AppMutedTextBrush}" />
            <StackPanel x:Name="RowsPanel" Spacing="26" />
        </StackPanel>
    </ScrollViewer>
</Page>
```

- [ ] **Step 2: Replace HomePage code-behind**

Replace `src/NextGenEmby.App/Views/HomePage.xaml.cs` with a version that loads real rows:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace NextGenEmby.App.Views
{
    public sealed partial class HomePage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private EmbySession? _session;
        private EmbyApiClient? _client;
        private HttpClient? _http;
        private EmbyMediaItem? _heroItem;

        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_OnLoaded;
            Unloaded += HomePage_OnUnloaded;
        }

        private async void HomePage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HomePage_OnLoaded;
            await LoadHomeAsync();
        }

        private void HomePage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _http?.Dispose();
        }

        private async void Refresh_OnClick(object sender, RoutedEventArgs e) => await LoadHomeAsync();

        private void MoviesLibrary_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LibraryPage), new LibraryNavigationRequest("Movies", "movies", "Movie"));
        }

        private void TvLibrary_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LibraryPage), new LibraryNavigationRequest("TV", "tvshows", "Series"));
        }

        private void HeroPlay_OnClick(object sender, RoutedEventArgs e)
        {
            if (_heroItem != null)
            {
                Frame.Navigate(typeof(PlaybackPage), new PlaybackLaunchRequest(_heroItem.Id, _heroItem.Name, _heroItem.UserData.PlaybackPositionTicks));
            }
        }

        private void HeroDetails_OnClick(object sender, RoutedEventArgs e)
        {
            if (_heroItem != null)
            {
                Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(_heroItem.Id, _heroItem.Name));
            }
        }

        private async Task LoadHomeAsync()
        {
            StatusBlock.Text = "Loading...";
            RowsPanel.Children.Clear();
            RefreshButton.IsEnabled = false;

            try
            {
                _session = await _sessionStore.LoadAsync();
                if (_session == null)
                {
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                _http?.Dispose();
                _http = new HttpClient();
                _client = EmbyClientFactory.Create(_http, _session);

                var continueItems = await _client.GetItemsAsync(_session, new EmbyItemsQuery
                {
                    IncludeItemTypes = "Movie,Episode",
                    Filters = "IsResumable",
                    SortBy = "DatePlayed",
                    SortOrder = "Descending",
                    Limit = 20
                });
                var latestItems = await _client.GetLatestItemsAsync(_session);

                RenderHero(continueItems.Count > 0 ? continueItems[0] : latestItems.Count > 0 ? latestItems[0] : null);
                AddRow("Continue Watching", continueItems);
                AddRow("Recently Added", latestItems);
                StatusBlock.Text = "";
            }
            catch (Exception ex)
            {
                StatusBlock.Text = "Unable to load home: " + ex.Message;
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private void RenderHero(EmbyMediaItem? item)
        {
            _heroItem = item;
            HeroPlayButton.IsEnabled = item != null;
            HeroDetailsButton.IsEnabled = item != null;
            HeroTitleBlock.Text = item == null ? "No resumable item" : item.Name;
            HeroMetaBlock.Text = item == null ? "" : CreateSubtitle(item);
            HeroPosterImage.Source = null;
            if (item != null && _client != null && _session != null && !string.IsNullOrWhiteSpace(item.PrimaryImageTag))
            {
                HeroPosterImage.Source = new BitmapImage(new Uri(_client.GetImageUrl(_session, item.Id, "Primary", 520)));
            }
        }

        private void AddRow(string title, IReadOnlyList<EmbyMediaItem> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            var section = new StackPanel { Spacing = 12 };
            section.Children.Add(new TextBlock { Text = title, FontSize = 28, FontWeight = Windows.UI.Text.FontWeights.SemiBold });
            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollMode = ScrollMode.Disabled
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            foreach (var item in items)
            {
                row.Children.Add(CreateItemButton(item));
            }

            scroller.Content = row;
            section.Children.Add(scroller);
            RowsPanel.Children.Add(section);
        }

        private Button CreateItemButton(EmbyMediaItem item)
        {
            var button = new Button
            {
                Width = 210,
                Height = 310,
                MinWidth = 210,
                Padding = new Thickness(0),
                Tag = item,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                UseSystemFocusVisuals = true
            };
            button.Click += ItemButton_OnClick;

            var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(255, 23, 34, 49)) };
            if (_client != null && _session != null && !string.IsNullOrWhiteSpace(item.PrimaryImageTag))
            {
                root.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(_client.GetImageUrl(_session, item.Id, "Primary", 420))),
                    Stretch = Stretch.UniformToFill
                };
            }

            root.Children.Add(new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
                Padding = new Thickness(14),
                Child = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 18,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 2
                }
            });
            button.Content = root;
            return button;
        }

        private void ItemButton_OnClick(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is EmbyMediaItem item)
            {
                Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(item.Id, item.Name));
            }
        }

        private static string CreateSubtitle(EmbyMediaItem item)
        {
            var label = string.IsNullOrWhiteSpace(item.Type) ? "Item" : item.Type;
            return item.ProductionYear.HasValue ? label + " / " + item.ProductionYear.Value : label;
        }
    }
}
```

- [ ] **Step 3: Build**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: build succeeds with Home and Shell changes.

- [ ] **Step 4: Commit**

Run:

```powershell
git add src\NextGenEmby.App
git commit -m "feat: add Xbox home hub"
```

Expected: commit succeeds.

---

### Task 5: Implement Movie and TV Library Grids

**Files:**
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml`
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml.cs`

- [ ] **Step 1: Replace LibraryPage XAML with filterable grid**

Replace `src/NextGenEmby.App/Views/LibraryPage.xaml` with:

```xml
<Page
    x:Class="NextGenEmby.App.Views.LibraryPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="{StaticResource AppBackgroundBrush}">
    <Grid Margin="72,42,72,64" RowSpacing="18">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <Grid Grid.Row="0" ColumnSpacing="18">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <StackPanel>
                <TextBlock x:Name="TitleBlock" Text="Library" FontSize="46" FontWeight="SemiBold" />
                <TextBlock x:Name="StatusBlock" FontSize="20" Foreground="{StaticResource AppMutedTextBrush}" />
            </StackPanel>
            <Button Grid.Column="1" Content="Refresh" Click="Refresh_OnClick" />
        </Grid>

        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="12">
            <ComboBox x:Name="SortBox" Width="220" Header="Sort" SelectionChanged="SortBox_OnSelectionChanged">
                <ComboBoxItem Content="Title" Tag="SortName" IsSelected="True" />
                <ComboBoxItem Content="Recently added" Tag="DateCreated" />
                <ComboBoxItem Content="Year" Tag="ProductionYear" />
            </ComboBox>
            <ComboBox x:Name="FilterBox" Width="220" Header="Filter" SelectionChanged="FilterBox_OnSelectionChanged">
                <ComboBoxItem Content="All" Tag="" IsSelected="True" />
                <ComboBoxItem Content="Unwatched" Tag="IsUnplayed" />
                <ComboBoxItem Content="Resumable" Tag="IsResumable" />
            </ComboBox>
        </StackPanel>

        <GridView
            x:Name="ItemsGrid"
            Grid.Row="2"
            IsItemClickEnabled="True"
            ItemClick="ItemsGrid_OnItemClick"
            SelectionMode="None"
            Padding="0,8,0,0" />
    </Grid>
</Page>
```

- [ ] **Step 2: Replace LibraryPage code-behind**

Replace `src/NextGenEmby.App/Views/LibraryPage.xaml.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class LibraryPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private LibraryNavigationRequest? _request;
        private EmbySession? _session;
        private EmbyApiClient? _client;
        private HttpClient? _http;

        public LibraryPage()
        {
            InitializeComponent();
            Unloaded += LibraryPage_OnUnloaded;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _request = e.Parameter as LibraryNavigationRequest;
            TitleBlock.Text = _request == null ? "Library" : _request.Title;
            await LoadItemsAsync();
        }

        private void LibraryPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _http?.Dispose();
        }

        private async void Refresh_OnClick(object sender, RoutedEventArgs e) => await LoadItemsAsync();

        private async void SortBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                await LoadItemsAsync();
            }
        }

        private async void FilterBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                await LoadItemsAsync();
            }
        }

        private async Task LoadItemsAsync()
        {
            if (_request == null)
            {
                StatusBlock.Text = "Library request is missing.";
                return;
            }

            StatusBlock.Text = "Loading...";
            ItemsGrid.Items.Clear();
            try
            {
                _session = await _sessionStore.LoadAsync();
                if (_session == null)
                {
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                _http?.Dispose();
                _http = new HttpClient();
                _client = EmbyClientFactory.Create(_http, _session);
                var items = await _client.GetItemsAsync(_session, new EmbyItemsQuery
                {
                    IncludeItemTypes = _request.IncludeItemTypes,
                    Recursive = true,
                    SortBy = SelectedTag(SortBox, "SortName"),
                    SortOrder = "Ascending",
                    Filters = SelectedTag(FilterBox, ""),
                    Limit = 100
                });

                RenderItems(items);
            }
            catch (Exception ex)
            {
                StatusBlock.Text = "Unable to load library: " + ex.Message;
            }
        }

        private void RenderItems(IReadOnlyList<EmbyMediaItem> items)
        {
            ItemsGrid.Items.Clear();
            StatusBlock.Text = items.Count == 0 ? "No items found." : items.Count + " items";
            foreach (var item in items)
            {
                ItemsGrid.Items.Add(CreateItemButton(item));
            }
        }

        private Button CreateItemButton(EmbyMediaItem item)
        {
            var button = new Button
            {
                Width = 210,
                Height = 320,
                MinWidth = 210,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 16, 18),
                Tag = item,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };

            var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(255, 23, 34, 49)) };
            if (_client != null && _session != null && !string.IsNullOrWhiteSpace(item.PrimaryImageTag))
            {
                root.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(_client.GetImageUrl(_session, item.Id, "Primary", 420))),
                    Stretch = Stretch.UniformToFill
                };
            }

            root.Children.Add(new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
                Padding = new Thickness(14),
                Child = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 18,
                    TextWrapping = TextWrapping.WrapWholeWords,
                    MaxLines = 2
                }
            });
            button.Content = root;
            return button;
        }

        private void ItemsGrid_OnItemClick(object sender, ItemClickEventArgs e)
        {
            if ((e.ClickedItem as Button)?.Tag is EmbyMediaItem item)
            {
                Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(item.Id, item.Name));
            }
        }

        private static string SelectedTag(ComboBox box, string fallback)
        {
            return (box.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;
        }
    }
}
```

- [ ] **Step 3: Build**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

Run:

```powershell
git add src\NextGenEmby.App
git commit -m "feat: add movie and TV library grids"
```

Expected: commit succeeds.

---

### Task 6: Expand Detail Page for Playback Decisions

**Files:**
- Modify: `src/NextGenEmby.App/Navigation/PlaybackLaunchRequest.cs`
- Modify: `src/NextGenEmby.App/Views/MediaDetailsPage.xaml`
- Modify: `src/NextGenEmby.App/Views/MediaDetailsPage.xaml.cs`

- [ ] **Step 1: Extend playback launch request**

Modify `src/NextGenEmby.App/Navigation/PlaybackLaunchRequest.cs` constructor and properties:

```csharp
public PlaybackLaunchRequest(
    string itemId,
    string itemName = "",
    long startPositionTicks = 0,
    string mediaSourceId = "")
{
    ItemId = itemId ?? "";
    ItemName = itemName ?? "";
    StartPositionTicks = startPositionTicks < 0 ? 0 : startPositionTicks;
    MediaSourceId = mediaSourceId ?? "";
}

public string MediaSourceId { get; }
```

- [ ] **Step 2: Replace detail XAML with summary sections**

In `src/NextGenEmby.App/Views/MediaDetailsPage.xaml`, keep the backdrop/poster layout and add these named panels under the Play button:

```xml
<StackPanel Orientation="Horizontal" Spacing="12">
    <Button x:Name="PlayButton" Width="220" Height="68" Click="Play_OnClick" UseSystemFocusVisuals="True">
        <StackPanel Orientation="Horizontal" Spacing="12">
            <SymbolIcon Symbol="Play" />
            <TextBlock x:Name="PlayButtonText" Text="Play" FontSize="24" />
        </StackPanel>
    </Button>
    <Button x:Name="RefreshButton" Width="180" Height="68" Click="Refresh_OnClick" Content="Refresh" />
</StackPanel>

<TextBlock Text="Versions" FontSize="26" FontWeight="SemiBold" />
<StackPanel x:Name="VersionsPanel" Spacing="8" />

<TextBlock Text="Audio" FontSize="26" FontWeight="SemiBold" />
<TextBlock x:Name="AudioSummaryBlock" FontSize="20" Foreground="{StaticResource AppMutedTextBrush}" TextWrapping="Wrap" />

<TextBlock Text="Subtitles" FontSize="26" FontWeight="SemiBold" />
<TextBlock x:Name="SubtitleSummaryBlock" FontSize="20" Foreground="{StaticResource AppMutedTextBrush}" TextWrapping="Wrap" />

<StackPanel x:Name="EpisodesSection" Spacing="12" Visibility="Collapsed">
    <TextBlock Text="Episodes" FontSize="26" FontWeight="SemiBold" />
    <StackPanel x:Name="EpisodesPanel" Spacing="8" />
</StackPanel>
```

Place these blocks between the existing Play button and `OverviewBlock`, replacing the old single Play button location.

- [ ] **Step 3: Update detail code-behind to load full detail and playback info**

Modify `src/NextGenEmby.App/Views/MediaDetailsPage.xaml.cs`:

- Accept both `MediaDetailsNavigationRequest` and legacy `EmbyMediaItem` in `OnNavigatedTo`.
- Use `EmbyApiClient.GetItemAsync` to load full detail by id.
- Use `GetPlaybackInfoAsync` to populate versions/audio/subtitle summary.
- If item type is `Series`, use `GetChildrenAsync(session, item.Id, "Season")`, then first season `GetChildrenAsync(session, season.Id, "Episode")`.

Use this pattern for the key methods:

```csharp
private string _itemId = "";
private IReadOnlyList<EmbyMediaSource> _sources = Array.Empty<EmbyMediaSource>();

protected override async void OnNavigatedTo(NavigationEventArgs e)
{
    base.OnNavigatedTo(e);
    if (e.Parameter is MediaDetailsNavigationRequest request)
    {
        _itemId = request.ItemId;
        TitleBlock.Text = request.ItemName;
    }
    else if (e.Parameter is EmbyMediaItem item)
    {
        _itemId = item.Id;
        _item = item;
    }

    await LoadDetailAsync();
}

private async Task LoadDetailAsync()
{
    if (string.IsNullOrWhiteSpace(_itemId))
    {
        RenderItem();
        return;
    }

    try
    {
        var session = await _sessionStore.LoadAsync();
        if (session == null)
        {
            StatusBlock.Text = "Sign in first.";
            return;
        }

        using (var http = new HttpClient())
        {
            var client = EmbyClientFactory.Create(http, session);
            _item = await client.GetItemAsync(session, _itemId);
            _sources = await client.GetPlaybackInfoAsync(session, _itemId);
            RenderItem();
            RenderPlaybackOptions();
            await LoadImagesAsync();
            if (_item.Type == "Series")
            {
                await LoadEpisodesAsync(client, session, _item.Id);
            }
        }
    }
    catch (Exception ex)
    {
        StatusBlock.Text = "Unable to load details: " + ex.Message;
    }
}
```

Add `RenderPlaybackOptions`:

```csharp
private void RenderPlaybackOptions()
{
    VersionsPanel.Children.Clear();
    if (_sources.Count == 0)
    {
        VersionsPanel.Children.Add(new TextBlock { Text = "No playable versions.", Foreground = (Brush)Application.Current.Resources["AppMutedTextBrush"] });
        AudioSummaryBlock.Text = "No audio tracks.";
        SubtitleSummaryBlock.Text = "No subtitles.";
        return;
    }

    foreach (var source in _sources)
    {
        VersionsPanel.Children.Add(new TextBlock
        {
            Text = CreateSourceLabel(source),
            FontSize = 20,
            Foreground = (Brush)Application.Current.Resources["AppMutedTextBrush"]
        });
    }

    var first = _sources[0];
    AudioSummaryBlock.Text = string.Join(" / ", first.AudioStreams.Select(CreateStreamLabel));
    SubtitleSummaryBlock.Text = string.Join(" / ", first.SubtitleStreams.Select(CreateStreamLabel));
}
```

Add `Refresh_OnClick`:

```csharp
private async void Refresh_OnClick(object sender, RoutedEventArgs e)
{
    await LoadDetailAsync();
}
```

Update `Play_OnClick`:

```csharp
Frame.Navigate(
    typeof(PlaybackPage),
    new PlaybackLaunchRequest(
        _item.Id,
        _item.Name,
        _item.UserData.PlaybackPositionTicks,
        _sources.Count > 0 ? _sources[0].Id : ""));
```

- [ ] **Step 4: Build**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: build succeeds. Fix missing `using System.Collections.Generic;`, `using System.Linq;`, `using Windows.UI.Xaml.Media;` if the compiler reports them.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src\NextGenEmby.App
git commit -m "feat: expand media details for playback decisions"
```

Expected: commit succeeds.

---

### Task 7: Convert Playback Page to Fullscreen OSD

**Files:**
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml`
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`

- [ ] **Step 1: Replace permanent debug panel with hidden OSD**

Modify `src/NextGenEmby.App/Views/PlaybackPage.xaml`:

- Keep `MediaPlayerElement` and `SwapChainPanel`.
- Wrap current bottom controls in a `Grid x:Name="OverlayRoot"` with `Visibility="Collapsed"`.
- Move source/audio/subtitle ComboBoxes into `Border x:Name="MoreDrawer"` aligned right with `Visibility="Collapsed"`.
- Keep `InfoPanel` inside `MoreDrawer`.
- Remove always-visible `StreamUrlBox` from normal OSD; keep it inside a collapsed `Border x:Name="ManualDebugPanel"` for no-launch manual testing.

Use this structure:

```xml
<Grid Background="Black" KeyDown="Page_OnKeyDown">
    <MediaPlayerElement x:Name="PlayerElement" AreTransportControlsEnabled="False" Stretch="Uniform" />
    <SwapChainPanel x:Name="NativeSurface" Visibility="Collapsed" />

    <Grid x:Name="OverlayRoot" Visibility="Collapsed">
        <Border VerticalAlignment="Bottom" Background="#E6070A0E" Padding="48,26">
            <StackPanel Spacing="16">
                <TextBlock x:Name="NowPlayingBlock" FontSize="24" FontWeight="SemiBold" />
                <Slider x:Name="ProgressSlider" Minimum="0" Maximum="1" Value="0" IsEnabled="False" />
                <TextBlock x:Name="SeekPreviewBlock" Visibility="Collapsed" FontSize="20" Foreground="{StaticResource AppMutedTextBrush}" />
                <StackPanel Orientation="Horizontal" Spacing="12">
                    <Button x:Name="PauseButton" Width="64" MinWidth="64" Click="Pause_OnClick"><SymbolIcon Symbol="Pause" /></Button>
                    <Button x:Name="ResumeButton" Width="64" MinWidth="64" Click="Resume_OnClick"><SymbolIcon Symbol="Play" /></Button>
                    <Button x:Name="SeekBackButton" Width="86" MinWidth="86" Click="SeekBack_OnClick" Content="-10s" />
                    <Button x:Name="SeekForwardButton" Width="86" MinWidth="86" Click="SeekForward_OnClick" Content="+30s" />
                    <Button x:Name="MoreButton" Content="More" Click="More_OnClick" />
                    <Button x:Name="StopButton" Content="Stop" Click="Stop_OnClick" />
                </StackPanel>
                <TextBlock x:Name="StatusBlock" FontSize="18" Foreground="{StaticResource AppMutedTextBrush}" />
            </StackPanel>
        </Border>

        <Border x:Name="MoreDrawer" Visibility="Collapsed" HorizontalAlignment="Right" Width="430" Background="#F0111923" Padding="24">
            <StackPanel Spacing="18">
                <TextBlock Text="Playback" FontSize="28" FontWeight="SemiBold" />
                <ComboBox x:Name="SourceBox" Header="Version" SelectionChanged="SourceBox_OnSelectionChanged" />
                <ComboBox x:Name="AudioStreamBox" Header="Audio" SelectionChanged="AudioStreamBox_OnSelectionChanged" />
                <ComboBox x:Name="SubtitleStreamBox" Header="Subtitles" SelectionChanged="SubtitleStreamBox_OnSelectionChanged" />
                <Button x:Name="InfoButton" Content="Info" Click="Info_OnClick" />
                <Border x:Name="InfoPanel" Background="{StaticResource AppSurfaceBrush}" Padding="16" Visibility="Collapsed">
                    <TextBlock x:Name="InfoBlock" FontSize="16" Foreground="{StaticResource AppMutedTextBrush}" TextWrapping="Wrap" />
                </Border>
            </StackPanel>
        </Border>
    </Grid>

    <Border x:Name="ManualDebugPanel" Visibility="Collapsed" VerticalAlignment="Top" Background="#DD000000" Padding="24">
        <TextBox x:Name="StreamUrlBox" Header="Direct stream URL" TextChanged="StreamUrlBox_OnTextChanged" />
    </Border>
</Grid>
```

- [ ] **Step 2: Add OSD visibility helpers**

In `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`, add fields:

```csharp
private readonly DispatcherTimer _overlayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
private bool _overlayVisible;
private bool _moreVisible;
```

In the constructor after `_progressTimer.Tick += ...`:

```csharp
_overlayTimer.Tick += OverlayTimer_OnTick;
```

Add methods:

```csharp
private void ShowOverlay(bool showMore = false)
{
    _overlayVisible = true;
    OverlayRoot.Visibility = Visibility.Visible;
    if (showMore)
    {
        _moreVisible = true;
        MoreDrawer.Visibility = Visibility.Visible;
    }

    _overlayTimer.Stop();
    _overlayTimer.Start();
}

private void HideOverlay()
{
    _overlayVisible = false;
    _moreVisible = false;
    OverlayRoot.Visibility = Visibility.Collapsed;
    MoreDrawer.Visibility = Visibility.Collapsed;
    InfoPanel.Visibility = Visibility.Collapsed;
    _infoVisible = false;
    _overlayTimer.Stop();
}

private void OverlayTimer_OnTick(object sender, object e)
{
    if (!_moreVisible)
    {
        HideOverlay();
    }
}

private void More_OnClick(object sender, RoutedEventArgs e)
{
    ShowOverlay(showMore: true);
}
```

Update `PlaybackPage_OnUnloaded` to stop and detach `_overlayTimer`.

- [ ] **Step 3: Update status and now playing**

In `StartItemPlaybackAsync`, replace `StreamUrlBox.Text` updates with:

```csharp
NowPlayingBlock.Text = string.IsNullOrWhiteSpace(_currentItemName)
    ? request.ItemId
    : _currentItemName;
ShowOverlay();
```

In `StartManualPlaybackAsync`, set `ManualDebugPanel.Visibility = Visibility.Visible;` before using `StreamUrlBox`.

In `UpdateControlStates`, guard `StreamUrlBox` access:

```csharp
StartButton.IsEnabled = _launchRequest != null || IsSupportedDirectStreamUrl(StreamUrlBox.Text);
```

If `StartButton` no longer exists in the new XAML, replace it with:

```csharp
var canStart = _launchRequest != null || IsSupportedDirectStreamUrl(StreamUrlBox.Text);
PauseButton.IsEnabled = hasActivePlayback && state != CorePlaybackState.Paused;
ResumeButton.IsEnabled = hasActivePlayback && state == CorePlaybackState.Paused;
StopButton.IsEnabled = hasActivePlayback || canStart;
```

- [ ] **Step 4: Build**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: build succeeds. Fix stale references to deleted `StartButton` or old layout names.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src\NextGenEmby.App\Views\PlaybackPage.xaml src\NextGenEmby.App\Views\PlaybackPage.xaml.cs
git commit -m "feat: convert playback controls to fullscreen OSD"
```

Expected: commit succeeds.

---

### Task 8: Wire Gamepad OSD and Cancellable Seek Preview

**Files:**
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Playback/SeekPreviewSessionTests.cs`

- [ ] **Step 1: Add playback page seek preview fields**

In `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`, add:

```csharp
private readonly SeekPreviewSession _seekPreview = new SeekPreviewSession(
    TimeSpan.FromSeconds(1.8),
    TimeSpan.FromSeconds(5),
    0.55);
private readonly DispatcherTimer _seekPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
```

In constructor:

```csharp
_seekPreviewTimer.Tick += SeekPreviewTimer_OnTick;
```

In unload:

```csharp
_seekPreviewTimer.Stop();
_seekPreviewTimer.Tick -= SeekPreviewTimer_OnTick;
```

- [ ] **Step 2: Add key handling**

Add handler:

```csharp
private async void Page_OnKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
{
    if (e.Key == Windows.System.VirtualKey.GamepadA)
    {
        if (_seekPreview.IsActive)
        {
            await ApplySeekDecisionAsync(_seekPreview.Confirm());
        }
        else
        {
            ShowOverlay();
        }

        e.Handled = true;
        return;
    }

    if (e.Key == Windows.System.VirtualKey.GamepadB)
    {
        if (_seekPreview.IsActive)
        {
            await ApplySeekDecisionAsync(_seekPreview.Cancel());
        }
        else if (_moreVisible)
        {
            _moreVisible = false;
            MoreDrawer.Visibility = Visibility.Collapsed;
        }
        else if (_overlayVisible)
        {
            HideOverlay();
        }
        else if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }

        e.Handled = true;
        return;
    }

    if (e.Key == Windows.System.VirtualKey.GamepadMenu)
    {
        ShowOverlay(showMore: true);
        e.Handled = true;
        return;
    }

    if (e.Key == Windows.System.VirtualKey.GamepadDPadLeft)
    {
        await RunPlaybackCommandAsync(() => SeekRelativeAsync(-SeekBackStep));
        ShowOverlay();
        e.Handled = true;
        return;
    }

    if (e.Key == Windows.System.VirtualKey.GamepadDPadRight)
    {
        await RunPlaybackCommandAsync(() => SeekRelativeAsync(SeekForwardStep));
        ShowOverlay();
        e.Handled = true;
        return;
    }

    if (e.Key == Windows.System.VirtualKey.GamepadLeftThumbstickLeft)
    {
        BeginOrMoveSeekPreview(TimeSpan.FromSeconds(-5));
        e.Handled = true;
        return;
    }

    if (e.Key == Windows.System.VirtualKey.GamepadLeftThumbstickRight)
    {
        BeginOrMoveSeekPreview(TimeSpan.FromSeconds(5));
        e.Handled = true;
    }
}
```

- [ ] **Step 3: Add seek preview helpers**

Add:

```csharp
private void BeginOrMoveSeekPreview(TimeSpan delta)
{
    ShowOverlay();
    var now = TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks);
    if (!_seekPreview.IsActive)
    {
        _seekPreview.Begin(GetCurrentPositionTicks(), now);
    }

    _seekPreview.MoveBy(delta, now);
    SeekPreviewBlock.Visibility = Visibility.Visible;
    SeekPreviewBlock.Text =
        "Jump to " + FormatPosition(TimeSpan.FromTicks(_seekPreview.TargetTicks)) +
        " / A to apply / B to cancel";
    _seekPreviewTimer.Stop();
    _seekPreviewTimer.Start();
}

private async void SeekPreviewTimer_OnTick(object sender, object e)
{
    var decision = _seekPreview.DecideTimeout(TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks));
    await ApplySeekDecisionAsync(decision);
}

private async Task ApplySeekDecisionAsync(SeekPreviewDecision decision)
{
    if (decision.Kind == SeekPreviewDecisionKind.None)
    {
        return;
    }

    _seekPreviewTimer.Stop();
    SeekPreviewBlock.Visibility = Visibility.Collapsed;

    if (decision.Kind == SeekPreviewDecisionKind.Commit)
    {
        await RunPlaybackCommandAsync(async () =>
        {
            await _orchestrator.SeekAsync(decision.PositionTicks);
            _lastPositionTicks = decision.PositionTicks;
            await ReportProgressAsync(PlaybackProgressEvent.TimeUpdate);
            UpdateStatus(_orchestrator.State, "Position " + FormatPosition(TimeSpan.FromTicks(decision.PositionTicks)));
        });
    }
    else
    {
        UpdateStatus(_orchestrator.State, "Seek canceled");
    }
}
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter SeekPreviewSessionTests -v minimal
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: seek tests pass and solution builds.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src\NextGenEmby.App\Views\PlaybackPage.xaml.cs
git commit -m "feat: add gamepad OSD and cancellable seek"
```

Expected: commit succeeds.

---

### Task 9: Implement Search Results

**Files:**
- Modify: `src/NextGenEmby.App/Views/SearchPage.xaml.cs`

- [ ] **Step 1: Replace search code-behind**

Replace `src/NextGenEmby.App/Views/SearchPage.xaml.cs` with:

```csharp
using System;
using System.Net.Http;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class SearchPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();

        public SearchPage()
        {
            InitializeComponent();
        }

        private async void Search_OnClick(object sender, RoutedEventArgs e)
        {
            ResultsPanel.Children.Clear();
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                StatusBlock.Text = "Enter a title.";
                return;
            }

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (session == null)
                {
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                using (var http = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(http, session);
                    var results = await client.SearchItemsAsync(session, SearchBox.Text.Trim(), "Movie,Series,Episode");
                    StatusBlock.Text = results.Count == 0 ? "No results." : results.Count + " results";
                    foreach (var item in results)
                    {
                        var button = new Button
                        {
                            Content = item.Name + " / " + item.Type,
                            Tag = item,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        button.Click += Result_OnClick;
                        ResultsPanel.Children.Add(button);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusBlock.Text = "Search failed: " + ex.Message;
            }
        }

        private void Result_OnClick(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is EmbyMediaItem item)
            {
                Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(item.Id, item.Name));
            }
        }
    }
}
```

- [ ] **Step 2: Build**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

Run:

```powershell
git add src\NextGenEmby.App\Views\SearchPage.xaml.cs
git commit -m "feat: add Emby search page"
```

Expected: commit succeeds.

---

### Task 10: Final Verification and Documentation

**Files:**
- Modify: `docs/foundation-status.md`

- [ ] **Step 1: Run all tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 2: Build solution**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 3: Local Windows smoke test**

Run the app on Local Machine from Visual Studio or with the currently installed package. Verify:

- Home loads real Emby rows.
- Movies and TV buttons open Library pages.
- Library cards enter detail pages.
- Detail page loads versions/audio/subtitle summaries.
- Play enters fullscreen playback.
- A shows OSD.
- Menu opens More drawer.
- B closes More drawer, then OSD.
- D-pad left/right performs immediate -10s/+30s seek.
- Left thumbstick left/right moves seek preview; A commits; B cancels.

- [ ] **Step 4: Record status**

Append to `docs/foundation-status.md`:

```markdown

## Xbox Fluent UI Redesign Status

Date: 2026-07-05

Verified on local Windows:

- Home uses real Emby rows and opens detail pages.
- Movies and TV Library pages open and load real Emby items.
- Detail pages load playback options and episode data where available.
- Playback defaults to fullscreen video with hidden OSD.
- OSD supports version, audio, subtitle, info, and seek controls.
- Thumbstick seek uses preview commit with A apply, B cancel, and auto-commit.

Xbox hardware validation remains pending until the page interaction pass is stable on Windows.
```

- [ ] **Step 5: Commit**

Run:

```powershell
git add docs\foundation-status.md
git commit -m "docs: record Xbox Fluent UI verification"
```

Expected: commit succeeds.

- [ ] **Step 6: Confirm clean working tree**

Run:

```powershell
git status --short
```

Expected: no output.

---

## Self-Review Notes

Spec coverage:

- Home Hub and Movies/TV entry points: Tasks 3 and 4.
- Real Library browsing: Tasks 1 and 5.
- Detail page playback decisions: Tasks 1 and 6.
- Fullscreen playback OSD and More drawer: Task 7.
- Version/audio/subtitle switching preserved from existing `PlaybackPage.xaml.cs` and moved into More drawer in Task 7.
- Cancellable seek preview: Tasks 2 and 8.
- Search and Settings visible entries: Tasks 3 and 9.
- Windows verification before Xbox hardware testing: Task 10.

Known follow-up after this plan:

- Refine visual polish with real screenshots after first implementation pass.
- Add virtualized/incremental loading if a large library stutters during Task 10 smoke.
- Revisit Xbox hardware-specific HDR/HEVC testing after the UI is usable.
