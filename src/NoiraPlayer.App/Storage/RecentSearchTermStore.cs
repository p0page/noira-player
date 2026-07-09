using System.Collections.Generic;
using NoiraPlayer.Core.Emby;
using Windows.Storage;

namespace NoiraPlayer.App.Storage
{
    public sealed class RecentSearchTermStore
    {
        public const string RecentSearchTermsKey = "Search.RecentTerms";

        private readonly ApplicationDataContainer _settings;

        public RecentSearchTermStore()
            : this(ApplicationData.Current.LocalSettings)
        {
        }

        public RecentSearchTermStore(ApplicationDataContainer settings)
        {
            _settings = settings;
        }

        public IReadOnlyList<string> Load()
        {
            object value;
            return _settings.Values.TryGetValue(RecentSearchTermsKey, out value) && value != null
                ? SearchRecentTermsPolicy.FromStoredValue(value.ToString())
                : SearchRecentTermsPolicy.FromStoredValue("");
        }

        public IReadOnlyList<string> Add(string term)
        {
            var terms = SearchRecentTermsPolicy.Add(Load(), term);
            _settings.Values[RecentSearchTermsKey] = SearchRecentTermsPolicy.ToStoredValue(terms);
            return terms;
        }
    }
}
