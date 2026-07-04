# Xbox Emby Player Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first working foundation slice for an Xbox-only Emby client: verified UWP toolchain, solution structure, tested Emby API core, playback orchestration interfaces, UWP Xbox shell, and a documented Kodi HDR research path.

**Architecture:** Keep the Emby API and playback orchestration in testable .NET Standard libraries, keep the Xbox UI in a UWP XAML app, and isolate the first playback backend behind an interface so the Kodi-grade C++/WinRT DirectX core can replace the system-player backend in a separate native-core plan. The first slice must support login, movie/TV library queries, PlaybackInfo parsing, media-version/audio/subtitle metadata, progress reporting, and a UWP shell that can navigate through the core flow.

**Tech Stack:** Visual Studio 2022 Community 17.14, MSBuild 17.14, Windows SDK 10.0.22621.0, UWP, C# XAML, WinUI 2.8.7, Microsoft.NETCore.UniversalWindowsPlatform 6.2.15, .NET Standard 2.0, .NET 9 test project, xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.7.0, System.Text.Json 10.0.9.

---

## Scope Check

The approved spec covers several large subsystems: Xbox UI, Emby protocol, playback orchestration, Kodi-grade native playback, HDR10 passthrough, subtitles, and distribution. This plan implements the first foundation vertical slice only.

Included in this plan:

- UWP and test toolchain verification.
- Solution and repository scaffolding.
- Tested Emby API models and client.
- Tested PlaybackInfo parsing for media versions, audio streams, and subtitles.
- Tested playback orchestration boundary.
- UWP shell with Xbox-oriented pages and a system-player backend behind the same interface that the native core will use.
- Kodi HDR research ADR with concrete files, APIs, and decisions to carry into the native-core plan.

Not included in this plan:

- Final C++/WinRT DirectX playback core.
- FFmpeg integration.
- HDR10 passthrough implementation.
- Native subtitle renderer.
- Store private-audience submission.

Those items require a dedicated native playback implementation plan after this foundation builds and runs.

## File Structure

Create or modify these files:

- `.gitignore`: ignore Visual Studio, build, app packages, and research checkout artifacts.
- `Directory.Build.props`: shared C# language and warning settings.
- `NextGenXboxEmby.sln`: solution containing core, tests, and UWP app.
- `src/NextGenEmby.Core/NextGenEmby.Core.csproj`: testable .NET Standard core library.
- `src/NextGenEmby.Core/Emby/*.cs`: Emby API client, request builders, DTOs, and parsing helpers.
- `src/NextGenEmby.Core/Playback/*.cs`: playback descriptors, backend interface, and orchestrator.
- `src/NextGenEmby.Core/Storage/*.cs`: session storage contracts.
- `tests/NextGenEmby.Core.Tests/NextGenEmby.Core.Tests.csproj`: .NET 9 xUnit tests.
- `tests/NextGenEmby.Core.Tests/TestHttpMessageHandler.cs`: deterministic HTTP test transport.
- `tests/NextGenEmby.Core.Tests/Emby/*.cs`: authentication, library, PlaybackInfo, and progress tests.
- `tests/NextGenEmby.Core.Tests/Playback/*.cs`: playback orchestrator tests.
- `src/NextGenEmby.App/NextGenEmby.App.csproj`: UWP app project.
- `src/NextGenEmby.App/Package.appxmanifest`: Xbox-capable UWP package manifest.
- `src/NextGenEmby.App/App.xaml` and `App.xaml.cs`: UWP application entry point.
- `src/NextGenEmby.App/MainPage.xaml` and `MainPage.xaml.cs`: shell navigation.
- `src/NextGenEmby.App/Views/*.xaml`: login, home, library, detail, and playback pages.
- `src/NextGenEmby.App/ViewModels/*.cs`: UWP-facing view models that wrap core services.
- `src/NextGenEmby.App/Playback/SystemMediaPlaybackBackend.cs`: first backend implementing the playback interface with UWP media controls.
- `src/NextGenEmby.App/Storage/ApplicationDataSessionStore.cs`: UWP local settings store.
- `docs/adr/0001-kodi-xbox-hdr-path.md`: Kodi HDR research findings and native-core implications.

---

### Task 1: Verify and Install the UWP Toolchain

**Files:**
- Modify: none

- [ ] **Step 1: Verify Visual Studio and MSBuild**

Run:

```powershell
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
& $vswhere -latest -products * -property installationPath
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' -version
```

Expected:

```text
C:\Program Files\Microsoft Visual Studio\2022\Community
17.14
```

- [ ] **Step 2: Verify the Universal Windows workload**

Run:

```powershell
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
& $vswhere -latest -products * -requires Microsoft.VisualStudio.Workload.Universal -property installationPath
```

Expected after the workload is installed:

```text
C:\Program Files\Microsoft Visual Studio\2022\Community
```

- [ ] **Step 3: Install UWP workload when Step 2 prints no path**

Run:

```powershell
$installer = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe'
$installPath = 'C:\Program Files\Microsoft Visual Studio\2022\Community'
& $installer modify `
  --installPath $installPath `
  --add Microsoft.VisualStudio.Workload.Universal `
  --add Microsoft.VisualStudio.Component.Windows10SDK.22621 `
  --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
  --includeRecommended `
  --passive `
  --norestart
```

Expected: Visual Studio Installer completes without an error dialog. Re-run Step 2 and confirm it prints the VS path.

- [ ] **Step 4: Verify Windows SDK**

Run:

```powershell
Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\Lib' |
  Sort-Object Name -Descending |
  Select-Object -First 3 -ExpandProperty Name
```

Expected:

```text
10.0.22621.0
```

- [ ] **Step 5: Commit no changes**

Run:

```powershell
git status --short
```

Expected: no source changes from this task.

---

### Task 2: Create the Solution and Shared Repository Files

**Files:**
- Create: `.gitignore`
- Create: `Directory.Build.props`
- Create: `NextGenXboxEmby.sln`

- [ ] **Step 1: Create `.gitignore`**

Create `.gitignore` with:

```gitignore
.vs/
bin/
obj/
TestResults/
*.user
*.suo
*.userosscache
*.sln.docstates
AppPackages/
BundleArtifacts/
Package.StoreAssociation.xml
.research/
.superpowers/
```

- [ ] **Step 2: Create `Directory.Build.props`**

Create `Directory.Build.props` with:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningsAsErrors>nullable</WarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create the solution file**

Run:

```powershell
dotnet new sln -n NextGenXboxEmby
```

Expected:

```text
The template "Solution File" was created successfully.
```

- [ ] **Step 4: Verify repository status**

Run:

```powershell
git status --short
```

Expected:

```text
new file: .gitignore
new file: Directory.Build.props
new file: NextGenXboxEmby.sln
```

- [ ] **Step 5: Commit**

Run:

```powershell
git add .gitignore Directory.Build.props NextGenXboxEmby.sln
git commit -m "chore: initialize solution structure"
```

Expected: commit succeeds.

---

### Task 3: Add Core Domain Models

**Files:**
- Create: `src/NextGenEmby.Core/NextGenEmby.Core.csproj`
- Create: `src/NextGenEmby.Core/Emby/EmbySession.cs`
- Create: `src/NextGenEmby.Core/Emby/EmbyMediaItem.cs`
- Create: `src/NextGenEmby.Core/Emby/EmbyMediaSource.cs`
- Create: `src/NextGenEmby.Core/Emby/EmbyMediaStream.cs`
- Create: `src/NextGenEmby.Core/Emby/EmbyStreamKind.cs`
- Create: `tests/NextGenEmby.Core.Tests/NextGenEmby.Core.Tests.csproj`
- Create: `tests/NextGenEmby.Core.Tests/Emby/EmbyModelTests.cs`

- [ ] **Step 1: Create the core project**

Run:

```powershell
New-Item -ItemType Directory -Force src\NextGenEmby.Core\Emby | Out-Null
```

Create `src/NextGenEmby.Core/NextGenEmby.Core.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <RootNamespace>NextGenEmby.Core</RootNamespace>
    <AssemblyName>NextGenEmby.Core</AssemblyName>
    <ProjectGuid>{3E3D8F22-1FD8-4A53-81D4-11998454C03B}</ProjectGuid>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="10.0.9" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the test project**

Run:

```powershell
New-Item -ItemType Directory -Force tests\NextGenEmby.Core.Tests\Emby | Out-Null
```

Create `tests/NextGenEmby.Core.Tests/NextGenEmby.Core.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <RootNamespace>NextGenEmby.Core.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.7.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\NextGenEmby.Core\NextGenEmby.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add projects to the solution**

Run:

```powershell
dotnet sln NextGenXboxEmby.sln add src\NextGenEmby.Core\NextGenEmby.Core.csproj
dotnet sln NextGenXboxEmby.sln add tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj
```

Expected: both projects are added.

- [ ] **Step 4: Write failing model tests**

Create `tests/NextGenEmby.Core.Tests/Emby/EmbyModelTests.cs` with:

```csharp
using System.Linq;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyModelTests
{
    [Fact]
    public void MediaSource_Exposes_Hdr_Audio_And_Subtitle_Metadata()
    {
        var source = new EmbyMediaSource
        {
            Id = "source-4k",
            Name = "4K HDR",
            Container = "mkv",
            Bitrate = 76_000_000,
            Width = 3840,
            Height = 2160,
            IsHdr = true,
            Streams =
            {
                new EmbyMediaStream
                {
                    Index = 0,
                    Kind = EmbyStreamKind.Video,
                    Codec = "hevc",
                    DisplayTitle = "4K HEVC Main10 HDR10"
                },
                new EmbyMediaStream
                {
                    Index = 1,
                    Kind = EmbyStreamKind.Audio,
                    Language = "jpn",
                    Codec = "truehd",
                    ChannelLayout = "7.1",
                    DisplayTitle = "Japanese TrueHD 7.1 Atmos"
                },
                new EmbyMediaStream
                {
                    Index = 2,
                    Kind = EmbyStreamKind.Subtitle,
                    Language = "chi",
                    Codec = "ass",
                    IsExternal = true,
                    DisplayTitle = "Chinese ASS"
                }
            }
        };

        Assert.Equal("4K HDR", source.Name);
        Assert.True(source.IsHdr);
        Assert.Equal("hevc", source.VideoStreams.Single().Codec);
        Assert.Equal("truehd", source.AudioStreams.Single().Codec);
        Assert.True(source.SubtitleStreams.Single().IsExternal);
    }
}
```

- [ ] **Step 5: Run the failing test**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter MediaSource_Exposes_Hdr_Audio_And_Subtitle_Metadata -v minimal
```

Expected: build fails because `EmbyMediaSource`, `EmbyMediaStream`, and `EmbyStreamKind` do not exist.

- [ ] **Step 6: Implement model classes**

Create `src/NextGenEmby.Core/Emby/EmbyStreamKind.cs` with:

```csharp
namespace NextGenEmby.Core.Emby
{
    public enum EmbyStreamKind
    {
        Video,
        Audio,
        Subtitle
    }
}
```

Create `src/NextGenEmby.Core/Emby/EmbyMediaStream.cs` with:

```csharp
namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyMediaStream
    {
        public int Index { get; set; }
        public EmbyStreamKind Kind { get; set; }
        public string Codec { get; set; } = "";
        public string Language { get; set; } = "";
        public string ChannelLayout { get; set; } = "";
        public string DisplayTitle { get; set; } = "";
        public bool IsExternal { get; set; }
    }
}
```

Create `src/NextGenEmby.Core/Emby/EmbyMediaSource.cs` with:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyMediaSource
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Container { get; set; } = "";
        public long Bitrate { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsHdr { get; set; }
        public string DirectStreamUrl { get; set; } = "";
        public List<EmbyMediaStream> Streams { get; } = new List<EmbyMediaStream>();

        public IEnumerable<EmbyMediaStream> VideoStreams => Streams.Where(s => s.Kind == EmbyStreamKind.Video);
        public IEnumerable<EmbyMediaStream> AudioStreams => Streams.Where(s => s.Kind == EmbyStreamKind.Audio);
        public IEnumerable<EmbyMediaStream> SubtitleStreams => Streams.Where(s => s.Kind == EmbyStreamKind.Subtitle);
    }
}
```

Create `src/NextGenEmby.Core/Emby/EmbyMediaItem.cs` with:

```csharp
namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyMediaItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Overview { get; set; } = "";
        public int? ProductionYear { get; set; }
        public long? RunTimeTicks { get; set; }
        public string PrimaryImageTag { get; set; } = "";
        public string BackdropImageTag { get; set; } = "";
    }
}
```

Create `src/NextGenEmby.Core/Emby/EmbySession.cs` with:

```csharp
namespace NextGenEmby.Core.Emby
{
    public sealed class EmbySession
    {
        public string ServerUrl { get; set; } = "";
        public string UserId { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string UserName { get; set; } = "";
    }
}
```

- [ ] **Step 7: Run the test**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter MediaSource_Exposes_Hdr_Audio_And_Subtitle_Metadata -v minimal
```

Expected: test passes.

- [ ] **Step 8: Commit**

Run:

```powershell
git add src\NextGenEmby.Core tests\NextGenEmby.Core.Tests NextGenXboxEmby.sln
git commit -m "feat: add core media models"
```

Expected: commit succeeds.

---

### Task 4: Add Emby Authentication Client

**Files:**
- Create: `src/NextGenEmby.Core/Emby/EmbyClientOptions.cs`
- Create: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Create: `tests/NextGenEmby.Core.Tests/TestHttpMessageHandler.cs`
- Create: `tests/NextGenEmby.Core.Tests/Emby/EmbyAuthenticationTests.cs`

- [ ] **Step 1: Write the failing authentication test**

Create `tests/NextGenEmby.Core.Tests/TestHttpMessageHandler.cs` with:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NextGenEmby.Core.Tests;

public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_handler(request));
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }
}
```

Create `tests/NextGenEmby.Core.Tests/Emby/EmbyAuthenticationTests.cs` with:

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyAuthenticationTests
{
    [Fact]
    public async Task AuthenticateAsync_Posts_Credentials_And_Returns_Session()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "AccessToken": "token-123",
              "User": {
                "Id": "user-1",
                "Name": "Alice"
              }
            }
            """));
        using var http = new HttpClient(handler);
        var client = new EmbyApiClient(http, new EmbyClientOptions
        {
            ServerUrl = "http://emby.local:8096",
            DeviceName = "Next Gen Xbox Emby",
            DeviceId = "test-device",
            ClientName = "Next Gen Xbox Emby",
            ClientVersion = "0.1.0"
        });

        var session = await client.AuthenticateAsync("alice", "secret");

        Assert.Equal("http://emby.local:8096", session.ServerUrl);
        Assert.Equal("user-1", session.UserId);
        Assert.Equal("Alice", session.UserName);
        Assert.Equal("token-123", session.AccessToken);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/Users/AuthenticateByName", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("MediaBrowser Client=", handler.LastRequest.Headers.Authorization!.Parameter);
    }
}
```

- [ ] **Step 2: Run the failing authentication test**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter AuthenticateAsync_Posts_Credentials_And_Returns_Session -v minimal
```

Expected: build fails because `EmbyApiClient` and `EmbyClientOptions` do not exist.

- [ ] **Step 3: Implement authentication**

Create `src/NextGenEmby.Core/Emby/EmbyClientOptions.cs` with:

```csharp
namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyClientOptions
    {
        public string ServerUrl { get; set; } = "";
        public string ClientName { get; set; } = "Next Gen Xbox Emby";
        public string ClientVersion { get; set; } = "0.1.0";
        public string DeviceName { get; set; } = "Xbox";
        public string DeviceId { get; set; } = "";
    }
}
```

Create `src/NextGenEmby.Core/Emby/EmbyApiClient.cs` with:

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyApiClient
    {
        private readonly HttpClient _http;
        private readonly EmbyClientOptions _options;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public EmbyApiClient(HttpClient http, EmbyClientOptions options)
        {
            _http = http;
            _options = options;
            _http.BaseAddress = new Uri(options.ServerUrl.TrimEnd('/') + "/");
        }

        public async Task<EmbySession> AuthenticateAsync(string username, string password)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "Users/AuthenticateByName");
            ApplyAuthorizationHeader(request, null);
            var json = JsonSerializer.Serialize(new { Username = username, Pw = password });
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<AuthResponseDto>(body, _jsonOptions);
            if (dto == null)
            {
                throw new InvalidOperationException("Emby authentication response was empty.");
            }

            return new EmbySession
            {
                ServerUrl = _options.ServerUrl.TrimEnd('/'),
                UserId = dto.User.Id,
                UserName = dto.User.Name,
                AccessToken = dto.AccessToken
            };
        }

        private void ApplyAuthorizationHeader(HttpRequestMessage request, string? token)
        {
            var value =
                $"MediaBrowser Client=\"{_options.ClientName}\", " +
                $"Device=\"{_options.DeviceName}\", " +
                $"DeviceId=\"{_options.DeviceId}\", " +
                $"Version=\"{_options.ClientVersion}\"";

            if (!string.IsNullOrWhiteSpace(token))
            {
                value += $", Token=\"{token}\"";
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Emby", value);
        }

        private sealed class AuthResponseDto
        {
            public string AccessToken { get; set; } = "";
            public UserDto User { get; set; } = new UserDto();
        }

        private sealed class UserDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
        }
    }
}
```

- [ ] **Step 4: Run the authentication test**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter AuthenticateAsync_Posts_Credentials_And_Returns_Session -v minimal
```

Expected: test passes.

- [ ] **Step 5: Run all core tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src\NextGenEmby.Core tests\NextGenEmby.Core.Tests
git commit -m "feat: add Emby authentication client"
```

Expected: commit succeeds.

---

### Task 5: Add Emby Library and Image Queries

**Files:**
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Create: `tests/NextGenEmby.Core.Tests/Emby/EmbyLibraryTests.cs`

- [ ] **Step 1: Write failing library tests**

Create `tests/NextGenEmby.Core.Tests/Emby/EmbyLibraryTests.cs` with:

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyLibraryTests
{
    [Fact]
    public async Task GetLatestItemsAsync_Parses_Movies_And_TV_Items()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            [
              {
                "Id": "movie-1",
                "Name": "Blade Runner",
                "Type": "Movie",
                "Overview": "Replicants and rain.",
                "ProductionYear": 1982,
                "RunTimeTicks": 70200000000,
                "ImageTags": { "Primary": "primary-tag" },
                "BackdropImageTags": [ "backdrop-tag" ]
              }
            ]
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetLatestItemsAsync(Session());

        var item = Assert.Single(items);
        Assert.Equal("movie-1", item.Id);
        Assert.Equal("Movie", item.Type);
        Assert.Equal("primary-tag", item.PrimaryImageTag);
        Assert.Equal("backdrop-tag", item.BackdropImageTag);
        Assert.Contains("/Users/user-1/Items/Latest", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public void GetImageUrl_Builds_Primary_Image_Url()
    {
        using var http = new HttpClient(new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var client = CreateClient(http);

        var url = client.GetImageUrl(Session(), "movie-1", "Primary", 600);

        Assert.Equal("http://emby.local:8096/Items/movie-1/Images/Primary?maxWidth=600&quality=90&api_key=token-123", url);
    }

    private static EmbyApiClient CreateClient(HttpClient http) => new EmbyApiClient(http, new EmbyClientOptions
    {
        ServerUrl = "http://emby.local:8096",
        DeviceName = "Next Gen Xbox Emby",
        DeviceId = "test-device",
        ClientName = "Next Gen Xbox Emby",
        ClientVersion = "0.1.0"
    });

    private static EmbySession Session() => new EmbySession
    {
        ServerUrl = "http://emby.local:8096",
        UserId = "user-1",
        UserName = "Alice",
        AccessToken = "token-123"
    };
}
```

- [ ] **Step 2: Run failing library tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter EmbyLibraryTests -v minimal
```

Expected: build fails because `GetLatestItemsAsync` and `GetImageUrl` do not exist.

- [ ] **Step 3: Implement library queries**

Modify `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`:

```csharp
// Add these using directives at the top.
using System.Collections.Generic;
using System.Linq;

// Add these methods inside EmbyApiClient.
public async Task<IReadOnlyList<EmbyMediaItem>> GetLatestItemsAsync(EmbySession session)
{
    using var request = new HttpRequestMessage(
        HttpMethod.Get,
        $"Users/{session.UserId}/Items/Latest?IncludeItemTypes=Movie,Series,Episode&Fields=Overview,ProductionYear,RunTimeTicks,PrimaryImageAspectRatio&Limit=50");
    ApplyAuthorizationHeader(request, session.AccessToken);

    using var response = await _http.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var dto = JsonSerializer.Deserialize<List<ItemDto>>(body, _jsonOptions);
    if (dto == null)
    {
        dto = new List<ItemDto>();
    }
    return dto.Select(MapItem).ToList();
}

public string GetImageUrl(EmbySession session, string itemId, string imageType, int maxWidth)
{
    return $"{session.ServerUrl}/Items/{itemId}/Images/{imageType}?maxWidth={maxWidth}&quality=90&api_key={session.AccessToken}";
}

private static EmbyMediaItem MapItem(ItemDto item)
{
    return new EmbyMediaItem
    {
        Id = item.Id,
        Name = item.Name,
        Type = item.Type,
        Overview = item.Overview,
        ProductionYear = item.ProductionYear,
        RunTimeTicks = item.RunTimeTicks,
        PrimaryImageTag = item.ImageTags.TryGetValue("Primary", out var primary) ? primary : "",
        BackdropImageTag = item.BackdropImageTags.Count > 0 ? item.BackdropImageTags[0] : ""
    };
}

private sealed class ItemDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Overview { get; set; } = "";
    public int? ProductionYear { get; set; }
    public long? RunTimeTicks { get; set; }
    public Dictionary<string, string> ImageTags { get; set; } = new Dictionary<string, string>();
    public List<string> BackdropImageTags { get; set; } = new List<string>();
}
```

- [ ] **Step 4: Run library tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter EmbyLibraryTests -v minimal
```

Expected: tests pass.

- [ ] **Step 5: Run all core tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src\NextGenEmby.Core tests\NextGenEmby.Core.Tests
git commit -m "feat: add Emby library queries"
```

Expected: commit succeeds.

---

### Task 6: Add PlaybackInfo Parsing

**Files:**
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Create: `tests/NextGenEmby.Core.Tests/Emby/EmbyPlaybackInfoTests.cs`

- [ ] **Step 1: Write failing PlaybackInfo test**

Create `tests/NextGenEmby.Core.Tests/Emby/EmbyPlaybackInfoTests.cs` with:

```csharp
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyPlaybackInfoTests
{
    [Fact]
    public async Task GetPlaybackInfoAsync_Parses_MediaVersions_Audio_And_Subtitles()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-4k",
                  "Name": "4K HDR",
                  "Container": "mkv",
                  "Bitrate": 76000000,
                  "Path": "/media/movie.mkv",
                  "MediaStreams": [
                    {
                      "Index": 0,
                      "Type": "Video",
                      "Codec": "hevc",
                      "Width": 3840,
                      "Height": 2160,
                      "VideoRange": "HDR10",
                      "DisplayTitle": "4K HEVC Main10 HDR10"
                    },
                    {
                      "Index": 1,
                      "Type": "Audio",
                      "Codec": "truehd",
                      "Language": "eng",
                      "ChannelLayout": "7.1",
                      "DisplayTitle": "English TrueHD 7.1 Atmos"
                    },
                    {
                      "Index": 2,
                      "Type": "Subtitle",
                      "Codec": "ass",
                      "Language": "chi",
                      "IsExternal": true,
                      "DisplayTitle": "Chinese ASS"
                    }
                  ]
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal("source-4k", source.Id);
        Assert.True(source.IsHdr);
        Assert.Equal(3840, source.Width);
        Assert.Equal(2160, source.Height);
        Assert.Equal("http://emby.local:8096/Videos/movie-1/stream?static=true&mediaSourceId=source-4k&api_key=token-123", source.DirectStreamUrl);
        Assert.Equal("hevc", source.VideoStreams.Single().Codec);
        Assert.Equal("truehd", source.AudioStreams.Single().Codec);
        Assert.True(source.SubtitleStreams.Single().IsExternal);
    }

    private static EmbyApiClient CreateClient(HttpClient http) => new EmbyApiClient(http, new EmbyClientOptions
    {
        ServerUrl = "http://emby.local:8096",
        DeviceName = "Next Gen Xbox Emby",
        DeviceId = "test-device",
        ClientName = "Next Gen Xbox Emby",
        ClientVersion = "0.1.0"
    });

    private static EmbySession Session() => new EmbySession
    {
        ServerUrl = "http://emby.local:8096",
        UserId = "user-1",
        UserName = "Alice",
        AccessToken = "token-123"
    };
}
```

- [ ] **Step 2: Run failing PlaybackInfo test**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter GetPlaybackInfoAsync_Parses_MediaVersions_Audio_And_Subtitles -v minimal
```

Expected: build fails because `GetPlaybackInfoAsync` does not exist.

- [ ] **Step 3: Implement PlaybackInfo parsing**

Modify `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`:

```csharp
// Add this method inside EmbyApiClient.
public async Task<IReadOnlyList<EmbyMediaSource>> GetPlaybackInfoAsync(EmbySession session, string itemId)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, $"Items/{itemId}/PlaybackInfo");
    ApplyAuthorizationHeader(request, session.AccessToken);

    using var response = await _http.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var dto = JsonSerializer.Deserialize<PlaybackInfoDto>(body, _jsonOptions);
    if (dto == null)
    {
        dto = new PlaybackInfoDto();
    }
    return dto.MediaSources.Select(source => MapMediaSource(session, itemId, source)).ToList();
}

private static EmbyMediaSource MapMediaSource(EmbySession session, string itemId, MediaSourceDto source)
{
    var result = new EmbyMediaSource
    {
        Id = source.Id,
        Name = string.IsNullOrWhiteSpace(source.Name) ? source.Id : source.Name,
        Container = source.Container,
        Bitrate = source.Bitrate,
        DirectStreamUrl = $"{session.ServerUrl}/Videos/{itemId}/stream?static=true&mediaSourceId={source.Id}&api_key={session.AccessToken}"
    };

    foreach (var stream in source.MediaStreams)
    {
        var kind = ParseStreamKind(stream.Type);
        result.Streams.Add(new EmbyMediaStream
        {
            Index = stream.Index,
            Kind = kind,
            Codec = stream.Codec,
            Language = stream.Language,
            ChannelLayout = stream.ChannelLayout,
            DisplayTitle = stream.DisplayTitle,
            IsExternal = stream.IsExternal
        });

        if (kind == EmbyStreamKind.Video)
        {
            result.Width = stream.Width;
            result.Height = stream.Height;
            result.IsHdr = stream.VideoRange.IndexOf("HDR", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    return result;
}

private static EmbyStreamKind ParseStreamKind(string type)
{
    if (string.Equals(type, "Video", StringComparison.OrdinalIgnoreCase))
    {
        return EmbyStreamKind.Video;
    }

    if (string.Equals(type, "Audio", StringComparison.OrdinalIgnoreCase))
    {
        return EmbyStreamKind.Audio;
    }

    return EmbyStreamKind.Subtitle;
}

private sealed class PlaybackInfoDto
{
    public List<MediaSourceDto> MediaSources { get; set; } = new List<MediaSourceDto>();
}

private sealed class MediaSourceDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Container { get; set; } = "";
    public long Bitrate { get; set; }
    public List<MediaStreamDto> MediaStreams { get; set; } = new List<MediaStreamDto>();
}

private sealed class MediaStreamDto
{
    public int Index { get; set; }
    public string Type { get; set; } = "";
    public string Codec { get; set; } = "";
    public string Language { get; set; } = "";
    public string ChannelLayout { get; set; } = "";
    public string DisplayTitle { get; set; } = "";
    public bool IsExternal { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string VideoRange { get; set; } = "";
}
```

- [ ] **Step 4: Run PlaybackInfo tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter EmbyPlaybackInfoTests -v minimal
```

Expected: tests pass.

- [ ] **Step 5: Run all core tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src\NextGenEmby.Core tests\NextGenEmby.Core.Tests
git commit -m "feat: parse Emby playback info"
```

Expected: commit succeeds.

---

### Task 7: Add Playback Progress Reporting

**Files:**
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Create: `src/NextGenEmby.Core/Emby/PlaybackProgressRequest.cs`
- Create: `tests/NextGenEmby.Core.Tests/Emby/EmbyProgressTests.cs`

- [ ] **Step 1: Write failing progress test**

Create `tests/NextGenEmby.Core.Tests/Emby/EmbyProgressTests.cs` with:

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyProgressTests
{
    [Fact]
    public async Task ReportProgressAsync_Posts_PlaybackProgress_To_Emby()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.NoContent, ""));
        using var http = new HttpClient(handler);
        var client = new EmbyApiClient(http, new EmbyClientOptions
        {
            ServerUrl = "http://emby.local:8096",
            DeviceName = "Next Gen Xbox Emby",
            DeviceId = "test-device",
            ClientName = "Next Gen Xbox Emby",
            ClientVersion = "0.1.0"
        });

        await client.ReportProgressAsync(Session(), new PlaybackProgressRequest
        {
            ItemId = "movie-1",
            MediaSourceId = "source-4k",
            PositionTicks = 12_000_000,
            IsPaused = false,
            EventName = "timeupdate"
        });

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/Sessions/Playing/Progress", handler.LastRequest.RequestUri!.AbsolutePath);
        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.Contains("\"ItemId\":\"movie-1\"", body);
        Assert.Contains("\"MediaSourceId\":\"source-4k\"", body);
        Assert.Contains("\"PositionTicks\":12000000", body);
    }

    private static EmbySession Session() => new EmbySession
    {
        ServerUrl = "http://emby.local:8096",
        UserId = "user-1",
        UserName = "Alice",
        AccessToken = "token-123"
    };
}
```

- [ ] **Step 2: Run failing progress test**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter ReportProgressAsync_Posts_PlaybackProgress_To_Emby -v minimal
```

Expected: build fails because `PlaybackProgressRequest` and `ReportProgressAsync` do not exist.

- [ ] **Step 3: Implement progress reporting**

Create `src/NextGenEmby.Core/Emby/PlaybackProgressRequest.cs` with:

```csharp
namespace NextGenEmby.Core.Emby
{
    public sealed class PlaybackProgressRequest
    {
        public string ItemId { get; set; } = "";
        public string MediaSourceId { get; set; } = "";
        public long PositionTicks { get; set; }
        public bool IsPaused { get; set; }
        public string EventName { get; set; } = "timeupdate";
    }
}
```

Modify `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`:

```csharp
// Add this method inside EmbyApiClient.
public async Task ReportProgressAsync(EmbySession session, PlaybackProgressRequest progress)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "Sessions/Playing/Progress");
    ApplyAuthorizationHeader(request, session.AccessToken);
    var json = JsonSerializer.Serialize(new
    {
        progress.ItemId,
        progress.MediaSourceId,
        progress.PositionTicks,
        progress.IsPaused,
        EventName = progress.EventName
    });
    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

    using var response = await _http.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
}
```

- [ ] **Step 4: Run progress tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter EmbyProgressTests -v minimal
```

Expected: tests pass.

- [ ] **Step 5: Run all core tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src\NextGenEmby.Core tests\NextGenEmby.Core.Tests
git commit -m "feat: report Emby playback progress"
```

Expected: commit succeeds.

---

### Task 8: Add Playback Orchestrator Interfaces

**Files:**
- Create: `src/NextGenEmby.Core/Playback/PlaybackDescriptor.cs`
- Create: `src/NextGenEmby.Core/Playback/PlaybackState.cs`
- Create: `src/NextGenEmby.Core/Playback/IPlaybackBackend.cs`
- Create: `src/NextGenEmby.Core/Playback/PlaybackOrchestrator.cs`
- Create: `tests/NextGenEmby.Core.Tests/Playback/PlaybackOrchestratorTests.cs`

- [ ] **Step 1: Write failing orchestrator tests**

Run:

```powershell
New-Item -ItemType Directory -Force src\NextGenEmby.Core\Playback tests\NextGenEmby.Core.Tests\Playback | Out-Null
```

Create `tests/NextGenEmby.Core.Tests/Playback/PlaybackOrchestratorTests.cs` with:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class PlaybackOrchestratorTests
{
    [Fact]
    public async Task StartAsync_Sends_Default_Source_To_Backend()
    {
        var backend = new RecordingPlaybackBackend();
        var orchestrator = new PlaybackOrchestrator(backend);
        var sources = Sources();

        await orchestrator.StartAsync("movie-1", sources, resumeTicks: 42);

        Assert.Equal("movie-1", backend.LastDescriptor!.ItemId);
        Assert.Equal("source-4k", backend.LastDescriptor.MediaSource.Id);
        Assert.Equal(42, backend.LastDescriptor.StartPositionTicks);
    }

    [Fact]
    public async Task SwitchMediaSourceAsync_Preserves_Current_Position()
    {
        var backend = new RecordingPlaybackBackend { CurrentPositionTicks = 50_000_000 };
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("movie-1", Sources(), resumeTicks: 0);

        await orchestrator.SwitchMediaSourceAsync("source-1080p");

        Assert.Equal("source-1080p", backend.LastDescriptor!.MediaSource.Id);
        Assert.Equal(50_000_000, backend.LastDescriptor.StartPositionTicks);
    }

    private static IReadOnlyList<EmbyMediaSource> Sources() => new[]
    {
        new EmbyMediaSource { Id = "source-4k", Name = "4K HDR", DirectStreamUrl = "http://server/4k.mkv" },
        new EmbyMediaSource { Id = "source-1080p", Name = "1080p SDR", DirectStreamUrl = "http://server/1080p.mkv" }
    };

    private sealed class RecordingPlaybackBackend : IPlaybackBackend
    {
        public long CurrentPositionTicks { get; set; }
        public PlaybackDescriptor? LastDescriptor { get; private set; }

        public Task StartAsync(PlaybackDescriptor descriptor)
        {
            LastDescriptor = descriptor;
            return Task.CompletedTask;
        }

        public Task PauseAsync() => Task.CompletedTask;
        public Task ResumeAsync() => Task.CompletedTask;
        public Task SeekAsync(long positionTicks)
        {
            CurrentPositionTicks = positionTicks;
            return Task.CompletedTask;
        }

        public Task StopAsync() => Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run failing orchestrator tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter PlaybackOrchestratorTests -v minimal
```

Expected: build fails because playback orchestration types do not exist.

- [ ] **Step 3: Implement playback abstractions**

Create `src/NextGenEmby.Core/Playback/PlaybackState.cs` with:

```csharp
namespace NextGenEmby.Core.Playback
{
    public enum PlaybackState
    {
        Stopped,
        Opening,
        Playing,
        Paused,
        Buffering,
        Failed
    }
}
```

Create `src/NextGenEmby.Core/Playback/PlaybackDescriptor.cs` with:

```csharp
using System.Collections.Generic;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Playback
{
    public sealed class PlaybackDescriptor
    {
        public string ItemId { get; set; } = "";
        public EmbyMediaSource MediaSource { get; set; } = new EmbyMediaSource();
        public IReadOnlyList<EmbyMediaSource> AvailableSources { get; set; } = new List<EmbyMediaSource>();
        public long StartPositionTicks { get; set; }
        public int? AudioStreamIndex { get; set; }
        public int? SubtitleStreamIndex { get; set; }
    }
}
```

Create `src/NextGenEmby.Core/Playback/IPlaybackBackend.cs` with:

```csharp
using System.Threading.Tasks;

namespace NextGenEmby.Core.Playback
{
    public interface IPlaybackBackend
    {
        long CurrentPositionTicks { get; }
        Task StartAsync(PlaybackDescriptor descriptor);
        Task PauseAsync();
        Task ResumeAsync();
        Task SeekAsync(long positionTicks);
        Task StopAsync();
    }
}
```

Create `src/NextGenEmby.Core/Playback/PlaybackOrchestrator.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Playback
{
    public sealed class PlaybackOrchestrator
    {
        private readonly IPlaybackBackend _backend;
        private string _itemId = "";
        private IReadOnlyList<EmbyMediaSource> _sources = Array.Empty<EmbyMediaSource>();

        public PlaybackOrchestrator(IPlaybackBackend backend)
        {
            _backend = backend;
        }

        public EmbyMediaSource? CurrentSource { get; private set; }

        public async Task StartAsync(string itemId, IReadOnlyList<EmbyMediaSource> sources, long resumeTicks)
        {
            if (sources.Count == 0)
            {
                throw new InvalidOperationException("Playback requires at least one media source.");
            }

            _itemId = itemId;
            _sources = sources;
            await StartSourceAsync(sources[0], resumeTicks).ConfigureAwait(false);
        }

        public async Task SwitchMediaSourceAsync(string mediaSourceId)
        {
            var source = _sources.Single(s => s.Id == mediaSourceId);
            var position = _backend.CurrentPositionTicks;
            await StartSourceAsync(source, position).ConfigureAwait(false);
        }

        private async Task StartSourceAsync(EmbyMediaSource source, long startTicks)
        {
            CurrentSource = source;
            await _backend.StartAsync(new PlaybackDescriptor
            {
                ItemId = _itemId,
                MediaSource = source,
                AvailableSources = _sources,
                StartPositionTicks = startTicks
            }).ConfigureAwait(false);
        }
    }
}
```

- [ ] **Step 4: Run orchestrator tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter PlaybackOrchestratorTests -v minimal
```

Expected: tests pass.

- [ ] **Step 5: Run all core tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src\NextGenEmby.Core tests\NextGenEmby.Core.Tests
git commit -m "feat: add playback orchestration boundary"
```

Expected: commit succeeds.

---

### Task 9: Add UWP App Shell

**Files:**
- Create: `src/NextGenEmby.App/NextGenEmby.App.csproj`
- Create: `src/NextGenEmby.App/Package.appxmanifest`
- Create: `src/NextGenEmby.App/App.xaml`
- Create: `src/NextGenEmby.App/App.xaml.cs`
- Create: `src/NextGenEmby.App/MainPage.xaml`
- Create: `src/NextGenEmby.App/MainPage.xaml.cs`
- Create: `src/NextGenEmby.App/Views/LoginPage.xaml`
- Create: `src/NextGenEmby.App/Views/LoginPage.xaml.cs`
- Create: `src/NextGenEmby.App/Views/HomePage.xaml`
- Create: `src/NextGenEmby.App/Views/HomePage.xaml.cs`

- [ ] **Step 1: Create UWP app directories**

Run:

```powershell
New-Item -ItemType Directory -Force src\NextGenEmby.App\Views src\NextGenEmby.App\Assets | Out-Null
```

- [ ] **Step 2: Create UWP project file**

Create `src/NextGenEmby.App/NextGenEmby.App.csproj` with:

```xml
<Project ToolsVersion="15.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">x64</Platform>
    <ProjectGuid>{2F85D12F-04BB-42E1-AE35-A9047E1E0111}</ProjectGuid>
    <OutputType>AppContainerExe</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>NextGenEmby.App</RootNamespace>
    <AssemblyName>NextGenEmby.App</AssemblyName>
    <DefaultLanguage>en-US</DefaultLanguage>
    <TargetPlatformIdentifier>UAP</TargetPlatformIdentifier>
    <TargetPlatformVersion>10.0.22621.0</TargetPlatformVersion>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <MinimumVisualStudioVersion>17</MinimumVisualStudioVersion>
    <FileAlignment>512</FileAlignment>
    <ProjectTypeGuids>{A5A43C5B-DE2A-4C0C-9213-0B5B7EC3F2D1};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
    <ApplicationType>Windows Store</ApplicationType>
    <ApplicationTypeRevision>10.0</ApplicationTypeRevision>
    <AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|x64'">
    <DebugSymbols>true</DebugSymbols>
    <OutputPath>bin\x64\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE;NETFX_CORE;WINDOWS_UWP</DefineConstants>
    <NoWarn>;2008</NoWarn>
    <DebugType>full</DebugType>
    <PlatformTarget>x64</PlatformTarget>
    <UseVSHostingProcess>false</UseVSHostingProcess>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="App.xaml.cs">
      <DependentUpon>App.xaml</DependentUpon>
    </Compile>
    <Compile Include="MainPage.xaml.cs">
      <DependentUpon>MainPage.xaml</DependentUpon>
    </Compile>
    <Compile Include="Views\LoginPage.xaml.cs">
      <DependentUpon>LoginPage.xaml</DependentUpon>
    </Compile>
    <Compile Include="Views\HomePage.xaml.cs">
      <DependentUpon>HomePage.xaml</DependentUpon>
    </Compile>
  </ItemGroup>
  <ItemGroup>
    <ApplicationDefinition Include="App.xaml" />
    <Page Include="MainPage.xaml" />
    <Page Include="Views\LoginPage.xaml" />
    <Page Include="Views\HomePage.xaml" />
  </ItemGroup>
  <ItemGroup>
    <AppxManifest Include="Package.appxmanifest">
      <SubType>Designer</SubType>
    </AppxManifest>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NETCore.UniversalWindowsPlatform" Version="6.2.15" />
    <PackageReference Include="Microsoft.UI.Xaml" Version="2.8.7" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\NextGenEmby.Core\NextGenEmby.Core.csproj">
      <Project>{3E3D8F22-1FD8-4A53-81D4-11998454C03B}</Project>
      <Name>NextGenEmby.Core</Name>
    </ProjectReference>
  </ItemGroup>
  <ItemGroup>
    <Content Include="Assets\StoreLogo.png" />
    <Content Include="Assets\Square44x44Logo.png" />
    <Content Include="Assets\Square150x150Logo.png" />
    <Content Include="Assets\Wide310x150Logo.png" />
    <Content Include="Assets\SplashScreen.png" />
  </ItemGroup>
  <Import Project="$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v17.0\Microsoft.Windows.UI.Xaml.CSharp.targets" />
</Project>
```

- [ ] **Step 3: Add the UWP project to the solution**

Run:

```powershell
dotnet sln NextGenXboxEmby.sln add src\NextGenEmby.App\NextGenEmby.App.csproj
```

Expected: project is added to the solution.

- [ ] **Step 4: Create package manifest**

Create `src/NextGenEmby.App/Package.appxmanifest` with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  IgnorableNamespaces="uap mp">
  <Identity Name="NextGenEmby.App" Publisher="CN=NextGenEmby" Version="0.1.0.0" />
  <mp:PhoneIdentity PhoneProductId="2f85d12f-04bb-42e1-ae35-a9047e1e0111" PhonePublisherId="00000000-0000-0000-0000-000000000000" />
  <Properties>
    <DisplayName>Next Gen Xbox Emby</DisplayName>
    <PublisherDisplayName>Personal</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Universal" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Applications>
    <Application Id="App" Executable="$targetnametoken$.exe" EntryPoint="NextGenEmby.App.App">
      <uap:VisualElements
        DisplayName="Next Gen Xbox Emby"
        Description="Xbox Emby player"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png"
        BackgroundColor="#101010">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
        <uap:SplashScreen Image="Assets\SplashScreen.png" BackgroundColor="#101010" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <Capability Name="internetClient" />
    <uap:Capability Name="videosLibrary" />
  </Capabilities>
</Package>
```

- [ ] **Step 5: Create app entry files**

Create `src/NextGenEmby.App/App.xaml` with:

```xml
<Application
    x:Class="NextGenEmby.App.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:muxc="using:Microsoft.UI.Xaml.Controls"
    RequestedTheme="Dark">
    <Application.Resources>
        <ResourceDictionary>
            <muxc:XamlControlsResources />
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

Create `src/NextGenEmby.App/App.xaml.cs` with:

```csharp
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(MainPage));
            }

            Window.Current.Activate();
        }
    }
}
```

- [ ] **Step 6: Create shell and pages**

Create `src/NextGenEmby.App/MainPage.xaml` with:

```xml
<Page
    x:Class="NextGenEmby.App.MainPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:muxc="using:Microsoft.UI.Xaml.Controls"
    Background="#101010">
    <Grid Padding="64">
        <muxc:NavigationView x:Name="ShellNav"
                             PaneDisplayMode="LeftMinimal"
                             IsBackButtonVisible="Collapsed"
                             IsSettingsVisible="False"
                             SelectionChanged="ShellNav_OnSelectionChanged">
            <muxc:NavigationView.MenuItems>
                <muxc:NavigationViewItem Content="Home" Tag="home" />
                <muxc:NavigationViewItem Content="Login" Tag="login" />
            </muxc:NavigationView.MenuItems>
            <Frame x:Name="ContentFrame" />
        </muxc:NavigationView>
    </Grid>
</Page>
```

Create `src/NextGenEmby.App/MainPage.xaml.cs` with:

```csharp
using Microsoft.UI.Xaml.Controls;
using NextGenEmby.App.Views;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            ContentFrame.Navigate(typeof(LoginPage));
        }

        private void ShellNav_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (!(args.SelectedItem is NavigationViewItem item))
            {
                return;
            }

            var tag = item.Tag?.ToString();
            ContentFrame.Navigate(tag == "home" ? typeof(HomePage) : typeof(LoginPage));
        }
    }
}
```

Create `src/NextGenEmby.App/Views/LoginPage.xaml` with:

```xml
<Page
    x:Class="NextGenEmby.App.Views.LoginPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="#101010">
    <Grid Padding="72" MaxWidth="900" HorizontalAlignment="Left">
        <StackPanel Spacing="20">
            <TextBlock Text="Next Gen Xbox Emby" FontSize="42" FontWeight="SemiBold" />
            <TextBox Header="Server URL" PlaceholderText="http://emby.local:8096" />
            <TextBox Header="Username" />
            <PasswordBox Header="Password or API token" />
            <Button Content="Connect" Width="220" />
        </StackPanel>
    </Grid>
</Page>
```

Create `src/NextGenEmby.App/Views/LoginPage.xaml.cs` with:

```csharp
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }
    }
}
```

Create `src/NextGenEmby.App/Views/HomePage.xaml` with:

```xml
<Page
    x:Class="NextGenEmby.App.Views.HomePage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="#101010">
    <Grid Padding="72">
        <StackPanel Spacing="28">
            <TextBlock Text="Home" FontSize="42" FontWeight="SemiBold" />
            <TextBlock Text="Continue Watching" FontSize="26" />
            <GridView SelectionMode="None" IsItemClickEnabled="True">
                <GridViewItem Width="220" Height="320" Background="#252525" Content="Movie" />
                <GridViewItem Width="220" Height="320" Background="#252525" Content="Series" />
            </GridView>
        </StackPanel>
    </Grid>
</Page>
```

Create `src/NextGenEmby.App/Views/HomePage.xaml.cs` with:

```csharp
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 7: Create package image assets**

Run:

```powershell
Add-Type -AssemblyName System.Drawing
function New-AppPng($Path, $Width, $Height) {
  $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $graphics.Clear([System.Drawing.Color]::FromArgb(16, 16, 16))
  $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 120, 212))
  $graphics.FillRectangle($brush, [int]($Width * 0.2), [int]($Height * 0.2), [int]($Width * 0.6), [int]($Height * 0.6))
  $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
  $brush.Dispose()
  $graphics.Dispose()
  $bitmap.Dispose()
}
New-AppPng 'src\NextGenEmby.App\Assets\StoreLogo.png' 50 50
New-AppPng 'src\NextGenEmby.App\Assets\Square44x44Logo.png' 44 44
New-AppPng 'src\NextGenEmby.App\Assets\Square150x150Logo.png' 150 150
New-AppPng 'src\NextGenEmby.App\Assets\Wide310x150Logo.png' 310 150
New-AppPng 'src\NextGenEmby.App\Assets\SplashScreen.png' 620 300
```

Expected: all five PNG files exist in `src/NextGenEmby.App/Assets`.

- [ ] **Step 8: Build the solution**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 9: Commit**

Run:

```powershell
git add src\NextGenEmby.App NextGenXboxEmby.sln
git commit -m "feat: add UWP Xbox shell"
```

Expected: commit succeeds.

---

### Task 10: Add Session Storage and Login View Model

**Files:**
- Create: `src/NextGenEmby.Core/Storage/ISessionStore.cs`
- Create: `src/NextGenEmby.App/Storage/ApplicationDataSessionStore.cs`
- Create: `src/NextGenEmby.App/ViewModels/LoginViewModel.cs`
- Modify: `src/NextGenEmby.App/Views/LoginPage.xaml`
- Modify: `src/NextGenEmby.App/Views/LoginPage.xaml.cs`

- [ ] **Step 1: Create session store contract**

Create `src/NextGenEmby.Core/Storage/ISessionStore.cs` with:

```csharp
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Storage
{
    public interface ISessionStore
    {
        Task<EmbySession?> LoadAsync();
        Task SaveAsync(EmbySession session);
        Task ClearAsync();
    }
}
```

- [ ] **Step 2: Create UWP session store**

Run:

```powershell
New-Item -ItemType Directory -Force src\NextGenEmby.App\Storage src\NextGenEmby.App\ViewModels | Out-Null
```

Create `src/NextGenEmby.App/Storage/ApplicationDataSessionStore.cs` with:

```csharp
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Storage;
using Windows.Storage;

namespace NextGenEmby.App.Storage
{
    public sealed class ApplicationDataSessionStore : ISessionStore
    {
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

        public Task<EmbySession?> LoadAsync()
        {
            if (!_settings.Values.TryGetValue("ServerUrl", out var serverUrl) ||
                !_settings.Values.TryGetValue("UserId", out var userId) ||
                !_settings.Values.TryGetValue("AccessToken", out var token))
            {
                return Task.FromResult<EmbySession?>(null);
            }

            return Task.FromResult<EmbySession?>(new EmbySession
            {
                ServerUrl = serverUrl == null ? "" : serverUrl.ToString(),
                UserId = userId == null ? "" : userId.ToString(),
                UserName = _settings.Values.TryGetValue("UserName", out var userName) && userName != null ? userName.ToString() : "",
                AccessToken = token == null ? "" : token.ToString()
            });
        }

        public Task SaveAsync(EmbySession session)
        {
            _settings.Values["ServerUrl"] = session.ServerUrl;
            _settings.Values["UserId"] = session.UserId;
            _settings.Values["UserName"] = session.UserName;
            _settings.Values["AccessToken"] = session.AccessToken;
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _settings.Values.Remove("ServerUrl");
            _settings.Values.Remove("UserId");
            _settings.Values.Remove("UserName");
            _settings.Values.Remove("AccessToken");
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 3: Create login view model**

Create `src/NextGenEmby.App/ViewModels/LoginViewModel.cs` with:

```csharp
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Storage;

namespace NextGenEmby.App.ViewModels
{
    public sealed class LoginViewModel : INotifyPropertyChanged
    {
        private readonly ISessionStore _store;
        private string _status = "";

        public LoginViewModel() : this(new ApplicationDataSessionStore())
        {
        }

        public LoginViewModel(ISessionStore store)
        {
            _store = store;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ServerUrl { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";

        public string Status
        {
            get => _status;
            private set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                using var http = new HttpClient();
                var client = new EmbyApiClient(http, new EmbyClientOptions
                {
                    ServerUrl = ServerUrl,
                    DeviceName = "Xbox",
                    DeviceId = "xbox-dev-mode",
                    ClientName = "Next Gen Xbox Emby",
                    ClientVersion = "0.1.0"
                });
                var session = await client.AuthenticateAsync(UserName, Password).ConfigureAwait(false);
                await _store.SaveAsync(session).ConfigureAwait(false);
                Status = $"Connected as {session.UserName}";
                return true;
            }
            catch (HttpRequestException)
            {
                Status = "Server unreachable or rejected the request.";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                Status = "Authentication failed.";
                return false;
            }
            catch (Exception ex)
            {
                Status = ex.Message;
                return false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
```

- [ ] **Step 4: Wire login page bindings**

Modify `src/NextGenEmby.App/Views/LoginPage.xaml` so the input controls have names and a status block:

```xml
<Page
    x:Class="NextGenEmby.App.Views.LoginPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="#101010">
    <Grid Padding="72" MaxWidth="900" HorizontalAlignment="Left">
        <StackPanel Spacing="20">
            <TextBlock Text="Next Gen Xbox Emby" FontSize="42" FontWeight="SemiBold" />
            <TextBox x:Name="ServerUrlBox" Header="Server URL" PlaceholderText="http://emby.local:8096" />
            <TextBox x:Name="UserNameBox" Header="Username" />
            <PasswordBox x:Name="PasswordBox" Header="Password or API token" />
            <Button Content="Connect" Width="220" Click="Connect_OnClick" />
            <TextBlock x:Name="StatusBlock" TextWrapping="Wrap" Foreground="#CCCCCC" />
        </StackPanel>
    </Grid>
</Page>
```

Modify `src/NextGenEmby.App/Views/LoginPage.xaml.cs`:

```csharp
using NextGenEmby.App.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class LoginPage : Page
    {
        private readonly LoginViewModel _viewModel = new LoginViewModel();

        public LoginPage()
        {
            InitializeComponent();
        }

        private async void Connect_OnClick(object sender, RoutedEventArgs e)
        {
            _viewModel.ServerUrl = ServerUrlBox.Text;
            _viewModel.UserName = UserNameBox.Text;
            _viewModel.Password = PasswordBox.Password;
            await _viewModel.ConnectAsync();
            StatusBlock.Text = _viewModel.Status;
        }
    }
}
```

- [ ] **Step 5: Include new files in UWP project**

Modify `src/NextGenEmby.App/NextGenEmby.App.csproj` by adding these entries to the `<ItemGroup>` containing compile files:

```xml
<Compile Include="Storage\ApplicationDataSessionStore.cs" />
<Compile Include="ViewModels\LoginViewModel.cs" />
```

- [ ] **Step 6: Build and test**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: tests pass and solution builds.

- [ ] **Step 7: Commit**

Run:

```powershell
git add src\NextGenEmby.Core src\NextGenEmby.App
git commit -m "feat: connect UWP login to Emby authentication"
```

Expected: commit succeeds.

---

### Task 11: Add System Playback Backend and Playback Page

**Files:**
- Create: `src/NextGenEmby.App/Playback/SystemMediaPlaybackBackend.cs`
- Create: `src/NextGenEmby.App/Views/PlaybackPage.xaml`
- Create: `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`
- Modify: `src/NextGenEmby.App/NextGenEmby.App.csproj`
- Modify: `src/NextGenEmby.App/MainPage.xaml`
- Modify: `src/NextGenEmby.App/MainPage.xaml.cs`

- [ ] **Step 1: Create system playback backend**

Run:

```powershell
New-Item -ItemType Directory -Force src\NextGenEmby.App\Playback | Out-Null
```

Create `src/NextGenEmby.App/Playback/SystemMediaPlaybackBackend.cs` with:

```csharp
using System;
using System.Threading.Tasks;
using NextGenEmby.Core.Playback;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Playback
{
    public sealed class SystemMediaPlaybackBackend : IPlaybackBackend
    {
        private readonly MediaPlayerElement _element;

        public SystemMediaPlaybackBackend(MediaPlayerElement element)
        {
            _element = element;
            _element.SetMediaPlayer(new MediaPlayer());
            _element.AreTransportControlsEnabled = false;
        }

        public long CurrentPositionTicks
        {
            get
            {
                var player = _element.MediaPlayer;
                if (player == null || player.PlaybackSession == null)
                {
                    return 0;
                }

                return player.PlaybackSession.Position.Ticks;
            }
        }

        public Task StartAsync(PlaybackDescriptor descriptor)
        {
            var player = _element.MediaPlayer;
            player.Source = MediaSource.CreateFromUri(new Uri(descriptor.MediaSource.DirectStreamUrl));
            player.Play();
            if (descriptor.StartPositionTicks > 0)
            {
                player.PlaybackSession.Position = TimeSpan.FromTicks(descriptor.StartPositionTicks);
            }

            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            _element.MediaPlayer.Pause();
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            _element.MediaPlayer.Play();
            return Task.CompletedTask;
        }

        public Task SeekAsync(long positionTicks)
        {
            _element.MediaPlayer.PlaybackSession.Position = TimeSpan.FromTicks(positionTicks);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _element.MediaPlayer.Pause();
            _element.MediaPlayer.Source = null;
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Create playback page**

Create `src/NextGenEmby.App/Views/PlaybackPage.xaml` with:

```xml
<Page
    x:Class="NextGenEmby.App.Views.PlaybackPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="Black">
    <Grid>
        <MediaPlayerElement x:Name="PlayerElement" Stretch="Uniform" />
        <Grid VerticalAlignment="Bottom" Background="#CC000000" Padding="48" Height="180">
            <StackPanel Spacing="12">
                <TextBlock x:Name="TitleBlock" Text="Playback" FontSize="28" />
                <StackPanel Orientation="Horizontal" Spacing="16">
                    <Button Content="Play" Click="Play_OnClick" />
                    <Button Content="Pause" Click="Pause_OnClick" />
                    <Button Content="Info" Click="Info_OnClick" />
                </StackPanel>
                <TextBlock x:Name="InfoBlock" Foreground="#CCCCCC" TextWrapping="Wrap" />
            </StackPanel>
        </Grid>
    </Grid>
</Page>
```

Create `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs` with:

```csharp
using NextGenEmby.App.Playback;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class PlaybackPage : Page
    {
        private readonly PlaybackOrchestrator _orchestrator;
        private readonly EmbyMediaSource _demoSource = new EmbyMediaSource
        {
            Id = "manual-url",
            Name = "Manual Direct Stream",
            DirectStreamUrl = "http://127.0.0.1:8096/Videos/demo/stream"
        };

        public PlaybackPage()
        {
            InitializeComponent();
            _orchestrator = new PlaybackOrchestrator(new SystemMediaPlaybackBackend(PlayerElement));
        }

        private async void Play_OnClick(object sender, RoutedEventArgs e)
        {
            await _orchestrator.StartAsync("demo", new[] { _demoSource }, 0);
            TitleBlock.Text = _demoSource.Name;
        }

        private async void Pause_OnClick(object sender, RoutedEventArgs e)
        {
            await _orchestrator.SwitchMediaSourceAsync(_demoSource.Id);
        }

        private void Info_OnClick(object sender, RoutedEventArgs e)
        {
            InfoBlock.Text = "System backend is active. Kodi-grade native backend replaces this interface in the native-core plan.";
        }
    }
}
```

- [ ] **Step 3: Fix pause behavior**

Modify `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs` so `Pause_OnClick` uses the backend directly:

```csharp
// Replace the fields.
private readonly SystemMediaPlaybackBackend _backend;
private readonly PlaybackOrchestrator _orchestrator;

// Replace the constructor body after InitializeComponent().
_backend = new SystemMediaPlaybackBackend(PlayerElement);
_orchestrator = new PlaybackOrchestrator(_backend);

// Replace Pause_OnClick.
private async void Pause_OnClick(object sender, RoutedEventArgs e)
{
    await _backend.PauseAsync();
}
```

- [ ] **Step 4: Add playback page to shell**

Modify `src/NextGenEmby.App/MainPage.xaml` by adding a menu item:

```xml
<muxc:NavigationViewItem Content="Playback" Tag="playback" />
```

Modify `src/NextGenEmby.App/MainPage.xaml.cs` selection routing:

```csharp
if (tag == "home")
{
    ContentFrame.Navigate(typeof(HomePage));
}
else if (tag == "playback")
{
    ContentFrame.Navigate(typeof(PlaybackPage));
}
else
{
    ContentFrame.Navigate(typeof(LoginPage));
}
```

- [ ] **Step 5: Include playback files in project**

Modify `src/NextGenEmby.App/NextGenEmby.App.csproj`:

```xml
<Compile Include="Playback\SystemMediaPlaybackBackend.cs" />
<Compile Include="Views\PlaybackPage.xaml.cs">
  <DependentUpon>PlaybackPage.xaml</DependentUpon>
</Compile>
```

Add page entry:

```xml
<Page Include="Views\PlaybackPage.xaml" />
```

- [ ] **Step 6: Build and test**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: tests pass and solution builds.

- [ ] **Step 7: Commit**

Run:

```powershell
git add src\NextGenEmby.App
git commit -m "feat: add playback backend boundary to UWP app"
```

Expected: commit succeeds.

---

### Task 12: Add Kodi HDR Research ADR

**Files:**
- Create: `docs/adr/0001-kodi-xbox-hdr-path.md`

- [ ] **Step 1: Clone Kodi for local source inspection**

Run:

```powershell
New-Item -ItemType Directory -Force .research | Out-Null
git clone --depth 1 https://github.com/xbmc/xbmc .research\kodi-xbox
```

Expected: `.research/kodi-xbox` exists and is ignored by git.

- [ ] **Step 2: Search Kodi HDR and Xbox APIs**

Run:

```powershell
rg -n "HdmiDisplayInformation|HdmiDisplayHdrOption|Eotf2084|HDR10|DXGI_COLOR_SPACE|SetColorSpace" .research\kodi-xbox
```

Expected: matches in Kodi Windows/Xbox rendering or display-management files.

- [ ] **Step 3: Write the ADR**

Run:

```powershell
New-Item -ItemType Directory -Force docs\adr | Out-Null
```

Create `docs/adr/0001-kodi-xbox-hdr-path.md` with:

```markdown
# ADR 0001: Kodi Xbox HDR Path for Native Playback Core

Date: 2026-07-05

## Status

Accepted for native-core research.

## Context

The app targets Xbox-only Emby playback. The first foundation slice uses a system-player backend only to verify UI, Emby API, and orchestration boundaries. The approved product direction requires a Kodi-grade C++/WinRT + DirectX backend for 4K HEVC HDR10 playback.

Kodi 21.3 added Xbox HDR support. The Kodi source path must be studied before writing the native playback implementation.

## Decision

The native playback core plan will use Kodi Xbox/UWP as the primary reference for:

- Detecting HDR display capability.
- Entering HDR10 output.
- Restoring SDR output after playback.
- Configuring DirectX/DXGI color spaces.
- Handling SDR UI over HDR video.
- Reporting display and HDR failures back to the C# playback orchestrator.

The foundation app keeps `IPlaybackBackend` stable so `SystemMediaPlaybackBackend` can be replaced by `NativeDirectXPlaybackBackend` without changing Emby API parsing or Xbox page navigation.

## Kodi Symbols to Track

The local research checkout must be searched for:

- `HdmiDisplayInformation`
- `HdmiDisplayHdrOption`
- `Eotf2084`
- `DXGI_COLOR_SPACE`
- `SetColorSpace`
- `HDR10`

## Native-Core Output Required by the Next Plan

The next plan must create:

- A C++/WinRT UWP component project.
- A C# projection or adapter named `NativeDirectXPlaybackBackend`.
- A playback descriptor bridge from `PlaybackDescriptor`.
- HDR capability detection.
- HDR entry and exit methods.
- Display state restoration on stop, failure, suspend, and resume.
- A fixture-based test matrix for 1080p H.264 SDR, 4K HEVC SDR, and 4K HEVC HDR10.

## References

- Kodi repository: https://github.com/xbmc/xbmc
- Kodi Xbox HDR10 passthrough PR: https://github.com/xbmc/xbmc/pull/24083
- Kodi 21.3 release notes: https://kodi.tv/article/kodi-21-3-omega-release/
```

- [ ] **Step 4: Verify ADR and ignore research checkout**

Run:

```powershell
git status --short
```

Expected:

```text
new file: docs/adr/0001-kodi-xbox-hdr-path.md
```

The `.research/` directory must not appear because `.gitignore` ignores it.

- [ ] **Step 5: Commit**

Run:

```powershell
git add docs\adr\0001-kodi-xbox-hdr-path.md
git commit -m "docs: record Kodi HDR research path"
```

Expected: commit succeeds.

---

### Task 13: Final Foundation Verification

**Files:**
- Modify: none

- [ ] **Step 1: Run all unit tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected:

```text
Passed!
```

- [ ] **Step 2: Build the full solution**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected:

```text
Build succeeded.
```

- [ ] **Step 3: Launch in Visual Studio for local smoke test**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe' NextGenXboxEmby.sln
```

Expected manual check:

- `NextGenEmby.App` is the startup project.
- Target is `x64`.
- The app launches on Local Machine.
- Login page renders in dark mode.
- Navigation moves between Login, Home, and Playback.
- The Playback page shows a black video surface and bottom overlay.
- Focus is visible using keyboard/gamepad navigation.

- [ ] **Step 4: Record the foundation status**

Create `docs/foundation-status.md` with:

```markdown
# Foundation Status

Date: 2026-07-05

## Verified

- Visual Studio 2022 and MSBuild are available.
- UWP app builds for x64 Debug.
- Core unit tests pass.
- Emby authentication, library query, PlaybackInfo parsing, and progress reporting are covered by tests.
- Playback orchestration has a stable backend interface.
- UWP shell launches with Login, Home, and Playback pages.
- Kodi HDR research path is recorded in ADR 0001.

## Next Plan

Create the native playback core plan for C++/WinRT, DirectX rendering, HEVC/HDR10 playback, subtitle rendering, audio track switching, and Kodi-derived HDR state management.
```

- [ ] **Step 5: Commit final status**

Run:

```powershell
git add docs\foundation-status.md
git commit -m "docs: record foundation verification status"
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

Spec coverage in this foundation plan:

- Single-server login: Task 4 and Task 10.
- Movie/TV media browsing foundation: Task 5 and Task 9.
- PlaybackInfo parsing for versions, audio, and subtitles: Task 6.
- Progress reporting: Task 7.
- Playback orchestration boundary: Task 8.
- Xbox UWP shell: Task 9 through Task 11.
- Kodi HDR direction: Task 12.
- Build and local launch verification: Task 13.

Spec requirements reserved for the native-core plan:

- C++/WinRT DirectX backend.
- HEVC Main10 decoding path.
- HDR10 passthrough.
- DXGI color space handling.
- Display state restoration implementation.
- Native subtitle rendering.
- Audio track switching inside the native backend.
- Media version switching against live Emby streams on Xbox hardware.
