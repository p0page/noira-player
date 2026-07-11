# WebView TV Browse UI Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver a real-data WebView2 TV browsing flow for Home, library, and details with deterministic directional focus and native playback handoff.

**Architecture:** React owns catalog pages and uses a Noira `FocusNavigationPolicy` facade over Norigin Spatial Navigation. React uses the existing direct-first Emby transport plus a narrowly expanded native GET fallback; UWP keeps durable credentials and native playback. Pages declare focus scopes and route intent, while global policy owns directional input, focus history, boundaries, Back priority, and restoration.

**Tech Stack:** React 19.2.7, TypeScript 7, Vite 8, Vitest 4, Norigin Spatial Navigation 3.2.1, Testing Library 16, jsdom 29, Lucide React 1.24, .NET 10 UWP/WinUI 2 WebView2, xUnit.

---

Use `@superpowers:test-driven-development` for every behavior change and `@superpowers:verification-before-completion` before each commit. Run every named RED command before editing production code and confirm the expected failure. Do not add a fictional catalog, fake poster set, or UI fixture route.

### Task 1: Prepare private QA boundaries and Web dependencies

**Files:**
- Modify: `.gitignore`
- Modify: `src/NoiraPlayer.Web/package.json`
- Modify: `src/NoiraPlayer.Web/package-lock.json`

**Step 1: Re-run the baseline**

```powershell
npm test --prefix src\NoiraPlayer.Web
npm run typecheck --prefix src\NoiraPlayer.Web
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj -v minimal
```

Expected: Web 15/15 and Core 820/820 pass.

**Step 2: Add the approved ignore boundary**

Add `.private/` to `.gitignore`. Prefer the existing native `PasswordVault` session. Create `.private/emby-test.local.env` only if real-data re-login automation needs it, and never expose the values as `VITE_*` variables.

**Step 3: Verify ignore behavior**

```powershell
git check-ignore -v .private/emby-test.local.env
```

Expected: exit 0 and the `.private/` rule is reported.

**Step 4: Install exact dependencies**

Run from `src/NoiraPlayer.Web`:

```powershell
npm install @noriginmedia/norigin-spatial-navigation@3.2.1 lucide-react@1.24.0
npm install --save-dev @testing-library/dom@10.4.1 @testing-library/react@16.3.2 jsdom@29.1.1
```

**Step 5: Verify and commit**

```powershell
npm test --prefix src\NoiraPlayer.Web
npm run typecheck --prefix src\NoiraPlayer.Web
git diff --check
git add .gitignore src/NoiraPlayer.Web/package.json src/NoiraPlayer.Web/package-lock.json
git commit -m "chore: prepare Web TV UI dependencies"
```

### Task 2: Remove production browser mocks

**Files:**
- Modify: `src/NoiraPlayer.Web/src/bridge.test.ts`
- Modify: `src/NoiraPlayer.Web/src/bridge.ts`

**Step 1: Write failing behavior tests**

Replace browser-fallback expectations with:

```typescript
it('rejects requests outside WebView2 instead of fabricating app data', async () => {
  await expect(requestBridge('auth.bootstrap')).rejects.toThrow(
    'Noira catalog requires the WebView2 host.',
  );
});

it('never fabricates a successful native playback launch', async () => {
  await expect(
    requestBridge('playback.nativePlayItem', { itemId: 'anonymous-item' }),
  ).rejects.toThrow('Noira catalog requires the WebView2 host.');
});
```

**Step 2: Run RED**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/bridge.test.ts
```

Expected: FAIL because browser mocks still return success.

**Step 3: Implement the minimal behavior**

Use this no-host branch and delete `getBrowserMockResponse` plus `getPayloadValue`:

```typescript
if (!isWebViewBridgeAvailable()) {
  throw new Error('Noira catalog requires the WebView2 host.');
}
```

**Step 4: Run GREEN and commit**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/bridge.test.ts
npm run typecheck --prefix src\NoiraPlayer.Web
git add src/NoiraPlayer.Web/src/bridge.ts src/NoiraPlayer.Web/src/bridge.test.ts
git commit -m "refactor: require native host for Web catalog"
```

### Task 3: Expand the native GET fallback with a testable path policy

**Files:**
- Create: `src/NoiraPlayer.Core/Emby/EmbyWebPathPolicy.cs`
- Create: `tests/NoiraPlayer.Core.Tests/Emby/EmbyWebPathPolicyTests.cs`
- Modify: `src/NoiraPlayer.App/Web/NoiraWebBridge.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs`

**Step 1: Write path-policy tests**

Cover these paths with `[Theory]` rows:

```csharp
[InlineData("Users/user-1/Views?Fields=ImageTags", true)]
[InlineData("Users/user-1/Items/Resume?IncludeItemTypes=Movie%2CEpisode&Limit=24", true)]
[InlineData("Users/user-1/Items/Latest?ParentId=library-1&Limit=24", true)]
[InlineData("Users/user-1/Items/item-1?Fields=Overview", true)]
[InlineData("Shows/NextUp?UserId=user-1&Limit=24", true)]
[InlineData("Shows/NextUp?UserId=other-user&Limit=24", false)]
[InlineData("Shows/NextUp?Limit=24", false)]
[InlineData("Users/other-user/Items/Resume?Limit=24", false)]
[InlineData("Users/user-1/Items/item-1/Images", false)]
[InlineData("https://outside.invalid/Users/user-1/Views", false)]
[InlineData("../Users/user-1/Views", false)]
```

Assert `EmbyWebPathPolicy.IsAllowed(session, path)` for a session whose user ID is `user-1`.

**Step 2: Run RED**

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~EmbyWebPathPolicyTests -v minimal
```

Expected: compile failure because the policy does not exist.

**Step 3: Implement and connect the policy**

Create this contract:

```csharp
namespace NoiraPlayer.Core.Emby
{
    public static class EmbyWebPathPolicy
    {
        public static bool IsAllowed(EmbySession session, string path);
    }
}
```

Parse the path and query structurally. Preserve rejection of absolute paths, fragments, backslashes, and dot segments. Allow only exact current-user `Views`, `Items`, `Items/{single-id}`, `Items/Resume`, `Items/Latest`, and `Shows/NextUp`. Require exactly one decoded `UserId` matching the session for NextUp. Replace the bridge's private prefix validator with this policy.

**Step 4: Run GREEN and commit**

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter "FullyQualifiedName~EmbyWebPathPolicyTests|FullyQualifiedName~ModernUwpSolutionContractTests" -v minimal
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj -v minimal
git add src/NoiraPlayer.Core/Emby/EmbyWebPathPolicy.cs src/NoiraPlayer.App/Web/NoiraWebBridge.cs tests/NoiraPlayer.Core.Tests/Emby/EmbyWebPathPolicyTests.cs tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs
git commit -m "feat: allow bounded Web home metadata routes"
```

### Task 4: Add real Emby TV catalog contracts

**Files:**
- Modify: `src/NoiraPlayer.Web/src/types.ts`
- Modify: `src/NoiraPlayer.Web/src/emby.test.ts`
- Modify: `src/NoiraPlayer.Web/src/emby.ts`

**Step 1: Write failing endpoint and mapping tests**

Test `getResumeItems(24)`, `getNextUpItems(24)`, `getLatestItems(options)`, and paged `getItemsPage(parentId, startIndex, limit)`. Test mapping for production year, runtime, series/episode context, playback position, media source, and Primary/Thumb/Banner/Backdrop candidates.

Use these public shapes:

```typescript
export interface MediaArtwork {
  primary?: string;
  thumb?: string;
  banner?: string;
  backdrop?: string;
}

export interface MediaItem {
  id: string;
  name: string;
  type: string;
  productionYear?: number;
  seriesName?: string;
  indexNumber?: number;
  parentIndexNumber?: number;
  overview?: string;
  runtimeTicks?: number;
  startPositionTicks?: number;
  mediaSourceId?: string;
  artwork: MediaArtwork;
}

export interface ItemPage {
  items: MediaItem[];
  startIndex: number;
  totalRecordCount: number;
}
```

**Step 2: Run RED**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/emby.test.ts
```

Expected: missing methods and fields.

**Step 3: Implement minimal catalog methods**

Use endpoint semantics already proven by `NoiraPlayer.Core.Emby.EmbyApiClient`. Keep URL construction in `EmbyWebClient`. Components receive explicit candidate URLs and never infer Emby paths. Replace the fixed 50-item list method with `getItemsPage`.

**Step 4: Run GREEN and commit**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/emby.test.ts
npm run typecheck --prefix src\NoiraPlayer.Web
git add src/NoiraPlayer.Web/src/types.ts src/NoiraPlayer.Web/src/emby.ts src/NoiraPlayer.Web/src/emby.test.ts
git commit -m "feat: add real TV catalog queries"
```

### Task 5: Add deterministic Home aggregation

**Files:**
- Create: `src/NoiraPlayer.Web/src/catalog/homeCatalog.ts`
- Create: `src/NoiraPlayer.Web/src/catalog/homeCatalog.test.ts`

**Step 1: Write failing service tests**

Define:

```typescript
export type HomeRowKind = 'resume' | 'nextUp' | 'libraries' | 'latest';

export interface HomeRow {
  key: string;
  title: string;
  kind: HomeRowKind;
  items: readonly (MediaItem | LibraryView)[];
}

export interface HomeCatalog {
  rows: HomeRow[];
  failedKinds: HomeRowKind[];
}

export async function loadHomeCatalog(client: HomeCatalogClient): Promise<HomeCatalog>;
export async function loadLibraryLatestRows(
  client: HomeCatalogClient,
  libraries: readonly LibraryView[],
): Promise<HomeRow[]>;
```

Prove all four core calls begin before any resolves, empty rows are omitted, one failure does not remove successful rows, and supplemental library rows preserve library order. Anonymous IDs in this hermetic service test are not a product fixture mode.

**Step 2: Run RED**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/catalog/homeCatalog.test.ts
```

Expected: module not found.

**Step 3: Implement with `Promise.allSettled`**

Build rows only after the core group settles, in `resume`, `nextUp`, `libraries`, `latest` order. Supplemental rows append below current rows and never reorder them.

**Step 4: Run GREEN and commit**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/catalog/homeCatalog.test.ts
npm run typecheck --prefix src\NoiraPlayer.Web
git add src/NoiraPlayer.Web/src/catalog/homeCatalog.ts src/NoiraPlayer.Web/src/catalog/homeCatalog.test.ts
git commit -m "feat: aggregate Home rows without focus churn"
```

### Task 6: Build route history and the Noira focus policy

**Files:**
- Create: `src/NoiraPlayer.Web/src/navigation/routes.ts`
- Create: `src/NoiraPlayer.Web/src/navigation/routes.test.ts`
- Create: `src/NoiraPlayer.Web/src/focus/focusPolicy.ts`
- Create: `src/NoiraPlayer.Web/src/focus/focusPolicy.test.ts`

**Step 1: Write failing pure-policy tests**

Use these contracts:

```typescript
export type BrowseRoute =
  | { kind: 'home' }
  | { kind: 'library'; libraryId: string; originFocusKey: string }
  | { kind: 'details'; itemId: string; libraryId: string; originFocusKey: string };

export type BackDecision =
  | { kind: 'closeGuide' }
  | { kind: 'navigate'; route: BrowseRoute; restoreFocusKey: string }
  | { kind: 'nativeBack' };

export interface FocusNavigationPolicy {
  remember(scopeKey: string, focusKey: string): void;
  resolve(scopeKey: string, availableKeys: readonly string[], defaultKey?: string): string | null;
  decideBack(routeStack: readonly BrowseRoute[], guideOpen: boolean): BackDecision;
  pause(): void;
  resume(): void;
}
```

Cover Home to library to details, exact Back restoration, Guide-before-route priority, default focus order, per-scope last focus, and nearest/scope-default fallback after removal.

**Step 2: Run RED**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/navigation/routes.test.ts src/focus/focusPolicy.test.ts
```

Expected: missing modules.

**Step 3: Implement in-memory stores only**

Do not use browser storage, query strings, media titles, or debug logging. Page code must not import Norigin types through this contract.

**Step 4: Run GREEN and commit**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/navigation/routes.test.ts src/focus/focusPolicy.test.ts
npm run typecheck --prefix src\NoiraPlayer.Web
git add src/NoiraPlayer.Web/src/navigation src/NoiraPlayer.Web/src/focus/focusPolicy.ts src/NoiraPlayer.Web/src/focus/focusPolicy.test.ts
git commit -m "feat: add global browse focus policy"
```

### Task 7: Wrap Norigin behind Noira focus components

**Files:**
- Create: `src/NoiraPlayer.Web/src/focus/FocusProvider.tsx`
- Create: `src/NoiraPlayer.Web/src/focus/FocusScope.tsx`
- Create: `src/NoiraPlayer.Web/src/focus/Focusable.tsx`
- Create: `src/NoiraPlayer.Web/src/focus/focusComponents.test.tsx`
- Modify: `src/NoiraPlayer.Web/src/main.tsx`

**Step 1: Write jsdom component tests**

Start the test with `// @vitest-environment jsdom`. Verify a `Focusable` renders a real button with the supplied stable key, calls `HTMLElement.focus()`, reports focus to Noira policy, invokes `onSelect` on Enter, and does not expose Norigin methods in page props. Add one three-element geometry test that dispatches ArrowRight and observes DOM focus move right.

**Step 2: Run RED**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/focus/focusComponents.test.tsx
```

Expected: missing components.

**Step 3: Implement wrappers**

Initialize Norigin once:

```typescript
init({
  shouldFocusDOMNode: true,
  layoutAdapter: GetBoundingClientRectAdapter,
  throttle: 100,
  throttleKeypresses: true,
  debug: false,
  visualDebug: false,
});
```

`FocusScope` owns `FocusContext.Provider`, preferred child, last-focused-child behavior, and optional boundaries. `Focusable` is the only component that calls `useFocusable`. Wrap the app in `FocusProvider` from `main.tsx`.

**Step 4: Run GREEN and commit**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/focus/focusComponents.test.tsx
npm run typecheck --prefix src\NoiraPlayer.Web
git add src/NoiraPlayer.Web/src/focus src/NoiraPlayer.Web/src/main.tsx
git commit -m "feat: adapt Norigin to Noira focus scopes"
```

### Task 8: Build the shell, Guide, cards, and Home

**Files:**
- Create: `src/NoiraPlayer.Web/src/components/Guide.tsx`
- Create: `src/NoiraPlayer.Web/src/components/MediaCard.tsx`
- Create: `src/NoiraPlayer.Web/src/components/MediaRow.tsx`
- Create: `src/NoiraPlayer.Web/src/pages/HomePage.tsx`
- Create: `src/NoiraPlayer.Web/src/pages/HomePage.test.tsx`
- Modify: `src/NoiraPlayer.Web/src/App.tsx`
- Modify: `src/NoiraPlayer.Web/src/styles.css`

**Step 1: Write Home structure and keyboard tests**

Use anonymous one-item structural objects, not a reusable fake catalog. Verify first-non-empty default focus, titles and metadata below artwork, resume progress inside the image region, left-from-first-card Guide entry, right/Escape restoration, empty-row omission, and stable keys when supplemental rows append.

**Step 2: Run RED**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/pages/HomePage.test.tsx
```

Expected: missing page and components.

**Step 3: Implement shell and Home composition**

Split the monolithic `App` into session/bootstrap orchestration and route rendering. Use Lucide icons for commands. The Guide is 72 pixels collapsed and a 248 pixel matte overlay when expanded. Show only functional Home, real library, and logout destinations.

Call `loadHomeCatalog` after bootstrap. Append supplemental library rows below the core render without moving current focus. Preserve rendered content during refresh.

**Step 4: Add the first tokenized CSS pass**

Add CSS custom properties from `docs/DESIGN.md`, 56 pixel safe-area behavior, stable poster/wide ratios, title-below-artwork anatomy, focus-safe gaps, real `:focus-visible`, and reduced-motion behavior. Do not finish details-specific artwork styling here.

**Step 5: Run GREEN and commit**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/pages/HomePage.test.tsx
npm test --prefix src\NoiraPlayer.Web
npm run typecheck --prefix src\NoiraPlayer.Web
git add src/NoiraPlayer.Web/src/App.tsx src/NoiraPlayer.Web/src/components src/NoiraPlayer.Web/src/pages/HomePage.tsx src/NoiraPlayer.Web/src/pages/HomePage.test.tsx src/NoiraPlayer.Web/src/styles.css
git commit -m "feat: build real-data TV Home shell"
```

### Task 9: Add the paged library grid and exact Back restoration

**Files:**
- Create: `src/NoiraPlayer.Web/src/pages/LibraryPage.tsx`
- Create: `src/NoiraPlayer.Web/src/pages/LibraryPage.test.tsx`
- Modify: `src/NoiraPlayer.Web/src/App.tsx`
- Modify: `src/NoiraPlayer.Web/src/styles.css`

**Step 1: Write failing grid tests**

Verify the page requests page zero, appends subsequent pages without replacing existing keys, preloads in the final two visual rows, keeps directional movement within grid boundaries, opens Guide from the first column, returns to the exact Home source on Escape, restores the same item after details Back, and chooses a valid same-grid fallback after removal.

**Step 2: Run RED**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/pages/LibraryPage.test.tsx
```

Expected: missing page.

**Step 3: Implement the grid**

Use CSS grid with a fixed poster ratio and responsive column count. Append `ItemPage.items` until the total is reached and deduplicate by item ID. Scroll focused backplates into the TV-safe viewport without centering every move.

**Step 4: Run GREEN and commit**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/pages/LibraryPage.test.tsx
npm test --prefix src\NoiraPlayer.Web
npm run typecheck --prefix src\NoiraPlayer.Web
git add src/NoiraPlayer.Web/src/App.tsx src/NoiraPlayer.Web/src/pages/LibraryPage.tsx src/NoiraPlayer.Web/src/pages/LibraryPage.test.tsx src/NoiraPlayer.Web/src/styles.css
git commit -m "feat: add focus-safe media library grid"
```

### Task 10: Add details and preserve Web state across native playback

**Files:**
- Create: `src/NoiraPlayer.Web/src/pages/DetailsPage.tsx`
- Create: `src/NoiraPlayer.Web/src/pages/DetailsPage.test.tsx`
- Modify: `src/NoiraPlayer.Web/src/App.tsx`
- Modify: `src/NoiraPlayer.Web/src/styles.css`
- Modify: `src/NoiraPlayer.App/MainPage.xaml.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs`

**Step 1: Write failing details tests**

Verify Play/Resume default DOM focus, exact native playback payload, Backdrop/Thumb/Banner/Primary atmosphere order, graphite no-art fallback, Escape restoration to the library origin, and policy pause before native playback. Add a source-contract expectation that `MainPage` uses `NavigationCacheMode.Required` so normal playback Back returns to the same WebView DOM and in-memory focus state.

**Step 2: Run RED**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/pages/DetailsPage.test.tsx
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~Modern_App_Primary_Shell_Is_WebView2_Hosted_React_Vite_Surface -v minimal
```

Expected: missing page and cache contract.

**Step 3: Implement details and playback handoff**

Render deterministic left content and one right atmosphere zone. Keep Play/Resume first and do not render unavailable placeholder actions. Remember details focus and pause Web navigation before `playback.nativePlayItem`. Set this in `MainPage` constructor:

```csharp
NavigationCacheMode = NavigationCacheMode.Required;
```

This preserves the same WebView for normal Frame navigation into and back from playback. Keep Xbox memory pressure as an explicit hardware validation risk.

**Step 4: Run GREEN and commit**

```powershell
npm test --prefix src\NoiraPlayer.Web -- src/pages/DetailsPage.test.tsx
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~Modern_App_Primary_Shell_Is_WebView2_Hosted_React_Vite_Surface -v minimal
npm test --prefix src\NoiraPlayer.Web
npm run typecheck --prefix src\NoiraPlayer.Web
git add src/NoiraPlayer.Web/src/App.tsx src/NoiraPlayer.Web/src/pages/DetailsPage.tsx src/NoiraPlayer.Web/src/pages/DetailsPage.test.tsx src/NoiraPlayer.Web/src/styles.css src/NoiraPlayer.App/MainPage.xaml.cs tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs
git commit -m "feat: add artwork-backed details handoff"
```

### Task 11: Add privacy-safe real-data QA support

**Files:**
- Create: `tools/Test-WebUiPrivateData.ps1`
- Create: `tools/Test-WebUiPrivateData.tests.ps1`
- Create locally only if needed: `.private/emby-test.local.env`

**Step 1: Write failing PowerShell tests**

Use a temporary fake repository and fake private values. Verify the guard fails when a tracked file or Web `dist` contains a private value, passes when values exist only in the ignored env file, and prints categories/counts but never the matched value.

**Step 2: Run RED**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-WebUiPrivateData.tests.ps1
```

Expected: FAIL because the guard is absent.

**Step 3: Implement the guard**

Read `NOIRA_EMBY_SERVER`, `NOIRA_EMBY_USER`, and `NOIRA_EMBY_PASSWORD` from the ignored file into memory. Scan tracked files, Web `dist`, and non-private QA reports without placing private values on command lines or in output.

Use the existing native `PasswordVault` session if available. Create the local env only after `git check-ignore` succeeds, restrict it to the current Windows user, and never add it to Git.

**Step 4: Run GREEN and commit only the guard**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-WebUiPrivateData.tests.ps1
git check-ignore -v .private/emby-test.local.env
git add tools/Test-WebUiPrivateData.ps1 tools/Test-WebUiPrivateData.tests.ps1
git commit -m "test: guard private Web UI QA data"
```

### Task 12: Run real WebView2 visual and keyboard acceptance

**Files:**
- Modify: `tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs`
- Create: `docs/qa/webview-tv-browse-ui.md`
- Modify only if evidence requires a TDD fix: files from Tasks 3-10

**Step 1: Add final source contracts and confirm RED where missing**

Require the built client to contain the Noira focus facade, FocusScope wrappers, Home/Library/Details pages, real Resume/NextUp/Latest/Views/paged Items calls, title-below-artwork styles, and TV safe-area variables. Require absence of browser mock catalog, HTML video, and direct-stream bridge paths.

**Step 2: Run the full automated batch**

```powershell
npm test --prefix src\NoiraPlayer.Web
npm run typecheck --prefix src\NoiraPlayer.Web
npm run build --prefix src\NoiraPlayer.Web
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-WebUiPrivateData.tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-WebUiPrivateData.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Noira.ps1 -Target Build -Configuration Debug -Platform x64
```

Expected: all commands pass and no private-value finding appears.

**Step 3: Register and launch without credentials on the command line**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Register-NoiraModernUwp.ps1 -Configuration Debug -Platform x64 -Launch
```

**Step 4: Run one complete keyboard batch**

Use `@computer-use:computer-use`. Record all findings before fixing any:

1. Home default focus and every non-empty real row.
2. Guide open/traverse/close/restore.
3. Horizontal row ends and nearest-column Up/Down.
4. First available real library, grid boundaries, pagination, and scrolling.
5. First playable item, details default Play/Resume, and exact Escape restoration.
6. Native playback entry and Back restoration to Play/Resume.
7. Refresh with a valid focus target.
8. Layout at 1920 by 1080, 1280 by 720, and 960 by 540 logical sizing where host resizing permits.

Store screenshots only under ignored private artifacts. Inspect blank rendering, safe areas, overlaps, clipped titles, focus reflow, card ratios, and Guide overlay locally. Do not attach or summarize private content.

**Step 5: Batch-fix findings with TDD**

Add one failing test per reproducible behavior, run RED, make the minimum fix, then repeat the full keyboard batch once after grouped fixes.

**Step 6: Write anonymous QA evidence**

`docs/qa/webview-tv-browse-ui.md` records commands, test counts, anonymous row/card counts, route outcomes, viewport outcomes, and the explicit statement that Windows keyboard evidence is not Xbox hardware evidence. Include no server, account, title, item ID, source ID, token, artwork, or private screenshot.

**Step 7: Verify and commit**

Repeat Step 2, then run `git diff --check` and inspect status. Commit only tracked contracts and anonymous QA evidence:

```powershell
git add tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs docs/qa/webview-tv-browse-ui.md
git commit -m "test: verify real-data Web TV browse flow"
```

### Task 13: Final branch review

**Files:**
- Review all branch changes against `docs/plans/2026-07-11-webview-tv-browse-ui-design.md`

**Step 1: Review requirements**

Map every design acceptance criterion to automated or anonymous real-app evidence. Explicitly list Xbox-only gaps, especially controller forwarding and WebView memory pressure while native playback is active.

**Step 2: Run final commands fresh**

```powershell
npm test --prefix src\NoiraPlayer.Web
npm run typecheck --prefix src\NoiraPlayer.Web
npm run build --prefix src\NoiraPlayer.Web
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-WebUiPrivateData.ps1
git diff main...HEAD --check
git status --short --branch
```

Expected: all checks pass and the worktree is clean.

**Step 3: Request review and finish the branch**

Use `@superpowers:requesting-code-review`. Address validated findings with TDD, rerun final verification, then use `@superpowers:finishing-a-development-branch` to decide integration.
