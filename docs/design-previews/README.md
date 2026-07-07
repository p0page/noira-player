# Visual System Preview Options

These are style-tile previews for the visual foundation only. They are not interaction specs and not implementation targets yet.

Use `contact-sheet.png` for the original A/B/C/D comparison. The A2 line has been superseded by the new Artwork-Backed Matte Fluent direction.

## A3. Artwork-Backed Matte Fluent

Current design direction. Scratch rendered previews exist outside the repo for review; commit a final preview only after the direction is selected.

- Real-artwork HTML previews are kept outside the repo because they use private library images. Do not treat mock wide-image previews as valid for movie or series poster rows.
- Main pages stay matte graphite: no decorative blur, no bright edge frames, and no drop shadows on normal cards, search boxes, banners, or navigation rows.
- Blur is allowed only when it samples meaningful media color behind it: active banner, focused item backdrop, poster field, or video frame.
- Normal card text uses a soft black gradient/scrim, not default frosted glass. A focused wide card may upgrade only its text/metadata zone to a subtle dark artwork-backed material, and that material must degrade to a non-blurred scrim.
- Unfocused resume wide cards must not use blur/material. Their text protection is the black gradient only.
- Continue Watching previews must use one wide-card anatomy per row. Prefer real wide artwork, but when only `Primary` exists, crop it directly into the wide card to preserve TV-row density. Do not mix full-landscape cards with separate vertical-poster cards in one rail. Selected state must use scale, artwork zoom/luminance, local dimming, integrated dark information material, and progress emphasis rather than a bright border or nested thumbnail. The selected information material may sit over the normal bottom scrim, but it must read as one continuous low-height text-protection zone, not as a second hard container.
- Focus is borderless by default: scale, luminance, matte selected fill, and local dimming before any edge.
- Green remains a sparse signal for play/confirm/progress, but additional content color can appear when it comes from artwork or a justified semantic state.
- This direction uses the Apple TV/Xbox references as usage logic for artwork-backed materials, not as layouts to copy.
- Next previews must follow the streaming/personal media client research in `../design-research/2026-07-07-tv-streaming-and-personal-media-clients.md`: Home is a high-throughput personal media dashboard, Continue Watching is a rail/list, and navigation must support a left source-guide or source-hub model for the full Emby destination family.
- Blur-backed regions must map to real Emby artwork candidates: `Backdrop`, `Thumb`, `Banner`, or sometimes `Primary`. No candidate means matte fallback.
- Every A3 preview must show three asset-availability states: rich `Backdrop`/`Thumb`, only `Primary`, and no usable artwork.

Risk: the next preview must prove that blur is meaningful only over real artwork, that matte fallback states still look intentional when no backdrop exists, and that the matte main surface does not become flat or generic.

## A2.13. Matte Cinema Fluent: Focus Without Frame

Rejected previous candidate.

- Uses realistic fictional movie and series posters as the primary visual stress test.
- Keeps normal UI chrome grayscale plus muted green only.
- Green appears only on the focused/current item progress, bottom playback progress, and tiny Play/Resume glyph accent.
- Focus explored lifted poster scale, luminance, local dimming, and shadow rather than a crisp complete focus frame.
- Acrylic appeared on the left rail, details sheet, and playback OSD, but the placement was too broad and not tied tightly enough to real artwork underneath.
- Historical value: useful for testing borderless focus and realistic poster noise, not for the final material system.

Why rejected: it still made acrylic, bright boundaries, and shadow feel like default visual tools. That combination contradicts the current artwork-backed blur rule.

Historical audit note: the right-side command buttons are command targets and may remain matte rectangles, but that rectangle treatment must not be reused for poster focus.

## A2.12. Matte Cinema Fluent: Muted Green Real Posters

Strong color-discipline candidate.

- Removed colored ratings and non-green metadata accents.
- Reduced green to playback and current-item progress.
- Still retained a slightly more visible poster edge than desired, so A2.13 supersedes it.

## A2.11. Matte Cinema Fluent: Grayscale Ratings

Intermediate correction.

- Replaced yellow/red rating color with grayscale metadata.
- Green progress was still too frequent across poster cards, making the page read greener than intended.

## A2.10. Matte Cinema Fluent: Real Poster Stress

Rejected stress-test candidate.

- Realistic poster complexity was useful.
- Normal UI used yellow/red rating accents, which violates the current rule that normal chrome is grayscale plus muted green only.

## A2.9. Matte Cinema Fluent: Real Poster App Screen

Previous review candidate.

- App-screen preview rather than a component board.
- Uses realistic fictional movie posters to test focus lift, acrylic legibility, OSD scrims, and visual noise.
- Normal UI chrome remains grayscale plus muted green only.
- Green appears only for tiny Play/Resume accent, playback progress, resume/watch progress, and success confirmation.
- No green nav marker, generic green settings slider, green focus border, or green button fill.
- Historical value: first app-screen preview that tested realistic poster noise, but it still treated acrylic as a reusable chrome material instead of an artwork-backed exception.

Risk: implementation must reserve enough focus-safe space for the lifted card so scale does not shift rails or collide with neighboring posters.

## A2.7. Matte Cinema Fluent: Realistic Poster Context

Previous realistic-poster preview.

- Same Green Signal direction as A2.6, but tested against realistic fictional movie posters and cover art.
- UI chrome used cool graphite with controlled acrylic, lifted focus, and restrained shadow.
- Normal UI color is grayscale plus muted green only; danger, error, and warning are separate exceptional states and are not shown here.
- Poster artwork may contain natural cinematic colors because it is content, not UI chrome.
- Green is limited to Play/Resume micro accents and playback/resume progress. Ordinary settings sliders should stay neutral gray.

Risk: still read too much like a component board and included some ambiguous generic controls.

## A2.6. Matte Cinema Fluent: Green Signal

Intermediate candidate before realistic poster stress testing.

- Removed amber progress from the normal UI palette.
- Normal UI color becomes grayscale plus muted green only.
- Stronger match for the current color direction than A2.5.

Risk: used mostly abstract/desaturated artwork, so it was too forgiving.

## A2.5. Matte Cinema Fluent: Cool Precision

Previous candidate.

- Cool graphite dark shell, less yellow than A2.4.
- Focus is shown through lift, luminance, selected fill, and context dimming rather than a bright complete outline.
- Play/Resume actions use neutral charcoal/steel surfaces; green is only a tiny play/confirm accent.
- Amber was still present for progress/resume state; this has been superseded by A2.6/A2.7.
- Controlled acrylic appeared on rail, sheets, OSD, and modal layers only.
- Historical value: useful cool-tone correction, but still too dependent on shadow/material styling.

Risk: if green is reduced too far, Play may need stronger icon position, label, or motion to stay immediately discoverable from ten feet.

## A2.4. Matte Cinema Fluent: Neutral Action

Intermediate exploration before A2.5.

- Removed large green action buttons.
- Kept a warmer ivory/amber cinema feel.
- Useful reference if A2.5 becomes too cool or sterile.

Risk: still reads slightly yellow/warm compared with the desired cool technology tone.

## Superseded A/B/C/D Contact Sheet

The following early options are retained only as historical comparison notes. Their amber progress, bright green action, cyan focus, and warmer color language do not define the current token system.

## A. Matte Cinema Fluent

Original early `docs/DESIGN.md` direction.

- Dark matte Fluent shell.
- Crisp controller focus and green primary action from the early exploration.
- Amber resume/progress.
- Warm off-white text.
- Strongest Xbox-compatible signal without using platform branding.

Risk: green feels too game-console-specific and visually loud for the current direction.

## B. Warm Archive Cinema

More personal-library and home-cinema oriented.

- Warmer graphite and olive-black surfaces.
- Ivory focus.
- Brass progress and state accents.
- More editorial and less platform-driven.

Risk: can drift toward boutique cinema/archive if applied too warmly.

## C. Neutral Xbox Fluent

Closest to a native, restrained Xbox/Windows Fluent app.

- Graphite surfaces.
- White focus frame.
- Green only for play.
- Amber only for resume/progress.
- Most utilitarian and least opinionated.

Risk: can become generic unless artwork handling and spacing are excellent.

## D. Poster Gallery Fluent

Most artwork-led and least chrome-led.

- Dark gallery wall feeling.
- Ivory focus edge.
- Small amber progress.
- UI chrome recedes behind posters and backdrops.

Risk: depends heavily on good artwork and may need stronger fallback rules.

## Historical Selection Notes

These notes explain why the early directions were not selected. They should not be used as active implementation options unless the design system is intentionally reopened.

- A was too console-green and retained the older crisp-focus language.
- B was too warm and archive-like for the cooler technology target.
- C was native and restrained, but too generic without A2's artwork/focus rules.
- D was artwork-led, but too dependent on content quality and still retained warm progress language.
