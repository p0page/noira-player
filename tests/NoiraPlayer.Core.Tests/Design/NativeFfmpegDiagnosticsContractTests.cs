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
    public void Native_Video_Decoder_Logs_Packet_Context_Before_Drain_Failure()
    {
        var source = ReadNativeSource("Media", "VideoDecoder.cpp");
        var failureMessageIndex = source.IndexOf(
            "FFmpeg decoder could not accept a packet and produced no frame while draining.",
            StringComparison.Ordinal);
        var diagnosticIndex = source.IndexOf(
            "VideoDecoder.SendPacket eagain no-frame",
            StringComparison.Ordinal);

        Assert.True(diagnosticIndex >= 0, "decoder drain failures must log packet context first.");
        Assert.True(failureMessageIndex >= 0, "decoder drain failure message must remain searchable.");
        Assert.True(
            diagnosticIndex < failureMessageIndex,
            "decoder drain diagnostic must be emitted before the failure is raised.");
        Assert.Contains("CreatePacketDiagnostic(", source, StringComparison.Ordinal);
        Assert.Contains("receiveResult=", source, StringComparison.Ordinal);
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
