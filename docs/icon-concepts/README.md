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

## Current Production Direction

Production assets now use the Matte Cinema Fluent `Matte Library Slat` direction:

- matte rounded-square tile;
- layered dark media slats;
- one crisp cyan focus edge used as a signal rather than glow;
- one green play/confirm surface;
- one flat amber progress base;
- no portal ring, prism beam, or official third-party logo.

Generated UWP assets:

- `src/NextGenEmby.App/Assets/StoreLogo.png`
- `src/NextGenEmby.App/Assets/Square44x44Logo.png`
- `src/NextGenEmby.App/Assets/Square150x150Logo.png`
- `src/NextGenEmby.App/Assets/Wide310x150Logo.png`
- `src/NextGenEmby.App/Assets/SplashScreen.png`
