using System;
using System.Collections.Generic;

namespace NextGenEmby.Core.Emby
{
    public sealed class EmbySearchScope
    {
        public EmbySearchScope(string key, string label, string includeItemTypes)
        {
            Key = key ?? "";
            Label = label ?? "";
            IncludeItemTypes = includeItemTypes ?? "";
        }

        public string Key { get; }

        public string Label { get; }

        public string IncludeItemTypes { get; }
    }

    public static class EmbySearchScopePolicy
    {
        private static readonly EmbySearchScope[] Scopes =
        {
            new EmbySearchScope("all", "All", "Movie,Series,Episode,Video,MusicVideo,BoxSet,Playlist,Person,MusicAlbum,Audio,Photo,TvChannel"),
            new EmbySearchScope("movies", "Movies", "Movie"),
            new EmbySearchScope("shows", "Shows", "Series"),
            new EmbySearchScope("episodes", "Episodes", "Episode"),
            new EmbySearchScope("collections", "Collections", "BoxSet"),
            new EmbySearchScope("playlists", "Playlists", "Playlist"),
            new EmbySearchScope("people", "People", "Person"),
            new EmbySearchScope("music", "Music", "MusicAlbum,Audio"),
            new EmbySearchScope("photos", "Photos", "Photo"),
            new EmbySearchScope("livetv", "Live TV", "TvChannel")
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
