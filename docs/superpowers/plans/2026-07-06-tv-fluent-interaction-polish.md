# TV Fluent Interaction Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing Xbox Emby Fluent TV prototype feel compact, keyboard/gamepad reliable, visually polished, and locally validated on Windows without changing decoder playback logic.

**Architecture:** Keep `NextGenEmby.Core` as the testable policy layer and UWP XAML/code-behind as the view layer. UI scale changes live in shared XAML resources and page XAML; behavior changes that can be unit-tested go into existing Core playback/input policies.

**Tech Stack:** UWP XAML, WinUI 2.8.7, C#, .NET Standard 2.0 Core library, .NET 9 xUnit tests, Visual Studio/MSBuild, Windows Computer Use verification.

---

## File Structure

- Modify `src/NextGenEmby.App/App.xaml`: compact TV resources for sizing, focus, and reusable button/card styles.
- Modify `src/NextGenEmby.App/MainPage.xaml`: compact shell margins, nav sizing, and focus state.
- Modify `src/NextGenEmby.App/Views/HomePage.xaml`: smaller hero, library tiles, row cards.
- Modify `src/NextGenEmby.App/Views/HomePage.xaml.cs`: apply compact card sizes to dynamically-created media cards.
- Modify `src/NextGenEmby.App/Views/LibraryPage.xaml`: smaller toolbar and poster grid.
- Modify `src/NextGenEmby.App/Views/LibraryPage.xaml.cs`: restore focus to a visible item/fallback after reload.
- Modify `src/NextGenEmby.App/Views/MediaDetailsPage.xaml`: smaller poster/title/body and tighter first viewport.
- Modify `src/NextGenEmby.App/Views/MediaDetailsPage.xaml.cs`: restore focus to Play/Refresh after load and keep back behavior one-level.
- Modify `src/NextGenEmby.App/Views/PlaybackPage.xaml`: denser OSD and narrower More drawer.
- Modify `src/NextGenEmby.App/Views/PlaybackPage.xaml.cs`: focus restoration when closing More and overlay pinning while seek preview is active.
- Modify `src/NextGenEmby.Core/Playback/PlaybackOverlayInputPolicy.cs`: add explicit policy for whether the overlay auto-hide timer may hide.
- Modify `tests/NextGenEmby.Core.Tests/Playback/PlaybackOverlayInputPolicyTests.cs`: regression tests for overlay pinning and More-close behavior.
- Modify `docs/foundation-status.md`: append local verification notes after implementation.

## Task 1: Policy Tests

- [ ] **Step 1: Write failing tests**

Add tests to `tests/NextGenEmby.Core.Tests/Playback/PlaybackOverlayInputPolicyTests.cs`:

```csharp
[Theory]
[InlineData(true, false, false, true)]
[InlineData(false, true, false, true)]
[InlineData(false, false, true, true)]
[InlineData(false, false, false, false)]
public void Overlay_Should_Pin_When_More_Seek_Or_Manual_Debug_Is_Active(
    bool moreVisible,
    bool seekPreviewActive,
    bool manualDebugVisible,
    bool expected)
{
    var shouldPin = PlaybackOverlayInputPolicy.ShouldKeepOverlayPinned(
        moreVisible,
        seekPreviewActive,
        manualDebugVisible);

    Assert.Equal(expected, shouldPin);
}
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "Overlay_Should_Pin" -v minimal
```

Expected: build fails because `ShouldKeepOverlayPinned` does not exist.

- [ ] **Step 3: Implement policy**

Add this method to `PlaybackOverlayInputPolicy`:

```csharp
public static bool ShouldKeepOverlayPinned(
    bool moreVisible,
    bool seekPreviewActive,
    bool manualDebugVisible)
{
    return moreVisible || seekPreviewActive || manualDebugVisible;
}
```

- [ ] **Step 4: Verify GREEN**

Run:

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "Overlay_Should_Pin" -v minimal
```

Expected: the new tests pass.

## Task 2: Shared Compact TV Scale

- [ ] **Step 1: Add compact resources**

Update `App.xaml` with compact TV sizing resources and apply them to default `Button`, `TextBox`, and `PasswordBox` styles. Keep controls at 52 effective pixels high by default, preserve 8px or smaller corners, and keep icon buttons 52x52.

- [ ] **Step 2: Apply shell and page scale**

Update `MainPage.xaml`, `HomePage.xaml`, `LibraryPage.xaml`, `MediaDetailsPage.xaml`, and `PlaybackPage.xaml` to use the compact scale from the design doc.

- [ ] **Step 3: Update dynamic card sizes**

Update `HomePage.xaml.cs` dynamic media card creation to use 168x246 cards, smaller overlay padding, and 16/13 font sizes.

- [ ] **Step 4: Build**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /p:UseSharedCompilation=false
```

Expected: build succeeds.

## Task 3: Page Layout and Focus Pass

- [ ] **Step 1: Compact Home**

Reduce Home margins, hero, row card, and library tile sizes. Set load success focus to the hero play button when playable; otherwise focus Movies.

- [ ] **Step 2: Compact Library**

Reduce grid card and toolbar sizes. Preserve first-item focus after reload, but route empty/error focus to Refresh.

- [ ] **Step 3: Compact Details**

Reduce poster/title/body sizes. Keep Play initial focus after item render. Keep episode/version buttons full-width and stable.

- [ ] **Step 4: Compact Search and Settings if needed**

Scan Search/Settings for the same oversized default controls. Reduce obvious page-title/margin issues while preserving the current data behavior.

- [ ] **Step 5: Build**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /p:UseSharedCompilation=false
```

Expected: build succeeds.

## Task 4: Playback Overlay Focus

- [ ] **Step 1: Use the new pinning policy**

Update `PlaybackPage.xaml.cs` so `ShouldKeepOverlayPinned()` delegates to `PlaybackOverlayInputPolicy.ShouldKeepOverlayPinned(_moreVisible, _seekPreview.IsActive, ManualDebugPanel.Visibility == Visibility.Visible)`.

- [ ] **Step 2: Restore focus when closing More**

In the `CloseMore` action, hide the drawer and programmatically focus `MoreButton`, so B/Escape from More leaves focus on a visible control.

- [ ] **Step 3: Build**

Run the same MSBuild command from Task 2.

Expected: build succeeds.

## Task 5: Verification

- [ ] **Step 1: Run all Core tests**

```powershell
dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --no-restore -v minimal
```

Expected: all tests pass.

- [ ] **Step 2: Build/package app**

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' src\NextGenEmby.App\NextGenEmby.App.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /p:UseSharedCompilation=false
```

Expected: build succeeds and a new Debug x64 MSIX test package is produced.

- [ ] **Step 3: Install and launch locally**

Use the existing local signing/install path recorded in `docs/foundation-status.md`, then launch `shell:AppsFolder\NextGenEmby.App_h8qjz0sr1sg4m!App`.

- [ ] **Step 4: Keyboard-only smoke**

Using Computer Use key input, verify Home, Movies, Details, Playback OSD, More drawer, and B/Escape close/return behavior. Avoid mouse clicks for app interaction; if Computer Use requires a non-content focus workaround, document it.

- [ ] **Step 5: Visual scale inspection**

Capture local screenshots with Computer Use after keyboard navigation. Check that shell, Home, Library, Details, Playback OSD, and More drawer do not overlap and do not read as oversized at desktop window scale.

- [ ] **Step 6: Record status**

Append verification evidence and any remaining limits to `docs/foundation-status.md`.

## Self-Review Notes

- The design doc covers current TV system guidance, Fluent constraints, layout scale, focus, playback OSD, and validation.
- The plan avoids core decoder/native playback changes.
- The plan adds a failing policy test before production behavior changes.
- The plan explicitly requires keyboard-equivalent validation rather than mouse interaction.

## Task 6: Dynamic Emby Home Expansion

- [x] **Step 1: Add Emby home API coverage**

Add Core API methods and RED/GREEN tests for:

- `/Users/{UserId}/HomeSections`
- `/Users/{UserId}/Sections/{SectionId}/Items`
- `/Shows/NextUp`
- `/Users/{UserId}/Items/Latest` with `ParentId`

- [x] **Step 2: Replace fixed Home libraries with server libraries**

Render Media Libraries from `/Views` so custom libraries such as Douban, Netflix, anime, action collections, and other user-defined libraries appear without hardcoding.

- [x] **Step 3: Render mature-client style home rails**

Home now composes Continue watching, Next up, server-configured sections, popular fallback rows, Latest in each library, and Latest. Duplicate server rows for Continue/Next up are skipped to avoid repeated rows.

- [x] **Step 4: Make More actions open the right Library view**

More buttons use `SectionId` for server home sections and `ParentId` for media libraries, so row expansion opens the same content taxonomy the user saw on Home.

- [x] **Step 5: Fix Home D-pad row entry regression with TDD**

Added `HomeFocusInputPolicy` tests before implementation. The fixed route is Hero -> Media Libraries -> first content row; Up from the first content row returns to Media Libraries.

- [x] **Step 6: Keyboard-only local verification**

Installed `0.1.0.67` locally and verified with computer-use key events only: Home loads dynamic libraries, Down moves from Hero to the first library, Down moves from the library rail to Continue watching, and Up returns to the library rail. Earlier `0.1.0.66` verification covered Home -> dynamic library -> Library grid -> Details and Library Sort/Filter focus.

- [x] **Step 7: Prefer dedicated library artwork**

Added RED/GREEN coverage for `/Views` image fields and mapped `ImageTags.Thumb`, `ImageTags.Primary`, `BackdropImageTags`, `PrimaryImageItemId`, `ParentBackdropItemId`, and `ParentThumbItemId`. Home library cards now prefer dedicated landscape library artwork before falling back to latest child media artwork. Installed `0.1.0.68` and visually verified the Media Libraries rail shows dedicated Terminus-style collection covers.
