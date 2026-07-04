# Xbox Emby Player Design

Date: 2026-07-05

## Goal

Build an Xbox-only Emby home theater client for personal daily use. The first version prioritizes a complete, reliable movie and TV playback path over broad Emby feature coverage.

The product should avoid the iPlayX failure mode: opening a video is not enough. Playback must support the core operations expected from a serious living-room player, including media version switching, subtitle switching, audio track switching, playback progress reporting, HDR/HEVC handling, and clear Xbox gamepad interaction.

## Product Scope

### In Scope

- Xbox-only UWP media app.
- Development via Xbox Dev Mode sideloading.
- Daily-use distribution target via Microsoft Store private audience.
- Single Emby server.
- Manual server URL entry.
- Username/password or API-token login.
- Remember server, user, and access token.
- Movie and TV libraries.
- Home page with Continue Watching, Recently Added, Movies, TV, and Search.
- Library grids for movies and TV.
- Detail pages with poster, backdrop, overview, year, runtime, resolution, HDR/SDR, video codec, audio format, and subtitle summary.
- Direct/original stream playback first.
- Media version switching, for example 4K HDR, 1080p SDR, or alternate cuts.
- Embedded subtitle switching.
- External SRT and ASS subtitle reading and switching.
- Audio track switching.
- Audio track metadata display, including language, codec, channel count, and formats such as Atmos, TrueHD, DTS, DTS-HD, AC3, AAC.
- Playback progress reporting to Emby.
- Resume playback and played/completed state updates.

### Out of Scope for Version 1

- Music.
- Photos.
- Live TV and DVR.
- Online subtitle search or download.
- Multi-server and multi-account management.
- Emby server administration.
- Transcode quality or bitrate switching.
- Audio passthrough and AVR-specific settings.
- Non-Xbox platform adaptation.
- Kodi media library, scraper, skin, plugin, PVR, UPnP, SMB/NFS browsing, or general source-management features.

## Chosen Approach

Use an independent Xbox app with a Kodi-grade native playback core.

- C# XAML + WinUI 2 for UWP provides the Xbox UI.
- C# implements Emby API access and playback orchestration.
- C++/WinRT + DirectX implements the native playback core.
- Kodi Xbox/UWP is the primary technical reference for HDR10 passthrough, HEVC/4K handling, DirectX rendering, DXGI color space handling, and display-state restoration.
- Kodi is not used as the product shell. The app keeps its own Emby-first UI and data model.

This approach is heavier than using a system media control or libVLC as the final player, but it targets the actual pain point: Xbox playback quality, HDR correctness, and complete in-playback controls.

## Architecture

### Xbox Shell and UI Layer

Technology: C# XAML + WinUI 2 for UWP.

Responsibilities:

- Login.
- Home screen.
- Movie and TV library pages.
- Search.
- Detail pages.
- Playback overlay.
- Settings.
- Xbox gamepad and remote interaction.
- Focus management, including Reveal Focus or equivalent high-visibility focus.

The UI should follow Xbox 10-foot guidance: dark, immersive, low-density, predictable, and optimized for D-pad/gamepad use.

### Emby Client Layer

Technology: C#.

Responsibilities:

- Authentication.
- Current user retrieval.
- Library and item queries.
- Image URL construction.
- PlaybackInfo requests.
- Media source and media stream parsing.
- Subtitle stream parsing.
- Audio stream parsing.
- Playback progress reporting.
- Played/completed status updates.

This layer exposes stable models to the UI and Playback Orchestrator. Pages should not build raw Emby API requests directly.

### Playback Orchestrator Layer

Technology: C#.

Responsibilities:

- Convert Emby playback metadata into native playback commands.
- Select the initial media version.
- Preserve all playable media versions for in-player switching.
- Track current subtitle, audio track, playback position, and media source.
- Coordinate version switching by saving the current position, opening the new version, and resuming near the same timestamp.
- Coordinate subtitle and audio-track switching.
- Synchronize native playback events back to Emby progress reporting.
- Present actionable playback errors to the UI.

This layer is the boundary between Emby semantics and native player mechanics.

### Native Playback Core

Technology: C++/WinRT + DirectX.

Responsibilities:

- Open original/direct media streams from Emby URLs.
- Demux and decode common movie/TV containers and streams.
- Support H.264 and HEVC, including HEVC Main10.
- Support 4K HDR10 playback.
- Manage HDR10 passthrough using the Kodi Xbox/UWP path as the primary reference.
- Configure DirectX rendering and DXGI color spaces correctly.
- Restore display/HDR state on controlled stop, playback failure, app suspend, and app resume. After an uncontrolled crash, recovery is best effort based on the last persisted playback/display state.
- Render subtitles.
- Switch subtitle streams.
- Switch audio tracks.
- Seek, pause, resume, and report buffering.
- Emit playback state, error, dropped-frame, and diagnostic events.

The native core should not understand Emby accounts, libraries, or UI state. It receives a playback descriptor and emits playback events.

## Playback Strategy

The playback model is client-decode-first.

The app defaults to original/direct stream playback. It does not depend on server-side transcoding for the main path. If the Emby server has no transcoding capability, the target experience should still work for compatible media.

Startup flow:

1. The user chooses Play from the detail page.
2. Emby Client requests PlaybackInfo.
3. Playback Orchestrator parses media sources, video streams, audio streams, subtitle streams, and resume position.
4. The default media version is selected.
5. Native Playback Core opens the original stream URL.
6. The native core initializes demuxing, decoding, audio, subtitles, and DirectX rendering.
7. For HDR10/HEVC content, the native core checks Xbox display and codec capability, enters HDR output when possible, configures the relevant DirectX/DXGI path, and records state for restoration.
8. Playback events flow back to the orchestrator.
9. Emby progress is reported periodically and on important lifecycle events.
10. Playback end marks the item complete; interruption saves the current position.

Required playback controls:

- Play and pause.
- Seek.
- Fast forward and rewind.
- Media version switching.
- Embedded subtitle switching.
- External SRT and ASS subtitle switching.
- Audio track switching.
- Playback information panel.

Playback information should include:

- Media version.
- Resolution.
- HDR/SDR state.
- Video codec.
- Audio codec.
- Audio channel layout.
- Subtitle stream.
- Bitrate when available.
- Container.
- Buffering state.
- Dropped frames or similar diagnostics when available.

Version 1 does not include transcode bitrate selection. If a media version fails to open, the user can return to version selection and choose another source.

## Xbox UI and Interaction

The app should use an Xbox Fluent TV style. This means Fluent-influenced native controls and motion, but adapted to TV rather than desktop.

Principles:

- Full-screen, dark, cinematic UI.
- Low information density.
- Clear current focus at all times.
- Predictable D-pad navigation.
- Minimal nested focus.
- Short paths to playback actions.
- Essential text and controls inside the TV-safe area.
- Nonessential backdrops and list surfaces can extend to screen edges for immersion.
- Avoid tooltips as required interaction affordances.
- Avoid subtle color differences as the only way to distinguish state on TV displays.

Pages:

- Login page: server URL, username, password/API token, system keyboard support.
- Home page: Continue Watching, Recently Added, Movies, TV, Search.
- Library page: movie or TV grid with basic sorting and search.
- Detail page: backdrop, poster, metadata, Play, version entry, and season/episode list for TV.
- Playback page: full-screen video with lightweight overlay.
- Settings page: server, account, playback preferences, default subtitle language, diagnostics toggle.

Playback overlay:

- Default overlay shows play/pause, progress, skip/seek affordances, current time, remaining time, and title.
- Advanced controls are hidden behind a More panel.
- The More panel contains subtitles, audio tracks, media versions, and playback information.
- Playback diagnostics do not remain visible during normal viewing.
- The overlay uses a dark translucent surface and should avoid covering subtitles whenever possible.

Gamepad mapping:

- A: confirm and primary action.
- B: close overlay, close panel, or go back.
- D-pad: move focus.
- Left/right: seek or move along the focused progress control.
- Menu: open More panel.
- View: open playback information directly.
- Y: search shortcut may be used on library/home screens, with a visible UI entry as a fallback.

## Error Handling

Errors should preserve the viewing session whenever the app can keep or restore current playback state. Unrecoverable playback errors return the user to the detail page or media-version selector with a clear next action.

- Login failure distinguishes server unreachable, authentication failure, HTTPS/certificate issue, and unknown errors.
- PlaybackInfo failure prompts retry.
- Original stream open failure reports that the selected media version is unavailable and allows version selection.
- Decode failure displays relevant container, video codec, and audio codec.
- HDR switching failure does not stop playback; the app falls back to non-HDR output and shows a warning in playback information.
- Subtitle loading failure does not stop playback; the selected subtitle is marked unavailable.
- Audio track switching failure restores the previous track.
- Media version switching failure restores the previous version and position.
- App interruption or playback crash should not lose Emby progress; the next detail-page visit should still offer resume when Emby has a recorded position.

## Testing Strategy

### Emby API Tests

- Authentication.
- Current user retrieval.
- Library discovery.
- Movie and TV item queries.
- Image URL generation.
- PlaybackInfo parsing.
- Media source/version parsing.
- Audio stream parsing.
- Subtitle stream parsing.
- Progress reporting.
- Played/completed state updates.

### Native Playback Tests

- 1080p H.264 SDR.
- 4K HEVC SDR.
- 4K HEVC HDR10.
- HEVC Main10.
- Common containers used by Emby libraries.
- Embedded subtitles.
- External SRT subtitles.
- External ASS subtitles.
- Multiple audio tracks.
- Seek, pause, resume, long playback, and playback stop.
- HDR entry and exit.
- Display state restoration after stop, failure, suspend, and resume.

### Xbox Experience Tests

- Gamepad-only login and navigation.
- Focus never disappears.
- B consistently closes the current layer or returns.
- Menu opens the More panel during playback.
- TV-safe area is respected.
- Overlay does not block subtitles in normal cases.
- Horizontal rows and grids keep the focused item visible.
- Dev Mode sideload package launches and plays.
- Store/private-audience package behavior is verified when ready.

### Initial Media Fixture Set

- 1080p H.264 SDR with embedded subtitle.
- 4K HEVC SDR.
- 4K HEVC HDR10.
- Multi-audio movie with several of AAC, AC3, TrueHD, DTS, DTS-HD, or Atmos.
- Embedded subtitle sample.
- External SRT sample.
- External ASS sample.
- One movie with multiple media versions, such as 4K HDR and 1080p SDR.

## Distribution Plan

Development starts with Xbox Dev Mode sideloading because it is the fastest path for native playback and HDR iteration.

The daily-use target is Microsoft Store private audience distribution:

- Package as a UWP media app.
- Submit through Partner Center.
- Restrict visibility to the developer's Microsoft account.
- Install and update from Retail mode once certification succeeds.

This avoids switching to Dev Mode for normal viewing while keeping distribution private.

## Reference Projects and Documents

- Kodi main repository: https://github.com/xbmc/xbmc
- Kodi Xbox HDR10 passthrough PR: https://github.com/xbmc/xbmc/pull/24083
- Kodi 21.3 Omega release notes: https://kodi.tv/article/kodi-21-3-omega-release/
- iPlay: https://github.com/saltpi/iPlay
- JellyCine: https://github.com/sureshfizzy/JellyCine
- Serenity for Android: https://github.com/NineWorlds/serenity-android
- Aether: https://github.com/DanielVNZ/Aether
- Jellyfin Xbox: https://github.com/jellyfin/jellyfin-xbox
- Jellyfin Android TV: https://github.com/jellyfin/jellyfin-androidtv
- Wholphin: https://github.com/damontecres/Wholphin
- Designing for Xbox and TV: https://learn.microsoft.com/en-us/windows/apps/design/devices/designing-for-tv
- Gamepad and remote control interactions: https://learn.microsoft.com/en-us/windows/uwp/ui-input/gamepad-and-remote-interactions
- Media players guidance: https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/media-playback
- Reveal Focus: https://learn.microsoft.com/en-us/windows/uwp/ui-input/reveal-focus
- WinUI 2 for UWP: https://learn.microsoft.com/en-us/windows/uwp/get-started/winui2/
- Xbox media app architecture: https://learn.microsoft.com/en-us/windows/uwp/apps-for-xbox/application-architecture
- Xbox media app development options: https://learn.microsoft.com/en-us/windows/uwp/apps-for-xbox/development-options
