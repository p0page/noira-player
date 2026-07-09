# A3 Visual Convergence Rules

Date: 2026-07-08

This document defines the next development phase after the initial design-system handoff. The goal is visual convergence with the retained A3 render targets, not interaction polish. It exists because the current app can perform many routes, but its screenshots still read too much like a desktop UWP management app instead of the intended Xbox/TV media client.

## Inputs

- `docs/design-previews/A3-ideal-home-dashboard.png`
- `docs/design-previews/A3-ideal-library-poster-focus.png`
- `docs/design-previews/A3-ideal-details-atmosphere.png`
- `docs/design-previews/A3-ideal-playback-osd-native-material.png`
- `docs/DESIGN.md`
- Current installed-app screenshots from saved-session, private real sample, and historical fixture QA batches.
- Focus/navigation research from the Xbox/UWP/TV client follow-up: mature TV apps use a system focus model, focus scopes, and navigation graphs instead of per-route key patches.

## Phase Objective

Make the four primary screenshots feel like the A3 targets:

- Home should read as a dense cinematic media wall.
- Library should read as a quiet movie wall with clear poster focus.
- Details should read as full-screen artwork atmosphere with a deterministic decision surface.
- Playback should read as video-first content with native-feeling material chrome.

The phase is complete only when screenshots are visually close enough to the A3 targets that remaining work is clearly interaction, data, or platform tuning rather than foundational visual direction.

## Source Order

1. `docs/DESIGN.md` remains the rule source for token safety, color limits, fallback behavior, and private-data constraints.
2. The A3 PNGs are AI-generated mood and composition targets, not pixel-perfect blueprints. Match the overall style, hierarchy, density, material feel, and media-first mood; do not reproduce generated-image artifacts or implausible details when they conflict with `DESIGN.md`, real Emby data, or Xbox/native feasibility.
3. This document decides phase scope: visual beauty first, interaction refinements later.
4. Existing operation and keyboard checklists remain useful smoke tests, but they do not define visual acceptance for this phase.

## Non-Goals

- Do not solve the full controller focus architecture in this phase.
- Do not add more page-specific `KeyDown` patches unless a route cannot be opened for visual capture.
- Do not treat UIA success, route completion, or passing unit tests as visual acceptance.
- Do not pixel-copy generated A3 images when they conflict with real Emby data, token rules, or Xbox feasibility.
- Do not commit private server URLs, credentials, screenshots, downloaded artwork, or real media assets.

## Global Visual Rules

### Dark Room Canvas

The app should feel like a media device in a dark room, not a desktop window. The main canvas should be near-black, with only small graphite luminance steps. Large page regions should not be filled with visible panel borders, desktop-style fields, or generic Windows form surfaces.

Desktop title-bar chrome is not part of the target. For desktop verification, either hide/customize it in implementation or crop it out when assessing Xbox visual quality. A white Windows title bar in a screenshot is a visual failure for the A3 target, even if it is an artifact of local packaging.

### Artwork Carries Color

UI chrome stays grayscale and graphite. Color should mostly come from posters, backdrops, thumbnails, video frames, and tiny semantic signals. Green is limited to play, progress, current playback, watched/success, or confirm glyphs. It must not become a page mood, navigation marker, banner fill, or focus outline.

### Chrome Recedes

Navigation, search, sort/filter, command bars, refresh, and secondary actions should recede behind media. They may exist, but they should not read as the most important objects on the screen. Avoid large bordered form controls, heavy rectangular command groups, and toolbar-first layouts.

### Borderless Media Focus

Normal media focus uses scale, luminance, local dimming, poster/backdrop priority, and matte backplate before any edge. A complete bright border around a focused card is not the default focus language. Poster-grid focus and wide-card focus are separate recipes.

### Material Only Over Real Content

Blur, acrylic, or frosted material is allowed only when there is meaningful artwork or video underneath. Blur over flat graphite is decorative and should be removed. If native blur is not feasible on Xbox for a surface, use a dark matte scrim that preserves the same hierarchy.

### TV Density

TV information density should stay close to the A3 renders: enough items visible to browse without feeling like a mobile hero page, but not so dense that labels and focus become cramped. Over-large headings, oversized toolbar rows, and desktop form spacing should be reduced.

### Text As Fallback, Not Chrome

Personal Emby libraries cannot guarantee that artwork contains readable title information. Cards still need textual fallback. The text should be protected by scrims or matte material and should not create a second hard card inside the card.

## Page Targets

### Home

Target: `A3-ideal-home-dashboard.png`

- Keep the left source Guide visually quiet and subordinate.
- Make rails and media cards the first read, not a dashboard hero panel.
- Continue Watching should feel like a row/list, not a single decision surface.
- The top viewport should show many real media choices, with artwork carrying color.
- Media library cards and server sections should not become large bordered management tiles.

Current gap:

- The implementation still has a desktop title bar, heavy page title, form-like refresh target, large hero/feature decision area, and visible card borders. It reads too much like a management dashboard.

### Library

Target: `A3-ideal-library-poster-focus.png`

- Poster grid should feel like a movie wall.
- Sort/filter controls should be light utility controls, not large form fields.
- Focused poster should use the integrated matte selected backplate, subtle scale, and local luminance.
- Title and metadata are required below cards, but should stay compact and secondary.
- No-art fallback should be quiet and intentional, not a dominant block.

Current gap:

- The implementation starts too much like a desktop list page: large title, large sort/filter fields, visible borders, and a poster focus state that reads blockier and heavier than A3.

### Details

Target: `A3-ideal-details-atmosphere.png`

- Details is the strongest visual reset. It should become a full-screen artwork-atmosphere page.
- The left content column contains title, metadata, badges, overview, and authorship.
- The action row is a low bottom decision island, not a normal top command bar.
- The right side is atmosphere, not a separate poster viewer.
- Backdrop/Thumb/Banner drive the atmosphere first; cropped Primary is acceptable when no better image exists; no image means black/matte.

Current gap:

- The implementation is still a left-form/right-poster split. It exposes version/action panels as desktop controls and lets the right poster read as a big asset panel rather than background atmosphere.

### Playback

Target: `A3-ideal-playback-osd-native-material.png`

- Video or artwork should dominate the entire screen.
- OSD is a compact floating material strip inside safe area.
- Title/status is a small top-left capsule, not a page panel.
- Source, audio, subtitles, and more remain reachable but subordinate.
- Subtitle baseline remains protected above the transport strip.

Current gap:

- Current desktop captures often show black playback and a conventional control strip. This is acceptable for smoke testing but not for visual acceptance. Playback visual acceptance needs video/artwork-backed evidence from a real sample.

### Secondary Pages

Search, Settings, Login, Live TV, Music, Photos, and utility surfaces should not define the phase mood. They inherit the canvas, typography, material, focus, and chrome discipline established by the four primary screens.

## Focus Governance Boundary

This phase should not expand controller-focus patching. The focus research points to a later architecture:

- global input semantics for D-pad, A, B, Menu, View, and Y;
- `FocusScope` / `FocusGroup` / `FocusBoundary` declarations;
- stable focus keys based on media id, row id, tab id, and action id;
- focus stack and restoration after navigation, refresh, deletion, modal close, and OSD hide;
- modal/flyout/OSD focus traps;
- page-local focus graph declarations for non-rectangular layouts;
- limited `XYFocus*`, `GettingFocus`, `LosingFocus`, and `NoFocusCandidateFound` overrides for named exceptions only.

During A3 Visual Convergence:

- Fix focus only when a page cannot be opened, captured, or visually inspected.
- Do not introduce new scattered direction-key handlers for cosmetic work.
- If a temporary focus workaround is unavoidable, label it as temporary and record the future focus-scope rule it belongs to.

## Verification Rules

- Use screenshots as the primary acceptance artifact.
- Compare each implementation screenshot directly against the matching A3 target.
- Judge visual closeness by intent and system coherence, not by pixel-level reproduction of AI-rendered details.
- Use UIA only to confirm route state, not visual quality.
- Follow `docs/qa/ui-development-data-sources.md` for UI data-source policy.
- Use private real UI samples for repeatable structural work: layout, spacing, focus treatment, text fallback, artwork density, source labels, and regression safety.
- Do not reintroduce `*-fixture`, `details-real-sample`, or `details-real-bright-sample` routes.
- Record all visual findings before fixing within a batch.
- Do not accept a page because it is functionally navigable if it still reads as desktop UWP.

## Phase Sequence

1. Establish screenshot and comparison protocol for A3 visual review.
2. Fix global canvas and shell chrome so every page starts from the right dark-room base.
3. Recompose Home toward the A3 media-wall target.
4. Recompose Library poster grid and toolbar toward the A3 movie-wall target.
5. Recompose Details toward the A3 atmosphere target.
6. Recompose Playback OSD toward the A3 native-material target.
7. Revisit secondary pages only after the four primary screens establish the visual system.
8. Start the separate controller-focus architecture phase after visual convergence.

## Anti-Patterns

- Treating a passing keyboard route as visual completion.
- Continuing to add page-local direction-key patches during visual work.
- Making the page title, toolbar, refresh button, or sort/filter controls the first read.
- Using bright focus borders, shadows, or glass as generic style.
- Applying blur over plain graphite.
- Creating fake artwork placeholders when no Emby image exists.
- Mixing poster-grid focus rules with Continue Watching wide-card rules.
- Letting implementation screenshots remain dominated by OS/window chrome.
