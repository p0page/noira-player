# WebView TV Browse UI QA

Date: 2026-07-12

Code under test: working tree after `637a9db` (`codex/webview-tv-browse-ui`)

## Scope

- Windows x64 modern UWP package using the packaged React/Vite client in WebView2.
- Existing native `PasswordVault` session and real Emby responses; no runtime fixture catalog.
- Keyboard input (`Arrow` keys, `Enter`, and `Escape`) for desktop interaction.
- App-owned `GamepadDeviceRegistry` polling all registered controllers into the semantic `host.input.v1` protocol; Web browse and native playback consume mutually exclusive `GlobalInputRouter` contexts. Keyboard remains a parallel desktop test input and physical Xbox controller validation remains pending.
- Private screenshots and catalog values were inspected locally only and are not stored in this document.

## Automated Evidence

| Check | Result |
| --- | --- |
| Web unit/integration tests | Pass, 279 tests |
| Web TypeScript and production build | Pass, 2,437 modules |
| Core and source-contract tests | Pass, 890 tests |
| Private-data guard tests | Pass |
| Private-data guard against this worktree | Skip by design: no local credential env file; native saved session used |
| Modern UWP Debug x64 Build | Pass after restoring the repository-declared native package cache |
| Modern UWP Debug x64 NativeAOT publish/layout validation | Pass |
| Isolated local QA package registration | Pass as `NoiraPlayer.App.InputArchitectureQa0712B`; not launched |

Commands used for the reproducible, non-private checks:

```powershell
npm --prefix src/NoiraPlayer.Web test -- --run
npm --prefix src/NoiraPlayer.Web run build
dotnet test tests/NoiraPlayer.Core.Tests/NoiraPlayer.Core.Tests.csproj -v minimal
./tools/Test-WebUiPrivateData.tests.ps1
./tools/Test-WebUiPrivateData.ps1
./tools/Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64
./tools/Register-NoiraModernUwp.ps1 -Configuration Debug -Platform x64 -LayoutDirectory src/NoiraPlayer.App/bin/Modern/AppxLayouts/Debug/x64/NativeAot-InputArchitecture -ValidateOnly
```

## Real-Data Acceptance

The saved session successfully exercised non-empty Home, Library, and Details routes. No catalog values or aggregate library sizes are retained here.

| Route or behavior | Result |
| --- | --- |
| Home initial content focus | Pass after the WebView receives host focus |
| Home first-viewport chrome and density | Pass: semantic Home heading is visually recessed and content rails begin at the safe-area top |
| Page scrollbar suppression | Pass: document scrolling remains focus-driven with no visible browser scrollbar |
| Integrated matte card focus | Pass: fill, luminance, and scale remain inside the card footprint without a bright frame |
| Horizontal Home movement | Pass |
| Home nearest-screen-column Up/Down | Failed initially; final center-coordinate focus-group policy passed in the latest packaged build |
| Guide open, traversal, Escape, and exact content restore | Pass |
| Library page-zero load and poster grid | Pass |
| Library directional movement, viewport scroll, and retained cards | Pass |
| Library to Details and exact Escape restoration | Pass |
| Details Play/Resume default focus | Pass |
| Details Play/Resume matte focus and text selection | Pass: stable focus target, no accidental selected button text |
| Relaunch while Details is active | Pass: returns to Home and restores the originating media card instead of opening the Guide |
| Details artwork atmosphere and matte fallback behavior | Real artwork passed; matte fallback is covered by automated tests; no private image retained |
| Native playback launch | Pass in an isolated local QA package; dispatcher-queued navigation opened the native page |
| Native video rendering | Pass |
| Native Back, teardown gate, cached WebView return, and Play/Resume restore | Pass |
| Refresh with a valid target | Covered by automated Home generation and focus-preservation tests; not repeated through a dedicated visible refresh command |

## Viewports

| Captured logical window | Result |
| --- | --- |
| 963 x 541 | Prior layout pass; current compact behavior remains covered by automated CSS/component contracts but was not visibly recaptured |
| 1203 x 935 | Current pass: compact Home chrome, hidden scrollbar, card focus, Details, native launch, and native return had no observed overlap |
| 2560 x 1392 maximized | Prior wide-layout pass: rows remained horizontal and scannable; not visibly recaptured after the current chrome-only change |

An exact 1280 x 720 resize was not available through the current window automation API. Automated CSS and component tests cover the same compact/wide breakpoints, but this is not equivalent to a visible 1280 x 720 capture.

## Residual Risk

- The real server returned a partial Home-row warning while still rendering usable core and supplemental rows. Partial failure recovery worked; the server-specific failed row was not recorded to avoid private diagnostics.
- Loose Debug registrations from multiple worktrees share `NoiraPlayer.App_hkwzw7pzpr4z0` and can overwrite each other's `InstallLocation`. This caused the reported playback toast to come from another worktree's older synchronous navigation build. The current pass used a temporary local-only package identity derived from the same built layout so visual and playback evidence could not be replaced mid-run.
- `NavigationCacheMode.Required` preserves browse state on this Windows run. Xbox memory pressure while native playback is active remains unverified.
- The app-owned input hub tracks startup/hot-plug controllers, polls all devices, uses active-controller arbitration, analog hysteresis, a neutral gate across context switches, and one shared 400/120 ms repeat state. Browse input is ready-gated and sent as typed `host.input.v1`; Web `GlobalInputPolicy` resolves layers, routes, Guide, text engagement, and native Back before the Norigin DOM-key compatibility adapter. Native playback consumes the same semantic stream while `CoreWindow` remains keyboard-only.
- Capture-stage Web deduplication handles both observed orderings for known Arrow/Enter/Back mappings: it suppresses a trusted platform key after host input, or suppresses the later semantic event when the trusted key arrives first. Its unit contract passes, but actual Xbox/WebView2 key names and Menu/View mapping must still be confirmed on hardware; a single physical press moving two targets remains a release blocker.
- Windows build and registration evidence is not Xbox hardware evidence. Controller behavior, Xbox remote provider coverage, TV safe-area behavior, suspend/resume, multi-controller takeover, and 30-minute WebView memory/input latency still require Xbox validation.
- Details currently exposes the real title, basic metadata, overview, and Play/Resume. Version/audio/subtitle summaries require a later real DTO expansion; no placeholder values should be introduced.
