using System;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

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
            IsHdr = true
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
        Assert.True(engine.LastRequest.IsHdr);
        Assert.Equal(2, engine.LastRequest.AudioStreamIndex);
        Assert.Equal(7, engine.LastRequest.SubtitleStreamIndex);
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

    private sealed class RecordingNativePlaybackEngine : INativePlaybackEngine
    {
        public NativePlaybackOpenRequest? LastRequest { get; private set; }
        public long CurrentPositionTicks { get; set; }
        public int? LastSwitchedAudioStreamIndex { get; private set; }
        public int? LastSwitchedSubtitleStreamIndex { get; private set; }
        public int SubtitleSwitchCount { get; private set; }
        public PlaybackBackendCapabilities Capabilities { get; } =
            new PlaybackBackendCapabilities(
                PlaybackBackendFeature.DirectPlayHttp |
                PlaybackBackendFeature.NativeAudioOutput);
        public PlaybackDisplayStatus DisplayStatus { get; } =
            new PlaybackDisplayStatus(HdrOutputStatus.Unknown, false, false);

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
    }
}
