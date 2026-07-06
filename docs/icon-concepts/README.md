# App Icon Concepts

Generated for the Next Gen Xbox Emby worktree on 2026-07-06.

These are historical concept previews only. They do not define the production UWP asset files under `src/NextGenEmby.App/Assets`.

## Options

1. `01-library-portal-2.png`
   - Closest to the current Library Portal direction.
   - Strong cyan portal and amber resume arc.
   - Best continuity with the existing generated icon.

2. `02-focus-rail-library.png`
   - Emphasizes the TV focus frame and stacked library cards.
   - Includes subtle poster-like artwork, so it may lose detail at 44 px.
   - Most "Xbox dashboard media app" feeling.

3. `03-direct-play-prism.png`
   - More premium and dimensional, with a direct-play light path.
   - Strongest HDR/direct playback metaphor.
   - Might need simplification before final small-size production.

4. `04-couch-queue-portal.png`
   - Cleanest silhouette and simplest queue metaphor.
   - Likely easiest to scale down.
   - Slightly more abstract than the current portal-card design.

Use `contact-sheet.png` for side-by-side comparison.
Use `small-size-check.png` to compare the 44 px and 150 px downscaled previews.

## Superseded Direction

The earlier production assets used `04-couch-queue-portal.png`.

Rationale:

- It has the cleanest 44 px silhouette: a cyan direct-play portal, a central playback cutout, stacked library cards, and an amber focus arc.
- It aligns with the current TV design direction: controller-first, dark Fluent shell, media rails, and direct playback.
- It avoids poster-specific detail, so the app icon does not look like a single movie thumbnail or a generic video player.

This direction has since been rejected by `docs/DESIGN.md` because it depends on cyan glow and portal language.

## Superseded Production Direction

Production assets then used the Matte Cinema Fluent `Matte Library Slat` direction:

- matte rounded-square tile;
- layered dark media slats;
- one crisp cyan focus edge used as a signal rather than glow;
- one green play/confirm surface;
- one flat amber progress base;
- no portal ring, prism beam, or official third-party logo.

## Superseded Production Direction

Production assets then used the Matte Cinema Fluent `Cinema Shelf Mark` direction:

- matte rounded-square tile;
- a quiet TV shelf with left Guide rail and horizontal content rails;
- one selected media card with a cyan L-shaped controller focus edge;
- one green play/confirm surface;
- one flat amber progress base;
- no portal ring, prism beam, film-strip cliche, or official third-party logo.

Rationale:

- It reads more like the app in use: controller focus moving through a TV media library.
- It keeps the 44 px silhouette simple while adding more product-specific meaning than a generic play-card stack.
- It was later replaced because it still read as a tiny UI screenshot rather than a standalone app identity mark.

## Superseded Production Direction

Production assets then used the Matte Cinema Fluent `Focus N Mark` direction:

- matte rounded-square tile;
- abstract Next/Navigation `N` built from dark media-slab forms;
- one cyan controller-focus path used as a signal rather than glow;
- one green play/confirm core;
- one flat amber progress base;
- no portal ring, prism beam, film-strip cliche, miniature UI screenshot, or official third-party logo.

Rationale:

- It starts from the app's current visual system instead of the historical concept sheet.
- It is more brand-like at 44 px than a shelf or card-stack screenshot.
- It still encodes the daily product loop: controller focus, media library, and direct play.
- It was later replaced because it depended on the current "Next Gen" name and the brand name can change.

## Current Production Direction

Production assets now use the Matte Cinema Fluent `Player Focus Mark` direction:

- matte rounded-square tile;
- compact playback viewport instead of a wordmark or initials;
- one cyan controller-focus path used as a signal rather than glow;
- one green play/confirm core;
- subtle subtitle/audio status marks;
- one flat amber progress base;
- no embedded brand name, initials, portal ring, prism beam, film-strip cliche, miniature UI screenshot, or official third-party logo.

Rationale:

- It is based on stable player attributes rather than the current product name.
- It remains readable at 44 px: focus, play, and progress survive the small size.
- The splash asset is symbol-only, so future renaming does not require rewriting raster text.
- Colors are mapped directly from `docs/DESIGN.md`, including `#050607`, `#101418`, `#1A2027`, `#303842`, `#3BD5FF`, `#61D47C`, and `#E0B86A`.
- The icon generator owns color and geometry tokens, so future theme/skin work can regenerate the full asset set from one source.

Generated UWP assets:

- `src/NextGenEmby.App/Assets/StoreLogo.png`
- `src/NextGenEmby.App/Assets/Square44x44Logo.png`
- `src/NextGenEmby.App/Assets/Square150x150Logo.png`
- `src/NextGenEmby.App/Assets/Wide310x150Logo.png`
- `src/NextGenEmby.App/Assets/SplashScreen.png`
