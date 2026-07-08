namespace NextGenEmby.Core.Emby
{
    public static class MusicBrowseQueryFactory
    {
        private const int AlbumLimit = 60;
        private const int SongLimit = 80;

        public static EmbyItemsQuery CreateAlbumsQuery()
        {
            return new EmbyItemsQuery
            {
                IncludeItemTypes = "MusicAlbum",
                SortBy = "SortName",
                SortOrder = "Ascending",
                Limit = AlbumLimit,
                Recursive = true
            };
        }

        public static EmbyItemsQuery CreateSongsQuery()
        {
            return CreateSongsQuery("");
        }

        public static EmbyItemsQuery CreateAlbumSongsQuery(string? albumId)
        {
            return CreateSongsQuery(Normalize(albumId));
        }

        public static EmbyItemsQuery CreateArtistAlbumsQuery(string? artistId)
        {
            var query = CreateAlbumsQuery();
            query.AlbumArtistIds = Normalize(artistId);
            return query;
        }

        public static EmbyItemsQuery CreateArtistSongsQuery(string? artistId)
        {
            var query = CreateSongsQuery("");
            query.ArtistIds = Normalize(artistId);
            return query;
        }

        private static EmbyItemsQuery CreateSongsQuery(string parentId)
        {
            return new EmbyItemsQuery
            {
                ParentId = parentId,
                IncludeItemTypes = "Audio",
                SortBy = string.IsNullOrWhiteSpace(parentId)
                    ? "Artist,Album,SortName"
                    : "ParentIndexNumber,IndexNumber,SortName",
                SortOrder = "Ascending,Ascending,Ascending",
                Limit = SongLimit,
                Recursive = string.IsNullOrWhiteSpace(parentId)
            };
        }

        private static string Normalize(string? value)
        {
            return value == null ? "" : value.Trim();
        }
    }
}
