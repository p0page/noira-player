# App Icon Refresh Design

Date: 2026-07-06

## Context

The app now uses the Matte Cinema Fluent direction: dark TV-safe surfaces, real library artwork, crisp controller focus, and restrained Xbox-compatible action color. The current production icon is legible, but it reads mostly as stacked cards plus a play button. It does not yet express the daily product loop: browsing a personal media library from a TV with a controller.

## Options Considered

### 1. Keep Matte Library Slat

This keeps the existing layered slats and green play tile. It is the safest small-size mark, but it feels closer to a generic video player than a complete Emby TV client.

### 2. Add A Filmstrip Or Server Symbol

This would make "media library" more literal, but filmstrip marks are overused and server shapes pull the brand toward infrastructure rather than couch use.

### 3. Cinema Shelf Mark

This shows a quiet TV media shelf: a left Guide rail, horizontal content rails, one focused media card, a green play surface, a crisp cyan focus corner, and a flat amber progress base. It is still readable at 44 px, but it carries more product-specific meaning.

## Selected Direction

Use **Cinema Shelf Mark**.

The mark should feel like a tiny snapshot of the app in use: dark room, media rails, controller focus, direct play. The memorable element is the cyan L-shaped focus corner around the selected media card. Everything else stays matte and quiet.

## Token Rules

- `canvas`: near-black tile background.
- `surface`: raised TV surface and splash body.
- `shelf`: media rail blocks.
- `focus`: cyan edge only; no glow or portal.
- `play`: green play/confirm surface.
- `progress`: amber progress base.
- `text`: splash title only; square icons must remain symbol-only.

The generator owns the icon tokens so future theme work can change the asset family in one place.

## Required Assets

- `StoreLogo.png` 50x50
- `Square44x44Logo.png` 44x44
- `Square150x150Logo.png` 150x150
- `Wide310x150Logo.png` 310x150
- `SplashScreen.png` 620x300

## Validation

- 44 px icon must preserve the focus-card silhouette.
- Wide tile must read as a media shelf, not a banner ad.
- Splash screen must match the app shell and not introduce a separate brand palette.
- UWP build must package the regenerated PNG assets.
