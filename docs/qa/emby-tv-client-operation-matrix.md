# Emby TV Client Operation Matrix

Date: 2026-07-07

This matrix tracks the user operations that a complete couch-first Emby client must support. It complements `emby-tv-client-keyboard-checklist.md`: the checklist is the executable route script, while this matrix records product coverage, verification evidence, and the next missing work.

Status values:

- `Verified`: keyboard-only local validation exists in the checklist run log.
- `Implemented`: code path exists, but the full keyboard route has not been freshly validated.
- `Partial`: a basic surface exists, but the expected Emby/TV behavior is incomplete.
- `Missing`: no deliberate user-facing flow yet.
- `External`: depends on server support, hardware, or non-goal capabilities.

## Startup And Session

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Launch installed app into saved session | App launch, wait | Verified | 0.1.0.133 launched to Home with the saved Emby session; 0.1.0.132 launched to Home with aligned Matte Cinema colors; 0.1.0.131 recovered real Media Libraries and rows after guarded interactive loads; 0.1.0.148 restored clean MSIX launch after the local provider-path activation failure | Keep startup diagnostics active; restore a saved session for full Emby routes |
| Log in from empty session | Tab/Enter through login fields | Implemented | Login page and credential store exist | Re-run with cleared session before completion |
| Failed login recovery | Enter invalid login, edit fields | Partial | UI has login status handling | Add explicit keyboard-only failure run |
| Open and close guide from Home | `M`, `Escape` | Verified | 0.1.0.77 guide route fixed | Add visual screenshot pass after major shell changes |

## Home

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Hero play/resume focus | Launch Home | Verified | 0.1.0.156 DEBUG `home-fixture` launched Home with `Play` focused and screenshot coverage; 0.1.0.104 Home focused Play on hero | Validate on non-resumable real libraries |
| Move from hero to rails | `Down` | Verified | 0.1.0.156 keyboard route moved from `Play` to `Hot Movies`; 0.1.0.104 moved to Media Libraries rail | Keep `StartBringIntoView` behavior |
| Move horizontally across section cards | `Right`, `Left` | Verified | 0.1.0.156 keyboard route moved across fixture libraries from `Hot Movies` to `Hot TV Series` and `Douban Top Rated`, with wide artwork visible on library/section cards; 0.1.0.104 focused `热门剧集`; 0.1.0.102 kept focused `日漫` visible | Validate at far-right end of very long real rails |
| Open media library card | `Return` | Verified | 0.1.0.131 `Down`, `Return` opened `热门电影` from Home with `34 items`; 0.1.0.104 opened `热门剧集`; 0.1.0.102 opened `日漫` | Keep as regression route |
| Return from library to originating Home card | `Escape` | Verified | 0.1.0.104 preserved `热门剧集` focus | Keep as regression route |
| Open section More action | `Down` to row, `Up`, `Return` | Verified | 0.1.0.132 `Down`, `Down`, `Down`, `Up` focused Hot Movies `More`; `Return` opened `热门电影` with `34 items`; `Escape` returned Home and `Down` from `More` focused the first Hot Movies poster | Add far-right and lower-row `More` stress route |
| Continue Watching item route | `Down` to row, `Return` | Verified | 0.1.0.133 `Down`, `Down`, `Return` from Hero `Play` focused the first Continue Watching card and opened `铸就传奇` Details with `Resume` focused, without starting playback; `Escape` returned Home to the originating card | Keep as Home pre-play regression route |
| Server-configured home sections | Rail navigation | Verified | 0.1.0.179 added a dedicated `Server sections` rail separate from `Media Libraries`, maps section-owned `Thumb`/`Backdrop`/`Banner`/`Primary` artwork before ParentItem fallback, and keyboard-validated `Down`, `Down`, `Right`, `Right`, `Return` from `home-fixture` into `Douban Top Rated` Library with 7 fixture items and packaged artwork; `Escape` returned Home with focus restored to the originating section card. 0.1.0.156 fixed DEBUG fixture library/section artwork; 0.1.0.132 validated Home row `More` for Hot Movies; 0.1.0.102 parsed many server categories with section artwork | Re-run screenshots against real server artwork after session restore |

## Guide Rail

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open guide without leaving current page | `M` | Verified | 0.1.0.77 fixed guide overlay behavior | Validate over Search and Playback boundaries |
| Navigate Home/Search/Movies/Shows | `M`, arrows, `Return` | Verified | 0.1.0.77 and later runs | Keep guide focus restoration tests |
| Navigate Collections/Playlists/Favorites/Unwatched | `M`, arrows, `Return` | Verified | 0.1.0.185 adds positive DEBUG Collections and Playlists browse validation; 0.1.0.78 run opened Playlists, Favorites, Unwatched | Re-run real-server positive Collections/Playlists after session restore |
| Navigate Live TV/Music/Photos | `M`, arrows, `Return` | Verified | Main guide destinations exist; 0.1.0.144 keyboard/UIA route opened Guide from Home, navigated to Live TV, reopened Guide to Music, then reopened Guide to Photos; 0.1.0.182 adds a positive DEBUG Photos browse route for album/photo navigation without saved server data; 0.1.0.120 debug route opened Photos with TV empty-state recovery | Keep as Guide regression route; re-run real-server Photos after session restore |

## Library Browsing

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open movie/series library grid | Guide/Home, `Return` | Verified | 0.1.0.129 Guide opened Movies with `100 items`; 0.1.0.104 `热门剧集`; 0.1.0.102 `日漫` | Validate more collection types |
| Move across and down grid | Arrows | Verified | 0.1.0.144 keyboard/UIA route opened Movies with `100 items`; initial focus was a `GridViewItem` at x=192/y=365, `Down` moved to x=192/y=761, `Right` to x=465/y=761 then x=738/y=761, `Down` to x=738/y=1157, and `Left` to x=465/y=1157 | Keep as grid focus regression route; add far-right/end-of-list stress route later |
| Sort through TV sheet | `Up`, `Return`, arrows, `Return` | Verified | 0.1.0.98 sort sheet validation | Extend sort options beyond Title/Recently added/Year |
| Filter through TV sheet | `Up`, `Right`, `Return`, arrows, `Return` | Verified | 0.1.0.98 filter sheet validation | Add favorite/resumable server-backed validation |
| Empty library recovery | Empty strict query | Verified | 0.1.0.78 Playlists showed `No items found` instead of wrong folders | Add screenshot pass |
| Genre/person/studio/tag browsing | Details `Explore` rail, `Return`, `Escape` | Verified | 0.1.0.190 DEBUG `details-fixture` rendered Genre/Studio/Tag chips from item metadata; keyboard route moved to `Genre / Sci-Fi`, opened `Genre: Sci-Fi` with 15 filtered fixture items, and `Escape` returned to Details with focus restored to the originating `Sci-Fi` chip. 0.1.0.109 person card opened a `PersonIds` Library from Details Cast & crew | Re-run against a real saved Emby session to verify server-specific `GenreItems`/`Studios`/`TagItems` shapes and live `/Items` facet filtering |

## Collections And Playlists

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Browse Collections root | DEBUG route `collections-fixture`, arrows | Verified | 0.1.0.185 opened `Collections` with two `BoxSet` cards, packaged wide artwork, `Sort`/`Filter`, and focus on `Signal Archives` | Re-run against real server `BoxSet` rows after session restore |
| Open Collection contents and return | `Return`, `Right`, `Escape` | Verified | 0.1.0.185 opened `Signal Archives` as a nested Library with 4 movies; `Right` moved focus from `Aurora Protocol` to `Midnight Signal`; `Escape` returned to root `Collections` with focus restored to `Signal Archives` | Add real-server collection child validation, including mixed media where supported |
| Browse Playlists root | DEBUG route `playlists-fixture`, arrows | Verified | 0.1.0.186 re-ran the installed Playlists root after adding the real playlist-items endpoint: `Playlists`, `2 items`, `Weekend Queue`, and `Documentary Stack` rendered with focus on `Weekend Queue`; 0.1.0.185 added the original positive fixture route | Re-run against real server `Playlist` rows after session restore |
| Open Playlist contents and return | `Return`, `Right`, `Right`, `Escape` | Verified | 0.1.0.186 keeps fixture behavior intact and routes real playlist child loads through Emby's `GET /Playlists/{Id}/Items` endpoint. Keyboard validation opened `Weekend Queue` with 5 mixed child items, hid ineffective Sort/Filter controls on the playlist child sequence, moved focus twice to `Ocean Archive`, and `Escape` restored focus to `Weekend Queue`. 0.1.0.185 first verified nested playlist browse and card clipping | Re-run real-server playlist child validation after session restore |

## Details

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Movie details default focus | Open item | Verified | 0.1.0.166 DEBUG `details-fixture` cold-launched Media Details with `Resume` focused after adding `ShellContentMode.MediaDetails`; 0.1.0.133 Continue Watching Details opened with `Resume` focused, title/metadata/actions in the first viewport, and the poster top-aligned to the decision surface; 0.1.0.69 first poster Details focused Play | Re-run against real saved-session Details after session restore |
| Series details episode loading | Open Shows item | Verified | 0.1.0.100 loaded Shows seasons/episodes | Add season selector when multiple seasons exist |
| Launch episode playback | Details, `Return` | Verified | 0.1.0.100 launched `铸就传奇` episode playback | Keep direct playback request stable |
| Version selection before playback | Details versions, arrows, `Return` | Verified | 0.1.0.166 DEBUG `details-fixture` keyboard route moved from `Resume` to Versions, selected `1080p fallback` with `Return`, kept the app on Details, and moved the warm selected-version status bar without reusing the cyan focus border; 0.1.0.145 added `MediaDetailsVersionSelectionPolicy` and tests proving Play/Restart resolve the selected `MediaSourceId` | Restore a saved session or test credentials, then run Home -> Movies -> multi-version Details against a real server item |
| Favorite / watched toggles | Details action row | Verified | 0.1.0.175 DEBUG `details-fixture` keyboard route fixed cold-launch focus so Resume receives the true controller focus, then verified `Right`, `Right`, `Return` changes Add favorite -> Remove favorite, `Right`, `Return` changes Mark watched -> Mark unwatched, `Return` toggles watched back, and `Left`, `Return` toggles favorite back. Fixture updates local user data only; API mutation tests still cover live Emby writes | Re-run live favorite/watched toggles only on a disposable item or fixture user after session restore |
| Similar items, people, collections/playlists | Details below fold | Verified | 0.1.0.166 DEBUG `details-fixture` rendered More like this, Cast & crew, current collection/playlist ancestors, fixture collection targets, and fixture playlist targets with packaged artwork; keyboard validation opened Add to collection, selected `Signal Archives`, restored focus, opened Add to playlist, closed with `Escape`, and moved into recommendation/person rails. 0.1.0.109 loaded Cast & crew from live Details; 0.1.0.112 added Organize/API coverage | Validate live add-to success on a disposable collection/playlist target and a real server item that returns similar results |

## Search

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open Search from guide | `M`, arrows, `Return` | Verified | 0.1.0.77 guide opened Search | Keep as shell regression |
| Submit query | Type text, `Return` | Verified | 0.1.0.129 searched `Terrifier` and returned 3 results without staying in `Searching All`; 0.1.0.105 also returned 3 results | Keep as Search regression route |
| Move scopes | `Down`, `Right` to far edge, `Return`, `Down`, `Up`, `Left` | Verified | 0.1.0.162 re-ran the DEBUG `search-fixture` route with packaged QA artwork on every visible result card: `Down` to All, nine `Right` presses to far-right `Live TV` while keeping it visible, `Return` filtered to `1 result / Live TV`, `Down` focused the artwork-backed `News 24` card, `Up` returned to `Live TV`, and `Left` moved to `Photos`; 0.1.0.161 added the scope fixture and singular status fix; 0.1.0.129 opened a real search result; `SearchFocusNavigationPolicy` tests cover directional decisions | Keep as Search scope regression route |
| Open result Details | Results, `Return` | Verified | 0.1.0.129 opened `断魂小丑` Details from Search results | Keep as result navigation regression |
| Empty search recovery | Nonsense query, `Down`, `Down`, `Return` | Verified | 0.1.0.105 showed central empty panel, focused `Edit search`, and selected the query on return | Keep as Search regression route |
| Search error recovery | DEBUG `search-error`, `Down`, `Down`, `Right`, `Return` | Verified | 0.1.0.159 added a deterministic Search error route and Computer Use keyboard validation: launch to error state, move from query to `All`, down to `Edit search`, right to `Search again`, `Return` retries and returns focus to the query; 0.1.0.129 adds `InteractiveRequestGuard` tests and wraps Search requests so the UI can recover instead of waiting forever | Keep as deterministic server-failure regression; optionally re-run against a real offline server after session restore |

## Playback

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Start direct playback | Details or DEBUG manual fixture, `Return` | Verified | 0.1.0.137 keyboard/UIA run started local playback from the Home hero Play route and showed the OSD with `Playing`; 0.1.0.100 playback started and showed controls; 0.1.0.148 used DEBUG `manual-playback` without a saved session, pressed `Enter`, and native diagnostics reached `Opening` then `Playing` on the Big Buck Bunny MP4 | Do not alter native decoding; re-run real Emby Details playback after session restore |
| Pause/resume | `Return`/focused transport | Verified | 0.1.0.138 Computer Use route closed More, moved visual transport focus to Pause, pressed `Return` to change `Playing` to `Paused`, then pressed `Return` again to return to `Playing` | Keep as playback OSD regression route |
| Transport seek buttons | `Right`/`Left`, `Return` | Verified | 0.1.0.138 Computer Use route moved focus to `30s`, pressed `Return`, and OSD position advanced to `00:07:52`, proving the focused seek-forward action fired | Add separate seek-preview cancel route for thumbstick/preview mode |
| Seek preview and cancel | `Shift+Left/Right`, `Escape`, `Return` | Verified | 0.1.0.143 added core keyboard surrogate policy and prompt tests for `Shift+Left/Right`, `A/Enter Confirm`, and `B/Escape Cancel`; 0.1.0.144 added a DEBUG manual-playback dev-command fixture that accepts `streamUrl` and `autoStart`. Keyboard/UIA validation launched a long seekable MP4 through the installed app, showed `Seek preview 00:00:12`, canceled with `Escape` to `Playing - Seek canceled`, previewed again to `00:00:14`, committed with `Enter`, and reached `Playing - Position 00:00:14`; native diagnostics also logged `PlaybackGraph.SeekPreroll reached target` | Keep this as the deterministic local seek-preview regression route; re-run on real Emby media when the saved session is restored |
| More drawer | `M`, arrows, `Escape` | Verified | 0.1.0.173 DEBUG `playback-options-fixture` cold-launched Playback with More pinned, Source focused, `Down` moved Source -> Audio without changing Source, Info stayed focused after activation, `Escape` returned to More, and reopening More cleared the transport double-highlight; 0.1.0.148 opened More during manual direct playback and closed it with `Escape` while playback remained `Playing`; 0.1.0.143 fixed handled-Cancel routing | Keep as playback OSD regression route |
| Audio stream switch | More drawer | Verified | 0.1.0.173 fixture route selected `Japanese AAC Stereo` with `Enter`, `Down`, `Enter` while Source remained `4K Direct`; fixture changes update local UI state only and do not call backend/native stream switching | Re-run on a real multi-audio Emby item after session restore |
| Subtitle switch/off | More drawer | Verified | 0.1.0.173 fixture route selected `English SDH External`; collapsed Source/Audio/Subtitles combo boxes route Up/Down to drawer focus, while expanded dropdowns use Up/Down to change values | Re-run subtitle on/off and external subtitle load on real Emby media after session restore |
| Playback progress reporting | Playback lifecycle | Implemented | Emby progress APIs and tests exist | Verify server progress changes after playback |

## Live TV, Music, Photos

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Live TV channel browsing | Guide Live TV or debug route `livetv-fixture` | Verified | 0.1.0.184 DEBUG `livetv-fixture` opened a positive channel guide with four channels, packaged channel artwork, and current-program previews; `Down` moved `101 News 24` -> `202 Cinema One` and updated the Now preview, additional `Down` reached `404 Kids Studio`, and `Up` returned to `303 Match Center`. 0.1.0.144 Guide-keyboard run still covers the real-server empty state; Core API tests cover `/LiveTv/Info`, `/LiveTv/Channels`, and `/LiveTv/Programs` | Re-run a positive channel list on a real Live TV-enabled server after session restore; live stream playback remains unsupported/non-goal for now |
| Live TV playback unsupported recovery | Open channel or debug route `livetv-fixture` / `livetv-unsupported` | Verified | 0.1.0.184 opened `Live TV playback unavailable` from fixture channel `Kids Studio`; `Escape` closed only that layer and restored focus to the invoking channel, then `Up` continued channel-list navigation. 0.1.0.122 covers the standalone unsupported route and returns focus to `Refresh live TV` | Re-run from a real channel when server channels exist; do not add Emby transcoding flow yet |
| Music browsing | Guide Music or debug route `music-fixture` | Verified | 0.1.0.177 DEBUG `music-fixture` keyboard route opened Music with 3 albums, 6 songs, packaged QA artwork, and Albums/Songs/Preview columns; `Down` moved album focus, `Return` filtered `City Lights Archive`, `Down` moved song focus, `All` restored the full list, and `Left`/`Right` crossed between Albums and Songs. 0.1.0.144 Guide-keyboard run opened Music and showed the real-server empty state; 0.1.0.125 opened the dedicated Music page; Core tests cover MusicAlbum/Audio queries and type guards | Re-run positive album/song rows on a real server with a music library; add artist hierarchy when artist metadata is exposed |
| Music playback unsupported recovery | Open audio or debug route `music-unsupported` | Verified | 0.1.0.125 shows a focused `Music playback unavailable` layer; Escape closes only that layer and returns to the Music page without stale Loading labels | Re-run from a real audio item when the server has songs |
| Photos browsing | Guide Photos or debug route `photos-fixture` | Verified | 0.1.0.182 DEBUG `photos-fixture` keyboard route opened Photos with 3 root items, packaged artwork, `Night Market` as a folder, and root photos; `Return` opened the nested album with 4 photos, `Right`, `Right` moved visible focus to `Blue Crossing`, and `Escape` from the nested grid returned to the root Photos grid. 0.1.0.144 Guide-keyboard run opened real-server Photos and showed `No items found`; 0.1.0.120 validated the empty-state recovery | Re-run against a real server library that contains Photo items/folders |
| Photo viewer and B recovery | Open Photo item or debug route `photo` / `photos-fixture` | Verified | 0.1.0.182 opened `Blue Crossing` from the nested Photos fixture album, loaded the packaged image in the immersive viewer without `Sign in first` or `Photo unavailable`, and `Escape` returned to the nested album with focus restored to `Blue Crossing`; a second `Escape` returned to the root Photos grid. 0.1.0.120 `photo` route opened the fallback viewer and returned Home | Validate positive image load when a real Photo item is available |

## Settings And Diagnostics

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open Settings | `M`, eleven `Down`, `Return` | Verified | 0.1.0.126 Computer Use route opened Settings from the expanded Guide with keyboard input only | Keep as shell regression route |
| Inspect signed-in server | Settings page text snapshot | Verified | 0.1.0.126 Settings showed `cyber on https://c1.zdz.plus:443`, `App 0.1.0.126 / Emby client 0.1.0`, input map, and latest diagnostics | Keep startup diagnostics available and add copy/export only if needed |
| Toggle input/playback diagnostics | `Space` on default Settings focus | Verified | 0.1.0.126 `Space` toggled thumbstick seek preview off and updated the status text; a second `Space` restored it on. No app-content mouse clicks were used | Add additional TV-mode scale/theme toggles only after shared theme tokens exist |
| App version / startup diagnostics | Settings page text snapshot | Verified | 0.1.0.126 Settings exposed app/client version and `Last launch completed`; formatter ignores older crash blocks | Keep startup diagnostics available and add copy/export only if needed |

## Current Highest-Value Gaps

1. Collections and Playlists now have deterministic positive fixture validation in 0.1.0.185/0.1.0.186, including root cards, nested child browsing, horizontal focus movement, B/Escape focus restoration, and playlist child pages that use Emby's dedicated `GET /Playlists/{Id}/Items` endpoint instead of relying on `ParentId`. Remaining risk is real-server validation for collection children and playlist children after session restore.
2. Details fixture coverage is now broad: default focus, version selection, favorite/watched toggles, similar rail, cast rail, metadata facet browse chips, and add-to sheets are keyboard-verified in 0.1.0.190/0.1.0.175/0.1.0.166. Remaining Details risk is live server add-to success on disposable user-data, collection, and playlist targets, a real similar-items response, and real-server metadata facet field variations after session restore.
3. Playback option selection is keyboard-verified through the 0.1.0.173 `playback-options-fixture`: Source/Audio/Subtitles/Info focus, collapsed combo navigation, dropdown selection, Info focus preservation, drawer cancel, and reopened-drawer focus visuals all passed locally without touching native decoding. Remaining playback risk is live stream switching on real multi-audio/subtitle-rich Emby media after session restore.
4. Live TV, Music, and Photos now have deterministic positive fixture validation: Live TV channel browsing in 0.1.0.184, Music album/song browsing in 0.1.0.177, and Photos album/photo browsing in 0.1.0.182. Remaining risk is real-server validation for Live TV channel lists, music artist hierarchy, and photo libraries after session restore; live TV streaming and Emby transcoding remain out of scope for this phase.
5. Search fixture visuals now use packaged QA artwork instead of initials-only fallbacks as of 0.1.0.162; the next visual pass should make fixture artwork more type-specific and compare it against real server-backed artwork when a saved session is restored.
6. Theme work should keep promoting repeated spacing, typography, focus, and component states into shared resources. The 0.1.0.179 pass separated media-library cards from server-section cards while preserving shared wide-card opacity/scrim resources and section-owned artwork priority; the 0.1.0.166 pass separated selected-version state from focus state by reserving cyan for focus and using a warm internal status bar for selection; the 0.1.0.162 pass added Search fixture artwork mapping; the 0.1.0.161 pass added a deterministic Search results fixture, far-right scope validation, scope `StartBringIntoView`, and singular/plural status text; the 0.1.0.159 pass fixed Search query-to-scope and error-action left/right focus routing found during keyboard validation; the 0.1.0.156 pass fixed Home fixture wide artwork, added Home wide-card opacity/scrim resources, and made generated QA artwork legible under card washes; the 0.1.0.153 pass added a deterministic Home fixture screenshot route and Home automation-name coverage; the 2026-07-06 runtime color-token contract pass added exact `DESIGN.md` coverage for runtime alpha/state colors and focus secondary color; the 0.1.0.150 pass added Home dynamic-card artwork wash resources and a guard against page-local raw color brushes in View code-behind; the 0.1.0.133 pass confirmed the runtime palette matches `docs/DESIGN.md` and moved Details measurements into shared resources; the 0.1.0.132 pass aligned core runtime colors; the 0.1.0.129 pass centralized Library/Search poster-grid dimensions and common card/empty-state typography. Remaining one-off page measurements should keep moving into skin resources before full theme swapping.
