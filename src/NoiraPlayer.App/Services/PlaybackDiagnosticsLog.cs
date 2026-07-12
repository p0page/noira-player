using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace NoiraPlayer.App.Services
{
    internal static class PlaybackDiagnosticsLog
    {
        private const string FileName = "playback-diagnostics.log";
        private const ulong MaxFileBytes = 256 * 1024;
        private const int MaxMessageCharacters = 2048;
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
                var safeMessage = message ?? "";
                if (safeMessage.Length > MaxMessageCharacters)
                {
                    safeMessage = safeMessage.Substring(0, MaxMessageCharacters);
                }

                var line = DateTimeOffset.Now.ToString("O") + " " + safeMessage + Environment.NewLine;
                var properties = await file.GetBasicPropertiesAsync();
                var maximumLineBytes = (ulong)(line.Length * 4);
                if (properties.Size + maximumLineBytes > MaxFileBytes)
                {
                    file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                        FileName,
                        CreationCollisionOption.ReplaceExisting);
                }

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
