# Native Playback Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the temporary UWP system-player backend with a Kodi-informed native playback core for Xbox direct-play HEVC/HDR10, audio stream switching, subtitle switching, and Emby progress integration.

**Architecture:** Keep Emby protocol and orchestration in the tested C# core, add a managed native-engine adapter that is testable without UWP, and put Xbox-specific decode/render/display control in a C++/WinRT UWP component. The native component owns the `SwapChainPanel`, D3D11/DXGI resources, HDR display state, decode queues, audio output, subtitle rendering, and native state events.

**Tech Stack:** Visual Studio 2022, UWP, C# XAML, C++/WinRT Windows Runtime Component, D3D11, DXGI, `SwapChainPanel`, FFmpeg with D3D11VA, XAudio2, DirectWrite/Direct2D for text subtitles, xUnit for managed tests, Xbox Dev Mode hardware smoke tests.

---

## Scope Check

This plan starts after the foundation branch. The original local blocker from `docs/foundation-status.md` was missing UWP `.NETCore,Version=v5.0` reference assemblies. That blocker was repaired on 2026-07-05; `NextGenXboxEmby.sln` now builds with `Debug|x64`.

Included:

- UWP toolchain repair verification.
- Managed status/capability contracts for native playback.
- Managed `NativeDirectXPlaybackBackend` adapter with unit tests.
- C++/WinRT component shell and C# wrapper.
- Xbox `SwapChainPanel` rendering surface.
- Kodi-derived HDR display state, DXGI color space, and restoration behavior.
- Native direct-play pipeline for HTTP input, HEVC Main/Main10, HDR10 metadata, audio switching, subtitle switching, and progress callbacks.
- Hardware smoke checklist for Xbox.

Not included:

- Emby server transcoding.
- Store submission or Partner Center packaging.
- Dolby Vision passthrough.
- Multiple-server account management.
- Non-Xbox UI.

## File Structure

Create or modify these files:

- `docs/foundation-status.md`: update after the toolchain blocker is fixed.
- `docs/native-playback-smoke-tests.md`: Xbox hardware smoke matrix and expected results.
- `docs/native-dependencies.md`: native dependency source, build flags, and binary provenance.
- `docs/superpowers/plans/2026-07-05-native-playback-core.md`: this implementation plan.
- `src/NextGenEmby.Core/Playback/PlaybackBackendCapabilities.cs`: feature flags and capabilities.
- `src/NextGenEmby.Core/Playback/PlaybackDisplayStatus.cs`: HDR/display status reported by native backends.
- `src/NextGenEmby.Core/Playback/IPlaybackBackendDiagnostics.cs`: optional diagnostic surface.
- `src/NextGenEmby.Core/Playback/INativePlaybackEngine.cs`: managed engine abstraction for tests.
- `src/NextGenEmby.Core/Playback/NativePlaybackOpenRequest.cs`: immutable request sent to native.
- `src/NextGenEmby.Core/Playback/NativeDirectXPlaybackBackend.cs`: `IPlaybackBackend` adapter over `INativePlaybackEngine`.
- `tests/NextGenEmby.Core.Tests/Playback/NativeDirectXPlaybackBackendTests.cs`: managed adapter tests.
- `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`: wrapper around the C++/WinRT runtime class.
- `src/NextGenEmby.App/Views/PlaybackPage.xaml`: add `SwapChainPanel` for native rendering.
- `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`: choose native backend when available.
- `src/NextGenEmby.App/NextGenEmby.App.csproj`: reference the native component.
- `src/NextGenEmby.Native/NextGenEmby.Native.vcxproj`: C++/WinRT UWP component.
- `src/NextGenEmby.Native/NativePlaybackEngine.idl`: WinRT API surface.
- `src/NextGenEmby.Native/NativePlaybackEngine.h/.cpp`: runtime class entry point.
- `src/NextGenEmby.Native/HdrDisplayController.h/.cpp`: Xbox HDR capability, toggle, and restoration.
- `src/NextGenEmby.Native/DxDeviceResources.h/.cpp`: D3D11 device, swapchain, color space, HDR metadata.
- `src/NextGenEmby.Native/Media/PlaybackGraph.h/.cpp`: native playback graph coordinator.
- `src/NextGenEmby.Native/Media/HttpMediaInput.h/.cpp`: HTTP direct-stream input.
- `src/NextGenEmby.Native/Media/VideoDecoder.h/.cpp`: HEVC Main/Main10 decode path.
- `src/NextGenEmby.Native/Media/VideoRenderer.h/.cpp`: D3D11 video renderer and HDR metadata propagation.
- `src/NextGenEmby.Native/Media/AudioRenderer.h/.cpp`: audio output and audio stream selection.
- `src/NextGenEmby.Native/Media/SubtitleRenderer.h/.cpp`: text subtitle selection and rendering.
- `NextGenXboxEmby.sln`: include `NextGenEmby.Native`.

---

### Task 0: Repair and Verify the UWP Native Toolchain

**Files:**
- Modify: `docs/foundation-status.md`

- [x] **Step 1: Verify the current blocker**

Run:

```powershell
Test-Path 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v5.0'
& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -products * -requires Microsoft.VisualStudio.ComponentGroup.UWP.NetCoreAndStandard -property installationPath
```

Expected before repair:

```text
False
```

The second command currently prints no path.

- [x] **Step 2: Install the missing UWP/.NET Native components**

Run from an elevated PowerShell or Visual Studio Installer UI:

```powershell
$installer = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe'
$installPath = 'C:\Program Files\Microsoft Visual Studio\2022\Community'
& $installer modify `
  --installPath $installPath `
  --add Microsoft.VisualStudio.Workload.Universal `
  --add Microsoft.VisualStudio.ComponentGroup.UWP.NetCoreAndStandard `
  --add Microsoft.VisualStudio.ComponentGroup.UWP.VC `
  --add Microsoft.VisualStudio.Component.Windows10SDK.22621 `
  --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
  --includeRecommended `
  --passive `
  --norestart
```

Expected: installer exits with code 0.

- [x] **Step 3: Verify UWP references now exist**

Run:

```powershell
Test-Path 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v5.0'
& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -products * -requires Microsoft.VisualStudio.ComponentGroup.UWP.NetCoreAndStandard -property installationPath
```

Expected:

```text
True
C:\Program Files\Microsoft Visual Studio\2022\Community
```

- [x] **Step 4: Rebuild the foundation solution**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

- [x] **Step 5: Update foundation status**

In `docs/foundation-status.md`, move the UWP app build from `Current Blocker` to `Verified` and add the successful MSBuild timestamp.

- [x] **Step 6: Commit**

```powershell
git add docs\foundation-status.md
git commit -m "docs: record UWP toolchain repair"
```

Expected: commit succeeds.

---

### Task 1: Add Managed Native Playback Diagnostics Contracts

**Files:**
- Create: `src/NextGenEmby.Core/Playback/PlaybackBackendCapabilities.cs`
- Create: `src/NextGenEmby.Core/Playback/PlaybackDisplayStatus.cs`
- Create: `src/NextGenEmby.Core/Playback/IPlaybackBackendDiagnostics.cs`
- Test: `tests/NextGenEmby.Core.Tests/Playback/PlaybackBackendDiagnosticsTests.cs`

- [x] **Step 1: Write tests for capabilities and display status**

Create `tests/NextGenEmby.Core.Tests/Playback/PlaybackBackendDiagnosticsTests.cs`:

```csharp
using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class PlaybackBackendDiagnosticsTests
{
    [Fact]
    public void Capabilities_Can_Report_Native_Hdr_Features()
    {
        var capabilities = new PlaybackBackendCapabilities(
            PlaybackBackendFeature.DirectPlayHttp |
            PlaybackBackendFeature.HevcMain10 |
            PlaybackBackendFeature.Hdr10 |
            PlaybackBackendFeature.AudioStreamSwitching |
            PlaybackBackendFeature.SubtitleStreamSwitching);

        Assert.True(capabilities.Supports(PlaybackBackendFeature.DirectPlayHttp));
        Assert.True(capabilities.Supports(PlaybackBackendFeature.HevcMain10));
        Assert.True(capabilities.Supports(PlaybackBackendFeature.Hdr10));
        Assert.False(capabilities.Supports(PlaybackBackendFeature.Transcoding));
    }

    [Fact]
    public void DisplayStatus_Requires_Message_For_Failed_Hdr_Status()
    {
        var status = new PlaybackDisplayStatus(
            HdrOutputStatus.Failed,
            isHdrDisplayAvailable: true,
            isHdrOutputActive: false,
            message: "DXGI SetColorSpace1 failed.");

        Assert.Equal(HdrOutputStatus.Failed, status.HdrStatus);
        Assert.True(status.IsHdrDisplayAvailable);
        Assert.False(status.IsHdrOutputActive);
        Assert.Equal("DXGI SetColorSpace1 failed.", status.Message);
    }
}
```

- [x] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: compile fails because `PlaybackBackendCapabilities` does not exist.

- [x] **Step 3: Add capability flags**

Create `src/NextGenEmby.Core/Playback/PlaybackBackendCapabilities.cs`:

```csharp
using System;

namespace NextGenEmby.Core.Playback
{
    [Flags]
    public enum PlaybackBackendFeature
    {
        None = 0,
        DirectPlayHttp = 1,
        Hevc = 2,
        HevcMain10 = 4,
        Hdr10 = 8,
        AudioStreamSwitching = 16,
        SubtitleStreamSwitching = 32,
        MediaSourceSwitching = 64,
        Transcoding = 128
    }

    public sealed class PlaybackBackendCapabilities
    {
        public PlaybackBackendCapabilities(PlaybackBackendFeature features)
        {
            Features = features;
        }

        public PlaybackBackendFeature Features { get; }

        public bool Supports(PlaybackBackendFeature feature)
        {
            return (Features & feature) == feature;
        }
    }
}
```

- [x] **Step 4: Add display status and diagnostics interface**

Create `src/NextGenEmby.Core/Playback/PlaybackDisplayStatus.cs`:

```csharp
using System;

namespace NextGenEmby.Core.Playback
{
    public enum HdrOutputStatus
    {
        Unknown = 0,
        Unsupported = 1,
        Off = 2,
        On = 3,
        Failed = 4
    }

    public sealed class PlaybackDisplayStatus
    {
        public PlaybackDisplayStatus(
            HdrOutputStatus hdrStatus,
            bool isHdrDisplayAvailable,
            bool isHdrOutputActive,
            string message = "")
        {
            if (hdrStatus == HdrOutputStatus.Failed && string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Failed HDR status requires a message.", nameof(message));
            }

            HdrStatus = hdrStatus;
            IsHdrDisplayAvailable = isHdrDisplayAvailable;
            IsHdrOutputActive = isHdrOutputActive;
            Message = message ?? "";
        }

        public HdrOutputStatus HdrStatus { get; }
        public bool IsHdrDisplayAvailable { get; }
        public bool IsHdrOutputActive { get; }
        public string Message { get; }
    }
}
```

Create `src/NextGenEmby.Core/Playback/IPlaybackBackendDiagnostics.cs`:

```csharp
namespace NextGenEmby.Core.Playback
{
    public interface IPlaybackBackendDiagnostics
    {
        PlaybackBackendCapabilities Capabilities { get; }
        PlaybackDisplayStatus DisplayStatus { get; }
    }
}
```

- [x] **Step 5: Run tests and verify pass**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [x] **Step 6: Commit**

```powershell
git add src\NextGenEmby.Core\Playback tests\NextGenEmby.Core.Tests\Playback
git commit -m "feat: add playback backend diagnostics contracts"
```

Expected: commit succeeds.

---

### Task 2: Add Managed Native Engine Adapter

**Files:**
- Create: `src/NextGenEmby.Core/Playback/INativePlaybackEngine.cs`
- Create: `src/NextGenEmby.Core/Playback/NativePlaybackOpenRequest.cs`
- Create: `src/NextGenEmby.Core/Playback/NativeDirectXPlaybackBackend.cs`
- Test: `tests/NextGenEmby.Core.Tests/Playback/NativeDirectXPlaybackBackendTests.cs`

- [x] **Step 1: Write adapter tests**

Create `tests/NextGenEmby.Core.Tests/Playback/NativeDirectXPlaybackBackendTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class NativeDirectXPlaybackBackendTests
{
    [Fact]
    public async Task StartAsync_Maps_Descriptor_To_Native_Open_Request()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            DirectStreamUrl = "https://emby.local/videos/1/stream.mkv?api_key=token"
        };

        await backend.StartAsync(new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 1234,
            audioStreamIndex: 2,
            subtitleStreamIndex: 7));

        Assert.Equal("item-1", engine.LastRequest!.ItemId);
        Assert.Equal("source-1", engine.LastRequest.MediaSourceId);
        Assert.Equal(source.DirectStreamUrl, engine.LastRequest.DirectStreamUrl);
        Assert.Equal(1234, engine.LastRequest.StartPositionTicks);
        Assert.Equal(2, engine.LastRequest.AudioStreamIndex);
        Assert.Equal(7, engine.LastRequest.SubtitleStreamIndex);
    }

    [Fact]
    public async Task StartAsync_Rejects_Missing_Direct_Stream_Url()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        var source = new EmbyMediaSource { Id = "source-1" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backend.StartAsync(new PlaybackDescriptor("item-1", source, new[] { source }, 0)));
    }

    [Fact]
    public void StateChanged_Propagates_From_Native_Engine()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        PlaybackStateChangedEventArgs? received = null;
        backend.StateChanged += (_, args) => received = args;

        engine.Raise(PlaybackState.Buffering, "buffering");

        Assert.NotNull(received);
        Assert.Equal(PlaybackState.Buffering, received!.State);
        Assert.Equal("buffering", received.Message);
    }

    private sealed class RecordingNativePlaybackEngine : INativePlaybackEngine
    {
        public NativePlaybackOpenRequest? LastRequest { get; private set; }
        public long CurrentPositionTicks { get; set; }
        public PlaybackBackendCapabilities Capabilities { get; } =
            new PlaybackBackendCapabilities(PlaybackBackendFeature.DirectPlayHttp);
        public PlaybackDisplayStatus DisplayStatus { get; } =
            new PlaybackDisplayStatus(HdrOutputStatus.Unknown, false, false);

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public Task OpenAsync(NativePlaybackOpenRequest request)
        {
            LastRequest = request;
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

        public void Raise(PlaybackState state, string message)
        {
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(state, message));
        }
    }
}
```

- [x] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: compile fails because native adapter types do not exist.

- [x] **Step 3: Add engine abstraction and open request**

Create `src/NextGenEmby.Core/Playback/INativePlaybackEngine.cs`:

```csharp
using System;
using System.Threading.Tasks;

namespace NextGenEmby.Core.Playback
{
    public interface INativePlaybackEngine : IPlaybackBackendDiagnostics
    {
        event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        long CurrentPositionTicks { get; }

        Task OpenAsync(NativePlaybackOpenRequest request);

        Task PauseAsync();

        Task ResumeAsync();

        Task SeekAsync(long positionTicks);

        Task StopAsync();
    }
}
```

Create `src/NextGenEmby.Core/Playback/NativePlaybackOpenRequest.cs`:

```csharp
using System;

namespace NextGenEmby.Core.Playback
{
    public sealed class NativePlaybackOpenRequest
    {
        public NativePlaybackOpenRequest(
            string itemId,
            string mediaSourceId,
            string directStreamUrl,
            long startPositionTicks,
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item id is required.", nameof(itemId));
            }

            if (string.IsNullOrWhiteSpace(mediaSourceId))
            {
                throw new ArgumentException("Media source id is required.", nameof(mediaSourceId));
            }

            if (!Uri.TryCreate(directStreamUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Direct stream URL must be an absolute HTTP or HTTPS URL.", nameof(directStreamUrl));
            }

            if (startPositionTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startPositionTicks), "Start position cannot be negative.");
            }

            ItemId = itemId;
            MediaSourceId = mediaSourceId;
            DirectStreamUrl = directStreamUrl;
            StartPositionTicks = startPositionTicks;
            AudioStreamIndex = audioStreamIndex;
            SubtitleStreamIndex = subtitleStreamIndex;
        }

        public string ItemId { get; }
        public string MediaSourceId { get; }
        public string DirectStreamUrl { get; }
        public long StartPositionTicks { get; }
        public int? AudioStreamIndex { get; }
        public int? SubtitleStreamIndex { get; }
    }
}
```

- [x] **Step 4: Add managed adapter**

Create `src/NextGenEmby.Core/Playback/NativeDirectXPlaybackBackend.cs`:

```csharp
using System;
using System.Threading.Tasks;

namespace NextGenEmby.Core.Playback
{
    public sealed class NativeDirectXPlaybackBackend : IPlaybackBackend, IPlaybackBackendDiagnostics
    {
        private readonly INativePlaybackEngine _engine;

        public NativeDirectXPlaybackBackend(INativePlaybackEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _engine.StateChanged += Engine_OnStateChanged;
        }

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public long CurrentPositionTicks => _engine.CurrentPositionTicks;

        public PlaybackBackendCapabilities Capabilities => _engine.Capabilities;

        public PlaybackDisplayStatus DisplayStatus => _engine.DisplayStatus;

        public Task StartAsync(PlaybackDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var source = descriptor.MediaSource;
            var request = new NativePlaybackOpenRequest(
                descriptor.ItemId,
                source.Id,
                source.DirectStreamUrl,
                descriptor.StartPositionTicks,
                descriptor.AudioStreamIndex,
                descriptor.SubtitleStreamIndex);

            return _engine.OpenAsync(request);
        }

        public Task PauseAsync() => _engine.PauseAsync();

        public Task ResumeAsync() => _engine.ResumeAsync();

        public Task SeekAsync(long positionTicks) => _engine.SeekAsync(positionTicks);

        public Task StopAsync() => _engine.StopAsync();

        private void Engine_OnStateChanged(object? sender, PlaybackStateChangedEventArgs args)
        {
            StateChanged?.Invoke(this, args);
        }
    }
}
```

- [x] **Step 5: Run tests and verify pass**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [x] **Step 6: Commit**

```powershell
git add src\NextGenEmby.Core\Playback tests\NextGenEmby.Core.Tests\Playback
git commit -m "feat: add native playback backend adapter"
```

Expected: commit succeeds.

---

### Task 3: 创建 C++/WinRT 组件外壳

**Files:**
- Create: `src/NextGenEmby.Native/NextGenEmby.Native.vcxproj`
- Create: `src/NextGenEmby.Native/NextGenEmby.Native.vcxproj.filters`
- Create: `src/NextGenEmby.Native/NextGenEmby.Native.def`
- Create: `src/NextGenEmby.Native/PropertySheet.props`
- Create: `src/NextGenEmby.Native/packages.config`
- Create: `src/NextGenEmby.Native/NativePlaybackEngine.idl`
- Create: `src/NextGenEmby.Native/pch.h`
- Create: `src/NextGenEmby.Native/pch.cpp`
- Create: `src/NextGenEmby.Native/NativePlaybackOpenRequest.h`
- Create: `src/NextGenEmby.Native/NativePlaybackOpenRequest.cpp`
- Create: `src/NextGenEmby.Native/NativePlaybackStatus.h`
- Create: `src/NextGenEmby.Native/NativePlaybackStatus.cpp`
- Create: `src/NextGenEmby.Native/NativePlaybackEngine.h`
- Create: `src/NextGenEmby.Native/NativePlaybackEngine.cpp`
- Modify: `.gitignore`
- Modify: `NextGenXboxEmby.sln`

- [x] **Step 1: 创建 C++/WinRT UWP 组件项目**

Use Visual Studio after Task 0 succeeds:

```text
Solution Explorer -> Add -> New Project -> C++/WinRT -> Windows Runtime Component (Universal Windows)
Project name: NextGenEmby.Native
Location: src
Target version: 10.0.22621.0
Minimum version: 10.0.19041.0
Platform: x64
```

Expected:

```text
src\NextGenEmby.Native\NextGenEmby.Native.vcxproj exists.
NextGenXboxEmby.sln includes NextGenEmby.Native.
```

After project creation, set the native project GUID to `{6F1A9D90-7A7D-4E91-8468-80D12D91A7D5}` in both `NextGenEmby.Native.vcxproj` and `NextGenXboxEmby.sln`. Using a fixed GUID keeps the next C# project-reference step deterministic.

- [x] **Step 2: 替换为第一版稳定 IDL API 面**

Edit `src/NextGenEmby.Native/NativePlaybackEngine.idl`:

```cpp
namespace NextGenEmby.Native
{
    enum NativePlaybackState
    {
        NativePlaybackState_Stopped = 0,
        NativePlaybackState_Opening = 1,
        NativePlaybackState_Buffering = 2,
        NativePlaybackState_Playing = 3,
        NativePlaybackState_Paused = 4,
        NativePlaybackState_Failed = 5
    };

    enum NativeHdrStatus
    {
        NativeHdrStatus_Unknown = 0,
        NativeHdrStatus_Unsupported = 1,
        NativeHdrStatus_Off = 2,
        NativeHdrStatus_On = 3,
        NativeHdrStatus_Failed = 4
    };

    runtimeclass NativePlaybackOpenRequest
    {
        NativePlaybackOpenRequest();
        String ItemId;
        String MediaSourceId;
        String DirectStreamUrl;
        Int64 StartPositionTicks;
        Int32 AudioStreamIndex;
        Boolean HasAudioStreamIndex;
        Int32 SubtitleStreamIndex;
        Boolean HasSubtitleStreamIndex;
    }

    runtimeclass NativePlaybackStatus
    {
        NativePlaybackStatus();
        NativeHdrStatus HdrStatus;
        Boolean IsHdrDisplayAvailable;
        Boolean IsHdrOutputActive;
        String Message;
    }

    delegate void NativePlaybackStateChangedHandler(NativePlaybackState state, String message);

    runtimeclass NativePlaybackEngine
    {
        NativePlaybackEngine();
        event NativePlaybackStateChangedHandler StateChanged;
        Int64 CurrentPositionTicks();
        NativePlaybackStatus DisplayStatus();
        Windows.Foundation.IAsyncAction OpenAsync(NativePlaybackOpenRequest request);
        Windows.Foundation.IAsyncAction PauseAsync();
        Windows.Foundation.IAsyncAction ResumeAsync();
        Windows.Foundation.IAsyncAction SeekAsync(Int64 positionTicks);
        Windows.Foundation.IAsyncAction StopAsync();
    }
}
```

- [x] **Step 3: 实现可构建的 native stub**

Edit `src/NextGenEmby.Native/NativePlaybackEngine.h`:

```cpp
#pragma once

#include "NativePlaybackEngine.g.h"

namespace winrt::NextGenEmby::Native::implementation
{
    struct NativePlaybackEngine : NativePlaybackEngineT<NativePlaybackEngine>
    {
        NativePlaybackEngine() = default;

        winrt::event_token StateChanged(NextGenEmby::Native::NativePlaybackStateChangedHandler const& handler);
        void StateChanged(winrt::event_token const& token) noexcept;

        int64_t CurrentPositionTicks() const noexcept;
        NextGenEmby::Native::NativePlaybackStatus DisplayStatus() const;

        winrt::Windows::Foundation::IAsyncAction OpenAsync(NextGenEmby::Native::NativePlaybackOpenRequest request);
        winrt::Windows::Foundation::IAsyncAction PauseAsync();
        winrt::Windows::Foundation::IAsyncAction ResumeAsync();
        winrt::Windows::Foundation::IAsyncAction SeekAsync(int64_t positionTicks);
        winrt::Windows::Foundation::IAsyncAction StopAsync();

    private:
        void Raise(NextGenEmby::Native::NativePlaybackState state, winrt::hstring const& message = L"");

        winrt::event<NextGenEmby::Native::NativePlaybackStateChangedHandler> m_stateChanged;
        int64_t m_positionTicks{0};
        NextGenEmby::Native::NativePlaybackStatus m_displayStatus{nullptr};
    };
}

namespace winrt::NextGenEmby::Native::factory_implementation
{
    struct NativePlaybackEngine : NativePlaybackEngineT<NativePlaybackEngine, implementation::NativePlaybackEngine>
    {
    };
}
```

Edit `src/NextGenEmby.Native/NativePlaybackEngine.cpp`:

```cpp
#include "pch.h"
#include "NativePlaybackEngine.h"
#include "NativePlaybackEngine.g.cpp"

namespace winrt::NextGenEmby::Native::implementation
{
    winrt::event_token NativePlaybackEngine::StateChanged(
        NextGenEmby::Native::NativePlaybackStateChangedHandler const& handler)
    {
        return m_stateChanged.add(handler);
    }

    void NativePlaybackEngine::StateChanged(winrt::event_token const& token) noexcept
    {
        m_stateChanged.remove(token);
    }

    int64_t NativePlaybackEngine::CurrentPositionTicks() const noexcept
    {
        return m_positionTicks;
    }

    NextGenEmby::Native::NativePlaybackStatus NativePlaybackEngine::DisplayStatus() const
    {
        if (m_displayStatus == nullptr)
        {
            auto status = winrt::make<NativePlaybackStatus>();
            status.HdrStatus(NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unknown);
            status.IsHdrDisplayAvailable(false);
            status.IsHdrOutputActive(false);
            status.Message(L"Native engine has not probed the display yet.");
            return status;
        }

        return m_displayStatus;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::OpenAsync(
        NextGenEmby::Native::NativePlaybackOpenRequest request)
    {
        if (request == nullptr || request.DirectStreamUrl().empty())
        {
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Failed, L"Direct stream URL is required.");
            co_return;
        }

        m_positionTicks = request.StartPositionTicks();
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Opening);
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Playing);
        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::PauseAsync()
    {
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Paused);
        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::ResumeAsync()
    {
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Playing);
        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::SeekAsync(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Failed, L"Seek position cannot be negative.");
            co_return;
        }

        m_positionTicks = positionTicks;
        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::StopAsync()
    {
        m_positionTicks = 0;
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Stopped);
        co_return;
    }

    void NativePlaybackEngine::Raise(NextGenEmby::Native::NativePlaybackState state, winrt::hstring const& message)
    {
        m_stateChanged(state, message);
    }
}
```

- [x] **Step 4: 构建 native 组件**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.Native\NextGenEmby.Native.vcxproj /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

执行记录（2026-07-05）:

- C++/WinRT NuGet 包使用 `Microsoft.Windows.CppWinRT.2.0.220531.1`，通过 `packages.config` 还原；本地 `packages/` 已加入忽略规则。
- `NativePlaybackEngine.idl` 不手写 `import "Windows.Foundation.idl";`。CppWinRT/MSBuild 已通过 winmd references 和 `winrtbase.idl` 提供基础 WinRT 类型，手写导入会导致 MIDL 重复定义 `IUnknown`、`IInspectable`、`IAsyncInfo`。
- 多个 runtimeclass 会生成独立的 `.g.h` 文件，因此 `NativePlaybackOpenRequest.h` 包含 `NativePlaybackOpenRequest.g.h`，`NativePlaybackStatus.h` 包含 `NativePlaybackStatus.g.h`，`NativePlaybackEngine.h` 包含 `NativePlaybackEngine.g.h`。
- 项目保留 `.def + module.g.cpp` 导出路径，并移除 `_WINRT_DLL` 宏，避免 `DllGetActivationFactory` 和 `DllCanUnloadNow` 重复导出警告。
- Native 单项目验证通过：`NextGenEmby.Native.vcxproj /restore /p:Configuration=Debug /p:Platform=x64`，0 警告、0 错误。
- 完整解决方案验证通过：`NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64`，0 警告、0 错误。
- Managed 测试验证通过：`dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal`，52 passed、0 failed、0 skipped。

- [x] **Step 5: 提交**

```powershell
git add NextGenXboxEmby.sln src\NextGenEmby.Native
git commit -m "feat: add native playback component shell"
```

Expected: commit succeeds.

---

### Task 4: Bridge the Native Runtime Class Into the UWP App

**Files:**
- Create: `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`
- Modify: `src/NextGenEmby.App/NextGenEmby.App.csproj`
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml`
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`

- [ ] **Step 1: Add a native project reference to the UWP app**

Modify `src/NextGenEmby.App/NextGenEmby.App.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\NextGenEmby.Core\NextGenEmby.Core.csproj">
    <Project>{3E3D8F22-1FD8-4A53-81D4-11998454C03B}</Project>
    <Name>NextGenEmby.Core</Name>
  </ProjectReference>
  <ProjectReference Include="..\NextGenEmby.Native\NextGenEmby.Native.vcxproj">
    <Project>{6F1A9D90-7A7D-4E91-8468-80D12D91A7D5}</Project>
    <Name>NextGenEmby.Native</Name>
  </ProjectReference>
</ItemGroup>
```

- [ ] **Step 2: Add the C# WinRT wrapper**

Create `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`:

```csharp
using System;
using System.Threading.Tasks;
using NextGenEmby.Core.Playback;
using NextGenEmby.Native;

namespace NextGenEmby.App.Playback
{
    public sealed class WinRtNativePlaybackEngine : INativePlaybackEngine
    {
        private readonly NativePlaybackEngine _engine;

        public WinRtNativePlaybackEngine(NativePlaybackEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _engine.StateChanged += Engine_OnStateChanged;
        }

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public long CurrentPositionTicks => _engine.CurrentPositionTicks();

        public PlaybackBackendCapabilities Capabilities { get; } =
            new PlaybackBackendCapabilities(
                PlaybackBackendFeature.DirectPlayHttp |
                PlaybackBackendFeature.Hevc |
                PlaybackBackendFeature.HevcMain10 |
                PlaybackBackendFeature.Hdr10 |
                PlaybackBackendFeature.AudioStreamSwitching |
                PlaybackBackendFeature.SubtitleStreamSwitching |
                PlaybackBackendFeature.MediaSourceSwitching);

        public PlaybackDisplayStatus DisplayStatus
        {
            get
            {
                var status = _engine.DisplayStatus();
                return new PlaybackDisplayStatus(
                    MapHdrStatus(status.HdrStatus),
                    status.IsHdrDisplayAvailable,
                    status.IsHdrOutputActive,
                    status.Message ?? "");
            }
        }

        public Task OpenAsync(NativePlaybackOpenRequest request)
        {
            var nativeRequest = new NextGenEmby.Native.NativePlaybackOpenRequest
            {
                ItemId = request.ItemId,
                MediaSourceId = request.MediaSourceId,
                DirectStreamUrl = request.DirectStreamUrl,
                StartPositionTicks = request.StartPositionTicks,
                HasAudioStreamIndex = request.AudioStreamIndex.HasValue,
                AudioStreamIndex = request.AudioStreamIndex.GetValueOrDefault(),
                HasSubtitleStreamIndex = request.SubtitleStreamIndex.HasValue,
                SubtitleStreamIndex = request.SubtitleStreamIndex.GetValueOrDefault()
            };

            return _engine.OpenAsync(nativeRequest).AsTask();
        }

        public Task PauseAsync() => _engine.PauseAsync().AsTask();

        public Task ResumeAsync() => _engine.ResumeAsync().AsTask();

        public Task SeekAsync(long positionTicks) => _engine.SeekAsync(positionTicks).AsTask();

        public Task StopAsync() => _engine.StopAsync().AsTask();

        private void Engine_OnStateChanged(NativePlaybackState state, string message)
        {
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(MapState(state), message ?? ""));
        }

        private static PlaybackState MapState(NativePlaybackState state)
        {
            switch (state)
            {
                case NativePlaybackState.NativePlaybackState_Opening:
                    return PlaybackState.Opening;
                case NativePlaybackState.NativePlaybackState_Buffering:
                    return PlaybackState.Buffering;
                case NativePlaybackState.NativePlaybackState_Playing:
                    return PlaybackState.Playing;
                case NativePlaybackState.NativePlaybackState_Paused:
                    return PlaybackState.Paused;
                case NativePlaybackState.NativePlaybackState_Failed:
                    return PlaybackState.Failed;
                default:
                    return PlaybackState.Stopped;
            }
        }

        private static HdrOutputStatus MapHdrStatus(NativeHdrStatus status)
        {
            switch (status)
            {
                case NativeHdrStatus.NativeHdrStatus_Unsupported:
                    return HdrOutputStatus.Unsupported;
                case NativeHdrStatus.NativeHdrStatus_Off:
                    return HdrOutputStatus.Off;
                case NativeHdrStatus.NativeHdrStatus_On:
                    return HdrOutputStatus.On;
                case NativeHdrStatus.NativeHdrStatus_Failed:
                    return HdrOutputStatus.Failed;
                default:
                    return HdrOutputStatus.Unknown;
            }
        }
    }
}
```

- [ ] **Step 3: Add a native rendering surface**

Modify `src/NextGenEmby.App/Views/PlaybackPage.xaml` so the media area contains both fallback and native surfaces:

```xml
<Grid Grid.Row="1">
  <MediaPlayerElement x:Name="PlayerElement"
                      AreTransportControlsEnabled="False"
                      Stretch="Uniform" />
  <SwapChainPanel x:Name="NativeSurface"
                  Visibility="Collapsed" />
</Grid>
```

- [ ] **Step 4: Wire the native backend in `PlaybackPage.xaml.cs`**

In `PlaybackPage.xaml.cs`, construct the backend like this after `InitializeComponent()`:

```csharp
var nativeEngine = new WinRtNativePlaybackEngine(new NextGenEmby.Native.NativePlaybackEngine());
_backend = new NativeDirectXPlaybackBackend(nativeEngine);
NativeSurface.Visibility = Visibility.Visible;
PlayerElement.Visibility = Visibility.Collapsed;
```

Keep `SystemMediaPlaybackBackend` behind a local fallback switch until the native renderer displays frames.

- [ ] **Step 5: Build solution**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add src\NextGenEmby.App NextGenXboxEmby.sln
git commit -m "feat: bridge native playback engine into UWP app"
```

Expected: commit succeeds.

---

### Task 5: Implement Kodi-Derived HDR Display Controller

**Files:**
- Create: `src/NextGenEmby.Native/HdrDisplayController.h`
- Create: `src/NextGenEmby.Native/HdrDisplayController.cpp`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.h`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.cpp`

- [ ] **Step 1: Add the HDR controller interface**

Create `src/NextGenEmby.Native/HdrDisplayController.h`:

```cpp
#pragma once

#include "NativePlaybackEngine.g.h"

namespace winrt::NextGenEmby::Native::implementation
{
    struct HdrDisplaySnapshot
    {
        NextGenEmby::Native::NativeHdrStatus Status{NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unknown};
        bool IsHdrDisplayAvailable{false};
        bool IsHdrOutputActive{false};
        winrt::hstring Message{};
    };

    class HdrDisplayController
    {
    public:
        HdrDisplaySnapshot Probe();
        HdrDisplaySnapshot EnterHdr10();
        HdrDisplaySnapshot RestoreInitialState();

    private:
        HdrDisplaySnapshot Apply(bool enableHdr);

        bool m_hasInitialState{false};
        bool m_initialHdrActive{false};
    };
}
```

- [ ] **Step 2: Implement probe and mode switching**

Create `src/NextGenEmby.Native/HdrDisplayController.cpp`:

```cpp
#include "pch.h"
#include "HdrDisplayController.h"

#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.Graphics.Display.h>
#include <winrt/Windows.Graphics.Display.Core.h>

namespace winrt::NextGenEmby::Native::implementation
{
    using namespace winrt::Windows::Foundation::Metadata;
    using namespace winrt::Windows::Graphics::Display;
    using namespace winrt::Windows::Graphics::Display::Core;

    HdrDisplaySnapshot HdrDisplayController::Probe()
    {
        HdrDisplaySnapshot snapshot;

        auto info = DisplayInformation::GetForCurrentView();
        if (info != nullptr)
        {
            auto advanced = info.GetAdvancedColorInfo();
            if (advanced != nullptr &&
                advanced.CurrentAdvancedColorKind() == AdvancedColorKind::HighDynamicRange)
            {
                snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_On;
                snapshot.IsHdrDisplayAvailable = true;
                snapshot.IsHdrOutputActive = true;
                return snapshot;
            }
        }

        if (!ApiInformation::IsTypePresent(L"Windows.Graphics.Display.Core.HdmiDisplayInformation"))
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"HdmiDisplayInformation is unavailable.";
            return snapshot;
        }

        auto hdmi = HdmiDisplayInformation::GetForCurrentView();
        if (hdmi == nullptr)
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"No HDMI display information is available.";
            return snapshot;
        }

        auto current = hdmi.GetCurrentDisplayMode();
        snapshot.IsHdrDisplayAvailable = current != nullptr && current.IsSmpte2084Supported();
        snapshot.IsHdrOutputActive = false;
        snapshot.Status = snapshot.IsHdrDisplayAvailable
            ? NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Off
            : NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
        return snapshot;
    }

    HdrDisplaySnapshot HdrDisplayController::EnterHdr10()
    {
        auto current = Probe();
        if (!m_hasInitialState)
        {
            m_hasInitialState = true;
            m_initialHdrActive = current.IsHdrOutputActive;
        }

        if (current.IsHdrOutputActive)
        {
            return current;
        }

        return Apply(true);
    }

    HdrDisplaySnapshot HdrDisplayController::RestoreInitialState()
    {
        if (!m_hasInitialState)
        {
            return Probe();
        }

        return Apply(m_initialHdrActive);
    }

    HdrDisplaySnapshot HdrDisplayController::Apply(bool enableHdr)
    {
        HdrDisplaySnapshot snapshot;

        if (!ApiInformation::IsTypePresent(L"Windows.Graphics.Display.Core.HdmiDisplayInformation"))
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"HdmiDisplayInformation is unavailable.";
            return snapshot;
        }

        auto hdmi = HdmiDisplayInformation::GetForCurrentView();
        if (hdmi == nullptr)
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"No HDMI display information is available.";
            return snapshot;
        }

        auto mode = hdmi.GetCurrentDisplayMode();
        if (mode == nullptr)
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Failed;
            snapshot.Message = L"Current HDMI display mode is unavailable.";
            return snapshot;
        }

        auto option = enableHdr ? HdmiDisplayHdrOption::Eotf2084 : HdmiDisplayHdrOption::None;
        auto operation = hdmi.RequestSetCurrentDisplayModeAsync(mode, option);
        auto result = operation.get();

        if (!result)
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Failed;
            snapshot.Message = enableHdr ? L"Failed to enter HDR10 display mode." : L"Failed to restore SDR display mode.";
            return snapshot;
        }

        snapshot.IsHdrDisplayAvailable = true;
        snapshot.IsHdrOutputActive = enableHdr;
        snapshot.Status = enableHdr
            ? NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_On
            : NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Off;
        return snapshot;
    }
}
```

- [ ] **Step 3: Expose display status from `NativePlaybackEngine`**

Add a `HdrDisplayController m_hdr;` field to `NativePlaybackEngine.h`, call `m_hdr.Probe()` in the constructor, call `m_hdr.EnterHdr10()` before HDR playback starts, and call `m_hdr.RestoreInitialState()` from `StopAsync()`.

- [ ] **Step 4: Build native component**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.Native\NextGenEmby.Native.vcxproj /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```powershell
git add src\NextGenEmby.Native
git commit -m "feat: add native HDR display controller"
```

Expected: commit succeeds.

---

### Task 6: Add D3D11 and DXGI Swapchain Resources

**Files:**
- Create: `src/NextGenEmby.Native/DxDeviceResources.h`
- Create: `src/NextGenEmby.Native/DxDeviceResources.cpp`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.idl`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.h`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.cpp`
- Modify: `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`

- [ ] **Step 1: Extend IDL to accept a `SwapChainPanel`**

Add this method to `NativePlaybackEngine` in `NativePlaybackEngine.idl`:

```cpp
void AttachSurface(Windows.UI.Xaml.Controls.SwapChainPanel panel);
```

- [ ] **Step 2: Add C# wrapper method**

Add this method to `WinRtNativePlaybackEngine`:

```csharp
public void AttachSurface(Windows.UI.Xaml.Controls.SwapChainPanel panel)
{
    _engine.AttachSurface(panel);
}
```

- [ ] **Step 3: Add DXGI resource manager skeleton**

Create `src/NextGenEmby.Native/DxDeviceResources.h`:

```cpp
#pragma once

#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <wrl/client.h>
#include <winrt/Windows.UI.Xaml.Controls.h>

namespace winrt::NextGenEmby::Native::implementation
{
    class DxDeviceResources
    {
    public:
        void AttachSurface(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel);
        void CreateDevice();
        void CreateSwapChain(uint32_t width, uint32_t height, bool useTenBit);
        bool SetHdr10ColorSpace();
        bool SetSdrColorSpace();
        bool SetHdr10Metadata(DXGI_HDR_METADATA_HDR10 const& metadata);

    private:
        Microsoft::WRL::ComPtr<ID3D11Device> m_device;
        Microsoft::WRL::ComPtr<ID3D11DeviceContext> m_context;
        Microsoft::WRL::ComPtr<IDXGISwapChain3> m_swapChain;
        winrt::Windows::UI::Xaml::Controls::SwapChainPanel m_panel{nullptr};
    };
}
```

- [ ] **Step 4: Implement color-space methods**

Create `src/NextGenEmby.Native/DxDeviceResources.cpp` with these methods first:

```cpp
#include "pch.h"
#include "DxDeviceResources.h"

#include <windows.ui.xaml.media.dxinterop.h>

namespace winrt::NextGenEmby::Native::implementation
{
    bool DxDeviceResources::SetHdr10ColorSpace()
    {
        if (!m_swapChain)
        {
            return false;
        }

        return SUCCEEDED(m_swapChain->SetColorSpace1(DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020));
    }

    bool DxDeviceResources::SetSdrColorSpace()
    {
        if (!m_swapChain)
        {
            return false;
        }

        return SUCCEEDED(m_swapChain->SetColorSpace1(DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709));
    }

    bool DxDeviceResources::SetHdr10Metadata(DXGI_HDR_METADATA_HDR10 const& metadata)
    {
        Microsoft::WRL::ComPtr<IDXGISwapChain4> swapChain4;
        if (!m_swapChain || FAILED(m_swapChain.As(&swapChain4)))
        {
            return false;
        }

        return SUCCEEDED(swapChain4->SetHDRMetaData(
            DXGI_HDR_METADATA_TYPE_HDR10,
            sizeof(metadata),
            const_cast<DXGI_HDR_METADATA_HDR10*>(&metadata)));
    }
}
```

Complete `CreateDevice`, `AttachSurface`, and `CreateSwapChain` in the same file using `D3D11CreateDevice`, `IDXGIFactory2::CreateSwapChainForComposition`, and `ISwapChainPanelNative::SetSwapChain`.

- [ ] **Step 5: Preserve Kodi's Xbox swapchain rule**

In HDR/SDR transitions, call `SetHdr10ColorSpace()` and `SetSdrColorSpace()` on the existing swapchain. Do not destroy and recreate the swapchain during HDR toggles on Xbox. This mirrors ADR 0001's Kodi finding.

- [ ] **Step 6: Build solution**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```powershell
git add src\NextGenEmby.Native src\NextGenEmby.App
git commit -m "feat: add native DXGI swapchain resources"
```

Expected: commit succeeds.

---

### Task 7: Add Native Playback Graph and Direct HTTP Input

**Files:**
- Create: `src/NextGenEmby.Native/Media/PlaybackGraph.h`
- Create: `src/NextGenEmby.Native/Media/PlaybackGraph.cpp`
- Create: `src/NextGenEmby.Native/Media/HttpMediaInput.h`
- Create: `src/NextGenEmby.Native/Media/HttpMediaInput.cpp`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.h`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.cpp`

- [ ] **Step 1: Add playback graph lifecycle**

Create `src/NextGenEmby.Native/Media/PlaybackGraph.h`:

```cpp
#pragma once

#include "../NativePlaybackEngine.g.h"

namespace winrt::NextGenEmby::Native::implementation
{
    class PlaybackGraph
    {
    public:
        void Open(NextGenEmby::Native::NativePlaybackOpenRequest const& request);
        void Pause();
        void Resume();
        void Seek(int64_t positionTicks);
        void Stop();
        int64_t CurrentPositionTicks() const noexcept;

    private:
        winrt::hstring m_url;
        int64_t m_positionTicks{0};
        bool m_open{false};
        bool m_paused{false};
    };
}
```

- [ ] **Step 2: Implement lifecycle and validation**

Create `src/NextGenEmby.Native/Media/PlaybackGraph.cpp`:

```cpp
#include "pch.h"
#include "PlaybackGraph.h"

namespace winrt::NextGenEmby::Native::implementation
{
    void PlaybackGraph::Open(NextGenEmby::Native::NativePlaybackOpenRequest const& request)
    {
        if (request == nullptr || request.DirectStreamUrl().empty())
        {
            throw winrt::hresult_invalid_argument(L"Direct stream URL is required.");
        }

        m_url = request.DirectStreamUrl();
        m_positionTicks = request.StartPositionTicks();
        m_open = true;
        m_paused = false;
    }

    void PlaybackGraph::Pause()
    {
        if (m_open)
        {
            m_paused = true;
        }
    }

    void PlaybackGraph::Resume()
    {
        if (m_open)
        {
            m_paused = false;
        }
    }

    void PlaybackGraph::Seek(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Seek position cannot be negative.");
        }

        m_positionTicks = positionTicks;
    }

    void PlaybackGraph::Stop()
    {
        m_url.clear();
        m_positionTicks = 0;
        m_open = false;
        m_paused = false;
    }

    int64_t PlaybackGraph::CurrentPositionTicks() const noexcept
    {
        return m_positionTicks;
    }
}
```

- [ ] **Step 3: Wire graph into `NativePlaybackEngine`**

Add `std::unique_ptr<PlaybackGraph> m_graph;` to the engine and route `OpenAsync`, `PauseAsync`, `ResumeAsync`, `SeekAsync`, and `StopAsync` through it. On exceptions, raise `NativePlaybackState_Failed` with the exception message.

- [ ] **Step 4: Add HTTP input class**

Create `HttpMediaInput.h/.cpp` as the direct-stream reader boundary for FFmpeg integration. It should accept an absolute `http` or `https` URL and expose `Open`, `Read`, and `Close` methods. Use FFmpeg AVIO after Task 8 adds FFmpeg libraries; before FFmpeg is linked, keep the class as a validating shell that opens no network connection.

- [ ] **Step 5: Build native component**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.Native\NextGenEmby.Native.vcxproj /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add src\NextGenEmby.Native
git commit -m "feat: add native playback graph skeleton"
```

Expected: commit succeeds.

---

### Task 8: Add Native Dependency Record and FFmpeg Decode Boundary

**Files:**
- Create: `docs/native-dependencies.md`
- Create: `src/NextGenEmby.Native/Media/VideoDecoder.h`
- Create: `src/NextGenEmby.Native/Media/VideoDecoder.cpp`
- Modify: `src/NextGenEmby.Native/NextGenEmby.Native.vcxproj`

- [ ] **Step 1: Record dependency decision**

Create `docs/native-dependencies.md`:

```markdown
# Native Dependencies

Date: 2026-07-05

## FFmpeg

Purpose: demux direct-play Emby HTTP streams and decode HEVC Main/Main10 with D3D11VA.

Required libraries:

- avformat
- avcodec
- avutil
- swresample
- swscale
- avfilter only if ASS subtitle rendering is delegated to FFmpeg

Build requirements:

- x64
- UWP-compatible
- D3D11VA enabled
- network protocols enabled for http and https
- GPL/LGPL mode recorded with the exact configure line

Binary policy:

- Do not commit large third-party binaries to the repo.
- Commit build scripts and provenance docs.
- Store local binaries under ignored `native-deps/ffmpeg/` and keep app packaging scripts responsible for copying runtime binaries into the UWP package.
```

- [ ] **Step 2: Add decoder interface**

Create `src/NextGenEmby.Native/Media/VideoDecoder.h`:

```cpp
#pragma once

#include <d3d11_4.h>
#include <memory>
#include <optional>
#include <wrl/client.h>

namespace winrt::NextGenEmby::Native::implementation
{
    enum class VideoHdrKind
    {
        None,
        Hdr10,
        Hlg
    };

    struct DecodedVideoFrame
    {
        Microsoft::WRL::ComPtr<ID3D11Texture2D> Texture;
        uint32_t Width{0};
        uint32_t Height{0};
        DXGI_FORMAT Format{DXGI_FORMAT_UNKNOWN};
        VideoHdrKind HdrKind{VideoHdrKind::None};
        std::optional<DXGI_HDR_METADATA_HDR10> Hdr10Metadata;
        int64_t PositionTicks{0};
    };

    class VideoDecoder
    {
    public:
        void Open(winrt::hstring const& url, int32_t selectedVideoStreamIndex);
        std::optional<DecodedVideoFrame> TryReadFrame();
        void Seek(int64_t positionTicks);
        void Close();
    };
}
```

- [ ] **Step 3: Add a compile-time stub**

Create `src/NextGenEmby.Native/Media/VideoDecoder.cpp`:

```cpp
#include "pch.h"
#include "VideoDecoder.h"

namespace winrt::NextGenEmby::Native::implementation
{
    void VideoDecoder::Open(winrt::hstring const& url, int32_t)
    {
        if (url.empty())
        {
            throw winrt::hresult_invalid_argument(L"Video URL is required.");
        }
    }

    std::optional<DecodedVideoFrame> VideoDecoder::TryReadFrame()
    {
        return std::nullopt;
    }

    void VideoDecoder::Seek(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Seek position cannot be negative.");
        }
    }

    void VideoDecoder::Close()
    {
    }
}
```

- [ ] **Step 4: Link FFmpeg after the stub builds**

Modify `NextGenEmby.Native.vcxproj` with include/lib paths from `docs/native-dependencies.md`. Add `avformat.lib`, `avcodec.lib`, `avutil.lib`, `swresample.lib`, and `swscale.lib` to linker inputs. Replace the stub internals with FFmpeg open/demux/decode code while preserving the public `VideoDecoder` interface.

- [ ] **Step 5: Build native component**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.Native\NextGenEmby.Native.vcxproj /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add docs\native-dependencies.md src\NextGenEmby.Native
git commit -m "feat: add native video decoder boundary"
```

Expected: commit succeeds.

---

### Task 9: Render Video Frames and Apply HDR Metadata

**Files:**
- Create: `src/NextGenEmby.Native/Media/VideoRenderer.h`
- Create: `src/NextGenEmby.Native/Media/VideoRenderer.cpp`
- Modify: `src/NextGenEmby.Native/DxDeviceResources.h`
- Modify: `src/NextGenEmby.Native/DxDeviceResources.cpp`
- Modify: `src/NextGenEmby.Native/Media/PlaybackGraph.h`
- Modify: `src/NextGenEmby.Native/Media/PlaybackGraph.cpp`

- [ ] **Step 1: Add renderer interface**

Create `src/NextGenEmby.Native/Media/VideoRenderer.h`:

```cpp
#pragma once

#include "VideoDecoder.h"
#include "../DxDeviceResources.h"

namespace winrt::NextGenEmby::Native::implementation
{
    class VideoRenderer
    {
    public:
        explicit VideoRenderer(DxDeviceResources& deviceResources);

        void Render(DecodedVideoFrame const& frame);
        void ClearToBlack();

    private:
        DxDeviceResources& m_deviceResources;
        VideoHdrKind m_currentHdrKind{VideoHdrKind::None};
    };
}
```

- [ ] **Step 2: Implement HDR metadata behavior**

Create `src/NextGenEmby.Native/Media/VideoRenderer.cpp`:

```cpp
#include "pch.h"
#include "VideoRenderer.h"

namespace winrt::NextGenEmby::Native::implementation
{
    VideoRenderer::VideoRenderer(DxDeviceResources& deviceResources)
        : m_deviceResources(deviceResources)
    {
    }

    void VideoRenderer::Render(DecodedVideoFrame const& frame)
    {
        if (frame.HdrKind == VideoHdrKind::Hdr10)
        {
            if (frame.Hdr10Metadata.has_value())
            {
                m_deviceResources.SetHdr10Metadata(frame.Hdr10Metadata.value());
            }

            m_deviceResources.SetHdr10ColorSpace();
        }
        else if (m_currentHdrKind != VideoHdrKind::None)
        {
            m_deviceResources.SetSdrColorSpace();
        }

        m_currentHdrKind = frame.HdrKind;
        // Copy or sample the decoded texture into the swapchain back buffer here.
    }

    void VideoRenderer::ClearToBlack()
    {
        m_currentHdrKind = VideoHdrKind::None;
        m_deviceResources.SetSdrColorSpace();
    }
}
```

- [ ] **Step 3: Complete texture presentation**

Use D3D11 video processing or shaders to draw NV12/P010 decoded textures to the swapchain back buffer. Present with `IDXGISwapChain::Present(1, 0)`. Keep P010 as 10-bit for HDR10.

- [ ] **Step 4: Build and smoke on Local Machine**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

Manual smoke: launch Playback page and confirm the native surface clears to black without crashing.

- [ ] **Step 5: Commit**

```powershell
git add src\NextGenEmby.Native
git commit -m "feat: render native video frames through DXGI"
```

Expected: commit succeeds.

---

### Task 10: Add Audio and Subtitle Track Control

**Files:**
- Create: `src/NextGenEmby.Native/Media/AudioRenderer.h`
- Create: `src/NextGenEmby.Native/Media/AudioRenderer.cpp`
- Create: `src/NextGenEmby.Native/Media/SubtitleRenderer.h`
- Create: `src/NextGenEmby.Native/Media/SubtitleRenderer.cpp`
- Modify: `src/NextGenEmby.Native/Media/PlaybackGraph.h`
- Modify: `src/NextGenEmby.Native/Media/PlaybackGraph.cpp`

- [ ] **Step 1: Add audio renderer boundary**

Create `src/NextGenEmby.Native/Media/AudioRenderer.h`:

```cpp
#pragma once

namespace winrt::NextGenEmby::Native::implementation
{
    class AudioRenderer
    {
    public:
        void Open(int32_t selectedAudioStreamIndex, bool hasSelection);
        void Start();
        void Pause();
        void Resume();
        void Stop();
        void SwitchStream(int32_t audioStreamIndex);
    };
}
```

- [ ] **Step 2: Add subtitle renderer boundary**

Create `src/NextGenEmby.Native/Media/SubtitleRenderer.h`:

```cpp
#pragma once

#include <optional>

namespace winrt::NextGenEmby::Native::implementation
{
    class SubtitleRenderer
    {
    public:
        void Open(std::optional<int32_t> selectedSubtitleStreamIndex);
        void Disable();
        void SwitchStream(int32_t subtitleStreamIndex);
        void RenderAt(int64_t positionTicks);
    };
}
```

- [ ] **Step 3: Implement first pass**

Implement `AudioRenderer` with XAudio2 for UWP. Implement `SubtitleRenderer` first for text subtitles by drawing with DirectWrite over the video back buffer. Preserve the public boundaries above so ASS/PGS support can be added without changing C#.

- [ ] **Step 4: Route selected indexes from `NativePlaybackOpenRequest`**

In `PlaybackGraph::Open`, pass `request.AudioStreamIndex()` only when `request.HasAudioStreamIndex()` is true, and pass `request.SubtitleStreamIndex()` only when `request.HasSubtitleStreamIndex()` is true.

- [ ] **Step 5: Build solution**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add src\NextGenEmby.Native
git commit -m "feat: add native audio and subtitle controls"
```

Expected: commit succeeds.

---

### Task 11: Wire Native Progress and Emby Playback Reporting

**Files:**
- Modify: `src/NextGenEmby.Core/Playback/PlaybackStateChangedEventArgs.cs`
- Modify: `src/NextGenEmby.Core/Playback/PlaybackOrchestrator.cs`
- Modify: `tests/NextGenEmby.Core.Tests/Playback/PlaybackOrchestratorTests.cs`
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`

- [ ] **Step 1: Add position-aware playback events**

Extend `PlaybackStateChangedEventArgs` with `PositionTicks`:

```csharp
public PlaybackStateChangedEventArgs(PlaybackState state, string message = "", long? positionTicks = null)
{
    State = state;
    Message = message ?? "";
    PositionTicks = positionTicks;
}

public long? PositionTicks { get; }
```

- [ ] **Step 2: Add orchestrator tests**

Add a test that backend state changes with `PositionTicks` are re-emitted and can be used by the UWP page for progress reporting.

- [ ] **Step 3: Update native wrapper**

Extend the WinRT event or add a polling timer so `PlaybackPage.xaml.cs` can report progress through the existing `EmbyApiClient.ReportPlaybackProgressAsync` path.

- [ ] **Step 4: Run managed tests**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Expected: all tests pass.

- [ ] **Step 5: Build solution**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add src tests
git commit -m "feat: report native playback progress"
```

Expected: commit succeeds.

---

### Task 12: Xbox Hardware Smoke Matrix

**Files:**
- Create: `docs/native-playback-smoke-tests.md`
- Modify: `docs/foundation-status.md`

- [ ] **Step 1: Create smoke matrix**

Create `docs/native-playback-smoke-tests.md`:

```markdown
# Native Playback Smoke Tests

Date: 2026-07-05

## Device

- Xbox model:
- OS version:
- TV/monitor model:
- HDMI mode:
- HDR enabled in Xbox settings:

## Test Files

1. 1080p H.264 SDR, AAC or AC3 audio, no subtitles
2. 4K HEVC SDR, AAC or AC3 audio, no subtitles
3. 4K HEVC Main10 HDR10, AC3/EAC3 audio, no subtitles
4. 4K HEVC Main10 HDR10, two audio tracks
5. 4K HEVC Main10 HDR10, text subtitle track

## Checks

- Login succeeds.
- Library item opens PlaybackInfo.
- Native backend starts direct stream without transcoding.
- Pause, resume, seek, stop work.
- Media source switch restarts at current position.
- Audio stream switch selects the requested track.
- Subtitle switch enables, changes, and disables subtitles.
- HDR10 file enters HDR output.
- SDR file restores SDR output after HDR playback.
- Stop restores the initial display state.
- App suspend/resume does not leave the display in the wrong HDR state.
- Emby progress updates appear on the server.

## Results

| File | Direct Play | Video | Audio | Subtitles | HDR State | Progress | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1080p H.264 SDR | | | | | | | |
| 4K HEVC SDR | | | | | | | |
| 4K HEVC HDR10 | | | | | | | |
| HDR10 multi-audio | | | | | | | |
| HDR10 subtitles | | | | | | | |
```

- [ ] **Step 2: Build app package for Xbox Dev Mode**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never
```

Expected: app package artifacts are produced under `src\NextGenEmby.App\AppPackages` or the configured UWP output folder.

- [ ] **Step 3: Run hardware smoke**

Deploy through Visual Studio or Xbox Device Portal in Dev Mode. Fill in `docs/native-playback-smoke-tests.md` with actual results.

- [ ] **Step 4: Update foundation status**

Add a `Native Playback Hardware Verification` section to `docs/foundation-status.md` summarizing which files passed and which failed.

- [ ] **Step 5: Commit**

```powershell
git add docs\native-playback-smoke-tests.md docs\foundation-status.md
git commit -m "docs: record native playback smoke results"
```

Expected: commit succeeds.

---

## Self-Review Notes

Spec coverage:

- Xbox-only interaction remains in the existing UWP pages and native `SwapChainPanel`.
- Emby direct-play URLs come from the already-tested `PlaybackInfo` path.
- Media source, audio stream, and subtitle selection flow through `PlaybackDescriptor` and `NativePlaybackOpenRequest`.
- HDR/HEVC ownership moves to the native component.
- Kodi HDR findings are represented in `HdrDisplayController`, `DxDeviceResources`, and the "do not recreate swapchain on Xbox HDR toggle" rule.
- Progress reporting is preserved through the managed app page and existing Emby progress API.

Known execution gates:

- Task 0 must pass before C++/WinRT work can be verified.
- FFmpeg binary policy must be recorded in `docs/native-dependencies.md` before committing native decode dependencies.
- HDR success can only be verified on Xbox hardware connected to an HDR10 display.
