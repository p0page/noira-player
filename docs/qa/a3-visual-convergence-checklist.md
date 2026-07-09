# A3 Visual Convergence Checklist

Date: 2026-07-08

This checklist is the visual acceptance gate for the A3 Visual Convergence phase. It is intentionally stricter than the existing design-conformance checklist: functional navigation can pass while this checklist fails.

Older run log entries in this file may mention retired fixture routes. Treat those entries as historical evidence only; new runs must follow `docs/qa/ui-development-data-sources.md`.

## Source Targets

- `docs/a3-visual-convergence-rules.md`
- `docs/DESIGN.md`
- `docs/design-previews/A3-ideal-home-dashboard.png`
- `docs/design-previews/A3-ideal-library-poster-focus.png`
- `docs/design-previews/A3-ideal-details-atmosphere.png`
- `docs/design-previews/A3-ideal-playback-osd-native-material.png`

## Execution Rules

- Judge screenshots first. UIA, unit tests, and keyboard scripts only prove the right route is visible.
- Treat A3 images as AI-generated style targets, not exact mocks. Do not copy visible generation mistakes, impossible spacing, or details that conflict with `docs/DESIGN.md`.
- Do not fix interaction issues during an A3 visual batch unless they block capture.
- Follow `docs/qa/ui-development-data-sources.md` for current UI data-source rules.
- Use private real UI samples when comparing composition, density, focus treatment, and regression safety.
- Treat historical fixture/mock artwork as trace evidence only. It cannot by itself prove artwork atmosphere, poster-wall realism, or playback mood.
- Use real saved-session artwork as local stress evidence before accepting Home, Library, Details, or Playback visuals. Never commit those screenshots or media assets.
- On Windows desktop, capture installed-app screenshots with a DPI-aware process. A non-DPI-aware capture can crop only the upper-left portion of a high-DPI UWP window and produce false visual failures.
- Record all visual findings before implementing a batch.
- Keep evidence outside the repository unless the asset is deterministic, generated, and safe to commit.

## Status Values

- `Pass`: the screen is visually close to the A3 target and remaining work is detail tuning.
- `Concern`: the screen moves in the right direction but still has visible desktop/UWP drift, or follows an A3 pixel detail that does not make design/system sense.
- `Fail`: the screen reads as a desktop app, dashboard, form, or old design direction.
- `Blocked`: capture or route state prevents visual evaluation.
- `Not Run`: intentionally deferred.

## Universal A3 Gates

Every primary screen must satisfy these before page-specific acceptance:

- The screenshot is not dominated by desktop OS chrome, window title bar, white border, or system frame.
- The canvas reads as a dark room, not a flat black form page.
- Artwork or video is the dominant color source.
- Persistent UI chrome is graphite and low-contrast.
- Green is only a small play/progress/current/success signal.
- Large bordered form controls are absent from first read.
- Focus does not rely on a complete bright outline.
- Material/blur appears only over artwork or video.
- Page-local shadows, bright rims, and decorative glass are absent.
- Text is readable at TV distance and does not create nested hard boxes inside media cards.
- No-art fallback is matte/black and intentional.
- The page can be evaluated from a screenshot without explaining the interaction path.

## Batch A3-00 - Baseline Gap Audit

Goal: document how the current implementation differs from the four A3 targets before changing visuals.

| ID | Route | Target | Checks | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| A3-00.01 | Home private real sample or saved-session Home | `A3-ideal-home-dashboard.png` | Identify desktop chrome, hero/dashboard weight, rail density, artwork dominance, guide treatment. | Concern | The 0.1.0.277 saved-session capture dims the Home title/header and proves real rows can populate the first viewport, but Home still reads as a sparse top hero plus rows rather than the denser A3 media dashboard. |
| A3-00.02 | Movies private real sample or saved-session Movies | `A3-ideal-library-poster-focus.png` | Identify toolbar weight, poster density, focused poster treatment, text/meta spacing, fallback-card tone. | Concern | The 0.1.0.277 saved-session capture proves real poster density and no-art cases are visible, but the page title/count/toolbar and focused fallback tile still read more like a functional UWP poster grid than the A3 poster-wall target. |
| A3-00.03 | Details fixture and one real Details item | `A3-ideal-details-atmosphere.png` | Identify split-panel drift, action-row placement, atmosphere quality, poster-only fallback behavior. | Concern | Details now uses a page-spanning atmosphere layer and a low decision dock. Fixture capture validates structure; local-only real Emby samples validate that real artwork can carry the right side of the canvas. Latest passes fixed a false non-DPI-aware screenshot crop, made the decision dock viewport-visible through layout anchoring, reduced source/version chrome to the current source summary, narrowed/lowered the reading band, removed the dock's single large outer panel, and collapsed source/audio/subtitle decisions into lighter single-line chips. Remaining gap: the action row and source chip still read slightly more tool-like than A3. |
| A3-00.04 | Playback private real sample or visual playback route | `A3-ideal-playback-osd-native-material.png` | Identify whether the video/artwork field exists, OSD material quality, subtitle clearance, top status weight. | Blocked | Historical playback fixture captures were useful as route smoke tests, but mostly black video fields cannot validate native material, subtitle clearance, or artwork/video-led mood. |

Acceptance:

- A short finding table exists before implementation begins.
- Findings are visual, not interaction bugs.
- No private screenshots or media assets are committed.

## Batch A3-01 - Canvas And Shell

Goal: make every page start from the right dark-room base.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-01.01 | Any current app route | Desktop/window chrome is hidden, customized, or excluded from visual acceptance. | Pass | Desktop verification now uses dark title-bar chrome, so screenshots are no longer dominated by a white Windows frame. |
| A3-01.02 | Home and Library | Left Guide is visually quiet and does not compete with media. | Concern | The collapsed icon rail is quieter than the original side menu, but selected icon surfaces still carry enough weight to be visible in the first read. Keep it, but do not let active-route chrome become the visual anchor. |
| A3-01.03 | Home and Library | Page title/toolbar weight is reduced enough that media is first read. | Concern | The Home header now recedes, but Movies still starts with a large title/count/toolbar group and Home still reserves a sparse hero band. Media is stronger than before, not yet first read across both pages. |
| A3-01.04 | All primary pages | Background, raised surfaces, buttons, and fields share the same cool graphite family. | Not Run | Avoid blue/purple sci-fi drift and yellow warmth. |

Acceptance:

- A screenshot of each primary page no longer reads as a default UWP desktop window.

## Batch A3-02 - Home Media Wall

Goal: move Home toward the A3 media-wall target.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-02.01 | Home | First viewport shows multiple media choices and rails, not a single management-style hero panel. | Concern | The 0.1.0.277 clean capture shows multiple real-artwork rails and a recessed Home title, but the top resume block still consumes too much empty space before the rail wall takes over. |
| A3-02.02 | Home | Continue Watching cards use one wide-card anatomy with black scrim by default and subtle focused material only in the text/progress zone. | Concern | Real Continue Watching cards use wide artwork and black scrims. The anatomy is directionally right, but borders/focus chrome remain more visible than the A3 target. |
| A3-02.03 | Home | Media libraries/server sections are subdued source cards, not large bordered dashboard tiles. | Concern | Library cards use real artwork and darkened overlays. The current pass improves the surrounding header hierarchy, but card frames still read too explicitly outlined for the intended matte TV shelf. |
| A3-02.04 | Home | Real artwork creates the page color; chrome stays graphite. | Concern | Real posters and thumbnails now supply most color, and the page chrome is cooler/less green than earlier passes. The refresh button and selected-route rail material still need more recession before this can pass. |

Acceptance:

- Home resembles `A3-ideal-home-dashboard.png` in first-read hierarchy, density, and artwork-led color.

## Batch A3-03 - Library Poster Wall

Goal: move Library/Movie grid toward the A3 poster-wall target.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-03.01 | Movies grid | Sort/filter controls are light utility controls, not the visual anchor. | Concern | The 0.1.0.277 chrome pass makes the toolbar more compact in source, but the real screenshot still reads the title/count/sort/filter cluster before the poster wall. Continue shrinking utility chrome and moving visual weight into posters. |
| A3-03.02 | Movies grid | Poster density and spacing are close to A3: many visible posters, enough breathing room, no desktop table feeling. | Concern | Real capture has strong poster density and enough first-viewport inventory. The remaining issue is not density, but the page chrome and focus material making the grid feel more operational than cinematic. |
| A3-03.03 | Focused poster | Focus uses integrated matte backplate, subtle scale/luminance, and readable title/meta. | Concern | Current focus is readable, especially on no-art fallback, but still depends on a large boxy selected backplate. Use the A3 integrated matte backplate, subtle scale/luminance, and title/meta floor as the next visual recipe. |
| A3-03.04 | No-art fallback | Fallback cards are quiet and do not dominate the grid. | Concern | Real capture includes no-art tiles. The fallback is functional, but the focused no-art tile becomes one of the strongest blocks in the viewport; it should remain a quiet absence state even while focused. |

Acceptance:

- Library resembles `A3-ideal-library-poster-focus.png` in composition and focus mood.

## Batch A3-04 - Details Atmosphere

Goal: move Details toward full-screen artwork atmosphere.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-04.01 | Details private real samples | The page reads as one atmospheric canvas, not left form plus right poster. | Concern | Historical fixture artwork validated repeatable structure only; it is too abstract/dark to prove final atmosphere. Local-only real Emby artwork shows the right side can carry color and subject matter while the left scrim protects text. Needs more real samples across varied metadata/source cases before Pass. |
| A3-04.02 | Details private real samples | Title, metadata, badges, overview, and credits sit in a left information column with strong hierarchy. | Concern | The reading band now uses `TvDetailsContentMargin` `56,156,56,48`, `TvDetailsContentColumnWidth`/`MaxWidth` `680`, and an overview max width of `640` with three-line ellipsis, moving the content below the page-header zone so it reads more like a cinematic information band. The latest facts pass adds a passive first-viewport fact row and compact director/genre text, so Details no longer reads as only a title plus overview. Remaining gap: facts/credits are structurally useful but need broader real-artwork and localization stress before Pass. |
| A3-04.03 | Details private real samples with varied source/audio/subtitle density | Play/source/audio/subtitle/actions form a bottom decision island close to A3. | Concern | The dock is bottom-anchored by layout instead of runtime top calculations, so DPI-aware screenshots show it in the first viewport. Historical fixture passes helped reduce source/audio/subtitle from detailed parameters to compact decision summaries and remove heavy button frames. Current acceptance must use private real samples for long source labels, multi-audio, subtitle-rich, no-art, primary-only, and bright-artwork cases. Remaining gap: action-row scale, source text length, and real controller traversal still need stress before Pass. |
| A3-04.04 | Details private real sample with no usable artwork | No image falls back to black/matte without fake poster, gradient, or generated placeholder. | Pass | Historical no-art fixture captures showed the intended black/matte fallback. Current validation should repeat this against a real item with missing Primary/Backdrop/Thumb artwork. |
| A3-04.05 | Details private real samples with primary-only or bright artwork | Primary-only atmosphere is dim/cropped enough to avoid becoming a separate poster viewer. | Concern | Historical primary-only and bright-artwork routes indicated the crop/wash direction can work, but broad acceptance now requires private real samples without committing screenshots/assets. |

Acceptance:

- Details resembles `A3-ideal-details-atmosphere.png` in first-read composition.

## Batch A3-05 - Playback Native Material

Goal: move Playback OSD toward video-first native material.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-05.01 | Playback private real sample or real playback | The video/artwork field dominates the viewport and is not captured as plain black unless evaluating fallback. | Not Run | Use a private real sample with visible video or artwork-backed playback evidence. |
| A3-05.02 | OSD visible | Bottom strip reads as floating material over video/artwork, with internal breathing room. | Not Run | No cramped edge contact. |
| A3-05.03 | OSD visible | Top-left title/status capsule is compact and secondary. | Not Run | It must not become a page card. |
| A3-05.04 | OSD visible | Subtitles remain clear and above the strip. | Not Run | Material must not overpower captions. |
| A3-05.05 | OSD more/options | Source/audio/subtitle/more actions remain subordinate and visually consistent with the strip. | Not Run | Do not flatten every option into the default OSD. |

Acceptance:

- Playback resembles `A3-ideal-playback-osd-native-material.png` when a visual source is available.

## Batch A3-06 - Secondary Page Inheritance

Goal: make secondary pages inherit the primary visual language after A3-01 through A3-05.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-06.01 | Search | Search utility controls feel graphite and subordinate; result cards inherit poster-wall rules. | Not Run | Search should not reset the app into a form page. |
| A3-06.02 | Settings/Login | Utility forms are quiet and practical, not a separate enterprise UI. | Not Run | Function over mood, but still graphite. |
| A3-06.03 | Live TV/Music/Photos | Media-specific surfaces inherit dark-room canvas and source-aware chrome. | Not Run | They may use denser lists or immersive photo treatment. |

Acceptance:

- Secondary pages no longer visually contradict the four primary pages.

## Evidence Template

### 2026-07-08 - A3 Details Reading Band Pass

App version: `0.1.0.260` local validation package; repository manifest restored after install.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-captures-dpiaware-20260708-143659`
- Data source: fixture plus saved-session artwork; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `details-fixture.png`, `details-real-sample-local-only.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.02 | Concern | `details-real-sample-local-only.png` | Left text reads as a concise movie information band while real artwork owns the right side. | The previous pass still let long overview text spread too far across bright artwork. | Lower and narrow the reading band: content margin `56,82,56,48`, column `680`, overview `640` and three-line ellipsis. |
| A3-04.03 | Concern | `details-fixture.png`, `details-real-sample-local-only.png` | Low decision area feels native/material and subordinate to the atmosphere. | Current dock is usable and data-complete, but the single large rectangle remains heavier than A3. | Keep for now; later reduce the single-container feeling without removing Emby source/audio/subtitle decisions. |

Decision:

- Continue visual fix. Details is closer, but A3-04 should remain `Concern` until more real artwork cases and a lighter decision material pass are reviewed.

### 2026-07-08 - A3 Details Floating Decision Tiles Pass

App version: `0.1.0.261` local validation package; repository manifest restored after install.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-captures-dpiaware-20260708-145625`
- Data source: fixture plus saved-session artwork; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `details-fixture.png`, `details-real-sample-local-only.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.03 | Concern | `details-fixture.png`, `details-real-sample-local-only.png` | Low decision area reads as independent floating material decisions over artwork, not one panel. | The previous dock was structurally correct but a single large outer rectangle made Details feel like a UWP form surface. | Make the dock outer shell transparent, remove its border/padding, and keep material on the individual action/source/audio/subtitle tiles. |

Decision:

- Continue visual fix. The large dock shell is resolved; remaining A3-04 work is to reduce lower decision-tile density and verify more real artwork shapes.

### 2026-07-08 - A3 Details Compact Decision Summaries Pass

App version: `0.1.0.262` local validation package; repository manifest restored after install.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-captures-dpiaware-20260708-153016`
- Data source: fixture plus saved-session artwork; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `details-fixture.png`, `details-real-sample-local-only.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.03 | Concern | `details-fixture.png`, `details-real-sample-local-only.png` | Source/audio/subtitle decisions remain present but scan as compact TV choices rather than a media-info table. | The lower row used long source names, resolution strings, and secondary codec/count lines that read as implementation metadata. | Use compact source decision summaries, constrain source/audio/subtitle tile widths, and show only the first selected audio/subtitle summary in the first viewport. |

Decision:

- Continue visual fix. The lower decision row is now closer to A3 and less data-like; A3-04 remains `Concern` until more artwork/source variants and final material tuning are reviewed.

### 2026-07-08 - A3 Details Primary-Only Atmosphere Coverage Pass

App version: `0.1.0.266` local validation package; repository manifest restored after install.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-primary-only-fixture`; compare against prior `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-primary-only-20260708-1633`
- Data source: deterministic fixture artwork plus local-only saved-session evidence from `%TEMP%\ngxe-a3-captures-dpiaware-20260708-153016`; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `details-primary-only-fixture-dpi-aware.png`
- Local-only comparison: `details-real-sample-local-only.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.05 | Concern | `details-primary-only-fixture-dpi-aware.png` | A vertical `Primary` image can supply a muted background atmosphere without reading as a second poster. | The first primary-only pass was too dark and used a heavy wash, making the right side nearly black instead of poster-derived atmosphere. | Use a more representative poster fixture, raise primary-only atmosphere opacity moderately, and keep the added wash light enough that it protects readability without erasing media color. |
| A3-04.03 | Concern | `details-primary-only-fixture-dpi-aware.png` | The low decision area floats over a clean first viewport without secondary rails showing through it. | The transparent decision area previously allowed lower page sections to appear behind or just under the dock. | Move secondary content below the first viewport for Details so the first read stays focused on title, atmosphere, and decision tiles. |

Decision:

- Continue visual fix. The primary-only fixture now proves the fallback rule structurally, and the real saved-session sample proves that actual Emby artwork can carry the page mood. A3-04 remains `Concern` because the current Details screenshot still reads more restrained/tool-like than the A3 target, especially in title scale and decision-tile presence.

### 2026-07-08 - A3 Details Cinematic Band Pass

App version: `0.1.0.269` local validation package; repository manifest restored after install.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-primary-only-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-details-band-20260708-174122`
- Data source: deterministic fixture artwork plus local-only saved-session artwork; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `details-fixture-band.png`, `details-primary-only-band.png`, `details-real-sample-local-only-band.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.02 | Concern | `details-fixture-scale.png`, `details-real-sample-local-only-scale.png` | The title and overview start as a cinematic information band, not a desktop page header pinned near the top edge. | The previous pass improved title scale, but the reading band still sat too high, leaving Details with a top-header plus bottom-toolbar composition. | Move Details content top margin from `82` to `112` while preserving the `680`-wide information column and three-line overview limit. |
| A3-04.03 | Concern | `details-fixture-scale.png`, `details-real-sample-local-only-scale.png` | The decision island sits low but breathes above the safe-area edge, close to the A3 target's native TV action row. | The dock was structurally correct but sat too close to the bottom edge, making it feel like a tool strip. | Add `TvDetailsDecisionDockMargin` `56,0,56,112` and bind the Details dock to it instead of generic `TvPageMargin`. |

Decision:

- Continue visual fix. The latest screenshots show a better Details composition: title and decisions no longer cling to the viewport edges, `Primary`-only artwork remains atmosphere instead of a second poster, and real saved-session artwork still carries the right side. A3-04 remains `Concern` because the first viewport is still sparse compared with the A3 target and needs more structured media facts/material tuning before Pass.

### 2026-07-08 - A3 Details First-Viewport Facts Pass

App version: `0.1.0.271` local validation package; repository manifest restored after install.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-primary-only-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-details-facts-20260708-180929`
- Data source: deterministic fixture artwork plus local-only saved-session artwork; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `details-fixture-facts.png`, `details-primary-only-facts.png`, `details-real-sample-local-only-facts.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.02 | Concern | `details-fixture-band.png`, `details-primary-only-band.png`, `details-real-sample-local-only-band.png` | The left first viewport contains enough movie facts to feel like a details page, while remaining quieter than the artwork. | The previous band pass fixed edge spacing but left the first viewport sparse: title, meta, overview, then a large empty zone before decisions. | Add a passive non-focusable fact chip row between metadata and overview, then a compact director/genre line below overview. Limit facts to short labels and at most five chips so they do not become a technical table. |

Decision:

- Continue visual fix. The first viewport now has more A3-like information density without turning source/audio/subtitle decisions into a duplicate control surface. A3-04 remains `Concern` because the fact row and credits need more real-library variety, especially localization, missing metadata, and unusual media-source labels.

### 2026-07-08 - A3 Details Immersive Content Depth Pass

App version: `0.1.0.272` local validation package; repository manifest restored after install.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-primary-only-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-details-depth-20260708-182829`
- Data source: deterministic fixture artwork plus local-only saved-session artwork; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `details-fixture-depth.png`, `details-primary-only-depth.png`, `details-real-sample-local-only-depth.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.02 | Concern | `details-fixture-facts.png`, `details-primary-only-facts.png`, `details-real-sample-local-only-facts.png` | The title, metadata, facts, overview, and credits sit below the desktop header zone as a cinematic information band while artwork owns the right side. | The facts pass improved information density, but the reading band still started high enough to read as a page header. | Move `TvDetailsContentMargin` from `56,112,56,48` to `56,156,56,48`, preserving the `680`-wide column, three-line overview, and low decision dock. |

Decision:

- Continue visual fix. The content band now sits deeper in the atmosphere and real saved-session artwork still carries the right side without interfering with text. A3-04 remains `Concern` because decision tiles still read more like UWP controls than native TV material, and the details surface needs more bright-artwork, missing-overview, localization, and source-label stress before Pass.

### 2026-07-08 - A3 Details Decision Material Pass

App version: `0.1.0.273` local validation package; repository manifest restored after install.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-primary-only-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-details-material-20260708-184529`
- Data source: deterministic fixture artwork plus local-only saved-session artwork; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `details-fixture-material.png`, `details-primary-only-material.png`, `details-real-sample-local-only-material.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.03 | Concern | `details-fixture-depth.png`, `details-primary-only-depth.png`, `details-real-sample-local-only-depth.png` | The low decision island reads as native matte material over artwork, with Play remaining primary and secondary/source decisions subordinate. | The previous pass had correct placement but secondary actions and source/audio/subtitle chips still read as UWP button boxes because hairline frames and solid chrome surfaces competed with the atmosphere. | Add `AppDetailsDecisionTileBrush`/`AppDetailsDecisionTileSelectedBrush`, use them for non-primary actions and decision chips, and remove default hairline borders from Details action tiles. |

Decision:

- Continue visual fix. The decision island now has less button-frame weight while preserving readable Play/source/audio/subtitle decisions over fixture, primary-only, and local real artwork. A3-04 remains `Concern` until focused/selected states, long labels, bright artwork, no-art fallback, and controller-driven routes are stress-reviewed.

### 2026-07-08 - A3 Details No-Art Matte Fallback Pass

App version: `0.1.0.273` local validation package already installed from the Details decision material pass; repository manifest remains restored.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-no-art-fixture`
- Evidence root: `%TEMP%\ngxe-a3-details-noart-20260708-185419`
- Data source: deterministic no-art fixture; no private artwork or saved-session media involved.

Screenshots reviewed:

- Current: `details-no-art-fixture.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.04 | Pass | `details-no-art-fixture.png` | When no artwork exists, Details falls back to black/matte atmosphere without fake poster, generated gradient, title watermark, or special no-art panel. | The screenshot shows a quiet black/matte right side, the normal left information band, and the low decision material. No placeholder artwork is introduced. | No code change. Keep this as the expected fallback; future work should not add synthetic no-art identity art to Details. |

Decision:

- Pass this local no-art fallback gate. Details as a whole remains `Concern` because bright real artwork, focused decision states, and controller-driven routes still need stress-review.

### 2026-07-08 - A3 Details Long Source Fixture Split

App version: `0.1.0.274` local validation package; repository manifest should remain restored after validation.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-long-source-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-details-long-source-20260708-1913`
- Data source: deterministic fixture split plus saved-session local-only real artwork; no private artwork, screenshots, tokens, server URLs, or credentials should be committed.

Screenshots reviewed:

- Current: `details-fixture-baseline.png`, `details-long-source-fixture.png`, `details-real-sample-local-only.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.03 | Concern | `details-long-source-fixture.png` | Long Emby source/audio/subtitle labels remain readable fallbacks without turning the first viewport into a technical table. | Long audio and subtitle summaries stay inside low-alpha decision tiles with one-line ellipsis. The low decision area does not resize or pull attention away from the artwork atmosphere. | Keep long labels in the dedicated `details-long-source-fixture` stress route. Keep `details-fixture` as the clean baseline for A3 composition screenshots. |

Decision:

- Treat long source/audio/subtitle label coverage as structurally handled by the dedicated route. A3-04 remains `Concern` because focused/selected decision states, bright real artwork, and controller-driven visual review still need stress before Pass.

### 2026-07-08 - A3 Details Focused Decision Material Pass

App version: `0.1.0.275` local validation package; repository manifest should remain restored after validation.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-details-focused-material-20260708-1930`
- Data source: deterministic fixture plus saved-session local-only real artwork; no private artwork, screenshots, tokens, server URLs, or credentials should be committed.

Screenshots reviewed:

- Current: `details-fixture-focused-material.png`, `details-real-sample-local-only-focused-material.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.03 | Concern | `details-fixture-focused-material.png`, `details-real-sample-local-only-focused-material.png` | Focused Details decisions should be readable from ten feet without becoming a bright UWP control layer over the artwork. | The focused Play/Resume action previously reused the global card-focus fill, which was visually heavier than the surrounding low-alpha decision tiles. | Add `details_decision_tile_focused` / `AppDetailsDecisionTileFocusedBrush` and use it for Details action/source focus so focus is a local graphite luminance lift, not a generic card overlay. |

Decision:

- Continue visual fix. Focused Details action/source material is now closer to the matte cinema rule and remains borderless over fixture and local real artwork. A3-04 remains `Concern` because bright real artwork and controller-driven traversal across source/audio/subtitle decisions still need stress before Pass.

### 2026-07-08 - A3 Details Bright Real Artwork Sampling Pass

App version: `0.1.0.276` local validation package; repository manifest should remain restored after validation.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-real-bright-sample`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-details-bright-real-20260708-2005`
- Data source: saved-session local-only real artwork. The bright route downloads small artwork samples into memory, estimates luma, and does not persist source images, tokens, server URLs, credentials, or item-specific private data.

Screenshots reviewed:

- Current: `details-real-bright-sample-local-only-completed.png`, `details-real-sample-local-only.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.01 | Concern | `details-real-bright-sample-local-only-completed.png` | A high-luma real artwork source should still read as atmosphere, not a white panel beside a form. | The left scrim protects title, facts, overview, and credits; the pale right side remains dimmed enough to feel like poster-derived atmosphere instead of a separate poster viewer. | Keep `details-real-bright-sample` as a local-only stress route for bright-artwork validation. |
| A3-04.03 | Concern | `details-real-bright-sample-local-only-completed.png` | The low decision island remains readable over bright real artwork without adding a bright outline or hard outer dock. | Play/source/audio/subtitle tiles remain legible and visually subordinate. The route takes longer than normal because it samples up to 60 real movie images before navigation. | For future capture scripts, wait on `dev-command-result.txt` completion instead of using a fixed sleep. |

Decision:

- Treat bright real artwork stress as covered structurally by the new route. A3-04 remains `Concern`, not `Pass`, because controller traversal and broader real-library metadata/source variation still need visual stress before final acceptance.

### 2026-07-08 - A3 Home And Library Real Artwork Baseline

App version: `0.1.0.275` local installed package; Home/Library implementation is unchanged by the later Details-only `0.1.0.276` validation route.

Scope:

- Batch: A3-02 Home Media Wall, A3-03 Library Poster Wall
- Routes: `home`, `movies`, `movies-fixture`
- Evidence root: `%TEMP%\ngxe-main-flow-pages-20260708-195222`
- Data source: saved-session local-only real artwork plus deterministic `movies-fixture`; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `home-real-current-local-only.png`, `movies-real-current-local-only.png`, `movies-fixture-current.png`
- Target: `docs/design-previews/A3-ideal-home-dashboard.png`, `docs/design-previews/A3-ideal-library-poster-focus.png`

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-02.01 | Concern | `home-real-current-local-only.png` | Home first read is a dense media wall with rails and real artwork carrying color. | Real artwork density is much better than early fixture evidence, but the top page title/status and sparse hero still make Home read partly like a desktop dashboard. | Reduce the page-header/refresh weight and let rails occupy the first read earlier. |
| A3-02.02 | Concern | `home-real-current-local-only.png` | Continue Watching wide cards feel like media, with black scrims and subtle focus material. | The anatomy is close enough to keep, but borders/focus chrome remain more visible than A3. | Reuse the Details/Library matte-focus direction: luminance/scale/text protection before outlines. |
| A3-03.01 | Concern | `movies-real-current-local-only.png` | Library utility controls are subordinate to the poster wall. | Sort/filter controls and refresh still read as form chrome. | Collapse toolbar visual weight before changing poster density. |
| A3-03.02 | Concern | `movies-real-current-local-only.png` | The poster wall shows many real posters while staying readable from TV distance. | Density is directionally good with real posters; the surrounding title/count/toolbar still makes the page feel desktop. | Preserve density, reduce top chrome and focused-card boxiness. |

Decision:

- Use these Home/Library screenshots as the next visual baseline. Do not treat them as Pass: they prove real artwork/data density is available, while also showing that chrome hierarchy and focus material still need A3 convergence.

### 2026-07-08 - A3 Home And Library Chrome Recession Pass

App version: `0.1.0.277` local installed package.

Scope:

- Batch: A3-01 Shell/Chrome, A3-02 Home Media Wall, A3-03 Library Poster Wall
- Routes: `home`, `movies`, `movies-fixture`
- Evidence root: `%TEMP%\ngxe-current-mainline-pages-20260708-clean`
- Data source: saved-session local-only real artwork plus deterministic `movies-fixture`; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `home-real-current-clean.png`, `movies-real-current-clean.png`, `movies-fixture-current-clean.png`
- Target: `docs/design-previews/A3-ideal-home-dashboard.png`, `docs/design-previews/A3-ideal-library-poster-focus.png`

Findings recorded after chrome pass:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-01.02 | Concern | `home-real-current-clean.png`, `movies-real-current-clean.png` | Collapsed left Guide is present but visually secondary to media. | The collapsed Guide is workable and does not expand into content, but selected icon tiles remain relatively bright in first read. | Keep the left rail model; tune selected-route material to a quieter luminance lift instead of a button-like tile. |
| A3-01.03 | Concern | `home-real-current-clean.png`, `movies-real-current-clean.png` | Page titles and toolbars recede enough that artwork is the first read. | Home title now recedes, but Home still has a sparse top resume block. Movies still leads with a title/count/sort/filter cluster before posters. | Continue reducing page chrome, especially Library title/count and utility controls, before changing poster density. |
| A3-02.01 | Concern | `home-real-current-clean.png` | Home first viewport reads as a dense media dashboard with rails and real artwork carrying color. | The page now shows real rails quickly, but the top resume feature leaves large empty zones and still separates itself from the media wall. | Tighten Home first-viewport composition: make the resume feature more artwork-led or let rails start higher. |
| A3-02.02 | Concern | `home-real-current-clean.png` | Continue Watching wide cards feel like media cards, not outlined controls. | Wide-card content, progress, and black scrim are directionally right. Card borders/focus frames are still more visible than the A3 matte target. | Use luminance/scale/text protection before outlines; keep black scrims as the default non-focused reading layer. |
| A3-03.01 | Concern | `movies-real-current-clean.png` | Library utility controls are subordinate to the poster wall. | The source tokens are more compact than before, but the rendered controls still feel like a form cluster. | Shrink and dim utility controls further, or move them into a lighter top command row that does not compete with posters. |
| A3-03.03 | Concern | `movies-real-current-clean.png` | Focused poster uses integrated matte backplate with subtle scale/luminance and readable title/meta. | The focused no-art card is readable but too block-like, making the fallback stronger than real posters. | Apply the ordinary poster selected-state recipe to no-art fallbacks, but cap fallback fill contrast so absence states stay quiet. |

Decision:

- Continue visual fix. This pass improves evidence quality and confirms real artwork density, but Home/Library remain `Concern`. The next Home/Library work should target top chrome weight, selected-route material, Home resume composition, and Library focus/fallback material rather than adding more content or color.

### 2026-07-08 - A3 Details Single-Line Decision Chips Pass

App version: `0.1.0.278` local installed package.

Scope:

- Batch: A3-04 Details Atmosphere
- Routes: `details-fixture`, `details-real-sample`
- Evidence root: `%TEMP%\ngxe-a3-details-single-line-decisions-20260708-2135`
- Data source: deterministic fixture artwork plus saved-session local-only real artwork; real screenshots remain local-only and must not be committed.

Screenshots reviewed:

- Current: `details-fixture-single-line-decisions.png`, `details-real-sample-local-only-single-line-decisions.png`
- Target: `docs/design-previews/A3-ideal-details-atmosphere.png`

Findings recorded after fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| A3-04.03 | Concern | `details-fixture-single-line-decisions.png`, `details-real-sample-local-only-single-line-decisions.png` | Source/audio/subtitle decisions stay present as Emby playback choices without becoming a two-line data table. | Audio and subtitle are now single-line low material chips over both fixture and real artwork. The source chip is also single-line, but can still become long and truncate because it carries version count plus technical summary. | Keep the one-line decision pattern. In the next pass, shorten source wording and consider reducing action-row scale if the first viewport still feels tool-like. |

Decision:

- Continue visual fix. This pass reduces decision-dock density and moves Details closer to the A3 low material target, but A3-04 remains `Concern` until action-row scale, source text length, and controller-driven visual traversal are reviewed.

```md
### YYYY-MM-DD - A3 Batch NN

App version: `x.y.z`

Scope:

- Batch:
- Routes:
- Evidence root:
- Data source: private real sample / saved session / real playback sample / other

Screenshots reviewed:

- Current:
- Target:

Findings recorded before fixes:

| ID | Severity | Screenshot | Expected A3 quality | Actual | Proposed visual fix |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

Decision:

- Pass / continue visual fix / blocked
```
