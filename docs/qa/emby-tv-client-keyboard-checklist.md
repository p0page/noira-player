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

### 2026-07-06 - Extended Library Query And Guide Entrypoints

- App version: 0.1.0.79.
- Scope: Emby item query coverage for collection/media/person/genre/favorite/played/folder filters, Guide entries for Playlists, Favorites, and Unwatched, and strict client-side item type guards for strong library views.
- Regression found on 0.1.0.78: `M`, six `Down`, `Enter` opened Playlists but the server returned root `CollectionFolder` views instead of `Playlist` items.
- Fix: strong library requests can now require returned item types to match `IncludeItemTypes`; incompatible Emby responses that ignore item type filters no longer masquerade as Playlists or other strict views.
- Keyboard-only validation:
  - Installed and launched 0.1.0.79 locally.
  - `M`, six `Down`, `Enter` opened Playlists; page now shows `No items found` instead of root library folders when the service returns no `Playlist` items.
  - From Playlists, `M`, three `Down`, `Enter` opened Favorites with 100 media items.
  - From Favorites, `M`, `Down`, `Enter` opened Unwatched with 100 media items.
- Result: pass on local Windows validation. No mouse clicks were used inside app content.

### 2026-07-06 - Matte Cinema Logo And Home Recovery Pass

- App version: 0.1.0.91.
- Scope: `docs/DESIGN.md` token alignment, Emby `Logo` artwork policy, Home/Details logo fallback, Home/Details backdrop wash validation, and Emby API request timeout policy.
- Design update:
  - `docs/DESIGN.md` now separates `focus #3BD5FF` from `primary #61D47C`.
  - Green is reserved for Play/Resume/confirm actions.
  - Cyan is reserved for controller focus and active-route edges, without glow, portal, or sci-fi background treatment.
- Automated verification:
  - Core tests passed: 197 total.
  - Package layout gate passed for 0.1.0.91: root app shim, `entrypoint\NextGenEmby.App.exe`, and XAML metadata provider layout all valid.
  - MSIX signed and installed locally as `NextGenEmby.App_0.1.0.91_x64__h8qjz0sr1sg4m`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.91 locally.
  - Home loaded real Emby data: Hero resume item, dedicated Media Libraries covers, Continue watching, Hot Movies, Hot TV Series, and latest rows.
  - From Hero Play, `Right`, `Enter` opened Details for `诡怪疑云`.
  - Details showed backdrop wash, poster artwork, metadata, Resume as initial action, version/audio/subtitle metadata, and overview.
  - The item did not expose a usable Logo image, so Details correctly fell back to the text title instead of leaving a blank title area.
  - `Escape` returned one level to Home; Home content remained present and focus returned to the Hero Details action.
- Result: pass for the Home -> Details -> Home route. No mouse clicks were used inside app content.
- Follow-up:
  - Home first-render latency needed a progressive load pass; addressed in the 0.1.0.93 run below.

### 2026-07-06 - Progressive Home Load

- App version: 0.1.0.94.
- Scope: Home first-render latency, supplemental row loading, focus preservation policy, and Home -> Library keyboard route after progressive load.
- Automated verification:
  - Core tests passed: 199 total.
  - App Debug x64 Build passed and produced `NextGenEmby.App_0.1.0.94_x64_Debug.msix`.
  - Package layout gate passed for 0.1.0.94.
  - MSIX signed and installed locally as `NextGenEmby.App_0.1.0.94_x64__h8qjz0sr1sg4m`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.94 locally.
  - At the 6-second text snapshot, Home had already rendered Hero, Media Libraries, Continue watching, Hot Movies, Hot TV Series, Latest-in-library rows, and Latest.
  - The same snapshot showed no `Loading...` or `Loading more rows...` state.
  - From Home hero, `Down`, `Enter` opened the first media library `热门电影`.
  - Library showed `34 items`, Sort, Filter, and a movie grid without loading dead end.
- Result: pass. No mouse clicks were used inside app content.

### 2026-07-06 - Library Sort And Filter Sheets

- App version: 0.1.0.98.
- Scope: Library sort/filter controls now open a matte TV selection sheet instead of cycling values directly from the toolbar button.
- Interaction changes:
  - A/Enter on Sort opens a `Sort by` sheet with Title, Recently added, and Year.
  - A/Enter on Filter opens a `Filter` sheet with All, Unwatched, and Resumable.
  - Up/Down/Left/Right move one option at a time inside the sheet.
  - A/Enter confirms and reloads only when the option changed.
  - B/Escape cancels and restores the originating toolbar focus.
  - Sort/Filter/Refresh have deterministic D-pad left/right movement.
  - Empty or failed Library loads show central recovery actions; filtered empty states can clear filters.
- Bugs found during keyboard-only validation:
  - A single Down in the Sort sheet initially moved from Title to Year because both Page and CoreWindow handlers processed the same key. Fixed by routing sheet keys only through Page-level input.
  - Right from Sort initially skipped Filter and reached Refresh for the same double-routing reason. Fixed by preventing CoreWindow fallback from handling horizontal keys.
- Automated verification:
  - Core tests passed: 206 total.
  - Focus policy tests passed: 7 targeted Library sheet/toolbar tests.
  - App Debug x64 clean build passed and produced `NextGenEmby.App_0.1.0.98_x64_Debug.msix`.
  - Package layout gate passed for 0.1.0.98.
  - MSIX signed and installed locally as `NextGenEmby.App_0.1.0.98_x64__h8qjz0sr1sg4m`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.98 locally.
  - From Home, `Down`, `Enter` opened `热门电影` Library with `34 items`.
  - From the first poster, `Up`, `Enter` opened the Sort sheet.
  - One `Down` moved the sheet preview to `Recently added`, not Year.
  - `Enter` confirmed sort, closed the sheet, reloaded the list, and kept Library content visible.
  - From content, `Up`, `Right`, `Enter` opened the Filter sheet.
  - `Escape` cancelled the Filter sheet and left Filter as `All`.
  - Reopening Filter, `Down`, `Enter` applied `Unwatched`; the Library stayed usable with visible grid items.
- Visual capture note:
  - Computer Use screenshot capture hit `FrameArrived timed out` twice during this run, so this batch is validated by accessibility text and keyboard behavior rather than a fresh screenshot. No mouse clicks were used inside app content.

### 2026-07-06 - Series Details Default Focus

- App version: 0.1.0.99.
- Scope: Details now implements the TV content focus contract and uses a central policy for the default focus target.
- Interaction changes:
  - Playable movie/episode Details still focus Play or Resume.
  - Non-playable Details with episode buttons focus the first episode.
  - Non-playable Details without episodes fall back to Refresh instead of leaving focus ambiguous.
- Automated verification:
  - Core tests passed: 209 total.
  - `MediaDetailsDefaultFocusPolicyTests` passed: playable -> Play, non-playable with episodes -> first episode, non-playable without episodes -> Refresh.
  - App Debug x64 clean build passed and produced `NextGenEmby.App_0.1.0.99_x64_Debug.msix`.
  - Package layout gate passed for 0.1.0.99.
  - MSIX signed and installed locally as `NextGenEmby.App_0.1.0.99_x64__h8qjz0sr1sg4m`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.99 locally.
  - `M`, three `Down`, `Enter` opened Shows / TV Shows.
  - `Enter` on the first visible Series opened Details.
  - The sampled Series showed disabled Play and `No episodes found`; pressing `Enter` stayed on Details and exercised the Refresh fallback rather than dead-ending.
  - Several adjacent Series entries were sampled but also did not expose episodes through the current `/Children` route in this validation run.
- Follow-up:
  - Re-run the positive first-episode focus path when a sampled Series exposes episode buttons through the current Details endpoint.

### 2026-07-06 - Standard Series Seasons And Episodes

- App version: 0.1.0.100.
- Scope: Series Details now prefer Emby's TV Shows endpoints for seasons and episodes, with the previous generic children query retained as a compatibility fallback.
- Data path:
  - `Shows/{Id}/Seasons` loads Series seasons.
  - `Shows/{Id}/Episodes` loads episodes for a selected season.
  - Empty or failed dedicated responses fall back to `Users/{UserId}/Items?ParentId=...`.
- Automated verification:
  - Core tests passed: 211 total.
  - New API tests passed for the Shows seasons and episodes endpoints.
  - App Debug x64 clean build passed and produced `NextGenEmby.App_0.1.0.100_x64_Debug.msix`.
  - Package layout gate passed for 0.1.0.100.
  - MSIX signed and installed locally as `NextGenEmby.App_0.1.0.100_x64__h8qjz0sr1sg4m`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.100 locally.
  - `M`, three `Down`, `Enter` opened Shows / TV Shows.
  - `Enter` on the first visible Series opened Details for `794450`.
  - Details loaded the Episodes section with `S1:E1 铸就传奇` through `S1:E6 你叫什么名字来着？`.
  - Pressing `Enter` from Details launched playback for `铸就传奇`; Playback showed `Playing`, Pause, seek controls, More, and Stop.
  - No app-content mouse clicks were used.

### 2026-07-06 - Home Library And Section Artwork Rail

- App version: 0.1.0.102.
- Scope: Home now treats server libraries and configured sections as a TV-first wide card rail, with section artwork selected from the section/parent item before falling back to child content.
- Data and visual changes:
  - Library and Home section wide artwork prefer `Thumb`, `Backdrop`, `Banner`, then `Primary`.
  - Server-configured section cards use their `ParentItem` image first; child item artwork is only a fallback.
  - `docs/DESIGN.md` now records this section-artwork rule.
  - Stale plan wording that still pointed to the rejected `Library Portal` icon direction was updated to the Matte Cinema Fluent icon direction.
  - Home focus now calls `StartBringIntoView` when a card receives focus so the current TV focus target remains fully visible while moving horizontally.
- Automated verification:
  - `EmbyArtworkPolicyTests` passed: 7 targeted tests.
  - Core tests passed: 212 total.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.102_x64_Debug.msix`.
  - MSIX signed and installed locally as `NextGenEmby.App_0.1.0.102_x64__h8qjz0sr1sg4m`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.102 locally.
  - Home parsed many server categories into `Media Libraries`, including `热门电影`, `热门剧集`, `豆瓣高分`, `Netflix`, `国产剧`, `国漫`, `日漫`, `动画电影`, `动作电影`, `美剧`, `综艺`, and more.
  - `Down` moved focus from the Home hero to the first visible media-library card.
  - Repeated `Right` moved across the horizontal rail; the focused `日漫` card stayed fully visible after the bring-into-view fix.
  - `Enter` opened the selected `日漫` Library, showing `100 items`, Sort, Filter, and a focused first content card.
  - Computer Use screenshot capture timed out once immediately after installing 0.1.0.102; rehydrating the app window and retrying with a lighter accessibility snapshot recovered. No app-content mouse clicks were used.

### 2026-07-06 - Matte Library Slat App Identity

- App version: 0.1.0.104.
- Scope: production UWP app identity was refreshed to match `docs/DESIGN.md` Matte Cinema Fluent direction, replacing the rejected cyan portal/glow language with a matte library slat mark.
- Visual changes:
  - App icon assets now use a matte rounded-square tile, layered dark media slats, a crisp cyan focus edge, a green play/confirm surface, and a flat amber progress base.
  - Splash screen copy changed from `Library Portal` to `Private media library`.
  - `docs/icon-concepts/README.md` now marks the old portal concept as historical and records the current production direction.
- Stability change:
  - Added early startup diagnostics around `App.InitializeComponent` and `OnLaunched`, written to LocalState `startup-diagnostics.log`, so launch-time UWP crashes are no longer opaque.
- Automated verification:
  - Core tests passed: 212 total.
  - Icon dimension gate passed: Store 50x50, Square44 44x44, Square150 150x150, Wide 310x150, Splash 620x300.
  - `git diff --check` passed with only existing line-ending warnings.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.104_x64_Debug.msix`.
  - MSIX signed and installed locally as `NextGenEmby.App_0.1.0.104_x64__h8qjz0sr1sg4m`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.104 locally; Home rendered Hero, Media Libraries, Continue watching, Hot Movies, and Hot TV Series.
  - `Down`, `Right`, `Right`, `Left` moved focus from Hero controls into the Media Libraries rail and left focus visibly on `热门剧集`.
  - `Return` opened `热门剧集`, showing `45 items`, Sort, Filter, and a focused first series card.
  - `Escape` returned to Home and preserved focus on the `热门剧集` media-library card.
  - Startup diagnostics recorded `App.InitializeComponent completed` and `App.OnLaunched completed`; no 0.1.0.104 crash appeared in the checked Application event sample.
  - No app-content mouse clicks were used.
