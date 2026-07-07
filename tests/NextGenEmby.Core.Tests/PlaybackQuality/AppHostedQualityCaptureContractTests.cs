using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class AppHostedQualityCaptureContractTests
{
    [Fact]
    public void Debug_Quality_Run_Route_Is_Wired_To_Playback_Capture()
    {
        var root = FindRepositoryRoot();
        var mainPage = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "MainPage.xaml.cs"));
        var launchRequest = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "Navigation", "PlaybackLaunchRequest.cs"));
        var playbackPage = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "Views", "PlaybackPage.xaml.cs"));

        Assert.Contains("case \"quality-run\":", mainPage, StringComparison.Ordinal);
        Assert.Contains("qualityRunId: command.RunId", mainPage, StringComparison.Ordinal);
        Assert.Contains("qualityExpected: command.Expected", mainPage, StringComparison.Ordinal);
        Assert.Contains("qualityRunDurationSeconds: command.DurationSeconds", mainPage, StringComparison.Ordinal);

        Assert.Contains("public string QualityRunId { get; }", launchRequest, StringComparison.Ordinal);
        Assert.Contains("public bool IsQualityRun", launchRequest, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityExpected?", launchRequest, StringComparison.Ordinal);

        Assert.Contains("ScheduleQualityRunCapture", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityCaptureReferenceCaseFactory.Create", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityCapturedReportPath.GetReportRelativePath", playbackPage, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityReportSerializer.Serialize", playbackPage, StringComparison.Ordinal);
        Assert.Contains("quality-run", playbackPage, StringComparison.Ordinal);
        Assert.Contains("captured", playbackPage, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NextGenEmby.App", "MainPage.xaml.cs")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NextGenEmby.Core", "PlaybackQuality", "PlaybackQualityReport.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
