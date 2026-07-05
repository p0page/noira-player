# Kodi HDR/DV Fallback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Kodi-inspired HDR10/PQ color handling and Dolby Vision fallback classification for the Xbox native Emby player.

**Architecture:** Add a tested C# HDR classification and source-selection layer first, then pass complete frame color metadata through the native decoder into a Kodi-style DXGI video processor path. Dolby Vision is never output as DV; compatible DV sources are classified as HDR10/HLG fallback and pure DV is blocked from native direct play.

**Tech Stack:** C#/.NET tests with xUnit, UWP C++/WinRT native component, FFmpeg metadata, D3D11/DXGI video processor, Xbox `SwapChainPanel`.

---

## Required Reading

- `docs/superpowers/specs/2026-07-05-kodi-hdr-dv-fallback-design.md`
- `docs/kodi-color-pipeline-comparison.md`
- Kodi reference files:
  - `xbmc/cores/VideoPlayer/VideoRenderers/HwDecRender/DXVAHD.cpp`
  - `xbmc/cores/VideoPlayer/VideoRenderers/HwDecRender/DXVAEnumeratorHD.cpp`
  - `xbmc/rendering/dx/DeviceResources.cpp`
  - `xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp`
  - `xbmc/utils/BitstreamConverter.cpp`

## File Structure

- Create: `src/NextGenEmby.Core/Playback/HdrPlaybackKind.cs`
  Defines the canonical HDR/DV fallback categories used by source selection and UI.
- Create: `src/NextGenEmby.Core/Playback/HdrPlaybackProfile.cs`
  Stores parsed HDR details, DV details, source selection rank, and user-facing diagnostics.
- Create: `src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs`
  Classifies Emby media stream metadata without needing a server.
- Modify: `src/NextGenEmby.Core/Emby/EmbyMediaSource.cs`
  Adds `HdrProfile` and keeps `IsHdr` as a compatibility property.
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
  Maps Emby stream metadata into `HdrPlaybackProfile`.
- Modify: `src/NextGenEmby.Core/Playback/PlaybackOrchestrator.cs`
  Uses profile rank instead of `!IsHdr` only.
- Create: `tests/NextGenEmby.Core.Tests/Playback/HdrPlaybackProfileClassifierTests.cs`
  Tests HDR10, HLG, DV P8 fallback, DV P5 unsupported, and selection rank.
- Modify: `tests/NextGenEmby.Core.Tests/Emby/EmbyPlaybackInfoTests.cs`
  Tests JSON mapping from Emby playback info.
- Modify: `src/NextGenEmby.Native/Media/VideoDecoder.h`
  Adds complete color metadata to `DecodedVideoFrame`.
- Modify: `src/NextGenEmby.Native/Media/VideoDecoder.cpp`
  Copies FFmpeg AV color metadata and DOVI side-data indicators into frame metadata.
- Create: `src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.h`
  Defines `VideoColorMetadata`, `DxgiColorSpaceMapping`, and mapping API.
- Create: `src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.cpp`
  Implements Kodi-style AV-to-DXGI color-space mapping.
- Modify: `src/NextGenEmby.Native/DxDeviceResources.h`
  Accepts full color metadata and exposes swapchain/color diagnostics.
- Modify: `src/NextGenEmby.Native/DxDeviceResources.cpp`
  Uses `ID3D11VideoContext1`, validates conversions, and prefers 10-bit swapchain.
- Modify: `src/NextGenEmby.Native/Media/VideoRenderer.cpp`
  Passes full frame color metadata and gates HDR output on validated state.
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.idl`
  Extends native playback status diagnostics.
- Modify: `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`
  Maps native diagnostics to C#.
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`
  Shows HDR/DV fallback strategy in diagnostics text.
- Modify: `docs/native-playback-smoke-tests.md`
  Adds HDR10/DV fallback verification matrix.

---

### Task 1: Add Canonical HDR/DV Classification in Core

**Files:**
- Create: `src/NextGenEmby.Core/Playback/HdrPlaybackKind.cs`
- Create: `src/NextGenEmby.Core/Playback/HdrPlaybackProfile.cs`
- Create: `src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs`
- Test: `tests/NextGenEmby.Core.Tests/Playback/HdrPlaybackProfileClassifierTests.cs`

- [ ] **Step 1: Write failing classifier tests**

Create `tests/NextGenEmby.Core.Tests/Playback/HdrPlaybackProfileClassifierTests.cs`:

```csharp
using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class HdrPlaybackProfileClassifierTests
{
    [Fact]
    public void Classify_Returns_Hdr10_For_Pq_Bt2020()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(new HdrPlaybackMetadata
        {
            VideoRange = "HDR10",
            ColorPrimaries = "bt2020",
            ColorTransfer = "smpte2084",
            ColorSpace = "bt2020nc"
        });

        Assert.Equal(HdrPlaybackKind.Hdr10, profile.Kind);
        Assert.True(profile.IsHdr);
        Assert.True(profile.CanNativeDirectPlay);
        Assert.Equal(2, profile.SelectionRank);
    }

    [Fact]
    public void Classify_Returns_Hlg_For_Hlg_Transfer()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(new HdrPlaybackMetadata
        {
            ColorPrimaries = "bt2020",
            ColorTransfer = "arib-std-b67",
            ColorSpace = "bt2020nc"
        });

        Assert.Equal(HdrPlaybackKind.Hlg, profile.Kind);
        Assert.Equal("HLG", profile.OriginalHdrType);
    }

    [Fact]
    public void Classify_Returns_Dv_Hdr10_Fallback_For_Profile_8_Compatibility_1()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(new HdrPlaybackMetadata
        {
            VideoRange = "Dolby Vision",
            DisplayTitle = "4K HEVC Dolby Vision Profile 8.1 HDR10",
            DolbyVisionProfile = 8,
            DolbyVisionCompatibilityId = 1,
            ColorTransfer = "smpte2084",
            ColorPrimaries = "bt2020"
        });

        Assert.Equal(HdrPlaybackKind.DolbyVisionWithHdr10Fallback, profile.Kind);
        Assert.True(profile.IsDolbyVision);
        Assert.True(profile.CanNativeDirectPlay);
        Assert.Equal("HDR10 fallback from Dolby Vision", profile.PlaybackStrategy);
    }

    [Fact]
    public void Classify_Returns_Dv_Hlg_Fallback_For_Profile_8_Compatibility_4()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(new HdrPlaybackMetadata
        {
            VideoRange = "DV",
            DolbyVisionProfile = 8,
            DolbyVisionCompatibilityId = 4,
            ColorTransfer = "arib-std-b67",
            ColorPrimaries = "bt2020"
        });

        Assert.Equal(HdrPlaybackKind.DolbyVisionWithHlgFallback, profile.Kind);
        Assert.Equal("HLG fallback from Dolby Vision", profile.PlaybackStrategy);
    }

    [Fact]
    public void Classify_Returns_Unsupported_For_Profile_5()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(new HdrPlaybackMetadata
        {
            VideoRange = "Dolby Vision",
            DolbyVisionProfile = 5,
            ColorTransfer = "smpte2084"
        });

        Assert.Equal(HdrPlaybackKind.DolbyVisionUnsupported, profile.Kind);
        Assert.False(profile.CanNativeDirectPlay);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter HdrPlaybackProfileClassifierTests -v minimal
```

Expected: compile fails because `HdrPlaybackKind`, `HdrPlaybackProfileClassifier`, and related types do not exist.

- [ ] **Step 3: Add classification types**

Create `src/NextGenEmby.Core/Playback/HdrPlaybackKind.cs`:

```csharp
namespace NextGenEmby.Core.Playback
{
    public enum HdrPlaybackKind
    {
        Sdr = 0,
        Hdr10 = 1,
        Hlg = 2,
        DolbyVisionWithHdr10Fallback = 3,
        DolbyVisionWithHlgFallback = 4,
        DolbyVisionUnsupported = 5,
        UnknownHdr = 6
    }
}
```

Create `src/NextGenEmby.Core/Playback/HdrPlaybackProfile.cs`:

```csharp
namespace NextGenEmby.Core.Playback
{
    public sealed class HdrPlaybackProfile
    {
        public HdrPlaybackKind Kind { get; set; } = HdrPlaybackKind.Sdr;
        public string OriginalHdrType { get; set; } = "SDR";
        public string PlaybackStrategy { get; set; } = "Native SDR";
        public int? DolbyVisionProfile { get; set; }
        public int? DolbyVisionCompatibilityId { get; set; }
        public bool HasDolbyVisionRpu { get; set; }
        public bool HasDolbyVisionEnhancementLayer { get; set; }

        public bool IsHdr =>
            Kind != HdrPlaybackKind.Sdr &&
            Kind != HdrPlaybackKind.DolbyVisionUnsupported;

        public bool IsDolbyVision =>
            Kind == HdrPlaybackKind.DolbyVisionWithHdr10Fallback ||
            Kind == HdrPlaybackKind.DolbyVisionWithHlgFallback ||
            Kind == HdrPlaybackKind.DolbyVisionUnsupported;

        public bool CanNativeDirectPlay => Kind != HdrPlaybackKind.DolbyVisionUnsupported;

        public int SelectionRank => Kind switch
        {
            HdrPlaybackKind.Sdr => 1,
            HdrPlaybackKind.Hdr10 => 2,
            HdrPlaybackKind.Hlg => 3,
            HdrPlaybackKind.DolbyVisionWithHdr10Fallback => 4,
            HdrPlaybackKind.DolbyVisionWithHlgFallback => 5,
            HdrPlaybackKind.UnknownHdr => 6,
            HdrPlaybackKind.DolbyVisionUnsupported => 100,
            _ => 100
        };
    }

    public sealed class HdrPlaybackMetadata
    {
        public string VideoRange { get; set; } = "";
        public string DisplayTitle { get; set; } = "";
        public string ColorPrimaries { get; set; } = "";
        public string ColorTransfer { get; set; } = "";
        public string ColorSpace { get; set; } = "";
        public int? DolbyVisionProfile { get; set; }
        public int? DolbyVisionCompatibilityId { get; set; }
        public bool HasDolbyVisionRpu { get; set; }
        public bool HasDolbyVisionEnhancementLayer { get; set; }
    }
}
```

- [ ] **Step 4: Add classifier**

Create `src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs`:

```csharp
using System;
using System.Text.RegularExpressions;

namespace NextGenEmby.Core.Playback
{
    public static class HdrPlaybackProfileClassifier
    {
        public static HdrPlaybackProfile Classify(HdrPlaybackMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            var profile = new HdrPlaybackProfile
            {
                DolbyVisionProfile = metadata.DolbyVisionProfile ?? TryFindProfile(metadata),
                DolbyVisionCompatibilityId = metadata.DolbyVisionCompatibilityId ?? TryFindCompatibilityId(metadata),
                HasDolbyVisionRpu = metadata.HasDolbyVisionRpu,
                HasDolbyVisionEnhancementLayer = metadata.HasDolbyVisionEnhancementLayer
            };

            if (LooksLikeDolbyVision(metadata))
            {
                return ClassifyDolbyVision(metadata, profile);
            }

            if (Contains(metadata.ColorTransfer, "arib-std-b67") || Contains(metadata.ColorTransfer, "hlg"))
            {
                profile.Kind = HdrPlaybackKind.Hlg;
                profile.OriginalHdrType = "HLG";
                profile.PlaybackStrategy = "Native HLG";
                return profile;
            }

            if (Contains(metadata.VideoRange, "hdr") ||
                Contains(metadata.ColorTransfer, "smpte2084") ||
                (Contains(metadata.ColorPrimaries, "bt2020") && Contains(metadata.ColorSpace, "bt2020")))
            {
                profile.Kind = HdrPlaybackKind.Hdr10;
                profile.OriginalHdrType = "HDR10";
                profile.PlaybackStrategy = "Native HDR10";
                return profile;
            }

            profile.Kind = HdrPlaybackKind.Sdr;
            profile.OriginalHdrType = "SDR";
            profile.PlaybackStrategy = "Native SDR";
            return profile;
        }

        private static HdrPlaybackProfile ClassifyDolbyVision(
            HdrPlaybackMetadata metadata,
            HdrPlaybackProfile profile)
        {
            profile.OriginalHdrType = profile.DolbyVisionProfile.HasValue
                ? $"Dolby Vision P{profile.DolbyVisionProfile.Value}"
                : "Dolby Vision";

            if (profile.DolbyVisionProfile == 5)
            {
                profile.Kind = HdrPlaybackKind.DolbyVisionUnsupported;
                profile.PlaybackStrategy = "Unsupported pure Dolby Vision";
                return profile;
            }

            if (profile.DolbyVisionCompatibilityId == 4 ||
                Contains(metadata.ColorTransfer, "arib-std-b67") ||
                Contains(metadata.ColorTransfer, "hlg"))
            {
                profile.Kind = HdrPlaybackKind.DolbyVisionWithHlgFallback;
                profile.PlaybackStrategy = "HLG fallback from Dolby Vision";
                return profile;
            }

            if (profile.DolbyVisionCompatibilityId == 1 ||
                profile.DolbyVisionProfile == 7 ||
                profile.DolbyVisionProfile == 8 ||
                Contains(metadata.ColorTransfer, "smpte2084") ||
                Contains(metadata.DisplayTitle, "hdr10"))
            {
                profile.Kind = HdrPlaybackKind.DolbyVisionWithHdr10Fallback;
                profile.PlaybackStrategy = "HDR10 fallback from Dolby Vision";
                return profile;
            }

            profile.Kind = HdrPlaybackKind.DolbyVisionUnsupported;
            profile.PlaybackStrategy = "Unsupported pure Dolby Vision";
            return profile;
        }

        private static bool LooksLikeDolbyVision(HdrPlaybackMetadata metadata) =>
            metadata.DolbyVisionProfile.HasValue ||
            Contains(metadata.VideoRange, "dolby") ||
            Contains(metadata.VideoRange, "dovi") ||
            Contains(metadata.VideoRange, "dv") ||
            Contains(metadata.DisplayTitle, "dolby vision") ||
            Contains(metadata.DisplayTitle, "dovi");

        private static int? TryFindProfile(HdrPlaybackMetadata metadata)
        {
            var match = Regex.Match(
                $"{metadata.VideoRange} {metadata.DisplayTitle}",
                @"(?:profile|p)\s*(?<profile>[578])(?:[.\s]|$)",
                RegexOptions.IgnoreCase);
            return match.Success ? int.Parse(match.Groups["profile"].Value) : null;
        }

        private static int? TryFindCompatibilityId(HdrPlaybackMetadata metadata)
        {
            var match = Regex.Match(
                $"{metadata.VideoRange} {metadata.DisplayTitle}",
                @"(?:p8|profile\s*8|dvhe\.08|dvh1\.08)[^\d]*(?<compat>[14])",
                RegexOptions.IgnoreCase);
            return match.Success ? int.Parse(match.Groups["compat"].Value) : null;
        }

        private static bool Contains(string value, string fragment) =>
            value?.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter HdrPlaybackProfileClassifierTests -v minimal
```

Expected: all `HdrPlaybackProfileClassifierTests` pass.

- [ ] **Step 6: Commit**

```powershell
git add src\NextGenEmby.Core\Playback\HdrPlaybackKind.cs src\NextGenEmby.Core\Playback\HdrPlaybackProfile.cs src\NextGenEmby.Core\Playback\HdrPlaybackProfileClassifier.cs tests\NextGenEmby.Core.Tests\Playback\HdrPlaybackProfileClassifierTests.cs
git commit -m "feat: classify HDR and Dolby Vision fallback"
```

---

### Task 2: Map Emby Metadata and Rank Media Sources

**Files:**
- Modify: `src/NextGenEmby.Core/Emby/EmbyMediaSource.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Modify: `src/NextGenEmby.Core/Playback/PlaybackOrchestrator.cs`
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyPlaybackInfoTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Playback/PlaybackOrchestratorTests.cs`

- [ ] **Step 1: Write failing Emby mapping test**

Add to `tests/NextGenEmby.Core.Tests/Emby/EmbyPlaybackInfoTests.cs`:

```csharp
[Fact]
public async Task GetPlaybackInfoAsync_Maps_DolbyVision_Profile8_As_Hdr10_Fallback()
{
    var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
        HttpStatusCode.OK,
        """
        {
          "MediaSources": [
            {
              "Id": "dv-p8",
              "Name": "4K DV",
              "MediaStreams": [
                {
                  "Index": 0,
                  "Type": "Video",
                  "Codec": "hevc",
                  "Width": 3840,
                  "Height": 2160,
                  "VideoRange": "Dolby Vision",
                  "DisplayTitle": "4K HEVC Dolby Vision Profile 8.1 HDR10",
                  "ColorPrimaries": "bt2020",
                  "ColorTransfer": "smpte2084",
                  "ColorSpace": "bt2020nc",
                  "DvProfile": 8,
                  "DvBlSignalCompatibilityId": 1
                }
              ]
            }
          ]
        }
        """));
    using var http = new HttpClient(handler);
    var client = CreateClient(http);

    var source = Assert.Single(await client.GetPlaybackInfoAsync(Session(), "movie-1"));

    Assert.Equal(HdrPlaybackKind.DolbyVisionWithHdr10Fallback, source.HdrProfile.Kind);
    Assert.True(source.IsHdr);
    Assert.Equal("HDR10 fallback from Dolby Vision", source.HdrProfile.PlaybackStrategy);
}
```

- [ ] **Step 2: Write failing source selection tests**

Add to `tests/NextGenEmby.Core.Tests/Playback/PlaybackOrchestratorTests.cs`:

```csharp
[Fact]
public async Task StartAsync_Prefers_Hdr10_Over_DolbyVision_Fallback()
{
    var hdr10 = Source("hdr10");
    hdr10.HdrProfile.Kind = HdrPlaybackKind.Hdr10;
    var dvFallback = Source("dv");
    dvFallback.HdrProfile.Kind = HdrPlaybackKind.DolbyVisionWithHdr10Fallback;
    var backend = new RecordingPlaybackBackend();
    var orchestrator = new PlaybackOrchestrator(backend);

    await orchestrator.StartAsync("item-1", new[] { dvFallback, hdr10 }, 0);

    Assert.Same(hdr10, orchestrator.CurrentMediaSource);
}

[Fact]
public async Task StartAsync_Does_Not_Select_Pure_DolbyVision_When_Alternative_Exists()
{
    var pureDv = Source("dv-p5");
    pureDv.HdrProfile.Kind = HdrPlaybackKind.DolbyVisionUnsupported;
    var sdr = Source("sdr");
    var backend = new RecordingPlaybackBackend();
    var orchestrator = new PlaybackOrchestrator(backend);

    await orchestrator.StartAsync("item-1", new[] { pureDv, sdr }, 0);

    Assert.Same(sdr, orchestrator.CurrentMediaSource);
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "FullyQualifiedName~EmbyPlaybackInfoTests|FullyQualifiedName~PlaybackOrchestratorTests" -v minimal
```

Expected: compile fails because `EmbyMediaSource.HdrProfile` and DTO DV fields do not exist.

- [ ] **Step 4: Add `HdrProfile` to `EmbyMediaSource`**

Modify `src/NextGenEmby.Core/Emby/EmbyMediaSource.cs`:

```csharp
using NextGenEmby.Core.Playback;
```

Inside `EmbyMediaSource`:

```csharp
public HdrPlaybackProfile HdrProfile { get; } = new HdrPlaybackProfile();

public bool IsHdr
{
    get => HdrProfile.IsHdr;
    set
    {
        HdrProfile.Kind = value ? HdrPlaybackKind.Hdr10 : HdrPlaybackKind.Sdr;
        HdrProfile.OriginalHdrType = value ? "HDR10" : "SDR";
        HdrProfile.PlaybackStrategy = value ? "Native HDR10" : "Native SDR";
    }
}
```

- [ ] **Step 5: Map Emby video metadata through classifier**

Modify `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`.

Add DTO fields to `MediaStreamDto`:

```csharp
public string Profile { get; set; } = "";
public int? DvProfile { get; set; }
public int? DvLevel { get; set; }
public int? DvBlSignalCompatibilityId { get; set; }
public bool RpuPresentFlag { get; set; }
public bool ElPresentFlag { get; set; }
```

Replace `result.IsHdr = IsHdrVideoStream(stream);` with:

```csharp
var profile = HdrPlaybackProfileClassifier.Classify(new HdrPlaybackMetadata
{
    VideoRange = stream.VideoRange ?? "",
    DisplayTitle = stream.DisplayTitle ?? "",
    ColorPrimaries = stream.ColorPrimaries ?? "",
    ColorTransfer = stream.ColorTransfer ?? "",
    ColorSpace = stream.ColorSpace ?? "",
    DolbyVisionProfile = stream.DvProfile,
    DolbyVisionCompatibilityId = stream.DvBlSignalCompatibilityId,
    HasDolbyVisionRpu = stream.RpuPresentFlag,
    HasDolbyVisionEnhancementLayer = stream.ElPresentFlag
});
result.HdrProfile.Kind = profile.Kind;
result.HdrProfile.OriginalHdrType = profile.OriginalHdrType;
result.HdrProfile.PlaybackStrategy = profile.PlaybackStrategy;
result.HdrProfile.DolbyVisionProfile = profile.DolbyVisionProfile;
result.HdrProfile.DolbyVisionCompatibilityId = profile.DolbyVisionCompatibilityId;
result.HdrProfile.HasDolbyVisionRpu = profile.HasDolbyVisionRpu;
result.HdrProfile.HasDolbyVisionEnhancementLayer = profile.HasDolbyVisionEnhancementLayer;
```

Keep `IsHdrVideoStream()` only if older tests still call it indirectly; otherwise remove it after tests pass.

- [ ] **Step 6: Rank media sources by profile**

Modify `src/NextGenEmby.Core/Playback/PlaybackOrchestrator.cs`:

```csharp
private static EmbyMediaSource SelectInitialMediaSource(IReadOnlyList<EmbyMediaSource> sources)
{
    return sources
        .Where(source => source.HdrProfile.CanNativeDirectPlay)
        .OrderBy(source => source.HdrProfile.SelectionRank)
        .ThenByDescending(source => source.Height)
        .ThenByDescending(source => source.Bitrate)
        .FirstOrDefault()
        ?? throw new InvalidOperationException("No native direct-play media source is available.");
}
```

- [ ] **Step 7: Run tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all core tests pass.

- [ ] **Step 8: Commit**

```powershell
git add src\NextGenEmby.Core\Emby\EmbyMediaSource.cs src\NextGenEmby.Core\Emby\EmbyApiClient.cs src\NextGenEmby.Core\Playback\PlaybackOrchestrator.cs tests\NextGenEmby.Core.Tests\Emby\EmbyPlaybackInfoTests.cs tests\NextGenEmby.Core.Tests\Playback\PlaybackOrchestratorTests.cs
git commit -m "feat: rank HDR and DV fallback media sources"
```

---

### Task 3: Carry Full FFmpeg Color Metadata Through Native Frames

**Files:**
- Modify: `src/NextGenEmby.Native/Media/VideoDecoder.h`
- Modify: `src/NextGenEmby.Native/Media/VideoDecoder.cpp`
- Modify: `src/NextGenEmby.Native/Media/VideoRenderer.cpp`

- [ ] **Step 1: Add native frame metadata fields**

Modify `DecodedVideoFrame` in `src/NextGenEmby.Native/Media/VideoDecoder.h`:

```cpp
int ColorPrimaries{2};      // AVCOL_PRI_UNSPECIFIED
int ColorSpace{2};          // AVCOL_SPC_UNSPECIFIED
int ColorTransfer{2};       // AVCOL_TRC_UNSPECIFIED
int ColorRange{0};          // AVCOL_RANGE_UNSPECIFIED
int ChromaLocation{0};      // AVCHROMA_LOC_UNSPECIFIED
bool HasDolbyVisionMetadata{false};
int DolbyVisionProfile{0};
int DolbyVisionCompatibilityId{0};
```

- [ ] **Step 2: Copy FFmpeg color metadata**

Modify `CreateDecodedVideoFrame()` in `src/NextGenEmby.Native/Media/VideoDecoder.cpp`:

```cpp
decodedFrame.ColorPrimaries = static_cast<int>(frame->color_primaries);
decodedFrame.ColorSpace = static_cast<int>(frame->colorspace);
decodedFrame.ColorTransfer = static_cast<int>(frame->color_trc);
decodedFrame.ColorRange = static_cast<int>(frame->color_range);
decodedFrame.ChromaLocation = static_cast<int>(frame->chroma_location);
```

Add DOVI side-data detection near HDR metadata extraction:

```cpp
auto doviSideData = av_frame_get_side_data(frame, AV_FRAME_DATA_DOVI_METADATA);
if (doviSideData != nullptr && doviSideData->data != nullptr)
{
    decodedFrame.HasDolbyVisionMetadata = true;
}
```

If the bundled FFmpeg headers do not define `AV_FRAME_DATA_DOVI_METADATA`, guard it:

```cpp
#ifdef AV_FRAME_DATA_DOVI_METADATA
// detection block
#endif
```

- [ ] **Step 3: Stop treating BT.2020 as BT.709 for hardware path**

Keep `MapSwsColorSpace()` for BGRA fallback, but stop using `ShouldUseBt709Matrix()` as the only hardware color signal. Leave `UsesBt709Matrix` populated for legacy fallback only.

- [ ] **Step 4: Build native project**

Run:

```powershell
& "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" NextGenXboxEmby.sln /p:Configuration=Debug /p:Platform=x64 /m
```

Expected: native project builds. If Visual Studio is installed in a different edition path, use the existing successful MSBuild path from this machine.

- [ ] **Step 5: Commit**

```powershell
git add src\NextGenEmby.Native\Media\VideoDecoder.h src\NextGenEmby.Native\Media\VideoDecoder.cpp src\NextGenEmby.Native\Media\VideoRenderer.cpp
git commit -m "feat: carry native video color metadata"
```

---

### Task 4: Add Kodi-Style DXGI Color Space Mapping and Conversion Validation

**Files:**
- Create: `src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.h`
- Create: `src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.cpp`
- Modify: `src/NextGenEmby.Native/NextGenEmby.Native.vcxproj`
- Modify: `src/NextGenEmby.Native/NextGenEmby.Native.vcxproj.filters`
- Modify: `src/NextGenEmby.Native/DxDeviceResources.h`
- Modify: `src/NextGenEmby.Native/DxDeviceResources.cpp`
- Modify: `src/NextGenEmby.Native/Media/VideoRenderer.cpp`

- [ ] **Step 1: Add mapper header**

Create `src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.h`:

```cpp
#pragma once

#include <dxgicommon.h>

namespace winrt::NextGenEmby::Native::implementation
{
    struct VideoColorMetadata
    {
        int ColorPrimaries{2};
        int ColorSpace{2};
        int ColorTransfer{2};
        int ColorRange{0};
        int ChromaLocation{0};
    };

    struct DxgiColorSpaceMapping
    {
        bool IsSupported{false};
        DXGI_COLOR_SPACE_TYPE InputColorSpace{DXGI_COLOR_SPACE_CUSTOM};
        DXGI_COLOR_SPACE_TYPE OutputColorSpace{DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709};
        bool IsHdrOutput{false};
    };

    DxgiColorSpaceMapping MapDxgiColorSpace(
        VideoColorMetadata const& metadata,
        bool requestHdrOutput);
}
```

- [ ] **Step 2: Add mapper implementation**

Create `src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.cpp`:

```cpp
#include "pch.h"
#include "DxgiColorSpaceMapper.h"

namespace winrt::NextGenEmby::Native::implementation
{
    namespace
    {
        constexpr int AVCOL_PRI_BT709 = 1;
        constexpr int AVCOL_PRI_BT2020 = 9;
        constexpr int AVCOL_SPC_RGB = 0;
        constexpr int AVCOL_SPC_BT709 = 1;
        constexpr int AVCOL_SPC_BT470BG = 5;
        constexpr int AVCOL_SPC_SMPTE170M = 6;
        constexpr int AVCOL_SPC_BT2020_NCL = 9;
        constexpr int AVCOL_TRC_BT709 = 1;
        constexpr int AVCOL_TRC_SMPTE2084 = 16;
        constexpr int AVCOL_TRC_ARIB_STD_B67 = 18;
        constexpr int AVCOL_RANGE_JPEG = 2;
        constexpr int AVCHROMA_LOC_LEFT = 1;
        constexpr int AVCHROMA_LOC_TOPLEFT = 3;

        bool IsFullRange(VideoColorMetadata const& metadata)
        {
            return metadata.ColorRange == AVCOL_RANGE_JPEG;
        }
    }

    DxgiColorSpaceMapping MapDxgiColorSpace(
        VideoColorMetadata const& metadata,
        bool requestHdrOutput)
    {
        DxgiColorSpaceMapping result{};
        result.IsHdrOutput = requestHdrOutput;
        result.OutputColorSpace = requestHdrOutput
            ? DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020
            : DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709;

        if (metadata.ColorSpace == AVCOL_SPC_RGB)
        {
            result.InputColorSpace = requestHdrOutput
                ? DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020
                : DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709;
            result.IsSupported = true;
            return result;
        }

        if (metadata.ColorPrimaries == AVCOL_PRI_BT2020 ||
            metadata.ColorSpace == AVCOL_SPC_BT2020_NCL)
        {
            if (metadata.ColorTransfer == AVCOL_TRC_SMPTE2084)
            {
                if (IsFullRange(metadata))
                {
                    result.IsSupported = false;
                    return result;
                }

                result.InputColorSpace = metadata.ChromaLocation == AVCHROMA_LOC_LEFT
                    ? DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020
                    : DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_TOPLEFT_P2020;
                result.IsSupported = true;
                return result;
            }

            if (metadata.ColorTransfer == AVCOL_TRC_ARIB_STD_B67)
            {
                result.InputColorSpace = IsFullRange(metadata)
                    ? DXGI_COLOR_SPACE_YCBCR_FULL_GHLG_TOPLEFT_P2020
                    : DXGI_COLOR_SPACE_YCBCR_STUDIO_GHLG_TOPLEFT_P2020;
                result.IsSupported = true;
                return result;
            }

            result.InputColorSpace = metadata.ChromaLocation == AVCHROMA_LOC_LEFT
                ? (IsFullRange(metadata)
                    ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P2020
                    : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P2020)
                : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_TOPLEFT_P2020;
            result.IsSupported = !IsFullRange(metadata) || metadata.ChromaLocation == AVCHROMA_LOC_LEFT;
            return result;
        }

        if (metadata.ColorSpace == AVCOL_SPC_BT470BG ||
            metadata.ColorSpace == AVCOL_SPC_SMPTE170M)
        {
            result.InputColorSpace = IsFullRange(metadata)
                ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P601
                : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P601;
            result.IsSupported = true;
            return result;
        }

        result.InputColorSpace = IsFullRange(metadata)
            ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P709
            : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709;
        result.IsSupported = true;
        return result;
    }
}
```

- [ ] **Step 3: Include mapper in project files**

Add to `src/NextGenEmby.Native/NextGenEmby.Native.vcxproj`:

```xml
<ClInclude Include="Media\DxgiColorSpaceMapper.h" />
<ClCompile Include="Media\DxgiColorSpaceMapper.cpp" />
```

Add matching entries to `src/NextGenEmby.Native/NextGenEmby.Native.vcxproj.filters`.

- [ ] **Step 4: Change video processing signature**

Modify `src/NextGenEmby.Native/DxDeviceResources.h`:

```cpp
#include "Media/DxgiColorSpaceMapper.h"
```

Change `TryProcessVideoFrameToBackBuffer` parameters from `bool usesBt709Matrix, bool isFullRange` to:

```cpp
VideoColorMetadata const& colorMetadata,
bool requestHdrOutput
```

- [ ] **Step 5: Use `ID3D11VideoContext1` and validate conversion**

Modify `TryProcessVideoFrameToBackBuffer()` in `src/NextGenEmby.Native/DxDeviceResources.cpp`:

```cpp
auto mapping = MapDxgiColorSpace(colorMetadata, requestHdrOutput);
if (!mapping.IsSupported)
{
    return false;
}

Microsoft::WRL::ComPtr<ID3D11VideoProcessorEnumerator1> enumerator1;
auto canUseColorSpace1 = SUCCEEDED(enumerator.As(&enumerator1));
if (canUseColorSpace1)
{
    BOOL supported = FALSE;
    if (FAILED(enumerator1->CheckVideoProcessorFormatConversion(
        sourceDescription.Format,
        mapping.InputColorSpace,
        targetDescription.Format,
        mapping.OutputColorSpace,
        &supported)) ||
        !supported)
    {
        return false;
    }
}
```

Before `VideoProcessorBlt()`:

```cpp
Microsoft::WRL::ComPtr<ID3D11VideoContext1> videoContext1;
if (SUCCEEDED(videoContext.As(&videoContext1)))
{
    videoContext1->VideoProcessorSetStreamColorSpace1(
        processor.Get(),
        0,
        mapping.InputColorSpace);
    videoContext1->VideoProcessorSetOutputColorSpace1(
        processor.Get(),
        mapping.OutputColorSpace);
}
else
{
    auto inputColorSpace = CreateInputColorSpace(false, colorMetadata.ColorRange == 2);
    auto outputColorSpace = CreateOutputColorSpace();
    videoContext->VideoProcessorSetStreamColorSpace(processor.Get(), 0, &inputColorSpace);
    videoContext->VideoProcessorSetOutputColorSpace(processor.Get(), &outputColorSpace);
}
```

Remove the old unconditional `VideoProcessorSetStreamColorSpace()` / `VideoProcessorSetOutputColorSpace()` calls.

- [ ] **Step 6: Pass metadata from renderer**

Modify `src/NextGenEmby.Native/Media/VideoRenderer.cpp` call:

```cpp
VideoColorMetadata colorMetadata{
    frame.ColorPrimaries,
    frame.ColorSpace,
    frame.ColorTransfer,
    frame.ColorRange,
    frame.ChromaLocation};

rendered = m_deviceResources.TryProcessVideoFrameToBackBuffer(
    frame.Texture.Get(),
    frame.TextureArrayIndex,
    frame.Width,
    frame.Height,
    frame.DisplayWidth,
    frame.DisplayHeight,
    colorMetadata,
    targetHdrKind != VideoHdrKind::None);
```

- [ ] **Step 7: Build**

Run:

```powershell
& "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" NextGenXboxEmby.sln /p:Configuration=Debug /p:Platform=x64 /m
```

Expected: solution builds.

- [ ] **Step 8: Commit**

```powershell
git add src\NextGenEmby.Native\Media\DxgiColorSpaceMapper.h src\NextGenEmby.Native\Media\DxgiColorSpaceMapper.cpp src\NextGenEmby.Native\NextGenEmby.Native.vcxproj src\NextGenEmby.Native\NextGenEmby.Native.vcxproj.filters src\NextGenEmby.Native\DxDeviceResources.h src\NextGenEmby.Native\DxDeviceResources.cpp src\NextGenEmby.Native\Media\VideoRenderer.cpp
git commit -m "feat: map video colors through DXGI color spaces"
```

---

### Task 5: Use 10-bit Swapchain and Report Native HDR Diagnostics

**Files:**
- Modify: `src/NextGenEmby.Native/DxDeviceResources.h`
- Modify: `src/NextGenEmby.Native/DxDeviceResources.cpp`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.idl`
- Modify: `src/NextGenEmby.Native/NativePlaybackStatus.h`
- Modify: `src/NextGenEmby.Native/NativePlaybackStatus.cpp`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.cpp`
- Modify: `src/NextGenEmby.Core/Playback/PlaybackDisplayStatus.cs`
- Modify: `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`

- [ ] **Step 1: Add swapchain diagnostics to native status**

Modify `src/NextGenEmby.Native/NativePlaybackEngine.idl`:

```idl
String SwapChainFormat;
String InputColorSpace;
String OutputColorSpace;
String ColorConversionStatus;
```

Add matching hstring fields/getters/setters in `NativePlaybackStatus.h/.cpp`.

- [ ] **Step 2: Track swapchain format in device resources**

Modify `DxDeviceResources.h`:

```cpp
DXGI_FORMAT SwapChainFormat() const noexcept { return m_swapChainFormat; }
bool IsTenBitSwapChain() const noexcept { return m_swapChainFormat == DXGI_FORMAT_R10G10B10A2_UNORM; }

DXGI_FORMAT m_swapChainFormat{DXGI_FORMAT_UNKNOWN};
```

In `CreateSwapChain()` after successful creation:

```cpp
m_swapChainFormat = description.Format;
```

- [ ] **Step 3: Prefer 10-bit swapchain with fallback**

Modify `AttachSurface()`:

```cpp
try
{
    CreateSwapChain(width, height, true);
}
catch (...)
{
    CreateSwapChain(width, height, false);
}
```

Use 3 buffers for Xbox-style 4K playback:

```cpp
description.BufferCount = useTenBit ? 3 : 2;
description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
```

- [ ] **Step 4: Gate HDR color space on 10-bit swapchain**

Modify `SetHdr10ColorSpace()`:

```cpp
if (!m_swapChain || !IsTenBitSwapChain())
{
    return false;
}
return SUCCEEDED(m_swapChain->SetColorSpace1(DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020));
```

- [ ] **Step 5: Surface diagnostics to C#**

Extend `PlaybackDisplayStatus` constructor and properties with:

```csharp
public string SwapChainFormat { get; }
public string InputColorSpace { get; }
public string OutputColorSpace { get; }
public string ColorConversionStatus { get; }
```

Map values in `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`.

- [ ] **Step 6: Run tests and build**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
& "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" NextGenXboxEmby.sln /p:Configuration=Debug /p:Platform=x64 /m
```

Expected: tests and build pass.

- [ ] **Step 7: Commit**

```powershell
git add src\NextGenEmby.Native\DxDeviceResources.h src\NextGenEmby.Native\DxDeviceResources.cpp src\NextGenEmby.Native\NativePlaybackEngine.idl src\NextGenEmby.Native\NativePlaybackStatus.h src\NextGenEmby.Native\NativePlaybackStatus.cpp src\NextGenEmby.Native\NativePlaybackEngine.cpp src\NextGenEmby.Core\Playback\PlaybackDisplayStatus.cs src\NextGenEmby.App\Playback\WinRtNativePlaybackEngine.cs
git commit -m "feat: add native HDR color diagnostics"
```

---

### Task 6: Add DV Fallback UI Labels and Verification Matrix

**Files:**
- Modify: `src/NextGenEmby.App/Views/MediaDetailsPage.xaml.cs`
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`
- Modify: `docs/native-playback-smoke-tests.md`
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyModelTests.cs`

- [ ] **Step 1: Update model test expectations**

Modify `tests/NextGenEmby.Core.Tests/Emby/EmbyModelTests.cs` to assert:

```csharp
source.HdrProfile.Kind = HdrPlaybackKind.DolbyVisionWithHdr10Fallback;
Assert.Equal("HDR10 fallback from Dolby Vision", source.HdrProfile.PlaybackStrategy);
Assert.True(source.HdrProfile.IsDolbyVision);
```

- [ ] **Step 2: Show source label strategy**

Modify `CreateSourceSummary()` in `MediaDetailsPage.xaml.cs`:

```csharp
if (source.HdrProfile.Kind != HdrPlaybackKind.Sdr)
{
    parts.Add(source.HdrProfile.PlaybackStrategy);
}
```

Modify `CreateSourceLabel()` in `PlaybackPage.xaml.cs` similarly:

```csharp
if (source.HdrProfile.Kind != HdrPlaybackKind.Sdr)
{
    label += $" · {source.HdrProfile.PlaybackStrategy}";
}
```

- [ ] **Step 3: Add smoke matrix rows**

Modify `docs/native-playback-smoke-tests.md` table to include:

```markdown
| DV Profile 8.1 + HDR10 base | 未测 | 未测 | 未测 | 未测 | HDR10 fallback | 未测 | 不应进入 DV 输出 |
| DV Profile 5 | 不支持直放 | N/A | N/A | N/A | Unsupported | N/A | 应提示或选择其它版本 |
| HDR10+ + DV hybrid | 未测 | 未测 | 未测 | 未测 | HDR10 fallback | 未测 | 不应黑屏 |
```

- [ ] **Step 4: Run verification**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src\NextGenEmby.App\Views\MediaDetailsPage.xaml.cs src\NextGenEmby.App\Views\PlaybackPage.xaml.cs docs\native-playback-smoke-tests.md tests\NextGenEmby.Core.Tests\Emby\EmbyModelTests.cs
git commit -m "feat: expose HDR fallback strategy in UI"
```

---

## Final Verification

- [ ] Run all core tests:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] Build Debug x64 package:

```powershell
& "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" NextGenXboxEmby.sln /p:Configuration=Debug /p:Platform=x64 /m
```

Expected: solution builds and creates a new UWP package.

- [ ] Xbox hardware verification:

Use `docs/native-playback-smoke-tests.md`. Record results for:

- HDR10 Main10.
- DV Profile 8.1 with HDR10 base.
- DV Profile 5 unsupported behavior.
- HDR10+ + DV hybrid.

## Self-Review

Spec coverage:

- Complete frame color metadata: Task 3.
- DXGI color-space mapping and conversion validation: Task 4.
- 10-bit swapchain and HDR color-space gating: Task 5.
- DV fallback categories and source ranking: Tasks 1 and 2.
- Diagnostics and user-visible strategy: Tasks 5 and 6.

Known deferred work:

- Real libdovi RPU conversion is intentionally not included. The spec says the project does not output Dolby Vision; first implementation only identifies DV and uses HDR10/HLG fallback. If Xbox testing later shows compatible DV samples still fail, create a follow-up plan specifically for packet-level `HevcCompatibilityBitstreamFilter`.
