# Xbox Fluent Emby Complete Client Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Turn the current Xbox Fluent Emby prototype into a complete, beautiful, keyboard/controller-first Emby client with a redesigned app icon and repeatable local interaction verification.

**Architecture:** Keep `NextGenEmby.Core` as the testable data, input, and playback policy layer. Keep UWP XAML/code-behind as the TV view layer. Add small, testable model and policy types before changing UI. Do not change native decoding or direct playback logic.

**Tech Stack:** UWP XAML, WinUI 2.8.7, C#, .NET Standard 2.0 Core library, .NET 9 xUnit tests, MSBuild, MSIX local install, Computer Use or Windows automation with keyboard input.

---

## Current Baseline

The worktree already contains a compact Fluent TV shell and dynamic Emby home expansion. Current dirty work includes Home, Library, Details, Playback, Search, Settings, Core Emby API, input policy, playback overlay policy, and tests. Treat these as current state and do not revert them.

Relevant documents:

- `docs/plans/2026-07-06-xbox-fluent-emby-complete-client-design.md`
- `docs/qa/emby-tv-client-keyboard-checklist.md`
- `docs/superpowers/specs/2026-07-06-tv-fluent-interaction-polish-design.md`
- `docs/superpowers/plans/2026-07-06-tv-fluent-interaction-polish.md`

## Phase 1: Artwork And Information Architecture

### Task 1: Artwork candidate model

**Files:**

- Create: `src/NextGenEmby.Core/Emby/EmbyImageCandidate.cs`
- Modify: `src/NextGenEmby.Core/NextGenEmby.Core.csproj`
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyArtworkPolicyTests.cs`

**Step 1: Write failing tests**

Add tests proving that hero artwork chooses `Backdrop`, then `Thumb`, then `Banner`, then `Primary`; library wide artwork chooses `Thumb`, then `Backdrop`, then `Banner`, then `Primary`; poster artwork chooses `Primary` first.

**Step 2: Run RED**

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "EmbyArtworkPolicy" -v minimal
```

Expected: fails because the policy does not exist.

**Step 3: Implement minimal policy**

Create small immutable candidate objects and a static policy in Core. Do not create image URLs in the policy; return item id, image type, and intended max width.

**Step 4: Run GREEN**

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "EmbyArtworkPolicy" -v minimal
```

Expected: tests pass.

### Task 2: Parse Banner and Logo image tags

**Files:**

- Modify: `src/NextGenEmby.Core/Emby/EmbyMediaItem.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyLibraryView.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyLibraryTests.cs`

**Step 1: Write failing tests**

Add tests for mapping `ImageTags.Banner`, `ImageTags.Logo`, inherited parent ids, and explicit image query flags when relevant.

**Step 2: Run RED**

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "Banner|Logo|Image" -v minimal
```

Expected: fails for missing Banner/Logo mapping.

**Step 3: Implement mapping**

Add fields to media/library models. Update mapping only; do not change playback.

**Step 4: Run GREEN**

Run the same filtered tests.

### Task 3: Apply artwork policy on Home and Details

**Files:**

- Modify: `src/NextGenEmby.App/Views/HomePage.xaml.cs`
- Modify: `src/NextGenEmby.App/Views/MediaDetailsPage.xaml.cs`

**Step 1: Build after Core policy**

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /p:UseSharedCompilation=false
```

Expected: succeeds before UI changes.

**Step 2: Use candidate policy**

Replace scattered image priority logic with the Core policy. Keep existing visual fallbacks.

**Step 3: Build**

Run the same MSBuild command.

## Phase 2: Xbox Guide Shell

### Task 4: Guide navigation model

**Files:**

- Create: `src/NextGenEmby.Core/Input/GuideNavigationPolicy.cs`
- Test: `tests/NextGenEmby.Core.Tests/Input/GuideNavigationPolicyTests.cs`

**Step 1: Write failing tests**

Cover Menu opening the guide, B closing it, A selecting a destination, and B returning focus to the previous page anchor.

**Step 2: Run RED**

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "GuideNavigationPolicy" -v minimal
```

Expected: fails because policy does not exist.

**Step 3: Implement policy**

Add pure policy decisions only.

**Step 4: Run GREEN**

Run the same filtered tests.

### Task 5: Replace top shell with left guide rail

**Files:**

- Modify: `src/NextGenEmby.App/MainPage.xaml`
- Modify: `src/NextGenEmby.App/MainPage.xaml.cs`
- Modify: `src/NextGenEmby.App/App.xaml`

**Step 1: Preserve current navigation**

Before editing, confirm Home, Movies, TV, Search, Settings navigation still has named controls in code-behind.

**Step 2: Implement collapsed guide rail**

Create a 72 px collapsed rail with icons/text fallback labels and a 240 px expanded state. Use focus visuals from the design doc.

**Step 3: Wire Menu and B**

Use `GuideNavigationPolicy` to open/close and restore focus.

**Step 4: Build**

Run app MSBuild.

## Phase 3: Complete Emby Surfaces

### Task 6: Library type coverage

**Files:**

- Modify: `src/NextGenEmby.Core/Emby/EmbyItemsQuery.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Modify: `src/NextGenEmby.App/Navigation/LibraryNavigationRequest.cs`
- Modify: `src/NextGenEmby.App/Views/LibraryPage.xaml.cs`
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyLibraryTests.cs`

**Step 1: Write failing tests**

Cover collection, playlist, music, photo, genre, person, favorite, watched/unwatched, and folder query construction.

**Step 2: Run RED**

Run filtered Emby library tests.

**Step 3: Implement query support**

Extend query objects without changing current playback.

**Step 4: Run GREEN**

Run filtered tests, then all Core tests.

### Task 7: Live TV browsing shell

**Files:**

- Create: `src/NextGenEmby.Core/Emby/EmbyLiveTvChannel.cs`
- Create: `src/NextGenEmby.Core/Emby/EmbyLiveTvProgram.cs`
- Modify: `src/NextGenEmby.Core/Emby/EmbyApiClient.cs`
- Create or modify app view as needed.
- Test: `tests/NextGenEmby.Core.Tests/Emby/EmbyLiveTvTests.cs`

**Step 1: Write failing tests**

Cover channels, guide/program parsing, and unsupported/empty responses.

**Step 2: Run RED**

Run Live TV filtered tests.

**Step 3: Implement Core client methods**

Add browse-only Live TV methods. Do not introduce transcoding.

**Step 4: Run GREEN**

Run filtered tests.

**Step 5: Add UI entry**

Add Live TV rail destination. If playback is unsupported for a stream, show a visible unsupported state with B recovery.

## Phase 4: App Icon

### Task 8: Generate and install new icon assets

**Files:**

- Modify binary assets under `src/NextGenEmby.App/Assets/`
- Verify: `src/NextGenEmby.App/Package.appxmanifest`

**Step 1: Generate icon concept**

Use the Matte Cinema Fluent icon direction from `docs/DESIGN.md`: a dark matte tile, layered media slats or a quiet screen aperture, one crisp focus/play affordance, and no cyan glow or portal motif.

**Step 2: Produce required sizes**

Create `StoreLogo.png`, `Square44x44Logo.png`, `Square150x150Logo.png`, `Wide310x150Logo.png`, and `SplashScreen.png`.

**Step 3: Inspect assets**

Use local image inspection to verify 44 px legibility and wide tile composition.

**Step 4: Build**

Run app MSBuild.

## Phase 5: Full Checklist Verification

### Task 9: Core test gate

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore -v minimal
```

Expected: all tests pass.

### Task 10: Build and package

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /p:UseSharedCompilation=false
```

Expected: build succeeds and package output is available.

### Task 11: Install and launch

Use the existing local signing/install path recorded in `docs/foundation-status.md`, then launch:

```powershell
Start-Process 'shell:AppsFolder\NextGenEmby.App_h8qjz0sr1sg4m!App'
```

Expected: app launches locally.

### Task 12: Keyboard-only checklist

Execute `docs/qa/emby-tv-client-keyboard-checklist.md` with keyboard input. Record failures as implementation work, not as accepted limitations, unless the limitation is outside this project and clearly documented.

### Task 13: Iterate

For every checklist failure:

1. Write a failing test if the behavior can be represented in Core.
2. Verify RED.
3. Implement the smallest fix.
4. Verify GREEN.
5. Build the app.
6. Re-run the affected keyboard route.
7. Update documentation with evidence.

## Completion Criteria

- Design doc exists and matches the approved Xbox Fluent direction.
- Checklist exists and covers complete Emby user routes.
- App icon assets are redesigned and installed in the package.
- Core tests pass.
- UWP Debug x64 build passes.
- Local installed app completes the checklist using keyboard input for app interaction.
- Any remaining issue has a concrete follow-up and does not violate the core principles in the checklist.
- Native decoding/playback core files are not modified for UI work.
