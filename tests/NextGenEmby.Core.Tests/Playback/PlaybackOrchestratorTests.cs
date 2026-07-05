using System;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class PlaybackOrchestratorTests
{
    [Fact]
    public async Task StartAsync_Uses_First_MediaSource_With_Resume_Position()
    {
        var backend = new RecordingPlaybackBackend();
        var firstSource = Source("source-1");
        var secondSource = Source("source-2");
        var sources = new[] { firstSource, secondSource };
        var orchestrator = new PlaybackOrchestrator(backend);

        await orchestrator.StartAsync("item-1", sources, 12_345);

        Assert.Equal(PlaybackState.Playing, orchestrator.State);
        Assert.Same(firstSource, orchestrator.CurrentMediaSource);
        Assert.Same(firstSource, backend.LastDescriptor!.MediaSource);
        Assert.Same(sources, backend.LastDescriptor.AvailableSources);
        Assert.Equal("item-1", backend.LastDescriptor.ItemId);
        Assert.Equal(12_345, backend.LastDescriptor.StartPositionTicks);
        Assert.Null(backend.LastDescriptor.AudioStreamIndex);
        Assert.Null(backend.LastDescriptor.SubtitleStreamIndex);
    }

    [Fact]
    public async Task SwitchMediaSourceAsync_Starts_Selected_Source_At_Current_Position()
    {
        var backend = new RecordingPlaybackBackend();
        var firstSource = Source("source-1");
        var secondSource = Source("source-2");
        var sources = new[] { firstSource, secondSource };
        var orchestrator = new PlaybackOrchestrator(backend);

        await orchestrator.StartAsync("item-1", sources, 1_000);
        backend.CurrentPositionTicks = 55_000;

        await orchestrator.SwitchMediaSourceAsync("source-2");

        Assert.Equal(PlaybackState.Playing, orchestrator.State);
        Assert.Same(secondSource, orchestrator.CurrentMediaSource);
        Assert.Same(secondSource, backend.LastDescriptor!.MediaSource);
        Assert.Same(sources, backend.LastDescriptor.AvailableSources);
        Assert.Equal("item-1", backend.LastDescriptor.ItemId);
        Assert.Equal(55_000, backend.LastDescriptor.StartPositionTicks);
    }

    [Fact]
    public async Task SwitchMediaSourceAsync_Clears_Source_Specific_Stream_Selections()
    {
        var backend = new RecordingPlaybackBackend();
        var firstSource = Source(
            "source-1",
            Stream(1, EmbyStreamKind.Audio),
            Stream(2, EmbyStreamKind.Subtitle));
        var secondSource = Source(
            "source-2",
            Stream(3, EmbyStreamKind.Audio),
            Stream(4, EmbyStreamKind.Subtitle));
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { firstSource, secondSource }, 0);
        await orchestrator.SwitchAudioStreamAsync(1);
        await orchestrator.SwitchSubtitleStreamAsync(2);

        await orchestrator.SwitchMediaSourceAsync("source-2");

        Assert.Same(secondSource, backend.LastDescriptor!.MediaSource);
        Assert.Null(backend.LastDescriptor.AudioStreamIndex);
        Assert.Null(backend.LastDescriptor.SubtitleStreamIndex);
    }

    [Fact]
    public async Task SwitchAudioStreamAsync_Restarts_Current_Source_With_Selected_Audio_Index()
    {
        var backend = new RecordingPlaybackBackend();
        var source = Source("source-1", Stream(4, EmbyStreamKind.Audio));
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 1_000);
        backend.CurrentPositionTicks = 70_000;

        await orchestrator.SwitchAudioStreamAsync(4);

        Assert.Same(source, backend.LastDescriptor!.MediaSource);
        Assert.Equal(70_000, backend.LastDescriptor.StartPositionTicks);
        Assert.Equal(4, backend.LastDescriptor.AudioStreamIndex);
        Assert.Null(backend.LastDescriptor.SubtitleStreamIndex);
    }

    [Fact]
    public async Task SwitchAudioStreamAsync_Uses_In_Place_Backend_When_Available()
    {
        var backend = new RecordingStreamSwitchingPlaybackBackend();
        var source = Source("source-1", Stream(4, EmbyStreamKind.Audio));
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 1_000);

        await orchestrator.SwitchAudioStreamAsync(4);

        Assert.Equal(1, backend.StartCount);
        Assert.Equal(4, backend.LastSwitchedAudioStreamIndex);
        Assert.Equal(4, orchestrator.CurrentDescriptor!.AudioStreamIndex);
        Assert.Equal(PlaybackState.Playing, orchestrator.State);
    }

    [Fact]
    public async Task SwitchSubtitleStreamAsync_Preserves_Selected_Audio_Index()
    {
        var backend = new RecordingPlaybackBackend();
        var source = Source(
            "source-1",
            Stream(1, EmbyStreamKind.Audio),
            Stream(7, EmbyStreamKind.Subtitle));
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 1_000);
        await orchestrator.SwitchAudioStreamAsync(1);
        backend.CurrentPositionTicks = 88_000;

        await orchestrator.SwitchSubtitleStreamAsync(7);

        Assert.Same(source, backend.LastDescriptor!.MediaSource);
        Assert.Equal(88_000, backend.LastDescriptor.StartPositionTicks);
        Assert.Equal(1, backend.LastDescriptor.AudioStreamIndex);
        Assert.Equal(7, backend.LastDescriptor.SubtitleStreamIndex);
    }

    [Fact]
    public async Task SwitchSubtitleStreamAsync_Uses_In_Place_Backend_To_Disable_Subtitles()
    {
        var backend = new RecordingStreamSwitchingPlaybackBackend();
        var source = Source("source-1", Stream(7, EmbyStreamKind.Subtitle));
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 0);
        await orchestrator.SwitchSubtitleStreamAsync(7);

        await orchestrator.SwitchSubtitleStreamAsync(null);

        Assert.Equal(1, backend.StartCount);
        Assert.Equal(2, backend.SubtitleSwitchCount);
        Assert.Null(backend.LastSwitchedSubtitleStreamIndex);
        Assert.Null(orchestrator.CurrentDescriptor!.SubtitleStreamIndex);
        Assert.Equal(PlaybackState.Playing, orchestrator.State);
    }

    [Fact]
    public async Task SwitchStreamsAsync_Rejects_Indexes_Not_Present_On_Current_Source()
    {
        var backend = new RecordingPlaybackBackend();
        var source = Source(
            "source-1",
            Stream(1, EmbyStreamKind.Audio),
            Stream(2, EmbyStreamKind.Subtitle));
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 0);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => orchestrator.SwitchAudioStreamAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => orchestrator.SwitchAudioStreamAsync(2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => orchestrator.SwitchSubtitleStreamAsync(1));
    }

    [Fact]
    public async Task SwitchSubtitleStreamAsync_Allows_Null_To_Disable_Subtitles()
    {
        var backend = new RecordingPlaybackBackend();
        var source = Source("source-1", Stream(7, EmbyStreamKind.Subtitle));
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 0);
        await orchestrator.SwitchSubtitleStreamAsync(7);

        await orchestrator.SwitchSubtitleStreamAsync(null);

        Assert.Null(backend.LastDescriptor!.SubtitleStreamIndex);
    }

    [Fact]
    public async Task PlaybackCommands_Throw_When_Playback_Has_Not_Started()
    {
        var backend = new RecordingPlaybackBackend();
        var orchestrator = new PlaybackOrchestrator(backend);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.PauseAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ResumeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.SeekAsync(10));

        await orchestrator.StartAsync("item-1", new[] { Source("source-1") }, 0);
        await orchestrator.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.PauseAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ResumeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.SeekAsync(10));
    }

    [Fact]
    public async Task Failed_Switch_Rolls_Back_Stream_Selection_And_State()
    {
        var backend = new RecordingPlaybackBackend();
        var source = Source(
            "source-1",
            Stream(1, EmbyStreamKind.Audio),
            Stream(2, EmbyStreamKind.Audio));
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 0);
        backend.FailNextStart = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.SwitchAudioStreamAsync(2));

        Assert.Equal(PlaybackState.Playing, orchestrator.State);
        Assert.Null(orchestrator.CurrentDescriptor!.AudioStreamIndex);
    }

    [Fact]
    public async Task Failed_Initial_Start_Does_Not_Leave_Playback_Started()
    {
        var backend = new RecordingPlaybackBackend { FailNextStart = true };
        var orchestrator = new PlaybackOrchestrator(backend);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync("item-1", new[] { Source("source-1") }, 0));

        Assert.Equal(PlaybackState.Failed, orchestrator.State);
        Assert.Null(orchestrator.CurrentMediaSource);
        Assert.Null(orchestrator.CurrentDescriptor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.PauseAsync());
    }

    [Fact]
    public async Task Backend_StateChanged_Updates_Orchestrator_State_And_Reemits_Event()
    {
        var backend = new RecordingPlaybackBackend();
        var orchestrator = new PlaybackOrchestrator(backend);
        PlaybackStateChangedEventArgs? received = null;
        orchestrator.StateChanged += (_, args) => received = args;
        await orchestrator.StartAsync("item-1", new[] { Source("source-1") }, 0);

        backend.RaiseStateChanged(PlaybackState.Buffering, "buffering");

        Assert.Equal(PlaybackState.Buffering, orchestrator.State);
        Assert.NotNull(received);
        Assert.Equal(PlaybackState.Buffering, received!.State);
        Assert.Equal("buffering", received.Message);
    }

    [Fact]
    public async Task Backend_StateChanged_Reemits_PositionTicks()
    {
        var backend = new RecordingPlaybackBackend();
        var orchestrator = new PlaybackOrchestrator(backend);
        PlaybackStateChangedEventArgs? received = null;
        orchestrator.StateChanged += (_, args) => received = args;
        await orchestrator.StartAsync("item-1", new[] { Source("source-1") }, 0);

        backend.RaiseStateChanged(PlaybackState.Playing, "position", 123_456);

        Assert.NotNull(received);
        Assert.Equal(PlaybackState.Playing, received!.State);
        Assert.Equal(123_456, received.PositionTicks);
    }

    [Fact]
    public async Task CreateProgressRequest_Maps_Current_Playback_Context()
    {
        var backend = new RecordingPlaybackBackend();
        var source = Source(
            "source-1",
            Stream(1, EmbyStreamKind.Audio),
            Stream(7, EmbyStreamKind.Subtitle));
        source.PlaySessionId = "play-session-1";
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 0);
        await orchestrator.SwitchAudioStreamAsync(1);
        await orchestrator.SwitchSubtitleStreamAsync(7);
        backend.CurrentPositionTicks = 456_789;

        var progress = orchestrator.CreateProgressRequest(PlaybackProgressEvent.TimeUpdate);

        Assert.Equal("item-1", progress.ItemId);
        Assert.Equal("source-1", progress.MediaSourceId);
        Assert.Equal("play-session-1", progress.PlaySessionId);
        Assert.Equal(456_789, progress.PositionTicks);
        Assert.False(progress.IsPaused);
        Assert.Equal(PlaybackProgressEvent.TimeUpdate, progress.EventName);
        Assert.Equal(PlaybackPlayMethod.DirectPlay, progress.PlayMethod);
        Assert.Equal(1, progress.AudioStreamIndex);
        Assert.Equal(7, progress.SubtitleStreamIndex);
    }

    [Fact]
    public async Task CreateSessionRequest_Maps_Current_Playback_Context()
    {
        var backend = new RecordingPlaybackBackend();
        var source = Source(
            "source-1",
            Stream(1, EmbyStreamKind.Audio),
            Stream(7, EmbyStreamKind.Subtitle));
        source.PlaySessionId = "play-session-1";
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 0);
        await orchestrator.SwitchAudioStreamAsync(1);
        await orchestrator.SwitchSubtitleStreamAsync(7);
        backend.CurrentPositionTicks = 456_789;

        var session = orchestrator.CreateSessionRequest();

        Assert.Equal("item-1", session.ItemId);
        Assert.Equal("source-1", session.MediaSourceId);
        Assert.Equal("play-session-1", session.PlaySessionId);
        Assert.Equal(456_789, session.PositionTicks);
        Assert.False(session.IsPaused);
        Assert.Equal(PlaybackPlayMethod.DirectPlay, session.PlayMethod);
        Assert.Equal(1, session.AudioStreamIndex);
        Assert.Equal(7, session.SubtitleStreamIndex);
    }

    [Theory]
    [InlineData(PlaybackState.Failed)]
    [InlineData(PlaybackState.Stopped)]
    public async Task Backend_Terminal_StateChanged_Clears_Playback_Context(PlaybackState terminalState)
    {
        var backend = new RecordingPlaybackBackend();
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { Source("source-1") }, 0);

        backend.RaiseStateChanged(terminalState, "terminal");

        Assert.Equal(terminalState, orchestrator.State);
        Assert.Null(orchestrator.CurrentMediaSource);
        Assert.Null(orchestrator.CurrentDescriptor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.SeekAsync(10));
    }

    [Fact]
    public async Task Failed_Replacement_Start_Clears_Previous_Playback_Context()
    {
        var backend = new RecordingPlaybackBackend();
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { Source("source-1") }, 0);
        backend.FailNextStart = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync("item-2", new[] { Source("source-2") }, 0));

        Assert.Equal(PlaybackState.Failed, orchestrator.State);
        Assert.Null(orchestrator.CurrentMediaSource);
        Assert.Null(orchestrator.CurrentDescriptor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.PauseAsync());
    }

    [Fact]
    public async Task Terminal_Backend_Event_During_Start_Wins_Over_Successful_Start_Return()
    {
        var backend = new RecordingPlaybackBackend
        {
            StateToRaiseDuringStart = PlaybackState.Failed
        };
        var orchestrator = new PlaybackOrchestrator(backend);

        await orchestrator.StartAsync("item-1", new[] { Source("source-1") }, 0);

        Assert.Equal(PlaybackState.Failed, orchestrator.State);
        Assert.Null(orchestrator.CurrentMediaSource);
        Assert.Null(orchestrator.CurrentDescriptor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ResumeAsync());
    }

    [Fact]
    public async Task Terminal_Backend_Event_During_Thrown_Switch_Start_Wins_Over_Rollback()
    {
        var backend = new RecordingPlaybackBackend();
        var source = Source(
            "source-1",
            Stream(1, EmbyStreamKind.Audio),
            Stream(2, EmbyStreamKind.Audio));
        var orchestrator = new PlaybackOrchestrator(backend);
        await orchestrator.StartAsync("item-1", new[] { source }, 0);
        backend.StateToRaiseDuringStart = PlaybackState.Failed;
        backend.FailNextStart = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.SwitchAudioStreamAsync(2));

        Assert.Equal(PlaybackState.Failed, orchestrator.State);
        Assert.Null(orchestrator.CurrentMediaSource);
        Assert.Null(orchestrator.CurrentDescriptor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ResumeAsync());
    }

    [Fact]
    public async Task StartAsync_Throws_When_No_MediaSources_Are_Available()
    {
        var backend = new RecordingPlaybackBackend();
        var orchestrator = new PlaybackOrchestrator(backend);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync("item-1", Array.Empty<EmbyMediaSource>(), 0));

        Assert.Equal(PlaybackState.Stopped, orchestrator.State);
        Assert.Null(backend.LastDescriptor);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task StartAsync_Throws_When_ItemId_Is_Empty(string itemId)
    {
        var backend = new RecordingPlaybackBackend();
        var orchestrator = new PlaybackOrchestrator(backend);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            orchestrator.StartAsync(itemId, new[] { Source("source-1") }, 0));

        Assert.Equal(PlaybackState.Stopped, orchestrator.State);
        Assert.Null(backend.LastDescriptor);
    }

    private static EmbyMediaSource Source(string id, params EmbyMediaStream[] streams)
    {
        var source = new EmbyMediaSource
        {
            Id = id,
            Name = id
        };

        source.Streams.AddRange(streams);
        return source;
    }

    private static EmbyMediaStream Stream(int index, EmbyStreamKind kind) => new EmbyMediaStream
    {
        Index = index,
        Kind = kind
    };

    private sealed class RecordingPlaybackBackend : IPlaybackBackend
    {
        public PlaybackDescriptor? LastDescriptor { get; private set; }
        public long CurrentPositionTicks { get; set; }
        public int StartCount { get; private set; }
        public bool FailNextStart { get; set; }
        public PlaybackState? StateToRaiseDuringStart { get; set; }

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public Task StartAsync(PlaybackDescriptor descriptor)
        {
            StartCount++;
            LastDescriptor = descriptor;
            if (StateToRaiseDuringStart.HasValue)
            {
                RaiseStateChanged(StateToRaiseDuringStart.Value);
                StateToRaiseDuringStart = null;
            }

            if (FailNextStart)
            {
                FailNextStart = false;
                throw new InvalidOperationException("Backend failed.");
            }

            return Task.CompletedTask;
        }

        public Task PauseAsync() => Task.CompletedTask;

        public Task ResumeAsync() => Task.CompletedTask;

        public Task SeekAsync(long positionTicks)
        {
            CurrentPositionTicks = positionTicks;
            return Task.CompletedTask;
        }

        public Task StopAsync() => Task.CompletedTask;

        public void RaiseStateChanged(PlaybackState state, string message = "", long? positionTicks = null)
        {
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(state, message, positionTicks));
        }
    }

    private sealed class RecordingStreamSwitchingPlaybackBackend : IPlaybackBackend, IPlaybackStreamSwitchingBackend
    {
        public PlaybackDescriptor? LastDescriptor { get; private set; }
        public long CurrentPositionTicks { get; set; }
        public int StartCount { get; private set; }
        public int? LastSwitchedAudioStreamIndex { get; private set; }
        public int? LastSwitchedSubtitleStreamIndex { get; private set; }
        public int SubtitleSwitchCount { get; private set; }

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public Task StartAsync(PlaybackDescriptor descriptor)
        {
            StartCount++;
            LastDescriptor = descriptor;
            return Task.CompletedTask;
        }

        public Task PauseAsync() => Task.CompletedTask;

        public Task ResumeAsync() => Task.CompletedTask;

        public Task SeekAsync(long positionTicks)
        {
            CurrentPositionTicks = positionTicks;
            return Task.CompletedTask;
        }

        public Task StopAsync() => Task.CompletedTask;

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

        public void RaiseStateChanged(PlaybackState state, string message = "", long? positionTicks = null)
        {
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(state, message, positionTicks));
        }
    }
}
