# Emby TV Client Keyboard Checklist

Date: 2026-07-06

This checklist is the required local verification path after every meaningful UI or interaction development batch. Use keyboard input only for app interaction, because these keys map to the future controller path.

Mouse is allowed only for operating external tooling, window management, or closing system prompts that cannot be reached by app input. Do not use mouse clicks inside the app content to pass a checklist item.

## Run Log

### 2026-07-06 - Library Portal App Icon

App version: `0.1.0.72`

Scope:

- Replaced the UWP icon set with the `Library Portal` direction from the complete-client design: dark TV tile, layered media cards, cyan play portal, and amber resume arc.
- Added `tools/Generate-AppIconAssets.ps1` so the required app assets can be regenerated consistently.
- Updated `StoreLogo.png`, `Square44x44Logo.png`, `Square150x150Logo.png`, `Wide310x150Logo.png`, and `SplashScreen.png`.

Asset validation:

- Pixel validation passed for all five assets: expected dimensions, dark base, cyan portal pixels, and amber progress pixels.
- Visual inspection passed for the 44 px icon, wide tile, and splash screen. The 44 px icon remains legible; the wide tile reads as a media-library mark rather than a banner ad.

Keyboard-only validation with Computer Use:

- Installed and launched `0.1.0.72`.
- Home restored the saved session and exposed real library sections including `热门电影`, `热门剧集`, `豆瓣高分`, `Netflix`, `国产剧`, and `国漫`.
- `Down` then `Enter` from Home opened `热门电影` with `34 items`, Sort, Filter, and a real movie grid.
- `Escape` returned Home without `Nothing queued yet` or `Loading...`.
- Pressing `Enter` immediately after returning Home reopened `热门电影`, proving focus returned to the originating Home library card.

### 2026-07-06 - Artwork Policy And Home Library Cards

App version: `0.1.0.69`

Scope:

- Emby API requests now ask for `Primary`, `Backdrop`, `Thumb`, `Banner`, and `Logo` images with `EnableImages=true`.
- Library cards prefer wide artwork (`Thumb`, then `Backdrop`, then `Banner`, then `Primary`) so server-provided collection/folder covers are used before falling back to item posters.
- Home hero and Details artwork now use the same central artwork policy.

Keyboard-only validation with Computer Use:

- Home restored the saved Emby session and showed real media libraries including `热门电影`, `热门剧集`, `豆瓣高分`, `Netflix`, `国产剧`, and `国漫`.
- `Down` from the Home hero reached the first visible media library card, with a clear focus frame.
- `Enter` on `热门电影` opened the matching library and focused the first poster.
- `Enter` on the first poster opened Details; initial focus landed on `Play`.
- `Escape` from Details returned to the same library card.
- `Escape` from Library returned Home. Home briefly showed a loading/empty state, then recovered real Emby rows after the async reload completed.

Follow-up:

- The transient Home empty/loading state during back navigation should be improved by retaining the previous Home model while refresh is in flight.

### 2026-07-06 - Cached Home Return

App version: `0.1.0.70`

Scope:

- Home now uses page caching plus a small load policy so returning from Library/Details preserves the last rendered Home content.
- Manual Home refresh keeps the existing rails visible while new data loads.
- Refresh failure with existing content shows a non-destructive status instead of replacing the screen with an empty state.

Keyboard-only validation with Computer Use:

- Relaunched installed `0.1.0.70` and waited for real Emby data and artwork.
- `Down` from Home hero, `Enter` on `热门电影`, `Enter` on the first poster, `Escape` back to Library, and `Escape` back to Home all used keyboard events only.
- Immediate Home snapshot after the second `Escape` retained the hero, media libraries, and Continue Watching rows.
- Immediate Home snapshot did not contain `Nothing queued yet` or `Loading...`.

## Input Map

- D-pad up/down/left/right: Arrow keys.
- A/Select: Enter or Space.
- B/Back: Escape or BrowserBack.
- Menu: M.
- Shoulder/tab jump where implemented: PageUp/PageDown.

## Global Principles

Each route passes only if:

- Current focus is visible from a couch distance.
- Focus never lands on a hidden or offscreen element.
- The next likely action is within 1 to 3 D-pad moves.
- B returns one level or closes one layer, never two.
- A never triggers destructive or surprising playback changes.
- A loading, empty, or failure state leaves a visible recovery target.
- Text does not overlap or clip at 1080p desktop validation size.
- Primary UI stays inside the TV safe area.
- No app-content mouse click is used.

## Session And Startup

- Launch installed app.
- If logged out, move through server, username, and password fields with Tab or arrows where supported.
- Submit login with Enter.
- Verify successful login lands on Home.
- Verify failed login keeps focus on a visible retry/edit target.
- Relaunch app.
- Verify saved session returns to Home without a mouse click.
- Press Menu from Home.
- Verify guide opens and focus is visible.
- Press B.
- Verify guide closes and focus returns to the prior page anchor.

## Home

- Verify Home loads a hero with Play/Resume or a visible fallback.
- Press Down from hero.
- Verify focus enters first visible rail.
- Press Up.
- Verify focus returns to hero action.
- Press Right across a rail.
- Verify row scrolls without losing focus.
- Press Left back to the first item.
- Verify focus remains visible.
- Press Down across rails: Continue Watching, Next Up, Media Libraries, server home sections, Latest rows, Live TV or Collections if present.
- Press A on a Continue Watching item.
- Verify it opens Details or Playback according to the intended route, without accidental unrelated navigation.
- Press B until Home.
- Press A on a Media Library card.
- Verify it opens the matching Library.
- Press B.
- Verify focus returns to the same Home library card.
- Press A on a More action.
- Verify it opens the matching section query.
- Press B.
- Verify focus returns to More or the originating rail.

## Guide Rail

- Press Menu on Home.
- Move Up/Down through Home, Search, Movies, Shows, Live TV, Collections, Music, Photos, Settings.
- Verify each item has label and focus.
- Press A on Search.
- Verify Search opens.
- Press B.
- Verify previous page returns.
- Press Menu again.
- Press A on Movies.
- Verify Movies Library opens.
- Press Menu from Library.
- Verify guide opens over Library and does not reset the page.

## Library Browsing

- Open Movies.
- Verify first content card receives focus.
- Press Right at least five times.
- Verify focus advances through posters and row scroll is stable.
- Press Down at least two rows.
- Verify vertical movement preserves column intent.
- Press Up to first row, then Up again.
- Verify focus enters sort/filter controls.
- Press Down.
- Verify focus returns to content.
- Open sort sheet with A.
- Change sort with arrows.
- Confirm with A.
- Verify list reloads and focus returns to a visible content card.
- Open filter sheet.
- Apply watched/unwatched or favorite filter when available.
- Verify empty state, if any, has Clear filters or Retry focused.
- Open a collection/folder style library.
- Verify cards use wide artwork when appropriate.
- Press A on a grid item.
- Verify Details opens.
- Press B.
- Verify focus returns to the same or nearest stable card.

## Details

- Open a movie Details page.
- Verify Play/Resume has initial focus.
- Press Down.
- Verify focus enters versions/audio/subtitle/actions or first secondary row.
- Press Up.
- Verify focus returns to Play/Resume.
- Open More/actions if present.
- Press B.
- Verify it closes only the action layer.
- Mark favorite or watched if available.
- Verify state update is visible and focus remains stable.
- Open a series Details page.
- Move into seasons/episodes.
- Press A on an episode.
- Verify episode details or playback route is deliberate.
- Press B.
- Verify one-level return.
- Select an alternate version if present.
- Verify selected version changes visible metadata without accidental playback.

## Search

- Open Search from guide.
- Type a query using keyboard.
- Press Enter.
- Verify results load.
- Press Down into results.
- Move across result cards.
- Press A on a result.
- Verify Details opens.
- Press B.
- Verify Search results return.
- Clear query or search nonsense text.
- Verify empty state has visible guidance and a focus target.
- Try result filters if available.
- Verify focus remains predictable after filter changes.

## Playback

- Start playback from Home resume.
- Verify shell chrome disappears.
- Verify OSD appears during opening or first second.
- Press Escape once.
- If OSD is visible, verify it hides rather than exiting immediately.
- Press Enter or Space.
- Verify OSD appears.
- Press Right.
- Verify seek preview or seek step is visible and controlled.
- Press B.
- Verify seek preview cancels before OSD closes.
- Press M.
- Verify More drawer opens.
- Wait at least 6 seconds.
- Verify More drawer and OSD do not auto-hide.
- Press Down/Up inside More.
- Verify source/audio/subtitle/info controls are reachable.
- Press B.
- Verify More closes and focus returns to More button or visible OSD control.
- Press B again.
- Verify OSD hides.
- Press B again.
- Verify playback exits to Details or previous page.
- Re-enter playback.
- Press Stop if present.
- Verify stopped state is visible, and B returns one level.

## Playback Tracks And Versions

- On an item with multiple versions, open version/source selector.
- Move through versions with arrows.
- Confirm one version.
- Verify selection is visible.
- Start playback.
- Verify selected source is passed to playback.
- On an item with multiple audio tracks, open audio selector.
- Change audio track.
- Verify playback continues or reports a visible error without dead end.
- On an item with subtitles, open subtitle selector.
- Select subtitle and Off.
- Verify state is visible and focus remains stable.

## Live TV

- Open Live TV from guide when server supports it.
- Verify channels or guide appears.
- Move through channel list.
- Open a program.
- Start live playback if direct playback is supported.
- Press B to return from program/details/playback.
- Open recordings if present.
- Verify timers/recordings surfaces do not dead-end when unsupported.

## Collections And Playlists

- Open Collections.
- Move through collections.
- Open a collection.
- Verify collection item list appears.
- Play an item or open Details.
- Press B back to collection.
- Open Playlists.
- Verify playlist rows and item order are clear.
- Empty playlists show a recovery target.

## Music And Photos

- Open Music if present.
- Browse artists, albums, and songs.
- Start an item if supported by current playback route.
- Verify unsupported playback has a visible explanation and B recovery.
- Open Photos if present.
- Browse albums/folders.
- Open a photo.
- Verify B returns one level.

## Settings

- Open Settings.
- Move through all focusable controls with arrows.
- Toggle TV mode scale or diagnostics where present.
- Verify toggles announce or visibly reflect state.
- Press B.
- Verify previous page returns.

## Visual Review

Capture screenshots after keyboard navigation for:

- Home hero plus at least two rails.
- Expanded guide rail.
- Movies library grid.
- Details first viewport.
- Search results.
- Playback OSD.
- Playback More drawer.
- Settings.

For each screenshot, check:

- No clipped primary text.
- No incoherent overlap.
- Focus frame visible.
- Page proportions are not oversized.
- Artwork is used in preference to flat placeholders when server provides it.

## Completion Rule

If any checklist item fails, record:

- App version.
- Page and route.
- Exact key sequence.
- Expected behavior.
- Actual behavior.
- Screenshot or note.
- Proposed design or implementation fix.

Then continue design and implementation until the route passes.

## Run Log

### 2026-07-06 - Xbox Guide Rail Keyboard Fix

- App version: 0.1.0.77.
- Scope: expanded left Guide rail, deterministic keyboard/controller navigation, Home/Search/Movies routing.
- Regression found on 0.1.0.76: `M`, `Down`, `Enter` from Home opened Movies instead of Search because handled Button key events bypassed the Page-level guide decision.
- Fix: Guide open state now owns a selected destination; handled Up/Down/Enter events are routed through `GuideNavigationPolicy`, then focused explicitly.
- Keyboard-only validation:
  - Launch app, Home visible with collapsed Guide rail and real library data loading.
  - `M`, `Down`, `Enter` opened Search and collapsed Guide.
  - `Escape` returned from Search to Home.
  - `M`, `Escape` closed Guide and stayed on Home.
  - `M`, `Down`, `Down`, `Enter` opened Movies and collapsed Guide.
- Result: pass on local Windows validation. No mouse clicks were used inside app content.
