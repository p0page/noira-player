using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ModernUwpSolutionContractTests
{
    [Fact]
    public void Primary_Solution_Is_Modern_And_Legacy_Entry_Files_Are_Removed()
    {
        var primarySolution = ReadRepositoryFile("NoiraPlayer.sln");

        Assert.Contains("# Visual Studio Version 18", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.App.Modern.csproj", primarySolution, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.App.csproj", primarySolution, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Legacy.sln", primarySolution, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Modern.sln", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.Core.csproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.Native.vcxproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.Core.Tests.csproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.PlaybackQuality.Headless.csproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.PlaybackQuality.Cli.csproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("Debug|x64", primarySolution, StringComparison.Ordinal);
        Assert.Contains("Release|x64", primarySolution, StringComparison.Ordinal);
        Assert.DoesNotContain("Debug|x86", primarySolution, StringComparison.Ordinal);
        Assert.False(File.Exists(RepositoryPath("NoiraPlayer.Legacy.sln")), "The VS2022 legacy solution should be removed from the modernized tree.");
        Assert.False(File.Exists(RepositoryPath("src", "NoiraPlayer.App", "NoiraPlayer.App.csproj")), "The old UAP app project should be removed from the modernized tree.");
        Assert.False(File.Exists(RepositoryPath("tools", "Register-NoiraLooseApp.ps1")), "The old VS2022 loose deploy helper should be removed from the modernized tree.");
        Assert.False(File.Exists(RepositoryPath("tools", "Register-NoiraLooseApp.tests.ps1")), "The old VS2022 loose deploy helper tests should be removed with the helper.");
        Assert.True(File.Exists(RepositoryPath("tools", "Register-NoiraModernUwp.ps1")), "The modern loose deploy helper should remain the supported registration entry.");
    }

    [Fact]
    public void Modern_App_Project_Remains_Uwp_Msix_Appcontainer_And_Xbox_Compatible()
    {
        var modernProject = ReadRepositoryFile("src", "NoiraPlayer.App", "NoiraPlayer.App.Modern.csproj");
        var manifest = ReadRepositoryFile("src", "NoiraPlayer.App", "Package.appxmanifest");

        Assert.Contains("<NoiraPlatformCompatibility>UWP-MSIX-AppContainer-Xbox</NoiraPlatformCompatibility>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<UseUwp>true</UseUwp>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<EnableMsixTooling>true</EnableMsixTooling>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<ApplicationManifest>Package.appxmanifest</ApplicationManifest>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<Platforms>x64</Platforms>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Microsoft.UI.Xaml\" Version=\"2.8.7\" />", modernProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.WindowsAppSDK", modernProject, StringComparison.Ordinal);
        Assert.DoesNotContain("WinUI3", modernProject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<TargetDeviceFamily Name=\"Windows.Universal\" MinVersion=\"10.0.19041.0\" MaxVersionTested=\"10.0.26100.0\" />", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxVersionTested=\"10.0.22621.0\"", manifest, StringComparison.Ordinal);
        Assert.Contains("<rescap:Capability Name=\"hevcPlayback\" />", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Modern_NativeAot_App_Always_Uses_Compiler_Optimizations()
    {
        var modernProject = ReadRepositoryFile("src", "NoiraPlayer.App", "NoiraPlayer.App.Modern.csproj");

        Assert.Contains("<PublishAot>true</PublishAot>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<Optimize>true</Optimize>", modernProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<Optimize>false</Optimize>", modernProject, StringComparison.Ordinal);
    }

    [Fact]
    public void Modern_App_Primary_Shell_Is_WebView2_Hosted_React_Vite_Surface()
    {
        var modernProject = ReadRepositoryFile("src", "NoiraPlayer.App", "NoiraPlayer.App.Modern.csproj");
        var mainPageXaml = ReadRepositoryFile("src", "NoiraPlayer.App", "MainPage.xaml");
        var mainPageSource = ReadRepositoryFile("src", "NoiraPlayer.App", "MainPage.xaml.cs");
        var nativeBridgeSource = ReadRepositoryFile("src", "NoiraPlayer.App", "Web", "NoiraWebBridge.cs");
        var nativeBridgeResultSource = ReadRepositoryFile("src", "NoiraPlayer.App", "Web", "NoiraWebBridgeResult.cs");
        var playbackPageSource = ReadRepositoryFile("src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs");
        var metadataTransportSource = ReadRepositoryFile("src", "NoiraPlayer.Core", "Emby", "EmbyMetadataTransport.cs");
        var sourceResolver = ReadRepositoryFile("src", "NoiraPlayer.App", "Web", "WebViewSourceResolver.cs");
        var webAppSource = ReadRepositoryFile("src", "NoiraPlayer.Web", "src", "App.tsx");
        var webBridgeSource = ReadRepositoryFile("src", "NoiraPlayer.Web", "src", "bridge.ts");
        var homeCatalogSource = ReadRepositoryFile("src", "NoiraPlayer.Web", "src", "catalog", "homeCatalog.ts");
        var libraryPageSource = ReadRepositoryFile("src", "NoiraPlayer.Web", "src", "pages", "LibraryPage.tsx");
        var detailsPageSource = ReadRepositoryFile("src", "NoiraPlayer.Web", "src", "pages", "DetailsPage.tsx");
        var webStyles = ReadRepositoryFile("src", "NoiraPlayer.Web", "src", "styles.css");

        Assert.Contains("xmlns:muxc=\"using:Microsoft.UI.Xaml.Controls\"", mainPageXaml, StringComparison.Ordinal);
        Assert.Contains("<muxc:WebView2", mainPageXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShellWebView\"", mainPageXaml, StringComparison.Ordinal);
        Assert.Contains("EnsureCoreWebView2Async()", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("NavigationCacheMode = NavigationCacheMode.Required", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("SetVirtualHostNameToFolderMapping(", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("\"app.noira.local\"", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("\"WebCode\"", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("CoreWebView2HostResourceAccessKind.Deny", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("AreDefaultContextMenusEnabled = false", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("IsStatusBarEnabled = false", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("IsPasswordAutosaveEnabled = false", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("IsGeneralAutofillEnabled = false", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("new Uri(PackagedWebAppUrl)", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("await WebViewSourceResolver.ResolveAsync", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("webview-dev-url.txt", sourceResolver, StringComparison.Ordinal);
        Assert.Contains("#if DEBUG", sourceResolver, StringComparison.Ordinal);
        Assert.Contains("ApplicationData.Current.LocalFolder.TryGetItemAsync", sourceResolver, StringComparison.Ordinal);
        Assert.Contains("FileIO.ReadTextAsync", sourceResolver, StringComparison.Ordinal);
        Assert.Contains("Uri.TryCreate", sourceResolver, StringComparison.Ordinal);
        Assert.Contains("Uri.UriSchemeHttp", sourceResolver, StringComparison.Ordinal);
        Assert.Contains("Uri.UriSchemeHttps", sourceResolver, StringComparison.Ordinal);
        Assert.Contains("return packagedSource;", sourceResolver, StringComparison.Ordinal);
        Assert.Contains("WebMessageReceived += ShellWebView_OnWebMessageReceived", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("PostWebMessageAsJson", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("PlaybackReturnedMessage", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("host.lifecycle", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("playback-returned", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("protected override void OnNavigatedTo", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("PlaybackPage.TeardownCompleted +=", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("_playbackReturnObserved", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("_playbackTeardownCompleted", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("TryPostPlaybackReturned", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("EnsureCoreWebView2Async", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("PlaybackReturnNotificationMaxAttempts", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("await Task.Delay", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("public static event EventHandler? TeardownCompleted", playbackPageSource, StringComparison.Ordinal);
        Assert.Contains("TeardownCompleted?.Invoke", playbackPageSource, StringComparison.Ordinal);
        Assert.Contains("new NoiraWebBridge()", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("await _webBridge.HandleAsync", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("args.Source", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("case \"auth.bootstrap\":", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("case \"auth.login\":", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("case \"auth.logout\":", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("case \"emby.get\":", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("case \"playback.nativePlayItem\":", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("new LoginViewModel(_sessionStore)", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("new ApplicationDataSessionStore()", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("EmbyAuthorization.CreateHeaderValue", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("private readonly EmbyMetadataTransport _metadataTransport;", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("EmbyMetadataTransport.CreateDefault()", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("_metadataTransport.GetAsync(", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("\\\"timing\\\":{", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new HttpClient", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("http.SendAsync", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("EmbyAuthorization.Apply", metadataTransportSource, StringComparison.Ordinal);
        Assert.Contains("HttpMethod.Get", metadataTransportSource, StringComparison.Ordinal);
        Assert.Contains("AllowAutoRedirect = false", metadataTransportSource, StringComparison.Ordinal);
        Assert.Contains("UseCookies = false", metadataTransportSource, StringComparison.Ordinal);
        Assert.Contains("PooledConnectionLifetime", metadataTransportSource, StringComparison.Ordinal);
        Assert.Contains("PooledConnectionIdleTimeout", metadataTransportSource, StringComparison.Ordinal);
        Assert.Contains("HttpCompletionOption.ResponseHeadersRead", metadataTransportSource, StringComparison.Ordinal);
        Assert.Contains("EmbyWebPathPolicy.IsAllowed(session, path)", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool IsAllowedEmbyPath", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("IsAllowedSource", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("UriComponents.SchemeAndServer", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("var responseId = \"\";", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("return Result(Error(responseId, \"bridge-failed\"", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("Frame.Navigate(", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("typeof(PlaybackPage)", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("new PlaybackLaunchRequest(", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("playback-navigation-failed", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("PlaybackNavigationFailedResponseJson", nativeBridgeResultSource, StringComparison.Ordinal);
        Assert.Contains("if (result.PlaybackRequest == null)", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("_playbackNavigationPending = Frame.Navigate", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("? result.ResponseJson", mainPageSource, StringComparison.Ordinal);
        Assert.Contains(": result.PlaybackNavigationFailedResponseJson", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("ReadPayloadLong(root, \"startPositionTicks\", 0)", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("ReadPayloadString(root, \"mediaSourceId\", \"\")", nativeBridgeSource, StringComparison.Ordinal);
        Assert.Contains("ReadPayloadLong(root, \"runtimeTicks\", 0)", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateDemoBridgeResponse", mainPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoItemsJson", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"session.get\":", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"home.load\":", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"items.list\":", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"item.get\":", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("playback.getDirectStream", nativeBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("interactive-examples.mdn.mozilla.net", mainPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Windows.UI.Xaml.Controls.WebView", mainPageXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize", mainPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ContentFrame", mainPageXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ContentFrame", mainPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GuideRail", mainPageXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterGuideButtonFocusHandlers", mainPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateLogin()", mainPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateTo(typeof(", mainPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("<video", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectStreamResult", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("directStreamUrl", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Play in WebView", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Native fallback", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("playback.getDirectStream", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("playback.getDirectStream", webBridgeSource, StringComparison.Ordinal);
        Assert.Contains("playback.nativePlayItem", webAppSource, StringComparison.Ordinal);
        Assert.Contains("new EmbyWebClient", webAppSource, StringComparison.Ordinal);
        Assert.Contains("createEmbyFetchTransport", webAppSource, StringComparison.Ordinal);
        Assert.Contains("setClient(nextClient)", webAppSource, StringComparison.Ordinal);
        Assert.Contains("'auth.bootstrap'", webAppSource, StringComparison.Ordinal);
        Assert.Contains("client.getViews()", homeCatalogSource, StringComparison.Ordinal);
        Assert.Contains("client.getItemsPage(", libraryPageSource, StringComparison.Ordinal);
        Assert.Contains("client.getItem(", webAppSource, StringComparison.Ordinal);
        Assert.Contains("<DetailsPage", webAppSource, StringComparison.Ordinal);
        Assert.Contains("getDetailsPlayFocusKey", detailsPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("'session.get'", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("'home.load'", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("'items.list'", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("'item.get'", webAppSource, StringComparison.Ordinal);
        Assert.DoesNotContain("'home.load'", webBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("'items.list'", webBridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("'item.get'", webBridgeSource, StringComparison.Ordinal);
        Assert.Contains(".media-card", webStyles, StringComparison.Ordinal);
        Assert.Contains(".details-page__atmosphere", webStyles, StringComparison.Ordinal);
        Assert.Contains("aspect-ratio:", webStyles, StringComparison.Ordinal);
        Assert.Contains("@media (max-height: 600px)", webStyles, StringComparison.Ordinal);
        Assert.Contains("-webkit-line-clamp: 2", webStyles, StringComparison.Ordinal);
        Assert.Contains("-webkit-line-clamp: 3", webStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("video {", webStyles, StringComparison.Ordinal);

        Assert.Contains("NoiraPlayer.Web", modernProject, StringComparison.Ordinal);
        Assert.Contains("BuildNoiraWebClient", modernProject, StringComparison.Ordinal);
        Assert.Contains("<NoiraWebClientDist Include=\"$(NoiraWebClientDirectory)dist\\**\\*\" />", modernProject, StringComparison.Ordinal);
        Assert.Contains("WebCode\\%(NoiraWebClientDist.RecursiveDir)%(NoiraWebClientDist.Filename)%(NoiraWebClientDist.Extension)", modernProject, StringComparison.Ordinal);
        Assert.Contains("Condition=\"'$(NoiraBuildWebClient)' == 'true'\">", modernProject, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Condition=\"'$(NoiraBuildWebClient)' == 'true' and Exists('$(NoiraWebClientDirectory)dist\\index.html')\"",
            modernProject,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Project_Uses_VS2026_Cpp_Toolset_And_Modern_Windows_Sdk()
    {
        var nativeProject = ReadRepositoryFile("src", "NoiraPlayer.Native", "NoiraPlayer.Native.vcxproj");

        Assert.Contains("<MinimumVisualStudioVersion>18.0</MinimumVisualStudioVersion>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<PlatformToolset>v145</PlatformToolset>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<WindowsTargetPlatformVersion>10.0.26100.0</WindowsTargetPlatformVersion>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<WindowsTargetPlatformMinVersion>10.0.19041.0</WindowsTargetPlatformMinVersion>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<LanguageStandard>stdcpp20</LanguageStandard>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<CompileAsWinRT>false</CompileAsWinRT>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<CppWinRTEnableLegacyCoroutines>false</CppWinRTEnableLegacyCoroutines>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("/utf-8", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<PlatformToolset>v143</PlatformToolset>", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<MinimumVisualStudioVersion>17.0</MinimumVisualStudioVersion>", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<LanguageStandard>stdcpp17</LanguageStandard>", nativeProject, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Project_Uses_Modern_CppWinRT_And_Current_Ffmpeg_Packages()
    {
        var nativeProject = ReadRepositoryFile("src", "NoiraPlayer.Native", "NoiraPlayer.Native.vcxproj");
        var packagesConfig = ReadRepositoryFile("src", "NoiraPlayer.Native", "packages.config");

        Assert.Contains("<package id=\"Microsoft.Windows.CppWinRT\" version=\"3.0.260520.1\" targetFramework=\"native\" />", packagesConfig, StringComparison.Ordinal);
        Assert.Contains("<package id=\"FFmpegInteropX.UWP.FFmpeg\" version=\"8.1.2\" targetFramework=\"native\" />", packagesConfig, StringComparison.Ordinal);
        Assert.Contains("packages\\Microsoft.Windows.CppWinRT.3.0.260520.1\\build\\native\\Microsoft.Windows.CppWinRT.props", nativeProject, StringComparison.Ordinal);
        Assert.Contains("packages\\Microsoft.Windows.CppWinRT.3.0.260520.1\\build\\native\\Microsoft.Windows.CppWinRT.targets", nativeProject, StringComparison.Ordinal);
        Assert.Contains("packages\\FFmpegInteropX.UWP.FFmpeg.8.1.2\\build\\native\\FFmpegInteropX.UWP.FFmpeg.targets", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Windows.CppWinRT.2.0.220531.1", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("version=\"2.0.220531.1\"", packagesConfig, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        return File.ReadAllText(RepositoryPath(segments));
    }

    private static string RepositoryPath(params string[] segments)
    {
        return Path.Combine(FindRepositoryRoot(), Path.Combine(segments));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NoiraPlayer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
