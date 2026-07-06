# TV Fluent Interaction Polish Design

Date: 2026-07-06

## Context

The current Xbox Emby worktree already has a Fluent-inspired TV shell, Home, Library, Details, Search, Settings, and fullscreen Playback OSD. The remaining problem is that the interface still feels too large and fragile on a TV: global button defaults, title sizes, poster sizes, and page margins stack into an oversized desktop UI, while keyboard/gamepad navigation still has several places where focus can be hard to predict.

This pass keeps the native decoding and playback pipeline untouched. It only changes UI scale, focus behavior, navigation policy, documentation, packaging, and local Windows validation.

## Research Basis

- Android TV recommends designing against a 960x540 MDPI logical canvas, keeping primary UI within a roughly 5% safe area, and using predictable horizontal/vertical axes for remote navigation. Source: https://developer.android.com/design/ui/tv/guides/styles/layouts and https://developer.android.com/design/ui/tv/guides/foundations/navigation-on-tv
- Amazon Fire TV recommends avoiding the outer 5% edge area, keeping focused items and text inside the inner 90%, using less saturated colors, and prioritizing clear, simple, visual consumption flows. Source: https://developer.amazon.com/docs/fire-tv/design-and-user-experience-guidelines.html
- Roku frames TV UI quality as invisible, legible, forgiving, and entertainment-led: users sit around 10 feet away, need easy recovery, and should remain focused on content rather than operating the app. Source: https://developer.roku.com/dev/docs/key-design-principles
- Microsoft UWP/Xbox guidance says gamepad and remote are the primary 10-foot inputs, keyboard behavior maps to gamepad/remote behavior, and critical actions must not depend on gamepad-only buttons. Source: https://learn.microsoft.com/en-us/windows/uwp/ui-input/gamepad-and-remote-interactions
- Xbox Accessibility Guideline 113 requires focus indicators to remain clear, visible, and on-screen, including when dialogs or overlays are open. Source: https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/113
- Fluent 2 elevation and motion should create hierarchy and explain state changes, not decorate. Source: https://fluent2.microsoft.design/elevation and https://fluent2.microsoft.design/motion

## Design Decision

Use a **compact TV cinema shell**: a 960x540-derived scale applied to UWP effective pixels. The app keeps a dark Fluent material language, but reduces oversized desktop defaults so a 1080p/4K TV shows more content with less effort.

The signature element is a **focus rail**: focused media cards and OSD controls get a stable cyan border plus slight elevation, without growing layout bounds. This honors Fluent reveal/elevation while avoiding TV focus jumps.

### Second Visual Polish Direction

The first compact pass improved proportions but still read as an engineering prototype: flat dark rectangles, default UWP button chrome, sparse library rows, and focus indicators that looked like system debug outlines rather than part of a living-room product.

The second pass uses a **cinema glass + poster wash** direction. Real media imagery becomes the visual material: hero and detail pages use artwork as a low-opacity backdrop under a dark Fluent glass layer, while cards keep a consistent cyan focus rail and a stable poster aspect ratio. The app should feel closer to a streaming console experience than to a desktop admin surface.

Design critique against the brief:

- Avoid a generic dark dashboard by using actual Emby artwork as the memorable visual layer.
- Keep the palette mixed and restrained: cyan is for focus, green is only for Play/Resume, amber is reserved for warnings or media metadata accents.
- Replace default control appearance with a calmer Fluent TV chrome: translucent button surfaces, hairline borders, and focus thickness that is visible from couch distance.
- Make Library a vertical poster wall instead of a one-row horizontal strip, because collection browsing needs scan density and obvious D-pad movement.
- Keep the aesthetic risk in one place: the poster wash. Do not add decorative orbs, unrelated gradients, or marketing-style hero cards.

Because there is time for a more complete pass, this implementation should not stop at making controls smaller. The recommended path is:

1. Establish a reusable compact TV scale and shared focus affordances.
2. Rework every primary page to fit that scale.
3. Make focus entry and return deterministic on Home, Library, Details, and Playback.
4. Preserve the simple daily route: resume from Home, browse Movies/TV, open Details, play, adjust playback, back out one level.
5. Validate through keyboard routes that map to controller buttons.

### Dynamic Emby Home Architecture

The provided Terminus+C reference image exposed an important gap: a mature Emby client does not treat Home as a fixed "Movies + TV" launch screen. It synthesizes a living media hub from the user's actual server configuration: media libraries, continue watching, popular movies, popular series, curated server home sections, latest items per library, and "More" entry points for each collection.

This pass therefore treats Emby as the source of the home information architecture:

- `GET /Users/{UserId}/Views` defines the Media Libraries rail. Every returned library becomes a TV-focusable card, including custom libraries such as Douban/Netflix/anime/action collections rather than only built-in Movies and TV.
- Library cards should consume the library item's own artwork before falling back to child media artwork. For wide TV cards the priority is `ImageTags.Thumb` / `ParentThumbItemId`, then `BackdropImageTags` / `ParentBackdropItemId`, then `ImageTags.Primary` / `PrimaryImageItemId`, and only then latest child item artwork.
- `GET /Users/{UserId}/Items/Resume` defines Continue watching and the hero item.
- `GET /Shows/NextUp` defines Next up for series continuity.
- `GET /Users/{UserId}/HomeSections` plus `GET /Users/{UserId}/Sections/{SectionId}/Items` defines server-configured home rows. Duplicate system rows such as Continue watching/Next up are skipped because the app has stronger local presentation for those.
- `GET /Users/{UserId}/Items/Latest?ParentId={libraryId}` defines both the Media Libraries artwork preview and "Latest in {library}" rows.
- `GET /Users/{UserId}/Items?ParentId={libraryId}&SortBy=PlayCount&SortOrder=Descending` provides popular fallback rows such as Hot Movies, Hot TV Series, and Popular in custom libraries when the server home sections are sparse.

The visual model is still Fluent TV, not a mobile clone of the screenshot. The borrowed principle is the content taxonomy: rich, data-driven rails that reflect the server. The local visual answer is a wide TV hero, a horizontally-scannable media library rail, poster rows with "More" actions, and deterministic D-pad focus between hero, libraries, and row content.

Keyboard/gamepad consequence:

- Down from Hero moves to the first library card.
- Down from a library card moves to the first card in the first content row, not just page-scroll.
- Up from the first content row returns to the library rail.
- Left/Right on the library rail stays within real library cards.
- More buttons open a filtered Library page using either `SectionId` for server home sections or `ParentId` for media libraries.

### Token System

- Background: `#05080D`, slightly darker than the current cinema black to keep video/artwork dominant.
- Surface: `#0E1621`, used for shell bands and OSD glass.
- Raised surface: `#162231`, used for cards, drawers, and focusable panels.
- Focus cyan: `#3BD5FF`, reserved for focus borders and current navigation state.
- Play green: `#61D47C`, reserved for Play/Resume only.
- Warning amber: `#E4B84C`, reserved for actionable warnings and not normal chrome.
- Hairline: `#2A3B4E`, used for card and shell edges.
- Chrome glass: `#B30A1018`, used for default button surfaces and TV control bands.

Typography remains Segoe UI / Segoe UI Variable. Page titles become 32-36 effective px instead of 42-52. Controls become 52 high by default instead of 64-68. Content metadata uses 14-18, with body copy capped around 20-22 on TV pages.

## Layout Rules

### Shell

The shell header becomes a compact two-row rail inside the 5% TV safe area:

```text
| Next Gen Xbox Emby                         [settings] |
| [Home] [Movies] [TV] [Search]                         |
|-------------------------------------------------------|
| page content                                          |
```

The shell must not consume more than about 116 effective pixels on a 1080p desktop window. Playback continues to hide all shell chrome.

Shell focus rules:

- Home/Movies/TV/Search/Settings keep one stable visible focus target.
- Returning from a child page restores focus to the relevant shell entry only when the child did not already place focus inside its own page.
- The shell never handles back when a child page already handled the event.

### Home

Home keeps the existing hub structure but reduces visual weight:

- Hero height around 220 instead of 260.
- Poster preview around 180x220 instead of 250x210.
- Media row cards around 168x246 instead of 220x310.
- Horizontal rows keep focus visible and stable; focus rings must not resize rows.
- Library entry buttons become compact content tiles that sit in the same focus rhythm as media rows.
- After load, focus starts on the best daily action: Resume/Play if a playable hero exists, otherwise Movies.
- Refresh failures must leave focus on Refresh and show a short actionable status, not a dead page.

### Library

Library should feel like a TV grid, not a large desktop gallery:

- Keep sort/filter visible, but shrink each ComboBox to about 220 wide.
- Grid poster cards around 168x250.
- Grid rows should fit at least five columns in a 1080p window after safe margins.
- Arrow/D-pad movement must stay predictable and never send focus to hidden controls.
- Sort/filter changes reload without leaving focus on a disabled control after completion.
- Empty/error states place focus on Refresh.
- If the first grid item exists, Library should focus it after load so the user can browse immediately.

### Details

Details should make the primary play decision fast:

- Poster around 240x360 instead of 320x480.
- Title around 40, metadata around 18, overview around 20.
- Play/Resume receives initial focus after data load.
- Versions, audio, subtitles, and episodes remain available but should not crowd the first viewport.
- B/Escape/GoBack returns exactly one level.
- If a details reload fails, the last known title remains visible and Play is disabled only when the current item is not playable.
- Episodes and version rows use full-width stable buttons so vertical D-pad movement is obvious.

### Playback

Playback remains true fullscreen video. OSD becomes shorter and denser:

- Bottom OSD padding around 40x22, title 24, status 18-20.
- OSD command buttons become mostly icon-first with short labels.
- More drawer width around 360-380 instead of 420.
- A/Enter/Space shows OSD or confirms a seek preview.
- B/Escape/GoBack cancels seek preview, closes More, hides OSD, or exits playback in that order.
- Menu/M opens More.
- D-pad left/right performs explicit small-step seek only when playback is seekable.
- Left thumbstick preview seek remains cancellable; hidden OSD thumbstick movement must not change playback position.
- Closing More returns focus to More.
- Hiding OSD returns focus to the page root or video surface, not an invisible drawer control.
- The overlay auto-hide timer does not hide while More, seek preview, or manual direct-stream debug is active.

## Complete Implementation Scope

This pass should include the following improvements, in priority order:

1. Shared compact scale resources and high-visibility focus defaults.
2. Compact shell and page layouts.
3. Deterministic focus after page loads and after overlay close.
4. Playback overlay input policy tests and implementation.
5. Home/Library/Details runtime layout fixes in both XAML and dynamic code-behind card creation.
6. Keyboard-only local smoke using real installed app where possible.
7. Documentation of any limitation that cannot be automated locally, especially true XInput injection.

## Bug-Fix Priorities

1. Replace page-level oversized constants with reusable compact TV resources.
2. Add regression tests for overlay focus/input decisions that currently depend on code-behind state.
3. Make More drawer close leave focus on `MoreButton`, so the next D-pad move starts from a visible element.
4. Keep overlay pinned while More is open, while seek preview is active, and while manual debug panel is visible.
5. Ensure page focus is restored after load/refresh to visible controls.
6. Package and validate locally without mouse clicks; use keyboard keys that map to gamepad behavior.

## Acceptance Criteria

- Core tests pass.
- UWP Debug x64 build and MSIX packaging pass.
- Local installed app can be driven with keyboard: Home -> Movies -> Details -> Playback -> OSD -> More -> back/close flows.
- No mouse click is required during validation except if a tool limitation prevents launching or stopping; such exceptions must be documented.
- Home, Library, Details, Playback OSD, and More drawer screenshots show compact TV proportions with no obvious overlap.
- Core decoding/playback files under `src/NextGenEmby.Native` and native playback backend logic are not changed except for rebuild artifacts.
