namespace NoiraPlayer.App.Navigation
{
    public enum MediaDetailsDevelopmentFixtureKind
    {
        Standard,
        NoArtwork,
        PrimaryOnlyArtwork,
        LongSourceLabels
    }

    public sealed class MediaDetailsNavigationRequest
    {
        public MediaDetailsNavigationRequest(
            string itemId,
            string itemName,
            bool useDevelopmentFixture = false,
            MediaDetailsDevelopmentFixtureKind developmentFixtureKind = MediaDetailsDevelopmentFixtureKind.Standard)
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
            UseDevelopmentFixture = useDevelopmentFixture;
            DevelopmentFixtureKind = developmentFixtureKind;
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public bool UseDevelopmentFixture { get; }

        public MediaDetailsDevelopmentFixtureKind DevelopmentFixtureKind { get; }
    }
}
