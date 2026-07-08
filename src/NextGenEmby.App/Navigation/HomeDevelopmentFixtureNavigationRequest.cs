namespace NextGenEmby.App.Navigation
{
    public sealed class HomeDevelopmentFixtureNavigationRequest
    {
        public HomeDevelopmentFixtureNavigationRequest(string name = "home-fixture")
        {
            Name = string.IsNullOrWhiteSpace(name) ? "home-fixture" : name;
        }

        public string Name { get; }
    }
}
