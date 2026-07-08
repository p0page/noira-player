# A3 Visual Convergence Checklist

Date: 2026-07-08

This checklist is the visual acceptance gate for the A3 Visual Convergence phase. It is intentionally stricter than the existing design-conformance checklist: functional navigation can pass while this checklist fails.

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
- Use deterministic fixture routes when comparing composition, density, focus treatment, and regression safety.
- Treat fixture/mock artwork as limited evidence. It cannot by itself prove artwork atmosphere, poster-wall realism, or playback mood.
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
| A3-00.01 | Home fixture or saved-session Home | `A3-ideal-home-dashboard.png` | Identify desktop chrome, hero/dashboard weight, rail density, artwork dominance, guide treatment. | Concern | Local fixture capture after shell pass no longer has white title chrome, but still reads as title/decision area plus rows. The A3 target is denser and more media-led in the first read. |
| A3-00.02 | Movies fixture or saved-session Movies | `A3-ideal-library-poster-focus.png` | Identify toolbar weight, poster density, focused poster treatment, text/meta spacing, fallback-card tone. | Concern | Movies fixture reaches a poster wall layout, but toolbar controls remain large, fixture poster art is too placeholder-like, and focused-poster emphasis needs direct visual verification. |
| A3-00.03 | Details fixture and one real Details item | `A3-ideal-details-atmosphere.png` | Identify split-panel drift, action-row placement, atmosphere quality, poster-only fallback behavior. | Concern | Details now uses a page-spanning atmosphere layer and a low decision dock. Fixture capture validates structure; local-only real Emby samples validate that real artwork can carry the right side of the canvas. Latest pass fixed a false non-DPI-aware screenshot crop, made the decision dock viewport-visible through layout anchoring, reduced source/version chrome to the current source summary, narrowed/lowered the reading band, and removed the dock's single large outer panel so actions read as floating tiles. Remaining gap: source/audio/subtitle tiles remain denser and more data-like than A3. |
| A3-00.04 | Playback fixture or visual playback route | `A3-ideal-playback-osd-native-material.png` | Identify whether the video/artwork field exists, OSD material quality, subtitle clearance, top status weight. | Blocked | Playback fixture is useful as a route smoke test, but currently captures a mostly black video field and cannot validate native material, subtitle clearance, or artwork/video-led mood. |

Acceptance:

- A short finding table exists before implementation begins.
- Findings are visual, not interaction bugs.
- No private screenshots or media assets are committed.

## Batch A3-01 - Canvas And Shell

Goal: make every page start from the right dark-room base.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-01.01 | Any fixture route | Desktop/window chrome is hidden, customized, or excluded from visual acceptance. | Pass | Desktop verification now uses dark title-bar chrome, so screenshots are no longer dominated by a white Windows frame. |
| A3-01.02 | Home and Library | Left Guide is visually quiet and does not compete with media. | Not Run | Icons/labels should be low-presence until invoked. |
| A3-01.03 | Home and Library | Page title/toolbar weight is reduced enough that media is first read. | Not Run | A large title can remain only if it does not dominate the viewport. |
| A3-01.04 | All primary pages | Background, raised surfaces, buttons, and fields share the same cool graphite family. | Not Run | Avoid blue/purple sci-fi drift and yellow warmth. |

Acceptance:

- A screenshot of each primary page no longer reads as a default UWP desktop window.

## Batch A3-02 - Home Media Wall

Goal: move Home toward the A3 media-wall target.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-02.01 | Home | First viewport shows multiple media choices and rails, not a single management-style hero panel. | Not Run | Continue Watching may lead, but should not become a form-like hero. |
| A3-02.02 | Home | Continue Watching cards use one wide-card anatomy with black scrim by default and subtle focused material only in the text/progress zone. | Not Run | No nested text containers. |
| A3-02.03 | Home | Media libraries/server sections are subdued source cards, not large bordered dashboard tiles. | Not Run | Artwork may be darkened; borders should not dominate. |
| A3-02.04 | Home | Real artwork creates the page color; chrome stays graphite. | Not Run | Green must remain sparse. |

Acceptance:

- Home resembles `A3-ideal-home-dashboard.png` in first-read hierarchy, density, and artwork-led color.

## Batch A3-03 - Library Poster Wall

Goal: move Library/Movie grid toward the A3 poster-wall target.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-03.01 | Movies grid | Sort/filter controls are light utility controls, not the visual anchor. | Not Run | Toolbar should not look like a settings form. |
| A3-03.02 | Movies grid | Poster density and spacing are close to A3: many visible posters, enough breathing room, no desktop table feeling. | Not Run | Preserve TV readability. |
| A3-03.03 | Focused poster | Focus uses integrated matte backplate, subtle scale/luminance, and readable title/meta. | Not Run | No bright complete border. |
| A3-03.04 | No-art fallback | Fallback cards are quiet and do not dominate the grid. | Not Run | Initials are acceptable but should remain secondary. |

Acceptance:

- Library resembles `A3-ideal-library-poster-focus.png` in composition and focus mood.

## Batch A3-04 - Details Atmosphere

Goal: move Details toward full-screen artwork atmosphere.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-04.01 | Details fixture, `details-primary-only-fixture`, and `details-real-sample` | The page reads as one atmospheric canvas, not left form plus right poster. | Concern | Fixture artwork validates repeatable structure only; it is too abstract/dark to prove final atmosphere. Local-only real Emby artwork shows the right side can carry color and subject matter while the left scrim protects text. The primary-only fixture proves cropped `Primary` can become dim atmosphere instead of a second poster viewer, and the no-art fixture now proves the black/matte fallback without fake placeholder art. Needs more real samples across bright artwork and varied metadata/source cases before Pass. |
| A3-04.02 | Details fixture and `details-real-sample` | Title, metadata, badges, overview, and credits sit in a left information column with strong hierarchy. | Concern | The reading band now uses `TvDetailsContentMargin` `56,156,56,48`, `TvDetailsContentColumnWidth`/`MaxWidth` `680`, and an overview max width of `640` with three-line ellipsis, moving the content below the page-header zone so it reads more like a cinematic information band. The latest facts pass adds a passive first-viewport fact row and compact director/genre text, so Details no longer reads as only a title plus overview. Remaining gap: facts/credits are structurally useful but need broader real-artwork and localization stress before Pass. |
| A3-04.03 | Details fixture, `details-long-source-fixture`, `details-primary-only-fixture`, and `details-real-sample` | Play/source/audio/subtitle/actions form a bottom decision island close to A3. | Concern | The dock is bottom-anchored by layout instead of runtime top calculations, so DPI-aware screenshots show it in the first viewport. The 0.1.0.261 local pass made the dock outer shell transparent, the 0.1.0.262 local pass reduced source/audio/subtitle from detailed parameters to compact decision summaries, and the 0.1.0.269 local pass moved the dock to `TvDetailsDecisionDockMargin` `56,0,56,112` so it no longer reads as a bottom-edge toolbar. The 0.1.0.273 local pass moved non-primary actions and source/audio/subtitle chips to lower-alpha `AppDetailsDecisionTileBrush` material and removed default hairline button frames. The 0.1.0.274 local pass split long source/audio/subtitle labels into `details-long-source-fixture`, keeping the standard fixture as a clean visual baseline while proving long labels stay one-line and do not resize the low decision area. The 0.1.0.275 local pass added a Details-specific focused decision tile fill so focused Play/source decisions no longer reuse the brighter global card-focus material. Remaining gap: bright artwork and real controller traversal still need stress before Pass. |
| A3-04.04 | Details no-art fixture | No image falls back to black/matte without fake poster, gradient, or generated placeholder. | Pass | `details-no-art-fixture` captures as a quiet black/matte atmosphere with the normal left information and low decision areas. It does not synthesize poster art, gradient art, title watermarks, or a no-art panel. |
| A3-04.05 | `details-primary-only-fixture` and real Details items | Primary-only atmosphere is dim/cropped enough to avoid becoming a separate poster viewer. | Concern | `details-primary-only-fixture` provides deterministic primary-only coverage without private assets, and `details-real-sample` runs against local saved-session artwork without committing screenshots/assets. Latest fixture review shows the right side remains atmosphere rather than a clear duplicate poster. Latest real sample uses actual artwork and confirms the source/audio/subtitle dock can populate from live data, but this is still not enough to accept broad real-world coverage. |

Acceptance:

- Details resembles `A3-ideal-details-atmosphere.png` in first-read composition.

## Batch A3-05 - Playback Native Material

Goal: move Playback OSD toward video-first native material.

| ID | Route | Checks | Result | Notes |
| --- | --- | --- | --- | --- |
| A3-05.01 | Playback visual fixture or real playback | The video/artwork field dominates the viewport and is not captured as plain black unless evaluating fallback. | Not Run | Add deterministic visual fixture if needed. |
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

```md
### YYYY-MM-DD - A3 Batch NN

App version: `x.y.z`

Scope:

- Batch:
- Routes:
- Evidence root:
- Data source: fixture / saved session / visual playback fixture

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
