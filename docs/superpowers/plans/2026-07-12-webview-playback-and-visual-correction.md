# WebView Playback And Visual Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make native playback launch reliably from WebView details and correct the confirmed Home/Details visual regressions without changing Guide information architecture.

**Architecture:** Playback is diagnosed at the Web message, frame navigation, page construction, XAML initialization, and native-engine boundaries before applying one evidence-backed fix. Visual changes stay in shared Web tokens and existing page/components, preserving the global focus policy and current routes.

**Tech Stack:** React 19, TypeScript, Vite, Norigin Spatial Navigation, UWP, WebView2, C#, xUnit, Vitest.

## Global Constraints

- Do not change Guide destinations, Guide data flow, or route taxonomy in this pass.
- `docs/DESIGN.md`, A3 convergence rules, and retained A3 previews are the visual source of truth.
- Catalog UI remains WebView2; playback remains native UWP.
- Real-data QA uses the saved native session; no runtime media fixture mode is added.
- Private credentials, catalog values, media IDs, artwork, and screenshots are not committed or printed.
- Existing directional focus, exact restore, safe area, and Back behavior must not regress.

---

### Task 1: Diagnose And Repair Native Playback Activation

**Files:**
- Modify: `src/NoiraPlayer.App/MainPage.xaml.cs`
- Modify: `src/NoiraPlayer.App/Views/PlaybackPage.xaml.cs`
- Test: `tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs`

- [x] Add a failing source contract requiring stage-only diagnostics with hexadecimal HRESULT.
- [x] Run the filtered Core contract tests and verify RED.
- [x] Instrument frame dispatch, page construction, XAML initialization, and native-engine creation without logging item/session data.
- [x] Build, register, reproduce once with the saved session, and inspect only sanitized activation lines.
- [x] Identify the same-Package-Family worktree registration collision as the source of the reported stale playback build.
- [x] Re-run filtered tests, package, launch, and verify real Details -> Playback -> Back.
- [x] Retain a bounded safe failure record plus MVID build marker and remove temporary diagnostic volume.
- [x] Commit the playback diagnostics independently.

### Task 2: Converge Confirmed Home And Details Visual Problems

**Files:**
- Modify: `src/NoiraPlayer.Web/src/styles.css`
- Modify: `src/NoiraPlayer.Web/src/pages/HomePage.tsx`
- Modify: `src/NoiraPlayer.Web/src/pages/HomePage.test.tsx`
- Modify: `src/NoiraPlayer.Web/src/pages/DetailsPage.test.tsx`
- Modify: `tests/NoiraPlayer.Core.Tests/Design/ModernUwpSolutionContractTests.cs`

- [x] Add RED assertions for compact Home chrome, hidden browser scrollbar, non-selectable controls, stable first viewport, and distinct route/focus material.
- [x] Reduce Home page-title weight and top chrome so a complete high-value row plus the next-row hint fit the first TV viewport.
- [x] Tighten row spacing while retaining stable card geometry and 56px safe area.
- [x] Hide browser scrollbars without disabling focus-driven scrolling.
- [x] Prevent accidental text selection on controller/button interaction.
- [x] Strengthen integrated matte card and action focus without bright frames or reflow.
- [x] Run focused Web tests, production build, and Core design contracts.
- [x] Commit the visual correction independently.

### Task 3: Real-Data Package Verification

**Files:**
- Modify: `docs/qa/webview-tv-browse-ui.md`

- [x] Run the private-data guard before package QA.
- [x] Build, register, and launch Debug x64 from this worktree.
- [x] Verify Home/Details in the current local window without recording private catalog values.
- [x] Verify one real Details -> native Playback -> Back traversal.
- [x] Run Web tests, production build, Core tests, UWP build, `git diff --check`, and the private-data guard.
- [x] Record only anonymous package identity, commands, viewport, pass/fail state, and Xbox residual risk.
