# A3 Visual Convergence Pass 1 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Move Home, Library, Details, and Playback toward A3 screenshot-level visual convergence without treating the AI-generated A3 renders as pixel-perfect mocks.

**Architecture:** `docs/DESIGN.md` remains the token and visual-system source of truth. `docs/design-previews/README.md` plus `docs/a3-visual-convergence-rules.md` define the A3 visual road map, and `docs/qa/a3-visual-convergence-checklist.md` defines screenshot-first acceptance. Implementation should begin with global chrome/canvas/card language, then proceed page by page so secondary visual drift does not harden into local exceptions.

**Tech Stack:** UWP XAML, C# code-behind, xUnit source-level design contract tests, deterministic DEBUG fixture routes, local screenshots stored outside the repository.

**Visual Evidence Rule:** Deterministic fixture artwork is useful for structure, spacing, focus, and regression checks, but it is not enough to accept final atmosphere. Before marking any primary page visually converged, review at least one local-only saved-session sample with real Emby artwork. Do not commit real screenshots, downloaded artwork, server URLs, usernames, passwords, or tokens.

---

### Task 1: Passive Chrome And Card Borders

**Files:**
- Modify: `src/NextGenEmby.App/App.xaml`
- Modify: `src/NextGenEmby.App/Views/HomePage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/HomeAccessibilitySourceTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/LibraryPageSourceTests.cs`

**Step 1: Write failing source contract tests**

Add tests proving that passive Home media cards and Library command buttons do not use `AppHairlineBrush` as their default visual structure. The tests should allow matte focus fill and source-sheet borders, but reject passive media-card hairlines in `CreateLibraryButton`, `CreateHomeSectionButton`, `CreateResumeItemButton`, and `CreateItemButton`.

**Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test tests/NextGenEmby.Core.Tests/NextGenEmby.Core.Tests.csproj --filter "FullyQualifiedName~HomeAccessibilitySourceTests|FullyQualifiedName~LibraryPageSourceTests"
```

Expected: FAIL until passive card and command-button borders are updated.

**Step 3: Implement passive border reduction**

Change passive Home card structural borders from `AppHairlineBrush` to `AppTransparentBrush`. Change `TvCommandButtonStyle`, `IconButtonStyle`, and Library command focus reset so nonfocused toolbar commands no longer read as bordered desktop controls. Preserve focused matte fill and existing automation names.

**Step 4: Run focused tests**

Run the same filtered `dotnet test` command.

Expected: PASS.

### Task 2: Shell And Page Header Weight

**Files:**
- Modify: `src/NextGenEmby.App/App.xaml`
- Modify: `src/NextGenEmby.App/MainPage.xaml`
- Modify: `src/NextGenEmby.App/Views/HomePage.xaml`
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml`
- Test: `tests/NextGenEmby.Core.Tests/Design/ShellGuideAccessibilitySourceTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/HomeAccessibilitySourceTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/LibraryPageSourceTests.cs`

**Step 1: Write source contracts for A3-01**

Assert that page titles and refresh controls are visually subordinate and that the left Guide remains graphite, quiet, and border-light.

**Step 2: Reduce title/refresh dominance**

Lower title scale where it dominates first read, make refresh icon treatment quiet and borderless, and avoid introducing top search or large desktop command fields.

**Step 3: Verify**

Run the focused design tests and capture Home/Library fixture screenshots if the app route tooling is available.

### Task 3: Home Media Wall

**Files:**
- Modify: `src/NextGenEmby.App/Views/HomePage.xaml`
- Modify: `src/NextGenEmby.App/Views/HomePage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/HomeAccessibilitySourceTests.cs`

**Step 1: Write source contracts for A3-02**

Assert that Home first viewport remains rail/list-led, Continue Watching is a list of wide cards, and the feature strip does not become a single form-like hero.

**Step 2: Rebalance Home composition**

Make media rows the first read, keep feature content compact and unframed, and preserve text fallback with black scrims rather than nested material boxes.

**Step 3: Verify**

Run Home design tests and capture Home fixture screenshots against `A3-ideal-home-dashboard.png`.

### Task 4: Library Poster Wall

**Files:**
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml`
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml.cs`
- Modify: `src/NextGenEmby.App/Views/PosterGridFocusVisuals.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/PosterGridVisualSourceTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/LibraryPageSourceTests.cs`

**Step 1: Write source contracts for A3-03**

Assert poster density, focused matte backplate, compact title/meta, quiet no-art fallback, and toolbar subordination.

**Step 2: Rebalance grid and toolbar**

Tune grid spacing, selected backplate strength, fallback-card opacity, and toolbar weight while avoiding bright outline focus.

**Step 3: Verify**

Run Library/poster-grid tests and compare fixture screenshot to `A3-ideal-library-poster-focus.png`.

### Task 5: Details Atmosphere

**Files:**
- Modify: `src/NextGenEmby.App/Views/MediaDetailsPage.xaml`
- Modify: `src/NextGenEmby.App/Views/MediaDetailsPage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/MediaDetailsAccessibilitySourceTests.cs`

**Step 1: Write source contracts for A3-04**

Assert one atmospheric image zone, no separate poster viewer, no fake no-art placeholder, and a low decision surface for Play/source/audio/subtitle actions.

**Step 2: Recompose Details**

Push right-side artwork into background atmosphere, reduce panel/form feel, and keep source/audio/subtitle controls deterministic and readable.

**Step 3: Verify**

Run Details design tests and compare fixture/no-art screenshots to `A3-ideal-details-atmosphere.png`. Also run the local-only `details-real-sample` route when a saved app session exists, because the fixture's dark abstract artwork is not sufficient to prove real Emby atmosphere, long overview behavior, or source-chip density.

### Task 6: Playback Native Material

**Files:**
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml`
- Modify: `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Design/PlaybackOptionsFixtureSourceTests.cs`
- Test: `tests/NextGenEmby.Core.Tests/Playback/*`

**Step 1: Write source contracts for A3-05**

Assert video/artwork-first layout, compact top capsule, padded bottom strip, subordinate More/options drawer, and no large centered glass card.

**Step 2: Tune playback OSD**

Use native-feeling matte/material overlays over video/artwork where feasible, keep subtitles clear, and preserve source/audio/subtitle access in More.

**Step 3: Verify**

Run playback tests and capture playback fixture or visual playback route screenshot against `A3-ideal-playback-osd-native-material.png`.

### Task 7: A3 Batch Evidence

**Files:**
- Modify: `docs/qa/a3-visual-convergence-checklist.md`
- Do not commit: private screenshots, real server URLs, credentials, downloaded artwork, or personal media assets.

**Step 1: Record batch evidence**

Update only checklist status/notes that can be proven by safe source contracts or non-private fixture screenshots.

**Step 2: Run safety checks**

Run:

```powershell
git diff --check
rg -n "<private-server-host>|<private-username>|<private-password>" docs src tests tools README.md .gitignore --hidden
```

Expected: no whitespace errors and no sensitive information matches.
