# ADR 0001: Kodi Xbox HDR Path for Native Playback Core

Date: 2026-07-05

## Status

Accepted for native-core research.

## Context

The app targets Xbox-only Emby playback. The first foundation slice uses a system-player backend only to verify UI, Emby API, and playback orchestration boundaries. The approved product direction requires a Kodi-grade native backend for direct-play 4K HEVC HDR10 playback, with no server transcoding assumed for the first playable Xbox target.

The local research checkout is Kodi `xbmc/xbmc` at `f0232910490189b97717bc5d309aec2e5751d6d3` under `.research/kodi-xbox`. That checkout is ignored by git; this ADR records the durable findings.

## Decision

The native playback core plan will use Kodi's Win10/UWP/Xbox DirectX path as the primary reference for HDR output behavior.

The foundation app keeps `IPlaybackBackend` stable so `SystemMediaPlaybackBackend` can be replaced by a native `NativeDirectXPlaybackBackend` without changing Emby API parsing, login/session storage, Xbox shell navigation, or playback orchestration.

The next playback backend should be a C++/WinRT UWP component with a C# adapter. It should own decode/render/display state and expose a small status surface back to C#:

- display HDR capability and current HDR output state
- selected media source, audio stream, and subtitle stream
- playback position, duration, pause/seek/buffering/end/error events
- HDR mode transitions and failures
- display-state restoration results

## Kodi Source Findings

Kodi separates HDR work into four responsibilities that we should mirror.

### 1. Xbox display mode and HDR toggle

Kodi uses `Windows.Graphics.Display.Core.HdmiDisplayInformation` on UWP/Xbox and requests HDR mode with `HdmiDisplayHdrOption::Eotf2084`.

Relevant local source:

- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10.cpp:317`
- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10.cpp:320`
- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10.cpp:361`
- `.research/kodi-xbox/xbmc/platform/win32/WIN32Util.cpp:1212`
- `.research/kodi-xbox/xbmc/platform/win32/WIN32Util.cpp:1235`
- `.research/kodi-xbox/xbmc/platform/win32/WIN32Util.cpp:1241`

Implication: the native backend should not treat HDR as just a media-player flag. It needs an Xbox display-mode service that can request the active HDMI mode with SDR or HDR10 EOTF.

### 2. HDR support and settings gate

Kodi routes HDR capability through the windowing abstraction and returns `HDR_STATUS` values for unsupported/off/on/toggle-failed states. It also gates auto switching behind an HDR-display setting.

Relevant local source:

- `.research/kodi-xbox/xbmc/HDRStatus.h:11`
- `.research/kodi-xbox/xbmc/windowing/WinSystem.h:242`
- `.research/kodi-xbox/xbmc/windowing/WinSystem.h:243`
- `.research/kodi-xbox/xbmc/windowing/WinSystem.cpp:320`
- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10DX.cpp:175`
- `.research/kodi-xbox/xbmc/windowing/win10/WinSystemWin10DX.cpp:179`

Implication: our native backend should have explicit `Unsupported`, `Off`, `On`, and `Failed` states instead of boolean-only HDR reporting. C# should be able to show or log why HDR was not entered.

### 3. Swapchain and DXGI HDR output

Kodi uses a 10-bit swapchain when HDR or 10-bit surfaces are needed. It sets HDR10 metadata through `IDXGISwapChain4::SetHDRMetaData` and switches transfer/color space with `IDXGISwapChain3::SetColorSpace1`.

Relevant local source:

- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:636`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:685`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:719`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1293`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1339`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1451`

Implication: the native backend must own the DXGI swapchain and must verify supported color spaces before entering playback. For HDR10 it should set `DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020`; for SDR restoration it should use `DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709`.

### 4. Xbox-specific swapchain preservation

Kodi has an important Xbox-only branch: when toggling HDR on Xbox, it changes the existing swapchain color space instead of destroying and recreating the swapchain. The source comment says recreating the swapchain on Xbox can lose native 4K quality.

Relevant local source:

- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1357`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1368`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1392`
- `.research/kodi-xbox/xbmc/rendering/dx/DeviceResources.cpp:1397`

Implication: the Xbox implementation should avoid swapchain recreation during HDR/SDR toggles unless later hardware testing proves it is safe. This is one of the main reasons to reference Kodi instead of using a generic desktop DirectX HDR sample.

### 5. Video metadata and HDR state machine

Kodi determines HDR type from FFmpeg stream metadata and frame side data, then the renderer updates HDR state per rendered buffer.

Relevant local source:

- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp:2546`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp:2554`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp:2556`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDDemuxers/DVDDemuxFFmpeg.cpp:2562`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDCodecs/Video/DVDVideoCodecFFmpeg.cpp:1107`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDCodecs/Video/DVDVideoCodecFFmpeg.cpp:1118`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/DVDCodecs/Video/DVDVideoCodecFFmpeg.cpp:1157`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:518`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:586`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:606`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:623`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:669`

Implication: Emby `MediaSource` metadata is useful for choosing a stream, but the native backend must still validate actual stream/frame metadata before entering HDR. The renderer should be able to switch between HDR10, HLG-as-PQ fallback, and SDR output based on decoded frames.

### 6. Display state restoration

Kodi stores the initial HDR state when configuring the renderer and restores it when playback stops. If auto-switching is disabled, it restores the appropriate DXGI color space directly.

Relevant local source:

- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:144`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:146`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:149`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:155`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:199`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:204`
- `.research/kodi-xbox/xbmc/cores/VideoPlayer/VideoRenderers/windows/RendererBase.cpp:212`

Implication: our native backend needs restoration paths for normal stop, playback error, backend disposal, app suspend, and app resume. `PlaybackOrchestrator.StopAsync` should remain the managed cleanup entry point, but native cleanup must also be defensive.

## Consequences

Native playback will be heavier than iPlayX-style system playback because it must own the HEVC decode/render/display loop. That weight is intentional: the missing core path is exactly HDR/HEVC/audio/subtitle control on Xbox.

The first native-core plan should avoid broad Kodi feature parity. It should implement a narrow vertical slice:

- direct-play HTTP input from Emby
- one selected media source
- audio and subtitle track switching
- HEVC Main/Main10 decode path
- HDR10 output and SDR restoration
- playback progress callbacks to existing Emby progress reporting
- diagnostic logging for HDR mode, DXGI color space, metadata, and failures

Transcoding remains out of scope for this phase.

## Native-Core Acceptance Checklist

The next plan must create:

- a C++/WinRT UWP component project
- a C# adapter named `NativeDirectXPlaybackBackend`
- a `PlaybackDescriptor` bridge into native input/open options
- HDR capability detection with explicit status codes
- HDR entry and exit methods
- 10-bit swapchain and DXGI color-space management
- HDR10 metadata propagation
- display state restoration on stop, failure, suspend, and resume
- fixture-based tests or hardware smoke scripts for 1080p H.264 SDR, 4K HEVC SDR, and 4K HEVC HDR10

## References

- Kodi repository: https://github.com/xbmc/xbmc
- Kodi Xbox HDR10 passthrough PR: https://github.com/xbmc/xbmc/pull/24083
- Kodi 21.3 release notes: https://kodi.tv/article/kodi-21-3-omega-release/
