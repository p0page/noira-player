# Emby TV Client Keyboard Checklist

Date: 2026-07-06

This checklist is the required local verification path after every meaningful UI or interaction development batch. Use keyboard input only for app interaction, because these keys map to the future controller path.

Mouse is allowed only for operating external tooling, window management, or closing system prompts that cannot be reached by app input. Do not use mouse clicks inside the app content to pass a checklist item.

For visual-system acceptance, run the relevant batch in `docs/qa/design-conformance-checklist.md` alongside this functional checklist. Record the whole batch first, then fix shared visual or interaction causes together instead of repairing one route at a time.

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
- Focus visible from TV distance without relying on a bright complete perimeter frame as the default card focus.
- Page proportions are not oversized.
- Artwork is used in preference to flat placeholders when server provides it.
- The screen still matches `docs/DESIGN.md` gates: neutral graphite chrome, restrained green, no decorative blur over plain graphite, no default highlit borders, and no committed private media.

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

For design-system failures, finish the current design-conformance batch first, document all findings, then make one grouped fix plan and rerun the batch.

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
  - Screenshot showed a redacted signed-in private server, `App 0.1.0.117 / Emby client 0.1.0`, visible checkbox focus, input map, and `Last launch completed`.
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
  - Settings exposed a redacted signed-in private server, `App 0.1.0.126 / Emby client 0.1.0`, `Playback input`, `Thumbstick seek preview`, the controller input map, and latest startup diagnostics.
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

### 2026-07-06 - Runtime Color Token Contract

- Scope: tighten the Matte Cinema Fluent color system so runtime resource colors, alpha overlays, focus secondary line, and disabled/hover button states are backed by `docs/DESIGN.md` tokens instead of page-local or brush-local hex values.
- Visual tokenization:
  - Added DESIGN.md YAML tokens for runtime alpha/state colors: shell rail, immersive scrim, chrome hover/pressed, hero wash variants, artwork dimming, modal scrim, playback drawer, and button disabled/hover borders.
  - Added matching `App.xaml` color resources and brushes for canvas alternate, text subtle, tertiary, danger, on-secondary, transparent, and the button state colors.
  - Changed `SystemControlFocusVisualSecondaryBrush` to consume `AppFocusSecondaryColor` from `DESIGN.md` rather than a hard-coded translucent pure white.
- Automated verification:
  - TDD red path confirmed `App_Runtime_Colors_Are_Backed_By_Design_Tokens` failed while `AppCanvasAltColor` and other runtime color tokens were missing.
  - Targeted design tests passed: 5 total across `DesignTokenResourceTests` and icon-token contract coverage.
  - App Debug x64 build passed with 0 warnings and 0 errors.
  - `git diff --check` passed; only line-ending normalization warnings were reported.
- Keyboard/UIA validation:
  - Computer Use app-control tools were not exposed in this resumed turn, so validation used Windows UI Automation plus `SendKeys`.
  - Attempted to register the current Debug loose manifest; Windows rejected it because the manifest was not in a package root.
  - Attempted to install the generated `NextGenEmby.App_0.1.0.144_x64_Debug.msix`; Windows rejected it because the package was unsigned.
  - Launched the already installed `NextGenEmby.App 0.1.0.144` and performed a keyboard-only shell smoke route: initial focus was a Home action button at rect `233,484,212,75`; `M` opened the Guide with Home focused at rect `12,216,346,78`; `Down` moved to a content button at rect `456,706,375,197`; `Right` moved to rect `663,706,375,197`; `Escape` returned focus to rect `233,484,212,75`.
  - No app-content mouse clicks were used.
- Limitation:
  - The current source resource changes are proven by tests and the local Debug build. The keyboard smoke used the already installed 0.1.0.144 package because the newly built unsigned MSIX could not be deployed over the installed app in this turn.

### 2026-07-06 - Details Version Selection Policy

- App version: 0.1.0.145.
- Scope: make Details version/source buttons select a media source before playback instead of immediately navigating to Playback.
- Regression addressed:
  - `SourceVersion_OnClick` previously called playback navigation directly. This made a version row behave like Play and violated the TV rule that A/Enter on a selector should change selection without a surprising playback transition.
- Interaction changes:
  - Details now tracks `_selectedMediaSourceId`.
  - Play and Restart resolve the selected media source, falling back to the first available source when the selection is stale.
  - Selecting a version updates status text and the version button selected state, but does not start playback.
  - Selected version buttons use existing Matte Cinema Fluent resources: `AppAccentBrush`, `AppChromePressedBrush`, `AppHairlineBrush`, and `AppChromeBrush`.
- Automated verification:
  - TDD red path confirmed `MediaDetailsVersionSelectionPolicyTests` failed before the policy existed.
  - Targeted version-selection tests passed: 4 total.
  - Targeted input tests passed: 103 total.
  - App Debug x64 clean build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.145_x64_Debug.msix`.
- Local deployment and keyboard validation:
  - Signed and installed the generated 0.1.0.145 MSIX with the trusted `CN=NextGenEmby` certificate.
  - The installed MSIX crashed before exposing a window. Event Viewer reported `MoAppCrash` for `NextGenEmby.App_0.1.0.145_x64__h8qjz0sr1sg4m`, `KERNELBASE.dll`, exception `0xe0434352`, during activation.
  - Re-registered the clean Debug loose output and also built/registered the previous `bb0a43c`/0.1.0.144 output as a control. Both failed to expose a targetable app window on this machine after the package reinstall cycle.
  - Directly running the loose UWP exe printed `System.IO.FileNotFoundException: System.Private.CoreLib, Version=4.0.0.0`, which is expected for direct non-AppContainer launch and suggests the remaining blocker is local UWP activation/runtime state rather than the Details selector policy itself.
  - No app-content mouse clicks were used. Runtime keyboard validation for this route remains blocked by local UWP startup, so this pass is not marked runtime Verified.
- Follow-up:
  - Restore a working local UWP launch path, then run Home -> Movies -> multi-version Details with keyboard only. Expected result: Enter on a version stays on Details and visibly selects the source; Enter on Play starts playback with that selected `MediaSourceId`.

### 2026-07-06 - Debug Startup Recovery And Manual Playback Focus

- App version: 0.1.0.148.
- Scope: restore a reliable local UWP launch and keyboard validation route after the saved Emby session was lost, without changing native decoding or normal Emby playback logic.
- Root cause:
  - The earlier local activation failure came from registering/installing the wrong loose package layout: the XAML metadata provider path was `CLRHost.dll`.
  - The clean MSIX package layout uses `NextGenEmby.App.exe` as the provider path and launches correctly.
  - `MainPage_OnLoaded` also gated DEBUG `dev-command.json` execution behind a saved session, so a missing session left the app on Login and blocked local playback/OSD validation.
- Interaction changes:
  - Added a DEBUG startup policy so a present `dev-command.json` runs even when no saved Emby session exists.
  - Manual Direct Stream now chooses initial focus from a policy: valid playable URL focuses `Play direct stream`; invalid or empty URL focuses the URL box.
  - PlaybackPage applies the manual focus target after Loaded with retry diagnostics, avoiding early focus failure before the page is ready.
- Automated verification:
  - TDD red path confirmed `DevelopmentCommandStartupPolicy` was missing before implementation.
  - TDD red path confirmed the manual direct-stream initial-focus policy was missing before implementation.
  - Core tests passed: 323 total.
  - `git diff --check` passed; only line-ending normalization warnings were reported.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.148_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.148`.
  - Installed package manifest provider paths were correct: `NextGenEmby.Native.dll`, `NextGenEmby.App.exe`, and `Microsoft.Web.WebView2.Core.dll`.
- Keyboard-only validation with Computer Use:
  - Wrote a local DEBUG `dev-command.json` using route `manual-playback`, stream URL `https://download.blender.org/peach/bigbuckbunny_movies/BigBuckBunny_320x180.mp4`, and `autoStart:false`.
  - Launched the installed app through Computer Use. `dev-command-result.txt` reported `completed / manual-playback`.
  - Playback diagnostics showed `MainPage.Navigated page=PlaybackPage parameter=ManualDirectStreamLaunchOptions`.
  - Diagnostics showed manual initial focus first failed before layout, then succeeded on Loaded with `target=StartButton applied=True`.
  - Pressed `Enter`; the native backend opened the MP4 and reached `Opening` then `Playing`. UIA text showed `Playing`, enabled `Pause`, enabled `Stop`, and `More`.
  - Pressed `M`; the playback More drawer opened and exposed `Playback Options`, current source `Manual Direct Stream`, Audio, Subtitles, and Info.
  - Pressed `Escape`; More closed and playback remained `Playing`.
  - No app-content mouse clicks were used.
- Tooling note:
  - Computer Use screenshot capture intermittently timed out on this UWP/native-surface window, so this run relied on accessibility text plus app diagnostics. UI Automation continued to report the text box as the focused element even when app logs and keyboard behavior proved the TV action target was active.
- Follow-up:
  - Re-run the real Emby Details version-selection route after a saved session or test credentials are restored.
  - Keep using the manual direct-stream fixture as the deterministic local playback/OSD regression path while the Xbox is unavailable.

### 2026-07-06 - Home Dynamic Card Theme Tokens

- App version: 0.1.0.150.
- Scope: continue the Matte Cinema Fluent skinning migration by removing the remaining page-local raw color brushes from Home dynamic card code.
- Visual tokenization:
  - Added `library_artwork_wash` and `section_artwork_wash` to `docs/DESIGN.md`.
  - Added matching `AppLibraryArtworkWashBrush` and `AppSectionArtworkWashBrush` resources to `App.xaml`.
  - Home media library cards and server section cards now consume those wash brushes.
  - Home poster fallback now consumes `AppRaisedSurfaceBrush`; poster artwork dimming consumes the existing `AppArtworkDimBrush`.
- Automated verification:
  - TDD red path confirmed `View_CodeBehind_Does_Not_Create_Page_Local_Raw_Color_Brushes` failed on four `HomePage.xaml.cs` raw color brushes before migration.
  - Design token tests passed: 3 total.
  - Manual direct-stream focus retry policy tests passed after a runtime smoke caught the pre-Loaded retry exhaustion bug.
  - Core tests passed: 328 total.
  - `git diff --check` passed; only line-ending normalization warnings were reported.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.150_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.150`.
- Keyboard-only validation with Computer Use:
  - Wrote the DEBUG `manual-playback` fixture with the Big Buck Bunny MP4 URL and `autoStart:false`.
  - Launched installed 0.1.0.150 and waited for the route to enter Manual Direct Stream.
  - Pressed `Enter`; UIA text showed `Playing`, enabled `Pause`, enabled `Stop`, and visible `More`.
  - Pressed `M`; the More drawer exposed `Playback Options`, source `Manual Direct Stream`, Audio, Subtitles, and Info.
  - Pressed `Escape`; More closed while playback continued.
  - Native diagnostics showed `NativePlaybackEngine.OpenAsync success end`, `Native state Playing`, and increasing render/audio counters.
  - No app-content mouse clicks were used.
- Limitation:
  - The saved Emby session is still absent on this machine, so this pass could not visually inspect real Home library cards after the token migration. The new design test prevents Home code-behind from reintroducing page-local raw color brushes, and a future saved-session run should capture Home screenshots with real server artwork.

### 2026-07-06 - Home Fixture Screenshot Route

- App version: 0.1.0.153.
- Scope: add a deterministic DEBUG-only Home QA route so Home visual and keyboard checks can run on this machine even while the saved Emby session is absent.
- Interaction changes:
  - `dev-command.json` accepts route `home-fixture`.
  - The route navigates to Home with representative fixture data: Continue watching, Next up, Media Libraries, server-configured rows such as Hot Movies, Hot TV Series, Douban Top Rated, Netflix, Anime, and Documentaries.
  - The fixture uses packaged QA artwork under `Assets/QaHome` and the normal Home rendering path, not a separate demo page.
  - Hero and dynamic Home buttons now expose automation names so keyboard/UIA verification can identify the current controller target.
- Automated verification:
  - TDD red path confirmed `home-fixture` was not accepted before route support.
  - TDD red path confirmed `DevelopmentHomeFixture` was missing before implementation.
  - TDD red path confirmed Hero action buttons did not expose automation names before the XAML update.
  - Targeted fixture/accessibility tests passed.
  - App Debug x64 build passed, producing `NextGenEmby.App_0.1.0.153_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.153`.
- Keyboard-only validation:
  - Wrote `dev-command.json` with route `home-fixture` and launched the installed app.
  - `dev-command-result.txt` reported `completed / home-fixture`.
  - UIA text confirmed Home, Media Libraries, Hot Movies, Hot TV Series, Douban Top Rated, Netflix, Continue watching, and More Hot Movies were present.
  - Using keyboard input only, focus moved from `Play` to `Hot Movies`, then horizontally to `Hot TV Series` and `Douban Top Rated`, then down to `Aurora Protocol` in Continue watching.
  - Screenshots were captured from the UWP `ApplicationFrameHost` window after minimizing unrelated same-title/Codex windows: top Play focus, Media Libraries focus, Douban focus, Continue watching focus, and a lower-row view.
  - No app-content mouse clicks were used.
- Limitation:
  - The fixture proves Home layout, focus, screenshot capture, and section coverage without a live Emby session. Real server artwork still needs a saved-session pass.
  - The generated QA artwork is intentionally local and repeatable, but it currently reads very dark under the Home card washes. A later visual polish pass should tune artwork visibility while preserving text contrast.

### 2026-07-06 - Home Wide Artwork Visibility Polish

- App version: 0.1.0.156.
- Scope: make Home media-library and server-section cards consume their own wide artwork in the deterministic fixture route, and move artwork visibility controls into shared Matte Cinema Fluent resources.
- Root cause:
  - `CreateLibraryArtworkBrush` and `CreateHomeSectionArtworkBrush` returned `null` when `_client` or `_session` was absent.
  - That was correct for live Emby URL construction, but wrong for the DEBUG `home-fixture` route because fixture artwork is resolved from the packaged `Assets/QaHome` URI map before a live client is needed.
  - The earlier dark-card screenshot therefore combined two issues: fixture cards were falling back too often, and the wide-card image treatment was still hard-coded.
- Visual/token changes:
  - Added `TvHomeLibraryArtworkOpacity`, `TvHomeSectionArtworkOpacity`, and `TvHomeWideCardTextScrimHeight` resources.
  - Reduced `library_artwork_wash` and `section_artwork_wash` so artwork carries more of the card surface.
  - Replaced the Home wide-card bottom text band with a transparent-to-scrim gradient.
  - Regenerated local QA artwork with a lighter global scrim so screenshots represent visible media artwork instead of nearly black placeholders.
- Automated verification:
  - TDD red path confirmed Home wide-card artwork treatment was not tokenized before implementation.
  - TDD red path confirmed the QA artwork generator still used the old heavy global scrim.
  - TDD red path confirmed fixture artwork was blocked by live `_client/_session` checks.
  - Targeted design/fixture/accessibility tests passed: 10 total.
  - App Debug x64 build passed, producing `NextGenEmby.App_0.1.0.156_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.156`.
- Keyboard-only validation:
  - Wrote `dev-command.json` with route `home-fixture` and launched the installed app.
  - `dev-command-result.txt` reported `completed / home-fixture`.
  - Using keyboard input only, focus moved from `Play` to `Hot Movies`, then horizontally to `Hot TV Series` and `Douban Top Rated`, then down to `Aurora Protocol`.
  - Screenshots were captured from the UWP `ApplicationFrameHost` window under `C:\Users\yqzzx\AppData\Local\Temp\nextgenemby-home-fixture-156-shots`.
  - The media library rail now shows the fixture wide artwork on library/section cards instead of mostly empty matte surfaces.
  - No app-content mouse clicks were used.

### 2026-07-06 - Search Error Recovery Fixture

- App version: 0.1.0.159.
- Scope: turn Search server-failure recovery into a deterministic keyboard regression route while the Xbox and saved Emby session are unavailable.
- Interaction changes:
  - `dev-command.json` accepts route `search-error`.
  - The DEBUG route navigates to Search, fills `Aurora Protocol`, and renders the existing `Unable to search` recovery panel with `Edit search` and `Search again`.
  - SearchBox now handles `Down` itself so TextBox editing behavior cannot trap D-pad navigation before the selected scope.
  - The Search error-state actions now handle `Left`/`Right` between `Edit search` and `Search again`.
- Automated verification:
  - TDD red path confirmed `search-error` was not accepted before route support.
  - TDD red path confirmed the Search error route did not render a deterministic keyboard recovery state before implementation.
  - Computer Use found two real focus bugs during validation; TDD red paths then covered query `Down` routing and error-action left/right routing before fixes.
  - Core tests passed: 340 total.
  - App Debug x64 build passed, producing `NextGenEmby.App_0.1.0.159_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.159`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `search-error` and launched the installed app.
  - `dev-command-result.txt` reported `completed / search-error`.
  - Initial Search state showed `Aurora Protocol`, `Unable to search`, recovery copy, `Edit search`, and `Search again`, with focus on the query box.
  - Pressed `Down`; visual focus moved from the query box to the selected `All` scope.
  - Pressed `Down`; visual focus moved to `Edit search`.
  - Pressed `Right`; visual focus moved to `Search again`.
  - Pressed `Return`; the deterministic retry re-rendered the error state and returned focus to the query box.
  - Repeated `Down`, `Down`, `Right`, `Left`; visual focus returned from `Search again` to `Edit search`.
  - No app-content mouse clicks were used.
- Tooling note:
  - UI Automation `focused_element` intermittently continued to report `SearchBox` after D-pad moves, while screenshots and actual button activation showed the visible TV focus correctly moved. This run therefore used Computer Use screenshots as the authority for focus position.

### 2026-07-06 - Search Results Scope Fixture

- App version: 0.1.0.161.
- Scope: add a deterministic Search results route that covers every TV search scope and stress-tests the far-right scope rail without a saved Emby session.
- Interaction changes:
  - `dev-command.json` accepts route `search-fixture`.
  - The DEBUG route renders Search with `Aurora Protocol` and representative results for Movie, Series, Episode, Video, MusicVideo, BoxSet, Playlist, Person, MusicAlbum, Audio, Photo, and TvChannel.
  - Search scope buttons call `StartBringIntoView` on focus so far-right scopes stay visible during D-pad navigation.
  - Search result status text now uses `1 result` for singular counts and `results` otherwise.
- Automated verification:
  - TDD red path confirmed `search-fixture` was not accepted before route support.
  - TDD red path confirmed `DevelopmentSearchFixture` did not exist before fixture data was implemented.
  - TDD red path confirmed the Search page did not render fixture results or keep scope focus visible before implementation.
  - Computer Use visual validation caught `1 results / Live TV`; a TDD red path then added `SearchResultStatusTextPolicy` before the text fix.
  - Core tests passed: 348 total.
  - App Debug x64 build passed, producing `NextGenEmby.App_0.1.0.161_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.161`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `search-fixture` and launched the installed app.
  - `dev-command-result.txt` reported `completed / search-fixture`.
  - Initial Search state showed `12 results / All` with fixture cards including `Aurora Protocol`, `Polar Archive`, `Signal Room`, `Night City Collection`, `Weekend Queue`, `Maya Chen`, `Nocturne Signals`, `Opening Credits`, `Neon Lobby Still`, and `News 24`.
  - Pressed `Down`, then nine `Right` presses; focus moved to far-right `Live TV` and the scope rail scrolled to keep it visible.
  - Pressed `Return`; the fixture filtered to `1 result / Live TV` and showed `News 24`.
  - Pressed `Down`; focus moved from `Live TV` to the `News 24` result card.
  - Pressed `Up`; focus returned to `Live TV`.
  - Pressed `Left`; focus moved from `Live TV` to `Photos`.
  - No app-content mouse clicks were used.
- Limitation:
  - The fixture currently uses initials-only fallback cards. A later visual pass should add packaged QA artwork for Search results so this route can validate poster/thumbnail treatment as well as focus. This was addressed in the 0.1.0.162 pass below.

### 2026-07-06 - Search Fixture Artwork Polish

- App version: 0.1.0.162.
- Scope: remove the initials-only visual fallback from the deterministic Search fixture route by giving every representative result a packaged QA poster image.
- Visual/data changes:
  - `DevelopmentSearchFixture` now exposes an artwork URI map using the same packaged `Assets/QaHome` artwork pipeline as the Home fixture.
  - Each fixture item now carries `PrimaryImageTag` and `PrimaryImageItemId`, so the fixture resembles normal Emby media item image metadata instead of UI-only mock data.
  - `SearchPage` resolves DEBUG fixture artwork through `CreateDevelopmentArtworkImageSource(item)` and still falls back cleanly if a fixture asset is missing.
- Automated verification:
  - TDD red path confirmed `DevelopmentSearchFixture` did not expose `CreateArtworkUris()` or `ArtworkKey()` before the change.
  - TDD red path confirmed Search UI source did not consume development artwork before implementation.
  - Targeted Search fixture/source tests passed: 8 total.
  - Core tests passed: 349 total.
  - `git diff --check` passed; only line-ending normalization warnings were reported.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.162_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.162`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `search-fixture` and cold-launched the installed app.
  - Initial Search fixture state showed `12 results / All` and every visible card rendered packaged QA artwork instead of initials-only fallback.
  - Pressed `Down`; focus moved from the query box to the selected `All` scope.
  - Pressed nine `Right` keys; focus moved to far-right `Live TV` and the scope rail scrolled to keep it visible.
  - Pressed `Return`; the fixture filtered to `1 result / Live TV` and showed `News 24` with packaged QA artwork.
  - Pressed `Down`; focus moved to the `News 24` card with a clear TV focus rectangle.
  - Pressed `Up`; focus returned to `Live TV`.
  - Pressed `Left`; focus moved from `Live TV` to `Photos`.
  - No app-content mouse clicks were used.
- Tooling note:
  - The first launch attempt only activated an already-running login window, so the DEBUG command was not re-read. The validation run killed the existing app process, rewrote `dev-command.json`, and cold-launched 0.1.0.162 before capturing screenshots.

### 2026-07-07 - Details Fixture Below-Fold Route

- App version: 0.1.0.166.
- Scope: add a deterministic Details route for media versions, organize sheets, similar items, and cast/crew validation while the Xbox and saved Emby session are unavailable.
- Interaction/data changes:
  - `dev-command.json` accepts route `details-fixture`.
  - The route opens `Aurora Protocol` through the normal `MediaDetailsPage` path with fixture playback versions, ancestors, collection targets, playlist targets, similar items, people, and packaged QA artwork.
  - Details fixture add-to sheets update local fixture ancestors and restore focus without calling live Emby mutation APIs.
  - Media Details now uses a `MediaDetails` shell content mode: Guide remains visible, but normal navigation prefers the Details content default focus instead of the left Guide rail.
  - Selected media versions no longer reuse the focus border color. The selected version uses a warm internal status bar; the cyan outer focus rectangle is reserved for the current keyboard/controller focus.
- Automated verification:
  - TDD red path confirmed `details-fixture` was not accepted before route support.
  - TDD red path confirmed `DevelopmentDetailsFixture` was missing before fixture data was implemented.
  - TDD red path confirmed the Details fixture source contract did not cover below-fold rails, fixture artwork, add-to sheets, or deferred content focus before implementation.
  - Computer Use caught a real cold-launch focus bug: 0.1.0.163/0.1.0.165 could land on the Guide Home button instead of `Resume`.
  - TDD red path confirmed `ShellContentMode.MediaDetails` did not exist before the shell focus-policy fix.
  - TDD red path confirmed selected version styling reused the focus border color before the internal status-bar fix.
  - Core tests passed: 356 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.166_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.166`.
- Keyboard-only validation with Computer Use:
  - Killed any running app process, wrote `dev-command.json` with route `details-fixture`, and cold-launched the installed app.
  - Initial Details state showed `Aurora Protocol`, `Resume`, two Versions, Organize actions, More like this, and Cast & crew. Focus landed on `Resume`, not the Guide rail.
  - Pressed `Down`; focus moved to the first version. The first version showed both the cyan focus rectangle and the warm selected-version status bar.
  - Pressed `Down`, `Enter`; the second version `1080p fallback` became selected with the warm status bar and the app stayed on Details instead of launching playback.
  - Pressed `Down` to `Add to collection`, `Enter` to open the sheet, `Down` to `Signal Archives`, and `Enter` to confirm. The sheet closed, focus restored, and status changed to `Added to fixture collection: Signal Archives`.
  - Pressed `Right` to `Add to playlist`, `Enter` to open the playlist sheet, and `Escape` to close it. Focus restored to `Add to playlist`.
  - Pressed `Down` to More like this, then `Down` to Cast & crew. Focus stayed visible and the viewport scrolled predictably.
  - No app-content mouse clicks were used.
- Follow-up:
  - Re-run against a real saved Emby session when available: multi-version server item, similar-items server response, and live add-to success on disposable collection/playlist targets.

### 2026-07-07 - Playback Options Fixture Route

- App version: 0.1.0.173.
- Scope: add deterministic playback More drawer Source/Audio/Subtitles/Info validation without touching native decoding or Emby transcode paths.
- Interaction changes:
  - `dev-command.json` accepts route `playback-options-fixture`.
  - The route renders `Aurora Protocol` directly on `PlaybackPage`, opens the More drawer, pins the overlay, and exposes fixture sources, audio tracks, subtitles, and Info.
  - Source/audio/subtitle fixture changes update local playback status/info only and do not call backend/orchestrator stream switching.
  - Collapsed Source/Audio/Subtitles combo boxes route Up/Down to drawer focus. Only expanded dropdowns use Up/Down to change values.
  - Info toggle preserves Info focus; opening More clears transport focus visuals.
- Automated verification:
  - TDD red paths confirmed missing route/fixture/source contract, combo directional policy, `ProcessKeyboardAccelerators` binding, replay guard, Info focus preservation, and transport focus clearing before implementation.
  - Core tests passed: 365 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.173_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.173`.
- Keyboard-only validation with Computer Use:
  - Killed any running app process, wrote `dev-command.json` with route `playback-options-fixture`, and cold-launched 0.1.0.173.
  - Initial More drawer focus landed on Source with `4K Direct · 3840x2160`.
  - Pressed `Down`; focus moved Source -> Audio without changing Source.
  - Pressed `Enter`, `Down`, `Enter`; Audio changed to `Japanese AAC Stereo`.
  - Pressed `Down`; focus moved Audio -> Subtitles without changing Audio.
  - Pressed `Enter`, `Down`, `Enter`; Subtitles changed to `English SDH External`.
  - Pressed `Down`, `Enter`; Info opened and showed State, Item, Source, Audio, Subtitles, Position, and Fixture fields. Focus stayed on Info.
  - Pressed `Escape`; More closed and focus returned to the transport More button.
  - Pressed `M`; More reopened with Source as the only strong focus, with no simultaneous transport More highlight.
  - No app-content mouse clicks were used.
- Bugs caught/fixed:
  - Collapsed ComboBox `Down` changed Source before moving focus.
  - Early ComboBox handling and CoreWindow handling double-processed one `Down`, skipping Audio.
  - Info toggle reset focus to Source.
  - Reopening More showed simultaneous Source and transport More focus.
- Follow-up:
  - Fixture validates UI/controller flow without native stream switching. A future saved-session/media pass should verify live multi-audio/subtitle switching against real Emby items.

### 2026-07-07 - Details Favorite And Watched Fixture Toggles

- App version: 0.1.0.175.
- Scope: verify Details favorite/watched actions with keyboard/controller input using the deterministic `details-fixture` route, without mutating live Emby user data.
- Interaction changes:
  - Added a core `MediaDetailsUserDataTogglePolicy` so favorite and watched toggles flip one field while preserving resume position, played percentage, and the other user-data state.
  - `details-fixture` now handles Favorite/Watched locally and updates button labels/status without calling `SetFavoriteAsync` or `SetPlayedAsync`.
  - Details fixture default focus now uses a short Low-priority retry loop. Computer Use caught the old single-dispatch focus path landing on the collapsed Guide Home button, so `Right`, `Right`, `Return` navigated Home instead of activating Add favorite.
- Automated verification:
  - TDD red path confirmed `MediaDetailsUserDataTogglePolicy` did not exist before implementation.
  - TDD red path confirmed the Details fixture source did not contain the local user-data toggle path.
  - TDD red path confirmed Details fixture focus did not use Low-priority retry before the cold-launch focus fix.
  - Core tests passed: 369 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.175_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.175`.
- Keyboard-only validation with Computer Use:
  - Killed any running app process, wrote `dev-command.json` with route `details-fixture`, and cold-launched 0.1.0.175.
  - Initial Details state showed `Aurora Protocol`; the true TV focus landed on `Resume` and the collapsed Guide Home button was not focused.
  - Pressed `Right`, `Right`, `Return`; `Add favorite` changed to `Remove favorite`, status changed to `Fixture favorite added.`, and focus stayed on the Favorite button.
  - Pressed `Right`, `Return`; `Mark watched` changed to `Mark unwatched`, status changed to `Fixture marked watched.`, and focus stayed on the Watched button.
  - Pressed `Return`; Watched toggled back to `Mark watched` with `Fixture marked unwatched.`.
  - Pressed `Left`, `Return`; Favorite toggled back to `Add favorite` with `Fixture favorite removed.`.
  - No app-content mouse clicks were used.
- Follow-up:
  - Re-run live favorite/watched toggles only on a disposable item or fixture user after the saved Emby session is restored.

### 2026-07-07 - Music Positive Browse Fixture Route

- App version: 0.1.0.177.
- Scope: add and verify a deterministic positive Music browse route while the current Emby server/session does not expose real `MusicAlbum` or `Audio` rows.
- Interaction changes:
  - `dev-command.json` accepts route `music-fixture`.
  - The route opens Music with packaged QA album/song artwork, 3 fixture albums, 6 fixture songs, and the existing Albums/Songs/Preview three-column layout.
  - Album activation filters Songs locally and keeps the route browse-only; song activation opens the existing `Music playback unavailable` layer.
  - Music now tracks album/song buttons explicitly and handles D-pad/arrow Up/Down/Left/Right in the page instead of relying on default UWP XY focus. This fixes the bug where `Down` stayed on the first song and `Return` activated the wrong item.
  - Closing the unsupported music layer restores focus to the song that opened it.
- Automated verification:
  - TDD red path confirmed `music-fixture` was not accepted before route support.
  - TDD red path confirmed `DevelopmentMusicFixture` did not exist before fixture data was implemented.
  - TDD red path confirmed Music page source did not render the positive fixture route before implementation.
  - Computer Use caught the real list-movement bug; TDD then added `MusicListFocusPolicy` before the explicit page focus fix.
  - Core tests passed: 383 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.177_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.177`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `music-fixture` and launched the installed app.
  - `dev-command-result.txt` reported `completed / music-fixture`.
  - Initial Music state showed `Fixture music library`, 3 albums, 6 songs, and focus on `Nocturne Signals`.
  - Pressed `Down`; focus moved to `City Lights Archive`, and the Preview pane updated to that album.
  - Pressed `Return`; Songs filtered to `City Lights Archive`, showed 2 songs, showed the `All` action, and focused `Late Train Window`.
  - Pressed `Down`, `Return`; focus moved to `Rooftop Weather`, and the `Music playback unavailable` layer opened for `Rooftop Weather` with `Close` focused.
  - Pressed `Escape`; the layer closed and focus returned to `Rooftop Weather`.
  - Pressed `Up`, `Up`; focus moved to `All`. Pressed `Return`; the full 6-song list returned with `Opening Credits` focused.
  - Pressed `Left`; focus moved back to `Nocturne Signals`. Pressed `Right`; focus returned to `Opening Credits`.
  - No app-content mouse clicks were used.
- Follow-up:
  - Re-run Music against a real saved Emby session with albums/songs after session restore, then add artist/album-artist hierarchy if the server exposes usable artist metadata.

### 2026-07-07 - Home Server Sections Artwork Rail

- App version: 0.1.0.179.
- Scope: make server-configured Emby home sections first-class TV entrances, separate from media-library cards, with section-owned wide artwork preferred over child poster fallbacks.
- Interaction/data changes:
  - `GetHomeSectionsAsync` now requests image metadata and maps section-owned `Thumb`, `Backdrop`, `Banner`, `Primary`, and `Logo` image fields.
  - Home renders a dedicated `Server sections` rail between `Media Libraries` and content/status rows.
  - `EmbyArtworkPolicy.SelectHomeSectionWideArtwork` now prefers section-owned wide images before falling back to `ParentItem` artwork.
  - Home focus policy now treats `Server sections` as its own controller stop: Hero -> Media Libraries -> Server sections -> content rows.
  - DEBUG `home-fixture` section requests carry fixture items/artwork into Library, so local validation no longer depends on a saved Emby session.
- Automated verification:
  - TDD red paths confirmed HomeSections did not request/map image metadata, section-owned artwork was ignored, and Home had no dedicated section focus zone before implementation.
  - Computer Use caught a real fixture bug where opening `Douban Top Rated` showed `Sign in first`; source tests then drove the fixture handoff fix.
  - Core tests passed: 395 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.179_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.179`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `home-fixture` and cold-launched the installed app.
  - Initial Home showed `Play` focused on the hero, a `Media Libraries` rail, and a distinct `Server sections` rail with Hot Movies, Hot TV Series, Douban Top Rated, and Netflix cards using packaged wide artwork.
  - Pressed `Down`; focus moved from hero `Play` to the first Media Libraries card.
  - Pressed `Down`; focus moved to the first Server sections card.
  - Pressed `Right`, `Right`; focus moved across Server sections to `Douban Top Rated` while the rail kept the focused card visible.
  - Pressed `Return`; Library opened as `Douban Top Rated` with 7 fixture items and artwork-backed cards.
  - Pressed `Escape`; Home restored focus to the originating `Douban Top Rated` Server sections card.
  - No app-content mouse clicks were used.
- Follow-up:
  - Re-run the same rail against real server-provided section artwork when the saved Emby session is restored.

### 2026-07-07 - Photos Positive Browse Fixture Route

- App version: 0.1.0.182.
- Scope: add and verify a deterministic positive Photos browse route while the current saved Emby session/server route only exposes the Photos empty state.
- Interaction changes:
  - `dev-command.json` accepts route `photos-fixture`.
  - The route opens the normal Library page as `Photos` with `Photo,Folder` item support, packaged QA artwork, root photos, and a root album.
  - Folder activation opens a nested Photos Library request with the same fixture item/artwork set and filters by `ParentId`.
  - Photo activation opens the immersive Photo viewer with a DEBUG-only packaged image URI so local validation does not require a saved Emby session.
  - Library back navigation stores the originating item on the navigation request and restores focus to that item after returning from the Photo viewer.
- Automated verification:
  - TDD red paths confirmed `DevelopmentPhotosFixture`, route support, `BrowseFolder` activation, Photo viewer image handoff, and `photos-fixture` source handling did not exist before implementation.
  - Computer Use caught a real focus restoration bug: after opening `Blue Crossing` and pressing `Escape`, focus initially reset to the first album item instead of the originating photo. A second TDD red path moved restore state onto `LibraryNavigationRequest`, after which focus returned to `Blue Crossing`.
  - Core tests passed: 407 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.182_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.182`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `photos-fixture` and launched the installed app.
  - `dev-command-result.txt` reported `completed / photos-fixture`.
  - Initial Photos state showed `Photos`, `3 items`, `Night Market`, `Rooftop After Rain`, and `Window Seat`, with focus on `Night Market`.
  - Pressed `Return`; `Night Market` opened as a nested album with `4 items`.
  - Pressed `Right`, `Right`; visible focus moved to `Blue Crossing`.
  - Pressed `Return`; the immersive Photo viewer opened for `Blue Crossing`, showed `Photo`, rendered the packaged image, and did not show `Sign in first` or `Photo unavailable`.
  - Pressed `Escape`; the app returned to the `Night Market` album and restored focus to `Blue Crossing`.
  - Pressed `Escape` again; the app returned to the root Photos grid with `Night Market` focused.
  - No app-content mouse clicks were used.
- Follow-up:
  - Re-run Photos against a real saved Emby session with server-provided photo folders and image URLs, then decide whether Photos needs a dedicated album-first layout instead of the generic Library grid.

### 2026-07-07 - Live TV Positive Browse Fixture Route

- App version: 0.1.0.184.
- Scope: add and verify a deterministic positive Live TV browse route while the current local validation path does not expose real Live TV channels.
- Interaction changes:
  - `dev-command.json` accepts route `livetv-fixture`.
  - The route opens the normal Live TV page with four fixture channels, current-program metadata, and packaged channel artwork.
  - Channel activation remains browse-only and opens the existing `Live TV playback unavailable` layer; it does not start live streams and does not touch the native decoding path.
  - Live TV now tracks channel buttons explicitly and handles Up/Down keys on the page so D-pad style movement remains deterministic.
  - Closing the unsupported layer restores focus to the channel that opened it.
- Automated verification:
  - TDD red path confirmed `DevelopmentLiveTvFixture` and `livetv-fixture` route acceptance did not exist before implementation.
  - TDD red path confirmed the Live TV page did not render a positive fixture route, consume fixture artwork, or restore invoking-channel focus before implementation.
  - Computer Use caught the real channel-list movement bug: `Down` initially did not leave the first channel. Source tests then drove the explicit `_channelButtons` movement path through `MusicListFocusPolicy`.
  - Core tests passed: 414 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.184_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.184`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `livetv-fixture` and launched the installed app.
  - `dev-command-result.txt` reported `completed / livetv-fixture`.
  - Initial Live TV state showed `Fixture Live TV guide`, `101 News 24`, `202 Cinema One`, `303 Match Center`, and `404 Kids Studio`, with focus on `101 News 24`.
  - Pressed `Down`; focus moved to `202 Cinema One`, and the Now preview updated to `Matinee Window`.
  - Pressed `Down`, `Down`; focus moved through the list to `404 Kids Studio`, and the Now preview updated to `Saturday Workshop - Paper City`.
  - Pressed `Return`; the `Live TV playback unavailable` layer opened for `Kids Studio` with `Close` focused.
  - Pressed `Escape`; the layer closed and focus returned to `404 Kids Studio`.
  - Pressed `Up`; focus moved to `303 Match Center`, and the Now preview updated to `Late Match - Quarter Final`.
  - No app-content mouse clicks were used.
- Follow-up:
  - Re-run Live TV against a real Live TV-enabled Emby server after session restore. Keep live stream playback and Emby transcoding outside this fixture route until they become explicit goals.

### 2026-07-07 - Collections And Playlists Positive Fixture Routes

- App version: 0.1.0.185.
- Scope: add and verify deterministic positive browse routes for organization libraries while keeping the existing Library grid and Matte Cinema Fluent card treatment.
- Interaction changes:
  - `dev-command.json` accepts routes `collections-fixture` and `playlists-fixture`.
  - `BoxSet` and `Playlist` now activate as browseable Library containers instead of opening dead-end Details routes.
  - Organization child pages use a media-child include set so collection/playlist contents can show Movie, Series, Episode, Video, MusicVideo, Audio, and Photo child items.
  - Library back navigation keeps the originating root card and restores focus after returning from the nested collection or playlist.
- Automated verification:
  - TDD red path confirmed `DevelopmentLibraryOrganizationFixture`, `collections-fixture`, and `playlists-fixture` did not exist before implementation.
  - TDD red path confirmed `BoxSet` and `Playlist` still routed to Details before the activation policy update.
  - Targeted fixture/route/source/activation tests passed: 46 total.
  - Core tests passed: 421 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.185_x64_Debug.msix`.
  - The first signing attempt used an untrusted same-subject certificate and install failed with `0x800B0109`; root-cause check found the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, re-signed the package, and installed `NextGenEmby.App 0.1.0.185`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `collections-fixture` and cold-launched the installed app.
  - `dev-command-result.txt` reported `completed / collections-fixture`.
  - Initial Collections state showed `Collections`, `2 items`, `Signal Archives`, and `City Nights`, with focus on `Signal Archives`.
  - Pressed `Return`; `Signal Archives` opened as a nested Library with `4 items`: `Aurora Protocol`, `Midnight Signal`, `Afterimage`, and `Quiet Orbit`.
  - Pressed `Right`; focus moved from `Aurora Protocol` to `Midnight Signal`.
  - Pressed `Escape`; the app returned to root Collections and restored focus to `Signal Archives`.
  - Wrote `dev-command.json` with route `playlists-fixture` and cold-launched the installed app.
  - Initial Playlists state showed `Playlists`, `2 items`, `Weekend Queue`, and `Documentary Stack`, with focus on `Weekend Queue`.
  - Pressed `Return`; `Weekend Queue` opened as a nested Library with `5 items`: `Northline S1:E4`, `Room Tone S2:E1`, `Ocean Archive`, `Sound Room`, and `Room Tone`.
  - Pressed `Right`, `Right`; focus moved to `Ocean Archive`, and long episode metadata stayed clipped within its card.
  - Pressed `Escape`; the app returned to root Playlists and restored focus to `Weekend Queue`.
  - No app-content mouse clicks were used.
- Follow-up:
  - Re-run Collections and Playlists against a real saved Emby session. If real playlist children do not resolve through `ParentId` on the target server, add a dedicated playlist-items API path while keeping this fixture route as a controller regression.

### 2026-07-07 - Playlist Items Endpoint Route

- App version: 0.1.0.186.
- Scope: replace the remaining real-server playlist child loading risk with Emby's dedicated playlist item endpoint while keeping the existing Playlists fixture route and Library grid interaction intact.
- Interaction changes:
  - `EmbyApiClient.GetPlaylistItemsAsync` uses `GET /Playlists/{Id}/Items` with `UserId`, `Limit`, standard item fields, and image metadata.
  - Nested playlist Library requests now carry `ContainerItemType`, so `Playlist` children use the playlist endpoint instead of a generic `ParentId` query.
  - Playlist child pages are treated as read-only ordered sequences: Sort/Filter are hidden, and Up from the first row routes to Refresh instead of exposing controls that the endpoint does not support.
- Automated verification:
  - TDD red path confirmed `GetPlaylistItemsAsync` did not exist before implementation.
  - TDD red path confirmed LibraryPage lacked `ContainerItemType`, `IsPlaylistRequest`, and `GetPlaylistItemsAsync` routing before implementation.
  - TDD red path confirmed read-only sequence requests did not hide Sort/Filter before implementation.
  - Targeted playlist endpoint, source, fixture, and activation tests passed: 15 total.
  - Core tests passed: 424 total.
  - `git diff --check` passed with only LF/CRLF working-copy warnings.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.186_x64_Debug.msix`.
  - First install attempt failed with `0x800B0100` because the package was not signed. Root-cause check showed `Get-AuthenticodeSignature` was `NotSigned`; signing with trusted `CN=NextGenEmby` thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0` produced `Status: Valid`, then install succeeded as `NextGenEmby.App 0.1.0.186`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `playlists-fixture` and cold-launched the installed app.
  - `dev-command-result.txt` reported `completed / playlists-fixture`.
  - Initial Playlists state showed `Playlists`, `2 items`, `Weekend Queue`, and `Documentary Stack`, with focus on `Weekend Queue`.
  - Pressed `Return`; `Weekend Queue` opened as a nested playlist page with `5 items`: `Northline S1:E4`, `Room Tone S2:E1`, `Ocean Archive`, `Sound Room`, and `Room Tone`.
  - Verified the nested playlist page hides Sort/Filter and keeps only the Refresh action in the toolbar, matching the ordered playlist endpoint behavior.
  - Pressed `Right`, `Right`; visible focus moved to `Ocean Archive` without card resizing or label overlap.
  - Pressed `Escape`; the app returned to root Playlists and restored focus to `Weekend Queue`.
  - No app-content mouse clicks were used. Computer Use required window rebinds after two capture/foreground metadata hiccups, but all app interactions were completed through keyboard/controller-mapped input.
- Follow-up:
  - Re-run a real saved Emby session where playlist items are not reliably returned by `ParentId`, and verify the live endpoint returns mixed video/audio playlist children with the same focus behavior.

### 2026-07-07 - Details Metadata Facet Browse

- App version: 0.1.0.190.
- Scope: make Details metadata actionable for couch navigation by rendering Genre, Studio, and Tag chips in an `Explore` rail, then opening Library result pages filtered to the selected facet.
- Interaction changes:
  - `EmbyApiClient.GetItemAsync` now requests and maps `GenreItems`, `Studios`, `TagItems`, and string-only `Tags` fallback into typed item references.
  - `EmbyItemsQuery` and `LibraryNavigationQuery` now carry `Genres`, `GenreIds`, `Studios`, `StudioIds`, and `Tags` so Details chips can open filtered Library pages.
  - Details renders focusable metadata chips below `More like this`; Genre/Studio chips prefer ids when available and fall back to names for less compatible Emby servers.
  - Back navigation stores the originating metadata chip by item id, so a rebuilt Details page restores focus to the chip instead of jumping to `Resume`.
- Automated verification:
  - TDD red paths confirmed metadata DTO mapping, `/Items` facet query serialization, Details chip rendering/navigation, and backstack focus restoration did not exist before implementation.
  - Computer Use found a real focus bug where `Escape` from `Genre: Sci-Fi` returned to `Resume`; the fix moved metadata focus restoration into a cross-instance pending map and suppressed default focus while restore is pending.
  - Core tests passed: 429 total.
  - `git diff --check` passed with only LF/CRLF working-copy warnings.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.190_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.190`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `details-fixture` and cold-launched the installed app.
  - Initial Details state showed `Aurora Protocol` with focus on `Resume`.
  - Pressed `Down` five times; focus moved to `Explore` and landed on `Genre / Sci-Fi`.
  - Pressed `Return`; Library opened as `Genre: Sci-Fi` with 15 fixture items and focus on `Aurora Protocol`.
  - Pressed `Escape`; Details returned to the `Explore` rail with focus restored to the originating `Sci-Fi` chip, not the top `Resume` action.
  - No app-content mouse clicks were used.
- Follow-up:
  - Re-run against a real saved Emby session to verify server-specific `GenreItems`, `Studios`, `TagItems`, and `Tags` shapes plus live `/Items` facet filtering.

### 2026-07-07 - Login Failure Recovery And Guide Focus Contrast

- App version: 0.1.0.195.
- Scope: verify failed login recovery with controller-mapped keyboard input while separating passive Guide active-route state from true controller focus.
- Interaction/design changes:
  - `dev-command.json` accepts route `login`, allowing deterministic local validation of Login without clearing real saved session state.
  - `docs/DESIGN.md` now defines `colors.guide_active_border`; `App.xaml` exposes `AppGuideActiveBorderColor` and `AppGuideActiveBorderBrush`.
  - Guide active route uses `AppGuideActiveBorderBrush` instead of `AppAccentBrush`, keeping cyan reserved for true focus.
  - `LoginPage` implements `ITvContentFocusTarget`, and `ShellContentMode.Login` makes normal Login navigation prefer content focus over the Guide rail.
  - Failed login returns focus to the relevant editable field: username/password validation errors target their field, while connection/auth failures return to Server URL.
  - Login controls register `KeyDownEvent` with `handledEventsToo: true`, so D-pad-style Up/Down moves through Server URL -> Username -> Password -> Connect even when a TextBox would otherwise consume the arrow key.
- Automated verification:
  - TDD red path confirmed `login` was not an accepted development route before implementation.
  - TDD red path confirmed Guide active state reused `AppAccentBrush` and had no dedicated active-route design token before implementation.
  - Computer Use found the real focus ambiguity where Guide Home looked focused while Server URL had true focus; the active-route brush was split from the focus brush.
  - Computer Use found the real Login recovery bug where focus could move to Guide Home after a failed retry; `ShellContentMode.Login` and `ITvContentFocusTarget` fixed default Login focus.
  - Computer Use found the real TextBox directional bug where `Down` stayed inside Server URL; `handledEventsToo` key routing fixed D-pad-style form movement.
  - Core tests passed: 434 total.
  - `git diff --check` passed with only LF/CRLF working-copy warnings.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.195_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.195`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `login` and `dev-login.json` with invalid credentials, then cold-launched the installed app.
  - Initial state showed Login failure text, Server URL focused, and Guide Home shown only as passive active route without cyan focus.
  - Pressed `Down`; focus moved from Server URL to Username, proving TextBox arrow-key capture works.
  - Pressed `Up`, `Ctrl+A`, typed `http://127.0.0.1:8`, then pressed `Down`, `Down`, `Down`, `Return`.
  - The retry failed as expected and focus returned to Server URL with the edited URL still visible.
  - No app-content mouse clicks were used. `dev-login.json` and `dev-command.json` were removed from LocalState after validation.
- Follow-up:
  - Re-run Login from a deliberately cleared real session before release packaging, then decide whether the Login page should get denser TV-mode copy or a server-discovery affordance.

### 2026-07-07 - Player Status Aperture App Icon Refresh

- App version: 0.1.0.196.
- Scope: refine the app identity away from brand text, initials, and miniature UI screenshots while keeping the Matte Cinema Fluent player vocabulary.
- Design alignment:
  - `docs/DESIGN.md`, the complete-client design plan, the app-icon refresh plan, and `docs/icon-concepts/README.md` now define **Player Status Aperture** as the production icon direction.
  - The generator uses reusable player-property primitives: `Draw-FocusPath`, `Draw-PlaybackCore`, and `Draw-ProgressBase`.
  - Store, square, wide, and splash assets reuse the same centered aperture symbol: compact playback viewport, cyan controller focus path, green play/confirm core, and flat amber progress base.
  - Removed the prior tiny subtitle/audio marks and wide-tile pseudo UI stack because they became too small and screenshot-like at 44 px.
- Automated verification:
  - TDD red path confirmed the new `Player Status Aperture` contract did not exist before implementation.
  - Targeted icon contract tests passed: 4 total.
  - Core tests passed: 435 total.
  - `git diff --check` passed with only LF/CRLF working-copy warnings.
  - Regenerated `StoreLogo.png`, `Square44x44Logo.png`, `Square150x150Logo.png`, `Wide310x150Logo.png`, and `SplashScreen.png` from `tools/Generate-AppIconAssets.ps1`.
  - Opened and inspected the generated 44 px, 150 px, wide, and splash PNGs locally; the 44 px mark preserves focus/play/progress, and the wide tile now reads as the same centered player-property symbol instead of a banner or page layout.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.196_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.196`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `home-fixture`, launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`, and confirmed the Home fixture rendered with Media Libraries, Server sections, and Continue watching.
  - Pressed `Down`, `Right`, `Right`; focus moved from the hero area into the Media Libraries rail and landed on `Douban Top Rated` without mouse clicks.
  - Cleared the process and cold-launched the installed app again; Windows process inspection confirmed the running executable path was `NextGenEmby.App_0.1.0.196_x64__h8qjz0sr1sg4m`.
  - On the Login page, pressed `Down`, `Down`, `Up`; the final screenshot showed focus on Username, proving the D-pad-style TextBox movement still works after the asset/package refresh.
  - Computer Use `get_window_state` intermittently returned a stale `0.1.0.162` path in its window metadata even while `list_windows` and Windows process inspection both reported `0.1.0.196`; process/package verification is the source of truth for this pass.
  - No app-content mouse clicks were used. `dev-command.json` and `dev-command-result.txt` were removed from LocalState after validation.
- Follow-up:
  - When Xbox hardware is available again, inspect the Start tile and splash surface on-console so the new centered wide tile can be judged in the real shell, not only the generated PNG and local package.

### 2026-07-07 - Home Rail Card Token And Focus Polish

- App version: 0.1.0.197.
- Scope: make the Home rails feel less assembled by hand by promoting repeated card measurements and focus treatment into shared skin resources.
- Interaction/design changes:
  - `App.xaml` now owns Home rail spacing, wide-card dimensions, poster-card dimensions, Home card title/meta type sizes, card radii, scrim padding, and `TvHomeFocusedCardScale`.
  - `HomePage.xaml` consumes Home spacing tokens for page sections, rail headers, rail card gaps, and row gaps.
  - `HomePage.xaml.cs` consumes Home card metrics through resource helpers instead of hardcoded library-card, section-card, and poster-card sizes.
  - Home media-library cards and server-section cards now share a unified wide-card footprint, making the two rails scan as one mature TV system.
  - Home rail cards use a render-transform focus scale so the current controller target lifts without resizing or reflowing the rail.
- Automated verification:
  - TDD red path confirmed the new Home rail card metric/focus token contract failed before implementation.
  - Targeted Home rail token contract passed: 1 test.
  - Design/source tests passed: 50 total.
  - Core tests passed: 436 total.
  - `git diff --check` passed with only LF/CRLF working-copy warnings.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.197_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.197`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `home-fixture` and launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`; the window path resolved to `NextGenEmby.App_0.1.0.197_x64__h8qjz0sr1sg4m`.
  - Initial Home showed unified-width `Media Libraries` and `Server sections` cards with no label overlap.
  - Pressed `Down`; focus moved from hero Play to `Hot Movies`, the card scaled visually, and the focus frame remained fully visible.
  - Pressed `Right`, `Right`; focus moved across Media Libraries to `Douban Top Rated`, old cards returned to normal scale, and the rail kept the focused card visible.
  - Pressed `Down`, `Right`; focus moved into Server sections and across to `Hot TV Series` without overlapping neighboring cards.
  - Pressed `Down`; focus moved to the `Continue watching` poster row, with poster title/meta still contained in the card.
  - No app-content mouse clicks were used. `dev-command.json` and `dev-command-result.txt` were removed from LocalState after validation.
- Follow-up:
  - Continue extracting remaining page-local Home hero measurements and evaluate whether real-server artwork needs a slightly lighter Home wash once saved-session screenshots are available again.

### 2026-07-07 - Home Hero Token And Poster Row Navigation Polish

- App version: 0.1.0.199.
- Scope: make the Home top decision surface less oversized on TV, promote its repeated visual measurements into skin resources, and fix poster-row horizontal D-pad movement found during keyboard validation.
- Interaction/design changes:
  - `App.xaml` now owns Home Hero height, padding, column spacing, logo max size, title/meta type sizes, poster size, poster fallback type size, accent width/margin/radius, and Hero/poster corner radii.
  - `HomePage.xaml` consumes the new Hero tokens instead of hard-coded `246px` height, `26,24,26,24` padding, `38px` title type, and fixed `182px` poster column.
  - The Hero is now a compact decision surface: Play remains the first visible action, while Media Libraries and Server sections have more useful first-screen presence.
  - Home poster-row focus targets now track both row index and item index, so `Left`/`Right` moves inside a content row instead of repeatedly resolving back to the first item.
- Automated verification:
  - TDD red path confirmed the Home Hero token contract failed before implementation.
  - TDD red path confirmed Home row focus targets lacked `itemIndex`/`ItemIndex` before the poster-row navigation fix.
  - Targeted Home Hero token contract passed: 1 test.
  - Targeted poster-row left/right focus tests passed: 3 total.
  - Home focus policy tests passed: 15 total.
  - Design/source tests passed: 51 total.
  - Core tests passed: 440 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.199_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.199`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `home-fixture` and launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`; the window path resolved to `NextGenEmby.App_0.1.0.199_x64__h8qjz0sr1sg4m`.
  - Initial Home showed the compact Hero with Play focused, Media Libraries visible immediately below, Server sections visible in the same first-screen scan, and no text overlap.
  - Pressed `Down`, `Down`, `Down`, `Right`; focus moved from Hero through Media Libraries and Server sections into Continue watching, then landed on the second poster `Northline S1:E4`.
  - Pressed `Left`; focus returned to the first Continue watching poster `Aurora Protocol`.
  - No app-content mouse clicks were used. `dev-command.json` was consumed by the app and `dev-command-result.txt` reported `completed` / `home-fixture`.
- Follow-up:
  - Continue extracting the remaining one-off Home action button metrics and evaluate row-item focus against real-server rows with more than two visible posters once saved-session screenshots are available again.

### 2026-07-07 - Home Poster Row Column Memory

- App version: 0.1.0.200.
- Scope: make Home poster-row vertical navigation preserve the viewer's visual column when moving between rows, matching mature TV streaming clients.
- Interaction/design changes:
  - `HomeFocusInputPolicy` now passes `rowItemCounts` into `MoveDown`/`MoveUp`.
  - Row targets carry the current `ItemIndex` across vertical moves and clamp to the target row's last available item.
  - No playback decoding, media loading, or Emby transcoding behavior changed.
- Automated verification:
  - TDD red path confirmed vertical row movement previously reset `ItemIndex` to 0.
  - Targeted column-preservation tests passed: 3 total.
  - Home focus policy tests passed: 18 total.
  - Core tests passed: 443 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.200_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.200`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `home-fixture` and launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`; the window path resolved to `NextGenEmby.App_0.1.0.200_x64__h8qjz0sr1sg4m`.
  - Pressed `Down`, `Down`, `Down`, `Right`, `Down`; focus moved from Continue watching column 2 to Next up column 2 (`Room Tone S2:E1`).
  - Pressed `Up`; focus returned to Continue watching column 2 (`Northline S1:E4`).
  - Pressed `Down`, `Down`; focus landed on Hot Movies column 2 (`Midnight Signal`) instead of snapping to `Aurora Protocol`.
  - No app-content mouse clicks were used. `dev-command.json` and `dev-command-result.txt` were removed from LocalState after validation.
- Follow-up:
  - Add a fixture row with fewer items than the source row to keyboard-validate the clamp case visually, not only through policy tests.

### 2026-07-07 - Search Recent Terms Rail

- App version: 0.1.0.203.
- Scope: reduce TV/controller text-entry cost by adding a keyboard-addressable recent-search rail to Search.
- Interaction/design changes:
  - Added `SearchRecentTermsPolicy` in Core for trimming, whitespace collapse, case-insensitive de-duplication, latest-first ordering, max-count limiting, and LocalSettings serialization.
  - Added `RecentSearchTermStore` in the app layer for persistent recent terms.
  - Search now renders a compact `Recent searches` chip rail between scope filters and results when terms exist.
  - `SearchFocusNavigationPolicy` now routes Scope -> Recent terms -> Results/Empty state, with Up returning through the same layers and Left/Right moving within recent terms.
  - DEBUG `search-fixture` seeds deterministic recent terms so the route can be validated without a real saved Emby session.
  - No playback decoding, media loading, or Emby transcoding behavior changed.
- Automated verification:
  - TDD red path confirmed `SearchRecentTermsPolicy` did not exist before implementation.
  - TDD red path confirmed `SearchFocusNavigationPolicy` lacked `RecentTerms` focus area/actions before implementation.
  - TDD red path confirmed Search XAML/code-behind did not render recent terms or include `RecentSearchTermStore` before implementation.
  - Targeted recent-term policy tests passed: 5 total.
  - Search focus navigation tests passed: 13 total.
  - Search-related Core tests passed: 44 total.
  - Full Core test suite passed: 455 total.
  - `git diff --check` passed with no whitespace errors.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.203_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.203`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `search-fixture` and launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`; the window path resolved to `NextGenEmby.App_0.1.0.203_x64__h8qjz0sr1sg4m`.
  - Initial Search showed the Search box focused, scope rail visible, `Recent searches` chips (`Friends`, `Aurora Protocol`, `News 24`) visible, and result cards below with no text overlap.
  - Pressed `Down`, `Down`, `Right`, `Return`; focus moved through All scope into recent terms, activated `Aurora Protocol`, promoted it to the first recent term, refreshed results, and placed the visible focus frame on the first result card.
  - Pressed `Up`; focus returned from the first result to `Aurora Protocol` in recent terms.
  - Pressed `Up`; focus returned from recent terms to the All scope.
  - Pressed `Down`; focus re-entered the first recent term instead of skipping directly to results.
  - Re-ran DEBUG `search-error`: initial error state showed no `Recent searches` rail, so `Down`, `Down` focused `Edit search`, `Right` focused `Search again`, and `Return` retried to the query box. The retry then added the submitted query as a recent term, which matches the committed-search behavior.
  - No app-content mouse clicks were used. `dev-command.json` and `dev-command-result.txt` were removed from LocalState after validation, and the app process was stopped.
- Follow-up:
  - Re-run Search recent terms against a non-DEBUG saved session across two app launches to prove LocalSettings persistence with real user queries.

### 2026-07-07 - Search Recent Term Style Tokens

- App version: 0.1.0.204.
- Scope: promote the new Search recent-term chip metrics into shared TV resources so future skins can adjust the component without editing code.
- Interaction/design changes:
  - Added `TvSearchRecentTermMinHeight`, `TvSearchRecentTermMinWidth`, `TvSearchRecentTermMaxWidth`, and `TvSearchRecentTermPadding` resources.
  - Added `TvSearchRecentTermButtonStyle` for the chip background, border, text, focus, sizing, and padding.
  - SearchPage now applies `TvSearchRecentTermButtonStyle` to dynamically created recent-term buttons instead of hard-coding button dimensions in code-behind.
  - No playback decoding, media loading, or Emby transcoding behavior changed.
- Automated verification:
  - TDD red path confirmed the shared Search recent-term resources/style did not exist before implementation.
  - Targeted source test passed: `Search_Recent_Term_Buttons_Use_Shared_Tv_Style_Tokens`.
  - Full Core test suite passed: 456 total.
  - `git diff --check` passed with no whitespace errors.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.204_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.204`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `search-fixture` and launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`; the window path resolved to `NextGenEmby.App_0.1.0.204_x64__h8qjz0sr1sg4m`.
  - Initial Search showed the styled `Recent searches` chips (`Friends`, `Aurora Protocol`, `News 24`) visible under the scope rail with no text overflow or layout jump.
  - Pressed `Down`, `Down`, `Right`, `Return`; focus moved through All scope into recent terms, activated `Aurora Protocol`, refreshed results, and placed the visible focus frame on the first result card.
  - Pressed `Up`; focus returned from the first result to `Aurora Protocol` in recent terms.
  - No app-content mouse clicks were used. `dev-command.json` and `dev-command-result.txt` were removed from LocalState after validation, and the app process was stopped.

### 2026-07-07 - App Icon Pixel Contract

- App version: 0.1.0.204.
- Scope: strengthen the app identity checklist for the brand-neutral `Player Status Aperture` icon without repainting the already-matching production assets.
- Interaction/design changes:
  - Confirmed the production icon family is generated from `tools/Generate-AppIconAssets.ps1` and matches the current script byte-for-byte for Store, square, wide, and splash assets.
  - Added a pixel-level icon contract that decodes the PNG assets without extra graphics packages and verifies that every required asset preserves the cyan controller-focus signal, green play/confirm signal, and amber progress signal.
  - Kept the icon symbol-only: no brand text, no initials, no official third-party logos, no portal/glow concept.
  - No playback decoding, media loading, or Emby transcoding behavior changed.
- Automated verification:
  - Temporary regeneration check matched all production icon asset hashes.
  - Targeted icon pixel test passed: `Icon_Assets_Preserve_Focus_Play_And_Progress_Signals_At_All_Sizes`.
- Visual validation:
  - Inspected `Square44x44Logo.png` at original size; the focus corner, play core, and progress base remain visible at 44 px without relying on text.

### 2026-07-07 - Home Poster Row Clamp Fixture

- App version: 0.1.0.205.
- Scope: keyboard-validate the Home poster-row clamp case where the source row is wider than the target row.
- Interaction/design changes:
  - Added a DEBUG Home fixture row, `Tonight Picks`, directly after the wider `Hot Movies` row.
  - `Tonight Picks` intentionally contains two items so D-pad movement from a later Hot Movies column must clamp to the last available card instead of losing focus or snapping back to column 1.
  - No playback decoding, media loading, real Emby loading, or Emby transcoding behavior changed.
- Automated verification:
  - TDD red path confirmed the Home fixture did not yet place a short row directly after `Hot Movies`.
  - Targeted Home fixture and Home focus policy tests passed: 23 total.
  - Full Core test suite passed: 458 total.
  - `git diff --check` passed with no whitespace errors.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.205_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.205`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `home-fixture` and launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`; the window path resolved to `NextGenEmby.App_0.1.0.205_x64__h8qjz0sr1sg4m`.
  - Initial Home showed `Tonight Picks` in Server sections, proving the fixture change was active in the installed app.
  - Pressed `Down` five times to reach the `Hot Movies` poster row, then `Right`, `Right` to move into a later column. The visible focus reached `Afterimage` in a row with six cards.
  - Pressed `Down`; focus clamped into the two-item `Tonight Picks` row on `City at Night`, the last available card, with no blank-column focus and no reset to `Ocean Archive`.
  - No app-content mouse clicks were used. `dev-command.json` and `dev-command-result.txt` were removed from LocalState after validation, and the app process was stopped.

### 2026-07-07 - Music Artist Hierarchy

- App version: 0.1.0.207.
- Scope: add artist-aware Music browsing without changing audio playback or native decoding.
- Interaction/design changes:
  - Music now renders four TV columns: Artists, Albums, Songs, and Preview.
  - The Artists column includes `All music` plus derived/fixture `MusicArtist` entries; selecting an artist filters Albums and Songs in place.
  - Emby item parsing now preserves `Artists`, `ArtistItems`, `AlbumArtist`, and `AlbumArtists`, and list queries request artist fields so real server metadata can feed the hierarchy.
  - Music fixture data now includes artist items, artist artwork, and explicit artist-album-song references.
  - Fixed the keyboard focus return path found during validation: after filtering by an artist, `Left` from Songs/Albums returns to the active artist instead of snapping to `All music`.
  - No playback decoding, music playback, real Emby loading contracts, or Emby transcoding behavior changed.
- Automated verification:
  - TDD red path confirmed `EmbyMediaItem`, `MusicBrowseQueryFactory`, `DevelopmentMusicFixtureSnapshot`, and Music page source lacked artist hierarchy support before implementation.
  - Targeted Music artist tests passed: 14 total.
  - Full Core test suite passed: 462 total.
  - `git diff --check` passed with no whitespace errors.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.207_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.207`.
- Keyboard-only validation with Computer Use:
  - Wrote `dev-command.json` with route `music-fixture` and launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`; the window path resolved to `NextGenEmby.App_0.1.0.207_x64__h8qjz0sr1sg4m`.
  - Initial Music showed Artists, Albums, Songs, and Preview columns with focus on `All music`, 3 artists, 3 albums, 6 songs, packaged artwork, and no text overlap.
  - Pressed `Down`; focus moved to `Kairos Collective` and the preview switched to Artist metadata.
  - Pressed `Return`; Music filtered to `Kairos Collective - 1 album - 3 songs`, Albums focused `Nocturne Signals`, and Songs showed the three matching songs.
  - Pressed `Right`; focus moved from the album column to `Opening Credits` in Songs. Pressed `Down`; focus moved to `Glass Elevator` and preview updated.
  - Pressed `Return`; the browse-only `Music playback unavailable` layer opened with `Close` focused. Pressed `Escape`; the layer closed and focus restored to `Glass Elevator`.
  - Pressed `Left`, `Left`; focus returned to active artist `Kairos Collective`, not `All music`, confirming the validation-found focus bug was fixed.
  - Pressed `Up`, `Return`; `All music` restored the full 3 album / 6 song list.
  - No app-content mouse clicks were used.

### 2026-07-07 - Playback Progress Reporting

- App version: 0.1.0.207.
- Scope: verify the real Emby playback lifecycle reporting path without changing native decoding or playback backend code.
- Validation setup:
  - Started a local fake Emby server on `127.0.0.1:5876` with authenticate, home data, `PlaybackInfo`, and playback session endpoints.
  - Added the UWP package loopback exemption for `NextGenEmby.App_h8qjz0sr1sg4m` so the installed app could reach the local validation server.
  - Seeded `dev-login.json` with the fake server session and `dev-command.json` with `{"route":"playback","itemId":"progress-item","itemName":"Progress Fixture","mediaSourceId":"progress-source-1","startPositionTicks":0}`.
- Keyboard-only validation with Computer Use:
  - Launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`; the window path resolved to `NextGenEmby.App_0.1.0.207_x64__h8qjz0sr1sg4m`.
  - The app reached Playback from the saved session, requested `/Items/progress-item/PlaybackInfo?UserId=progress-user`, and started direct playback of the returned media source.
  - The fake server captured `/Sessions/Playing` with `ItemId` `progress-item`, `MediaSourceId` `progress-source-1`, `PlaySessionId` `progress-session-1`, `PlayMethod` `DirectPlay`, and a non-negative `PositionTicks`.
  - After the timer ticks, the fake server captured four `/Sessions/Playing/Progress` requests with `EventName` `TimeUpdate` and increasing `PositionTicks`.
  - Pressed `Escape` from Playback; the app returned to Home and the fake server captured `/Sessions/Playing/Stopped` with the same item, media source, play session, direct-play method, and final `PositionTicks`.
  - No app-content mouse clicks were used.
- Follow-up:
  - Re-run against a real Emby server session when hardware or a persistent validation server is available.

### 2026-07-07 - Empty Session Login

- App version: 0.1.0.207.
- Scope: verify the first-run / no-saved-session login route using keyboard-only input.
- Validation setup:
  - Rebuilt the Debug x64 solution and re-signed `NextGenEmby.App_0.1.0.207_x64_Debug.msix` after an earlier stale package registration produced a local `System.BadImageFormatException` during COM activation; the fresh MSBuild/sign/install path launched successfully.
  - Reinstalled the package with `Add-AppDevPackage.ps1 -Force` to clear the saved session and start from the Login page.
  - Started a local fake Emby server on `127.0.0.1:5877` with authenticate, views, and empty home row endpoints.
  - Added the UWP package loopback exemption for `NextGenEmby.App_h8qjz0sr1sg4m` so the installed app could reach the local validation server.
- Keyboard-only validation with Computer Use:
  - Launched the installed app through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`; the window path resolved to `NextGenEmby.App_0.1.0.207_x64__h8qjz0sr1sg4m`.
  - The clean launch showed the Login page with focus on Server URL.
  - Pressed `Ctrl+A`, typed `http://127.0.0.1:5877`, pressed `Tab`, typed the username, pressed `Tab`, typed the password, pressed `Tab`, and pressed `Return`.
  - The fake server captured `POST /Users/AuthenticateByName` with the keyboard-entered username/password, then the app navigated to Home and requested Resume, NextUp, Latest, Views, HomeSections, and library rows.
  - Closed and relaunched the app; Home loaded from the saved token without another `/Users/AuthenticateByName` request, confirming credential persistence.
  - No app-content mouse clicks were used.
- Cleanup and follow-up:
  - Stopped the fake login server, removed temporary debug files, removed the loopback exemption, removed local crash-dump registry capture, and reinstalled the package again to leave the app on the Login page with no saved fake session.
  - A final Computer Use screenshot confirmed the clean Login page and Server URL focus.
  - Add a TV-accessible sign-out / clear-session action in Settings so this route can be revalidated without reinstalling the package.

### 2026-07-07 - Settings Sign Out And Session Clear

- App version: 0.1.0.207.
- Scope: add and verify a TV/controller-reachable sign-out route so empty-session login can be reset without reinstalling the app.
- Interaction/design changes:
  - Added a `Sign out` account action to Settings below the signed-in session summary.
  - Added a modal confirmation layer with a safe default focus on `Keep signed in`; users must move right to the destructive `Sign out` action before confirming.
  - Added shared `TvSettingsAccountActionButtonStyle` and `TvSettingsDangerButtonStyle` resources so account actions and destructive confirmations are skin/theme-adjustable from `App.xaml`.
  - Added directional focus handling so `Up` from the default `Thumbstick seek preview` focus reaches `Sign out`, `Down` returns to the playback input, `Left`/`Right` move within the confirmation layer, and `Escape` cancels the layer.
  - Confirming sign-out calls `ApplicationDataSessionStore.ClearAsync()`, navigates to `LoginPage`, and clears the frame back stack.
  - No playback decoding, playback reporting, Emby media loading contracts, or Emby transcoding behavior changed.
- Automated verification:
  - TDD red path confirmed Settings did not yet expose shared sign-out styles, controller-reachable sign-out controls, confirmation handlers, session clearing, or Login navigation.
  - Targeted Settings source contract passed: `Settings_Page_Renders_Controller_Reachable_Sign_Out_Action`.
  - Full Core test suite passed: 463 total.
  - `git diff --check` passed with no whitespace errors.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.207_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate thumbprint `6CB453A2FEC300C6E5034152C6C1A68DE31A7BD0`, verified with `signtool verify /pa`, and installed locally as `NextGenEmby.App 0.1.0.207`.
- Keyboard-only validation with Computer Use:
  - Started a local fake Emby server on `127.0.0.1:5878`, added a UWP loopback exemption for `NextGenEmby.App_h8qjz0sr1sg4m`, seeded `dev-login.json`, and used DEBUG route `settings` so the installed app logged in and opened Settings.
  - Initial Settings showed `Settings Tester on http://127.0.0.1:5878`, app/client version, `Sign out`, and default focus on `Thumbstick seek preview`.
  - Pressed `Up`; focus moved to `Sign out` with a clear cyan focus frame.
  - Pressed `Return`; the confirmation layer opened, dimmed the Settings page, and focused `Keep signed in`.
  - Pressed `Escape`; the layer closed and focus returned to `Sign out`.
  - Removed `dev-login.json` before final confirmation so Login would not auto-authenticate again after sign-out.
  - Pressed `Return`, `Right`, `Return`; focus moved to the destructive `Sign out`, the app cleared the saved session, navigated to Login, and focused Server URL.
  - Relaunched the app; it stayed on Login and the fake server request count remained unchanged, proving the saved session was cleared.
  - No app-content mouse clicks were used.
- Cleanup:
  - Stopped the fake sign-out server, removed temporary debug files, removed the loopback exemption, and left the installed app in a logged-out Login state.

### 2026-07-07 - Design Conformance Source Gate

- App version: source tree after `40444cc docs: add design conformance QA batches`; installed local app remains `0.1.0.207`.
- Scope: begin the new design-conformance loop by running the lowest-cost source/design contract layer before launching a visual fixture batch.
- Checklist batch:
  - `docs/qa/design-conformance-checklist.md` universal gates and Automation Ladder.
  - Batch 01 was not visually executed yet because source-level design contracts already failed.
- Automated validation:
  - Ran `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "FullyQualifiedName~Design"`.
  - Result: 53 passed, 3 failed.
- Findings recorded before fixes:

| ID | Severity | Page | Evidence | Expected | Actual | Proposed batch fix |
| --- | --- | --- | --- | --- | --- | --- |
| DC-00.01 | Fail | Shared tokens / app shell / playback / icon generator | `DesignTokenResourceTests.App_Runtime_Colors_Are_Backed_By_Design_Tokens`, `DesignTokenResourceTests.Playback_Resources_ReUse_Design_Canvas_And_Surface_Family`, and `IconAssetContractTests.Icon_Generator_Tokens_Map_To_Design_Tokens` failed. | Runtime XAML resources and icon generator consume the current `docs/DESIGN.md` token palette, including `canvas #05070A`. | Runtime XAML and icon generator still use older `canvas #050607`, and source inspection shows additional old palette values such as cyan focus, warm amber, and older surface shades. | Update the shared runtime color resources and icon generator token map as one token-alignment batch, then rerun the design source tests before slower Home/Guide visual verification. |

- Decision:
  - Do not start per-route visual fixes yet.
  - Fix the shared token drift first because it affects all pages and would make screenshot comparison noisy.

### 2026-07-07 - Design Token Alignment Batch

- App version: 0.1.0.208.
- Scope: fix the shared token drift found by the Design Conformance Source Gate before running slower screenshot-based visual review.
- Implementation changes:
  - Updated `App.xaml` runtime color resources to match the current `docs/DESIGN.md` palette: cool graphite canvas/surfaces, neutral focus, muted green progress, and no default cyan/amber action language.
  - Updated `tools/Generate-AppIconAssets.ps1` from the superseded Player Status Aperture color contract to the current Player Lift Mark token contract.
  - Regenerated Store, 44px, 150px, wide, and splash PNG assets from the updated generator.
  - Updated design source tests so `AppFocusSecondaryColor` maps to `focus_fill`, and the icon contract checks neutral focus plus muted green playback/progress signals instead of cyan focus and amber progress.
  - Updated `docs/DESIGN.md` migration notes to reflect that shared runtime tokens and icon generator tokens are now aligned, while page-level usage still needs migration.
- Automated verification:
  - `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "FullyQualifiedName~Design"` passed: 56 total.
  - `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj` passed: 463 total.
  - Restored native `packages.config` dependencies into `src/NextGenEmby.Native/packages` after the first App build found the native NuGet packages missing locally.
  - `MSBuild NextGenXboxEmby.sln /p:Configuration=Debug /p:Platform=x64 /m` passed with 0 warnings and 0 errors.
  - Signed and installed `NextGenEmby.App 0.1.0.208`.
- Follow-up:
  - Page controls still use old focus-frame and action-brush shapes in places. Those should be fixed in page batches, not by adding new raw colors.

### 2026-07-07 - Design Conformance Batch 01 Home And Guide Baseline

- App version: 0.1.0.208.
- Scope: run the first Home/Guide design-conformance batch after shared token alignment.
- Data source: DEBUG `home-fixture` with packaged QA artwork; no private Emby server or personal media assets.
- Automation layer: installed app DEBUG fixture plus Windows keyboard/screenshot automation, not app-content mouse clicks.
- Keyboard-only validation:
  - Killed the running app, wrote `dev-command.json` with route `home-fixture`, and launched through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`.
  - `dev-command-result.txt` reported `completed / home-fixture`.
  - Initial focus: `Play`.
  - Pressed `M`; Guide opened with focus on `Home`.
  - Pressed `Escape`, then `Down`; focus moved to `Hot Movies`.
  - Pressed `Right`, `Right`; focus moved to `Anime`.

Findings recorded before fixes:

| ID | Severity | Page | Evidence | Expected | Actual | Proposed batch fix |
| --- | --- | --- | --- | --- | --- | --- |
| DC-01.01 | Fail | Home first viewport | Home screenshot from `home-fixture` shows a large top decision surface with a full highlighted border and vertical active edge. | Home should read as a rail-first TV media surface, with artwork and rows carrying the page, not a large framed dashboard hero. | The current top surface still looks like the previous implementation model and uses a bright complete outline. | Rework Home top composition toward the A3 rail-first structure; remove complete bright frame from the hero/decision surface. |
| DC-01.02 | Fail | Home card focus | Focused `Hot Movies` library card uses a complete bright perimeter frame. | Card focus should use scale, luminance, local dimming, and optional matte selected backplate; bright full frames are not the normal focus language. | Focus remains readable but visually contradicts `DESIGN.md`. | Replace default Home card focus frame with matte fill/luminance/scale treatment and reserve high-visibility edge for fallback only. |
| DC-01.03 | Fail | Continue Watching / Next Up | Home fixture screenshot shows Continue Watching and Next Up as small poster-like cards below the library rows. | Continue Watching and Next Up should use wide resume-card anatomy with full-bleed crop, bottom black scrim, progress, and stable title protection. | The rows do not match the wide resume-card rule and reduce TV scan clarity. | Implement the wide resume-card anatomy for Home progress rows before deeper route visual QA. |
| DC-01.04 | Concern | Guide | Expanded Guide is source-aware and keyboard reachable, but it lists every source directly and still uses a bright full focus outline. | Guide should stay quiet, source-aware, and include Search plus More/Source Hub for lower-frequency or unpinned sources. Focus should use matte fill, not a bright outline. | Guide is functional and comprehensive, but visually too frame-heavy and does not yet express the Source Hub model. | Adjust Guide focus treatment and decide whether to introduce Source Hub grouping in the same shell batch or defer to a source-management batch. |
| DC-01.05 | Concern | Fixture artwork / green usage | Packaged QA cards use abstract block artwork and repeated muted green bars/top accents. | Visual QA should include realistic fictional posters/wide art, and green should stay a signal rather than page texture. | Fixture is useful for layout but weak for final visual judgment; green still appears as repeated card decoration. | Replace/extend fixture artwork with realistic fictional media covers and remove passive green card accents. |

- Decision:
  - Do not fix single rows one by one.
  - Next implementation batch should address shared Home/Shell focus language, Home progress-row anatomy, and passive green usage together before rerunning Batch 01.

### 2026-07-08 - Design Conformance Batch 01 Home And Guide Fix Rerun

- App version: 0.1.0.210.
- Scope: unified fix for the Home/Shell focus language, Home progress-row anatomy, and passive green card decoration found in Batch 01.
- Data source: DEBUG `home-fixture` with packaged QA artwork; no private Emby server or personal media assets.
- Source contracts added before implementation:
  - Home cards must use matte focus treatment instead of system bright focus rings.
  - Continue Watching and Next Up must render as wide resume cards with progress and bottom text protection.
  - Passive Home section chrome must not use green decorative accents.
  - Guide active/focus state must use quiet matte fill instead of a bright full border.
- Automated verification:
  - Red path confirmed the new source contracts failed before the implementation batch.
  - Targeted Design tests passed: 59 total.
  - Full Core test suite passed: 466 total.
  - `git diff --check` passed with line-ending warnings only.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.210_x64_Debug.msix`.
- Keyboard-only validation with installed DEBUG fixture:
  - Wrote `dev-command.json` with route `home-fixture` and launched through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`.
  - `dev-command-result.txt` reported `completed / home-fixture`.
  - Initial focus: `Play`.
  - Pressed `M`; Guide opened with focus on `Home`.
  - Pressed `Escape`, then `Down` repeatedly to traverse library and resume rows; focus reached `Aurora Protocol`, `Northline S1:E4`, and then `Harbor Run` after `Right`.
  - Screenshot set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch01-210-20260708-005212`.

Fix rerun findings:

| ID | Severity | Page | Evidence | Expected | Actual | Follow-up |
| --- | --- | --- | --- | --- | --- | --- |
| DC-01.01 | Fail | Home first viewport | Initial Home screenshot still shows the top decision surface as a large framed module with a vertical active edge. | Home should read as a rail-first TV media surface, with artwork and rows carrying the page. | Unchanged in this batch; still too close to the older dashboard-like model. | Handle as the next Home top-composition batch rather than mixing it into row/focus fixes. |
| DC-01.02 | Pass with concern | Home card focus | Focused Home cards now use matte fill/scale treatment and suppress the system bright focus ring. | Card focus should use scale, luminance, local dimming, and optional matte selected backplate. | Shared Home cards now follow the quiet focus language. Normal poster-grid selected-backplate still needs a library-grid-specific pass. | Recheck movie grid selected state when the library grid design is implemented. |
| DC-01.03 | Pass | Continue Watching / Next Up | Resume rows now render wide cards with full-bleed wide art, bottom black scrim, stable title/meta area, and progress indicator. | Continue Watching and Next Up should use wide resume-card anatomy. | Matches the current design rule and improves TV scan clarity. | Continue to tune artwork crops with more realistic fixture media. |
| DC-01.04 | Concern | Guide | Expanded Guide focus/active state now uses matte fill and transparent border. | Guide should stay quiet, source-aware, and use matte focus language. | Focus treatment is aligned; Source Hub / lower-frequency source grouping is still deferred. | Handle source grouping in a source-management/navigation batch. |
| DC-01.05 | Concern | Fixture artwork / green usage | Passive green card accents were removed from Home cards; green remains only as a signal/progress color. | Green should stay a signal rather than page texture, and visual QA should use realistic fictional media covers. | Color usage is improved, but fixture artwork is still abstract and weak for final judgment. | Replace or extend packaged QA artwork with realistic fictional posters and wide art. |

- Decision:
  - Commit this batch as the shared Home/Shell focus and resume-card correction.
  - Next design-conformance batch should target the Home first viewport/top composition and realistic fixture artwork, then revisit normal poster selected state.

### 2026-07-08 - Design Conformance Batch 02 Library, Search, And Poster Grids Baseline

- App version: 0.1.0.210.
- Scope: run the next design-conformance batch before fixing poster-grid, search-result, option-sheet, and fixture-artwork visual drift.
- Data source:
  - DEBUG `home-fixture` for Home first viewport carry-over evidence.
  - DEBUG `collections-fixture` for deterministic collection root, child movie grid, toolbar, and sort sheet.
  - DEBUG `search-fixture` for deterministic mixed search-result poster grid.
  - DEBUG `search-error` for search recovery / empty-error state.
- Screenshot sets:
  - Home: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch02-20260708-010135`.
  - Collections: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch02-collections-20260708-010335`.
  - Search: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch02-search-20260708-010515`.
  - Search error: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch02-search-error-20260708-010702`.
- Keyboard-only validation:
  - `home-fixture`: launch succeeded, `dev-command-result.txt` reported `completed / home-fixture`, initial focus was `Play`.
  - `collections-fixture`: launch succeeded, initial focus landed on the first collection item, `Enter` opened `Signal Archives`, `Up` reached `Sort`, and `Enter` opened the sort sheet.
  - `search-fixture`: launch succeeded, focus moved from `Search title` to scope chips, recent terms, and then the search result grid with `Down`/`Right`.
  - `search-error`: launch succeeded and showed the matte search recovery state.

Findings recorded before fixes:

| ID | Severity | Page | Evidence | Expected | Actual | Proposed batch fix |
| --- | --- | --- | --- | --- | --- | --- |
| DC-02.01 | Fail | Library poster grid | `collections-child-grid.png` shows focused `Aurora Protocol` with a bright complete perimeter frame, title/meta inside the poster scrim, and the old per-card hairline template. | Movie/series items use vertical poster cards, with focused state as an integrated matte selected backplate around poster plus title/meta below or attached as one object; no bright frame, glass card, or heavy shadow. | Focus is readable but still uses the rejected older card-focus vocabulary. Text is trapped inside the artwork instead of the newer ordinary-movie-card direction. | Replace the shared poster grid template with image-first poster artwork, title/meta below the image, and a focused matte backplate/scale treatment. |
| DC-02.02 | Concern | Library toolbar / sort sheet | `collections-toolbar.png` and `collections-sort-sheet.png` show a grounded matte sheet, but focused options still use a high-contrast full outline. | Toolbar controls may use command matte focus. Sheets should feel grounded and avoid floating glass plates or bright focus frames. | The sheet is acceptably matte and not glassy, but option focus still feels like the old bright-frame system. | Keep the sheet structure for now, but move option/button focus toward the shared matte command recipe in the same component pass if scope allows. |
| DC-02.03 | Fail | Search results | `search-down3.png` shows Search result cards sharing the same bright perimeter focus and text-inside-poster template as Library. | Search results share the poster-grid selected treatment. Scope chips are compact and neutral; recent terms should not steal recovery focus. | Scope/recent navigation is usable, but result cards visually fail the same poster-grid rule as Library. | Update Search to consume the same poster-card selected-state structure as Library. |
| DC-02.04 | Concern | Empty / no-artwork states | `search-error-initial.png` shows a matte recovery state; source inspection shows Search has fallback initials, but Library's poster template lacks an equivalent fallback text/icon inside the card. | Empty state, unavailable poster tile, and fallback initials remain intentional matte surfaces, not abstract generated art. | Search error state is acceptable. No-artwork tile coverage is incomplete and Library fallback would look blank rather than intentional. | Add or standardize Library/Search no-artwork fallback anatomy and add source tests for it. |
| DC-02.05 | Fail | Fixture artwork / visual QA | `tools/Generate-HomeQaArtworkAssets.ps1` still generates abstract geometric artwork with high-saturation cyan/green/amber/red accents; screenshots show media covers do not resemble film posters. | Visual QA must include realistic fictional posters with faces, title blocks, high-contrast crops, saturated content color, and varied genres. | Current fixture art is useful for layout but weak for judging the final movie-card direction and still pulls the product toward generic sci-fi UI. | Replace generated QA artwork with more movie-like fictional poster/wide compositions and align generator tokens with the current cool graphite/artwork-led rules. |
| DC-02.06 | Blocked | Movies route | `movies` route has no deterministic fixture payload and may require a saved session. | Batch 02 should be able to validate a normal Movies grid without depending on private server data. | Collections/search fixtures cover poster-grid behavior, but they are not a direct Movies fixture. | Add a deterministic movie-grid fixture route or route `movies` to QA movie items in DEBUG when explicitly launched by `dev-command.json`. |
| DC-02.07 | Fail | Home first viewport carry-over | `home-initial.png` still shows a large framed Home decision surface above rails. | Home should be a rail-first personal media dashboard; any hero must be compact enough that high-value rails carry the first viewport. | Batch 01's deferred Home top-composition failure remains visible. | Fold the Home first-viewport cleanup into this batch or schedule it as the immediate next batch if poster-grid scope grows too large. |

- Decision:
  - Fix now as a grouped visual-system batch for shared poster cards, Search/Library parity, no-artwork fallback, deterministic Movies fixture coverage, and QA artwork realism.
  - Keep sort/filter sheet layout unless it blocks the shared command-focus cleanup; its current issue is lower severity than media-card focus.
  - Treat Home first viewport as part of the same design-conformance wave, but do not let it delay poster-grid source contracts if it becomes too broad.

### 2026-07-08 - Design Conformance Batch 02 Library, Search, And Poster Grids Fix Rerun

- App version: 0.1.0.211.
- Scope: grouped correction for shared poster-grid focus, Search/Library parity, deterministic Movies fixture coverage, no-artwork fallback contracts, and packaged QA artwork realism.
- Data source: DEBUG fixtures only; no private server data or personal media assets.
- Source contracts added before implementation:
  - Poster-grid items suppress the system bright focus ring and use a matte selected backplate plus slight lift.
  - Poster artwork remains image-first, while title/meta are placed below the image instead of inside a bottom card box.
  - Library and Search consume the same poster selected-state structure.
  - Library and Search expose an intentional no-artwork fallback with initials rather than an empty blank tile.
  - `movies-fixture` provides deterministic movie-grid coverage for visual QA.
  - QA artwork generation moves from abstract neon blocks to fictional movie-like poster and wide artwork.
- Automated verification:
  - Red path confirmed the new poster-grid source contracts failed before the implementation batch.
  - Targeted poster/navigation tests passed: 34 total.
  - Targeted Design tests passed: 64 total.
  - Full Core test suite passed: 472 total.
  - App Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.211_x64_Debug.msix`.
  - Signed and installed `NextGenEmby.App 0.1.0.211` for screenshot rerun.
- Keyboard-only validation with installed DEBUG fixtures:
  - `movies-fixture`: launch succeeded, `dev-command-result.txt` reported `completed / movies-fixture`, focus landed on the movie grid, and keyboard traversal reached neighboring movie cards and the toolbar.
  - `search-fixture`: launch succeeded, focus moved through the search controls into the result grid, and result-card traversal used the shared poster selected treatment.
  - Movies screenshot set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch02-rerun-movies-20260708-012810`.
  - Search screenshot set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch02-rerun-search-20260708-012942`.

Fix rerun findings:

| ID | Severity | Page | Evidence | Expected | Actual | Follow-up |
| --- | --- | --- | --- | --- | --- | --- |
| DC-02.01 | Pass with concern | Library / Movies poster grid | `movies-initial.png` and `movies-right.png` show the selected movie card using an integrated matte backplate and slight lift, without the previous full bright perimeter frame. | Movie/series items use vertical poster cards with quiet selected treatment, title/meta below the image, and no heavy glass/shadow vocabulary. | The poster selected state now matches the ordinary movie-card direction. The deterministic Movies fixture currently has a small item count, so dense multi-row behavior should keep being checked with larger datasets. | Expand fixture coverage when richer media samples are added. |
| DC-02.02 | Concern | Library toolbar / sort sheet | `movies-toolbar.png` and `movies-sort-sheet.png` still show a functional matte command sheet, but option focus keeps some system-level contrast. | Sheets should stay grounded and avoid floating glass plates or bright focus frames. | Lower-severity drift remains; media-card focus was corrected first. | Handle command-sheet option focus in a command-component batch. |
| DC-02.03 | Pass | Search results | `search-results.png` and `search-results-right.png` show Search result cards using the same poster selected-state structure as Library/Movies. | Search results share the poster-grid selected treatment. | Search/Library visual parity is restored for poster cards. | Continue testing with mixed item types and missing artwork. |
| DC-02.04 | Pass with concern | Empty / no-artwork states | Source contracts now require `Initials` fallback in Library and Search poster templates; Search recovery remains matte. | Empty state, unavailable poster tile, and fallback initials remain intentional matte surfaces. | The shared fallback anatomy is present in source. Runtime screenshots did not yet include a forced no-artwork movie tile. | Add a deterministic missing-poster item to a future fixture. |
| DC-02.05 | Pass with concern | Fixture artwork / visual QA | Regenerated QA posters and wide art now use fictional cover-like compositions instead of abstract cyan/green/amber/red blocks. | Visual QA should include realistic fictional posters and wide art that stress real card composition. | Artwork now supports judging poster-card structure better, though it is still generated/stylized rather than photoreal production artwork. | Improve genre variety and real-media-like crops when fixture art is expanded. |
| DC-02.06 | Pass | Movies route | `movies-fixture` launches deterministically and reports `completed / movies-fixture`. | Movie-grid validation must not depend on private server state. | Deterministic Movies visual QA route is available. | Keep this route as the default movie-grid regression target. |
| DC-02.07 | Deferred fail | Home first viewport carry-over | Home first-viewport evidence from the Batch 02 baseline still applies. | Home should be a rail-first personal media dashboard; any hero must be compact enough that high-value rails carry the first viewport. | Not addressed in this poster-grid batch to keep the commit reviewable. | Make Home top composition the next design-conformance batch. |

- Decision:
  - Commit this batch as the shared Library/Search/Movies poster-grid and fixture-artwork correction.
  - Next design-conformance batch should target Home first viewport/top composition and command-sheet option focus, rather than broadening this commit further.

### 2026-07-08 - Design Conformance Batch 02 Follow-Up Home Top And Command Sheets Baseline

- App version: 0.1.0.211.
- Scope: run the deferred Home first-viewport/top-composition issue and the lower-severity command-sheet option-focus issue before fixing either one.
- Data source: DEBUG fixtures only; no private server data or personal media assets.
- Screenshot set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch03-baseline-20260708-014441`.
- Keyboard-only validation:
  - `home-fixture`: launch succeeded, `dev-command-result.txt` reported `completed / home-fixture`.
  - Captured Home initial focus, Guide-open state, first rail focus, and resume-row focus.
  - `movies-fixture`: launch succeeded, `dev-command-result.txt` reported `completed / movies-fixture`.
  - Pressed `Up` from the movie grid to focus Sort, `Enter` to open Sort sheet, `Escape`, `Right` to Filter, and `Enter` to open Filter sheet.

Findings recorded before fixes:

| ID | Severity | Page | Evidence | Expected | Actual | Proposed batch fix |
| --- | --- | --- | --- | --- | --- | --- |
| DC-02F.01 | Fail | Home first viewport | `home-initial.png` shows a large framed Continue Watching decision surface with a complete hairline perimeter and a bright left active edge. | Home should be a rail-first personal media dashboard. A hero/continue decision can exist, but it must be compact, unframed, and leave the first rail as the dominant first-viewport structure. | The top surface still reads as a dashboard module. It makes Continue Watching look like a one-item hero promo even though Continue Watching is also a rail concept. | Remove the full framed hero container and bright active edge. Convert the top decision into an unframed compact feature strip, then let Media Libraries / source rails carry the first viewport. |
| DC-02F.02 | Concern | Home Guide over content | `home-guide.png` shows Guide focus is matte and readable, but the large framed Home top surface remains visible behind it and competes with the opened guide. | Guide should remain quiet and source-aware while content remains primary. Underlying Home content should not expose a second strong frame competing with the guide. | Guide treatment is acceptable, but the Home top frame makes the composition feel heavier than the current design system intends. | Fix with the same Home top-composition change as DC-02F.01 rather than changing Guide behavior. |
| DC-02F.03 | Fail | Movies Sort / Filter sheets | `movies-sort-sheet.png`, `movies-filter-sheet.png`, and `movies-filter-sheet-down.png` show the sheet as a large bottom panel whose option area is pushed too low in the viewport; source inspection from Batch 02 already showed option focus using system-like outline treatment. | Command sheets should be grounded matte panels with visible options and matte command focus. They should not feel like floating glass, bright focus frames, or clipped bottom drawers. | The sheet is matte, but its vertical placement and option visibility are weak at 1600x900. The option focus recipe is not yet promoted to the shared matte command style. | Rework the Library option sheet presenter to sit higher with a fixed visible content area and shared matte option-button focus. |
| DC-02F.04 | Concern | Movies toolbar controls | `movies-toolbar-sort-focus.png` and `movies-toolbar-filter-focus.png` show toolbar controls are quiet enough, but still look like generic bordered command boxes rather than the final matte command recipe. | Toolbar controls may use subtle hairline structure, but focused state should be fill/luminance first with no bright edge as the default cue. | Usable and lower priority than the sheet; the control shape is close but not fully aligned with the command-focus vocabulary. | Align toolbar focus tokens while touching the sheet styles, if it can be done through shared resources without broad page churn. |

- Decision:
  - Fix Home top composition and Library command sheets as one visual-system batch.
  - Do not change Guide behavior in this batch; the guide issue is secondary to the underlying Home frame.
  - Keep media-card poster focus unchanged from Batch 02 unless a shared resource forces a small adjustment.

### 2026-07-08 - Design Conformance Batch 02 Follow-Up Home Top And Command Sheets Fix Rerun

- App version: 0.1.0.213.
- Scope: rerun the Home top-composition and Library command-sheet checks after source-level fixes.
- Data source: DEBUG fixtures only; no private server data or personal media assets.
- Screenshot set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch02-followup-rerun-20260708-020356`.
- Implementation note:
  - A first rerun on app version 0.1.0.212 corrected the oversized Home frame, but exposed clipped Home command buttons at the bottom of the top feature strip.
  - App version 0.1.0.213 increased the compact Home strip height and tightened inner spacing so the Play and Details commands fit without restoring the old framed hero module.
- Keyboard-only validation:
  - `home-fixture`: launch succeeded and produced Home initial, first-rail-focus, resume-focus, and guide screenshots.
  - `movies-fixture`: launch succeeded and produced Sort sheet, Filter sheet, and option-focus screenshots.
  - Sort and Filter sheets were opened with keyboard focus movement only (`Up`, `Enter`, `Escape`, `Right`, `Enter`, then arrow movement inside the sheet).

Fix rerun findings:

| ID | Result | Page | Evidence | Outcome | Residual risk |
| --- | --- | --- | --- | --- | --- |
| DC-02F.01 | Pass with concern | Home first viewport | `home-initial.png` and `home-first-rail-focus.png`. | The top Continue Watching decision is now an unframed compact feature strip. The previous complete hairline perimeter and bright left active edge are gone, and the first rail reads as the main Home structure. | The feature strip is still a single-item decision area. That is acceptable for this batch, but future real-data runs should confirm it does not compete with populated Continue Watching rails. |
| DC-02F.02 | Pass | Home Guide over content | `home-guide.png`. | Guide behavior did not need to change. Removing the large underlying Home frame makes the opened Guide feel less visually contested. | None specific to this batch. |
| DC-02F.03 | Pass | Movies Sort / Filter sheets | `movies-sort-sheet.png`, `movies-filter-sheet.png`, and `movies-filter-sheet-down.png`. | Sort and Filter now use a top-positioned matte sheet with visible options and a fill-first focus treatment. The sheet no longer behaves like a large bottom drawer, and option focus no longer depends on a bright outline. | The current sheet width is intentionally generous for TV readability; future longer option labels should be checked against localized strings. |
| DC-02F.04 | Pass with concern | Movies toolbar controls | `movies-toolbar-sort-focus.png` and `movies-toolbar-filter-focus.png`. | Sort and Filter toolbar commands now use the shared matte command treatment. Resting hairlines remain subtle enough to define hit targets without becoming the focus cue. | This is acceptable for keyboard/controller use, but native Xbox focus rendering may still need per-platform tuning if WinUI adds unexpected focus visuals. |

- Source-level contracts:
  - Added a Home source test that requires the compact unframed top feature strip and rejects the removed accent-edge resources.
  - Added a Library source test that requires shared matte command styles, top-positioned option sheets, and no old bottom-drawer alignment.
- Verification:
  - Targeted design source tests were first run red against the baseline, then green after implementation.
  - `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "FullyQualifiedName~HomeAccessibilitySourceTests|FullyQualifiedName~LibraryPageSourceTests"`: passed, 21 tests.
  - `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj --filter "FullyQualifiedName~Design"`: passed, 66 tests.
  - `dotnet test tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj`: passed, 474 tests.
  - `MSBuild.exe NextGenXboxEmby.sln /p:Configuration=Debug /p:Platform=x64 /m`: passed, 0 warnings and 0 errors.
  - Signed and installed `NextGenEmby.App_0.1.0.213_x64_Debug.msix`; installed app version query returned 0.1.0.213.
- Decision:
  - Commit this batch as the Home top-composition and Library command-sheet focus correction.
  - Next design-conformance batch can move to the detail/playback surfaces or broader native Xbox focus tuning, depending on which screen development consumes first.

### 2026-07-08 - Design Conformance Batch 03 Details And Metadata Actions Baseline

- App version: 0.1.0.213.
- Scope: run Batch 03 Details before making visual fixes, covering first viewport, media versions, organize sheet, and below-fold secondary rails.
- Data source: DEBUG `details-fixture`; no private server data or personal media assets.
- Valid screenshot set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch03-details-baseline-20260708-021939`.
- Launch probe set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-details-relaunch-probe-20260708-021839`.
- Keyboard-only validation:
  - Wrote `dev-command.json` with route `details-fixture`.
  - Cold launch consumed the command and `dev-command-result.txt` reported `completed / details-fixture`.
  - The first attempt left a stale UWP frame on Login after `MediaDetailsPage` briefly navigated; the validation run closed the app frame, relaunched, and confirmed the Details fixture remained visible through repeated screenshots.
  - From the Details default focus, pressed `Down` into Versions, `Down` and `Enter` to select the fallback source, `Down` into Organize, `Enter` to open Add to collection, `Down` inside the sheet, `Escape` to close, then `Down` toward secondary rails.

Findings recorded before fixes:

| ID | Severity | Page | Evidence | Expected | Actual | Proposed batch fix |
| --- | --- | --- | --- | --- | --- | --- |
| DC-03.01 | Fail | Details first viewport | `details-initial.png` and probe screenshots show a complete visible poster card on the left and the deterministic information column on the right. | Details should use a deterministic content/action column plus one right-side atmosphere artwork zone. The artwork zone is not a poster viewer and should fade into the content column. | The page still follows the older poster-and-info layout. The visible poster competes with the Details decision surface, while the right side is occupied by controls instead of atmosphere. | Recompose Details into a left/middle content column and a single right-side artwork atmosphere zone. Use `Backdrop`, then `Thumb`, `Banner`, and `Primary` as atmosphere source, with black/matte fallback and no duplicate visible poster. |
| DC-03.02 | Fail | Details action row | `details-initial.png` shows `Resume` with a bright complete focus outline. Source shows Details action buttons still opt into system focus visuals. | Details media actions should use 52px matte command targets. Focus should be fill/luminance first with no bright full perimeter; Play/Resume fill remains neutral graphite. | Action focus is readable but uses the rejected system outline vocabulary. | Add a shared Details media-action button style, disable system focus visuals on action buttons, and apply the neutral matte focused fill in code or style resources. |
| DC-03.03 | Fail | Version/source selection | `details-version-focus.png` and `details-version-selected.png` show source rows with full bright focus outlines and a green vertical selected bar. | Source selection must be reachable before playback and visually separate selection from focus. It should not use green for neutral source selection or a bright focus frame as the main cue. | Focus and selection are both visually heavy. The selected-source marker uses the same green family reserved for active playback/progress/watched state. | Create a Details source-option style using matte fill for focus and a neutral/tertiary selected marker. Keep selected state distinct from focus without using green or a bright perimeter. |
| DC-03.04 | Concern | Add-to collection sheet | `details-collection-sheet.png` and `details-collection-sheet-down.png` show a usable matte sheet, but option focus is a high-contrast complete outline and the panel is visually disconnected from the newer command-sheet recipe. | Add-to sheets should be grounded matte panels with visible options and matte option focus. A complete outline can be reserved for exceptional separation, not normal focus. | The sheet works, but it looks like an older modal style compared with the newer Library Sort/Filter sheets. | Reuse or parallel the shared matte option-sheet command treatment introduced for Library, while preserving the richer thumbnail row anatomy if useful. |
| DC-03.05 | Fail | Details secondary rails | `details-similar-focus.png` shows More like this cards with a bright full focus frame and text still trapped inside the poster image. | Secondary media rails should follow the shared poster-card selected-backplate rules: poster artwork first, title/meta below or attached as one object, no bright frame. | Details recommendation cards did not receive the Batch 02 poster-card update. | Move Details similar/person media cards onto the shared poster-card visual recipe or add a Details-specific source contract that requires the same selected-backplate behavior. |
| DC-03.06 | Concern | Below-fold vertical focus | `details-explore-focus.png` and `details-explore-second-down.png` did not produce a visible Explore chip focus transition after moving down from More like this. | D-pad movement should move predictably between More like this, Explore facets, and Cast & crew, with visible focus and scroll anchoring. | Navigation may still be functional through another path, but the attempted vertical route did not visibly land on Explore. | Add an explicit Details section focus policy for Similar -> Explore -> Cast & crew, then rerun the same keyboard path. |
| DC-03.07 | Concern | Fixture automation reliability | `dev-command-result.txt` can report `completed / details-fixture` even when a stale frame still shows Login unless the old app frame is closed first. | Fixture routes should provide reliable installed-app evidence. The validation protocol should not confuse a completed command with the final visible page. | This was recoverable by closing the UWP frame before relaunch, but it is easy to mis-capture. | Update the run protocol to close the `Next Gen Xbox Emby` ApplicationFrameWindow before cold-launch fixture routes; consider adding a visible-page assertion to future automation. |

- Decision:
  - Fix now as a grouped Details visual-system batch for first-viewport composition, media action focus, version/source focus, add-to sheet focus, and secondary poster rails.
  - Treat the Explore focus issue as part of the same batch if it can be solved through a local Details focus policy; otherwise document it as a separate interaction follow-up after the visual structure is changed.
  - Keep playback/native logic out of this batch.

### 2026-07-08 - Design Conformance Batch 03 Details And Metadata Actions Fix Rerun

- App version: 0.1.0.216.
- Scope: rerun the Details first viewport, source/version selection, organize sheet, and secondary media rails after visual-system fixes.
- Data source: DEBUG `details-fixture`; no private server data or personal media assets.
- Screenshot sets:
  - Primary rerun: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch03-details-rerun-20260708-024354`.
  - Atmosphere tuning rerun: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch03-details-rerun2-20260708-024826`.
  - Final first-viewport check: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch03-details-final-20260708-025051`.
- Keyboard-only validation:
  - Closed the existing `Next Gen Xbox Emby` ApplicationFrameWindow before each installed-app fixture launch.
  - `details-fixture` launch succeeded and `dev-command-result.txt` reported `completed / details-fixture`.
  - Captured the first viewport, source focus, selected fallback source, add-to sheet, and lower secondary rail state through keyboard/controller-equivalent navigation.
- Source contracts added before implementation:
  - Details uses a deterministic content/action column plus a right-side atmosphere zone, not a visible poster viewer.
  - Atmosphere artwork resolves through `Backdrop`, then `Thumb`, `Banner`, and `Primary`; when artwork is missing or visually weak, a black/matte fallback is acceptable.
  - Details action controls suppress system focus visuals and use matte fill/luminance as the primary focus cue.
  - Version/source selected state uses a neutral marker instead of green; green remains reserved for progress, playback, or watched-state signals.
  - Add-to sheet options and secondary media rails avoid bright full-perimeter focus frames.
  - Details recommendation cards follow the shared poster-card anatomy: poster artwork first, title/meta below the image.
- Automated verification:
  - Red path confirmed the new Details visual-system source contracts failed before implementation.
  - Targeted Details source tests passed: 3 total.
  - Full Core test suite passed: 477 total.
  - Visual Studio MSBuild Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.216_x64_Debug.msix`.
  - Signed and installed `NextGenEmby.App 0.1.0.216` for screenshot rerun.

Fix rerun findings:

| ID | Result | Page | Evidence | Outcome | Residual risk |
| --- | --- | --- | --- | --- | --- |
| DC-03.01 | Pass with concern | Details first viewport | `details-final-initial.png`. | The visible poster viewer is removed. The page now reads as a left content/action column with a right atmosphere zone. The right side intentionally stays quiet and can fall back to black/matte when no useful artwork exists. | The packaged QA wide artwork is very dark, so this fixture mostly validates composition rather than rich atmosphere rendering. Real Emby backdrops should still be checked with live media because native image loading and backdrop quality will vary. |
| DC-03.02 | Pass | Details action row | `details-final-initial.png`. | Details actions now use shared matte command styles and suppress the old complete system focus outline. Resume keeps a neutral graphite action fill instead of becoming an accent-colored CTA. | Native Xbox focus rendering may still require platform tuning if WinUI adds unexpected focus adorners. |
| DC-03.03 | Pass | Version/source selection | `details-rerun-source-focus.png` and `details-rerun-source-selected.png`. | Source focus uses matte fill, and selected source uses a neutral steel marker instead of green. Selection and focus are visually distinct without a bright full frame. | Longer localized stream labels should be checked for wrapping and clipping. |
| DC-03.04 | Pass with concern | Add-to collection sheet | `details-rerun-add-sheet.png`. | Add-to options use the same quieter matte option treatment as the newer command-sheet direction, without the previous bright option frame. | The sheet still has richer thumbnail anatomy than Sort/Filter. This is acceptable for choosing collections/playlists, but it should not drift into decorative glass. |
| DC-03.05 | Pass | Details secondary rails | `details-rerun-secondary-focus.png`. | Secondary media cards now share the poster-grid anatomy: image-first poster, title/meta below, matte focus treatment, no text trapped in an in-poster scrim. | The scripted Down route did not reliably land on every below-fold group, so this pass validates visual anatomy more than full section-to-section navigation. |
| DC-03.06 | Concern | Below-fold vertical focus | Rerun keyboard path toward secondary rails. | The page remains keyboard navigable through primary controls and sheets, but the Similar -> Explore -> Cast & crew vertical route still needs a dedicated interaction pass. | Keep as a follow-up interaction issue rather than mixing it into the visual-system commit. |
| DC-03.07 | Pass | Fixture automation reliability | Installed-app reruns for version 0.1.0.216. | Closing the old app frame before writing and launching fixture commands produced stable Details screenshots matching the requested route. | Future automation should add a visible-page assertion so a completed command result cannot be confused with a stale frame. |

- Decision:
  - Commit this batch as the Details page visual-system alignment.
  - Leave Explore/Cast vertical focus routing for a smaller interaction-focused pass.
  - Keep atmosphere rendering intentionally source-aware: use backdrop-derived mood when available, and black/matte fallback when Emby lacks useful artwork.

### 2026-07-08 - Design Conformance Batch 04 Playback OSD And Options Baseline

- App version: 0.1.0.216.
- Scope: run Batch 04 Playback before making visual fixes, covering the default OSD, transport strip, source/audio/subtitle options, and info panel.
- Data source: DEBUG `playback-options-fixture`; no private server data or personal media assets.
- Runtime evidence:
  - Screenshot attempt set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch04-playback-baseline-20260708-030052`.
  - UIA snapshot set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch04-playback-baseline-uia-20260708-030412`.
  - `dev-command-result.txt` reported `completed / playback-options-fixture`.
- Tooling limitation:
  - `CopyFromScreen` and `PrintWindow(PW_RENDERFULLCONTENT)` both captured the UWP content layer as black in this run.
  - UI Automation confirmed the fixture and overlay controls were present, so this baseline uses UIA text/bounds snapshots plus source inspection for evidence.
  - Future playback visual QA should prefer Computer Use / Windows Graphics Capture when available, or add an app-owned diagnostic screenshot surface if playback composition remains black to external capture.
- Keyboard-only validation:
  - Closed the existing app frame, wrote `dev-command.json` with route `playback-options-fixture`, and launched through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`.
  - Initial fixture state opened Playback with More drawer visible and Source focused.
  - Pressed `Escape`; More closed and default OSD stayed visible with More focused in the transport row.
  - Pressed `Right`; transport focus moved within the bottom row.
  - Pressed `M`; More reopened.
  - Pressed `Down` to Audio, `Down` to Subtitles, `Down` to Info, and `Enter` to open the info panel.

Findings recorded before fixes:

| ID | Severity | Page | Evidence | Expected | Actual | Proposed batch fix |
| --- | --- | --- | --- | --- | --- | --- |
| DC-04.01 | Fail | Playback default OSD | UIA `02-default-osd-after-escape.uia.txt` shows `Aurora Protocol`, progress slider, `State`, `Playing - Fixture options ready`, and the full transport row all packed into the bottom overlay. Source `PlaybackPage.xaml` uses one bottom `Border` with three stacked rows. | Default playback invocation should be compact chrome: optional top-left title/status capsule plus bottom transport strip inside safe area. Playback UI stays subordinate to video/subtitles. | The current default OSD is a large bottom information panel. Title, state, timeline, and controls all live in the same bottom block, so it reads like a debug/control surface rather than the A3 compact strip model. | Split playback chrome into a small top-left status/title capsule and a compact bottom transport strip. Keep default strip focused on timeline, chips, transport, subtitles/audio, and More. |
| DC-04.02 | Fail | Transport strip focus and density | UIA shows transport buttons as large text rectangles, for example `Pause` `183x78`, `More` `177x78`, and `Stop` `168x78`. Source uses default `Button` elements without a playback-specific compact focus style. | Focused transport targets should sit fully inside the strip with visible internal breathing room, use matte fill/luminance, and avoid bright complete focus frames. Expected target height is roughly 52px to 56px. | Buttons are usable but too tall and command-button-like for the compact OSD. They likely inherit generic button focus visuals instead of a playback-specific matte transport recipe. | Introduce shared playback transport button styles for compact icon/text or icon-only controls, disable system focus visuals where needed, and reserve internal strip padding for focus scale. |
| DC-04.03 | Concern | Timeline and progress | UIA shows a disabled `Slider` with no exposed current/duration time labels in the default OSD snapshot. Source places status text on the right instead of time labels around the timeline. | Playback progress should use muted green for active progress, expose current/duration time, and keep non-playback controls neutral. | Seek/progress behavior exists, but the visual anatomy is not the target TV transport anatomy. The default OSD prioritizes state text over time comprehension. | Replace the default slider anatomy with a compact timeline row that includes start/current and duration labels, with muted green active progress and neutral track. |
| DC-04.04 | Fail | Playback options / More | UIA `01-options-drawer-initial.uia.txt`, `05-audio-focus.uia.txt`, and `08-info-open.uia.txt` show Source, Audio, Subtitles, and Info are reachable, but they live in a full-height right drawer (`489x1974` at the captured scale). | Source, audio, subtitle, info, and more choices should open lightweight menus. They should not be flattened into the default OSD, and the menu should not compete with the video/subtitle field. | The options are correctly not flattened into the default strip, but the full-height drawer feels heavier than the playback design target and can dominate the video. | Rework More into a compact matte floating menu or short side sheet aligned to the bottom OSD, keeping Source/Audio/Subtitles/Info reachable through D-pad. |
| DC-04.05 | Blocked | Subtitle/video conflict | The deterministic `playback-options-fixture` exposes playback controls and menus, but no subtitle text/video baseline is rendered for measuring subtitle collision. | Subtitle baseline remains readable. Move or shorten OSD/menu before adding stronger blur, borders, or shadows. | Cannot verify subtitle collision from this fixture. Source-level review still suggests the large bottom OSD would reduce subtitle-safe space more than the compact target. | Add a deterministic playback visual fixture with subtitle sample text or a synthetic subtitle baseline so subtitle-safe OSD can be tested without private media. |
| DC-04.06 | Concern | Visual capture reliability | Screenshot files in `ngxe-batch04-playback-baseline-20260708-030052` and `ngxe-batch04-printwindow.png` captured a black app content layer while UIA saw all controls. | Playback visual QA should produce reliable evidence without depending on manual inspection. | Current capture method is weak for UWP playback composition. UIA can validate geometry and routes, but not material, contrast, or focus paint. | Keep UIA/source as fallback, but prefer Computer Use / Windows Graphics Capture for playback visual passes and consider a non-video fixture background for OSD screenshots. |

- Decision:
  - Fix now as a grouped playback visual-system batch for compact OSD structure, transport focus/density, timeline anatomy, and More menu weight.
  - Add source-level design contracts first because screenshot capture is unreliable for playback composition in this environment.
  - Keep actual native playback/stream switching behavior out of this visual batch.

### 2026-07-08 - Design Conformance Batch 04 Playback OSD And Options Fix Rerun

- App version: 0.1.0.219.
- Scope: rerun Batch 04 after compact playback OSD, matte transport focus, timeline anatomy, source/audio/subtitle chips, subtitle-safe sample text, and compact More menu changes.
- Data source: DEBUG `playback-options-fixture`; no private server data or personal media assets.
- Runtime evidence:
  - Screenshot: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch04-playback-fix-219-more.png`.
  - UIA snapshot set: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch04-playback-fix-219-uia-20260708-033058`.
  - `dev-command-result.txt` reported `completed / playback-options-fixture`.
- Keyboard-only validation:
  - Closed the existing app frame, wrote `dev-command.json` with route `playback-options-fixture`, and launched through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`.
  - Initial fixture state opened Playback with compact More visible and Source focused.
  - Pressed `Escape`; More closed and default OSD stayed visible with More focused in the bottom strip.
  - Screenshot capture now produced usable UI evidence for the synthetic matte fixture background. Live/native video composition may still need separate hardware capture.

Fix rerun findings:

| ID | Status | Page | Evidence | Result | Residual risk |
| --- | --- | --- | --- | --- | --- |
| DC-04.01 | Pass | Playback default OSD | `01-initial-more.uia.txt` and `02-default-osd.uia.txt` show top-left `Aurora Protocol` capsule at `114,135,303,44` plus a separate bottom transport strip. | The OSD is no longer one large bottom information panel. Title/status moved to the compact top-left capsule, and playback controls sit in the bottom strip. | Real playback may tune auto-hide timing and status copy separately from this fixture. |
| DC-04.02 | Pass | Transport strip focus and density | Bottom controls sit inside the strip: Pause `1526,1920,157,81`, Resume `1695,1920,180,81`, More `3564,1920,153,81`. Screenshot shows matte focus fill rather than bright complete outlines. | Transport now reads as TV playback chrome instead of a generic command row, with internal breathing room retained. | Xbox focus rendering should still be checked on hardware because WinUI theme focus can vary by platform. |
| DC-04.03 | Pass | Timeline and progress | UIA exposes current time `02:40`, duration `01:58:00`, and slider `267,1857,3285,48`. Screenshot shows time labels around the scrubber. | Timeline anatomy now prioritizes current/duration comprehension and keeps debug state out of the transport strip. | Seek preview behavior should be rerun with real seekable media in an interaction batch. |
| DC-04.04 | Pass | Playback options / More | More menu changed from the baseline full-height drawer (`489x1974`) to a compact bottom-aligned menu (`579x494`). | Source, audio, subtitles, and info remain reachable through D-pad, but no longer dominate the video field. Source focus is matte fill, not a bright white frame. | If the final native control cannot suppress ComboBox template visuals on Xbox, replace these with matte option rows plus lightweight flyouts. |
| DC-04.05 | Pass with concern | Subtitle/video conflict | Fixture screenshot displays `Subtitle sample stays above controls` between the video field and the bottom strip. | The synthetic subtitle baseline remains above the compact strip and is not occluded by More. | This is still a synthetic subtitle sample, not timed text over a live video renderer. Recheck with real subtitles and overscan on hardware. |
| DC-04.06 | Pass with concern | Visual capture reliability | `CopyFromScreen` produced usable evidence for `ngxe-batch04-playback-fix-219-more.png`. | Playback visual QA can now capture the synthetic fixture because it uses a matte app-owned visual field instead of only relying on native video composition. | Real video composition may still capture differently; keep UIA/source as fallback and prefer hardware/Windows Graphics Capture for native playback. |

- Decision:
  - Commit this batch as playback OSD visual-system alignment.
  - Keep native stream switching, real subtitle collision, and hardware focus rendering as follow-up validation, not as blockers for this visual-system slice.

### 2026-07-08 - Design Conformance Batch 05 Secondary Media Surfaces Baseline

- App version: 0.1.0.219.
- Scope: run Batch 05 before visual fixes, covering Live TV, Music, Photos, and the current source/destination model.
- Data source: DEBUG fixture routes only; no private server data, credentials, or personal media assets were written to the repository.
- Evidence root: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch05-secondary-baseline-20260708-033759`.
- Contact sheets:
  - Live TV: `contact-livetv.png`.
  - Music: `contact-music.png`.
  - Photos: `contact-photos.png`.
- Fixture routes:
  - `livetv-fixture`.
  - `music-fixture`.
  - `photos-fixture`.
  - `home-fixture` for the current Guide / destination-family check.
- Keyboard-only validation:
  - Closed the existing `Next Gen Xbox Emby` ApplicationFrameWindow before each installed-app fixture launch.
  - Wrote `dev-command.json` into the app LocalState folder and launched through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`.
  - Live TV path: `Down`, `Down`, `Down`, `Enter`, `Escape`.
  - Music path: `Down`, `Right`, `Right`, `Enter`, `Escape`, `Left`.
  - Photos path: `Enter`, `Right`, `Right`, `Enter`, `Escape`, `Escape`.
  - Guide / destination path: launched Home fixture and captured the current collapsed rail / page action model.

Findings recorded before fixes:

| ID | Severity | Page | Evidence | Expected | Actual | Proposed batch fix |
| --- | --- | --- | --- | --- | --- | --- |
| DC-05.01 | Fail | Live TV | `livetv-fixture\01-initial.png` through `06-after-escape.png`, `contact-livetv.png`, and UIA snapshots for focused channels and the unsupported layer. | Live TV should use dense matte channel/program rows, a source-aware current-program preview, and a transient unsupported layer that follows the newer matte command language. Focus should be visible without relying on a bright complete perimeter frame. | Channel navigation works and Escape restores focus to `Kids Studio`, but channel rows still use a bright full focus frame. The right preview reads as a large empty graphite information panel, and the unsupported playback layer uses the older centered modal plus bright outlined Close target. | Keep the dense channel-list structure, but migrate row focus to the shared matte list treatment, redesign the current-program preview as a compact media-first panel, and move unsupported playback messaging to a shared matte transient layer. |
| DC-05.02 | Concern | Music | `music-fixture\01-initial.png` through `07-back-album-or-artist.png`, `contact-music.png`, and UIA snapshots for Artist -> Album -> Song focus. | Music should preserve TV-readable density with list/column navigation where it is better than poster grids. Focus and empty/unsupported states should still follow the same matte visual system as the rest of the app. | The three-column browsing model is appropriate and keyboard paths work. Artist, album, and song focus restores correctly after the unsupported layer. Visual focus still uses bright complete outlines, the Now panel is sparse, and the unsupported layer shares the older modal style. | Preserve the three-column architecture, migrate list focus and the unsupported layer to shared matte styles, and tune the Now panel into a compact album/song context area instead of a static diagnostic panel. |
| DC-05.03 | Fail | Photos | `photos-fixture\01-root.png` through `07-album-escape-root.png`, `contact-photos.png`, and UIA snapshots for album, photo, viewer, and focus restoration. | Photos should prioritize immersive artwork and minimal chrome. Root/album grids can be dense, but they should occupy the TV canvas with photo-appropriate media tiles rather than generic poster-library anatomy. Focus should avoid bright perimeter frames. | Album opening, photo navigation, viewer opening, and Escape restoration all work. The viewer is close to the intended minimal immersive direction. Root and album pages still look like generic poster grids: small vertical cards cluster on the left with a large unused right side, and focused cards use bright full frames. | Add a photo-specific visual recipe: larger landscape or square media tiles, stronger use of the available canvas, matte focus without a perimeter frame, and viewer styling kept minimal. Reuse Library mechanics only where they do not force movie-poster proportions. |
| DC-05.04 | Concern | Guide / source destinations | `home-guide\01-guide.png` and `01-guide.uia.txt`. | The client should expose the full Emby destination family, including lower-frequency or server-defined surfaces, through a source-aware navigation model. If the current model is collapsed Guide only, the design docs and checklist should explicitly say that. | The collapsed left rail exposes Home, Search, Movies, Shows, Live TV, Collections, Playlists, Music, Photos, Favorites, Unwatched, and Settings. No explicit Source Hub or expanded More destination view was found in this fixture. Pressing the tested guide key path captured the collapsed rail and Home content rather than a separate source hub. | Product/design decision needed before implementation: either accept the collapsed Guide plus section More buttons as the current source model and update the docs, or add a Source Hub fixture/view for unpinned libraries and secondary routes. |

- Decision:
  - Treat Batch 05 as a secondary-media visual-system batch, not as a playback/native-feature batch.
  - Fix Live TV and Music together around the shared matte list focus and shared unsupported/transient layer.
  - Fix Photos with a photo-specific grid recipe instead of forcing the movie-poster card system.
  - Resolve the Source Hub question before code changes: the current app has a complete collapsed destination rail, but not a distinct Source Hub surface.

### 2026-07-08 - Design Conformance Batch 05 Secondary Media Surfaces Fix Rerun

- App version: 0.1.0.220.
- Scope: rerun Live TV, Music, Photos, and source-destination assumptions after secondary-media visual-system fixes.
- Data source: DEBUG fixture routes only; no private server data, credentials, or personal media assets were written to the repository.
- Evidence root: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch05-secondary-fix-220-20260708-040111`.
- Contact sheets:
  - Live TV: `contact-livetv.png`.
  - Music: `contact-music.png`.
  - Photos: `contact-photos.png`.
- Keyboard-only validation:
  - Signed and installed `NextGenEmby.App 0.1.0.220`.
  - Live TV path: `Down`, `Down`, `Down`, `Enter`, `Escape`.
  - Music path: `Down`, `Right`, `Right`, `Enter`, `Escape`, `Left`.
  - Photos path: `Enter`, `Right`, `Right`, `Enter`, `Escape`, `Escape`.
  - Fixture routes completed through `dev-command.json`; final result reported `completed / photos-fixture`.
- Source-level contracts added before implementation:
  - Live TV and Music list rows opt into shared matte button focus instead of system focus rings.
  - Live TV and Music unsupported playback messages use a shared bottom transient matte panel.
  - Photos use a photo-specific landscape tile recipe through `LibraryGridItem` dimensions instead of forcing poster-card proportions.

Fix rerun findings:

| ID | Status | Page | Evidence | Result | Residual risk |
| --- | --- | --- | --- | --- | --- |
| DC-05.01 | Pass with concern | Live TV | `contact-livetv.png`, `livetv-fixture\05-unsupported-layer.png`. | Channel focus now reads as a matte fill change without a bright complete perimeter frame. The current-program preview is compact and top-aligned instead of a tall empty graphite panel. Unsupported playback opens as a bottom transient matte panel and Escape returns to the invoking channel. | Preview content is still text-first because the fixture has channel logos and EPG text, not a real program artwork feed. Real Live TV servers should be checked for logo and program metadata density. |
| DC-05.02 | Pass with concern | Music | `contact-music.png`, `music-fixture\05-unsupported-layer.png`. | The three-column artist/album/song model remains dense and TV-readable. Artist, album, and song focus use matte fill rather than the old complete focus frame. Unsupported audio playback uses the same transient panel and Escape restores song focus. | The Now panel is intentionally text-first. A richer album-art panel can be added later, but it should not reduce list density or turn music into a poster grid. |
| DC-05.03 | Pass with concern | Photos | `contact-photos.png`, `photos-fixture\02-album-opened.png`, and `05-viewer-opened.png`. | Photos now use a landscape media-tile recipe with image-first artwork and metadata below. The viewer remains immersive and Escape restores to the photo, then the album root. | The fixture has only a few photos/albums, so the right side still has open canvas. That is acceptable for sparse folders, but real larger albums should be checked for wrap density. |
| DC-05.04 | Concern | Guide / source destinations | Batch 05 baseline `home-guide\01-guide.png`; no source code changes in this fix. | The current product model remains the collapsed left Guide plus page-level section actions. This exposes the full pinned destination family without introducing a new Source Hub surface. | A distinct Source Hub may still be useful for unpinned server-defined libraries, but it should be designed as a product-model decision rather than slipped into this visual fix. |

- Verification:
  - Red path confirmed the new Live TV, Music, and Photos source contracts failed before implementation.
  - Targeted source tests passed: `LiveTvPageSourceTests`, `MusicPageSourceTests`, and `PhotoViewerSourceTests`, 14 tests.
  - Design source tests passed: 75 tests.
  - Full Core test suite passed: 483 tests.
  - Visual Studio MSBuild Debug x64 build passed with 0 warnings and 0 errors.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.220`.
- Decision:
  - Commit this batch as secondary-media visual-system alignment.
  - Keep Source Hub as an explicit future product/navigation decision.
  - Keep real Live TV stream playback, real audio playback, and real server artwork density out of this visual batch.

### 2026-07-08 - Design Conformance Batch 06 Account, Settings, Login, And Recovery Baseline

- App version: 0.1.0.220.
- Scope: run Batch 06 before utility-surface visual fixes, covering Login, Settings, sign-out confirmation, and Search error recovery.
- Data source: DEBUG fixture routes and empty-session utility routes only; no private server data, credentials, screenshots, or personal media assets were written to the repository.
- Evidence root: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch06-utility-baseline-20260708-041330`.
- Contact sheets:
  - Login: `login\contact-login.png`.
  - Settings: `settings\contact-settings.png`.
  - Search error: `search-error\contact-search-error.png`.
- Fixture routes:
  - `login`.
  - `settings`.
  - `search-error`.
- Keyboard-only validation:
  - Closed the existing `Next Gen Xbox Emby` ApplicationFrameWindow before each installed-app route launch.
  - Wrote `dev-command.json` into the app LocalState folder and launched through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`.
  - Login path: `Tab`, `Tab`, `Tab`, `Enter`.
  - Settings path: `Up`, `Enter`, `Escape`.
  - Search error path: `Down`, `Down`, `Right`.
  - Fixture routes completed and screenshots/UIA snapshots were captured into the evidence root.

Findings recorded before fixes:

| ID | Severity | Page | Evidence | Expected | Actual | Proposed batch fix |
| --- | --- | --- | --- | --- | --- | --- |
| DC-06.01 | Fail | Login | `login\contact-login.png` and UIA snapshots `01-initial.uia.txt` through `05-empty-submit-error.uia.txt`. | Login should be TV-accessible and visually aligned with the matte utility system: calm panel, readable form scale, neutral focus, and failed login returning to an editable target. | Keyboard navigation reaches the fields and empty submit returns focus to the editable form, but the page still looks like a developer form. Controls are tiny and left-biased on a large black canvas, the Connect button uses default command styling, and TextBox/PasswordBox focus relies on bright perimeter lines. | Introduce a shared utility form surface: centered/left-balanced matte panel with TV-scale fields, neutral form focus resources, `TvCommandButtonStyle` for Connect, and clearer empty/error status placement. |
| DC-06.02 | Concern | Settings | `settings\contact-settings.png`, especially `01-initial.png` and `02-signout-focus.png`. | Settings should use shared typography, command focus, and panel surfaces. Diagnostics should be secondary and should not dominate the first viewport. | The account/playback/diagnostics structure is usable and already uses shared panels, but Sign out and checkbox focus still show old complete focus outlines. Diagnostics appears as a prominent first-screen panel instead of a secondary/support section. | Migrate settings buttons, checkbox, and combo-like utility controls to matte focus defaults. Reduce diagnostics visual priority or move it behind a secondary disclosure while keeping it reachable. |
| DC-06.03 | Concern | Sign out confirmation | `settings\03-confirm-layer.png` and `settings\03-confirm-layer.uia.txt`. | Destructive action should be explicit, local, and safe by default. Danger color should appear only inside the confirmation context. | The confirmation copy and scoped danger button are appropriate, and Escape cancels. However the buttons inherit old focus visuals, so the confirmation still visually belongs to the older utility styling. | Keep the centered confirmation model and safe default, but use matte command focus for both actions. Keep red only on the final destructive button. |
| DC-06.04 | Concern | Search error recovery | `search-error\contact-search-error.png` and UIA snapshots for initial, scope, recovery, and retry focus. | Error states should explain what happened and provide visible recovery controls without changing the app's visual language. | Recovery works: the page shows `Unable to search`, `Edit search`, and `Search again`, and focus can reach both actions. The search box, scope chips, and recovery buttons still use older border-heavy utility styling. | Apply the shared utility command/form focus recipe to Search input, scope chips, recent-term chips, and error recovery actions. Keep the centered recovery copy because it is clear and does not steal navigation. |

- Decision:
  - Treat Batch 06 as a utility-surface visual-system batch.
  - Fix Login, Settings, and Search recovery together around a shared neutral matte form/command style instead of one-off page tweaks.
  - Keep destructive sign-out red only inside the confirmation action.
  - Keep diagnostics reachable but visually secondary.

### 2026-07-08 - Design Conformance Batch 06 Account, Settings, Login, And Recovery Fix Rerun

- App version: 0.1.0.222.
- Scope: rerun Login, Settings, sign-out confirmation, and Search error recovery after shared matte utility form/command fixes.
- Data source: DEBUG fixture routes and empty-session utility routes only; no private server data, credentials, screenshots, or personal media assets were written to the repository.
- Evidence root: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch06-utility-fix-222-20260708-043420`.
- Contact sheets:
  - Login: `login\contact-login.png`.
  - Settings: `settings\contact-settings.png`.
  - Search error: `search-error\contact-search-error.png`.
- Keyboard-only validation:
  - Signed and installed `NextGenEmby.App 0.1.0.222`.
  - Login path: `Tab`, `Tab`, `Tab`, `Enter`.
  - Settings path: `Up`, `Enter`, `Escape`.
  - Search error path: `Down`, `Down`, `Right`.
  - Fixture routes completed through `dev-command.json`.
- Source-level contracts added before implementation:
  - Login uses shared matte utility form, field, and command styles, and Connect uses `MatteButtonFocusVisuals`.
  - Settings uses shared utility command/danger styles, a lower-weight diagnostics panel, and matte focus helpers for sign-out confirmation actions.
  - Search input, scope chips, recent terms, and error recovery actions use the same matte utility recipe; selected scope chips no longer use accent borders.

Fix rerun findings:

| ID | Status | Page | Evidence | Result | Residual risk |
| --- | --- | --- | --- | --- | --- |
| DC-06.01 | Pass with concern | Login | `login\contact-login.png`, especially `01-initial.png` and `05-empty-submit-error.png`. | Login now presents a stable matte utility panel with TV-scale fields and a neutral Connect action. Empty submit still returns focus to an editable field. | Native text-entry focus and software keyboard behavior should be checked on Xbox hardware. |
| DC-06.02 | Pass with concern | Settings | `settings\contact-settings.png`, especially `01-initial.png` and `02-signout-focus.png`. | Settings keeps the simple account/playback/diagnostics structure, but command focus now uses matte fill instead of old bright perimeter focus. Diagnostics is visually lower weight. | Diagnostics remains visible in the first viewport for local/debug usefulness; a future production build may hide it behind a secondary disclosure. |
| DC-06.03 | Pass | Sign out confirmation | `settings\03-confirm-layer.png`. | Confirmation remains explicit and safe by default. Red is scoped to the final destructive Sign out button, and focus is matte rather than a bright outline. | None for this visual slice. |
| DC-06.04 | Pass with concern | Search error recovery | `search-error\contact-search-error.png`, especially `03-recovery-focus.png` and `04-search-again-focus.png`. | Recovery controls remain centered, readable, and reachable. Search input, scope chips, and retry/edit actions now use matte utility styling and no accent focus border. | Real offline/server failures should be rerun after hardware text input validation. |

- Verification:
  - Red path confirmed the new Login, Settings, and Search source contracts failed before implementation.
  - Targeted source tests passed: `LoginAccessibilitySourceTests`, `SettingsPageSourceTests`, and `SearchAccessibilitySourceTests`, 12 tests.
  - Design source tests passed: 78 tests.
  - Visual Studio MSBuild Debug x64 build passed with 0 warnings and 0 errors, producing `NextGenEmby.App_0.1.0.222_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.222`.
- Decision:
  - Commit this batch as utility-surface visual-system alignment.
  - Keep Xbox hardware text-entry behavior, production diagnostics disclosure, and real offline-server recovery as follow-up validation rather than blockers for this visual-system slice.

### 2026-07-08 - Design Conformance Batch 03 Details Secondary Interaction Follow-Up

- App version: 0.1.0.222.
- Scope: rerun the remaining Batch 03 below-fold interaction concern after later details/poster/focus fixes.
- Data source: DEBUG `details-fixture`; no private server data or personal media assets.
- Evidence root: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch07-details-interaction-baseline-20260708-044207`.
- Keyboard-only validation:
  - Closed the existing app frame, wrote `dev-command.json` with route `details-fixture`, and launched through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`.
  - Pressed `Down` repeatedly from the default Details focus and captured screenshots plus UIA focused element names at every step.
  - Pressed `Up` from the Cast & crew focused state to verify reverse movement.
- Focus log:
  - Initial: primary action button.
  - `Down 1`: selected `4K SDR Direct` source.
  - `Down 2`: `1080p fallback` source.
  - `Down 3`: organize/action area.
  - `Down 4`: More like this item `Midnight Signal`.
  - `Down 5`: Explore facet `Browse Genre Sci-Fi`.
  - `Down 6`: Cast & crew item `Maya Chen, Lena Ortiz`.
  - `Up from Cast`: returned to Explore facet `Browse Genre Sci-Fi`.

Follow-up findings:

| ID | Status | Page | Evidence | Result | Residual risk |
| --- | --- | --- | --- | --- | --- |
| DC-03.06 | Pass with concern | Details below-fold vertical focus | `down-04.png`, `down-05.png`, `down-06.png`, and `focus-log.txt`. | The current route now moves predictably from More like this to Explore to Cast & crew, with all three sections visible and focused targets reachable by D-pad. Up from Cast returns to Explore. | Fixture cast/facet data is compact. Recheck with real Details items that have longer people names, more facets, and larger similar/cast rails. |
| DC-03.08 | Concern | Details atmosphere crop while below fold | `down-04.png` through `down-06.png`. | The right-side atmosphere remains non-interactive and does not block reading/focus. | The fixture's large Logo crop appears in the lower-right atmosphere zone and can become visually loud below the first viewport. This is not an interaction blocker, but real backdrop/logo crops should be sampled during live-media validation. |

- Decision:
  - Mark the Batch 03 below-fold interaction concern as resolved for the deterministic fixture.
  - Keep real-media metadata density and atmosphere crop behavior as future live-data validation, not as blockers for the current design-system pass.

### 2026-07-08 - Design Conformance Batch 02 Missing Poster Fallback Rerun

- App version: 0.1.0.224.
- Scope: close the remaining Batch 02 no-artwork runtime gap by adding a deterministic missing-poster item to the movie-grid and search fixtures.
- Data source: DEBUG `movies-fixture` and `search-fixture` only; no private server data, credentials, screenshots, or personal media assets were written to the repository.
- Evidence root: `C:\Users\yqzzx\AppData\Local\Temp\ngxe-batch02-missing-poster-fallback-224-20260708-050532`.
- Keyboard-only validation:
  - Closed the existing app frame, wrote `dev-command.json` with route `movies-fixture`, and launched through AppUserModelId `NextGenEmby.App_h8qjz0sr1sg4m!App`.
  - Pressed `Right` from the first Movies grid item to focus `No Poster Signal`.
  - Closed the app frame, wrote `dev-command.json` with route `search-fixture`, relaunched, then pressed `Down`, `Down`, `Down`, `Right` to focus the same missing-poster item in Search results.
  - Both fixture routes reported `completed` in `dev-command-result.txt`; `dev-command.json` and `dev-command-result.txt` were removed after validation.
- Screenshots:
  - Movies selected no-art tile: `movies-01.png`.
  - Search selected no-art result: `search-04.png`.
  - Focus log: `focus-log.txt`.

Follow-up findings:

| ID | Status | Page | Evidence | Result | Residual risk |
| --- | --- | --- | --- | --- | --- |
| DC-02.04 | Pass | Movies and Search no-artwork fallback | `movies-01.png`, `search-04.png`, and `focus-log.txt`. | `No Poster Signal` renders as an intentional quiet fallback surface with the `N` initials, title/meta below artwork, and the shared matte selected backplate. It does not use generated abstract art, a bright focus frame, or a glass/shadow treatment. | Real Emby libraries can expose missing artwork through different tag/item-id combinations; re-run against live saved-session items when one is available. |

- Verification:
  - Red path confirmed `DevelopmentHomeFixtureTests.Create_Includes_Deterministic_Movie_Without_Artwork_For_Fallback_Validation` and `DevelopmentSearchFixtureTests.CreateItemsForScope_Includes_NoArtwork_Result_For_Fallback_Validation` failed before fixture implementation.
  - Targeted fixture tests passed: `DevelopmentHomeFixtureTests` and `DevelopmentSearchFixtureTests`, 11 tests.
  - Design/source plus fixture tests passed: 89 tests.
  - Full Core test suite passed: 488 tests.
  - Visual Studio MSBuild Debug x64 build passed, producing `NextGenEmby.App_0.1.0.224_x64_Debug.msix`.
  - MSIX signed with the trusted `CN=NextGenEmby` certificate and installed locally as `NextGenEmby.App 0.1.0.224`.
- Decision:
  - Mark Batch 02 no-artwork runtime coverage as resolved for deterministic fixtures.
  - Keep live-server missing-artwork variants as a future data-shape validation, not a blocker for the current visual-system pass.
