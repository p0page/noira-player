using System;
using System.IO;
using System.Linq;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class AppHostedQualityCaptureContractTests
{
    [Fact]
    public void Debug_Quality_Run_Route_Is_Wired_To_Playback_Capture()
    {
        var root = FindRepositoryRoot();
        var mainPage = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "MainPage.xaml.cs"));
        var webBridge = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Web", "NoiraWebBridge.cs"));
        var launchRequest = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Navigation", "PlaybackLaunchRequest.cs"));
        var playbackPage = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs"));

        Assert.Contains("PostWebMessageAsJson", mainPage, StringComparison.Ordinal);
        Assert.Contains("case \"playback.nativePlayItem\":", webBridge, StringComparison.Ordinal);
        Assert.Contains("Frame.Navigate(", mainPage, StringComparison.Ordinal);
        Assert.Contains("typeof(PlaybackPage)", mainPage, StringComparison.Ordinal);
        Assert.Contains("new PlaybackLaunchRequest(", webBridge, StringComparison.Ordinal);
        Assert.Contains("ReadPayloadString(root, \"itemId\", \"\")", webBridge, StringComparison.Ordinal);
        Assert.Contains("ReadPayloadString(root, \"itemName\", \"\")", webBridge, StringComparison.Ordinal);
        Assert.Contains("ReadPayloadLong(root, \"startPositionTicks\", 0)", webBridge, StringComparison.Ordinal);
        Assert.Contains("ReadPayloadString(root, \"mediaSourceId\", \"\")", webBridge, StringComparison.Ordinal);
        Assert.Contains("ReadPayloadLong(root, \"runtimeTicks\", 0)", webBridge, StringComparison.Ordinal);
        Assert.DoesNotContain("playback.getDirectStream", webBridge, StringComparison.Ordinal);
        Assert.DoesNotContain("<video", File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Web", "src", "App.tsx")), StringComparison.Ordinal);
        Assert.DoesNotContain("case \"quality-run\":", mainPage, StringComparison.Ordinal);
        Assert.DoesNotContain("qualityRunId: command.RunId", mainPage, StringComparison.Ordinal);
        Assert.DoesNotContain("qualityExpected: command.Expected", mainPage, StringComparison.Ordinal);
        Assert.DoesNotContain("qualityRunDurationSeconds: command.DurationSeconds", mainPage, StringComparison.Ordinal);
        Assert.DoesNotContain("streamUrl: command.StreamUrl", mainPage, StringComparison.Ordinal);

        Assert.Contains("public string QualityRunId { get; }", launchRequest, StringComparison.Ordinal);
        Assert.Contains("public string DirectStreamUrl { get; }", launchRequest, StringComparison.Ordinal);
        Assert.Contains("public bool HasDirectStreamUrl", launchRequest, StringComparison.Ordinal);
        Assert.Contains("public bool IsQualityRun", launchRequest, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityExpected?", launchRequest, StringComparison.Ordinal);

        Assert.Contains("StartLaunchRequestPlaybackAsync", playbackPage, StringComparison.Ordinal);
        Assert.Contains("StartDirectStreamQualityRunPlaybackAsync", playbackPage, StringComparison.Ordinal);
        Assert.Contains("CreateDirectStreamQualityRunSource", playbackPage, StringComparison.Ordinal);
        Assert.Contains("ScheduleQualityRunCapture", playbackPage, StringComparison.Ordinal);
        Assert.Contains("RunQualityRunLifecycleProbeAsync", playbackPage, StringComparison.Ordinal);
        Assert.Contains("_orchestrator.PauseAsync()", playbackPage, StringComparison.Ordinal);
        Assert.Contains("_orchestrator.ResumeAsync()", playbackPage, StringComparison.Ordinal);
        Assert.Contains("_orchestrator.SeekAsync", playbackPage, StringComparison.Ordinal);
        Assert.Contains("_orchestrator.StopAsync()", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityCaptureReferenceCaseFactory.Create", playbackPage, StringComparison.Ordinal);
        Assert.Contains("Scenario = referenceCase.ExecutionRequirement.Scenario", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult", playbackPage, StringComparison.Ordinal);
        Assert.Contains("WriteQualityRunErrorReportAsync", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityRuntimeEvidenceCollector.ComposeErrorRunResult", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityCapturedReportPath.GetReportRelativePath", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityReportSerializer.Serialize", playbackPage, StringComparison.Ordinal);
        Assert.Contains("lifecycle.Events.Add", playbackPage, StringComparison.Ordinal);
        Assert.Contains("\"pause\"", playbackPage, StringComparison.Ordinal);
        Assert.Contains("\"resume\"", playbackPage, StringComparison.Ordinal);
        Assert.Contains("\"seek\"", playbackPage, StringComparison.Ordinal);
        Assert.Contains("\"stop\"", playbackPage, StringComparison.Ordinal);
        Assert.Contains("quality-run", playbackPage, StringComparison.Ordinal);
        Assert.Contains("captured", playbackPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Debug_Quality_Run_Captures_Runtime_Evidence_Before_Stopping_Playback()
    {
        var root = FindRepositoryRoot();
        var playbackPage = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs"));
        var captureMethod = ExtractMethodBody(playbackPage, "CaptureQualityRunAsync");
        var lifecycleProbeMethod = ExtractMethodBody(playbackPage, "RunQualityRunLifecycleProbeAsync");
        var seekProbeMethod = ExtractMethodBody(playbackPage, "private async Task RunQualityRunSeekProbeAsync");

        var captureEvidenceIndex = captureMethod.IndexOf(
            "CaptureQualityRunEvidence(_backend, descriptor)",
            StringComparison.Ordinal);
        var seekProbeIndex = captureMethod.IndexOf(
            "await RunQualityRunSeekProbeAsync",
            StringComparison.Ordinal);
        var stopIndex = captureMethod.IndexOf(
            "await StopQualityRunPlaybackAsync",
            StringComparison.Ordinal);

        Assert.DoesNotContain("_orchestrator.StopAsync()", lifecycleProbeMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("_orchestrator.SeekAsync", lifecycleProbeMethod, StringComparison.Ordinal);
        Assert.Contains("_orchestrator.SeekAsync", seekProbeMethod, StringComparison.Ordinal);
        Assert.True(captureEvidenceIndex >= 0, "quality-run capture must sample runtime evidence.");
        Assert.True(seekProbeIndex >= 0, "quality-run capture must run seek probe after evidence is captured.");
        Assert.True(stopIndex >= 0, "quality-run capture must stop playback after evidence is captured.");
        Assert.True(
            captureEvidenceIndex < seekProbeIndex,
            "quality-run must sample playback metrics before the seek probe mutates native runtime counters.");
        Assert.True(
            captureEvidenceIndex < stopIndex,
            "quality-run must sample playback metrics before StopAsync resets native runtime metrics.");
        Assert.Contains("evidence.Diagnostics", captureMethod, StringComparison.Ordinal);
        Assert.Contains("evidence.MetricsProvider", captureMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void Debug_Quality_Run_Item_Playback_Guards_PlaybackInfo_With_Interactive_Timeout()
    {
        var root = FindRepositoryRoot();
        var playbackPage = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs"));
        var startItemPlaybackMethod = ExtractMethodBody(playbackPage, "private async Task StartItemPlaybackAsync");

        Assert.Contains("InteractiveRequestGuard.WithTimeoutAsync", startItemPlaybackMethod, StringComparison.Ordinal);
        Assert.Contains("() =>", startItemPlaybackMethod, StringComparison.Ordinal);
        Assert.Contains("playbackInfoClient.GetPlaybackInfoAsync", startItemPlaybackMethod, StringComparison.Ordinal);
        Assert.Contains("playbackInfoSession", startItemPlaybackMethod, StringComparison.Ordinal);
        Assert.Contains("playbackInfoItemId", startItemPlaybackMethod, StringComparison.Ordinal);
        var compactMethod = new string(startItemPlaybackMethod.Where(value => !char.IsWhiteSpace(value)).ToArray());
        Assert.Contains(
            "playbackInfoClient.GetPlaybackInfoAsync(playbackInfoSession,playbackInfoItemId,request.MediaSourceId)",
            compactMethod,
            StringComparison.Ordinal);
        Assert.Contains("EmbyRequestTimeoutPolicy.InteractiveRequestTimeout", startItemPlaybackMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void Debug_Quality_Run_Direct_Stream_Source_Projects_Expected_Metadata()
    {
        var root = FindRepositoryRoot();
        var playbackPage = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs"));
        var sourceMethod = ExtractMethodBody(
            playbackPage,
            "private static EmbyMediaSource CreateDirectStreamQualityRunSource");

        Assert.Contains("Width = expected == null ? 0 : expected.Width", sourceMethod, StringComparison.Ordinal);
        Assert.Contains("Height = expected == null ? 0 : expected.Height", sourceMethod, StringComparison.Ordinal);
        Assert.Contains("HdrProfile = CreateQualityRunHdrProfile(expected)", sourceMethod, StringComparison.Ordinal);
        Assert.Contains("Codec = expected == null ? \"\" : expected.Codec", sourceMethod, StringComparison.Ordinal);
        Assert.Contains("VideoRange = expected == null ? \"\" : expected.VideoRange", sourceMethod, StringComparison.Ordinal);
        Assert.Contains("ColorPrimaries = expected == null ? \"\" : expected.ColorPrimaries", sourceMethod, StringComparison.Ordinal);
        Assert.Contains("ColorTransfer = expected == null ? \"\" : expected.ColorTransfer", sourceMethod, StringComparison.Ordinal);
        Assert.Contains("ColorSpace = expected == null ? \"\" : expected.ColorSpace", sourceMethod, StringComparison.Ordinal);
        Assert.Contains("CreateQualityRunHdrProfile", playbackPage, StringComparison.Ordinal);
        Assert.Contains("ParseQualityRunHdrKind", playbackPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Debug_Native_WinRt_Display_Status_Projects_Refresh_Rate()
    {
        var root = FindRepositoryRoot();
        var playbackEngine = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Playback", "WinRtNativePlaybackEngine.cs"));
        var displayStatusProperty = ExtractMethodBody(
            playbackEngine,
            "public PlaybackDisplayStatus DisplayStatus");

        Assert.Contains("status.RefreshRateHz", displayStatusProperty, StringComparison.Ordinal);
    }

    [Fact]
    public void Debug_Quality_Run_Uses_Software_Refresh_Snapshot_When_Display_Refresh_Is_Missing()
    {
        var root = FindRepositoryRoot();
        var playbackPage = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs"));
        var captureEvidenceMethod = ExtractMethodBody(
            playbackPage,
            "private static QualityRunEvidence CaptureQualityRunEvidence");

        Assert.Contains("CaptureQualityRunEvidence(", playbackPage, StringComparison.Ordinal);
        Assert.Contains("IPlaybackBackend backend", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackDescriptor descriptor", playbackPage, StringComparison.Ordinal);
        Assert.Contains("CreateQualityRunDisplayStatus", captureEvidenceMethod, StringComparison.Ordinal);
        Assert.Contains("PlaybackRefreshRatePolicy.SelectSoftwareOnlyRefreshRateSnapshot", playbackPage, StringComparison.Ordinal);
        Assert.Contains("descriptor.MediaSource.VideoFrameRate", playbackPage, StringComparison.Ordinal);
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        var methodIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        if (methodIndex < 0)
        {
            throw new InvalidOperationException(methodName + " was not found.");
        }

        var openBraceIndex = source.IndexOf('{', methodIndex);
        if (openBraceIndex < 0)
        {
            throw new InvalidOperationException(methodName + " has no opening brace.");
        }

        var depth = 0;
        for (var index = openBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(openBraceIndex, index - openBraceIndex + 1);
                }
            }
        }

        throw new InvalidOperationException(methodName + " has no closing brace.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.App", "MainPage.xaml.cs")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.Core", "PlaybackQuality", "PlaybackQualityReport.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
