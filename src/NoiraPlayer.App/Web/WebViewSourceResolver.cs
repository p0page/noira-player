using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace NoiraPlayer.App.Web
{
    internal static class WebViewSourceResolver
    {
        private const string DevelopmentUrlFileName = "webview-dev-url.txt";

        public static async Task<Uri> ResolveAsync(Uri packagedSource)
        {
            if (packagedSource == null)
            {
                throw new ArgumentNullException(nameof(packagedSource));
            }

#if DEBUG
            try
            {
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(DevelopmentUrlFileName);
                var file = item as StorageFile;
                if (file != null)
                {
                    var value = (await FileIO.ReadTextAsync(file)).Trim();
                    if (Uri.TryCreate(value, UriKind.Absolute, out var developmentSource) &&
                        (string.Equals(developmentSource.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(developmentSource.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                    {
                        return developmentSource;
                    }
                }
            }
            catch
            {
            }
#endif

            return packagedSource;
        }
    }
}
