# Foundation Status

Date: 2026-07-05

## Branch

`codex/xbox-emby-foundation`

## Verified

- Visual Studio 2022 Community is installed and launchable at `C:\Program Files\Microsoft Visual Studio\2022\Community`.
- MSBuild is available at `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`.
- Core unit tests pass: 52 passed, 0 failed, 0 skipped.
- `NextGenEmby.Core` restores and builds as part of both `dotnet test` and solution MSBuild.
- `NextGenEmby.Core.Tests` restores and builds as part of solution MSBuild.
- Emby authentication, authenticated request headers, library query URL construction, PlaybackInfo parsing, direct stream URL construction, and progress reporting are covered by unit tests.
- Playback orchestration has a stable managed backend interface: `IPlaybackBackend`, `PlaybackDescriptor`, `PlaybackState`, and `PlaybackOrchestrator`.
- Native playback diagnostics contracts are implemented: `PlaybackBackendCapabilities`, `PlaybackDisplayStatus`, and `IPlaybackBackendDiagnostics`.
- The managed native adapter is implemented: `INativePlaybackEngine`, `NativePlaybackOpenRequest`, and `NativeDirectXPlaybackBackend`.
- The UWP app source contains the Xbox-first shell, Login, Home, and Playback pages.
- The UWP login flow is wired to `EmbyApiClient`, `ApplicationDataSessionStore`, and `ApplicationDataDeviceIdProvider`.
- The UWP playback page is wired to `SystemMediaPlaybackBackend` for the temporary system-player slice.
- Kodi HDR research path is recorded in ADR 0001.

## Verification Commands

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj -v minimal
```

Result:

```text
Passed: 52
Failed: 0
Skipped: 0
```

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Result:

```text
Restore succeeded.
NextGenEmby.Core built.
NextGenEmby.Core.Tests built.
NextGenEmby.App failed before compile with MSB3644.
```

## Current Blocker

The full UWP app build is blocked by the local Visual Studio/UWP component installation, not by a source compile error.

MSBuild error:

```text
error MSB3644: Could not find the reference assemblies for .NETCore,Version=v5.0
```

Observed environment details:

- `vswhere` finds Visual Studio Community 2022 version `17.14.34`.
- `vswhere -requires Microsoft.VisualStudio.ComponentGroup.UWP.VC Microsoft.VisualStudio.ComponentGroup.UWP.NetCoreAndStandard` returns no matching installation.
- `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore` contains `v4.5` only.
- `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v5.0` is missing.
- `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Microsoft\WindowsXaml\v17.0\Microsoft.Windows.UI.Xaml.CSharp.targets` exists.
- Windows SDK UAP platform `10.0.22621.0` exists.

Earlier passive Visual Studio Installer modification failed because the installer requires elevation. The log path from that attempt was:

```text
C:\Users\yqzzx\AppData\Local\Temp\dd_installer_20260705003612.log
```

## Not Yet Verified

- `NextGenEmby.App` has not been compiled past reference-assembly resolution.
- XAML type checking has not run because the UWP app build stops before compile.
- Visual Studio local-machine launch and manual smoke test were not run.
- Xbox hardware deployment was not attempted.
- The C++/WinRT native component has not been created because Task 0 of the native plan is blocked by missing UWP tooling.
- HDR/HEVC native playback is not implemented yet; the managed adapter boundary is ready for the native component.

## Recommended Next Local Action

Install or repair the Visual Studio UWP/.NET Native tooling that provides `.NETCore,Version=v5.0` reference assemblies for UWP C# projects. After that, rerun:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' NextGenXboxEmby.sln /restore /p:Configuration=Debug /p:Platform=x64
```

Then open `NextGenXboxEmby.sln` in Visual Studio and run the manual smoke test:

- startup project: `NextGenEmby.App`
- platform: `x64`
- target: Local Machine
- login page renders in dark mode
- navigation moves between Login, Home, and Playback
- Playback page shows the black video surface and bottom overlay
- keyboard/gamepad focus is visible

## Next Plan

Continue `docs/superpowers/plans/2026-07-05-native-playback-core.md` from Task 0 after repairing the UWP/.NET Native toolchain. Tasks 1 and 2 of that plan are already complete and committed.
