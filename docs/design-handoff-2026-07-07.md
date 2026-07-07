# Artwork-Backed Matte Fluent Handoff

This document summarizes the design output on the `codex/xbox-emby-design-system-v2` branch. It is a development handoff, not a new visual direction.

## Read Order

1. `docs/DESIGN.md` is the source of truth for tokens, visual rules, page derivation, artwork policy, focus behavior, and QA.
2. `docs/design-research/2026-07-07-tv-streaming-and-personal-media-clients.md` explains why the system uses a source-aware TV dashboard rather than a sparse streaming-service hero layout.
3. `docs/design-previews/README.md` explains which preview directions are current and which are rejected historical artifacts.
4. `docs/design-previews/playback-osd-compact.html` is the current playback OSD HTML preview. It is a visual target for density and spacing, not production code.
5. `docs/design-previews/A3-ideal-*.png` files are native render targets for final mood and material. They are useful for visual calibration, but `DESIGN.md` is still authoritative when a generated image overshoots green, borders, or glass.

Historical PNG previews under `docs/design-previews/` remain useful for comparison, but the A2 line is superseded by the A3 Artwork-Backed Matte Fluent rules in `DESIGN.md`.

## Core Page Coverage

The branch now covers the visual system for the core product pages:

- Shell and navigation: quiet left source Guide, Search as a first-class destination, More / Source Hub for unpinned sources, and no persistent long search field on normal browsing pages.
- Home: high-throughput personal media dashboard with rails for Continue Watching, Next Up, recent media, libraries, Live TV, collections, playlists, favorites/watchlist/unwatched, and server-defined sections.
- Library and Search: shared poster-grid/card/list behavior, fixed card geometry, matte fallback states, and borderless focus.
- Details: left content/actions/selectors plus one right-side artwork atmosphere zone; no-image fallback is black/matte canvas, not generated placeholder art.
- Playback: video-first screen with compact top status, bottom transport strip, menu-based track/source/diagnostic choices, subtitle protection, and matte fallback when live-video blur is not reliable.

This is enough to start first-pass page development. It does not mean every secondary page is individually mocked.

## Secondary Page Derivation

Secondary pages should compose existing rules instead of creating local visual exceptions:

- Settings, server selection, user switching, and diagnostics use matte panels, list rows, neutral buttons, and `mono` only for diagnostics.
- More / Source Hub uses the Guide/source model with grouped destinations, pinned/unpinned sources, and stable D-pad targets.
- Live TV can use denser list rows and channel/program artwork, but must assume sparse artwork and preserve matte fallbacks.
- Music and Photos reuse source-aware rails plus square/list card variants. They should not introduce a separate decorative gallery language.
- Version/source selector, audio selector, subtitle selector, confirmation, loading, empty, and error states reuse Details/Playback panels and focus rules.
- Any page that needs new visual primitives should update `DESIGN.md` before adding page-local colors, glass, shadows, focus frames, or card types.

## Implementation Priorities

1. Map `DESIGN.md` tokens to shared XAML resources before page-local styling spreads.
2. Migrate focus away from bright perimeter frames toward fill, luminance, scale, and local dimming.
3. Implement card families first: poster cards, wide resume cards, fallback cards, and selected states.
4. For normal poster grids, implement the selected state as an integrated matte backplate around poster plus title/metadata, with scale/luminance lift and no bright frame.
5. Implement Details artwork policy: `Backdrop`, then `Thumb`, then `Banner`, then cropped `Primary`, then black/matte no-art fallback.
6. Implement Playback OSD using the compact strip model. Keep OSD internal padding; do not let focused controls touch the panel edge.
7. Use real Emby artwork only in temporary/local visual tests. Do not commit private server URLs, credentials, or private media images.

## Open Decisions

- Whether the default Guide is fully hidden, a quiet 72px collapsed rail, or adaptive by page type on Xbox.
- Final Home ordering when the user has both active in-progress items and many server libraries.
- Exact XAML feasibility for live-video-backed blur on Xbox. If unreliable or expensive, use matte `surface_overlay`.
- Live TV density and channel/program row anatomy.
- How much source pinning/reordering belongs in v1 versus later customization.

## QA Before Development Acceptance

- The screen reads as a TV media client, not a desktop dashboard or a marketing landing page.
- At least 90 percent of persistent chrome is neutral graphite.
- Green appears only as play/progress/success signal, never as a large fill or navigation marker.
- Blur appears only over real artwork or live video and degrades to matte scrim.
- Focus is legible from ten feet without a bright complete outline.
- Cards and OSD controls reserve enough space for focus scale without clipping, overlap, or layout shift.
- Realistic poster and sparse/no-art fallback cases are tested before a page is considered visually done.
