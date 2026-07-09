# .NET Modernization Design

## Goal

Move Noira to Visual Studio 2026 modern .NET UWP with Native AOT while preserving UWP/MSIX/AppContainer/Xbox compatibility. The old solution, old UAP app project entry, and old loose deploy helper are removed from the active tree after the modern-only cutover gates pass.

## Current Baseline

- Retired legacy entries: the old solution, old UAP app project entry, and old loose deploy helper are no longer active repository entry points.
- Core library: `src/NoiraPlayer.Core/NoiraPlayer.Core.csproj` targets `net10.0` directly.
- Test and playback-quality tools target `net10.0` directly.
- Native component: `src/NoiraPlayer.Native/NoiraPlayer.Native.vcxproj` uses C++/WinRT 3.0.260520.1, UWP, VS2026 C++ toolset `v145`, Windows SDK `10.0.26100.0`, `packages.config`, FFmpegInteropX.UWP.FFmpeg 8.1.2, and x64/x86 configurations.
- Verified historical baseline in this worktree: `dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj -v minimal` passed 735 tests, and the old Debug x64 solution build passed before the active build entry point was switched to the VS2026 modern path.

## Modernization Strategy

Use an incremental, reversible migration strategy.

1. Keep the old UWP app project untouched during the initial spike so regressions are attributable.
2. Add a parallel SDK-style modern UWP app project that reuses the same source tree, manifest, assets, Core project, and native component.
3. Add explicit VS2026 build, registration, page, and playback-quality scripts for reproducible local validation.
4. Resolve modern build failures in small commits: SDK-style item inclusion, Core target compatibility, C++/WinRT interop, XAML binding/AOT annotations, then packaging/launch.
5. Switch the primary solution and repository build entry point only after the modern path can build, package, launch locally, enter the app page, and run strict playback-quality validation.
6. Retire active old build commands after the modern-only cutover gate is green, then remove old solution/project/script entry files from the active tree.

## Problem Boundary Between .NET Modernization And Playback Strategy

Modernization-owned: VS2026/MSBuild 18 resolution, .NET 10/UWP project shape, Native AOT/trimming compatibility, C#/WinRT projection wiring, MSIX package layout, local registration, app launch, logged-in page entry, app-hosted playback-quality command/report capture, and diagnostics needed to classify failures.

Playback-worktree-owned: source ranking, automatic fallback, transcode/direct-stream decisions, decoder retry/drain policy, starvation handling, render-loop pacing, clock synchronization, and media-quality threshold tuning.

Do not change source selection, transcode/direct-stream policy, decoder retry, starvation, or frame pacing in this branch.

Every playback failure must be classified as host/toolchain, validation harness, native diagnostic, or playback strategy before the branch advances.

A failure is a modernization blocker when it prevents building, packaging, registering, launching, logging in, entering the app page, executing the development command, exporting the report, or collecting runtime evidence independent of media-specific behavior.

A failure is a playback-strategy follow-up when the modern app reaches playback and exposes native FFmpeg/decoder/render/sync behavior that would need policy changes to continue or improve playback. This branch may add diagnostics at that boundary, but the behavioral fix belongs in the playback optimization worktree.

### Regression Attribution Matrix

Use paired comparisons before changing playback behavior:

- `same media, same playback code, different toolchain`: compare the legacy/main or pre-modern app against the modernization branch using the same media item, source id, playback command, and playback code. If only the modern toolchain fails before or during host/harness evidence collection, treat it as a modernization blocker.
- `same toolchain, same media, different playback strategy`: compare the modernization branch against the playback optimization worktree after rebasing or cherry-picking only the playback strategy change set. If the difference is decoder retry, starvation handling, source ranking, transcode/direct-stream policy, frame pacing, clock sync, or threshold tuning, classify it as playback strategy.
- `same toolchain, same playback strategy, different media`: use this only to decide whether a failure is media-specific. Media-specific native FFmpeg/decoder/render behavior is diagnostic evidence first; Modernization branch must not carry the behavioral fix.

When the modern app can build, register, launch, log in, enter the app page, execute the quality-run command, export a report, and record enough runtime evidence to show the native playback failure boundary, the .NET modernization part has done its job for that case. The next change should either improve diagnostics in this branch or move the behavioral fix to the playback optimization worktree.

## Modern Project Shape

The modern project follows the Visual Studio 2026 UWP template:

- `Project Sdk="Microsoft.NET.Sdk"`
- `TargetFramework=net10.0-windows10.0.26100.0`
- `TargetPlatformMinVersion=10.0.19041.0`
- `Package.appxmanifest` keeps `TargetDeviceFamily Name="Windows.Universal"` with `MinVersion=10.0.19041.0` and `MaxVersionTested=10.0.26100.0`
- `UseUwp=true`
- `EnableMsixTooling=true`
- `PublishAot=true`
- `DisableRuntimeMarshalling=true`
- `NoiraPlatformCompatibility=UWP-MSIX-AppContainer-Xbox`

The first spike is x64-only because Xbox and the current validation path are x64-focused. x86 can be reintroduced after the modern x64 path works.

## Spike Result

- Added `src/NoiraPlayer.App/NoiraPlayer.App.Modern.csproj` as a parallel SDK-style UWP project during the spike. It is now the only active app project entry.
- The modern app project explicitly records `NoiraPlatformCompatibility=UWP-MSIX-AppContainer-Xbox`, keeps `UseUwp=true`, `EnableMsixTooling=true`, WinUI 2 `Microsoft.UI.Xaml` 2.8.7, the shared UWP `Package.appxmanifest`, and x64/win-x64 output only. A design contract prevents accidental Windows App SDK / WinUI 3 drift while Xbox remains a hard target.
- Promoted `NoiraPlayer.sln` to the VS2026 modern solution entry containing the modern app, Core, Native, Core tests, and playback-quality headless/CLI tools.
- Added `tools/Build-NoiraModernUwp.ps1` as the VS2026/MSBuild 18 entry point. It restores the native `packages.config` packages and can run either `-Target Build` or `-Target Publish`. The modern publish target captures MSBuild output and fails when AOT is enabled and any `IL2xxx` trimming or `IL3xxx` Native AOT warning line appears, turning the production-switch warning policy into an executable gate.
- Added `tools/Build-Noira.ps1` as the repository-level build entry point. It builds the primary `NoiraPlayer.sln`, delegates modern publish/verification to the modern scripts, provides `-Target Check` for configuration-matched Core tests plus the modern page gate, exposes `-Target PlaybackCheck`, and uses `-Target CutoverCheck` as the modern-only local readiness gate. The unified entry point no longer resolves or runs the old legacy toolchain.
- Added `tools/NoiraModernToolchain.ps1` as the shared helper for modern MSBuild resolution and .NET SDK 10 preflight. The unified modern entry point, direct `Build-NoiraModernUwp.ps1` script, and `Register-NoiraModernUwp.ps1` all dot-source it, so modern toolchain checks stay consistent without adding a repository-wide `global.json`.
- Confirmed the modern project can consume `NoiraPlayer.Native.vcxproj` by generating a C#/WinRT projection directly in the app project with `Microsoft.Windows.CsWinRT` 2.2.0.
- Confirmed Debug x64 modern Build succeeds.
- Confirmed Debug x64 Native AOT Publish succeeds without app-owned `IL2026` or `IL3050` JSON warnings and produces `NoiraPlayer.App.exe` plus native dependencies under `src\NoiraPlayer.App\bin\Modern\Debug\net10.0-windows10.0.26100.0\win-x64\publish\`.
- Confirmed a staged Native AOT loose AppX layout can be registered and launched locally. The launched process path is under `src\NoiraPlayer.App\bin\Modern\AppxLayouts\Debug\x64\NativeAot`, and a real Emby login smoke reaches the logged-in Home page with real Home rows/media libraries loaded.
- Added `tools\Test-NoiraModernUwp.ps1` as the repeatable modern verification gate. It runs Native AOT publish, registers the staged loose AppX layout, clears stale DEBUG development commands and stale Home evidence, launches the package, waits for the app process, waits for privacy-safe `home-page-evidence.json` to report `page=Home` and `renderStage=supplemental`, captures a desktop screenshot after a short stabilization delay, verifies that screenshot exists and is non-empty, and writes structured `pageEvidence` into the JSON report. The report includes `postLaunchDelaySeconds`, `screenshotStabilizationSeconds`, `semanticEvidenceStatus`, and Home counts such as `libraryCount`, `libraryPreviewCount`, `libraryPreviewMissingCount`, `rowCount`, and `continueItemCount`. This turns the page-launch check into a reusable command for each later migration step and avoids treating every partial library-preview fetch as a .NET/AOT failure.
- Added `tools\Test-NoiraModernPlaybackQuality.ps1` as the repeatable modern app-hosted playback-quality smoke gate, exposed through `tools\Build-Noira.ps1 -Target PlaybackCheck`. It runs the existing playback-quality `plan-runs`, `Write-AppQualityRunCommand.ps1`, app launch, report export, and `analyze-report-set` flow, then requires source metadata and runtime playback samples to be captured. Before launching the app it clears the package LocalState `quality-run\captured` root, and after export/analysis it verifies exported/analyzed report counts match the current run plan so stale reports cannot pollute the gate. The script records the model `qualityResult` and can enforce `modelAnalysis.result=pass` with `-RequireQualityPass` for strict corpus runs.
- Promoted `NoiraPlayer.Core`, the Core tests, and playback-quality CLI/headless tools to direct `net10.0` targets. The modern build, playback scripts, and playback-quality CLI smoke all run the `net10.0` tool outputs and no longer pass `NoiraEnableModernCoreTarget` or `NoiraEnableModernToolTarget` flags.
- Updated the shared UWP package manifest to `MaxVersionTested=10.0.26100.0`, matching the modern `net10.0-windows10.0.26100.0` app target and VS2026 Windows SDK while preserving `MinVersion=10.0.19041.0` for the current Xbox compatibility floor.
- Promoted the native C++/WinRT component to the VS2026 C++ toolset by setting `MinimumVisualStudioVersion=18.0`, `PlatformToolset=v145`, and `WindowsTargetPlatformVersion=10.0.26100.0`; MSBuild 18 resolves this to the v180 VC targets and MSVC 14.51 toolchain. The native project now compiles as C++20, sets `CppWinRTEnableLegacyCoroutines=false` so the C++/WinRT package no longer injects the deprecated `/await` coroutine path, sets `CompileAsWinRT=false` so AppContainer defaults do not reintroduce C++/CX `/ZW`, and passes `/utf-8` so generated C++/WinRT 3 headers build without code-page warnings.
- Upgraded the native C++/WinRT NuGet package from `Microsoft.Windows.CppWinRT` 2.0.220531.1 to 3.0.260520.1 after package audit showed managed packages were current and FFmpegInteropX.UWP.FFmpeg was already at 8.1.2. The upgrade keeps the same UWP/AppContainer native package shape and is protected by source contracts on `packages.config` and the `.vcxproj` import paths.
- Confirmed an app-hosted playback-quality smoke can drive the modern Native AOT app with a public direct-stream case and capture runtime playback samples before playback is stopped. The current public SDR video-only smoke now passes with source metadata matched, runtime metrics captured, 358 rendered frames, and A/V sync marked `not-applicable` because no audio track exists.
- Confirmed item playback quality-runs should narrow PlaybackInfo by `MediaSourceId` when the launch request already has one. This avoids huge all-source PlaybackInfo responses in Native AOT and lets private Emby item runs reach native playback.
- Confirmed a logged-in, audio-bearing private quality-run can complete app-hosted capture on the modern Native AOT app. `private-emby/1106215/26ab4f64b43a414885064cc28ef5cf89/sdr-smoke` writes a report with one video track, one audio track, captured runtime metrics, SDR output, and successful load/play/pause/resume/seek/stop lifecycle events. After moving runtime evidence capture before the quality-run seek probe, the same case captures clean playback metrics. After the diagnostics-only native change, it passes the quality gate with `startup.startupDurationMs=6286.6194`, `decodedVideoFrames=253`, `renderedVideoFrames=252`, `droppedVideoFrames=0`, `videoStarvedPasses=0`, and `audioVideoDriftMsP95=10`.
- Confirmed the private 4K HEVC case `private-emby/145559/c97a3453a8ea47f4a61679ae6458418b/sdr-smoke` is not blocked in the .NET/AOT host layer: the modern app narrows `PlaybackInfo`, opens the selected source, and enters native playback. It then exposes a native D3D11VA HEVC decoder strategy issue where `avcodec_send_packet` and `avcodec_receive_frame` both return `EAGAIN` on the same packet (`codec=173`, `pixFmt=171`, `hw=1`, `receiveResult=-11`) and app-hosted capture later reports `Playback has not been started`. This branch keeps that diagnostic evidence but does not change decoder retry/starvation policy.

## Known Modernization Follow-ups

- The next playback-quality gate is expanding from one passing public SDR video-only smoke and one captured private audio-bearing run to a small representative private set. Direct-uri source metadata is now projected from the reference manifest expected values into the app-hosted quality-run playback descriptor, SDR output is reported as actual `Sdr` output even when HDR capability is unsupported, desktop loose-UWP runs use a software-only cadence refresh snapshot when HDMI display mode evidence is unavailable, and video-only sources no longer require impossible A/V drift evidence.
- Native playback risks remain after the .NET/AOT host fixes: `private-emby/145559/c97a3453a8ea47f4a61679ae6458418b/sdr-smoke` reaches native playback, then hits the D3D11VA HEVC double-EAGAIN boundary. Decoder retry/starvation policy is intentionally left to the playback optimization worktree. `avformat_open_input` and `avformat_find_stream_info` are the dominant startup segments on remote streams and still vary by run.
- Modern Build now inherits the repository nullable warning-as-error policy and passes cleanly after fixing App storage nullability and object identity comparisons.
- JSON serialization now uses source-generated contexts for Emby API DTOs, development diagnostics parsing, and playback-quality report serialization. Keep this gate in place for new app-owned JSON entry points.
- App-hosted debug quality-runs must capture backend diagnostics and metrics before calling `StopAsync` and before running the seek probe; a source contract test now protects that order so Native AOT reports do not regress to `empty-snapshot` or seek-preroll-polluted runtime samples.

## Risk Controls

- Keep `NoiraPlayer.sln` as the VS2026 modern primary solution.
- Keep old solution, UAP project, and loose deploy script entry points out of the active tree.
- Do not migrate to Windows App SDK or WinUI 3 in this branch; that is a later UI framework migration.
- Treat Native AOT and trimming warnings as blockers before declaring the modern path production-ready.
- Keep Xbox availability as a hard constraint: the modern path must remain UWP/MSIX/AppContainer-compatible.

## Verification Gates

Each stage must keep or restore these gates:

1. Modern default check: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Check -Configuration Debug -Platform x64`
2. Modern Release check: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Check -Configuration Release -Platform x64`
3. Modern build-only loop: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64`
4. Modern page gate only: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Verify -Configuration Debug -Platform x64`
5. Modern playback-quality smoke: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target PlaybackCheck -Configuration Debug -Platform x64`
6. Modern cutover gate: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target CutoverCheck -Platform x64`
7. Broader playback-quality validation after the modern app-hosted smoke remains green.

## Open Migration Questions

- Resolved for the spike: the existing C++/WinRT UWP component can be consumed by the modern .NET UWP app through an in-project C#/WinRT projection. A separate projection NuGet is not necessary yet.
- Resolved for the local VS2026 phase: native package audit found `FFmpegInteropX.UWP.FFmpeg` already current at 8.1.2, and `Microsoft.Windows.CppWinRT` was upgraded to 3.0.260520.1 with the native build and cutover gate still passing.
- Whether `Microsoft.UI.Xaml` 2.8.7 remains sufficient for modern .NET UWP with this app's XAML surface.
- Which view models or DTOs need `[GeneratedBindableCustomProperty]`, `partial`, or trimming annotations for Native AOT.
- Resolved for the spike: Core now targets `net10.0` directly after the repository build entry point stopped running the old legacy path.
- Resolved for the spike: test and playback-quality tool execution now use `net10.0` directly.
