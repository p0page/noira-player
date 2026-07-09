# Noira

Noira is an open-source TV media player for Emby-compatible personal media
servers, built for Xbox and Windows.

The project focuses on a controller-first TV interface, direct playback, and a
native Windows media pipeline for personal libraries. It is not an official
Emby, Microsoft, or Xbox product.

## Status

Noira is in early development. The current codebase contains a UWP app shell,
Emby-compatible authentication and library browsing, media details, search,
music, photo, Live TV surfaces, playback orchestration, and a native DirectX /
FFmpeg playback path under active validation.

Expect APIs, package identity, UI details, and playback behavior to change
before a stable release.

## Features

- TV-first navigation for keyboard, controller, and remote-style input.
- Emby-compatible server sign-in, session storage, library browsing, search,
  and playback progress reporting.
- Native playback path using DirectX, DXGI, XAudio2, and FFmpeg libraries.
- Early HDR, audio, subtitle, and media-source selection plumbing.
- Unit tests for the core Emby client and playback policy logic.

## Repository Layout

```text
src/NoiraPlayer.App      UWP app, XAML views, app services, storage
src/NoiraPlayer.Core     Emby API models, policies, testable core logic
src/NoiraPlayer.Native   C++/WinRT native playback engine
tests/                   Automated tests
docs/                    Documentation index, design, QA, playback-quality notes
tools/                   Local helper scripts
```

## Build

Recommended environment:

- Windows 10/11
- Visual Studio 2026 with UWP and C++ workloads for the modern .NET path
- Windows SDK 10.0.26100.0 or compatible
- .NET SDK 10 for the modern .NET path and test project
- NuGet package restore enabled

Run the default modern check. This builds/tests the modern path, publishes the
Native AOT UWP app, registers it locally, launches it, and captures a page
screenshot. The primary solution is `NoiraPlayer.sln`, and it is the VS2026
modern solution entry:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Check -Configuration Debug -Platform x64
```

Modern publish treats Native AOT/trimming warnings as blockers: any `IL2xxx`
or `IL3xxx` warning fails the publish gate while AOT is enabled.

Run the modern Release gate before treating a change as production-shape:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Check -Configuration Release -Platform x64
```

Run the modern playback-quality smoke after changes that touch playback,
Native AOT app-hosted capture, or native media diagnostics. The default smoke
requires source matching and captured runtime playback samples; use
`tools\Test-NoiraModernPlaybackQuality.ps1 -RequireQualityPass` for strict
threshold enforcement on a stable corpus:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target PlaybackCheck -Configuration Debug -Platform x64
```

The primary local readiness gate for merging or cutting over the modern path is
the modern-only cutover gate. It runs modern Debug and Release checks plus
strict playback-quality, and intentionally does not run the legacy build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target CutoverCheck -Platform x64
```

Build only, without registering or launching:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64
```

Register the Debug x64 loose layout for faster local app iteration:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Register-NoiraModernUwp.ps1 `
  -Configuration Debug `
  -Platform x64 `
  -Launch
```

Start with `docs/README.md` for the current documentation map, source-of-truth
rules, and validation notes.

## Third-Party Software

Noira uses third-party open-source components. In particular, native playback
uses FFmpeg libraries through `FFmpegInteropX.UWP.FFmpeg` version `8.1.2`.

Project source code is licensed under the MIT License. Third-party components
remain under their own licenses. See `NOTICE.md` for attribution, FFmpeg
distribution notes, and release compliance reminders.

## Trademark Notice

Noira is an independent open-source project. Emby, Microsoft, Xbox, Windows,
and other product names are trademarks of their respective owners. Their use in
this repository is only to describe compatibility, build targets, or required
platforms.

## License

Noira source code is available under the MIT License. See `LICENSE`.
