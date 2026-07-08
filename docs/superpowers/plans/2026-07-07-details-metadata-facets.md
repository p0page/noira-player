# Details Metadata Facets

## Goal

Make Emby item metadata useful in the 10-foot flow: genres, studios, and tags should render as controller-focusable browse chips on Details, and each chip should open a Library result page filtered to that facet.

## Sources

- Emby `BaseItemDto` exposes `GenreItems`, `Studios`, `TagItems`, and `Tags`.
- Emby `/Items` supports `GenreIds`, `Genres`, `StudioIds`, `Studios`, and `Tags` query parameters.
- This slice does not touch native decoding, playback stream selection, or any transcoding flow.

## Plan

1. Add RED Core tests for mapping `GenreItems`, `Studios`, `TagItems`, and string-only `Tags` fallback from item details.
2. Add RED Core tests for `EmbyItemsQuery` building name/id facet filters: `Genres`, `StudioIds`, `Studios`, and `Tags`.
3. Add RED source tests for Details rendering a metadata browse section and navigating facet chips into `LibraryPage`.
4. Implement a small shared metadata reference model, extend `EmbyMediaItem`, and map the new DTO fields.
5. Extend `EmbyItemsQuery` and `LibraryNavigationQuery`, including MainPage request equality and LibraryPage query creation.
6. Render a Details `Explore` rail with Genre / Studio / Tag chips. Use ids when present, fall back to names where Emby only supplies names.
7. Update the deterministic Details fixture so keyboard-only local validation can enter a facet result page without a live server.
8. Verify with targeted tests, full Core tests, build/install, and Computer Use keyboard-only through `details-fixture`.

## Acceptance

- Details fixture shows focusable Genre / Studio / Tag chips.
- Pressing keyboard `Return` on a chip opens Library results using the matching facet query.
- Pressing `Escape` returns to Details with normal shell back behavior.
- Core tests prove query serialization and metadata mapping.
- QA docs record the tested keyboard path and remaining live-server risk.

## Result

- Implemented in local package `0.1.0.190`.
- Core tests passed: 429 total.
- x64 Debug App build passed with 0 warnings and 0 errors.
- Signed and installed `NextGenEmby.App 0.1.0.190` locally.
- Keyboard-only Computer Use route passed: `details-fixture` -> `Down` x5 to `Genre / Sci-Fi` -> `Return` to `Genre: Sci-Fi` Library -> `Escape` returned to Details with focus restored to the originating `Sci-Fi` chip.
- Bug found and fixed during validation: UWP recreated `MediaDetailsPage` on back navigation, so instance-only restore state was lost and focus jumped to `Resume`. The fix stores pending metadata focus by item id and suppresses default focus while restore is pending.

## Remaining Risk

- Real Emby servers may vary between `GenreItems`, `Studios`, `TagItems`, and name-only `Tags`; live validation is still needed after a saved session is available.
- Live `/Items` facet filtering should be re-run against the user's real server before treating every metadata entrypoint as fully server-verified.
