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

### 4. Focus N Mark

This replaces the miniature-shelf idea with a standalone product mark: an abstract Next/Navigation `N` formed from dark media slabs, a cyan controller-focus path, a green play/confirm core, and an amber progress base. It keeps the Matte Cinema Fluent palette, but reads less like a tiny screenshot.

### 5. Player Status Aperture

This removes dependence on the current product name and avoids making the icon a miniature home page. The mark is a compact playback viewport with a cyan controller-focus path, green play/confirm core, and an amber progress base. It is based on player attributes rather than text, initials, or a brand word that may change.

## Selected Direction

Use **Player Status Aperture**.

The mark should feel like a compact identity for a couch-first player rather than a literal screenshot or wordmark. The memorable elements are the focus corner, play core, and progress base. Everything else stays matte and quiet. Square, wide, and splash assets should repeat the same centered aperture primitive instead of inventing separate text, initials, or page-layout decorations.

## Token Rules

- `canvas`: near-black tile background.
- `surface`: raised TV surface and splash body.
- `shelf`: player surface and status blocks.
- `focus`: cyan edge only; no glow or portal.
- `play`: green play/confirm surface.
- `progress`: amber progress base.
- `text`: not used in production raster assets; the splash asset should remain symbol-only so future renaming does not require repainting text.

The generator owns the icon tokens so future theme work can change the asset family in one place.

The production script maps icon colors directly to `docs/DESIGN.md`: `#050607` canvas, `#101418` surface, `#1A2027` raised surfaces, `#303842` hairline, `#3BD5FF` focus, `#61D47C` play, `#E0B86A` progress, `#F6F1E8` text, and `#B9C0C8` muted text.

## Required Assets

- `StoreLogo.png` 50x50
- `Square44x44Logo.png` 44x44
- `Square150x150Logo.png` 150x150
- `Wide310x150Logo.png` 310x150
- `SplashScreen.png` 620x300

## Validation

- 44 px icon must preserve focus, action, and progress signals.
- Wide tile must read as the same player-property symbol, not a banner ad or a miniature UI.
- Splash screen must match the app shell, stay symbol-only, and not introduce a separate brand palette.
- UWP build must package the regenerated PNG assets.
