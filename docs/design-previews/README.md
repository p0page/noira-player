# Visual System Preview Targets

This directory now keeps only the preview assets that are precise enough to guide visual implementation of the selected Artwork-Backed Matte Fluent direction.

Development handoff:

- `../DESIGN.md` is the source of truth for tokens, visual rules, artwork policy, focus behavior, accessibility, and fallback requirements.
- `../design-handoff-2026-07-07.md` explains how to consume the design system during page development.
- `../a3-visual-convergence-rules.md` defines the current screenshot-first convergence phase for the retained A3 render targets.
- `playback-osd-compact.html` is the current playback OSD HTML preview. Use `?state=tracks` to inspect the subtitle/audio menu state. It is useful for density and layout checks, not native blur quality.

## Retained Native Render Targets

- `A3-ideal-home-dashboard.png`: target mood for Home density, source-aware left Guide, rails, Continue Watching behavior, and artwork-led color.
- `A3-ideal-library-poster-focus.png`: target mood for normal movie/library poster-grid focus. Use the integrated matte selected backplate around poster plus title/metadata as the selected-state reference. This does not replace the separate Continue Watching wide-card rules.
- `A3-ideal-details-atmosphere.png`: target mood for Details composition: deterministic information/action column plus one right-side artwork atmosphere zone. The green play icon in this generated image is stronger than the final token rule allows; `DESIGN.md` still wins.
- `A3-ideal-playback-osd-native-material.png`: target mood for playback OSD material over active video. It illustrates the desired native-material direction better than HTML can, but final blur strength, opacity, and compositor fallback must be tuned in Xbox/native implementation.

These generated renders express the desired native feel, especially material, focus priority, TV density, and artwork atmosphere. They are AI-generated style targets, not implementation screenshots or pixel-perfect mocks. Do not copy generated-image mistakes, impossible spacing, or decorative details that do not hold up against `DESIGN.md`, real Emby data, or Xbox/native feasibility.

Use `../qa/a3-visual-convergence-checklist.md` when judging whether the current app is visually close enough to these targets. Passing keyboard routes or UIA checks is not enough during the A3 convergence phase.

## Removed Preview Lines

Superseded A/B/C/D and A2 PNGs were removed from the worktree because they encouraged rejected behavior: bright focus frames, broad acrylic/glass usage, louder green, amber progress, warmer/yellower surfaces, and component-board compositions that do not match the final direction.

If the design system is intentionally reopened later, recover those historical previews from git history instead of using this directory as a mixed option gallery.

## Current Direction Guardrails

- Real-artwork HTML previews are kept outside the repo when they use private library images. Do not commit private server URLs, credentials, screenshots, or downloaded media artwork.
- Main pages stay matte graphite: no decorative blur, no bright edge frames, and no drop shadows on normal cards, search boxes, banners, or navigation rows.
- Blur is allowed only when it samples meaningful media color behind it: active banner, focused item backdrop, poster field, or video frame.
- Normal card text uses a soft black gradient/scrim, not default frosted glass.
- Unfocused resume wide cards must not use blur/material. Their text protection is the black gradient only.
- A focused wide card may upgrade only its low-height text/progress zone to subtle dark artwork-backed material, and that material must read as one continuous text-protection zone, not as a second hard container.
- Continue Watching previews must use one wide-card anatomy per row. Prefer real wide artwork, but when only `Primary` exists, crop it directly into the wide card to preserve TV-row density.
- Focus is borderless by default: scale, luminance, matte selected fill, and local dimming before any edge.
- Green remains a sparse signal for play/confirm/progress, never a page mood.
- Blur-backed regions must map to real Emby artwork candidates: `Backdrop`, `Thumb`, `Banner`, or sometimes `Primary`. No candidate means matte fallback.
- Details pages use one right-side atmosphere zone. If no image exists, leave the page on black/matte canvas.
- Playback pages use compact video-first OSD chrome: top-left title/status, bottom transport strip, subtitle protection, and lightweight menus for source/audio/subtitle/more.
