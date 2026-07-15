using System;
using System.Threading.Tasks;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.Playback;

public sealed class NativeDirectXPlaybackBackendTests
{
    [Fact]
    public async Task StartAsync_Maps_Descriptor_To_Native_Open_Request()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            DirectStreamUrl = "https://emby.local/videos/1/stream.mkv?api_key=token",
            HdrProfile = new HdrPlaybackProfile { Kind = HdrPlaybackKind.Hdr10 },
            VideoFrameRate = 23.976
        };

        await backend.StartAsync(new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 1234,
            audioStreamIndex: 2,
            subtitleStreamIndex: 7));

        Assert.Equal("item-1", engine.LastRequest!.ItemId);
        Assert.Equal("source-1", engine.LastRequest.MediaSourceId);
        Assert.Equal(source.DirectStreamUrl, engine.LastRequest.DirectStreamUrl);
        Assert.Equal(1234, engine.LastRequest.StartPositionTicks);
        Assert.Equal(23.976, engine.LastRequest.VideoFrameRate);
        Assert.Equal(2, engine.LastRequest.AudioStreamIndex);
        Assert.Equal(7, engine.LastRequest.SubtitleStreamIndex);
    }

    [Fact]
    public async Task StartAsync_Does_Not_Need_Profile_Metadata_To_Open_Native_Source()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            DirectStreamUrl = "https://emby.local/videos/1/stream.mkv?api_key=token",
            HdrProfile = new HdrPlaybackProfile
            {
                Kind = HdrPlaybackKind.DolbyVisionWithHdr10Fallback,
                IsDolbyVision = true,
                HasHdr10BaseLayer = true
            }
        };

        await backend.StartAsync(new PlaybackDescriptor("item-1", source, new[] { source }, 0));

        Assert.Equal("source-1", engine.LastRequest!.MediaSourceId);
        Assert.Equal(source.DirectStreamUrl, engine.LastRequest.DirectStreamUrl);
    }

    [Fact]
    public async Task StartAsync_Does_Not_Need_Unknown_Hdr_Metadata_To_Open_Native_Source()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            DirectStreamUrl = "https://emby.local/videos/1/stream.mkv?api_key=token",
            IsHdr = true
        };

        await backend.StartAsync(new PlaybackDescriptor("item-1", source, new[] { source }, 0));

        Assert.Equal("source-1", engine.LastRequest!.MediaSourceId);
        Assert.Equal(source.DirectStreamUrl, engine.LastRequest.DirectStreamUrl);
    }

    [Fact]
    public async Task StartAsync_Rejects_Missing_Direct_Stream_Url()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        var source = new EmbyMediaSource { Id = "source-1" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            backend.StartAsync(new PlaybackDescriptor("item-1", source, new[] { source }, 0)));
    }

    [Fact]
    public void StateChanged_Propagates_From_Native_Engine()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        PlaybackStateChangedEventArgs? received = null;
        backend.StateChanged += (_, args) => received = args;

        engine.Raise(PlaybackState.Buffering, "buffering");

        Assert.NotNull(received);
        Assert.Equal(PlaybackState.Buffering, received!.State);
        Assert.Equal("buffering", received.Message);
    }

    [Fact]
    public async Task StreamSwitching_Is_Forwarded_To_Native_Engine()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        var streamBackend = Assert.IsAssignableFrom<IPlaybackStreamSwitchingBackend>(backend);

        await streamBackend.SwitchAudioStreamAsync(4);
        await streamBackend.SwitchSubtitleStreamAsync(null);

        Assert.Equal(4, engine.LastSwitchedAudioStreamIndex);
        Assert.Null(engine.LastSwitchedSubtitleStreamIndex);
        Assert.Equal(1, engine.SubtitleSwitchCount);
    }

    [Fact]
    public void Diagnostics_Are_Exposed_From_Native_Engine()
    {
        var engine = new RecordingNativePlaybackEngine();
        var backend = new NativeDirectXPlaybackBackend(engine);
        var diagnostics = Assert.IsAssignableFrom<IPlaybackBackendDiagnostics>(backend);

        Assert.True(diagnostics.Capabilities.Supports(PlaybackBackendFeature.DirectPlayHttp));
        Assert.True(diagnostics.Capabilities.Supports(PlaybackBackendFeature.NativeAudioOutput));
        Assert.Equal(HdrOutputStatus.Unknown, diagnostics.DisplayStatus.HdrStatus);
    }

    [Fact]
    public void QualityMetrics_Are_Delegated_When_Native_Engine_Provides_Them()
    {
        var engine = new RecordingNativePlaybackEngine
        {
            Metrics = new PlaybackQualityMetricsSnapshot
            {
                RenderedVideoFrames = 240,
                DroppedVideoFrames = 1,
                VideoPositionTicks = 90_000_000,
                AudioClockTicks = 89_950_000,
                MaxFrameGapMs = 42.5,
                AudioVideoDriftMsP95 = 12.0
            }
        };
        var backend = new NativeDirectXPlaybackBackend(engine);
        var metricsProvider = Assert.IsAssignableFrom<IPlaybackQualityMetricsProvider>(backend);

        Assert.True(metricsProvider.TryGetQualityMetrics(out var metrics));
        Assert.Equal(240UL, metrics.RenderedVideoFrames);
        Assert.Equal(1UL, metrics.DroppedVideoFrames);
        Assert.Equal(90_000_000, metrics.VideoPositionTicks);
        Assert.Equal(89_950_000, metrics.AudioClockTicks);
        Assert.Equal(42.5, metrics.MaxFrameGapMs);
        Assert.Equal(12.0, metrics.AudioVideoDriftMsP95);
    }

    private sealed class RecordingNativePlaybackEngine :
        INativePlaybackEngine,
        IPlaybackQualityMetricsProvider
    {
        public NativePlaybackOpenRequest? LastRequest { get; private set; }
        public long CurrentPositionTicks { get; set; }

        public long DurationTicks { get; set; }
        public int? LastSwitchedAudioStreamIndex { get; private set; }
        public int? LastSwitchedSubtitleStreamIndex { get; private set; }
        public int SubtitleSwitchCount { get; private set; }
        public PlaybackBackendCapabilities Capabilities { get; } =
            new PlaybackBackendCapabilities(
                PlaybackBackendFeature.DirectPlayHttp |
            PlaybackBackendFeature.NativeAudioOutput);
        public PlaybackDisplayStatus DisplayStatus { get; } =
            new PlaybackDisplayStatus(HdrOutputStatus.Unknown, false, false);
        public PlaybackQualityMetricsSnapshot? Metrics { get; set; }

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public Task OpenAsync(NativePlaybackOpenRequest request)
        {
            LastRequest = request;
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            return Task.CompletedTask;
        }

        public Task SeekAsync(long positionTicks)
        {
            CurrentPositionTicks = positionTicks;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }

        public Task SwitchAudioStreamAsync(int audioStreamIndex)
        {
            LastSwitchedAudioStreamIndex = audioStreamIndex;
            return Task.CompletedTask;
        }

        public Task SwitchSubtitleStreamAsync(int? subtitleStreamIndex)
        {
            SubtitleSwitchCount++;
            LastSwitchedSubtitleStreamIndex = subtitleStreamIndex;
            return Task.CompletedTask;
        }

        public void Raise(PlaybackState state, string message)
        {
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(state, message));
        }

        public bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics)
        {
            if (Metrics == null)
            {
                metrics = new PlaybackQualityMetricsSnapshot();
                return false;
            }

            metrics = Metrics;
            return true;
        }
    }
}
