# Artwork-Backed Matte Fluent Design Conformance Checklist

Date: 2026-07-07

This checklist turns `docs/DESIGN.md` and the A3 render targets into an executable review path. The operation matrix proves the client can perform Emby tasks; this file proves those tasks still look and feel like the agreed Xbox/TV design system while they are performed with keyboard/controller input.

## Source Of Truth

- `docs/DESIGN.md`: authoritative visual rules, token rules, artwork feasibility, and QA gates.
- `docs/design-handoff-2026-07-07.md`: development read order and current implementation priorities.
- `docs/design-previews/A3-ideal-*.png`: mood and material calibration only. If a render conflicts with `DESIGN.md`, `DESIGN.md` wins.
- `docs/qa/emby-tv-client-operation-matrix.md`: full functional operation coverage.
- `docs/qa/emby-tv-client-keyboard-checklist.md`: keyboard/controller execution protocol and run log.

## Execution Rules

- Run this checklist in batches. During a batch, record every finding before making fixes.
- Do not repair one checklist item at a time while the batch is still running.
- After a batch, group findings by shared cause, make a unified fix plan, implement that batch, then rerun the same batch.
- Use keyboard-only app input: arrows, `Enter`/`Space`, `Escape`, `M`, and any documented surrogate keys.
- Prefer deterministic DEBUG fixture routes when the visual state needs repeatable evidence.
- Use real saved-session artwork when available for stress testing, but never commit private screenshots, credentials, server URLs, or downloaded personal media assets.
- Record screenshots or text snapshots for every `Concern`, `Fail`, or `Blocked` result.

## Automation Ladder

Use the most precise and repeatable validation layer that can answer the question:

1. Core policy/unit tests for deterministic input decisions, selection state, route parsing, artwork choice, and text/status rules.
2. Source-level design contract tests for XAML resources, DEBUG routes, automation names, token usage, and absence of page-local visual drift.
3. Installed app DEBUG fixture routes for realistic focus movement, layout, screenshots, and controller-keyboard behavior without requiring a live server.
4. Installed app saved-session routes for real Emby artwork, real library shapes, playback progress, and server-specific metadata variations.
5. Computer Use or manual Windows automation only when the app surface must be visually inspected or no lower-level automation can observe the issue.

When a lower layer fails, fix that first before spending time on a slower visual pass. When a lower layer passes but the screen still feels wrong, record the visual finding in the batch log and treat it as a design-system issue rather than a test gap.

## Status Values

- `Pass`: matches the design system and the operation succeeds.
- `Concern`: usable, but the visual or interaction direction may drift from the design system.
- `Fail`: violates a non-negotiable design or interaction rule.
- `Blocked`: cannot be evaluated because data, tooling, install state, or platform behavior is unavailable.
- `Not Run`: intentionally deferred.

## Universal Gates

Every screen and route in every batch must satisfy these gates:

- Persistent chrome is at least 90 percent neutral cool graphite, not green, cyan, amber, purple, or sci-fi blue.
- Green is limited to tiny play/confirm glyphs, active playback/resume progress, watched/current state, or success feedback.
- Focus is visible from ten feet without a bright complete perimeter frame as the normal card focus.
- Media focus uses scale, local luminance, context dimming, and optional matte selected backplate before blur.
- Normal page cards, search boxes, navigation rows, banners, and OSD controls do not rely on decorative drop shadows or highlighted borders.
- Blur appears only over meaningful active artwork or video. Blur over plain graphite is a failure.
- At most one persistent artwork-backed blur region is visible on a normal browsing screen.
- Unfocused wide cards use a bottom black gradient/scrim for text protection, not frosted glass.
- Focused wide cards may upgrade only the low-height text/progress zone to subtle artwork-backed material.
- Normal poster-grid focus may use the integrated matte selected backplate around poster plus title/metadata.
- Continue Watching and Next Up wide cards do not use the poster-grid selected backplate recipe.
- Text is readable at TV distance, does not overlap, does not clip, and does not resize cards or rows.
- Focus scale stays inside reserved layout space and never pushes neighboring cards, rails, or OSD controls.
- Empty, loading, error, and no-artwork states use the same matte visual system as loaded content.
- Artwork-backed decisions identify the Emby image role that powers them: `Backdrop`, `Thumb`, `Banner`, `Primary`, or `Logo`.

## Batch 01 - Shell, Guide, And Home

Goal: verify that the broad Emby product model is clear without turning the app into a web dashboard or a top-only streaming clone.

| ID | Route | Keyboard Path | Design Checks | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| 01.01 | Home saved session or `home-fixture` | Launch | Home opens as a rail-first TV surface, not a landing page or single hero promo. Continue Watching and Next Up are lists/rails when data exists. | Not Run | |
| 01.02 | Home | `M`, arrows, `Escape` | Guide opens as a quiet left source guide; content remains primary; no green nav markers; Search and More/Source Hub are present or intentionally deferred. | Not Run | |
| 01.03 | Home rails | `Down`, `Right`, `Left`, `Down` | Focus is borderless and readable; row movement preserves column intent; card scale does not shift layout. | Not Run | |
| 01.04 | Continue Watching / Next Up | Focus several wide cards | Unfocused cards keep bottom black gradient only; focused card uses low-height attached text/progress material if blur is available; no hard glass box or double container. | Not Run | |
| 01.05 | Media Libraries / server sections | Focus and open a card | Library and section cards use source-appropriate artwork and quiet matte focus; no oversized green banner or bright outline. | Not Run | |

Evidence to capture:

- Home first viewport.
- Guide-open state over Home.
- Focused Continue Watching card.
- Focused media library or server-section card.

## Batch 02 - Library, Search, And Poster Grids

Goal: verify normal poster-grid browsing, filtering, searching, and fallback states against the selected-backplate rule.

| ID | Route | Keyboard Path | Design Checks | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| 02.01 | Movies / library grid | Open grid, `Right`, `Down`, `Up` | Movie/series items use vertical poster cards by default. Focused card may use integrated matte selected backplate around poster plus title/meta; no bright frame, glass card, or heavy shadow. | Not Run | |
| 02.02 | Library toolbar | `Up`, open Sort/Filter, `Escape` | Toolbar controls use matte command focus, not poster-card focus. Sheets feel grounded, not floating glass plates. | Not Run | |
| 02.03 | Search `search-fixture` | Type/search or use fixture scopes | Search results share the poster-grid selected treatment. Scope chips are compact and neutral. Recent search terms do not steal recovery focus. | Not Run | |
| 02.04 | Empty and no-artwork grid states | Empty query or fallback fixture | Empty state, unavailable poster tile, and fallback initials remain intentional matte surfaces, not abstract generated art. | Not Run | |
| 02.05 | Collections and playlists | `collections-fixture`, `playlists-fixture` | Mixed collection/playlist surfaces use the same grid/list language while preserving ordered playlist semantics and folder-like collection behavior. | Not Run | |

Evidence to capture:

- Focused normal poster card.
- Sort or filter sheet.
- Search results with focused card.
- No-artwork fallback tile.
- Collection or playlist root.

## Batch 03 - Details And Metadata Actions

Goal: verify the deterministic details decision surface, Emby-specific source controls, and artwork atmosphere fallback.

| ID | Route | Keyboard Path | Design Checks | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| 03.01 | Movie details or `details-fixture` | Open item | First viewport shows title, metadata, source/version/action decisions, and one right-side artwork atmosphere zone. If no artwork exists, the right side falls back to black/matte, not fake gradients. | Not Run | |
| 03.02 | Versions/source | Move to version selector, change option | Source selection is reachable before playback and shows selected state separately from focus. Selection does not reuse bright focus color. | Not Run | |
| 03.03 | Audio/subtitle controls | Move through available controls | Audio and subtitle choices are reachable, compact, and framed as Emby playback decisions, not decorative chips. | Not Run | |
| 03.04 | Favorite/watched/actions | Toggle fixture states | State feedback is visible, muted, and local. Green appears only when the state is active/current and not as a large action fill. | Not Run | |
| 03.05 | Cast, similar, facets, collection/playlist actions | Move below fold and open/close layers | Secondary rails derive from card/list rules, with B/Escape closing one layer and restoring focus. | Not Run | |

Evidence to capture:

- Details first viewport with artwork.
- Details fallback/no-artwork state if available.
- Version/source selector.
- One below-fold secondary rail or sheet.

## Batch 04 - Playback OSD And Playback Options

Goal: verify that playback controls remain subordinate to video/subtitles while still exposing Emby source, audio, subtitle, and more actions.

| ID | Route | Keyboard Path | Design Checks | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| 04.01 | `manual-playback` or real playback | Start playback, show OSD | Shell chrome disappears. OSD is compact: optional top-left status/title capsule plus bottom transport strip inside safe area. | Not Run | |
| 04.02 | Transport strip | `Left`/`Right`, `Enter`, `Escape` | Strip preserves internal padding. Focused 52px to 56px targets sit fully inside the panel with top and bottom breathing room. | Not Run | |
| 04.03 | Timeline and progress | Seek or preview seek | Playback progress uses muted green only for active playback/progress. Other sliders and diagnostics stay neutral. | Not Run | |
| 04.04 | `playback-options-fixture` | `M`, `Down`/`Up`, selectors, `Escape` | Source, audio, subtitles, info, and more options open lightweight menus. They are not flattened into the default OSD. | Not Run | |
| 04.05 | Subtitles/video conflict | Show subtitle text if available | Subtitle baseline remains readable. Move or shorten OSD/menu before adding stronger blur, borders, or shadows. | Not Run | |

Evidence to capture:

- Default OSD.
- Focused transport target.
- More/options menu.
- Subtitle-safe OSD state when available.

## Batch 05 - Secondary Media Surfaces

Goal: verify that non-movie media types are first-class Emby surfaces without breaking the shared visual language.

| ID | Route | Keyboard Path | Design Checks | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| 05.01 | `livetv-fixture` | Move channel list and open unsupported layer | Channel list, current-program preview, and unsupported playback layer use matte list/card rules and recover with B/Escape. | Pass with concern | 0.1.0.220 uses matte row focus and a bottom transient unsupported layer. Program preview is compact and text-first until real EPG artwork is available. |
| 05.02 | `music-fixture` | Move artists/albums/songs, open unsupported layer | Dense music browsing stays TV-readable and does not copy poster-grid treatment where list columns work better. | Pass with concern | 0.1.0.220 preserves the dense three-column model and matte row focus. Now panel remains intentionally text-first. |
| 05.03 | `photos-fixture` | Open album, open photo, B/Escape back | Photos use immersive artwork priority. Chrome is minimal, focus stays visible, and back behavior restores the photo/album anchor. | Pass with concern | 0.1.0.220 uses a photo-specific landscape tile recipe and preserves viewer/back recovery. Sparse fixture data still leaves open canvas. |
| 05.04 | Favorites / Unwatched / server libraries | Guide or More/Source Hub | Source-aware navigation supports the full Emby destination family without overloading first-level navigation. | Concern | Current accepted model is collapsed Guide plus page section actions; no distinct Source Hub route exists yet. |

Evidence to capture:

- Live TV channel browsing.
- Music browse columns.
- Photo viewer.
- Source Hub or equivalent lower-frequency route surface.

## Batch 06 - Account, Settings, Login, And Recovery

Goal: verify utility surfaces are practical and consistent without becoming a separate enterprise settings UI.

| ID | Route | Keyboard Path | Design Checks | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| 06.01 | Login empty session | Tab/arrows through fields, submit success or failure | Login is TV-accessible, failure focus returns to an editable target, and form controls use neutral matte focus. | Not Run | |
| 06.02 | Settings | Guide to Settings, move all controls | Settings uses shared typography, command focus, and panel surfaces. Diagnostics stay secondary and do not dominate. | Not Run | |
| 06.03 | Sign out confirmation | Open, cancel, reopen, confirm only on safe target | Destructive action is explicit, local, and safe by default. Danger color appears only inside the confirmation context. | Not Run | |
| 06.04 | Network/server errors | Fixture or controlled failure | Error states explain what happened and provide visible recovery controls without changing the app's visual language. | Not Run | |

Evidence to capture:

- Login focused field.
- Settings first viewport.
- Sign-out confirmation.
- Error or retry state.

## Batch Run Template

Copy this block into `docs/qa/emby-tv-client-keyboard-checklist.md` after each run:

```md
### YYYY-MM-DD - Design Conformance Batch NN

App version: `x.y.z`

Scope:

- Batch:
- Build/install source:
- Data source: fixture / saved session / local fake server / other

Keyboard-only validation:

- Route:
- Keys:
- Expected:
- Actual:
- Result: Pass / Concern / Fail / Blocked

Findings recorded before fixes:

| ID | Severity | Page | Evidence | Expected | Actual | Proposed batch fix |
| --- | --- | --- | --- | --- | --- | --- |
| | | | | | | |

Decision:

- Fix now as part of this batch / defer with reason / blocked with reason.
```

## Acceptance Rule

A development batch is visually acceptable only when:

- The functional operation rows it touches remain `Verified` or have a documented rerun plan.
- The matching design-conformance batch has no unresolved `Fail`.
- Any `Concern` has an explicit owner decision: accept, revise in the current batch, or schedule for a later named batch.
- No private server details, credentials, screenshots, or personal media assets are committed.
