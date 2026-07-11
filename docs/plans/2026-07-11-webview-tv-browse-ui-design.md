# WebView TV Browse UI Design

## Goal

Build the first complete TV browsing flow for Noira's React/Vite WebView2 shell: Home, library grid, item details, and return-to-origin focus restoration. All catalog UI stays in WebView2. The existing native playback page remains the playback surface.

The implementation uses real Emby data. It does not add a fictional media fixture mode, fake posters, or a demo catalog.

## Scope

This milestone includes:

- a quiet source-aware shell with a collapsed and expanded Guide;
- Home rows for Continue Watching, Next Up, My Media, and recently added media;
- a poster-based library grid;
- an artwork-backed details page with Play or Resume as its default focus;
- deterministic directional navigation and focus restoration across the full flow;
- keyboard arrow, Enter, and Escape validation as the development equivalent of gamepad input;
- the existing bridge into the native playback page.

Search, settings, text input, full source customization, and playback-page redesign are deferred. The Guide must not expose dead entries for deferred routes.

## Visual Direction

`docs/DESIGN.md` is the visual source of truth. The implementation uses its current Artwork-Backed Matte Fluent tokens, including the restrained soft-violet playback and progress signal.

The supplied peer-product screenshot informs density, source grouping, mixed horizontal and poster rows, and titles below artwork. It is not copied literally. In particular, Noira does not keep a wide desktop sidebar or long search box permanently visible.

The shell uses a 960 by 540 logical TV canvas with a 56 pixel safe margin. A roughly 72 pixel icon Guide remains quiet during browsing. Pressing left from the first content column expands a roughly 248 pixel matte overlay without reflowing the content canvas. Moving right or pressing Escape closes it and restores content focus.

Home has no oversized marketing hero. The first viewport shows a complete high-value row and the beginning of the next row. Continue Watching and Next Up use stable wide cards. Movie, series, collection, and library browsing rows use stable poster cards when their semantics call for posters.

Card titles sit below artwork. A second line may show year or concise metadata. Resume progress remains attached to the image. Artwork, title, and metadata form one focus target. Focus uses reserved scale space, luminance lift, surrounding dimming, and an integrated matte backplate; it does not use a bright perimeter frame or drop shadow.

The library page uses a stable responsive poster grid. The details page places title, metadata, overview, and actions on the left and a real Backdrop, Thumb, Banner, or Primary-derived atmosphere zone on the right. Missing artwork falls back to graphite, not generated decoration. Play or Resume is the details default focus.

## Focus Architecture

`@noriginmedia/norigin-spatial-navigation` is the spatial calculation engine. No page consumes its service API directly. A Noira-owned `FocusNavigationPolicy` provides the application contract and keeps the third-party engine replaceable.

The policy owns:

- input normalization for left, right, up, down, select, back, and future menu commands;
- engine initialization, real DOM focus, key repeat throttling, and layout measurement;
- route focus history and per-scope last-focused-child state;
- default focus, restore focus, and fallback focus decisions;
- overlay and Guide focus boundaries;
- scrolling focused cards into a TV-safe viewport;
- pause and restore behavior while native playback owns input.

Pages declare only focus scopes, stable focus keys, default targets, allowed exits, and activation behavior. Initial scopes are Guide, each Home row, library grid, details actions, and page root.

Home defaults to the first item in Continue Watching, then Next Up, then My Media. Library defaults to its restored item or first card. Details defaults to Play or Resume.

Horizontal rows stop at their right edge. Left from their first item opens the Guide. Up and down choose the nearest horizontal candidate in the adjacent row. Grid edges do not leak into unrelated controls. The Guide is a temporary focus boundary; right or Escape returns to the saved content target.

Back priority is:

1. close the expanded Guide or active Web overlay;
2. return from details to its source library item;
3. return from a library to its Home source target;
4. hand Home-level back to the native host.

When a refresh removes a focused item, fallback order is nearest item in the same scope, first item in that scope, then the page default. Async data completion never steals focus after the user has moved it.

## Web And Native Boundary

All login, Home, library, and details UI runs in React inside WebView2. Native code owns durable credentials, the narrow metadata fallback bridge, and playback.

During development, arrow keys, Enter, and Escape drive the same policy used by controller-equivalent commands. The Web input adapter listens for DOM key events. The policy also accepts normalized commands from the native bridge so Xbox hardware support does not depend solely on undocumented WebView key forwarding behavior.

Before invoking `playback.nativePlayItem`, Web saves an in-memory focus snapshot and pauses Web focus handling. The native playback page owns all input while visible. Returning to Web restores the details route and its Play or Resume target. No playback control is duplicated in React.

## Navigation State

A small typed navigation store replaces the current single `View` string. Routes carry only the state required for the current session:

- `home`;
- `library` with its source identifier and Home origin focus key;
- `details` with its item identifier and library origin focus key.

Navigation and focus history stay in memory. Credentials, catalog payloads, media identifiers, and focus snapshots are not persisted to browser storage.

## Real Emby Data Flow

React continues to own catalog URLs, DTO mapping, and view models. Initial Home loading requests these endpoints in parallel:

- `Users/{userId}/Items/Resume` for Continue Watching;
- `Shows/NextUp` for Next Up;
- `Users/{userId}/Items/Latest` for recent media;
- `Users/{userId}/Views` for My Media.

The first render waits for the core request group to settle so rows do not insert above active focus. Per-library Latest rows load as a second phase and append below existing rows without reordering them. Library and details data load on demand.

The Web DTO adds the artwork and metadata required by the approved card and details families, including Primary, Thumb, Backdrop, Banner, production year, runtime, series context, user data, and media-source hints. Image URLs retain the existing in-memory authenticated convention and are never logged.

Browser fetch remains first. The native CORS fallback remains a narrow JSON GET bridge. Its allowlist is expanded only for the exact current-user Views, Items, Items/Latest, Items/Resume, and Shows/NextUp contracts. Shows/NextUp must validate that its `UserId` query matches the saved session. The bridge must not become a generic proxy.

Every load has a generation or cancellation identity. A superseded response cannot overwrite the active route. Refresh preserves rendered content and focus until replacement data is ready.

## Failure Handling

Authentication failures present an actionable re-login state. They never fabricate data. Network failures preserve already rendered content and expose retry without moving focus unexpectedly.

Home rows fail independently. A failed optional row does not blank the whole page. Empty rows are omitted. A failed image uses a matte text fallback. A failed required route keeps the user on the current route with Retry and Back targets placed by the same focus policy.

Loading, empty, and error states reserve stable geometry where practical. Labels, progress, errors, and focus effects do not resize cards or move neighboring targets.

## Private Test Configuration

Local credentials may be stored only in the worktree-local ignored file `.private/emby-test.local.env`. The entire `.private/` directory must be ignored and verified with `git check-ignore` before the file is created. The file is restricted to the current Windows user.

The local file is for QA and re-login helpers only. Production application code continues to use the native `PasswordVault`. Secrets are never exposed through `VITE_*`, copied into `dist` or AppX output, written to logs, added to screenshots, or included in test reports.

A privacy guard checks tracked files, Web build output, and QA artifacts for the locally supplied private values without printing those values.

## Testing

No new fictional media fixture mode is introduced.

Pure focus-policy unit tests use anonymous geometry and route keys because they test algorithms rather than media content. API transport and DTO unit tests remain hermetic. Product UI, visual validation, keyboard traversal, and end-to-end navigation use the real Emby server through the real WebView2 session.

The automated and manual validation matrix covers:

- default focus on every non-empty Home composition;
- Guide open, traverse, close, and focus restoration;
- horizontal row edges and nearest-column vertical movement;
- library grid edges, scrolling, and target visibility;
- Home to library to details traversal and exact Back restoration;
- details to native playback and return-to-Play restoration;
- refresh, removed target, empty row, image failure, partial request failure, and expired session;
- 1920 by 1080, 1280 by 720, and 960 by 540 logical layout checks;
- TV safe area, title placement, focus scale envelope, text overflow, and overlap.

Real-data automation selects structural targets such as the first non-empty row and first playable item. It records only anonymous counts and pass or fail states. Real-data screenshots stay in an ignored private artifact directory and are not attached to commits or responses. Layout assertions use element bounds rather than content-dependent golden pixel matching.

The regular verification batch includes Web tests, TypeScript checking, a production Web build, focused Core tests, a Debug x64 WebView2 build, and one complete keyboard traversal. Windows keyboard evidence is reported as Windows WebView2 evidence. Xbox controller behavior remains unproven until a separate hardware pass succeeds.

## Acceptance Criteria

- The packaged or HMR-hosted WebView loads real Home rows through the saved native session.
- Home, library, and details share the approved Artwork-Backed Matte Fluent system.
- Card titles render below artwork and remain readable at TV distance.
- Directional input never leaves the application without a focus target.
- Guide, row, grid, details, refresh, and Back behavior follow the policy above.
- Returning from details restores the exact source card when it still exists.
- Returning from native playback restores details Play or Resume.
- Layout is free of overlap and focus-induced reflow at all required TV viewports.
- No private credential or catalog value is committed, logged, or included in shared QA output.
- Existing native playback behavior and bridge security contracts do not regress.
