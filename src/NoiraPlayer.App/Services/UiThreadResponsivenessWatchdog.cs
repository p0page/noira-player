using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using NoiraPlayer.Core.Diagnostics;
using Windows.UI.Core;

namespace NoiraPlayer.App.Services
{
    internal sealed class UiThreadResponsivenessWatchdog : IDisposable
    {
        internal const string FileName = "ui-responsiveness.log";
        private static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan LogInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaximumHealthyLatency = TimeSpan.FromSeconds(1);

        private readonly CoreDispatcher _dispatcher;
        private readonly string _logPath;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly UiResponsivenessTracker _tracker = new UiResponsivenessTracker();
        private Timer? _timer;
        private TimeSpan _lastLogAt;
        private int _timerCallbackActive;
        private bool _disposed;

        public UiThreadResponsivenessWatchdog(CoreDispatcher dispatcher, string logPath)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _logPath = string.IsNullOrWhiteSpace(logPath)
                ? throw new ArgumentException("A log path is required.", nameof(logPath))
                : logPath;
        }

        public void Start()
        {
            if (_timer != null)
            {
                return;
            }

            TryWriteText(
                DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture) +
                " session-start" + Environment.NewLine,
                replace: true);
            _timer = new Timer(OnTimer, null, ProbeInterval, ProbeInterval);
        }

        public void Dispose()
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }

        private void OnTimer(object? state)
        {
            if (_disposed || Interlocked.Exchange(ref _timerCallbackActive, 1) != 0)
            {
                return;
            }

            try
            {
                var now = _clock.Elapsed;
                if (_tracker.TryBeginProbe(now))
                {
                    try
                    {
                        _ = _dispatcher.RunAsync(
                            CoreDispatcherPriority.Low,
                            () => _tracker.CompleteProbe(_clock.Elapsed));
                    }
                    catch
                    {
                        _tracker.CancelProbe();
                    }
                }

                if (now - _lastLogAt >= LogInterval)
                {
                    _lastLogAt = now;
                    WriteSnapshot(_tracker.CaptureAndReset(now));
                }
            }
            finally
            {
                Volatile.Write(ref _timerCallbackActive, 0);
            }
        }

        private void WriteSnapshot(UiResponsivenessSnapshot snapshot)
        {
            var maxLatencyMs = snapshot.MaximumDispatchLatency.TotalMilliseconds;
            var pendingMs = snapshot.PendingDuration.TotalMilliseconds;
            var healthy = maxLatencyMs <= MaximumHealthyLatency.TotalMilliseconds &&
                pendingMs <= MaximumHealthyLatency.TotalMilliseconds;
            var line =
                DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture) +
                " completed=" + snapshot.CompletedProbeCount.ToString(CultureInfo.InvariantCulture) +
                " skipped=" + snapshot.SkippedProbeCount.ToString(CultureInfo.InvariantCulture) +
                " maxDispatchMs=" + maxLatencyMs.ToString("F1", CultureInfo.InvariantCulture) +
                " pendingMs=" + pendingMs.ToString("F1", CultureInfo.InvariantCulture) +
                " healthy=" + healthy.ToString(CultureInfo.InvariantCulture) +
                Environment.NewLine;
            TryWriteText(line, replace: false);
        }

        private void TryWriteText(string text, bool replace)
        {
            try
            {
                if (replace)
                {
                    File.WriteAllText(_logPath, text);
                }
                else
                {
                    File.AppendAllText(_logPath, text);
                }
            }
            catch
            {
            }
        }
    }
}
