namespace NextGenEmby.App.Navigation
{
    public sealed class SearchDevelopmentNavigationRequest
    {
        public SearchDevelopmentNavigationRequest(
            string term = "Aurora Protocol",
            bool simulateError = true,
            bool useFixtureResults = false,
            string initialScopeKey = "all")
        {
            Term = string.IsNullOrWhiteSpace(term) ? "Aurora Protocol" : term;
            SimulateError = simulateError;
            UseFixtureResults = useFixtureResults;
            InitialScopeKey = string.IsNullOrWhiteSpace(initialScopeKey) ? "all" : initialScopeKey;
        }

        public string Term { get; }

        public bool SimulateError { get; }

        public bool UseFixtureResults { get; }

        public string InitialScopeKey { get; }
    }
}
