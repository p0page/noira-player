using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace NoiraPlayer.App.Services
{
    internal static class PlaybackDiagnosticsLog
    {
        private const string FileName = "playback-diagnostics.log";
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        public static void WriteLine(string message)
        {
            _ = WriteLineAsync(message);
        }

        public static async Task ClearAsync()
        {
            await Gate.WaitAsync();
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    FileName,
                    CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, "");
            }
            catch
            {
            }
            finally
            {
                Gate.Release();
            }
        }

        public static async Task WriteLineAsync(string message)
        {
            await Gate.WaitAsync();
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    FileName,
                    CreationCollisionOption.OpenIfExists);
                var line = DateTimeOffset.Now.ToString("O") + " " + (message ?? "") + Environment.NewLine;
                await FileIO.AppendTextAsync(file, line);
            }
            catch
            {
            }
            finally
            {
                Gate.Release();
            }
        }
    }
}
