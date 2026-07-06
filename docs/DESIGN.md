---
version: "alpha"
name: "Next Gen Xbox Emby - Matte Cinema Fluent"
description: "A dark Xbox/TV visual system for a personal Emby library, built on quiet Fluent surfaces, real media artwork, and high-contrast controller focus without retro-futurist glow."
colors:
  primary: "#61D47C"
  on_primary: "#041007"
  secondary: "#E0B86A"
  on_secondary: "#120B02"
  tertiary: "#7FA7C7"
  neutral: "#050607"
  canvas: "#050607"
  canvas_alt: "#090C0F"
  surface: "#101418"
  surface_raised: "#1A2027"
  surface_overlay: "#D9101418"
  hairline: "#303842"
  focus: "#3BD5FF"
  focus_secondary: "#F6F1E8"
  success: "#61D47C"
  warning: "#E0B86A"
  danger: "#FF6B6B"
  text: "#F6F1E8"
  text_muted: "#B9C0C8"
  text_subtle: "#78838F"
  scrim: "#D9050607"
typography:
  h1:
    fontFamily: "Segoe UI Variable Display"
    fontSize: "48px"
    fontWeight: "600"
    lineHeight: "56px"
    letterSpacing: "0"
  h2:
    fontFamily: "Segoe UI Variable Display"
    fontSize: "32px"
    fontWeight: "600"
    lineHeight: "40px"
    letterSpacing: "0"
  body_md:
    fontFamily: "Segoe UI Variable Text"
    fontSize: "18px"
    fontWeight: "400"
    lineHeight: "26px"
    letterSpacing: "0"
  label:
    fontFamily: "Segoe UI Variable Text"
    fontSize: "14px"
    fontWeight: "600"
    lineHeight: "18px"
    letterSpacing: "0"
  metadata:
    fontFamily: "Segoe UI Variable Text"
    fontSize: "15px"
    fontWeight: "400"
    lineHeight: "20px"
    letterSpacing: "0"
  mono:
    fontFamily: "Cascadia Mono"
    fontSize: "13px"
    fontWeight: "400"
    lineHeight: "18px"
    letterSpacing: "0"
rounded:
  xs: "2px"
  sm: "4px"
  md: "6px"
  lg: "8px"
  pill: "999px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  xxl: "48px"
  tv_safe: "56px"
components:
  button_primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.on_primary}"
    rounded: "{rounded.md}"
    padding: "20px 10px"
    height: "52px"
  button_secondary:
    backgroundColor: "{colors.surface_overlay}"
    textColor: "{colors.text}"
    rounded: "{rounded.md}"
    padding: "20px 10px"
    height: "52px"
  focus_frame:
    backgroundColor: "transparent"
    textColor: "{colors.focus}"
    rounded: "{rounded.lg}"
    padding: "0"
  card_surface:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    rounded: "{rounded.lg}"
    padding: "0"
  overlay_panel:
    backgroundColor: "{colors.surface_overlay}"
    textColor: "{colors.text}"
    rounded: "{rounded.lg}"
    padding: "{spacing.lg}"
  navigation_chrome:
    backgroundColor: "#E603060A"
    collapsedWidth: "72px"
    expandedWidth: "248px"
  immersive_viewer:
    scrim: "#66050607"
    controlBackgroundColor: "{colors.surface_overlay}"
    pageMargin: "56px 28px 56px 40px"
---

## Overview

Matte Cinema Fluent is the visual language for Next Gen Xbox Emby. It keeps the Xbox dark-room feel and Fluent Design discipline, but rejects the older "technology UI" vocabulary: neon cyan portals, glass prisms, glowing rings, fake holograms, perspective beams, and busy blue-black sci-fi dashboards.

The interface should feel like a calm private screening room with a precise Xbox controller layer on top. The app chrome stays matte, quiet, and legible. The user's media artwork carries color and atmosphere. System color is reserved for action, focus, progress, and warnings.

This document is visual only. It does not define navigation behavior, API shape, playback behavior, or page-level product scope. When a page spec and this file overlap, this file is the source of truth for visual tone, tokens, density, and visual anti-patterns.

## Design Thesis

The product is not "futuristic media tech." It is a personal library on a television. The visual system should therefore be:

- cinematic, but not theatrical decoration;
- Fluent, but not generic Windows desktop;
- Xbox-ready, but not Xbox-branded;
- premium, but not Apple imitation;
- quiet enough that posters, backdrops, and subtitles remain primary.

The signature is **matte depth plus hard-readable focus**. Depth comes from luminance, spacing, scrim, and real artwork, not from glow effects. Focus is obvious from ten feet, but it does not turn every selected object into a neon sign.

## Design Sources

This file follows the DESIGN.md pattern: machine-readable YAML tokens first, then human-readable rationale. The format is inspired by Google Labs' DESIGN.md specification, where YAML gives agents exact values and Markdown explains how to apply them.

Local design-skill references used for structure and quality gates:

- `clean`: practical semantic tokens, explicit states, reduced visual clutter.
- `sleek`: intentional limited palette and spacing discipline.
- `spacious`: 8-point rhythm, readable density, high-contrast support.
- `material`: layered surfaces, accessible state definitions, tokenized motion logic.
- `stitch`: human-facing DESIGN.md companion structure.

These references inform the document shape, not the final aesthetic.

## Colors

### Palette Roles

- **Canvas `#050607`:** true app background. Use for full-screen shell and playback-adjacent surfaces. It should read black on TV without crushing text.
- **Canvas alternate `#090C0F`:** subtle full-screen variation for non-playback pages.
- **Surface `#101418`:** default panel, toolbar, sheet, and card fallback.
- **Raised surface `#1A2027`:** selected row, focused card base, modal body, and elevated controls.
- **Overlay `#D9101418`:** acrylic-like panel over artwork or video. Use only when content remains visible and readable.
- **Hairline `#303842`:** dividers and inactive card borders. Do not use bright cyan borders for ordinary structure.
- **Text `#F6F1E8`:** warm off-white primary text. This avoids sterile blue-white sci-fi contrast.
- **Muted text `#B9C0C8`:** metadata and secondary labels.
- **Subtle text `#78838F`:** timestamps, disabled labels, and tertiary metadata.
- **Primary / play `#61D47C`:** Xbox-compatible green for Play, Resume, confirmation, and positive action states.
- **Focus `#3BD5FF`:** crisp controller focus frame and active route signal. Use it as an edge, underline, or focus line, never as a glowing background mood.
- **Secondary / progress `#E0B86A`:** watch progress, resume state, warnings that are not destructive.
- **Tertiary `#7FA7C7`:** informational state only, such as diagnostics and neutral playback capability badges.
- **Danger `#FF6B6B`:** destructive or failed state only.

### Usage Rules

Use neutral surfaces for at least 85 percent of the UI. Let media artwork provide most saturated color. Cyan, green, and amber must feel like signals, not decoration.

Do not create blue or cyan as the default brand mood. Cyan is allowed for the controller focus system because it separates navigation state from Play/Resume. It must stay sharp, flat, and sparse: no electric glow fields, portals, or cyan-tinted page backgrounds.

Never place saturated accents on top of saturated artwork without a dark scrim. A selected item over artwork needs either a solid focus frame or a 70 to 85 percent dark overlay.

Contrast checks from the initial token set:

- `text` on `canvas`: 18.02:1.
- `text_muted` on `canvas`: 11.04:1.
- `text` on `surface`: 16.44:1.
- `primary` on `canvas`: 10.83:1.
- `focus` on `canvas`: 11.73:1.
- `secondary` on `canvas`: 10.85:1.

## Typography

Use Segoe UI Variable as the default type family because it belongs to the Windows/Xbox environment and avoids imported brand personality. The distinction comes from scale, spacing, and restraint, not exotic fonts.

Type roles:

- `h1`: page title, major hero title, and rare large decisions. Use sparingly.
- `h2`: section headers, sheet titles, and media group labels.
- `body_md`: descriptions, settings copy, and body labels intended for ten-foot reading.
- `label`: button labels, nav labels, chip text, and compact actions.
- `metadata`: year, runtime, codec, audio, episode count, and secondary media facts.
- `mono`: diagnostics only. Never use monospace for ordinary media metadata.

Rules:

- Letter spacing is always `0`.
- Do not scale font size with viewport width.
- Do not use all-caps as a general styling device. It can appear only for short technical badges when the source term is conventionally uppercase, such as HDR or HEVC.
- A compact panel title must not use hero-size type.
- Long labels must wrap or truncate at stable widths. Text must not resize controls.

## Layout

The base layout is a 960x540 TV logical canvas that can scale to 1080p or 4K output. UI should be designed for distance first, pixel density second.

Layout rules:

- Main page content starts inside a 5 percent TV safe area. The default safe margin token is `56px`.
- Use an 8-point spacing rhythm. `4px` exists only for optical corrections.
- Horizontal media rails and grids must have stable card dimensions. Focus, badges, loading states, and labels cannot reflow the rail.
- Do not use nested cards. Repeated media items can be cards. Page sections are full-width regions, rails, or unframed layouts.
- Avoid decorative containers around whole pages. A page is a surface, not a card.
- Empty states and error states must reserve the same layout footprint as loaded content when practical.

Density should be "comfortable TV dense": enough items visible to support browsing, but never so dense that card labels, focus frames, or metadata compete with artwork.

## Theme Tokens And Skinning

The implementation should express repeated visual choices as XAML resources before they spread across pages. Colors, brushes, page margins, panel padding, typography styles, focus thickness, card radii, and common panel shapes belong in `App.xaml` or a future merged theme dictionary. Page XAML can still own one-off layout structure, such as a two-column diagnostics grid, but it should not duplicate raw font sizes, surface colors, or focus styling when a shared token exists.

This keeps Matte Cinema Fluent replaceable. A future skin should be able to swap resource dictionaries for color, typography, spacing, and panel treatment without rewriting Emby data flow, controller navigation, or playback behavior. Skins may change visual language, but they must preserve the TV safety rules in this file: visible focus, stable card dimensions, dark scrims over artwork, and no hover-only affordances.

The current XAML token migration has started with:

- app shell and Guide rail resources: `AppShellRailBrush`, `TvGuideCollapsedWidth`, and `TvGuideExpandedWidth`;
- immersive viewer resources: `AppImmersiveScrimBrush`, `AppImmersiveControlBrush`, and `TvImmersivePageMargin`;
- shared TV text, panel, diagnostics, icon button, nav button, and settings checkbox styles.

New UI surfaces should follow this migration order:

- use existing semantic brushes and spacing resources first;
- promote repeated hard-coded values into named resources or styles;
- keep interaction state colors semantic, such as focus, play, progress, danger, and muted text;
- avoid page-local visual constants unless they describe a unique layout constraint.

## Elevation and Depth

Depth is matte and structural. The design should resemble stacked black materials in a dim room, not transparent glass floating in blue light.

Allowed depth tools:

- luminance step from `canvas` to `surface` to `surface_raised`;
- 1px hairline boundaries;
- soft shadow only behind modal or playback overlay layers;
- dark scrim over artwork;
- small focus lift or scale for selected media cards;
- limited acrylic/translucency when a sheet covers artwork or video.

Prohibited depth tools:

- neon outer glow as a brand device;
- glass prism effects;
- holographic beams;
- decorative bokeh, orbs, particles, or streaks;
- shiny metal/card reflections;
- blue gradients used as generic "premium tech."

## Shapes

The system uses modest Fluent geometry:

- `4px`: chips, badges, tiny technical labels.
- `6px`: buttons, input boxes, nav items.
- `8px`: media cards, sheets, panels, app icon safe-shape references.
- `999px`: progress pills and tiny status dots only.

Avoid large 18px to 32px "soft app" rounding. It reads mobile and candy-like on a TV interface. Avoid square brutalist corners unless the element is a pure video frame or image crop.

## Components

This file does not define page interaction, but it does define visual anatomy for reusable UI families.

### Buttons

Primary buttons use `primary` background with `on_primary` text. Secondary buttons use `surface_overlay` with `text`. Buttons are 52px high by default, with 6px radius and a minimum readable label width.

Visual states:

- Default: matte fill, no glow.
- Focus-visible: 2px `focus` frame plus a 1px `focus_secondary` inner or outer contrast line where needed. Do not reuse the green Play/Resume fill as the generic focus color.
- Pressed: fill darkens or compresses by luminance, not by glow.
- Disabled: reduce text and border contrast but keep the control shape visible.
- Loading: preserve button width and label footprint.

### Media Action Rows

Details and playback-adjacent pages use a compact media action row rather than hiding primary state inside a menu. The first action is Play or Resume and uses the `primary` green fill. Adjacent actions such as Restart, Favorite, Watched, Refresh, stream selectors, and playlist or collection actions use secondary matte buttons.

Visual rules:

- Keep every action at the standard 52px TV target height.
- Use icons plus short labels for actions that change item state.
- Do not use green for Favorite, Watched, Refresh, or neutral selectors.
- Preserve stable button widths when labels change, such as Add favorite to Remove favorite.
- The focus frame remains cyan even when the focused action is the green Play or Resume button.
- If an action is unavailable, keep the rest of the row aligned; do not let hidden controls collapse the primary Play target.

### Cards

Media cards are artwork-first. The artwork crop, title, progress, and focus frame are the card's identity.

Visual rules:

- Poster, wide, square, and episode cards must each have fixed aspect ratios.
- Titles should live below or over a controlled gradient/scrim band, never directly over uncontrolled bright artwork.
- Progress uses `secondary`, 3px to 5px high depending on card size.
- Focus uses a clean perimeter frame and optional small scale. Do not add neon glow.
- Fallback cards use `surface` plus a simple text label, not generated abstract art.

### Panels and Sheets

Panels use `surface_overlay` over artwork/video and `surface` over plain canvas. Sheets are grounded surfaces. They should not look like floating glass plates.

Rules:

- One layer of panel over a page is normal. Two transient layers is the visual maximum.
- Use scrim to protect text. Do not blur heavily enough to become a decorative background.
- Panel text uses `body_md`, `metadata`, and `label`, not hero type.

### Navigation Chrome

Navigation chrome is quiet. The active route can use a `focus` marker or focused outline, but inactive nav remains neutral.

Rules:

- Icons should be Fluent-compatible, simple, and stroke-based.
- Do not use filled neon icons.
- Do not use permanent bright borders around inactive nav.
- Expanded labels must align to the same grid as collapsed icon targets.

### Playback OSD

Playback UI is subordinate to video and subtitles.

Visual rules:

- OSD panels are dark matte overlays with clear controls.
- Keep the bottom subtitle region readable. If a panel competes with subtitles, move or shorten the panel before adding stronger decoration.
- Transport controls may use `focus` for the current controller target, `primary` for Play/Resume/confirm, and `secondary` for progress.
- Diagnostics use `mono` and `tertiary`, never the primary focus color.

## Artwork

Artwork is the emotional layer. The UI system should not invent atmosphere when the user's media already provides it.

Rules:

- Backdrops may be used as wide environmental washes only when they are darkened, cropped, and protected by scrim.
- Poster and backdrop artwork should keep recognizable subject matter. Avoid excessive blur that turns content into generic color.
- Use one artwork-driven accent at most per view, and only if it does not conflict with `primary`, `secondary`, or state colors.
- Never use AI-generated abstract gradients as a fallback for missing media.
- Missing artwork fallback is a matte surface with text and a small media-type icon.
- Server-configured home section cards use the section or parent item artwork first, especially `Thumb`, `Backdrop`, `Banner`, and `Primary` in that order for wide cards. Child item artwork is only a fallback when the section itself has no consumable image.
- Details organize sheets use the destination item artwork first for collection and playlist rows, with the same wide-card preference: `Thumb`, `Backdrop`, `Banner`, then `Primary`. Child item posters are not the primary visual source for these rows.

## Icons and App Identity

The previous glowing "Library Portal" icon direction is rejected for final identity work. It is too close to an older stereotype of tech design: cyan rings, portal beams, glass cards, and neon progress arcs.

Future icon work should use this vocabulary instead:

- matte rounded-square tile;
- cropped media slats or a quiet library aperture;
- one high-contrast focus or play affordance;
- no cyan glow;
- no official Emby, Xbox, Microsoft, or platform logos;
- no film-strip cliche;
- no generic play triangle standing alone;
- legible at 44px with a stable silhouette.

Possible icon concepts aligned with this system:

- **Matte Library Slat:** layered black media rectangles with one crisp focus edge and one green play/confirm surface.
- **Screen Room Mark:** a dark screen shape with a subtle amber progress base.
- **Quiet Play Aperture:** a flat negative-space play cutout inside a neutral media tile.

## Motion

Motion should clarify controller state. It should not be ambient decoration.

Allowed motion:

- focus entrance, 80ms to 140ms;
- sheet entrance, 140ms to 220ms;
- card focus scale, 1.015 to 1.03;
- opacity fade for scrim and overlays;
- progress changes that track playback state.

Rules:

- No looping decorative animation.
- No particle systems.
- No parallax that changes reading order.
- Reduced-motion mode must keep focus, state, and hierarchy understandable.

## Content Tone

UI copy is calm and operational. It should sound like a media device, not a marketing page.

Use:

- "Resume"
- "Play"
- "Retry"
- "No movies found"
- "Server unavailable"
- "Subtitles off"
- "Version changed"

Avoid:

- "Experience next-gen entertainment"
- "Unlock your cinematic portal"
- "Something magical happened"
- vague errors such as "Oops" without next action.

## Do's and Don'ts

Do:

- use semantic tokens instead of raw hex values;
- let media artwork provide color and specificity;
- keep focus unmistakable from ten feet;
- preserve stable sizes during loading, focus, and error states;
- use matte layers and controlled scrims;
- keep page sections unframed unless they are repeated media cards or transient panels.

Don't:

- use cyan glow, portal rings, prism beams, or hologram language;
- use blue-purple gradients as a default dark UI mood;
- place cards inside cards;
- use stock abstract backgrounds;
- use all-caps metadata styling as decoration;
- let focus animation move neighboring content;
- hide primary visual state behind hover-only effects;
- use text-heavy explanations inside the app to compensate for unclear hierarchy.

## Migration Notes

Existing tokens and assets should migrate visually in a later implementation pass. This document does not make those code changes.

Suggested migration mapping:

- Current cyan `AppAccentColor #4EE7FF` becomes `focus #3BD5FF` or the nearest platform-safe cyan used by XAML resources.
- Current green `AppActionColor #78E68B` becomes `primary #61D47C`.
- Current warm `AppWarmColor #E4B84C` becomes `secondary #E0B86A`.
- Current surfaces stay in the same dark family but should shift toward the matte `canvas`, `surface`, and `surface_raised` roles.
- Current generated app icon concepts under `docs/icon-concepts` are exploration artifacts only and should not define the final visual identity.

## QA Checklist

Before accepting a visual implementation based on this file, verify:

- The screen still reads as Xbox/TV dark Fluent, not a web dashboard.
- No neon cyan glow, prism, portal beam, decorative particle, or generic sci-fi motif appears.
- At least 85 percent of persistent chrome uses neutral surfaces.
- Focus is visible from ten feet on every actionable element.
- Focus, loading, and labels do not resize cards, rows, or buttons.
- Text uses Segoe UI Variable roles and never scales with viewport width.
- Primary text and key state colors meet WCAG AA contrast on their intended surfaces.
- Artwork remains recognizable and does not become a generic blurred gradient.
- Playback overlays do not make subtitles unreadable.
- Empty, loading, and error states follow the same visual system as loaded content.
- The app icon direction remains matte, legible at 44px, and free of official third-party logos.
