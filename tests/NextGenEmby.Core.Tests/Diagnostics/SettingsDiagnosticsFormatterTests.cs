using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class SettingsDiagnosticsFormatterTests
{
    [Fact]
    public void FormatAccount_Shows_User_And_Server_When_Signed_In()
    {
        var text = SettingsDiagnosticsFormatter.FormatAccount(
            "terminus",
            "http://emby.local:8096");

        Assert.Equal("terminus on http://emby.local:8096", text);
    }

    [Fact]
    public void FormatAccount_Shows_Server_When_User_Name_Is_Missing()
    {
        var text = SettingsDiagnosticsFormatter.FormatAccount(
            "",
            "http://emby.local:8096");

        Assert.Equal("Signed in on http://emby.local:8096", text);
    }

    [Fact]
    public void FormatAccount_Shows_Signed_Out_When_Server_Is_Missing()
    {
        var text = SettingsDiagnosticsFormatter.FormatAccount("terminus", "");

        Assert.Equal("Not signed in", text);
    }

    [Fact]
    public void FormatVersionSummary_Combines_App_And_Client_Versions()
    {
        var text = SettingsDiagnosticsFormatter.FormatVersionSummary("0.1.0.113", "0.1.0");

        Assert.Equal("App 0.1.0.113 / Emby client 0.1.0", text);
    }

    [Fact]
    public void FormatStartupSummary_Reports_Last_Completed_Launch()
    {
        var text = SettingsDiagnosticsFormatter.FormatStartupSummary(new[]
        {
            "2026-07-06T10:00:00.0000000+08:00 App.ctor start",
            "2026-07-06T10:00:01.0000000+08:00 App.OnLaunched completed"
        });

        Assert.Equal("Last launch completed", text);
    }

    [Fact]
    public void FormatStartupSummary_Reports_Exceptions_First()
    {
        var text = SettingsDiagnosticsFormatter.FormatStartupSummary(new[]
        {
            "2026-07-06T10:01:00.0000000+08:00 App.ctor start",
            "2026-07-06T10:01:01.0000000+08:00 UnhandledException System.InvalidOperationException boom"
        });

        Assert.Equal("Last launch recorded an exception", text);
    }

    [Fact]
    public void FormatStartupSummary_Ignores_Older_Exception_When_Last_Launch_Completed()
    {
        var text = SettingsDiagnosticsFormatter.FormatStartupSummary(new[]
        {
            "2026-07-06T10:00:00.0000000+08:00 App.ctor start",
            "2026-07-06T10:00:01.0000000+08:00 UnhandledException System.InvalidOperationException boom",
            "2026-07-06T10:01:00.0000000+08:00 App.ctor start",
            "2026-07-06T10:01:01.0000000+08:00 App.OnLaunched completed"
        });

        Assert.Equal("Last launch completed", text);
    }

    [Fact]
    public void FormatStartupSummary_Reports_Missing_Log()
    {
        var text = SettingsDiagnosticsFormatter.FormatStartupSummary(System.Array.Empty<string>());

        Assert.Equal("No startup diagnostics yet", text);
    }
}
