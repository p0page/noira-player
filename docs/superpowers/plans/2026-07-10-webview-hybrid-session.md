# WebView Hybrid Session Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a packaged WebView2 React spike that reuses native Emby login/session storage and native `PlaybackPage`, while React directly fetches real catalog data and supports Vite HMR.

**Architecture:** Native exposes auth bootstrap/login/logout, a user-scoped JSON GET fallback, and native playback launch. React keeps bootstrap credentials in memory and uses a small typed Emby client for views, item lists, and details; it tries browser fetch first and switches once to native GET only when browser networking fails. Debug startup may resolve a Vite URL from LocalState; packaged assets remain the fallback.

**Tech Stack:** UWP/WinUI 2 WebView2, .NET 10, React 19.2.7, Vite 8.1.4, TypeScript 7.0.2, Vitest 4.1.10.

## Global Constraints

- Work only in `C:\Users\yqzzx\Documents\Next Gen Xbox Emby\.worktrees\webview-react-vite-spike` on `codex/webview-react-vite-spike`.
- Preserve `ApplicationDataSessionStore`, `LoginViewModel`, `EmbyApiClient`, `PlaybackLaunchRequest`, and `PlaybackPage` as the proven native boundaries.
- Keep the Emby token in React memory only; never use browser persistence or logs.
- React owns catalog endpoints and DTO mapping. Do not add native `home.load`, `items.list`, or `item.get` handlers; `emby.get` may proxy only saved-user Views/Items JSON GET after a browser network failure.
- Playback is native only. Do not add `<video>`, `playback.getDirectStream`, or a web player.
- Bridge messages are accepted only from the resolved packaged or Vite origin.
- WebView bridge failures are visible errors; never silently use mock data inside WebView2.
- Keep the UI basic DOM and do not spend work on visual styling.

---

### Task 1: Shared Emby Authorization Value

**Files:**
- Modify: `src/NoiraPlayer.Core/Emby/EmbyAuthorization.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/Emby/EmbyAuthenticationTests.cs`

**Interfaces:**
- Produces: `EmbyAuthorization.CreateHeaderValue(EmbyClientOptions, EmbySession?) -> string`

- [ ] Add a failing test asserting the exact authenticated header value includes scheme, user ID, client, device, device ID, and version.
- [ ] Run the targeted Core test and confirm it fails because the public helper is absent.
- [ ] Implement `CreateHeaderValue` and make `Apply` consume the same value without changing existing request semantics.
- [ ] Run the targeted test and all Emby authentication tests.

### Task 2: Native Auth And Playback Bridge

**Files:**
- Create: `src/NoiraPlayer.App/Web/NoiraWebBridge.cs`
- Create: `src/NoiraPlayer.App/Web/NoiraWebBridgeResult.cs`
- Modify: `src/NoiraPlayer.App/MainPage.xaml.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/AppHostedQualityCaptureContractTests.cs`

**Interfaces:**
- Consumes: `EmbyAuthorization.CreateHeaderValue`, `LoginViewModel`, `ApplicationDataSessionStore`, and `PlaybackLaunchRequest`.
- Produces: bridge commands `auth.bootstrap`, `auth.login`, `auth.logout`, `emby.get`, and `playback.nativePlayItem`.

- [ ] Add failing source-contract tests for the four-command allowlist, real session/login dependencies, async dispatch, exact-origin validation, and absence of catalog/web-player commands.
- [ ] Run the targeted design tests and confirm the new assertions fail.
- [ ] Implement typed parsing/validation and stable JSON responses in `NoiraWebBridge`.
- [ ] Replace `CreateDemoBridgeResponse` with async bridge dispatch in `MainPage` and preserve native `Frame.Navigate(typeof(PlaybackPage), request)`.
- [ ] Run targeted design/playback contract tests.

### Task 3: Direct React Emby Client

**Files:**
- Create: `src/NoiraPlayer.Web/src/types.ts`
- Create: `src/NoiraPlayer.Web/src/emby.ts`
- Create: `src/NoiraPlayer.Web/src/emby.test.ts`

**Interfaces:**
- Consumes: in-memory `SessionBootstrap` from native.
- Produces: `EmbyWebClient.getViews`, `getItems`, `getItem`, and `getImageUrl`.

- [ ] Add failing Vitest cases for request headers, URL encoding, response mapping, resume/source hints, image URLs, and non-success errors.
- [ ] Run `npm test -- --run src/emby.test.ts` and confirm failure because the client is absent.
- [ ] Implement the minimal fetch client and DTO mapping.
- [ ] Re-run the targeted tests and then all web tests.

### Task 4: Real React Login And Browse Flow

**Files:**
- Modify: `src/NoiraPlayer.Web/src/bridge.ts`
- Modify: `src/NoiraPlayer.Web/src/bridge.test.ts`
- Modify: `src/NoiraPlayer.Web/src/App.tsx`
- Create: `src/NoiraPlayer.Web/src/transport.ts`
- Create: `src/NoiraPlayer.Web/src/transport.test.ts`

**Interfaces:**
- Consumes: `auth.bootstrap/login/logout`, `EmbyWebClient`, and `playback.nativePlayItem`.
- Produces: minimal login, libraries, items, details, retry/error, logout, and native-play actions.

- [ ] Add failing bridge tests proving WebView timeout rejects by default and bootstrap mock data is browser-only.
- [ ] Run the bridge test and confirm the timeout expectation fails against the current fallback behavior.
- [ ] Restrict bridge command types and change WebView timeout behavior.
- [ ] Replace native semantic catalog commands in `App` with `EmbyWebClient` calls and wrap every async UI action in loading/error handling.
- [ ] Add a direct-first transport that switches once to `emby.get` after browser `TypeError`, without falling back on normal HTTP error responses.
- [ ] Run web tests, typecheck, and production build.

### Task 5: Debug Vite Source Resolver

**Files:**
- Create: `src/NoiraPlayer.App/Web/WebViewSourceResolver.cs`
- Create: `tools/Write-WebViewDevServerUrl.ps1`
- Create: `tools/Write-WebViewDevServerUrl.tests.ps1`
- Modify: `src/NoiraPlayer.App/MainPage.xaml.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs`

**Interfaces:**
- Produces: a resolved page URI and exact allowed origin; LocalState file name `webview-dev-url.txt`.

- [ ] Add failing C# source-contract and PowerShell tests for packaged fallback, HTTP/HTTPS validation, latest installed package selection, and LocalState output.
- [ ] Run both targeted tests and confirm failures.
- [ ] Implement Debug-only LocalState resolution and the writer helper.
- [ ] Run tests, start Vite, write the dev URL, launch the app, edit a visible text marker, and verify HMR without rebuilding UWP.
- [ ] Remove the dev URL file and verify packaged fallback after relaunch.

### Task 6: Package, Deploy, And Interaction Proof

**Files:**
- Create: `docs/qa/webview-react-vite-spike.md`

**Interfaces:**
- Consumes: the complete hybrid shell.
- Produces: repeatable evidence and explicit Windows/Xbox limitations.

- [ ] Run `npm test -- --run`, `npm run typecheck`, and `npm run build` in `src/NoiraPlayer.Web`.
- [ ] Run the full Core test project.
- [ ] Run `tools/Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64`.
- [ ] Stop any running app instance, register the fresh Debug x64 package, and launch it.
- [ ] Use Windows UI automation to exercise login/bootstrap, library navigation, item details, and Play navigation into native `PlaybackPage`; record anything blocked by unavailable private credentials or CORS.
- [ ] Inspect the built output for `WebCode/index.html` and hashed chunks.
- [ ] Record packaged fallback, Vite HMR, direct-fetch/CORS, native playback, and Xbox-device verification status in the QA document.
