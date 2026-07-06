using System;
using System.Collections.Generic;

namespace NextGenEmby.Core.Emby
{
    public sealed class EmbySearchScope
    {
        public EmbySearchScope(
            string key,
            string label,
            string includeItemTypes,
            bool requireItemTypeMatch)
        {
            Key = key ?? "";
            Label = label ?? "";
            IncludeItemTypes = includeItemTypes ?? "";
            RequireItemTypeMatch = requireItemTypeMatch;
        }

        public string Key { get; }

        public string Label { get; }

        public string IncludeItemTypes { get; }

        public bool RequireItemTypeMatch { get; }
    }

    public static class EmbySearchScopePolicy
    {
        private static readonly EmbySearchScope[] Scopes =
        {
            new EmbySearchScope("all", "All", "Movie,Series,Episode,Video,MusicVideo,BoxSet,Playlist,Person,MusicAlbum,Audio,Photo,TvChannel", false),
            new EmbySearchScope("movies", "Movies", "Movie", true),
            new EmbySearchScope("shows", "Shows", "Series", true),
            new EmbySearchScope("episodes", "Episodes", "Episode", true),
            new EmbySearchScope("collections", "Collections", "BoxSet", true),
            new EmbySearchScope("playlists", "Playlists", "Playlist", true),
            new EmbySearchScope("people", "People", "Person", true),
            new EmbySearchScope("music", "Music", "MusicAlbum,Audio", true),
            new EmbySearchScope("photos", "Photos", "Photo", true),
            new EmbySearchScope("livetv", "Live TV", "TvChannel", true)
        };

        public static IReadOnlyList<EmbySearchScope> AllScopes => Scopes;

        public static EmbySearchScope GetScope(string? key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                foreach (var scope in Scopes)
                {
                    if (string.Equals(scope.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return scope;
                    }
                }
            }

            return Scopes[0];
        }
    }
}
