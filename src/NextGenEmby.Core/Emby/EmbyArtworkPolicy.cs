namespace NextGenEmby.Core.Emby
{
    public static class EmbyArtworkPolicy
    {
        public static EmbyImageCandidate? SelectHeroArtwork(EmbyMediaItem? item, int maxWidth)
        {
            if (item == null)
            {
                return null;
            }

            return
                CreateCandidate(item.Id, item.BackdropImageItemId, "Backdrop", item.BackdropImageTag, maxWidth) ??
                CreateCandidate(item.Id, item.ThumbImageItemId, "Thumb", item.ThumbImageTag, maxWidth) ??
                CreateCandidate(item.Id, item.BannerImageItemId, "Banner", item.BannerImageTag, maxWidth) ??
                CreateCandidate(item.Id, item.PrimaryImageItemId, "Primary", item.PrimaryImageTag, maxWidth);
        }

        public static EmbyImageCandidate? SelectPosterArtwork(EmbyMediaItem? item, int maxWidth)
        {
            if (item == null)
            {
                return null;
            }

            return
                CreateCandidate(item.Id, item.PrimaryImageItemId, "Primary", item.PrimaryImageTag, maxWidth) ??
                CreateCandidate(item.Id, item.ThumbImageItemId, "Thumb", item.ThumbImageTag, maxWidth) ??
                CreateCandidate(item.Id, item.BackdropImageItemId, "Backdrop", item.BackdropImageTag, maxWidth);
        }

        public static EmbyImageCandidate? SelectLibraryWideArtwork(EmbyLibraryView? view, int maxWidth)
        {
            if (view == null)
            {
                return null;
            }

            return
                CreateCandidate(view.Id, view.ThumbImageItemId, "Thumb", view.ThumbImageTag, maxWidth) ??
                CreateCandidate(view.Id, view.BackdropImageItemId, "Backdrop", view.BackdropImageTag, maxWidth) ??
                CreateCandidate(view.Id, view.BannerImageItemId, "Banner", view.BannerImageTag, maxWidth) ??
                CreateCandidate(view.Id, view.PrimaryImageItemId, "Primary", view.PrimaryImageTag, maxWidth);
        }

        private static EmbyImageCandidate? CreateCandidate(
            string ownerItemId,
            string imageItemId,
            string imageType,
            string imageTag,
            int maxWidth)
        {
            if (string.IsNullOrWhiteSpace(imageTag))
            {
                return null;
            }

            var resolvedItemId = string.IsNullOrWhiteSpace(imageItemId) ? ownerItemId : imageItemId;
            if (string.IsNullOrWhiteSpace(resolvedItemId))
            {
                return null;
            }

            return new EmbyImageCandidate(resolvedItemId, imageType, maxWidth);
        }
    }
}
