using System;
using System.Globalization;
using System.IO;
using Windows.Storage;

namespace NoiraPlayer.App.Input
{
    internal static class InputDiagnosticsLog
    {
        private const string FileName = "input-diagnostics.log";

        public static void Write(string message)
        {
            try
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);
                var line = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                    " " + (message ?? "") + Environment.NewLine;
                File.AppendAllText(path, line);
            }
            catch
            {
            }
        }
    }
}
