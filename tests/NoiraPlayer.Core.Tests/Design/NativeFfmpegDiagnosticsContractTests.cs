using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class NativeFfmpegDiagnosticsContractTests
{
    [Fact]
    public void Native_Ffmpeg_Open_Logs_Blocking_Substeps_With_Durations()
    {
        var source = ReadNativeSource("Media", "FfmpegMediaSource.cpp");

        Assert.Contains("FfmpegMediaSource.Open avformat_open_input begin", source, StringComparison.Ordinal);
        Assert.Contains("FfmpegMediaSource.Open avformat_open_input end result=0 durationMs=", source, StringComparison.Ordinal);
        Assert.Contains("FfmpegMediaSource.Open avformat_find_stream_info begin", source, StringComparison.Ordinal);
        Assert.Contains("FfmpegMediaSource.Open avformat_find_stream_info end result=0 durationMs=", source, StringComparison.Ordinal);
        Assert.Contains("streamCountBefore=", source, StringComparison.Ordinal);
        Assert.Contains("streamCountAfter=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Ffmpeg_Open_Preserves_Blocking_Substep_Durations_As_Structured_Evidence()
    {
        var header = ReadNativeSource("Media", "FfmpegMediaSource.h");
        var source = ReadNativeSource("Media", "FfmpegMediaSource.cpp");

        Assert.Contains("struct FfmpegOpenTimingSnapshot", header, StringComparison.Ordinal);
        Assert.Contains("FfmpegOpenTimingSnapshot OpenTimingSnapshot() const noexcept;", header, StringComparison.Ordinal);
        Assert.Contains("m_openTiming.OpenInputDurationMs", source, StringComparison.Ordinal);
        Assert.Contains("m_openTiming.StreamInfoDurationMs", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Ffmpeg_Startup_Attributes_Avio_Transport_Bytes_To_Each_Blocking_Phase()
    {
        var header = ReadNativeSource("Media", "FfmpegMediaSource.h");
        var source = ReadNativeSource("Media", "FfmpegMediaSource.cpp");
        var graph = ReadNativeSource("Media", "PlaybackGraph.cpp");

        Assert.Contains("uint64_t OpenInputBytesRead", header, StringComparison.Ordinal);
        Assert.Contains("uint64_t StreamInfoBytesRead", header, StringComparison.Ordinal);
        Assert.Contains("uint64_t TransportBytesRead() const noexcept;", header, StringComparison.Ordinal);
        Assert.Contains("m_formatContext->pb->bytes_read", source, StringComparison.Ordinal);
        Assert.Contains("m_openTiming.OpenInputBytesRead", source, StringComparison.Ordinal);
        Assert.Contains("m_openTiming.StreamInfoBytesRead", source, StringComparison.Ordinal);
        Assert.Contains("TransportByteDelta", source, StringComparison.Ordinal);
        Assert.Contains("transportBytesBeforeStartupSeek", graph, StringComparison.Ordinal);
        Assert.Contains("NativeStartupSeekBytesRead", graph, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Video_Decoder_Logs_Packet_Context_During_Bounded_Eagain_Recovery()
    {
        var source = ReadNativeSource("Media", "VideoDecoder.cpp");
        var failureMessageIndex = source.IndexOf(
            "FFmpeg video decoder made no progress after bounded packet recovery.",
            StringComparison.Ordinal);
        var retryDiagnosticIndex = source.IndexOf(
            "VideoDecoder.SendPacket eagain retry",
            StringComparison.Ordinal);
        var exhaustedDiagnosticIndex = source.IndexOf(
            "VideoDecoder.SendPacket eagain exhausted",
            StringComparison.Ordinal);

        Assert.True(retryDiagnosticIndex >= 0, "decoder recovery attempts must log packet context.");
        Assert.True(exhaustedDiagnosticIndex > retryDiagnosticIndex, "retry exhaustion must have a distinct diagnostic.");
        Assert.True(failureMessageIndex >= 0, "bounded recovery failure message must remain searchable.");
        Assert.True(
            exhaustedDiagnosticIndex < failureMessageIndex,
            "decoder exhaustion diagnostic must be emitted before the failure is raised.");
        Assert.Contains("CreatePacketDiagnostic(", source, StringComparison.Ordinal);
        Assert.Contains("receiveResult=", source, StringComparison.Ordinal);
        Assert.Contains("maxRetries=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Ffmpeg_Blocking_Io_Has_A_Deadline_And_Can_Be_Interrupted_By_Stop()
    {
        var header = ReadNativeSource("Media", "FfmpegMediaSource.h");
        var source = ReadNativeSource("Media", "FfmpegMediaSource.cpp");
        var graphSource = ReadNativeSource("Media", "PlaybackGraph.cpp");

        Assert.Contains("void Interrupt() noexcept;", header, StringComparison.Ordinal);
        Assert.Contains("std::atomic<bool> m_interruptRequested", header, StringComparison.Ordinal);
        Assert.Contains("interrupt_callback.callback", source, StringComparison.Ordinal);
        Assert.Contains("BeginBlockingIo", source, StringComparison.Ordinal);
        Assert.Contains("m_mediaSource.Interrupt();", graphSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Http_Input_Enables_Bounded_Ffmpeg_Reconnect_For_Idle_Connection_Recovery()
    {
        var source = ReadNativeSource("Media", "FfmpegMediaSource.cpp");

        Assert.Contains("IsHttpSource", source, StringComparison.Ordinal);
        Assert.Contains("AVDictionary* openOptions", source, StringComparison.Ordinal);
        Assert.Contains("\"reconnect\", \"1\"", source, StringComparison.Ordinal);
        Assert.Contains("\"reconnect_on_network_error\", \"1\"", source, StringComparison.Ordinal);
        Assert.Contains("\"reconnect_max_retries\", \"3\"", source, StringComparison.Ordinal);
        Assert.Contains("\"reconnect_delay_max\", \"2\"", source, StringComparison.Ordinal);
        Assert.Contains("\"reconnect_delay_total_max\", \"6\"", source, StringComparison.Ordinal);
        Assert.Contains("avformat_open_input(&formatContext, source.c_str(), nullptr, &openOptions)", source, StringComparison.Ordinal);
        Assert.Contains("av_dict_free(&openOptions);", source, StringComparison.Ordinal);
    }

    private static string ReadNativeSource(params string[] segments)
    {
        var parts = new string[segments.Length + 3];
        parts[0] = FindRepositoryRoot();
        parts[1] = "src";
        parts[2] = "NoiraPlayer.Native";
        Array.Copy(segments, 0, parts, 3, segments.Length);
        return File.ReadAllText(Path.Combine(parts));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.Native", "NoiraPlayer.Native.vcxproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

}
