# UI permanent freeze: root cause and regression gate

Status: root cause confirmed on packaged desktop UWP, fixes implemented, automated desktop evidence gate passing. Physical Xbox validation remains required.

## Symptom classification

Two different failures looked similar in the UI and had to be separated:

1. **Permanent UI freeze:** a few seconds after authenticated UI or native playback starts, every XAML control stops responding. Video can continue because decode/render threads are still alive.
2. **Long UI stall:** opening, stopping, seeking, or reading current position blocks the UI thread while native/network work completes.

The first failure is the historical cross-page problem. It reproduces without the old native Home page and therefore is not caused by leftover Home/Login page logic. The second group made playback controls less reliable, but it did not explain a permanently dead dispatcher by itself.

## Confirmed root cause

The app targets .NET 10 NativeAOT. Debug previously inherited `Optimize=false`, so the ILC response file did not contain `-O`.

During XAML reference tracking, the runtime starts a GC and enters `TrackerObjectManager.BeginReferenceTracking`. In an unoptimized NativeAOT build, `FindReferenceTargetsCallback` is initialized lazily inside that GC. Its class constructor allocates a managed object, enters the allocation slow path, and waits for the GC that is already running on the same thread. The XAML UI thread then waits for itself forever.

The captured `ApplicationView ASTA` stack was:

```text
WKS::gc_heap::wait_for_gc_done
RhpGcAlloc / RhpNewObject
ClassConstructorRunner::EnsureClassConstructorRun
FindReferenceTargetsCallback.Instance::.ctor
TrackerObjectManager::WalkExternalTrackerObjects
TrackerObjectManager::BeginReferenceTracking
GCStartCollection
ReferenceTrackerHost::DisconnectUnusedReferenceSources
Microsoft.UI.Xaml / CoreMessaging
```

This matches the .NET runtime report for NativeAOT XAML apps becoming permanently unresponsive after roughly 10-20 seconds. The upstream fix marks the callback holder for eager static construction. It is present in current runtime source and was merged for .NET 11, so this .NET 10 app must keep ILC optimization enabled.

References:

- <https://github.com/dotnet/runtime/issues/121538>
- <https://github.com/dotnet/runtime/pull/121558>
- <https://source.dot.net/System.Private.CoreLib/System/Runtime/InteropServices/TrackerObjectManager.NativeAot.cs.html>

Authenticated pages trigger the failure more readily because they create a much larger graph of XAML/WinRT reference-tracked objects. The logged-out page is small enough that the same GC/reference-tracker path may not run during a short observation window.

## Implemented fixes

### Runtime deadlock

`NoiraPlayer.App.Modern.csproj` now sets `<Optimize>true</Optimize>` unconditionally for NativeAOT builds. A source contract prevents it from drifting back. The generated Debug ILC response file must contain `-O`.

### Native UI-thread stalls

- Every `NativePlaybackEngine` async operation now retains a strong lifetime and switches to `resume_background()` before graph work.
- `PlaybackGraph.CurrentPositionTicks()` reads an atomic snapshot instead of waiting for the render-loop mutex.
- FFmpeg open/probe has a 30-second deadline; read/seek has a 20-second deadline; stop explicitly interrupts pending FFmpeg I/O.
- `DisplayInformation.GetForCurrentView()` remains on the view UI thread. The agile `HdmiDisplayInformation` object and display snapshot are cached for background HDR apply/restore work.
- Playback uses one global CoreWindow key route. Accept activates the focused transport control, and pointer activity no longer rebuilds focus while the overlay is already visible.

The display-thread split follows the platform contract: `DisplayInformation.GetForCurrentView()` is view-thread-affine, while `HdmiDisplayInformation` is agile and supports both threading models.

References:

- <https://learn.microsoft.com/uwp/api/windows.graphics.display.displayinformation.getforcurrentview>
- <https://learn.microsoft.com/uwp/api/windows.graphics.display.core.hdmidisplayinformation>

## Why this verification is reliable

Watching the picture is insufficient: the native render loop can continue while XAML is dead. The regression gate therefore combines three independent signals:

1. **Build invariant:** inspect the actual ILC command and require `-O`.
2. **Dispatcher liveness:** a Debug watchdog posts a low-priority UI callback every 250 ms and records five-second windows. A blocked UI thread leaves a pending probe even if video continues.
3. **Behavioral lifecycle:** the App-hosted quality run must successfully load, play, pause, resume, and stop through the real native playback backend, capture native frame metrics, write its report, and remain dispatcher-responsive after report creation.

`tools/Test-NoiraUiResponsivenessEvidence.ps1` rejects:

- any watchdog window marked unhealthy;
- a window with no completed UI callback;
- dispatch or pending latency above 1000 ms;
- less than 120 seconds of continuous evidence;
- terminal playback errors or missing native playback samples;
- missing successful load/play/pause/resume/stop operations;
- less than ten seconds of healthy dispatcher evidence after the report is written.

## Repeatable desktop gate

Run from the repository root in PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-NoiraModernPlaybackQuality.ps1 `
  -Configuration Debug `
  -Platform x64 `
  -ManifestPath docs\qa\playback-quality-reference-manifest.example.json `
  -CaseId w3c/ui-freeze-regression-sintel `
  -Purpose ui-responsiveness `
  -DurationSeconds 120 `
  -WaitSeconds 180 `
  -RequireQualityPass `
  -KeepRunning

Start-Sleep -Seconds 15
$command = Get-Content docs\qa\private\modern-aot-playback-check-command-summary.local.json -Raw | ConvertFrom-Json
$gate = Get-Content docs\qa\private\modern-aot-playback-check.local.json -Raw | ConvertFrom-Json

powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-NoiraUiResponsivenessEvidence.ps1 `
  -WatchdogLogPath (Join-Path $command.localState 'ui-responsiveness.log') `
  -QualityReportPath $gate.capturedReportPath `
  -MinimumHealthySeconds 120 `
  -MinimumPostReportHealthySeconds 10
```

Also verify the compiler invariant after the build:

```powershell
$rsp = Get-ChildItem src\NoiraPlayer.App\obj\Modern\x64\Debug -Filter NoiraPlayer.App.ilc.rsp -Recurse | Select-Object -First 1
if (-not (Select-String -LiteralPath $rsp.FullName -Pattern '(^|\s)-O(\s|$)')) {
  throw 'NativeAOT ILC optimization is missing.'
}
```

## Current A/B evidence

Before the optimization fix, the public 52-second sample reached end of stream and native playback logged completion, but the watchdog stopped producing callbacks and UI Automation could no longer obtain a root element. A full dump showed the exact GC/class-constructor cycle above.

With the optimized package and native responsiveness fixes:

- the same public sample naturally reached end of stream;
- App-hosted quality result was `pass`;
- load, play, pause, resume, and stop all succeeded;
- 1,253 native video frames were decoded and rendered;
- 31 consecutive watchdog windows covered 160.3 seconds;
- maximum UI dispatch latency was 48 ms, pending latency was 0 ms, skipped probes were 0;
- 86.8 seconds of healthy UI evidence remained after the report was created;
- the external evidence checker returned `pass`;
- after end of stream, the native playback options UI still opened in 582 ms.

An authenticated private-server smoke also completed WebView library -> details -> native PlaybackPage -> real video, then opened source/audio/subtitle options and performed pause/resume. No server URL, account, token, media identifier, or private screenshot is part of repository evidence.

The reported historical native-Library path was also checked with a temporary, uncommitted diagnostic package that started the authenticated native Home page under the same optimized NativeAOT build. It entered the native Library page and remained dispatcher-responsive for 39 consecutive windows (about 195 seconds total, about 140 seconds on Library), with zero unhealthy windows, 0 ms maximum pending latency, and 199.9 ms maximum dispatch latency during data/render work. Clicking a media item after that observation navigated to the native details page in 2.3 seconds. This directly confirms that the optimization fix covers the former post-login native Library freeze trigger as well as current playback.

That diagnostic run also exposed a separate dormant old-page defect: deliberately opening the Library sort option throws `InvalidCastException` from `LibraryPage.CreateOptionSheetButton` and terminates the process. It is a deterministic click-time crash, not the passive post-login permanent freeze, and the final WebView entry path cannot reach it. Track it separately only if the retired native Library UI is kept as a supported route.

## Xbox acceptance gate

Desktop packaged UWP evidence proves the diagnosed deadlock is removed from this build, but it is not Xbox proof. Before closing the Xbox bug, run the same Debug package on physical Xbox and require:

- at least 15 minutes of authenticated browsing with zero unhealthy watchdog windows;
- three WebView -> native playback cycles;
- controller pause, resume, seek, source/audio/subtitle drawer, stop, and back navigation on every cycle;
- at least one natural end-of-stream transition followed by a control action within one second;
- no `Failed` native state, terminal quality error, FFmpeg timeout, or display-thread-affinity exception;
- the exported watchdog and quality report pass the same evidence checker.

If Xbox still freezes, capture a dump before changing page code. A recurrence with the GC/class-constructor stack means the build invariant was lost; a different UI-thread stack identifies a new blocker without conflating it with this fixed runtime deadlock.
