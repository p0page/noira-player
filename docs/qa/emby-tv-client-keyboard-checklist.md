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

### 2026-07-06 - Home Row More Keyboard Route And Matte Token Alignment

- App version: 0.1.0.132.
- Scope: align the runtime color palette with `docs/DESIGN.md` and make Home row `More` buttons reachable through D-pad style keyboard input.
- Visual token alignment:
  - `App.xaml` now maps the core runtime brushes to the `DESIGN.md` Matte Cinema Fluent palette: canvas `#050607`, surface `#101418`, raised surface `#1A2027`, overlay `#D9101418`, focus `#3BD5FF`, play/action `#61D47C`, progress/warm `#E0B86A`, hairline `#303842`, text `#F6F1E8`, muted text `#B9C0C8`, and scrim `#D9050607`.
  - Package tile and splash background now use `#050607`, matching the app canvas instead of the older blue-black startup color.
  - Hover/pressed button fills were shifted into the same matte neutral family instead of the older graphite-blue family.
- Interaction changes:
  - Added `HomeFocusZone.RowMore` to the core Home focus policy.
  - `Up` from a Home content row now targets that row's `More` button when present.
  - `Down` from a row `More` button returns to that row's first content card.
  - `Up` from the first row `More` returns to the first Media Libraries card, preserving the previous vertical hierarchy.
  - Home page now registers row `More` buttons as first-class focus targets rather than leaving them to mouse/hover reachability.
- Automated verification:
  - Added Core tests for row `More` targeting, rows without `More`, `Down` from row `More`, and first-row `More` upward navigation.
  - Core tests passed: 283 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.132_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.132`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.132 locally and waited for saved-session Home rows.
  - Home rendered Media Libraries, Continue watching, Hot Movies, Hot TV Series, and latest rows with the aligned matte palette.
  - Pressed `Down`, `Down`, `Down`, `Up`; focus moved to the `Hot Movies` row `More` button with a visible cyan focus frame.
  - Pressed `Return`; `热门电影` opened with `34 items`, Sort, Filter, Refresh, and a populated movie grid.
  - Pressed `Escape`; Home returned to the same real rails and kept focus on `Hot Movies` `More`.
  - Pressed `Down`; focus moved from `Hot Movies` `More` to the first `Hot Movies` poster (`奇幻变身大冒险`) instead of jumping to the next row.
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

### 2026-07-06 - Search Empty State Recovery

- App version: 0.1.0.105.
- Scope: Search now has a TV-focused empty/error recovery panel instead of status text only.
- Interaction changes:
  - Empty results show a centered matte panel with a readable title, recovery copy, and `Edit search`.
  - Search errors show the same recovery surface with `Edit search` and `Search again`.
  - D-pad/keyboard `Down` from the scope rail can move into the empty-state action.
  - `Return` on `Edit search` restores focus to the search box and selects the existing query for quick replacement.
- Automated verification:
  - Core tests passed: 214 total.
  - `SearchFocusNavigationPolicyTests` cover `Down` from scope rail into the empty state and `Up` back to the selected scope.
  - `git diff --check` passed with only line-ending warnings.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.105_x64_Debug.msix`.
  - MSIX signed and installed locally as `NextGenEmby.App 0.1.0.105`.
- Keyboard-only validation with Computer Use:
  - From Home, `M`, `Down`, `Return` opened Search with focus in the search box.
  - Typed `zzzzzznomatch20260706`, pressed `Return`, and got `No results` with a centered `Edit search` action.
  - Pressing `Down`, `Down` moved focus to `Edit search`; the focus frame was visibly clear.
  - Pressing `Return` on `Edit search` returned focus to the search box and selected the whole query.
  - Replacing it with `Terrifier` and pressing `Return` returned 3 media results and removed the empty panel.
  - No app-content mouse clicks were used.

### 2026-07-06 - Details Action Row

- App version: 0.1.0.107.
- Scope: Details now has a TV-first media action row with `Resume`/`Play`, `Restart`, `Add favorite`/`Remove favorite`, `Mark watched`/`Mark unwatched`, and `Refresh`.
- Interaction changes:
  - `Resume` remains the default first-viewport action.
  - `Restart` is visible only when the item has a resume position.
  - Favorite and watched actions update Emby user-data through the standard user-data endpoints and restore focus to the changed action.
  - Left/Right across the action row is handled explicitly so D-pad movement does not depend on UWP desktop focus guessing.
  - Down from an action moves to the first visible version button, and Up from versions returns to `Resume`/`Play`.
- Automated verification:
  - Core tests passed: 224 total.
  - `MediaDetailsActionPolicyTests` passed for resume, favorite/played labels, and non-playable restart hiding.
  - `MediaDetailsActionNavigationPolicyTests` passed for horizontal movement, hidden Restart skipping, and edge stops.
  - Emby user-data API tests passed for Favorite add/remove and Played mark/unmark.
  - `git diff --check` passed with only line-ending warnings.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.107_x64_Debug.msix`.
  - MSIX signed and installed locally as `NextGenEmby.App 0.1.0.107`.
- Keyboard-only validation with Computer Use:
  - From Home hero, `Right`, `Return` opened Details without mouse input.
  - Details exposed `Resume`, `Restart`, `Add favorite`, `Mark watched`, `Refresh`, `Versions`, audio summary, subtitle summary, and overview in the first text snapshot.
  - `Right`, `Right`, `Right`, `Left`, `Down`, `Up` were sent through Computer Use against the Details page without leaving the page or dead-ending.
  - Favorite and watched were not activated against the live Emby library during this run to avoid mutating the user's real server state; the write endpoints are covered by HTTP-level tests.
- Visual capture note:
  - Computer Use screenshot capture repeatedly hit `FrameArrived timed out` after installing 0.1.0.107. This run is therefore recorded as text-snapshot and keyboard-behavior validation, not fresh screenshot validation. No app-content mouse clicks were used.

### 2026-07-06 - Details Secondary Rails

- App version: 0.1.0.109.
- Scope: Details now loads below-fold Emby secondary content with `More like this` and `Cast & crew` rails, using real item/person artwork where available and fixed TV card dimensions.
- Interaction changes:
  - `GetItemAsync` requests `People` for Details so person rows can be rendered from Emby metadata.
  - `GetSimilarItemsAsync` calls `Items/{Id}/Similar` with the same image field policy used by other media rows.
  - Person cards open Library with a `PersonIds` query and media item types, instead of being inert text.
  - Details directional focus now explicitly moves through action row, versions, episodes, similar items, and people, so D-pad `Down` no longer gets trapped on the first version button.
- Automated verification:
  - Core tests passed: 226 total.
  - New Emby API tests cover Details `People` mapping and the `Items/{Id}/Similar` request shape.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.109_x64_Debug.msix`.
  - MSIX signed and installed locally as `NextGenEmby.App 0.1.0.109`.
- Keyboard-only validation with Computer Use:
  - From Home hero, `Right`, `Return` opened Details for `铸就传奇`.
  - Details exposed `Resume`, `Restart`, `Add favorite`, `Mark watched`, `Refresh`, two version buttons, audio/subtitle summaries, and `Cast & crew`.
  - `Down`, `Down`, `Down` moved focus from the action row through versions and onto the first person card `Arian Kashef`, with a visible cyan focus frame.
  - `Return` on `Arian Kashef` opened the filtered person Library showing `1 items`.
  - `Escape` returned one level back to the original Details page.
  - No app-content mouse clicks were used.
- Follow-up:
  - The sampled episode did not render a visible `More like this` rail from the live server response. The API and UI path are implemented, but a future run should validate a sampled item that returns similar items.
  - Details collection/playlist add-to actions were added in the 0.1.0.112 run below; future validation should use a disposable destination to verify live add success.

### 2026-07-06 - Details Organize Sheets

- App version: 0.1.0.112.
- Scope: Details now has an `Organize` section with collection/playlist membership summary, `Add to collection`, and `Add to playlist`.
- Interaction changes:
  - `Items/{Id}/Ancestors` is loaded for existing collection/playlist links when the server supports it.
  - `Add to collection` opens a centered matte TV sheet backed by `BoxSet` destinations.
  - `Add to playlist` opens the same sheet pattern backed by `Playlist` destinations.
  - Empty destination responses show a focusable empty row rather than a dead end.
  - B/Escape closes only the sheet and restores focus to the originating organize action.
- Automated verification:
  - Core tests passed: 230 total.
  - New API tests cover `Items/{Id}/Ancestors`, `POST /Collections/{Id}/Items`, and `POST /Playlists/{Id}/Items`.
  - New artwork policy test covers item wide artwork preference: `Thumb`, `Backdrop`, `Banner`, then `Primary`.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.112_x64_Debug.msix`.
  - MSIX signed and installed locally as `NextGenEmby.App 0.1.0.112`.
- Keyboard-only validation:
  - From Home hero, `Right`, `Return` opened Details for `閾稿氨浼犲`.
  - Details exposed `Organize`, `Library links unavailable.`, `Add to collection`, and `Add to playlist` below version/audio/subtitle selectors.
  - `Down`, `Down`, `Down`, `Return` opened `Add to collection`; the sheet showed a focusable `No destinations found.` empty row on this server.
  - `Escape`, `Right`, `Return` opened `Add to playlist`; it also showed the focusable empty row.
  - `Escape` closed the sheet and restored focus to `Add to playlist`, without navigating back from Details.
  - No app-content mouse clicks were used. Live add confirmation was not executed because this validation server exposed no collection/playlist destinations and the run should not mutate the user's real library accidentally.

### 2026-07-06 - Settings Diagnostics And Theme Tokens

- App version: 0.1.0.117.
- Scope: Settings stopped being a placeholder and now exposes signed-in server, app/client version, thumbstick seek-preview preference, input mapping, startup diagnostics summary, and recent startup log lines.
- Visual tokenization:
  - Added shared TV text, panel, diagnostics, and Settings checkbox styles in `App.xaml`.
  - Settings consumes these resources instead of page-local font, padding, and panel constants, establishing the migration pattern for future skin/theme dictionaries.
- Bugs found and fixed:
  - Settings initially failed through the debug route with `XamlParseException: Failed to assign to property 'ToggleButton.IsChecked'`; fixed by removing the XAML `IsChecked` default and loading the persisted value in code.
  - Startup diagnostics initially reported an old crash as the latest launch state; fixed by evaluating only the latest `App.ctor start` block.
  - Default Settings focus was not visually obvious enough; fixed by focusing the checkbox with keyboard focus state and shared focus visual settings.
- Automated verification:
  - `SettingsDiagnosticsFormatterTests` passed: 8 targeted tests.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.117_x64_Debug.msix`.
  - MSIX signed and installed locally as `NextGenEmby.App 0.1.0.117`.
- Local visual validation:
  - Debug route opened Settings with result `completed / settings`.
  - Screenshot showed account `cyber on https://c1.zdz.plus:443`, `App 0.1.0.117 / Emby client 0.1.0`, visible checkbox focus, input map, and `Last launch completed`.
  - No app-content mouse clicks were used.
- Keyboard validation limitation:
  - Windows `SendInput` kept `Next Gen Xbox Emby` foreground, but synthetic `Down`, `Enter`, and `Space` did not reliably drive UWP focus/toggle state in this desktop session.
  - Guide-to-Settings and checkbox toggle remain pending for a true hardware keyboard or dedicated Computer Use run; this batch does not mark those routes as Verified.

### 2026-07-06 - Photos Viewer Route And Shell Tokens

- App version: 0.1.0.120.
- Scope: Photos gained a dedicated activation route and immersive viewer fallback instead of sending Photo items through generic movie/series Details.
- Visual tokenization:
  - Added shared shell resources for Guide rail color and collapsed/expanded widths.
  - Added immersive viewer resources for scrim, control surface, and page safe margin.
  - Added `ShellChromePolicy` so standard pages, playback, and photo viewer chrome behavior are not hard-coded in `MainPage`.
- Automated verification:
  - `PhotoViewerInputPolicyTests`, `ShellChromePolicyTests`, `ShellNavigationFocusPolicyTests`, `LibraryItemActivationPolicyTests`, and `DevelopmentNavigationCommandTests` passed: 31 targeted tests.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.120_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.120`.
- Local visual and keyboard validation:
  - Debug route `photo` completed and opened `Missing Photo` in an immersive viewer.
  - Screenshot `nextgen-emby-photo-viewer-120-fallback.png` showed no left Guide rail, a clear Back focus frame, and a visible `Photo unavailable` fallback.
  - UI Automation reported current focus as `Back`; fallback text bounds were centered in the maximized UWP window.
  - `Escape` returned from the photo viewer to Home after the page-level handled-events-too B/Escape handler was added.
  - Debug route `photos` completed and opened Photos with `No items found` plus a focused `Retry` recovery action.
- Limitation:
  - The live server returned no Photo items in this run, so positive image loading from a real Photo item remains pending.

### 2026-07-06 - Live TV Browse Shell

- App version: 0.1.0.122.
- Scope: Live TV now has a dedicated browse shell instead of routing through the generic Library grid.
- API/data changes:
  - Added browse-only Emby Live TV models and client methods for `/LiveTv/Info`, `/LiveTv/Channels`, and `/LiveTv/Programs`.
  - No playback, transcoding, or native decode path was changed.
- Interaction changes:
  - Guide Live TV and debug route `livetv` open `LiveTvPage`.
  - The page checks Live TV availability, renders channels when returned, and shows a centered recovery panel when unavailable or empty.
  - Channel activation shows a focused `Live TV playback unavailable` layer instead of attempting playback.
  - B/Escape closes only the unsupported layer and restores focus to the Live TV page.
- Visual tokenization:
  - Added shared `TvListButtonStyle` and `TvBadgeTextStyle` in `App.xaml` for channel-list style surfaces.
- Automated verification:
  - `EmbyLiveTvTests`, `TransientLayerInputPolicyTests`, and `DevelopmentNavigationCommandTests` passed: 20 targeted tests.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.122_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.122`.
- Local visual and keyboard validation:
  - Debug route `livetv` completed and showed `Live TV unavailable` with focused `Retry` because this server returned no channels.
  - UI Automation reported focus as `Retry live TV`.
  - Escape from the fallback page returned to Home.
  - Debug route `livetv-unsupported` completed and showed `Live TV playback unavailable` with focused `Close`.
  - Escape closed only the unsupported layer and left the page on Live TV; UI Automation then reported focus as `Refresh live TV`.
- Limitation:
  - The live server did not return channels, so positive channel-row navigation and real channel activation remain pending on a Live TV-enabled server.

### 2026-07-06 - Music Browse Shell

- App version: 0.1.0.125.
- Scope: Music now has a dedicated TV browse shell instead of the generic Library grid.
- API/data changes:
  - Added `MusicBrowseQueryFactory` for album, all-song, and album-song Emby item queries.
  - Added `MusicBrowseItemPolicy` so incompatible servers cannot cause section cards or collection folders to be mislabeled as songs.
  - No playback, transcoding, or native decode path was changed.
- Interaction changes:
  - Guide Music and debug route `music` open `MusicPage`.
  - The page renders Albums, Songs, and Preview columns when true `MusicAlbum` or `Audio` items are returned.
  - Empty music results show a centered `No music found` recovery panel with `Retry`.
  - Song activation is browse-only for now and shows a focused `Music playback unavailable` layer.
  - B/Escape closes only the unsupported layer and leaves the page on Music.
- Visual tokenization:
  - Added shared `TvSectionTitleTextStyle`, `TvListArtworkSize`, and `TvCompactArtworkSize` in `App.xaml`.
  - Music consumes shared list, panel, badge, text, and artwork tokens instead of page-local theme colors.
- Bugs found and fixed:
  - The live server ignored or broadened `IncludeItemTypes=Audio`, initially causing Home/server section cards such as `热门电影` to appear in Songs. The new type guard now filters those out.
  - The debug `music-unsupported` path initially left Albums/Songs labels stuck on `Loading` after Escape; browse-only preview now initializes with `Refresh to load`.
- Automated verification:
  - `MusicBrowseQueryFactoryTests`, `MusicBrowseItemPolicyTests`, and `DevelopmentNavigationCommandTests.TryParseJson_Accepts_Guide_Routes` passed: 14 targeted tests.
  - App Debug x64 build passed and produced `NextGenEmby.App_0.1.0.125_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.125`.
- Local Computer Use validation:
  - Debug route `music` completed and opened Music.
  - Final 0.1.0.125 accessibility check reported `0 albums`, `No songs returned.`, `No music found`, and `Retry music`; no server section cards remained in Songs.
  - Escape from Music fallback returned to Home in the earlier 0.1.0.124 visual pass, and the only later Music change was the browse-only unsupported label initialization.
  - Debug route `music-unsupported` completed and showed `Music playback unavailable` for `Sample Song`.
  - Escape removed the unsupported layer; UI Automation then reported no `Music playback unavailable`, no `Sample Song`, and stable `Refresh to load` labels.
  - No app-content mouse clicks were used.
- Limitation:
  - This Emby server did not expose real `MusicAlbum` or `Audio` rows after type filtering, so positive album/song navigation remains pending on a true music library.
  - Windows Graphics Capture timed out once during final Music screenshot capture; resetting the Computer Use session recovered accessibility verification.

### 2026-07-06 - Cinema Shelf App Icon Refresh

- App version: 0.1.0.126.
- Scope: app identity assets were refreshed from `Matte Library Slat` to `Cinema Shelf Mark`.
- Design changes:
  - Updated the production icon vocabulary to a dark TV media shelf with a left Guide rail, content rails, one focused media card, green play surface, cyan focus edge, and amber progress base.
  - Added `docs/plans/2026-07-06-app-icon-refresh-design.md` for the selected direction and trade-offs.
  - Updated `docs/DESIGN.md`, `docs/icon-concepts/README.md`, and the complete-client design doc to name `Cinema Shelf Mark` as the current production direction.
- Asset generation:
  - Reworked `tools/Generate-AppIconAssets.ps1` so icon colors and geometry live in generator tokens.
  - Regenerated `StoreLogo.png`, `Square44x44Logo.png`, `Square150x150Logo.png`, `Wide310x150Logo.png`, and `SplashScreen.png`.
- Asset validation:
  - Pixel dimensions matched manifest requirements: 50x50, 44x44, 150x150, 310x150, and 620x300.
  - Color-pixel check found focus cyan, play green, and progress amber in the square, wide, and splash assets.
  - Visual inspection confirmed the 44 px icon keeps the TV shelf/focus-card silhouette, while wide and splash assets align with the dark Fluent shell.
- Local Computer Use validation:
  - Installed and launched 0.1.0.126 locally.
  - Normal launch reached Home with saved session and exposed Home, Continue watching, and Media Libraries.
  - Keyboard-only `Down`, `Return` opened a media Details page with `Play/Resume` controls.
  - Keyboard-only `Escape` returned to Home.
  - No app-content mouse clicks were used.

### 2026-07-06 - Settings Guide Route And Toggle Pass

- App version: 0.1.0.126.
- Scope: close the Settings keyboard gap left by the earlier local `SendInput` limitation.
- Keyboard-only validation with Computer Use:
  - Launched the installed app and reached Home with the saved Emby session.
  - Pressed `M` to open the expanded Guide rail.
  - Pressed `Up`; screenshot validation showed the Guide rail stayed open and the Home guide item had a visible cyan focus frame.
  - Pressed eleven `Down`, then `Return`; Settings opened from the Guide without using an app-content mouse click.
  - Settings exposed account `cyber on https://c1.zdz.plus:443`, `App 0.1.0.126 / Emby client 0.1.0`, `Playback input`, `Thumbstick seek preview`, the controller input map, and latest startup diagnostics.
  - Pressed `Space` on the default Settings focus; the status text changed to `Left thumbstick seek preview is off; D-pad seek remains available.`
  - Pressed `Space` again to restore the setting; the status text returned to `Left thumbstick previews the target position before seek commits.`
  - No app-content mouse clicks were used.
- Tooling note:
  - One Windows Graphics Capture screenshot request timed out after Settings opened. Accessibility text snapshots and keyboard behavior continued to work, so this run records both the screenshot evidence from the expanded Guide and text-snapshot evidence from Settings.

### 2026-07-06 - Theme Token Extraction Pass

- App version: 0.1.0.127.
- Scope: centralize repeated visual values so future skins can swap resource dictionaries rather than editing individual pages.
- Visual tokenization:
  - Added shared overlay/action resources in `App.xaml` for action foreground, artwork dimming, hero backdrop wash, details backdrop wash, modal scrim, playback canvas, playback overlay, and playback drawer.
  - Added shared text styles for subsection titles, option labels, and status text.
  - Home, Library, Search, Details, and Playback no longer contain page-local raw hex colors for artwork dimming, sheets, playback overlays, or green-action foreground text.
- Automated verification:
  - Core tests passed: 275 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.127_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.127`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.127 locally; Home rendered the saved Emby session with Continue watching, Media Libraries, Hot Movies, Hot TV Series, and latest rows.
  - Keyboard-only `Down`, `Return` opened the `热门电影` library with `34 items`, Sort, Filter, and a populated movie grid.
  - Keyboard-only `Return` opened Details for `137号案件`.
  - Details exposed Play, Add favorite, Mark watched, Refresh, Versions, audio/subtitle summaries, Organize, Cast & crew, and overview text.
  - Startup diagnostics recorded `App.InitializeComponent completed` and `App.OnLaunched completed` for the 17:49 local run.
  - No app-content mouse clicks were used.
- Visual capture note:
  - Windows Graphics Capture timed out during the final Details screenshot request. The route is validated by build output, launch diagnostics, accessibility text snapshots, and keyboard behavior; a fresh screenshot remains desirable in a later Computer Use session.

### 2026-07-06 - Player Focus App Icon Refresh

- App version: 0.1.0.130.
- Scope: replace the name/letter/shelf-oriented app identity with a player-attribute mark that survives future product renaming.
- Design changes:
  - `docs/DESIGN.md` now defines **Player Focus Mark** as the production icon direction.
  - The icon is symbol-only: compact playback viewport, cyan controller focus path, green play/confirm core, subtle subtitle/audio status marks, and amber progress base.
  - No production app icon asset embeds the current product name, product initials, or label text.
  - The icon generator maps directly to `DESIGN.md` tokens: canvas `#050607`, surface `#101418`, raised surface `#1A2027`, hairline `#303842`, focus `#3BD5FF`, play `#61D47C`, progress `#E0B86A`, text `#F6F1E8`, and muted text `#B9C0C8`.
- Asset generation:
  - Reworked `tools/Generate-AppIconAssets.ps1` around player-status primitives instead of media-library UI miniatures.
  - Removed unused text drawing helpers from the generator so future raster assets do not drift back into wordmark behavior.
  - Regenerated `StoreLogo.png`, `Square44x44Logo.png`, `Square150x150Logo.png`, `Wide310x150Logo.png`, and `SplashScreen.png`.
- Asset validation:
  - Pixel dimensions matched manifest requirements: 50x50, 44x44, 150x150, 310x150, and 620x300.
  - Exact color-token check found the required `DESIGN.md` core colors in square, wide, and splash assets.
  - Visual inspection confirmed the 44 px icon keeps focus/play/progress readable, the wide tile remains text-free, and the splash is symbol-only.
- Automated verification:
  - Core tests passed: 277 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.130_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.130`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.130 locally; Windows process and package path both resolved to `NextGenEmby.App_0.1.0.130_x64__h8qjz0sr1sg4m`.
  - Home exposed Home, Search, Movies, Favorites, Settings, Continue watching, Hot Movies, and library rows from the saved Emby session.
  - Pressed `M`, `Down`, `Down`, `Return`; Movies opened with Sort, Filter, Refresh, and a populated movie grid.
  - Pressed `Return` from Movies; Details opened with Resume/Play, audio/subtitle summaries, playlist action, and overview text, without starting playback.
  - Pressed `Escape`, then `M`, `Up`, `Return`; Search opened with focus in `Search title`.
  - Typed `friend`, pressed `Return`; Search returned `50 results / All`.
  - Pressed `Down`, `Down`, `Return`; the first reachable search result opened Details with Play as the default action, without starting playback.
  - No app-content mouse clicks were used.
- Visual capture note:
  - Windows Graphics Capture timed out during two app-window screenshot requests (`FrameArrived timed out`). Accessibility text snapshots, keyboard behavior, app version checks, and asset-level visual inspections were used as evidence for this pass.

### 2026-07-06 - Home Startup Timeout Recovery

- App version: 0.1.0.131.
- Scope: prevent saved-session Home startup from staying in the cleared placeholder state when one of the Home Emby requests stalls.
- Interaction changes:
  - Home list loads now use the shared interactive request timeout guard and return an empty list on timeout or request failure.
  - If Home has libraries but no playable hero item, the hero explains `Browse your libraries`, keeps Play/Details disabled, and moves focus to the first available library or recovery target.
  - No decoding, direct playback, or transcoding path changed.
- Automated verification:
  - Added Core tests for completed and timed-out interactive list requests.
  - Core tests passed: 279 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.131_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.131`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.131 locally and waited 18 seconds on Home.
  - Home resolved to real saved-session content instead of `Refresh after signing in`: Media Libraries included `热门电影`, `热门剧集`, `动画电影`, `动作电影`, `儿童电影`, `全部电影`, and `全部剧集`; rows included Continue watching, Hot Movies, Hot TV Series, and multiple Latest rows.
  - Pressed `Down`, `Return` from Home; the focused `热门电影` library opened with `34 items`, Sort, Filter, Refresh, and a populated movie grid.
  - Pressed `Escape`; Home returned with the same real rails and did not fall back to the placeholder state.
  - No app-content mouse clicks were used.
- Visual capture:
  - Windows Graphics Capture succeeded at 2560x1392.
  - The captured Home frame showed the hero, focused Media Libraries card, Continue watching, Hot Movies, and Hot TV Series in a dense but readable TV layout.

### 2026-07-06 - Poster Grid Tokens And Search Recovery

- App version: 0.1.0.129.
- Scope: Library/Search poster-grid tokenization and Search request recovery after a live keyboard route previously stayed in `Searching All...` for longer than the interactive request budget.
- Visual tokenization:
  - Added shared poster-grid resources in `App.xaml` for item margin, card width/height, corner radius, scrim padding, card title/meta typography, fallback initials, and empty-state title/body typography.
  - Library and Search now consume the shared poster-grid resources instead of owning local card dimensions and repeated text sizes.
  - Search column-count navigation reads the shared poster card width and grid item margin resources, keeping directional focus math aligned with future skin/card-size changes.
- Search recovery:
  - Added `InteractiveRequestGuard.WithTimeoutAsync` in Core.
  - Search wraps Emby search requests with the interactive timeout so the page can return to the existing retry/edit recovery panel if the request stalls.
  - Added Core tests covering completed requests and non-completing request timeout.
- Automated verification:
  - Core tests passed: 277 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.129_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.129`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.129 locally; Home rendered the saved Emby session with Media Libraries, Continue watching, Hot Movies, Hot TV Series, and latest rows.
  - From Details, pressed `M`, `Down`, `Down`, `Return`; Movies opened with `100 items`, Sort, Filter, and a populated poster grid.
  - From Movies, pressed `M`, `Up`, `Return`; Search opened with focus in `Search title`.
  - Typed `Terrifier`, pressed `Return`, waited 14 seconds, and Search showed `3 results / All` rather than staying in `Searching All`.
  - Pressed `Down`, `Down`, `Return` from Search and opened Details for `断魂小丑`.
  - No app-content mouse clicks were used.

### 2026-07-06 - Details Poster Anchor And Design Color Check

- App version: 0.1.0.133.
- Scope: keep the current Matte Cinema Fluent palette from `docs/DESIGN.md`, extract Details layout measurements into shared XAML resources, and fix the Details poster being vertically centered too low in the first viewport.
- Color review:
  - Runtime `App.xaml` core colors match the active `docs/DESIGN.md` palette: canvas `#050607`, surface `#101418`, raised surface `#1A2027`, hairline `#303842`, focus `#3BD5FF`, play/action `#61D47C`, progress/warm `#E0B86A`, primary text `#F6F1E8`, muted text `#B9C0C8`, and subtle text `#78838F`.
  - The current visual issue was layout/composition rather than a need to change palette. This pass did not introduce a new color direction.
  - Remaining alpha variants such as modal/detail scrims are implementation overlays derived from the same dark matte roles and should stay tokenized.
- Visual tokenization:
  - Added shared Details measurements in `App.xaml` for poster width/height, column spacing, content max width, and logo max size.
  - `MediaDetailsPage.xaml` now consumes those resources instead of duplicating hard-coded Details measurements.
  - The Details poster is explicitly top-aligned so the poster, title, metadata, and action row read as one first-viewport decision surface.
- Automated verification:
  - Core tests passed: 283 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.133_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.133`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.133 locally; Home rendered the saved Emby session with Hero, Media Libraries, Continue watching, Hot Movies, Hot TV Series, and latest rows.
  - From Hero `Play`, pressed `Down`, `Down`, `Return`; the first Continue watching item opened Details for `铸就传奇` instead of starting playback.
  - Details showed `铸就传奇`, `Resume` as the default focused action, `Restart`, `Add favorite`, `Mark watched`, Refresh, two version rows, audio/subtitle summaries, Organize, and overview text.
  - Windows Graphics Capture succeeded at 2560x1392. The captured Details frame showed the poster anchored near the top of the first viewport beside the title/action stack, not centered low in the row.
  - Pressed `Escape`; Home returned and focus landed on the originating first Continue watching card.
  - No app-content mouse clicks were used.

### 2026-07-06 - Playback More Drawer Keyboard Focus

- App version: 0.1.0.137.
- Scope: stabilize the playback More drawer for keyboard/controller input without touching native decoding, direct playback, or transcoding paths.
- Interaction changes:
  - Added a core `PlaybackMoreDrawerFocusPolicy` so Source, Audio, Subtitles, and Info use a deterministic vertical order and disabled stream controls are skipped.
  - The More drawer now sets an explicit TV-visible focus border/background on the current drawer target, independent of UWP ComboBox focus quirks.
  - Handled Up/Down key events are still routed to the drawer while it is open, preventing ComboBox controls from opening unexpectedly during directional movement.
  - Source, Audio, and Subtitles controls use the shared `TvComboBoxStyle` so their size, colors, borders, and focus visuals are tokenized for future skin work.
- Design/color review:
  - The drawer focus visuals consume the existing `docs/DESIGN.md` token roles through `AppAccentBrush`, `AppHairlineBrush`, `AppRaisedSurfaceBrush`, and `AppChromeBrush`.
  - This pass did not introduce a new palette direction; it keeps the Matte Cinema Fluent colors and improves focus hierarchy.
- Automated verification:
  - TDD red path confirmed the new focus policy tests failed before the policy existed.
  - Targeted focus policy tests passed: 5 total.
  - Core tests passed: 288 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.137_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.137`.
- Keyboard-only validation with Computer Use and local UI Automation:
  - Launched 0.1.0.137 locally into the saved Emby Home session.
  - Home rendered real library rows and section artwork including Hot Movies, Hot TV Series, Douban-style high score, Netflix, domestic drama, Chinese animation, Japanese animation, animated movies, and action movies.
  - From Home, used keyboard input to start playback from the hero Play route.
  - Playback opened for the selected movie and showed the OSD with `Playing`, Pause, Resume, 10s, 30s, More, and Stop controls.
  - Pressed `M`; the More drawer opened and stayed visible with Source as the initial cyan-focused target.
  - Pressed `Down` to Audio, `Down` to Subtitles, and `Down` to Info. Focus moved through the drawer targets and did not open the subtitle dropdown unexpectedly.
  - Pressed `Return` on Info; the playback diagnostics panel opened with State, Item, Source, HDR, Audio, Subtitles, Position, HDR output, swapchain, video processor, and URL fields.
  - Pressed `Escape` until Home returned. The app ended outside playback with the saved Home rows visible.
  - No app-content mouse clicks were used.
- Tooling note:
  - The full visual Computer Use route was completed earlier in this batch. After the final 0.1.0.137 package version bump, the resumed session did not expose the Computer Use operation tools, so the final installed package was rechecked with keyboard-only `SendKeys` plus Windows UI Automation text/focus snapshots.
- Limitation:
  - This run validated drawer focus, activation, and persistence on the available media item. A fresh multi-audio or subtitle-switching content route is still needed to verify actual stream switching on items with multiple selectable tracks.

### 2026-07-06 - Playback Transport Focus Activation

- App version: 0.1.0.138.
- Scope: make the visible playback transport focus the source of truth for `Enter`/A activation, so the OSD behaves like a controller surface even when UWP UI Automation reports focus on the window rather than the button.
- Regression found on 0.1.0.137:
  - Playback OSD showed a cyan focus frame on `Pause`, but `Return` did not pause playback because the routed focus path still resolved to the window.
  - This violated the TV rule that the highlighted control must be the action triggered by A/Select.
- Interaction changes:
  - Added a core `PlaybackTransportFocusPolicy` with deterministic Pause, Resume, seek back, seek forward, More, and Stop ordering.
  - Playback now tracks the current transport target explicitly while the OSD is visible.
  - `Left`/`Right` and gamepad D-pad left/right move through enabled transport controls and skip disabled targets such as `Resume` while playing.
  - `Return`, `Space`, and gamepad A activate the tracked transport target directly instead of relying on platform focus quirks.
  - Transport focus visuals use the existing Matte Cinema Fluent token brushes: `AppAccentBrush`, `AppHairlineBrush`, `AppRaisedSurfaceBrush`, and `AppChromeBrush`.
- Automated verification:
  - TDD red path confirmed the new transport focus policy tests failed before the policy existed.
  - Targeted transport focus tests passed: 4 total.
  - Targeted playback input tests passed: 28 total.
  - Core tests passed: 292 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.138_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.138`.
- Keyboard-only validation with Computer Use:
  - Started from active playback and the More drawer, with `Playing`, Pause, disabled Resume, `10s`, `30s`, More, and Stop visible.
  - Pressed `Escape`; More closed and the OSD returned focus to the More transport target.
  - Pressed `Left`, `Left`, `Left`; visual focus moved to `Pause`.
  - Pressed `Return`; playback state changed from `Playing` to `Paused`, and visual focus moved to `Resume`.
  - Pressed `Return` again; playback state changed back to `Playing`, and visual focus returned to `Pause`.
  - Pressed `Right`, `Right`; visual focus moved to `30s`.
  - Pressed `Return`; playback continued and the reported OSD position advanced to `00:07:52`, confirming the focused seek-forward action fired.
  - Pressed `M`; the More drawer opened from the transport strip with Source focused.
  - Pressed `Escape`; More closed and visual focus returned to the More transport button.
  - No app-content mouse clicks were used.
- Tooling note:
  - UI Automation continued to report the window as the focused element in several snapshots, but the screenshot and state text showed the tokenized cyan transport focus and the correct playback state transitions. The app now owns the TV focus target instead of depending on UIA focus accuracy.

### 2026-07-06 - Playback Seek Preview Surrogate And Drawer Cancel Recovery

- App version: 0.1.0.143.
- Scope: add a local keyboard surrogate for left-thumbstick seek preview, make the seek-preview prompt name both controller and keyboard confirm/cancel inputs, fix handled `Escape` not closing the playback More drawer, and add a DEBUG-only manual playback route for QA setup.
- Interaction changes:
  - `Shift+Left` and `Shift+Right` are now the local keyboard surrogate for thumbstick seek preview. Plain `Left`/`Right` remain D-pad transport focus/seek behavior.
  - The seek-preview prompt now reads `A/Enter Confirm / B/Escape Cancel`.
  - Handled `Escape` is routed through the page when More is open or seek preview is active, while open ComboBox controls keep their own `Escape`.
  - DEBUG `manual-playback` route opens the existing Manual Direct Stream panel without an Emby session.
  - The Manual Direct Stream text box now has an explicit Enter policy, so a valid URL can be started from the text field without needing a mouse.
- Automated verification:
  - TDD red path confirmed missing `PlaybackSeekPreviewPrompt`, handled-shortcut routing, `manual-playback` route, and manual direct-stream input policy before implementation.
  - Targeted Core tests passed: 64 total across `ManualDirectStreamInputPolicyTests`, `DevelopmentNavigationCommandTests`, `PlaybackOverlayInputPolicyTests`, `PlaybackSeekPreviewKeyboardPolicyTests`, `SeekPreviewSessionTests`, and `PlaybackTransportFocusPolicyTests`.
  - App Debug x64 builds passed with 0 warnings and 0 errors through the final `NextGenEmby.App_0.1.0.143_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.143`.
- Keyboard-only validation with Computer Use:
  - Launched 0.1.0.143 locally. The saved Emby session was not present; Home stayed at `Refresh after signing in to load your Emby home screen`, so a real Emby playback seek-preview route could not be completed in this run.
  - Wrote a local DEBUG `dev-command.json` with `{"route":"manual-playback"}` and launched the app into Manual Direct Stream.
  - Pressed `M`; the More drawer opened from the playback OSD.
  - Pressed `Escape`; the right-side Playback Options drawer closed and focus returned to the bottom transport strip, validating the handled-Cancel fix with keyboard input only.
  - Attempted to use Manual Direct Stream as a seekable playback fixture. The first public MP4 URL returned `403 Forbidden`; a second HEAD-verified MP4 could not be reliably entered/started through Computer Use text input before this pass ended, so seek-preview cancel/confirm remains unverified at runtime.
  - No app-content mouse clicks were used.
- Limitation:
  - This pass proves the seek-preview keyboard policy and prompt at the Core level and verifies the More drawer cancel regression in the installed app. It does not yet prove runtime seek preview on a seekable local playback item. The next pass should restore a playable saved session or add a deterministic seekable QA fixture before marking `Seek preview and cancel` as `Verified`.

### 2026-07-06 - Player Focus Icon Contract Guard

- Scope: turn the current `Player Focus Mark` app-icon direction into a repeatable contract, without creating a new visual concept.
- Design alignment:
  - The production direction remains symbol-only: compact playback viewport, cyan controller-focus path, green play/confirm core, subtle playback status marks, and amber progress base.
  - The generator no longer contains text rendering setup. This keeps the icon independent of the current product name, product initials, or any splash copy.
  - The generator token contract maps directly back to `docs/DESIGN.md` colors for canvas, surface, raised surface, hairline, focus, play, progress, text, and muted text.
- Automated verification:
  - TDD red path confirmed the symbol-only generator test failed while `TextRenderingHint` still existed in `tools/Generate-AppIconAssets.ps1`.
  - Targeted icon contract tests passed: 3 total.
  - Contract coverage verifies required PNG dimensions, package manifest references, project asset includes, text-free generator APIs, rejected old icon wording, and icon token alignment with `docs/DESIGN.md`.
- Runtime validation:
  - Local asset inspection opened `Square44x44Logo.png`, `Wide310x150Logo.png`, and `SplashScreen.png`. The 44 px asset preserves focus/play/progress, the wide tile reads as a player tile rather than a banner ad, and the splash remains symbol-only.
  - Computer Use app-control tools were not exposed in this resumed turn. No new keyboard/screenshot runtime icon validation was performed in this pass.
  - The next visual verification pass should inspect the Start tile/package surface after reinstalling the current MSIX and compare the generated icon against the 44 px, wide tile, and splash constraints.

### 2026-07-06 - Manual Playback Dev Command Fixture

- App version: 0.1.0.144.
- Scope: make local playback QA setup deterministic when the saved Emby session is missing or Computer Use text entry is unreliable.
- Interaction changes:
  - DEBUG `dev-command.json` for `manual-playback` now accepts `streamUrl` and `autoStart`.
  - PlaybackPage opens the Manual Direct Stream panel with the URL prefilled.
  - `autoStart` is ignored when the URL is empty; with a valid URL, it starts the same existing manual direct-stream path.
  - This does not change core decoding or Emby item playback logic.
- Automated verification:
  - TDD red path confirmed `DevelopmentNavigationCommand` lacked `StreamUrl`/`AutoStart`.
  - TDD red path confirmed `ManualDirectStreamLaunchOptions` did not exist.
  - Targeted Core tests passed: 22 total across `DevelopmentNavigationCommandTests`, `ManualDirectStreamLaunchOptionsTests`, and `ManualDirectStreamInputPolicyTests`.
  - App Debug x64 build passed, producing `NextGenEmby.App_0.1.0.144_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.144`.
- Keyboard/UIA validation:
  - Wrote `dev-command.json` with route `manual-playback`, a public MP4 `streamUrl`, and `autoStart:false`.
  - Launched the installed app. `dev-command-result.txt` reported `completed / manual-playback`.
  - UI Automation found the Manual Direct Stream page and confirmed the `Direct stream URL` textbox value matched the dev-command URL.
  - Focused the textbox through UI Automation and sent keyboard `Enter`; playback diagnostics showed FFmpeg opened the MP4 and the native backend reached `Opening` then `Playing`.
  - Re-ran with `autoStart:true` and no keyboard activation; diagnostics again showed `Native open enter`, `Opening`, and `Playing`.
  - No app-content mouse clicks were used.
- Limitation:
  - The verified public MP4 is a short 10-second sample and naturally ended quickly. It proves the QA fixture can start native direct playback, but a longer seekable URL or restored saved Emby playback route is still needed before marking seek-preview cancel/confirm as runtime `Verified`.

### 2026-07-06 - Seek Preview Cancel And Commit Runtime Pass

- App version: 0.1.0.144.
- Scope: validate the keyboard/controller surrogate route for seek-preview cancel and commit on an installed app, without depending on the missing saved Emby session.
- Fixture:
  - Used DEBUG `manual-playback` dev-command with `autoStart:true`.
  - Stream URL: `https://download.blender.org/peach/bigbuckbunny_movies/BigBuckBunny_320x180.mp4`.
  - `curl -I -L` returned `HTTP/1.1 200 OK`, `Content-Type: video/mp4`, `Content-Length: 64657027`, and `Accept-Ranges: bytes`.
- Keyboard/UIA validation:
  - Launched installed `NextGenEmby.App 0.1.0.144`; `dev-command-result.txt` reported `completed / manual-playback`.
  - UI Automation text snapshot showed `Manual Direct Stream`, `Playing`, and the transport strip controls.
  - Focused `Pause` and sent `Shift+Right`; UI showed `Seek preview 00:00:12 - A/Enter Confirm / B/Escape Cancel`.
  - Sent `Escape`; UI showed `Playing - Seek canceled` and the seek-preview text disappeared.
  - Sent `Shift+Right` again; UI showed `Seek preview 00:00:14 - A/Enter Confirm / B/Escape Cancel`.
  - Sent `Enter`; UI showed `Playing - Position 00:00:14`.
  - Native playback diagnostics logged `PlaybackGraph.SeekPreroll reached target=146613333`, proving the commit path reached the backend seek logic.
  - No app-content mouse clicks were used.
- Result:
  - `Seek preview and cancel` is now runtime `Verified` for the local keyboard surrogate route.
- Limitation:
  - This validates the local keyboard surrogate and native direct-stream route. A later pass should still re-run the same behavior on real Emby media after the saved session is restored, and on actual controller thumbstick input when the Xbox is available.

### 2026-07-06 - Guide Live TV Music Photos Keyboard Route

- App version: 0.1.0.144.
- Scope: validate the main Guide route to Live TV, Music, and Photos on the restored saved Emby session.
- Keyboard/UIA validation:
  - Started from Home with real saved-session rows visible.
  - Pressed `M`; Guide opened with Home focused and visible routes Home, Search, Movies, Shows, Live TV, Collections, Playlists, Music, Photos, Favorites, Unwatched, and Settings.
  - Pressed `Down` four times and `Enter`; Live TV opened.
  - Live TV showed `Select a channel`, `Live TV unavailable`, `This server did not return Live TV channels`, and `Retry`.
  - Pressed `M`, then `Down` three times and `Enter`; Music opened.
  - Music showed `Albums`, `0 albums`, `Songs`, `No songs returned`, `Select music`, and `No music found`.
  - Pressed `M`, then `Down` once and `Enter`; Photos opened.
  - Photos showed `Photos` and `No items found`.
  - No app-content mouse clicks were used.
- Result:
  - `Navigate Live TV/Music/Photos` is now runtime `Verified` for keyboard Guide navigation.
- Limitation:
  - The current server still returned no Live TV channels, no true MusicAlbum/Audio rows, and no Photo items in this run. Positive browsing for those libraries remains a server-data gap rather than a Guide navigation gap.

### 2026-07-06 - Movies Grid Directional Focus Runtime Pass

- App version: 0.1.0.144.
- Scope: validate ordinary library grid browsing with keyboard/controller-equivalent directional input.
- Keyboard/UIA validation:
  - Started from Home with the restored saved Emby session.
  - Pressed `M`, then `Down`, `Down`, `Enter` to open Movies through the Guide.
  - Movies opened with `100 items`, Sort `Title`, Filter `All`, and a real poster grid including `"Friends"`, `#Guilty`, `#PTGF出租女友`, `#Y`, and more.
  - Initial focus was a `GridViewItem` at x=192, y=365.
  - Pressed `Down`; focus moved to another `GridViewItem` at x=192, y=761.
  - Pressed `Right`; focus moved to x=465, y=761.
  - Pressed `Right`; focus moved to x=738, y=761.
  - Pressed `Down`; focus moved to x=738, y=1157.
  - Pressed `Left`; focus moved to x=465, y=1157.
  - No app-content mouse clicks were used.
- Result:
  - `Move across and down grid` is now runtime `Verified` for normal library-grid browsing.
- Follow-up:
  - Add a later far-right/end-of-list stress pass to verify edge wrapping and bring-into-view behavior at the bottom/right edge of very long grids.
