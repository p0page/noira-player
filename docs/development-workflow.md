# Noira Development Workflow

This document records the current local development and verification workflow.
It does not replace final MSIX packaging, signing, Xbox hardware validation, or
release qualification.

## Modern .NET / VS2026 Default Entry

The repository build entry point is the modern .NET / VS2026 path. It defaults
to `NoiraPlayer.sln`, the SDK-style UWP app project, Native AOT publish, local
registration, app launch, and page evidence capture. The modern path requires
.NET SDK 10; `tools\Build-Noira.ps1` checks `dotnet --list-sdks` before running
modern targets.

Run the default Debug gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Check -Configuration Debug -Platform x64
```

Run the Release gate before treating a change as production-shaped:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Check -Configuration Release -Platform x64
```

Run the playback-quality smoke after changes touching playback, Native AOT
app-hosted capture, or native media diagnostics:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target PlaybackCheck -Configuration Debug -Platform x64
```

The primary local readiness gate for merging or cutting over the modern path is
the modern-only cutover gate. It runs modern Debug and Release checks plus
strict playback-quality:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target CutoverCheck -Platform x64
```

Build only, without registering or launching:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64
```

## XAML Hot Reload

XAML Hot Reload is a local iteration aid only, not a readiness gate. The modern
readiness path remains `Build-Noira.ps1 -Target Check` or
`Build-Noira.ps1 -Target CutoverCheck`.

## Local Loose File Deploy

Use the modern Native AOT loose AppX registration helper for fast local app
iteration:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Register-NoiraModernUwp.ps1 `
  -Configuration Debug `
  -Platform x64 `
  -Launch
```

Validate an existing modern loose layout without registering it:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Register-NoiraModernUwp.ps1 `
  -Configuration Debug `
  -Platform x64 `
  -SkipBuild `
  -ValidateOnly
```

Common parameters:

- `-SkipBuild`: reuse the current modern publish output.
- `-Launch`: start the app through `shell:AppsFolder` after registration.
- `-MsBuildPath`: explicitly pass the VS2026 MSBuild path.

## Xbox / Remote Loose File Deploy

Xbox hardware validation is deferred to the next phase. Until then, local
desktop UWP/MSIX/AppContainer validation is required for each substantial
migration step. When Xbox hardware is available, validate package identity,
input, display/HDR behavior, audio, native playback, and playback-quality
capture on the device before closing the migration goal.

## Current Tradeoffs

- Loose deploy shortens local iteration, but is not a release or quality
  conclusion by itself.
- Native AOT and trimming warnings remain blockers for the modern production
  path.
- Playback strategy changes remain separate from .NET/VS2026 modernization;
  use playback-quality reports to classify failures before changing playback
  policy in this branch.
