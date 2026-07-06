# Emby TV Client Operation Matrix

Date: 2026-07-06

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
| Launch installed app into saved session | App launch, wait | Verified | 0.1.0.132 launched to Home with the saved Emby session; 0.1.0.131 recovered real Media Libraries and rows after guarded interactive loads | Keep startup diagnostics active |
| Log in from empty session | Tab/Enter through login fields | Implemented | Login page and credential store exist | Re-run with cleared session before completion |
| Failed login recovery | Enter invalid login, edit fields | Partial | UI has login status handling | Add explicit keyboard-only failure run |
| Open and close guide from Home | `M`, `Escape` | Verified | 0.1.0.77 guide route fixed | Add visual screenshot pass after major shell changes |

## Home

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Hero play/resume focus | Launch Home | Verified | 0.1.0.104 Home focused Play on hero | Validate on non-resumable libraries |
| Move from hero to rails | `Down` | Verified | 0.1.0.104 moved to Media Libraries rail | Keep `StartBringIntoView` behavior |
| Move horizontally across section cards | `Right`, `Left` | Verified | 0.1.0.104 focused `热门剧集`; 0.1.0.102 kept focused `日漫` visible | Validate at far-right end of very long rails |
| Open media library card | `Return` | Verified | 0.1.0.131 `Down`, `Return` opened `热门电影` from Home with `34 items`; 0.1.0.104 opened `热门剧集`; 0.1.0.102 opened `日漫` | Keep as regression route |
| Return from library to originating Home card | `Escape` | Verified | 0.1.0.104 preserved `热门剧集` focus | Keep as regression route |
| Open section More action | `Down` to row, `Up`, `Return` | Verified | 0.1.0.132 `Down`, `Down`, `Down`, `Up` focused Hot Movies `More`; `Return` opened `热门电影` with `34 items`; `Escape` returned Home and `Down` from `More` focused the first Hot Movies poster | Add far-right and lower-row `More` stress route |
| Continue Watching item route | `Down` to row, `Return` | Implemented | Home resume cards and Details/Playback requests exist | Fresh keyboard run needed |
| Server-configured home sections | Rail navigation | Verified | 0.1.0.132 validated Home row `More` for Hot Movies; 0.1.0.102 parsed many server categories with section artwork | Add far-end lower-row validation |

## Guide Rail

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open guide without leaving current page | `M` | Verified | 0.1.0.77 fixed guide overlay behavior | Validate over Search and Playback boundaries |
| Navigate Home/Search/Movies/Shows | `M`, arrows, `Return` | Verified | 0.1.0.77 and later runs | Keep guide focus restoration tests |
| Navigate Collections/Playlists/Favorites/Unwatched | `M`, arrows, `Return` | Verified | 0.1.0.78 run opened Playlists, Favorites, Unwatched | Add fresh visual screenshots |
| Navigate Live TV/Music/Photos | `M`, arrows, `Return` | Implemented | Main guide destinations exist; 0.1.0.120 debug route opened Photos with TV empty-state recovery | Need real Guide-keyboard run for Live TV/Music and a positive Photos library |

## Library Browsing

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open movie/series library grid | Guide/Home, `Return` | Verified | 0.1.0.129 Guide opened Movies with `100 items`; 0.1.0.104 `热门剧集`; 0.1.0.102 `日漫` | Validate more collection types |
| Move across and down grid | Arrows | Implemented | GridView focus and wrap layout exist | Fresh grid stress route needed |
| Sort through TV sheet | `Up`, `Return`, arrows, `Return` | Verified | 0.1.0.98 sort sheet validation | Extend sort options beyond Title/Recently added/Year |
| Filter through TV sheet | `Up`, `Right`, `Return`, arrows, `Return` | Verified | 0.1.0.98 filter sheet validation | Add favorite/resumable server-backed validation |
| Empty library recovery | Empty strict query | Verified | 0.1.0.78 Playlists showed `No items found` instead of wrong folders | Add screenshot pass |
| Genre/person/studio/tag browsing | Guide, sheet, or Details person rail | Partial | 0.1.0.109 person card opened a `PersonIds` Library from Details Cast & crew | Add deliberate genre/studio/tag entrypoints and sheets |

## Details

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Movie details default focus | Open item | Verified | 0.1.0.69 first poster Details focused Play | Fresh Matte Cinema visual pass |
| Series details episode loading | Open Shows item | Verified | 0.1.0.100 loaded Shows seasons/episodes | Add season selector when multiple seasons exist |
| Launch episode playback | Details, `Return` | Verified | 0.1.0.100 launched `铸就传奇` episode playback | Keep direct playback request stable |
| Version selection before playback | Details versions, arrows | Implemented | Version buttons exist and pass `MediaSourceId` | Fresh keyboard run with multi-version item |
| Favorite / watched toggles | Details action row | Implemented | 0.1.0.107 shows Add favorite and Mark watched in the first viewport; API mutation tests cover Emby user-data writes | Do a live toggle only on a disposable item or fixture user |
| Similar items, people, collections/playlists | Details below fold | Partial | 0.1.0.109 loaded Cast & crew, moved focus into person cards, and opened a `PersonIds` Library; 0.1.0.112 added Organize, ancestors lookup, collection/playlist add sheets, and keyboard-validated sheet cancel/focus restore; API tests cover `Items/{Id}/Similar`, `Items/{Id}/Ancestors`, `POST /Collections/{Id}/Items`, and `POST /Playlists/{Id}/Items` | Validate visible similar-items rail on a server item that returns results; validate live add-to success on a disposable collection/playlist target |

## Search

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open Search from guide | `M`, arrows, `Return` | Verified | 0.1.0.77 guide opened Search | Keep as shell regression |
| Submit query | Type text, `Return` | Verified | 0.1.0.129 searched `Terrifier` and returned 3 results without staying in `Searching All`; 0.1.0.105 also returned 3 results | Keep as Search regression route |
| Move scopes | `Down`, `Left`/`Right` | Verified | 0.1.0.129 `Down`, `Down`, `Return` moved from query toward results and opened the first result; `SearchFocusNavigationPolicy` tests cover directional decisions | Add far-right scope rail run |
| Open result Details | Results, `Return` | Verified | 0.1.0.129 opened `断魂小丑` Details from Search results | Keep as result navigation regression |
| Empty search recovery | Nonsense query, `Down`, `Down`, `Return` | Verified | 0.1.0.105 showed central empty panel, focused `Edit search`, and selected the query on return | Keep as Search regression route |
| Search error recovery | Stalled network/API request | Implemented | 0.1.0.129 adds `InteractiveRequestGuard` tests and wraps Search requests so the UI can recover instead of waiting forever | Add offline/server-failure keyboard run |

## Playback

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Start direct playback | Details, `Return` | Verified | 0.1.0.100 playback started and showed controls | Do not alter native decoding |
| Pause/resume | `Return`/focused transport | Implemented | OSD buttons exist | Fresh keyboard run |
| Seek preview and cancel | `Right`, `Escape` | Implemented | Core playback overlay policy tests exist | Fresh route on local playback |
| More drawer | `M`, arrows, `Escape` | Implemented | Playback More drawer XAML exists | Validate no auto-hide while drawer is open |
| Audio stream switch | More drawer | Implemented | Backend has stream switching interfaces | Fresh multi-audio item route |
| Subtitle switch/off | More drawer | Implemented | Native disable subtitles path exists | Fresh subtitle item route |
| Playback progress reporting | Playback lifecycle | Implemented | Emby progress APIs and tests exist | Verify server progress changes after playback |

## Live TV, Music, Photos

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Live TV channel browsing | Guide Live TV or debug route `livetv` | Partial | 0.1.0.122 opens a dedicated Live TV page; Core API tests cover `/LiveTv/Info`, `/LiveTv/Channels`, and `/LiveTv/Programs`; current server returned no channels and focused Retry | Validate a positive channel list on a Live TV-enabled server |
| Live TV playback unsupported recovery | Open channel or debug route `livetv-unsupported` | Verified | 0.1.0.122 shows a focused `Live TV playback unavailable` layer; Escape closes only that layer and returns focus to `Refresh live TV` | Re-run from a real channel when server channels exist |
| Music browsing | Guide Music or debug route `music` | Implemented | 0.1.0.125 opens a dedicated Music page with Albums/Songs/Preview columns; Core tests cover MusicAlbum/Audio queries and type guards; current server returned no true `MusicAlbum` or `Audio` after filtering out server section cards | Validate positive album/song rows on a server with a real music library; add artist hierarchy when artist metadata is exposed |
| Music playback unsupported recovery | Open audio or debug route `music-unsupported` | Verified | 0.1.0.125 shows a focused `Music playback unavailable` layer; Escape closes only that layer and returns to the Music page without stale Loading labels | Re-run from a real audio item when the server has songs |
| Photos browsing | Guide Photos or debug route `photos` | Implemented | 0.1.0.120 opened Photos with `No items found` and focused `Retry` when the server returned no Photo items | Validate a server library that contains Photo items/folders |
| Photo viewer and B recovery | Open Photo item or debug route `photo` | Verified | 0.1.0.120 `photo` route opened an immersive viewer with hidden Guide rail, focused Back, fallback panel, and Escape returned to Home | Validate positive image load when a real Photo item is available |

## Settings And Diagnostics

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open Settings | `M`, eleven `Down`, `Return` | Verified | 0.1.0.126 Computer Use route opened Settings from the expanded Guide with keyboard input only | Keep as shell regression route |
| Inspect signed-in server | Settings page text snapshot | Verified | 0.1.0.126 Settings showed `cyber on https://c1.zdz.plus:443`, `App 0.1.0.126 / Emby client 0.1.0`, input map, and latest diagnostics | Keep startup diagnostics available and add copy/export only if needed |
| Toggle input/playback diagnostics | `Space` on default Settings focus | Verified | 0.1.0.126 `Space` toggled thumbstick seek preview off and updated the status text; a second `Space` restored it on. No app-content mouse clicks were used | Add additional TV-mode scale/theme toggles only after shared theme tokens exist |
| App version / startup diagnostics | Settings page text snapshot | Verified | 0.1.0.126 Settings exposed app/client version and `Last launch completed`; formatter ignores older crash blocks | Keep startup diagnostics available and add copy/export only if needed |

## Current Highest-Value Gaps

1. Details still needs visible similar-items validation plus live add-to success on a disposable collection/playlist target.
2. Playback More drawer needs a fresh keyboard validation pass with real audio/subtitle streams.
3. Live TV and Music have dedicated browse shells, but both still need positive validation on servers that expose channels or real music items.
4. Search error recovery now has a tested timeout guard, but still needs a deliberate offline/server-failure keyboard run.
5. Theme work should keep promoting repeated spacing, typography, focus, and component states into shared resources. The 0.1.0.132 pass aligned core runtime colors with `docs/DESIGN.md`; the 0.1.0.129 pass centralized Library/Search poster-grid dimensions and common card/empty-state typography. Remaining one-off page measurements should keep moving into skin resources before full theme swapping.
