namespace NextGenEmby.App.Navigation
{
    public sealed class SearchDevelopmentNavigationRequest
    {
        public SearchDevelopmentNavigationRequest(
            string term = "Aurora Protocol",
            bool simulateError = true)
        {
            Term = string.IsNullOrWhiteSpace(term) ? "Aurora Protocol" : term;
            SimulateError = simulateError;
        }

        public string Term { get; }

        public bool SimulateError { get; }
    }
}
