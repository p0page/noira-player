# Xbox Fluent Emby Complete Client Design

Date: 2026-07-06

## Goal

Build a feature-complete, beautiful, controller-first Emby client for Xbox and TV-class Windows devices. The app should feel like a native Xbox Fluent media console, not a desktop app stretched onto a television.

This design keeps the current authentication compatibility and playback validation behavior. It does not introduce Emby Premiere enforcement, official-client purchase gates, or transcoding flow. It also keeps the native decoding and direct playback pipeline out of scope unless a UI change needs to pass an existing playback request through unchanged.

## Product Subject

Subject: a personal media library viewed from a couch, driven by Emby server metadata.

Audience: users with a real Emby server, custom libraries, collections, playlists, partially watched content, multiple versions, audio tracks, subtitles, and possibly Live TV.

Single job: make the next thing to watch obvious, while keeping the full library within a few predictable controller moves.

## Research Basis

- Xbox Dashboard and Xbox Guide: fast horizontal navigation, stacked UI layers, pinned content, bumper/D-pad friendly tab movement, and clear current focus.
- Xbox Cloud Gaming Web preview: console-like navigation inside a cross-device app surface, with a refreshed navigation model suitable for desktop, TV, and handheld.
- Microsoft Movies & TV on Xbox: large poster/backdrop media surfaces, dark 10-foot layout, default focus on Play/Resume, and minimal text density.
- Microsoft gamepad and remote guidance: keyboard navigation must map cleanly to D-pad, A/Select, B/Back, and Menu. Focus must be visible, predictable, and recoverable.
- Android TV, Fire TV, and Roku guidance: keep content inside safe areas, make focus obvious from 10 feet, minimize decisions before watching, and avoid hidden hover-only behavior.
- Netflix, YouTube TV, Apple TV, Hulu, Plex, Emby Android TV, Jellyfin Android TV, and Infuse: mature TV apps combine a daily home surface, quick global navigation, artwork-rich rows, strong library customization, and side panels that preserve context.

## Visual Direction

Name: Xbox Guide Media Console.

The UI uses a dark Fluent shell with a recallable left guide rail, cinematic artwork washes, dense but calm rails, and a strong focus rail. The app should feel closer to Xbox Dashboard plus Microsoft Movies & TV than to a web dashboard.

The signature visual element is the artwork lane: focused content projects a wide backdrop wash behind the active region while the card itself receives a crisp cyan focus frame. This gives the screen personality through the user's own media art instead of decorative gradients.

Self-critique:

- This avoids the generic one-color dark dashboard by letting server artwork carry the surface.
- Cyan is reserved for focus and active navigation, not general decoration.
- Green is reserved for Play/Resume.
- Amber is reserved for warnings, progress chips, and attention states.
- Acrylic and reveal effects are used only to clarify layers and focus.

## Tokens

Color:

- Canvas black: `#05080D`
- Console graphite: `#0C121A`
- Acrylic surface: `#CC101923`
- Raised surface: `#172230`
- Hairline: `#2A3B4E`
- Focus cyan: `#3BD5FF`
- Play green: `#61D47C`
- Progress amber: `#D9A441`
- Text primary: `#F4F7FA`
- Text secondary: `#AAB7C5`

Type:

- Display and page titles: Segoe UI Variable Semibold.
- Body and metadata: Segoe UI Variable Regular.
- Utility labels and debug/status: Segoe UI Variable Semibold with small caps avoided.

Scale:

- Design from a 960x540 TV logical canvas, then scale through UWP effective pixels.
- Keep primary controls inside a 5 percent safe area.
- Default controller target height: 52 effective px.
- Compact icon button: 52x52.
- Card corner radius: 6 to 8 px.
- Focus frame: 2 px default, 3 px on large hero cards.

## Primary Navigation

Use a left Xbox Guide style rail.

Collapsed state:

```text
avatar
home
search
movies
shows
live
collections
music
photos
settings
```

Expanded state appears when focus enters the rail or the user presses Menu from non-playback pages. It shows labels and profile/server status, but does not take the user away from the current page.

Keyboard/controller mapping:

- Arrow keys: D-pad.
- Enter and Space: A/Select.
- Escape and BrowserBack: B/Back.
- M and GamepadMenu: Menu.
- PageUp/PageDown or shoulder-equivalent input, where available: jump between major rails or tabs.

Back behavior:

- B closes the topmost transient layer first: menu drawer, filter sheet, details sub-sheet, OSD, or modal.
- If no transient layer is open, B returns exactly one navigation level.
- B never performs two back actions from one physical press.

## Emby Capability Model

The client should align with Emby standard capabilities while keeping compatibility gates unchanged.

Home data:

- `/Users/{UserId}/Views`
- `/Users/{UserId}/Items/Resume`
- `/Shows/NextUp`
- `/Users/{UserId}/HomeSections`
- `/Users/{UserId}/Sections/{SectionId}/Items`
- `/Users/{UserId}/Items/Latest`
- `/Users/{UserId}/Items?SortBy=PlayCount`

Library data:

- Movies, series, episodes, seasons, collections, playlists, music albums, artists, songs, photos, folders, genres, people, studios, tags, favorites, watched/unwatched filters, resume filters, sort orders, and recursive browsing.

Details data:

- Primary item metadata, overview, genres, people, runtime, production year, community rating, critic rating when available, user rating, played/favorite state, media sources, versions, audio streams, subtitle streams, seasons, episodes, similar items, collections, and playlists.

Playback data:

- Direct playback through current playback path.
- Version selection.
- Audio stream switching.
- Subtitle stream switching.
- Resume position.
- Playback progress reporting.
- Stop reporting.
- No Emby transcoding design in this phase.

Live TV data:

- Channels.
- Guide.
- Programs.
- Recommended programs.
- Recordings.
- Timers and series timers as browsable surfaces when the server supports them.

Settings data:

- Server/session status.
- TV mode scale.
- Playback diagnostics toggles already present.
- Resume behavior.
- Artwork quality.
- Input diagnostics.

## Artwork Strategy

Hero banner:

1. Item `Backdrop`.
2. Item `Thumb`.
3. Item `Banner`.
4. Item `Primary` with dark crop and blur fallback.
5. Generated local mosaic from the first 3 to 5 row items.

Library and section wide cards:

1. View or collection `ImageTags.Thumb` with `ParentThumbItemId` when present.
2. `BackdropImageTags` with `ParentBackdropItemId`.
3. `ImageTags.Banner`.
4. `ImageTags.Primary` with `PrimaryImageItemId`.
5. Child item backdrop mosaic.
6. Text-only acrylic fallback.

Poster cards:

1. `Primary`.
2. `Thumb`.
3. `Backdrop`.
4. Text-only fallback.

Logo overlay:

- Use `Logo` only as an optional overlay for focused hero/details when it improves readability.
- Never require `Logo` for core navigation.

API query guidance:

- Prefer explicit image query flags where supported: `EnableImages=true`, `EnableImageTypes=Primary,Backdrop,Thumb,Banner,Logo`, and `ImageTypeLimit=1`.
- Keep image URL construction compatible with the existing `GetImageUrl(session, itemId, imageType, maxWidth)` path.

## Home Screen

Home is the daily route. It should answer: continue, next episode, new arrivals, recommended sections, and library entry.

Layout:

```text
left guide | top status/search
           | focused hero with backdrop wash and Play/Details
           | Continue Watching
           | Next Up
           | Media Libraries
           | Server Home Sections
           | Latest by Library
           | Live TV / Collections / Playlists when available
```

Behavior:

- Initial focus goes to Resume/Play on the hero when playable.
- Down from hero goes to the first visible rail.
- Up from the first rail returns to the hero action.
- Right/Left move inside a rail without stealing focus to hidden scrollbars.
- More opens the matching library, section, or query result.
- Empty rows do not render.
- Loading failures keep a visible retry target.

## Library Screen

Library is for deliberate browsing.

Required surfaces:

- Poster/grid view.
- Wide card view for collections and folders.
- Sort, filter, and genre sheets.
- Watched, unwatched, favorites, resume, recently added, release date, name, rating, runtime.
- View switching between all item types supported by the current library.

Behavior:

- Entering a library focuses the first content card, not the toolbar.
- Up from the first row enters controls.
- Down from controls returns to the previous content anchor.
- Sort/filter opens a controller-friendly sheet, not a tiny ComboBox.
- Changing sort/filter returns focus to the first stable result.
- Empty state focuses Retry or Clear filters.

## Details Screen

Details is a decision screen, not a text page.

First viewport:

- Backdrop wash.
- Poster.
- Title or logo.
- metadata line.
- Play/Resume as default focus.
- More actions: favorite, watched, versions, subtitles, audio, add to playlist/collection when available.

Below:

- Seasons and episodes for series.
- Similar items.
- People.
- Collections.
- Technical media details behind an Info sheet.

Behavior:

- A on Play starts or resumes.
- Down from Play goes to versions or episodes.
- B returns one level.
- Stream selection changes the launch request but does not start accidental playback unless the user selects Play.

## Playback OSD

Playback remains a true fullscreen video surface.

OSD layers:

- Bottom compact transport.
- Seek preview.
- More drawer.
- Info/debug drawer.
- Track/version sheets.

Behavior:

- A/Enter/Space: show OSD or confirm current focused control.
- Left/Right: seek when OSD is active and playback is seekable.
- Menu/M: open More.
- B/Escape: cancel seek preview, close sheet, close More, hide OSD, or exit playback in that order.
- OSD never auto-hides while More, seek preview, manual diagnostics, opening, failed, or needs-attention states are active.
- Hidden OSD thumbstick or key drift must not change playback position.

## Search

Search should be usable from a remote.

Required:

- Text search with keyboard.
- Filter chips by Movies, Shows, Episodes, Collections, Playlists, People, Music, Photos, Live TV where server results exist.
- Recent searches.
- Empty and error states with visible focus target.

Behavior:

- Enter submits.
- Down enters results.
- B closes keyboard/search overlay first, then returns.

## App Icon Direction

Icon name: Library Portal.

Concept:

- A dark rounded-square tile.
- A luminous cyan/teal play portal cut through layered media cards.
- A small warm amber progress arc at the bottom edge.
- No Emby logo, Xbox logo, Microsoft logo, film-strip cliche, or generic play triangle alone.

Rationale:

- The layered cards represent personal libraries.
- The play portal represents direct couch playback.
- Cyan matches the focus rail.
- Amber hints at resume/progress.

Required assets:

- `StoreLogo.png`
- `Square44x44Logo.png`
- `Square150x150Logo.png`
- `Wide310x150Logo.png`
- `SplashScreen.png`

Validation:

- Icon remains legible at 44 px.
- Wide tile does not look like a banner ad.
- Splash screen matches the dark Fluent TV shell.

## Non-Goals

- Do not rewrite native decoding.
- Do not add Emby transcoding.
- Do not enforce Premium validation.
- Do not require mouse hover.
- Do not hide primary operations inside context menus only.

## Acceptance

The design is acceptable only when the interaction checklist in `docs/qa/emby-tv-client-keyboard-checklist.md` can be completed with keyboard input that maps to controller behavior, with no app-content mouse clicks and no obvious focus, scale, overlap, or dead-end issues.
