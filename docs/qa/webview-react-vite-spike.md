# WebView React Vite Spike QA

Date: 2026-07-10

## Scope

This record covers the local Windows Debug x64 feasibility run for the WebView2 React/Vite shell. Private server URLs, usernames, item IDs, media source IDs, tokens, and titles are intentionally omitted.

Package used for the run:

- Name: `NoiraPlayer.App`
- Version: `0.1.0.279`
- Family: `NoiraPlayer.App_hkwzw7pzpr4z0`
- UI source modes: packaged `https://app.noira.local/index.html` and Debug Vite URL from `LocalState\webview-dev-url.txt`

## Automated Evidence

- Web tests cover bridge timeout behavior, browser-fetch receiver binding, Emby request headers and DTO mapping, direct-first transport selection, native fallback selection, URL scoping, and HTTP error behavior.
- Core tests cover the shared Emby authorization value and source contracts for WebView2 hosting, exact-origin bridge validation, native-only playback launch, HMR source resolution, and removal of semantic native catalog commands and web video playback.
- `tools\Write-WebViewDevServerUrl.tests.ps1` covers latest-package selection, HTTP/HTTPS validation, LocalState output, and clearing the setting.
- The modern Debug x64 solution and NativeAOT package build complete with the generated React assets under `WebCode`.

## Network Finding

The real Emby endpoint was reachable and `/System/Info/Public` returned HTTP 200. A request with the WebView development Origin returned no `Access-Control-Allow-Origin`. The preflight for `Authorization` and `X-Emby-Token` returned HTTP 404 and no CORS headers.

Result: browser direct fetch cannot be the only transport for this server. React tries browser fetch first. On a browser network `TypeError`, the transport switches once to native `emby.get`. The native handler adds the same Emby authorization used by existing native code and allows only relative GET paths for the saved user's Views and Items endpoints on the saved server. Image elements continue to use Emby's existing `api_key` URL convention.

## HMR Evidence

- Vite served the page at `127.0.0.1:5173` after adding a package loopback exemption with `CheckNetIsolation`.
- The running WebView loaded the Vite page without rebuilding or re-registering UWP.
- Vite recorded three live updates for `/src/App.tsx` while the same app process remained open.
- The LocalState writer can clear the dev URL to restore packaged startup.
- Binding Vite to `0.0.0.0` exposed the LAN URL but triggered the first-run Windows Node.js firewall permission dialog. That permission is a prerequisite for LAN/Xbox HMR and was not persisted during this local proof.

## Packaged Fallback Evidence

After clearing `webview-dev-url.txt` and stopping every listener on port 5173, the app was rebuilt, re-registered, and launched again. The package layout contained `WebCode\index.html` plus hashed JavaScript and CSS assets. The relaunched app loaded the saved session and all 21 real libraries from the packaged React build, confirming that development HMR is optional and not a startup dependency.

## Real Interaction Evidence

The saved session loaded from native storage into React without browser persistence. The app then completed this flow against real private data:

1. Loaded 21 Emby library views.
2. Opened a movie library and rendered a real item list.
3. Opened one real item and rendered its title, image, type, and overview.
4. Posted `playback.nativePlayItem` from React.
5. Navigated into the existing native `PlaybackPage`.
6. Progressed from `Opening` to `Playing` with decoded video visible.
7. Displayed native source information (`1080p`, approximately `4.2 Mbps`, AVC/AAC), audio state, subtitle state, timeline, pause, seek, stop, and more controls.

No WebView `<video>` element or web direct-stream command participated in playback.

## Usability Finding

Unconstrained Emby images initially expanded list buttons beyond the viewport, which made an automation click target land off-screen. The web CSS now gives list images a stable `96px` square and gives detail images a constrained poster aspect ratio. The obsolete web-video CSS rule was removed.

## Remaining Xbox Work

- No Xbox device was connected during this run, so WebView2 runtime availability, LAN firewall routing, controller focus, memory pressure, remote DevTools, and actual Xbox codec/HDR behavior remain unverified on hardware.
- Xbox HMR should use the development PC LAN URL plus an allowed Node.js firewall rule; the same-PC loopback exemption is only for local Windows validation.
- The adaptive transport should be exercised on Xbox with both a CORS-enabled server and a CORS-blocked server.
