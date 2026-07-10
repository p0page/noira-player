# WebView React Vite Spike QA

Date: 2026-07-10
Latest performance pass: 2026-07-11

## Scope

This record covers the local Windows Debug x64 feasibility run for the WebView2 React/Vite shell. Private server URLs, usernames, item IDs, media source IDs, tokens, and titles are intentionally omitted.

Package used for the run:

- Name: `NoiraPlayer.App`
- Version: `0.1.0.279`
- Family: `NoiraPlayer.App_hkwzw7pzpr4z0`
- UI source modes: packaged `https://app.noira.local/index.html` and Debug Vite URL from `LocalState\webview-dev-url.txt`

## Metadata Transport Performance Pass

The native CORS fallback now owns one long-lived `EmbyMetadataTransport` instead of creating an `HttpClient` per request. Its default .NET 10 client uses a single `SocketsHttpHandler`, five-minute pooled connection lifetime, two-minute idle lifetime, HTTP/2 with downgrade, response decompression, no cookies, and no automatic redirects. Authentication remains on each request, and the Core transport independently rejects destinations outside the saved Emby server.

WebView bridge dispatch now uses one message listener per WebView host plus a request-ID pending map. Synthetic native responses expose only numeric `X-Noira-Transport`, network, bridge-round-trip, and body-size headers. No URL, identity, token, item ID, media-source ID, or response body is included in diagnostics.

Fresh automated evidence for this pass:

- Web: 15/15 Vitest cases passed; TypeScript checking and the Vite production build passed.
- Core: 778/778 xUnit cases passed, including the final destination-defense test.
- PowerShell: WebView HMR helper tests and privacy-safe hybrid transport probe tests passed.
- Platform: the Debug x64 modern UWP build compiled the transport under .NET 10/NativeAOT, and package `NoiraPlayer.App_0.1.0.279_x64__hkwzw7pzpr4z0` was re-registered and launched.
- Final packaged-state readback found a responding app process, packaged `WebCode/index.html`, no Vite URL override, the React `Libraries` heading, and 24 private button nodes without recording their labels.
- Privacy audit: 13 changed/untracked source or documentation files produced zero matches for the supplied private host, account, or password; the detailed probe report remained ignored.

The private-server benchmark alternated 12 pooled and 12 per-request-client calls to the same bounded authenticated Views request, after unrecorded warm-ups. The ignored local report contains only these anonymous measurements:

| Mode | p50 | p95 |
| --- | ---: | ---: |
| Long-lived client | 311.992 ms | 375.705 ms |
| New client per request | 467.251 ms | 644.514 ms |
| Ratio | 0.6677 | 0.5829 |

The pooled path was about 33% lower at p50 and 42% lower at p95 in this run. Both non-regression gates and the desired p50 improvement gate passed. This is comparative Windows/network evidence rather than an Xbox latency promise; Xbox hardware remains the release authority.

The probe reads credentials only from a transient process environment or in-memory credential, never emits them, and writes detailed output only to ignored `*.local.json`. It attempts the official Emby `POST /Sessions/Logout` cleanup after sampling. The validation server returned 404 for that route, including uncredentialed root and common-prefix probes, so the result records `sessionCleanup: unsupported` instead of claiming token revocation. The process still clears all local credential variables. See the [Emby authentication protocol](https://dev.emby.media/doc/restapi/User-Authentication.html).

## Automated Evidence

- Web tests cover bridge timeout behavior, browser-fetch receiver binding, Emby request headers and DTO mapping, direct-first transport selection, native fallback selection, URL scoping, and HTTP error behavior.
- Core tests cover the shared Emby authorization value and source contracts for WebView2 hosting, exact-origin bridge validation, native-only playback launch, HMR source resolution, and removal of semantic native catalog commands and web video playback.
- `tools\Write-WebViewDevServerUrl.tests.ps1` covers latest-package selection, HTTP/HTTPS validation, LocalState output, and clearing the setting.
- The modern Debug x64 solution and NativeAOT package build complete with the generated React assets under `WebCode`.

## Network Finding

The real Emby endpoint was reachable and `/System/Info/Public` returned HTTP 200. A request with the WebView development Origin returned no `Access-Control-Allow-Origin`. The preflight for `Authorization` and `X-Emby-Token` returned HTTP 404 and no CORS headers.

Result: browser direct fetch cannot be the only transport for this server. React tries browser fetch first. On a browser network `TypeError`, the transport switches once to native `emby.get`. The native handler adds the same Emby authorization used by existing native code and allows only relative GET paths for the saved user's Views and Items endpoints on the saved server. Image elements continue to use Emby's existing `api_key` URL convention.

The 2026-07-11 repeat checked both `https://app.noira.local` and Vite loopback origins. Both public GETs returned 200 without `Access-Control-Allow-Origin`; both authenticated-header preflights returned 404. Direct WebView fetch therefore remains unsupported by the current server configuration.

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

For the 2026-07-11 performance package, a fresh launch restored the saved native session and exposed the React `Libraries` heading, logout/refresh actions, and the private library button set. Windows Computer Use then reported the UWP frame as minimized even after an activation/rehydration retry, so it could not safely establish DOM click geometry for a fresh item-detail/playback traversal. No fallback keyboard/mouse automation was used. The full interaction evidence above is from the immediately preceding package, while this pass changed only metadata transport/bridge code and left `PlaybackPage` and native playback untouched; a fresh post-change playback traversal remains an explicit manual/Xbox check rather than being reported as completed.

## Usability Finding

Unconstrained Emby images initially expanded list buttons beyond the viewport, which made an automation click target land off-screen. The web CSS now gives list images a stable `96px` square and gives detail images a constrained poster aspect ratio. The obsolete web-video CSS rule was removed.

## Remaining Xbox Work

- No Xbox device was connected during this run, so WebView2 runtime availability, LAN firewall routing, controller focus, memory pressure, remote DevTools, and actual Xbox codec/HDR behavior remain unverified on hardware.
- Xbox HMR should use the development PC LAN URL plus an allowed Node.js firewall rule; the same-PC loopback exemption is only for local Windows validation.
- The adaptive transport should be exercised on Xbox with both a CORS-enabled server and a CORS-blocked server.
