# TV Streaming And Personal Media Client Research

Date: 2026-07-07

Scope: visual-system and shell-structure research for Next Gen Xbox Emby on Xbox/TV. This document records the current reference pass before new preview generation or implementation work. It contains no private server credentials, no private artwork, and no implementation changes.

## Framing

Streaming apps and personal media clients solve adjacent but different problems.

Streaming apps such as Netflix and Apple TV are optimized for one owned catalog, strong editorial promotion, and a small set of content classes. They are useful references for dark-room polish, poster-led visual rhythm, focus behavior, and how much chrome can disappear.

Personal media clients such as Plex, Emby, Jellyfin, Infuse, and Kodi are closer to this product. They must expose user-owned libraries, server-provided home sections, multiple media types, playlists, collections, Live TV, music, photos, and sometimes multiple servers. They are the stronger reference for information architecture and density.

The working conclusion is:

- Use streaming clients for cinematic restraint, focus clarity, and artwork-backed atmosphere.
- Use personal media clients for shell model, home composition, library/source navigation, and fallback requirements.
- Do not let Netflix's simplified navigation collapse the product into a movies-and-shows-only app.

## Source Set

Primary product references:

- [Plex Support: Navigating the Big Screen Apps](https://support.plex.tv/articles/navigating-the-big-screen-apps/)
- [Plex Support: Customizing the Big Screen Apps](https://support.plex.tv/articles/customizing-the-apps/)
- [Plex Blog: Introducing Modern Layout](https://www.plex.tv/blog/choose-your-own-adventure-introducing-modern-layout/)
- [Emby for Android TV](https://emby.media/emby-for-android-tv.html)
- [Jellyfin for Android TV on Google Play](https://play.google.com/store/apps/details?hl=en_US&id=org.jellyfin.androidtv)
- [Jellyfin Android TV v0.12 UI update](https://jellyfin.org/posts/android-tv-12/)
- [Jellyfin Android TV v0.17 update](https://jellyfin.org/posts/androidtv-v0.17.0/)
- [Infuse 7.7 Direct Mode](https://firecore.com/blog/infuse-77-direct-mode)
- [Infuse Support: Streaming from Plex, Emby, and Jellyfin](https://support.firecore.com/hc/en-us/articles/360006462093-Streaming-from-Plex-Emby-and-Jellyfin)
- [Kodi Wiki: Estuary settings](https://kodi.wiki/view/Add-on%3AEstuary/Settings)

Streaming and platform references:

- [Netflix Tudum: Netflix's New TV Layout](https://www.netflix.com/tudum/articles/netflix-new-tv-layout)
- [Netflix Help: New TV Experience](https://help.netflix.com/en/node/321880164349028)
- [Netflix Tudum: New Homepage Layout User Guide](https://www.netflix.com/tudum/articles/netflix-new-homepage-layout-user-guide)
- [Apple HIG: Designing for tvOS](https://developer.apple.com/design/human-interface-guidelines/designing-for-tvos)
- [Apple HIG: Focus and selection](https://developer.apple.com/design/human-interface-guidelines/focus-and-selection)
- [Apple HIG: Materials](https://developer.apple.com/design/human-interface-guidelines/materials)
- [Microsoft Learn: Gamepad and remote control interactions](https://learn.microsoft.com/en-us/windows/uwp/ui-input/gamepad-and-remote-interactions)
- [Windows Developer Blog: Tailoring your app for Xbox and the TV](https://blogs.windows.com/windowsdeveloper/2016/09/09/tailoring-your-app-for-xbox-and-the-tv-app-dev-on-xbox-series/)
- [Android Developers: TV layouts](https://developer.android.com/design/ui/tv/guides/styles/layouts)
- [Android Developers: TV focus system](https://developer.android.com/design/ui/tv/guides/styles/focus-system)
- [Amazon Fire TV: Design and User Experience Guidelines](https://developer.amazon.com/docs/fire-tv/design-and-user-experience-guidelines.html)

Community threads and third-party commentary were used only as weak signals for pain points, especially around home customization, missing Continue Watching rows, and Android TV remote constraints. They should not override official platform behavior or the product goal.

## Streaming Client Lessons

### Netflix TV

Netflix's recent TV direction moves primary shortcuts to a top navigation area and uses `My Netflix` as a personal hub for saved, rated, and in-progress content. It also moves deeper genre/category browsing into Search and relies on scrollable rows for discovery.

What to adopt:

- Shorter global route set on the primary shell.
- Strong poster rows and fast return to in-progress content.
- More information at focus time so the user does not have to enter details for every decision.
- A personal hub concept can be useful for Favorites, Continue Watching, Playlists, and account-scoped lists.

What not to copy:

- A top-only navigation model. Netflix can do this because its content model is narrow and editorially controlled.
- Treating Categories as a Search sub-mode for all content. Emby users often browse by library, folder, collection, playlist, genre, Live TV, music, and photos.
- Oversized marketing hero composition. It reduces useful first-viewport density for a personal library.

### Apple TV / tvOS

Apple TV is useful as a material and focus reference, especially the idea that artwork carries emotional color while chrome stays quiet. The blur/glass lesson is not "use glass everywhere"; it is that translucent material only makes visual sense when it samples meaningful content underneath.

What to adopt:

- Artwork-backed atmosphere with darkening and contrast protection.
- Focus through scale, luminance, hierarchy, and content priority.
- Restraint around persistent chrome.

What not to copy:

- Apple-branded glass or Liquid Glass-like refraction.
- Repeated shadow and highlight effects as a default language.
- Full-page translucency over plain graphite.

### Xbox / Android TV / Fire TV Platform Guidance

The implementation target is a TV/controller environment. Android TV and Fire TV both reinforce a 960x540 logical design canvas for 1080p output, and Microsoft guidance reinforces Xbox/gamepad directional navigation and 10-foot testing.

Design implications:

- The visual system should be evaluated at a 960x540 logical canvas before 4K screenshots.
- The UI needs high content throughput without desktop density. Rails, tabs, and side/source guides should show enough choices, but every focus target must remain obvious from couch distance.
- D-pad movement must follow visual geometry. Avoid layouts where a hero, rail, and floating tool compete for directional focus.
- Search can have accelerator access, but a long persistent search field is expensive chrome on a TV home surface.

## Personal Media Client Lessons

### Plex Big Screen Apps

Plex is the closest information-architecture reference. Its big-screen apps combine a Home screen, a navigation sidebar, pinned sources, `More` for unpinned sources, and source-level tabs such as Recommended, Library, Collections, Playlists, and Categories.

Important lessons:

- Home content is driven by pinned media sources.
- Pinned source order can affect Home row order.
- Continue Watching, Recently Added, and What's On Now are list rows, not singleton hero blocks.
- Source pages need modes: Recommended for discovery, Library for direct browsing, Collections, Playlists, and Categories.
- `More` is not a junk drawer. It is a complete source browser for libraries that are not pinned.
- Plex's Modern Layout experiment is relevant for focused poster metadata and artwork-color backgrounds, but the background color must remain subordinate to artwork and contrast.

### Emby Android TV

Emby's official Android TV page describes a home screen that surfaces unfinished items, latest unwatched movies, next episodes for current shows, and Live TV currently airing. This confirms that an Emby-compatible TV home needs mixed media rows and task-oriented resume/discovery sections.

Important lessons:

- Continue Watching and Next Up must be first-class rows.
- Live TV belongs in the home/system model when available, not as an afterthought.
- Library breadth matters: personal videos, music, photos, movies, TV, and Live TV all need routes.
- The home should minimize hunting, but still preserve the user's library structure.

### Jellyfin Android TV

Jellyfin's Android TV client and release notes point toward a simpler TV UI with top-right tools for search/settings/user, home/media sections, removed colored card backgrounds, home-button navigation improvements, and support for Live TV plus recorded shows.

Important lessons:

- Personal media clients often accumulate settings and customization. The shell must leave room for user/server-specific variation.
- Removing colored card backgrounds is consistent with this design system's grayscale chrome rule.
- Toolbar actions are useful, but visual placement should not force a permanent long search field.
- Home section APIs and plugin ecosystems create many possible row types; the visual system must define row behavior without assuming exact server features.

### Infuse

Infuse is useful because it is a polished third-party client for Plex, Emby, and Jellyfin. Direct Mode emphasizes native lists, collections, newly added titles, up-to-date search, and large-library performance.

Important lessons:

- Respect server-native lists and collections instead of flattening everything into a generic recommendation feed.
- A client can feel premium while still preserving personal-server structure.
- Search should query current server state directly; stale local-only search is not acceptable for the shell model.
- Multiple sources should be reorderable and visually clear.

### Kodi / Estuary

Kodi is older and more configurable, but it is still an important home-theater reference because it supports movies, TV shows, music, pictures, Live TV/PVR, plugins, custom nodes, and menu-item visibility.

Important lessons:

- Full media-center apps need a broad route model, not a streaming-service route model.
- Library nodes and smart playlists can be powerful, but exposing too much taxonomy directly can overwhelm TV browsing.
- Home customization is useful, but the default must remain understandable without setup.

## Information Architecture Implications

The next design should treat Home as a media dashboard, not a streaming landing page.

Required first-class surfaces:

- Continue Watching rail.
- Next Up / Up Next rail for episodic content.
- Recently Added / Latest rows per relevant library.
- My Media / Libraries source rail or guide entries.
- Live TV row or entry when available.
- Collections and Playlists as direct, recognizable concepts.
- Favorites / Watchlist / Unwatched as user-intent routes.
- Search as a shell destination and accelerator action.
- Settings, users, and server switching without stealing home-screen space.

Navigation should keep a left source-guide model in the design system. It may be collapsed, summoned, or expanded depending on later Xbox implementation testing, but the core model should be source-aware and able to expose more than five streaming-service categories.

The extended menu should be `More` or an equivalent Source Hub:

- It contains unpinned libraries, server sources, special media types, folders, and lower-frequency destinations.
- It is not a visual dumping ground for unclear commands.
- It should support pin/reorder concepts later, even if not implemented immediately.

## Visual Density Implications

The previous sparse generated previews were too close to a streaming-service promo screen. A personal Emby client needs more useful media visible at once.

Target density:

- A 960x540 logical viewport should show multiple rows or clear evidence of multiple rows.
- Continue Watching must show a horizontal list, not a single dominant item.
- A hero/banner, if used, should be compact enough that at least one high-value rail remains visible.
- Rows should prefer stable card sizes and predictable D-pad movement over dramatic expanded-poster interactions.
- Expanded/focused poster metadata is allowed, but it must not make adjacent content feel unstable or reduce the screen to one title.

This is not desktop density. The target is high content throughput with TV-sized focus targets.

## Visual System Implications

The current Artwork-Backed Matte Fluent direction remains valid with adjustments:

- Keep cool graphite matte chrome.
- Keep green as a micro-signal for play, progress, and success only.
- Use artwork color from real posters, backdrops, thumbnails, or video frames.
- Use blur only when it samples real artwork/video.
- Avoid bright borders, decorative shadows, and frosted panels over plain graphite.
- Prefer rows, source tabs, and guide states to decorative panels.
- Test with real poster-like images, mixed genres, faces, typography, bright covers, and missing-artwork fallbacks.

## Design Decisions To Carry Forward

1. Home is a personal media dashboard with rails, not a marketing hero page.
2. Continue Watching is always a rail/list when more than one item exists.
3. Navigation is source-aware and left-guide compatible, even if the exact collapsed/hidden state is decided later.
4. Search is first-class but should not consume normal home chrome as a long persistent field.
5. `More` or Source Hub is required for unpinned libraries and broader Emby content.
6. Library pages need visual support for at least Recommended, Library, Collections, Playlists, and Categories-style modes where the server supports them.
7. Artwork-backed materials are acceptable only when mapped to feasible Emby image candidates or live video.
8. Generated previews must show realistic media-card density and fallback states before being considered useful.

## Open Questions

- Should the default left guide be fully hidden until invoked, a 72px collapsed rail, or adaptive by page type?
- Should Home lead with Continue Watching, My Media/Libraries, or a compact featured row when the user has active in-progress items?
- How much of Plex's source pin/reorder model should be exposed in v1 versus documented as future behavior?
- Should Search include genre/category browsing like Netflix, or should genre/category stay inside source-level tabs like Plex?
- How should Live TV density differ from movie/series rails on Xbox?

These should be resolved with new high-density mockups and Xbox/XAML feasibility checks before implementation.
