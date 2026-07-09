using System.Collections.Generic;

namespace NoiraPlayer.App.Navigation
{
    public sealed class SearchDevelopmentNavigationRequest
    {
        public SearchDevelopmentNavigationRequest(
            string term = "Aurora Protocol",
            bool simulateError = true,
            string initialScopeKey = "all")
        {
            Term = string.IsNullOrWhiteSpace(term) ? "Aurora Protocol" : term;
            SimulateError = simulateError;
            InitialScopeKey = string.IsNullOrWhiteSpace(initialScopeKey) ? "all" : initialScopeKey;
        }

        public string Term { get; }

        public bool SimulateError { get; }

        public string InitialScopeKey { get; }
    }
}
