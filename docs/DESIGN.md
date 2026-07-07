---
version: "alpha"
name: "Next Gen Xbox Emby - Artwork-Backed Matte Fluent"
description: "A dark Xbox/TV visual system for a personal Emby library, built on cool graphite matte chrome, artwork-backed blur only when real media color sits behind it, borderless controller focus, and sparse green signal color without retro-futurist glow."
colors:
  primary: "#252D35"
  on_primary: "#EEF3F6"
  play_accent: "#78B985"
  secondary: "#5D8F68"
  on_secondary: "#031006"
  tertiary: "#84909A"
  neutral: "#05070A"
  canvas: "#05070A"
  canvas_alt: "#080D12"
  surface: "#10161C"
  surface_raised: "#202832"
  surface_overlay: "#D910161C"
  artwork_blur_surface: "#D910161C"
  artwork_blur_tint: "#33080D12"
  hairline: "#2E3944"
  focus: "#EEF3F6"
  focus_fill: "#8068727A"
  high_visibility_focus_edge: "#66EEF3F6"
  success: "#78B985"
  warning: "#D3A64A"
  error: "#D66A5F"
  danger: "#E05F5F"
  text: "#EEF3F6"
  text_muted: "#A9B3BA"
  text_subtle: "#73818C"
  scrim: "#D905070A"
  transparent: "#00000000"
  shell_rail: "#E6080D12"
  immersive_scrim: "#6605070A"
  chrome_hover: "#E6202832"
  chrome_pressed: "#F02B3540"
  hero_gradient_start: "#F205070A"
  hero_gradient_end: "#6605070A"
  library_artwork_wash: "#A6080D12"
  section_artwork_wash: "#9C080D12"
  artwork_dim: "#26000000"
  hero_poster_dim: "#1A000000"
  modal_scrim: "#CC05070A"
  playback_drawer: "#F010161C"
  button_disabled_background: "#4D10161C"
  button_disabled_foreground: "#7AA9B3BA"
  button_hover_border: "#516170"
  button_disabled_border: "#332E3944"
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
    accentColor: "{colors.play_accent}"
    rounded: "{rounded.md}"
    padding: "20px 10px"
    height: "52px"
  button_secondary:
    backgroundColor: "{colors.surface_raised}"
    textColor: "{colors.text}"
    rounded: "{rounded.md}"
    padding: "20px 10px"
    height: "52px"
  focus_state:
    backgroundColor: "{colors.focus_fill}"
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
    backgroundColor: "{colors.shell_rail}"
    defaultState: "collapsed_or_summoned"
    invocation: "left_source_guide"
    peekWidth: "0px or 72px after platform validation"
    expandedWidth: "248px"
    extendedSources: "More / Source Hub"
  section_title:
    fontSize: "24px"
    fontWeight: "600"
    textColor: "{colors.text}"
  list_artwork:
    defaultSize: "76px"
    compactSize: "64px"
    rounded: "{rounded.md}"
  immersive_viewer:
    scrim: "{colors.immersive_scrim}"
    controlBackgroundColor: "{colors.surface_overlay}"
    pageMargin: "56px 28px 56px 40px"
---

## Overview

Artwork-Backed Matte Fluent is the visual language for Next Gen Xbox Emby. It keeps the Xbox dark-room feel and Fluent Design discipline, borrows the content-led restraint of Apple TV where it helps, and rejects the older "technology UI" vocabulary: neon cyan portals, glass prisms, glowing rings, fake holograms, perspective beams, and busy blue-black sci-fi dashboards.

The interface should feel like a calm media device in a dark room: cool graphite matte chrome, precise controller state, real artwork carrying the color, and no decorative glass layer on empty graphite. Blur is allowed only when it is backed by real media color: a focused banner, movie poster, backdrop, or video frame. Outside artwork and exceptional warning/error/danger states, normal chrome should stay grayscale with sparse muted green signals.

This document is visual-system first. It does not define API shape, playback behavior, or final page implementation. It does define the shell, density, and information-architecture constraints that materially affect visual design. When a page spec and this file overlap, this file is the source of truth for visual tone, tokens, density, chrome hierarchy, and visual anti-patterns.

## Design Thesis

The product is not "futuristic media tech." It is a personal library on a television. The visual system should therefore be:

- cinematic, but not theatrical decoration;
- Fluent, but not generic Windows desktop;
- Xbox-ready, but not Xbox-branded;
- premium, but not Apple imitation;
- quiet enough that posters, backdrops, and subtitles remain primary.

The signature is **artwork-backed atmosphere plus borderless focus**. Focus should read from ten feet through scale, luminance, local dimming, content priority, and matte selected fill before it uses any edge. Depth comes from real artwork, cool graphite luminance steps, spacing, and scrim, not from highlit borders, floating shadows, or glass applied to plain backgrounds.

## Design Sources

This file follows the DESIGN.md pattern: machine-readable YAML tokens first, then human-readable rationale. The format is inspired by Google Labs' DESIGN.md specification, where YAML gives agents exact values and Markdown explains how to apply them.

Local design-skill references used for structure and quality gates:

- `clean`, `sleek`, and `minimal`: reduced visual clutter, limited color, explicit states.
- `spacious` and `geometric`: 8-point rhythm, stable TV grids, precise component geometry.
- `material`: layered surfaces, accessible state definitions, tokenized motion logic.
- `premium`: Apple-like precision and lifted content focus, used without copying Apple brand styling.
- `power`: high-end dark restraint and near-monochrome chrome.
- `glassmorphism`: used only as a negative constraint and for blur vocabulary; this system rejects decorative glass as a style.
- `enterprise`, `contemporary`, and `futuristic`: cool system precision and technical clarity; their saturated blue/purple, sci-fi typography, and dashboard cues are rejected for this product.
- `stitch`: human-facing DESIGN.md companion structure.

External TV references used as pattern inputs: Xbox app navigation/focus restraint, Apple TV/tvOS content-led pages, older iOS-style translucent materials over real content, Android TV focus-state clarity, Netflix TV discovery/navigation tradeoffs, Plex big-screen source navigation, Emby Android TV home composition, Jellyfin Android TV media sections, Infuse server-native list handling, Kodi/Estuary media-center breadth, and streaming TV app artwork-led layouts. These references inform principles and thresholds; this document is not a clone of any platform.

Reference translation rules:

- **Apple TV / tvOS:** content should carry the color and atmosphere. Translate this into poster/banner-led surfaces, subtle scale, luminance, and local dimming. Do not translate it into Apple-branded glass, full-page translucency, glossy highlights, or repeated shadows.
- **Xbox / console apps:** controller focus must be unambiguous from ten feet. Translate this into predictable focus geometry, selected fills, stable rails, and clear D-pad movement. Do not make focus depend on a thin neon outline.
- **Android TV / Google TV:** focus state can be composed from multiple signals. Translate this into scale, luminance, matte fill, and context dimming first; do not adopt shadow or outline as the default language.
- **Fire TV / streaming TV apps:** the screen is viewed at distance and should minimize chrome load. Translate this into large artwork, restrained labels, strong safe areas, and simple state transitions.
- **Netflix / TV streaming app class:** navigation should be fast, metadata should appear early enough to reduce details-page hopping, and personal hubs can group saved/in-progress content. Translate this into efficient shortcuts, clear focused-title context, and rail-led discovery. Do not copy Netflix's top-only navigation as the primary model; it is optimized for a narrower streaming catalog.
- **Plex big-screen apps:** personal media IA needs pinned sources, a left source sidebar, `More` for unpinned sources, home rows driven by sources, and source-level tabs such as Recommended, Library, Collections, Playlists, and Categories. Translate this into a source-aware Guide and high-density home rails.
- **Emby / Jellyfin TV clients:** Home must surface unfinished items, next episodes, latest media, Live TV, music, photos, and server-specific sections. Translate this into a mixed media dashboard, not a hero-only streaming page.
- **Infuse:** polished third-party clients can preserve server-native lists and collections while feeling premium. Translate this into respecting Emby-provided lists, search results, collections, and library structure instead of flattening everything into generic recommendations.
- **Kodi / Estuary:** full media-center clients require broad routes and optional customization. Translate this into a clear default source model plus room for More/Source Hub, not visible taxonomy overload.
- **Disney+ / Apple TV app class:** media artwork is the emotional color system. Translate this into grayscale chrome and artwork-led pages. Do not add page-wide accent washes competing with posters.
- **Older iOS material / vibrancy:** blur is meaningful only when it samples real content underneath. Translate this into dark translucent panels over active artwork or video, with readable foreground contrast. Do not use the newer Liquid Glass vocabulary of bright rims, refraction, specular highlights, or material-as-spectacle.

Reference links reviewed for this pass:

- [TV Streaming And Personal Media Client Research](design-research/2026-07-07-tv-streaming-and-personal-media-clients.md)
- [Apple Human Interface Guidelines: Focus and selection](https://developer.apple.com/design/human-interface-guidelines/focus-and-selection)
- [Apple Human Interface Guidelines: Materials](https://developer.apple.com/design/human-interface-guidelines/materials)
- [Apple Developer Documentation: SwiftUI Material](https://developer.apple.com/documentation/swiftui/material)
- [Microsoft Learn: Gamepad and remote control interactions](https://learn.microsoft.com/en-us/windows/uwp/ui-input/gamepad-and-remote-interactions)
- [Microsoft Learn: Reveal Focus](https://learn.microsoft.com/en-us/windows/uwp/ui-input/reveal-focus)
- [Plex Support: Navigating the Big Screen Apps](https://support.plex.tv/articles/navigating-the-big-screen-apps/)
- [Plex Support: Customizing the Big Screen Apps](https://support.plex.tv/articles/customizing-the-apps/)
- [Emby for Android TV](https://emby.media/emby-for-android-tv.html)
- [Jellyfin Android TV v0.12 UI update](https://jellyfin.org/posts/android-tv-12/)
- [Infuse 7.7 Direct Mode](https://firecore.com/blog/infuse-77-direct-mode)
- [Android Developers: TV focus system](https://developer.android.com/design/ui/tv/guides/styles/focus-system)
- [Amazon Fire TV: Design and User Experience Guidelines](https://developer.amazon.com/docs/fire-tv/design-and-user-experience-guidelines.html)
- [Netflix Tudum: Netflix's New Layout](https://www.netflix.com/tudum/articles/netflix-new-tv-layout)

## Colors

### Palette Roles

- **Canvas `#05070A`:** true app background. Use for full-screen shell and playback-adjacent surfaces. It should read black on TV without crushing text.
- **Canvas alternate `#080D12`:** subtle cool full-screen variation for non-playback pages.
- **Surface `#10161C`:** default panel, toolbar, sheet, and card fallback.
- **Raised surface `#202832`:** selected fill base, modal body, elevated controls, and neutral action surfaces. It is a luminance step, not a shadow/elevation mandate.
- **Overlay `#D910161C`:** dark overlay panel over artwork or video when blur is unavailable or undesirable.
- **Artwork blur surface `#D910161C`:** dark translucent material used only when a real poster, banner, backdrop, or video frame sits underneath and contributes color. It must never be used over plain graphite canvas.
- **Artwork blur tint `#33080D12`:** optional cool dark tint over the sampled artwork before blur. Use it to stabilize contrast, not to create a colored wash.
- **Hairline `#2E3944`:** dividers and inactive card borders. Hairlines support structure; they must not become the focus language.
- **Text `#EEF3F6`:** cool off-white primary text. It keeps the interface precise and slightly technological without blue glow.
- **Muted text `#A9B3BA`:** metadata and secondary labels.
- **Subtle text `#73818C`:** timestamps, disabled labels, and tertiary metadata.
- **Primary action surface `#252D35`:** neutral Play, Resume, and primary command surface. It is not a green button.
- **Play accent `#78B985`:** tiny play/confirm signal only: play glyph accent, micro tick, or small status dot. Never use it as a large fill.
- **Focus `#EEF3F6`, `focus_fill #8068727A`:** borderless focus state system. Luminance, scale, selected fill, and local dimming do the work. Do not treat this as a perimeter-frame recipe.
- **High-visibility focus edge `#66EEF3F6`:** accessibility fallback only, used when platform or user settings require a visible edge. It is not the default visual style.
- **Secondary / progress `#5D8F68`:** muted green for active playback, current resume, focused watched-progress, and explicit watched/success state. It is darker and quieter than `play_accent`; ordinary passive progress can use neutral gray.
- **Tertiary `#84909A`:** steel-gray informational state only, such as diagnostics and neutral playback capability badges. It is gray, not blue accent color.
- **Warning `#D3A64A`, error `#D66A5F`, and danger `#E05F5F`:** exceptional semantic state colors only. They are allowed for warnings, failed states, destructive confirmation, and critical alerts, but never for normal chrome, focus, navigation, playback progress, or decorative emphasis.

Derived runtime colors such as `shell_rail`, `chrome_hover`, `chrome_pressed`, `library_artwork_wash`, `section_artwork_wash`, `modal_scrim`, playback drawer alpha, and disabled button states are also listed in the YAML block. They are not new palette moods; they are named alpha/state variants of the same canvas, surface, text, artwork blur, and hairline roles so skins can replace them deliberately.

### Usage Rules

Use neutral cool graphite surfaces for at least 90 percent of persistent chrome. Let media artwork provide most saturated color. The only normal non-gray UI color is green, and it must remain a signal, not page mood.

Do not create blue or cyan as the default technology mood. Cool graphite, steel gray, exact spacing, and artwork-backed content color provide the technological feel. Cyan is not part of the default focus system.

Never place saturated accents on top of saturated artwork without a dark scrim. A selected item over artwork should use scale, luminance, context dimming, and selected fill first. Do not add a visible edge unless it is a high-visibility accessibility fallback.

Green budget: no large green buttons, green nav markers, green poster borders, green hero accents, green page washes, or green settings sliders. Use `play_accent` only as a micro-signal inside a neutral primary action or as a tiny success/confirm state. Use `secondary` for active playback, current resume, focused watched-progress, and explicit watched/success confirmation only. Passive history progress and unfocused poster progress should default to neutral gray so green stays a signal rather than page texture.

No amber, red, cyan, purple, or blue as normal UI accents. Ratings, metadata badges, codec labels, route state, and ordinary status icons are grayscale by default. Warning, error, and danger are the only exceptions, and they must be local to the affected message, toast, badge, dialog, or destructive action.

Contrast checks from the initial token set:

- `text` on `canvas`: high contrast by design.
- `text_muted` on `canvas`: must meet WCAG AA for metadata at TV sizes.
- `text` on `surface`: must meet WCAG AA.
- `primary` on `canvas`: surface contrast should be visible without reading as an accent block.
- `focus` on `canvas`: must be visible from ten feet through combined fill, scale, luminance, and context dimming.
- `secondary` on `canvas`: must be visible as playback/resume progress without becoming page color or a large green band.

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
- Standard browsing screens must prioritize content rails over chrome. A collapsed left Guide, hidden Guide, or page-specific source rail are all valid candidates until Xbox implementation testing decides the final default state.
- The shell keeps a left source-guide model because a complete Emby client has more destinations than a streaming app: movies, TV, Live TV, music, photos, collections, playlists, favorites, unwatched, server libraries, and settings.
- The main content canvas should not be dominated by navigation. Opening or expanding the Guide may overlay, dim, or reserve a narrow collapsed area, but it must not make poster rails feel like secondary content.
- Search is a first-class shell destination and can have a controller accelerator, but it is not a persistent long top-right field on normal browsing pages.
- Home is a mixed media dashboard. Continue Watching, Next Up, Recently Added, Live TV, library/source, collection, and playlist rows are the default composition vocabulary.
- Continue Watching and Next Up are rails/lists, not single-item hero promos.
- A feature banner or hero, if used, must be compact enough that a high-value rail remains visible in the first viewport.
- Use an 8-point spacing rhythm. `4px` exists only for optical corrections.
- Horizontal media rails and grids must have stable card dimensions. Focus, badges, loading states, and labels cannot reflow the rail.
- Do not use nested cards. Repeated media items can be cards. Page sections are full-width regions, rails, or unframed layouts.
- Avoid decorative containers around whole pages. A page is a surface, not a card.
- Empty states and error states must reserve the same layout footprint as loaded content when practical.

Density should be "high-throughput TV dense": enough cards and rows visible to support personal-library browsing, but with TV-sized targets, legible metadata, and predictable D-pad geometry. It is closer to a mobile information budget than a sparse cinema poster wall, but it is not desktop density.

### Home Information Architecture

Home is a personal media dashboard, not a marketing landing page. It should make the user's own server feel alive and current before it promotes any single title.

Default Home vocabulary:

- Continue Watching rail.
- Next Up / Up Next rail for episodic content.
- Recently Added / Latest rows by relevant library.
- My Media / Libraries source rail or Guide entries.
- Live TV row or entry when available.
- Collections and Playlists as recognizable destinations.
- Favorites, Watchlist, and Unwatched as user-intent routes.
- Search as a shell destination and controller accelerator.
- Settings, user switching, and server switching in shell chrome, not inside content rows.

The exact ordering may become user/server configurable later. The visual system must still define stable row behavior, source labels, fallback artwork states, and focus treatment before that customization exists.

## Focus System

The default focus language is **borderless content priority**, not a drawn focus frame. This is the main difference between this system and older technology UI. The user should understand what is focused because that item becomes clearer, slightly larger, and more visually prioritized while its surroundings recede.

Focus priority order:

1. Increase local luminance and content clarity.
2. Scale media cards subtly within reserved focus-safe bounds.
3. Dim surrounding artwork or chrome when the focused object sits on a busy background.
4. Add a matte selected fill on controls and navigation.
5. Use motion timing to make the state change legible.
6. Add `high_visibility_focus_edge` only for accessibility/high-contrast fallback.

Media card focus:

- Scale from `1.015` to `1.035` for poster, wide, and episode cards when the layout has reserved focus-safe space.
- Increase artwork brightness slightly and reduce surrounding context by scrim or luminance, not by color tint.
- Do not add a visible drop shadow for normal page cards. If a dense poster rail needs separation, use local dimming or a darker backplate behind the rail.
- Do not use a complete bright perimeter frame as the normal card focus.
- Do not add glossy highlights, cool-white glints, or edge sheens to focused posters.
- `high_visibility_focus_edge` is allowed only for accessibility/high-contrast fallback or a platform-required focus visual. It must never become the default card focus.
- Focus scale must not push adjacent cards, rail labels, or OSD controls. Reserve the scale envelope in the rail/grid spacing before applying the focus effect.

Navigation and command focus:

- Use `focus_fill` as a matte cool-gray/steel-gray selected surface.
- Brighten the icon and label to `text`.
- Keep inactive icons at `text_muted` or below.
- Avoid permanent outlines around inactive items.
- Separate current route from current focus in the Guide. The focused destination gets the stronger matte fill; the active route may use text lift or a quieter fill. If they are the same destination, show only one state treatment.

Accessibility focus:

- A high-visibility focus mode may add `high_visibility_focus_edge` or a larger fill delta.
- The stronger mode must remain cool-neutral; it should not introduce cyan, neon green, or page-wide glow.
- Reduced-motion mode must keep fill, luminance, and context dimming so focus remains understandable without scale animation.

## Theme Tokenization Status

Shared TV resources should continue migrating into `App.xaml` before page-local constants are introduced. As of the 0.1.0.129 theme-token pass, these page families are tokenized:

- Shell: Guide chrome color, collapsed/expanded widths, standard and immersive page margins.
- Text: page title/subtitle, section title, subsection title, option label, status, panel title, body, muted body, badge, and diagnostics text styles.
- Surfaces: panel surface, list button, icon button, nav button, modal scrim, artwork dim, hero wash, details wash, playback canvas, playback overlay, playback drawer, and immersive control brush.
- Media rows: shared `TvListButtonStyle`, `TvListArtworkSize`, and `TvCompactArtworkSize` for browse-only list shells such as Live TV and Music.
- Poster grids: shared `TvPosterGridItemStyle`, `TvPosterGridItemMargin`, `TvPosterCardWidth`, `TvPosterCardHeight`, `TvPosterCardCornerRadius`, `TvPosterCardScrimPadding`, poster title/meta text styles, fallback-initial style, and empty-state title/body styles for Library and Search.
- Shared view usage: Home, Library, Search, Details, and Playback no longer own page-local raw hex colors for artwork dimming, sheet scrims, playback overlays, action surfaces, or play-accent foreground marks.
- Playback chrome uses the same canvas and surface RGB families as the rest of the app. The OSD and drawer may vary alpha for subtitle/video readability, but they must not introduce a separate pure-black palette branch.
- App identity: `tools/Generate-AppIconAssets.ps1` owns the current icon color and geometry tokens for regenerating the Store, square, wide, and splash PNG assets.

Future skins should override these resources first. Page code may read token values for navigation math, as Search now does for poster-grid column counts, but should not define new color, focus, or repeated artwork-size constants unless the value is truly page-specific.

## Theme Tokens And Skinning

The implementation should express repeated visual choices as XAML resources before they spread across pages. Colors, brushes, page margins, panel padding, typography styles, focus thickness, card radii, and common panel shapes belong in `App.xaml` or a future merged theme dictionary. Page XAML can still own one-off layout structure, such as a two-column diagnostics grid, but it should not duplicate raw font sizes, surface colors, or focus styling when a shared token exists.

This keeps Artwork-Backed Matte Fluent replaceable. A future skin should be able to swap resource dictionaries for color, typography, spacing, and panel treatment without rewriting Emby data flow, controller navigation, or playback behavior. Skins may change visual language, but they must preserve the TV safety rules in this file: visible focus, stable card dimensions, dark scrims over artwork, and no hover-only affordances.

### Implementation Contract

Treat the YAML tokens at the top of this file as the design-source map, and treat XAML resources as the runtime skin contract. New UI work should first consume existing semantic resources; if a new visual value appears more than once, promote it into a named resource or style before adding more pages that depend on it. The goal is for a future skin to replace colors, typography, spacing, focus treatment, card shape, and overlay treatment through resource dictionaries rather than code edits.

The YAML component token is named `focus_state`, not `focus_frame`, intentionally. Future resource names should describe focus as a state recipe made of fill, scale, luminance, context dimming, and optional accessibility edge fallback, not as a mandatory border.

Page-local XAML may define structure-specific measurements, such as a diagnostics column width or a one-off media layout breakpoint. Page-local XAML should not define raw hex colors, repeated font sizes, repeated safe-area margins, focus border brushes, common card radii, or shared artwork sizes. Generator scripts such as the app-icon asset generator should keep their own visual tokens in one clearly labeled block and document which DESIGN.md roles they correspond to.

The current XAML token migration has started with:

- app shell and Guide rail resources: `AppShellRailBrush`, `TvGuideCollapsedWidth`, and `TvGuideExpandedWidth`;
- immersive viewer resources: `AppImmersiveScrimBrush`, `AppImmersiveControlBrush`, and `TvImmersivePageMargin`;
- shared TV text, panel, diagnostics, icon button, nav button, list button, badge text, and settings checkbox styles;
- shared overlay/action resources: `AppOnActionBrush`, `AppArtworkDimBrush`, `AppModalScrimBrush`, `AppPlaybackOverlayBrush`, `AppPlaybackDrawerBrush`, and the hero/details/home artwork wash colors.
- shared poster-grid resources for Library and Search cards, including card size, item margin, corner radius, scrim padding, card title/meta text, fallback initials, and empty-state typography.
- playback canvas, OSD, and drawer resources reuse the DESIGN.md canvas/surface color families, with only opacity differing by layer.

New UI surfaces should follow this migration order:

- use existing semantic brushes and spacing resources first;
- promote repeated hard-coded values into named resources or styles;
- keep interaction state treatment semantic, such as focus, play, progress, danger, and muted text;
- avoid page-local visual constants unless they describe a unique layout constraint.

### Drift Guards

During implementation review, reject changes that introduce any of these visual drifts:

- a new raw hex color in page XAML for normal chrome;
- star, rating, codec, source, or route badges using yellow, red, blue, purple, or cyan outside exceptional warning/error/danger states;
- poster-card focus implemented primarily as a thick `BorderBrush`;
- green used for active navigation, generic selected state, settings sliders, favorite/watched buttons, or page headers;
- blur applied to a whole page, plain graphite background, poster card, grid container, or button;
- fallback artwork implemented as abstract gradients, generated noise, or decorative shapes.
- a blur-backed design that does not name the Emby image candidate supplying the sampled background.

If a page needs an exception, document the semantic state first and add a token before using the value.

## Depth and Artwork-Backed Blur

Depth is matte and structural. The design should resemble calm cool graphite beside real media artwork, not transparent glass floating in blue light.

Allowed depth tools:

- 1px hairline boundaries;
- luminance step from inactive to selected surfaces;
- dark scrim over artwork;
- focus scale for selected media cards, within reserved layout bounds;
- artwork-backed blur when a rail, sheet, or OSD covers an active poster, banner, backdrop, or video frame.

Prohibited depth tools:

- neon outer glow as a brand device;
- glass prism effects;
- decorative translucent panels over plain graphite;
- highlit borders combined with blur;
- drop shadows on normal page cards, navigation rows, search boxes, or banners;
- holographic beams;
- decorative bokeh, orbs, particles, or streaks;
- shiny metal/card reflections;
- blue gradients used as generic "premium tech";
- warm golden page washes that make the app feel sepia or boutique rather than precise.

### Artwork-Backed Blur Rules

Blur is allowed only when it improves separation over real media color that the Emby client can actually fetch. It should behave like older iOS material/vibrancy: it samples content underneath, darkens it for contrast, and lets foreground text remain readable. It must not become the primary visual identity.

Required blur recipe:

- There must be meaningful sampled content behind the material, selected through the Emby artwork feasibility contract below: active banner, focused item backdrop, movie poster field, or video frame.
- Use only `artwork_blur_surface` or an opacity variant of the same `surface` RGB family.
- Keep blur functional: 16px to 36px equivalent blur for still artwork; 8px to 20px for video-adjacent OSD where subtitle readability matters.
- Keep the final layer dark enough for text: 80 percent to 92 percent effective darkness after scrim and blur.
- Do not add highlit borders, colored outlines, glossy highlights, refraction, or drop shadows to blur layers.
- Use no more than one major blur region per screen, plus one transient OSD/dialog if needed.
- If the background is plain graphite, low-contrast, missing, protocol-unavailable, or performance makes blur unstable, use `surface_overlay` with no blur.

Blur must not appear on poster cards, page backgrounds, grid containers, hero washes without active artwork, buttons, chips, search boxes, navigation rows, or generic settings rows. Those stay matte.

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

Primary buttons use neutral `primary` action surface with `on_primary` text. A Play or Resume button may include a tiny `play_accent` glyph, tick, or side mark, but the button itself is not green. Secondary buttons use `surface_raised` or `surface_overlay` depending on whether they sit on canvas or artwork. Buttons are 52px high by default, with 6px radius and a minimum readable label width.

Buttons may keep a clear matte rectangle because they are command targets. Do not copy that rectangle treatment onto poster focus. Media selection should feel like lifted content; command selection may feel like a focused control.

Visual states:

- Default: matte fill, no glow.
- Focus-visible: `focus_fill` selected surface and text/icon lift to `text`. Do not use a bright complete outline, glossy highlight, or shadow as the normal button focus.
- Pressed: fill darkens or compresses by luminance, not by glow.
- Disabled: reduce text and border contrast but keep the control shape visible.
- Loading: preserve button width and label footprint.

### Media Action Rows

Details and playback-adjacent pages use a compact media action row rather than hiding primary state inside a menu. The first action is Play or Resume and uses the neutral `primary` action surface. Its play glyph or tiny side mark may use `play_accent`, but the button fill stays cool graphite. Adjacent actions such as Restart, Favorite, Watched, Refresh, stream selectors, and playlist or collection actions use secondary matte buttons.

Visual rules:

- Keep every action at the standard 52px TV target height.
- Use icons plus short labels for actions that change item state.
- Do not use green fills for any action. Do not use `play_accent` for Favorite, Watched, Refresh, neutral selectors, settings sliders, or navigation.
- Preserve stable button widths when labels change, such as Add favorite to Remove favorite.
- The focus treatment remains neutral even when the focused action is Play or Resume.
- If an action is unavailable, keep the rest of the row aligned; do not let hidden controls collapse the primary Play target.

### Cards

Media cards are artwork-first. The artwork crop, title, progress, and borderless focus treatment are the card's identity.

Visual rules:

- Poster, wide, square, and episode cards must each have fixed aspect ratios.
- Movie, series, collection, and playlist library rows default to vertical poster cards using `Primary` artwork. Do not represent these rows with 16:9 landscape cards in visual previews unless the actual Emby item provides a suitable wide image and the row's semantics call for it.
- Continue Watching, Next Up, and episode rows use one wide resume-card anatomy per row: full-bleed artwork crop, title/metadata, and progress in stable positions. Prefer real wide sources in this order: `Thumb`, `Backdrop`, `Banner`, then episode still when available for the item. If the item only has `Primary`, crop it directly into the wide card rather than introducing a separate vertical-poster layout. This matches common TV media-client behavior and preserves row density, but the selected-state treatment must compensate for crop risk through stronger title protection and focus clarity.
- Default cards use a controlled bottom black gradient/scrim band for title and metadata. The scrim should be soft, not a hard black label, and should preserve enough of the artwork to avoid feeling like a cheap caption strip.
- Unfocused resume wide cards must use only the bottom black gradient/scrim for text protection. Do not apply blur, frosted glass, acrylic, backdrop material, or a translucent boxed text panel to unfocused cards.
- Do not use blur or frosted-glass material on every card by default. Repeated per-card blur creates visual noise, weakens focus hierarchy, and is expensive to render on TV/Xbox.
- A focused wide card may upgrade its text zone from gradient/scrim to a very subtle dark artwork-backed material when the artwork underneath is meaningful and the platform can render it reliably. This is a readability and focus affordance, not decoration.
- Focused poster-grid cards should prefer stronger scrim, text lift, scale, and local dimming before using blur. Dense poster grids usually do not need blur on selection.
- Any focused-card blur must be limited to the title/metadata zone, keep the layer dark, avoid bright rims, and degrade cleanly to a non-blurred translucent scrim.
- Focused resume wide cards use this default state recipe: card scale `1.015` to `1.025`, artwork zoom `1.035` to `1.06`, slight artwork brightness lift, surrounding cards dimmed by luminance, integrated bottom information material, and muted green only for active/current progress. Do not add a bright perimeter frame, neon edge, or separate poster thumbnail to make focus readable.
- The focused information material should feel attached to the card, not like a nested floating card. It may sit over the normal bottom black gradient, but the two layers must read as one continuous text-protection zone: the gradient carries the image into darkness, and the material is a low-height local enhancement around the title/progress only. Do not use a hard rectangular glass box, do not raise the text zone to half the card, and do not add a visible perimeter edge. If blur is unavailable, keep the same low-height translucent scrim without blur.
- Active, current-resume, or focused watched progress may use `secondary`, 3px to 5px high depending on card size. Passive unfocused history progress should use neutral gray.
- Focus uses scale, brightness, context dimming, and optional selected backplate. Do not use a bright perimeter frame or drop shadow as the default card focus.
- Fallback cards use `surface` plus a simple text label, not generated abstract art.

### Panels and Sheets

Panels use `surface_overlay` over plain content and `artwork_blur_surface` only over active artwork/video when blur improves separation. Sheets are grounded surfaces. They should not look like floating glass plates.

Rules:

- One matte panel over a page is normal. One artwork-backed blur region is the visual maximum for persistent UI.
- Use scrim to protect text. Do not blur so heavily that artwork becomes a generic abstract background.
- If the panel sits over plain graphite, do not blur it.
- Panel text uses `body_md`, `metadata`, and `label`, not hero type.

### Navigation Chrome

Navigation chrome is a quiet left source Guide. It provides fast access from anywhere and preserves a product model broad enough for a real Emby client. The default state may be collapsed, summoned, or page-adaptive after Xbox implementation testing, but the visual system should not collapse into a Netflix-style top-only nav.

Rules:

- Default browsing state: navigation must be quiet enough that content rails remain primary. If a collapsed rail is visible, it should be narrow, matte, and low-contrast. If the Guide is hidden, it must remain quickly reachable from controller navigation.
- Guide-open state: a left matte overlay or expanded rail around `248px` wide with Search near the top, primary destinations below it, `More` / Source Hub for unpinned libraries and secondary media types, and Settings pinned to the bottom. It may dim content behind it, but it should not make the page feel rebuilt around the menu.
- The Guide is source-aware. It must handle movies, TV, Live TV, music, photos, collections, playlists, favorites, unwatched, and server-defined libraries without forcing all of them into the first-level visible set.
- `More` / Source Hub is a designed destination for unpinned libraries, folders, and lower-frequency routes. It is not an overflow trash can.
- The Guide surface is matte graphite. Do not apply artwork-backed blur to the Guide unless real active artwork visibly sits behind it and the blur improves readability; default to matte.
- Icons should be Fluent-compatible, simple, and stroke-based.
- Do not use filled neon icons.
- Do not use permanent bright borders around inactive nav.
- Do not use green nav markers.
- Expanded labels must align to the same grid as icon targets.
- Search belongs as a first-class Guide/source action, Search page, and optional controller accelerator. Do not add a persistent long search field to normal Home, Library, Details, or playback-adjacent browsing pages.

### Playback OSD

Playback UI is subordinate to video and subtitles.

Visual rules:

- OSD panels are dark matte overlays by default. Use artwork-backed blur only over active video/artwork when it improves readability.
- Keep the bottom subtitle region readable. If a panel competes with subtitles, move or shorten the panel before adding stronger decoration.
- Transport controls use neutral focus fill for the current controller target, neutral action surface for Play/Resume, tiny `play_accent` only inside the play glyph/confirm mark, and `secondary` for playback progress. Non-playback sliders such as brightness, audio, filters, or diagnostics stay neutral gray.
- Diagnostics use `mono` and `tertiary`, never the focus or play accent colors.

## Artwork

Artwork is the emotional layer. The UI system should not invent atmosphere when the user's media already provides it.

### Emby Artwork Feasibility Contract

Designs may depend only on image types the current Emby client requests and maps: `Primary`, `Backdrop`, `Thumb`, `Banner`, and `Logo`. The current API query path requests `EnableImages=true`, `EnableImageTypes=Primary,Backdrop,Thumb,Banner,Logo`, and `ImageTypeLimit=1`, then builds image URLs through `/Items/{itemId}/Images/{imageType}` with a max-width cap. Do not design a required surface around unavailable artwork types, multiple backdrop choices, extracted video stills, generated gradients, or third-party provider art.

Every artwork-backed material in a mockup or implementation review must name its source image role. A preview may look cinematic, but it is not accepted as a system rule unless the same composition still works when Emby returns only `Primary` or no usable image for that item.

Artwork-backed blur is allowed only after a matching `EmbyArtworkPolicy` candidate exists:

- Hero and details backdrop source: `Backdrop`, then `Thumb`, then `Banner`, then `Primary`.
- Item wide-card source: `Thumb`, then `Backdrop`, then `Banner`, then `Primary`.
- Library and server-section wide-card source: `Thumb`, then `Backdrop`, then `Banner`, then `Primary` from the library view or section parent item.
- Poster-card source: `Primary`, then `Thumb`, then `Backdrop`; use it as poster artwork, not as a page blur source unless it is the focused item and the crop remains recognizable.
- Logo source: `Logo` only when available; never require it for navigation or title readability.

Fallback rules:

- If no candidate exists, use matte `surface`/`surface_overlay`; do not blur, synthesize, or substitute abstract artwork.
- If the only candidate is `Primary` and the target is a wide resume card, crop `Primary` directly into the wide card. Keep the crop center-stable unless the app later has reliable focal-point metadata. Do not add a secondary mini-poster inside the card. If the cropped artwork becomes unusably blank or text-only, fall back to matte rather than inventing generated art.
- Child item artwork can populate child cards. It must not become the parent section's persistent blur background unless the section or parent has no usable artwork and the view explicitly presents that child item as the active subject.
- Playback OSD may sample the live video layer only if the platform compositor provides it cheaply and reliably. Otherwise use matte `surface_overlay`; do not require video-frame extraction from Emby.
- Live TV and program surfaces may use available channel/program artwork, but must assume sparse images and keep matte fallbacks visually first-class.

Rules:

- Visual reviews must include at least one realistic poster stress-test screen, not only abstract landscapes, grayscale placeholders, or component boards. Use fictional but movie-like covers with faces, typography blocks, high-contrast crops, saturated content color, and varied genres to test whether chrome, focus, artwork-backed blur, and OSD remain legible.
- Visual reviews must include three artwork-availability states: rich `Backdrop`/`Thumb` available, only `Primary` available, and no usable artwork. The matte fallback must feel designed, not like a broken cinematic layout.
- Backdrops may be used as wide environmental washes only when they are darkened, cropped, and protected by scrim.
- Poster and backdrop artwork should keep recognizable subject matter when they are presented as artwork. `Primary` cropped into a wide resume card is allowed even though it can lose part of the poster composition; compensate with title readability, progress clarity, and selected-state emphasis instead of adding a second visible poster.
- Use one artwork-driven accent at most per view, and only if it does not conflict with `primary`, `secondary`, or state colors.
- Never use AI-generated abstract gradients as a fallback for missing media.
- Missing artwork fallback is a matte surface with text and a small media-type icon.
- Server-configured home section cards use the section or parent item artwork first, especially `Thumb`, `Backdrop`, `Banner`, and `Primary` in that order for wide cards. Child item artwork is only a fallback when the section itself has no consumable image.
- Details organize sheets use the destination item artwork first for collection and playlist rows, with the same wide-card preference: `Thumb`, `Backdrop`, `Banner`, then `Primary`. Child item posters are not the primary visual source for these rows.

## Icons and App Identity

The previous glowing "Library Portal" icon direction is rejected for final identity work. It is too close to an older stereotype of tech design: cyan rings, portal beams, glass cards, and neon progress arcs.

Future icon work should use this vocabulary instead:

- matte rounded-square tile;
- a quiet TV media shelf or library aperture;
- one high-contrast controller focus affordance;
- one neutral play/confirm surface with at most a tiny muted green play accent when scale allows;
- one muted green progress base when scale allows;
- no cyan glow;
- no official Emby, Xbox, Microsoft, or platform logos;
- no film-strip cliche;
- no generic play triangle standing alone;
- legible at 44px with a stable silhouette.

Current production icon direction:

- **Player Lift Mark:** a matte cool-graphite tile with a compact playback viewport, a lifted neutral play surface, a tiny muted green play accent only if it remains legible, subtle subtitle/audio status marks, and one muted green progress base. It must not depend on the current product name, initials, or embedded text, because the brand name can change.

The production icon generator must map colors directly to this document's tokens: `canvas #05070A`, `surface #10161C`, `surface_raised #202832`, `hairline #2E3944`, `focus #EEF3F6`, `focus_fill #8068727A`, `play_accent #78B985`, `secondary/progress #5D8F68`, `text #EEF3F6`, and `text_muted #A9B3BA`. Raster assets should not introduce a separate palette.

Superseded concepts aligned with the older system:

- **Focus N Mark:** an abstract Next/Navigation `N` built from media-slab forms. It was readable as a brand mark, but depended too much on the current name.
- **Cinema Shelf Mark:** a dark TV shelf with a left Guide rail, horizontal content rails, one focused media card, a green play surface, a cyan L-shaped focus edge, and an amber progress base. It was more product-specific than the portal concepts, but still read too much like a miniature UI screenshot at app-icon scale and carried too much old focus language.
- **Matte Library Slat:** layered black media rectangles with one crisp focus edge and one green play/confirm surface.
- **Screen Room Mark:** a dark screen shape with a subtle warm progress base.
- **Quiet Play Aperture:** a flat negative-space play cutout inside a neutral media tile.

## Motion

Motion should clarify controller state. It should not be ambient decoration.

Allowed motion:

- focus entrance, 80ms to 140ms;
- sheet entrance, 140ms to 220ms;
- card focus scale, 1.02 to 1.04 when reserved space prevents layout shift;
- opacity fade for scrim and overlays;
- progress changes that track playback state.

Rules:

- No looping decorative animation.
- No particle systems.
- No parallax that changes reading order. If tilt/parallax is used, it must be shallow enough for controller/D-pad predictability.
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
- keep focus unmistakable from ten feet through scale, fill, luminance, and context dimming;
- keep green as a micro play/confirm signal rather than a button or brand fill;
- keep green limited to play/confirm/progress signals;
- preserve stable sizes during loading, focus, and error states;
- use matte layers and controlled scrims;
- use blur only when it samples meaningful artwork or video behind it;
- keep page sections unframed unless they are repeated media cards or transient panels.

Don't:

- use cyan glow, portal rings, prism beams, or hologram language;
- use blue-purple gradients as a default dark UI mood;
- use large green fills, green settings sliders, green nav markers, or green poster focus;
- use warm golden or colored washes that make the app read yellow, sepia, blue, purple, or brand-gradient;
- use bright complete focus frames as the default card focus;
- use drop shadows or glossy highlight edges on normal page cards, navigation items, search boxes, or banners;
- use blur over plain graphite, missing artwork, generated gradients, or abstract fallback art;
- use Liquid Glass-style refraction, bright rims, or material spectacle;
- place cards inside cards;
- use stock abstract backgrounds;
- use all-caps metadata styling as decoration;
- let focus animation move neighboring content;
- hide primary visual state behind hover-only effects;
- use text-heavy explanations inside the app to compensate for unclear hierarchy.

## Migration Notes

Existing tokens and assets should migrate visually in a later implementation pass. This document does not make those code changes.

Suggested migration mapping:

- Current cyan `AppAccentColor #4EE7FF` should be retired from default focus. Map focus to `focus #EEF3F6`, `focus_fill #8068727A`, and `high_visibility_focus_edge #66EEF3F6` only for accessibility/high-contrast fallback.
- Current green `AppActionColor #78E68B` should shrink to `play_accent #78B985` and appear only as a micro-signal.
- Current warm `AppWarmColor #E4B84C` should be retired from default UI progress. Map progress to muted green `secondary/progress #5D8F68`.
- Current surfaces stay in the same dark family but should shift toward the matte `canvas`, `surface`, and `surface_raised` roles.
- Current generated app icon concepts under `docs/icon-concepts` are exploration artifacts only and should not define the final visual identity.

Known implementation drift to resolve later:

- `src/NextGenEmby.App/App.xaml` still defines the previous cyan `AppAccentColor`, warm `AppWarmColor`, and green `AppActionColor` token family.
- `SystemControlFocusVisualPrimaryBrush` currently follows the old accent color. Future work should remap high-visibility focus to the neutral focus family while preserving accessibility.
- `tools/Generate-AppIconAssets.ps1` still uses the older Player Focus Mark color/geometry language. Future icon generation should align with Player Lift Mark before producing final assets.
- Existing XAML may still use `BorderBrush`, `UseSystemFocusVisuals`, and current action/warm brushes for focus and state. That is expected until an implementation pass migrates visual resources; new work should not extend the old color language.
- Existing surface resources may still imply acrylic-like panels. Future work should replace generic acrylic with `artwork_blur_surface` only where an active poster/banner/video sits behind it.
- Main latest shell code still has a visible collapsed `72px` Guide rail. The visual target is now reopened as `collapsed_or_summoned`: keep the left source Guide model, then validate whether Xbox feels better with a 72px collapsed rail, a fully hidden Guide, or page-adaptive behavior.
- Any temporary top-right search-field preview is superseded. Search should remain a first-class shell destination plus the dedicated Search page and optional controller accelerator, not permanent long browsing chrome.

## QA Checklist

Before accepting a visual implementation based on this file, verify:

- The screen still reads as Xbox/TV dark Fluent, not a web dashboard.
- No neon cyan glow, prism, portal beam, decorative particle, or generic sci-fi motif appears.
- At least 90 percent of persistent chrome uses neutral cool graphite surfaces.
- Normal UI color outside gray uses muted green only.
- Green appears only as tiny Play/Resume accent, playback/resume/watched progress, or success confirmation, and never as a large fill.
- Warning, error, and danger colors appear only inside their exceptional local states.
- Progress/resume uses muted green or neutral gray only; no amber progress remains.
- Focus is visible from ten feet on every actionable element without depending on a bright full outline.
- Poster-card focus uses scale/luminance/context dimming first; command-button rectangles are not reused as poster focus frames.
- Normal page cards, navigation rows, search boxes, and banners have no decorative drop shadow or highlit border.
- Default browsing screens keep content rails primary, whether the left Guide is collapsed, hidden, or page-adaptive.
- Guide-open visual QA includes Search, More / Source Hub, and the full Emby destination family without green nav markers or bright outlines.
- Home visual QA shows Continue Watching and Next Up as rails/lists, not single-item hero promos.
- Blur appears only over meaningful active artwork/video and no more than one persistent artwork-backed blur region is visible at once.
- Focus, loading, and labels do not resize cards, rows, or buttons.
- Text uses Segoe UI Variable roles and never scales with viewport width.
- Primary text and key state colors meet WCAG AA contrast on their intended surfaces.
- Visual QA includes realistic fictional posters with faces, title blocks, and mixed color temperatures, not only abstract grayscale art.
- Visual QA includes fallback cases for `Backdrop` available, only `Primary` available, and no usable artwork. All three must remain intentional.
- Artwork remains recognizable and does not become a generic blurred gradient.
- Playback overlays do not make subtitles unreadable.
- Empty, loading, and error states follow the same visual system as loaded content.
- The app icon direction remains matte, legible at 44px, and free of official third-party logos.
