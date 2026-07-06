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
| Launch installed app into saved session | App launch, wait | Verified | 0.1.0.104 launched to Home with saved Emby session | Keep startup diagnostics active |
| Log in from empty session | Tab/Enter through login fields | Implemented | Login page and credential store exist | Re-run with cleared session before completion |
| Failed login recovery | Enter invalid login, edit fields | Partial | UI has login status handling | Add explicit keyboard-only failure run |
| Open and close guide from Home | `M`, `Escape` | Verified | 0.1.0.77 guide route fixed | Add visual screenshot pass after major shell changes |

## Home

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Hero play/resume focus | Launch Home | Verified | 0.1.0.104 Home focused Play on hero | Validate on non-resumable libraries |
| Move from hero to rails | `Down` | Verified | 0.1.0.104 moved to Media Libraries rail | Keep `StartBringIntoView` behavior |
| Move horizontally across section cards | `Right`, `Left` | Verified | 0.1.0.104 focused `热门剧集`; 0.1.0.102 kept focused `日漫` visible | Validate at far-right end of very long rails |
| Open media library card | `Return` | Verified | 0.1.0.104 opened `热门剧集`; 0.1.0.102 opened `日漫` | Add route for section `More` buttons |
| Return from library to originating Home card | `Escape` | Verified | 0.1.0.104 preserved `热门剧集` focus | Keep as regression route |
| Continue Watching item route | `Down` to row, `Return` | Implemented | Home resume cards and Details/Playback requests exist | Fresh keyboard run needed |
| Server-configured home sections | Rail navigation | Verified | 0.1.0.102 parsed many server categories with section artwork | Add far-end and `More` validation |

## Guide Rail

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open guide without leaving current page | `M` | Verified | 0.1.0.77 fixed guide overlay behavior | Validate over Search and Playback boundaries |
| Navigate Home/Search/Movies/Shows | `M`, arrows, `Return` | Verified | 0.1.0.77 and later runs | Keep guide focus restoration tests |
| Navigate Collections/Playlists/Favorites/Unwatched | `M`, arrows, `Return` | Verified | 0.1.0.78 run opened Playlists, Favorites, Unwatched | Add fresh visual screenshots |
| Navigate Live TV/Music/Photos | `M`, arrows, `Return` | Implemented | Main guide destinations exist | Need server-supported keyboard run and unsupported-state audit |

## Library Browsing

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open movie/series library grid | Guide/Home, `Return` | Verified | 0.1.0.104 `热门剧集`; 0.1.0.102 `日漫` | Validate more collection types |
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
| Submit query | Type text, `Return` | Verified | 0.1.0.105 searched `Terrifier` and returned 3 results | Add result Details route |
| Move scopes | `Down`, `Left`/`Right` | Implemented | `SearchFocusNavigationPolicy` tests exist | Validate long scope rail on TV viewport |
| Open result Details | Results, `Return` | Implemented | Result item click navigates Details | Fresh keyboard run |
| Empty search recovery | Nonsense query, `Down`, `Down`, `Return` | Verified | 0.1.0.105 showed central empty panel, focused `Edit search`, and selected the query on return | Keep as Search regression route |
| Search error recovery | Network/API failure | Implemented | Error state now exposes retry/edit query focus targets | Add offline/server-failure keyboard run |

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
| Live TV channel browsing | Guide Live TV | Partial | Generic library request exists for channels | Add dedicated channel/program model when server supports it |
| Live TV playback unsupported recovery | Open stream | Missing | No deliberate unsupported state | Add visible non-dead-end state before playback support |
| Music browsing | Guide Music | Partial | Generic music album/audio query exists | Add album/artist/song hierarchy |
| Music playback unsupported recovery | Open audio | Partial | Playback can route audio-like items, but UI copy not audited | Validate and add explanation if unsupported |
| Photos browsing | Guide Photos | Partial | Photo query exists | Add photo viewer route and B recovery |

## Settings And Diagnostics

| Operation | Keyboard Path | Status | Evidence | Next Work |
| --- | --- | --- | --- | --- |
| Open Settings | Guide Settings | Implemented | 0.1.0.117 debug route opened Settings without XAML exception and rendered the TV page | Fresh Guide keyboard route with real keyboard or Computer Use; local `SendInput` did not drive UWP navigation reliably |
| Inspect signed-in server | Arrows | Implemented | 0.1.0.117 Settings shows `cyber on https://c1.zdz.plus:443` and app/client version | Re-run through Guide keyboard route |
| Toggle input/playback diagnostics | Arrows, `Return` or `Space` | Implemented | Thumbstick seek preview setting persists through `PlaybackPreferenceStore`; PlaybackPage reads it before thumbstick preview seek | Fresh real-keyboard toggle validation; local key injection kept focus visible but did not toggle the UWP checkbox |
| App version / startup diagnostics | Settings or logs | Implemented | 0.1.0.117 Settings shows `App 0.1.0.117 / Emby client 0.1.0` and latest-launch diagnostics; formatter ignores older crash blocks | Keep startup diagnostics available and add copy/export only if needed |

## Current Highest-Value Gaps

1. Details still needs visible similar-items validation plus live add-to success on a disposable collection/playlist target.
2. Playback More drawer needs a fresh keyboard validation pass with real audio/subtitle streams.
3. Live TV, Music, and Photos need dedicated non-dead-end surfaces instead of generic library fallback.
4. Settings Guide route and checkbox toggle need a fresh real-keyboard or Computer Use pass because local synthetic key injection did not change UWP focus/toggle state.
5. Search error recovery needs a deliberate offline/server-failure keyboard run.
