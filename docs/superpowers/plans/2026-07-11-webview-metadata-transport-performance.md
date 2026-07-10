# WebView Metadata Transport Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 优化 WebView CORS 失败后的严格 native metadata GET transport，使其复用连接、降低 bridge 调度开销、提供可测量的分层时延，并在不泄露私人服务器信息的前提下完成真实服务器和打包应用验收。

**Architecture:** React 继续拥有 Emby catalog URL、DTO 和页面状态，浏览器直连仍是首选；发生浏览器网络错误后，同一个 `EmbyWebClient` 只切换一次到 native GET。Native bridge 保留用户和路径白名单，但把逐请求创建的 `HttpClient` 替换为 Core 中可注入、长期存活的 `EmbyMetadataTransport`；transport 使用一个 `SocketsHttpHandler` 连接池、逐请求鉴权、禁用 cookie/redirect，并返回不含 URL 或身份信息的 timing/bytes 指标。播放继续使用现有 `PlaybackPage` 页面级客户端和 native core。

**Tech Stack:** .NET 10, UWP AppContainer, NativeAOT, `System.Net.Http`, WinUI 2 WebView2, React 19, TypeScript 7, Vitest 4, PowerShell 5.1-compatible QA probe.

## Global Constraints

- Work only in `C:\Users\yqzzx\Documents\Next Gen Xbox Emby\.worktrees\webview-react-vite-spike` on `codex/webview-react-vite-spike`.
- Never write a private server URL, username, password, token, item ID, media-source ID, response body, or private title into tracked files, command summaries, test snapshots, or logs.
- Real-server credentials may exist only in process environment variables or an in-memory `PSCredential`; clear them after the probe.
- Preserve the direct-first React model and the existing one-way transition to native mode after browser `TypeError`.
- Preserve native path validation: GET only, saved server only, saved user only, and only `Views`, `Items`, or `Items/{id}`.
- Do not add POST/PUT/DELETE proxy commands, arbitrary URLs, cookies, automatic redirects, web video, or direct-stream bridge commands.
- Do not modify `PlaybackPage`, playback source/audio/subtitle selection, progress reporting, or native playback core as part of this plan.
- Browser/WebView networking, native metadata networking, and native playback networking remain separate clients and separate connection pools.
- Do not add `Microsoft.Extensions.Http` or a DI container for a single transport. Reconsider `IHttpClientFactory` only when the app has multiple named policies or a real application service container.

## Current Baseline

- `NoiraWebBridge.GetEmbyAsync` currently creates and disposes a new `HttpClient` for every metadata GET, so every call owns a fresh handler and connection pool.
- React makes approximately one JSON request per transition: libraries, a 50-item page, and one item detail. Images stay in Chromium and media stays in native playback.
- `requestBridge` currently attaches one WebView `message` listener per in-flight request; every native response is observed by all active listeners before request IDs filter it.
- Native reads the complete JSON response as a string and quotes it into the outer bridge envelope. This copy remains acceptable for the current bounded metadata pages, but must be measured before any envelope redesign.
- Unauthenticated CORS probes against the private validation endpoint on 2026-07-11 found, for both packaged and Vite origins: public GET returned 200 without `Access-Control-Allow-Origin`; authenticated-header OPTIONS returned 404 without CORS headers. Direct browser API access is therefore unsupported by the current server configuration.

## Acceptance Standards

### Hard Gates

1. `NoiraWebBridge` contains no per-request `new HttpClient` and dispatches every allowed metadata GET through one bridge-owned `EmbyMetadataTransport`.
2. The default transport owns one long-lived `SocketsHttpHandler`/`HttpClient`; repeated requests use that same client concurrently without locks or a custom client-object pool.
3. Handler policy sets `AllowAutoRedirect = false`, `UseCookies = false`, automatic response decompression, finite `PooledConnectionLifetime`, and finite `PooledConnectionIdleTimeout`.
4. Authentication remains on each `HttpRequestMessage`; the shared client has no session token or server-specific `DefaultRequestHeaders`/`BaseAddress`.
5. HTTP status responses, including 401/403/5xx, are returned to React and do not cause transport switching or hidden retries.
6. Native responses expose only `networkMs` and `bodyBytes` diagnostics. They never expose request URLs, tokens, user IDs, item IDs, or bodies as diagnostics.
7. A native response becomes a synthetic browser `Response` with `X-Noira-Transport: native`, `X-Noira-Network-Ms`, `X-Noira-Bridge-Ms`, and `X-Noira-Body-Bytes` headers.
8. The WebView bridge uses one listener per WebView host with a pending-request map; timeout and completion both remove the request entry.
9. Existing strict URL/path tests remain green, and new tests cover redirect/cookie policy by source contract plus per-request authentication behavior through a real `HttpMessageHandler` test double.
10. Web tests, Core tests, PowerShell probe tests, TypeScript checking, Vite production build, Debug x64 NativeAOT package build, registration, and launch all succeed.
11. The packaged app loads the private account, opens a library and item detail, then enters the existing native `PlaybackPage`; native source/audio/subtitle controls remain available.
12. A changed-file secret scan finds none of the supplied private URL, username, password, access token, user ID, item ID, or media-source ID.

### Private Server Performance Gate

The private probe alternates pooled and per-request client modes against the same authenticated, bounded metadata endpoint. It performs one unrecorded warm-up and at least 12 recorded samples per mode.

- Every recorded request must return the same successful status class and a non-empty JSON body.
- No request may exceed `EmbyRequestTimeoutPolicy.InteractiveRequestTimeout`.
- Pooled warm `p50` must be no worse than `110%` of per-request `p50`.
- Pooled warm `p95` must be no worse than `115%` of per-request `p95`.
- The desired optimization signal is pooled `p50 <= 85%` of per-request `p50`; missing the desired signal is recorded, not hidden, provided the hard non-regression gates pass.
- Timing output contains only mode, status, byte count, sample count, min, p50, p95, and max. Host, account, token, IDs, headers, and response content are omitted.

These relative gates are local acceptance evidence, not CI gates: a private WAN endpoint and CDN edge can vary between runs. Structural connection-lifetime tests remain the deterministic regression guard.

### CORS Direct-Access Gate

Browser direct access is considered supported for an origin only when both conditions hold:

1. A GET carrying that `Origin` receives `Access-Control-Allow-Origin` matching the origin or `*` where credentials are not used.
2. An OPTIONS request for GET plus `authorization,x-emby-token` returns 2xx and permits the origin, method, and both headers.

If either condition fails, the expected packaged behavior is one failed browser attempt followed by persistent native mode for that React client.

---

### Task 1: Document Baseline And Privacy-Safe Probe Contract

**Files:**
- Create: `docs/superpowers/plans/2026-07-11-webview-metadata-transport-performance.md`
- Modify: `docs/qa/webview-react-vite-spike.md`

**Interfaces:**
- Produces: the hard gates, relative performance gates, CORS decision rule, and secret-handling contract used by all later tasks.

- [x] **Step 1: Inspect the live request path and lifecycle**

Confirm `MainPage -> NoiraWebBridge -> new HttpClient -> Emby` for metadata and confirm that `PlaybackPage` already owns a page-scoped `_httpClient` disposed on unload.

- [x] **Step 2: Run unauthenticated CORS probes**

Send GET and OPTIONS requests for packaged and Vite origins without credentials. Record only status and presence/absence of CORS headers.

- [x] **Step 3: Write this plan before production edits**

Do not include the private endpoint or identity in the plan.

### Task 2: Test-Drive The Long-Lived Core Transport

**Files:**
- Create: `src/NoiraPlayer.Core/Emby/EmbyMetadataTransport.cs`
- Create: `tests/NoiraPlayer.Core.Tests/Emby/EmbyMetadataTransportTests.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/TestHttpMessageHandler.cs`

**Interfaces:**
- Produces: `EmbyMetadataTransport.CreateDefault() -> EmbyMetadataTransport`.
- Produces: `EmbyMetadataTransport.GetAsync(Uri, EmbyClientOptions, EmbySession, CancellationToken) -> Task<EmbyMetadataResponse>`.
- Produces: `EmbyMetadataResponse` fields `StatusCode`, `ReasonPhrase`, `Body`, `NetworkDurationMilliseconds`, and `BodyLengthBytes`.

- [ ] **Step 1: Add failing lifecycle and request tests**

Tests must send two sequential requests through one transport and assert request count, GET method, URL, Accept header, Emby authorization, token, status/body passthrough, positive/non-negative timing, and that the injected handler remains usable until the owning transport is disposed.

- [ ] **Step 2: Run the focused tests and observe RED**

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~EmbyMetadataTransportTests -v minimal
```

Expected: compilation failure because `EmbyMetadataTransport` and `EmbyMetadataResponse` do not exist.

- [ ] **Step 3: Implement the minimal transport**

The default client configuration must follow this shape:

```csharp
var handler = new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    UseCookies = false,
    AutomaticDecompression = DecompressionMethods.GZip |
        DecompressionMethods.Deflate |
        DecompressionMethods.Brotli,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
};

var client = new HttpClient(handler)
{
    Timeout = EmbyRequestTimeoutPolicy.InteractiveRequestTimeout,
    DefaultRequestVersion = HttpVersion.Version20,
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
};
```

`GetAsync` must create a new request, apply per-request headers, call `SendAsync` with `ResponseHeadersRead`, read the body with cancellation, and return status rather than calling `EnsureSuccessStatusCode`.

- [ ] **Step 4: Run focused and complete Core tests**

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~EmbyMetadataTransportTests -v minimal
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj -v minimal
```

### Task 3: Integrate The Transport Into The Strict Native Bridge

**Files:**
- Modify: `src/NoiraPlayer.App/Web/NoiraWebBridge.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs`

**Interfaces:**
- Consumes: `EmbyMetadataTransport.CreateDefault` and `GetAsync`.
- Extends native `emby.get` result with `timing.networkMs` and `timing.bodyBytes`.

- [ ] **Step 1: Add failing source-contract assertions**

Assert that the bridge owns `_metadataTransport`, calls `EmbyMetadataTransport.CreateDefault`, calls `_metadataTransport.GetAsync`, includes timing fields, and no longer contains `using var http = new HttpClient` or direct `http.SendAsync`.

- [ ] **Step 2: Run the design test and observe RED**

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter Modern_App_Primary_Shell_Is_WebView2_Hosted_React_Vite_Surface -v minimal
```

- [ ] **Step 3: Inject and reuse one transport**

The bridge constructors must create or accept one transport. `GetEmbyAsync` keeps all existing session/path validation, builds the saved-server absolute URI, delegates the request, and serializes only status/body/timing into the existing response contract.

- [ ] **Step 4: Re-run focused Core tests**

Run the Task 2 transport tests and the modern UWP source-contract test.

### Task 4: Reduce Web Bridge Listener Fan-Out And Surface Timing

**Files:**
- Modify: `src/NoiraPlayer.Web/src/bridge.ts`
- Modify: `src/NoiraPlayer.Web/src/bridge.test.ts`
- Modify: `src/NoiraPlayer.Web/src/transport.ts`
- Modify: `src/NoiraPlayer.Web/src/transport.test.ts`

**Interfaces:**
- Produces: one `BridgeState` per `WebViewHost`, stored in a `WeakMap`, with one message listener and a pending request map.
- Extends `NativeEmbyGetResult` with optional `timing: { networkMs: number; bodyBytes: number }`.

- [ ] **Step 1: Add failing listener and timing tests**

Two requests through the same fake WebView host must attach one listener, route responses by ID, remove completed/expired pending entries, and preserve timeout rejection. Native transport tests must assert the four synthetic diagnostic headers and confirm direct HTTP responses are returned untouched.

- [ ] **Step 2: Run targeted Vitest and observe RED**

```powershell
npm test -- --run src/bridge.test.ts src/transport.test.ts
```

- [ ] **Step 3: Implement the per-host pending registry**

Create the request ID and timeout as before, but store resolve/reject/timeout in one host state. The single listener removes the matching pending entry before resolving or rejecting.

- [ ] **Step 4: Add native timing response headers**

Measure bridge round-trip with `performance.now()` when available and `Date.now()` otherwise. Do not add URL or identity headers.

- [ ] **Step 5: Run all web verification**

```powershell
npm test -- --run
npm run typecheck
npm run build
```

### Task 5: Add A Privacy-Safe CORS And Connection-Reuse Probe

**Files:**
- Create: `tools/Test-NoiraHybridTransport.ps1`
- Create: `tools/Test-NoiraHybridTransport.tests.ps1`

**Interfaces:**
- Consumes environment variables `NOIRAPLAYER_QA_SERVER_URL`, `NOIRAPLAYER_QA_USERNAME`, and `NOIRAPLAYER_QA_PASSWORD`, or an in-memory `PSCredential`.
- Produces sanitized JSON schema `noira.hybrid-transport-probe.v1` on stdout or an optional ignored `*.local.json` path.

- [ ] **Step 1: Write a failing PowerShell contract test**

The test must assert required-secret failure, rejection of non-HTTPS URLs unless explicitly allowed, no secret values in output/errors, deterministic percentile calculation, CORS classification, and sanitized JSON field allowlisting.

- [ ] **Step 2: Run the script test and observe RED**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-NoiraHybridTransport.tests.ps1
```

Expected: failure because the probe script does not exist.

- [ ] **Step 3: Implement CORS and authenticated benchmark modes**

Authenticate in memory, select a bounded user Views/Items endpoint without emitting IDs, alternate shared-client and new-client requests, discard warm-up, calculate min/p50/p95/max, and emit only allowlisted metrics. Dispose every temporary client and clear plaintext password variables in `finally`.

- [ ] **Step 4: Run the script tests**

Re-run the PowerShell contract test with only synthetic/local fixtures; no real secrets are used by the committed test.

- [ ] **Step 5: Run the real private probe ephemerally**

Set credentials only in a transient PowerShell process, write any detailed result to `docs/qa/private/hybrid-transport-probe.local.json`, clear environment variables, and summarize only sanitized metrics in the tracked QA record.

### Task 6: Package, Deploy, And Verify The Real App

**Files:**
- Modify: `docs/qa/webview-react-vite-spike.md`

**Interfaces:**
- Consumes the optimized transport and probe output.
- Produces repeatable automated, packaged, interaction, CORS, and performance evidence.

- [ ] **Step 1: Run all automated checks fresh**

```powershell
npm test -- --run
npm run typecheck
npm run build
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Write-WebViewDevServerUrl.tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-NoiraHybridTransport.tests.ps1
```

- [ ] **Step 2: Build and register Debug x64 NativeAOT**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Register-NoiraModernUwp.ps1 -Configuration Debug -Platform x64 -SkipBuild
```

- [ ] **Step 3: Exercise the packaged flow**

Launch the package, bootstrap or log in using the private credentials without persistence outside `PasswordVault`, load libraries, open a bounded item list and detail, launch native playback, and confirm source/audio/subtitle controls.

- [ ] **Step 4: Inspect timing and fallback behavior**

Confirm the first browser network failure switches the React client once, subsequent metadata requests use native mode, and native timing headers contain finite non-negative values.

- [ ] **Step 5: Update QA evidence without private data**

Record test counts, build/package identity, CORS classification, sanitized p50/p95 ratios, functional flow, and remaining Xbox hardware limitations.

- [ ] **Step 6: Run secret and diff checks**

Search changed tracked files for the supplied private values plus token-like fields, inspect `git diff --check`, verify `git status --short`, and commit only after the scan is clean.

## Deferred Work

- Removing the outer JSON body-string copy requires a typed binary or raw-JSON bridge protocol and is deferred until measurements show bridge serialization is material.
- Request cancellation propagated from React through WebMessage to `HttpClient` is deferred because the current UI serializes its primary navigation actions; pending-map cleanup still prevents web-side listener accumulation.
- Sharing one handler between login, metadata, legacy native pages, and playback is explicitly deferred. Their ownership, timeout, redirect, and streaming policies differ.
- Xbox hardware p50/p95, suspend/resume behavior, network changes, controller interaction, and memory pressure remain separate hardware gates.
