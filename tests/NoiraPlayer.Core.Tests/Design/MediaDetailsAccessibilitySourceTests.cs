using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class MediaDetailsAccessibilitySourceTests
{
    [Fact]
    public void Details_Selected_Version_State_Does_Not_ReUse_Focus_Border_Color()
    {
        var root = FindRepositoryRoot();
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.DoesNotContain("SourceSelectionMarker", detailsPageSource);
        Assert.DoesNotContain("isSelected ? \"AppAccentBrush\" : \"AppHairlineBrush\"", detailsPageSource);
        Assert.Contains("button.BorderBrush = (Brush)Application.Current.Resources[\"AppTransparentBrush\"];", detailsPageSource);
    }

    [Fact]
    public void Details_Renders_Metadata_Facet_Chips_That_Open_Library_Browse()
    {
        var root = FindRepositoryRoot();
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));
        var requestSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Navigation",
            "LibraryNavigationRequest.cs"));

        Assert.Contains("MetadataSection", detailsXaml);
        Assert.Contains("MetadataPanel", detailsXaml);
        Assert.Contains("RenderMetadataFacetRail", detailsPageSource);
        Assert.Contains("CreateMetadataFacetButton", detailsPageSource);
        Assert.Contains("MetadataFacet_OnClick", detailsPageSource);
        Assert.Contains("FocusFirstButton(MetadataPanel, FocusState.Keyboard)", detailsPageSource);
        Assert.Contains("new LibraryNavigationQuery(genres:", detailsPageSource);
        Assert.Contains("new LibraryNavigationQuery(studioIds:", detailsPageSource);
        Assert.Contains("new LibraryNavigationQuery(tags:", detailsPageSource);
        Assert.Contains("_restoreMetadataFacetKey", detailsPageSource);
        Assert.Contains("e.NavigationMode == NavigationMode.Back", detailsPageSource);
        Assert.Contains("FocusMetadataFacetWhenReadyAsync()", detailsPageSource);
        Assert.Contains("FocusMetadataFacetByKey", detailsPageSource);
        Assert.Contains("Genres", requestSource);
        Assert.Contains("StudioIds", requestSource);
        Assert.Contains("Tags", requestSource);
    }

    [Fact]
    public void Details_Metadata_Facet_Restore_Survives_Backstack_Page_Recreation()
    {
        var root = FindRepositoryRoot();
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("s_pendingMetadataFacetRestoreKeys", detailsPageSource);
        Assert.Contains("ResolveMetadataRestoreItemId(e.Parameter)", detailsPageSource);
        Assert.Contains("ConsumeMetadataFacetRestoreKey(restoreItemId)", detailsPageSource);
        Assert.Contains("QueueMetadataFacetRestore(facet)", detailsPageSource);
        Assert.Contains("FocusDefaultContent();", detailsPageSource);
        Assert.Contains("string.IsNullOrWhiteSpace(_restoreMetadataFacetKey)", detailsPageSource);
    }

    [Fact]
    public void Details_Visual_System_Uses_Atmosphere_Zone_Instead_Of_Visible_Poster_Viewer()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "App.xaml"));
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("DetailsAtmosphereZone", detailsXaml);
        Assert.Contains("AtmosphereImage", detailsXaml);
        Assert.Contains("DetailsAtmosphereFallback", detailsXaml);
        Assert.Contains("EmbyArtworkPolicy.SelectHeroArtwork(item, 1920)", detailsSource);
        Assert.Contains("AtmosphereImage.Source = new BitmapImage", detailsSource);
        Assert.Contains("ApplyDetailsAtmosphereTreatment(atmosphereArtwork.ImageType)", detailsSource);
        Assert.DoesNotContain("x:Name=\"PosterImage\"", detailsXaml);
        Assert.DoesNotContain("x:Name=\"PosterFallbackBlock\"", detailsXaml);
        Assert.DoesNotContain("PosterImage.Source", detailsSource);
    }

    [Fact]
    public void A3_Details_Atmosphere_And_Content_Order_Read_As_Cinematic_Details()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "App.xaml"));
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        var atmosphereZone = SliceFrom(detailsXaml, "x:Name=\"DetailsAtmosphereZone\"", "x:Name=\"AtmosphereImage\"");
        Assert.Contains("Grid.ColumnSpan=\"2\"", atmosphereZone);
        Assert.DoesNotContain("Grid.Column=\"1\"", atmosphereZone);
        Assert.DoesNotContain("Canvas.ZIndex=\"1\"", atmosphereZone);

        var detailsScroll = SliceFrom(detailsXaml, "<ScrollViewer", "VerticalScrollBarVisibility=\"Auto\">");
        Assert.Contains("Canvas.ZIndex=\"2\"", detailsScroll);

        var atmosphereImage = SliceFrom(detailsXaml, "x:Name=\"AtmosphereImage\"", "<Border");
        Assert.Contains("Opacity=\"0.9\"", atmosphereImage);

        Assert.Contains("<Thickness x:Key=\"TvDetailsContentMargin\">56,156,56,48</Thickness>", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvDetailsContentColumnWidth\">680</x:Double>", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvDetailsContentMaxWidth\">680</x:Double>", appXaml);
        Assert.Contains("Margin=\"{StaticResource TvDetailsContentMargin}\"", detailsXaml);

        var overviewBlock = SliceFrom(detailsXaml, "x:Name=\"OverviewBlock\"", "/>");
        Assert.Contains("FontSize=\"19\"", overviewBlock);
        Assert.Contains("LineHeight=\"27\"", overviewBlock);
        Assert.Contains("MaxLines=\"3\"", overviewBlock);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", overviewBlock);
        Assert.Contains("MaxWidth=\"640\"", overviewBlock);

        Assert.True(
            detailsXaml.IndexOf("x:Name=\"OverviewBlock\"", StringComparison.Ordinal) <
            detailsXaml.IndexOf("x:Name=\"DetailsActionPanel\"", StringComparison.Ordinal),
            "Overview must come before the details action island so Details reads as a movie page, not a form.");
        Assert.True(
            detailsXaml.IndexOf("x:Name=\"OverviewBlock\"", StringComparison.Ordinal) <
            detailsXaml.IndexOf("x:Name=\"DetailsSecondaryContentSpacer\"", StringComparison.Ordinal) &&
            detailsXaml.IndexOf("x:Name=\"DetailsSecondaryContentSpacer\"", StringComparison.Ordinal) <
            detailsXaml.IndexOf("x:Name=\"OrganizeSection\"", StringComparison.Ordinal),
            "Secondary details content should start below the first viewport decision surface.");
        Assert.True(
            detailsXaml.IndexOf("x:Name=\"DetailsActionPanel\"", StringComparison.Ordinal) <
            detailsXaml.IndexOf("x:Name=\"VersionsPanel\"", StringComparison.Ordinal),
            "Source/version controls should remain available but not dominate the first read.");

        var secondarySpacer = SliceFrom(detailsXaml, "x:Name=\"DetailsSecondaryContentSpacer\"", "x:Name=\"OrganizeSection\"");
        Assert.Contains("Height=\"1240\"", secondarySpacer);

        Assert.Contains("button.BorderBrush = BrushResource(\"AppTransparentBrush\");", detailsSource);
        Assert.DoesNotContain("button.BorderBrush = BrushResource(\"AppHairlineBrush\");", detailsSource);
    }

    [Fact]
    public void Details_Interactive_Controls_Use_Matte_Focus_Without_System_Focus_Rings()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "App.xaml"));
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("TvDetailsPrimaryActionButtonStyle", appXaml);
        Assert.Contains("TvDetailsActionButtonStyle", appXaml);
        Assert.Contains("TvDetailsSourceOptionButtonStyle", appXaml);
        Assert.Contains("TvDetailsDecisionChipButtonStyle", appXaml);
        Assert.Contains("TvDetailsAddToSheetOptionButtonStyle", appXaml);
        Assert.Contains("Style=\"{StaticResource TvDetailsPrimaryActionButtonStyle}\"", detailsXaml);
        Assert.Contains("Style=\"{StaticResource TvDetailsActionButtonStyle}\"", detailsXaml);
        Assert.Contains("Style=\"{StaticResource TvDetailsIconActionButtonStyle}\"", detailsXaml);
        Assert.DoesNotContain("UseSystemFocusVisuals=\"True\"", detailsXaml);
        Assert.Contains("UseSystemFocusVisuals = false", detailsSource);
        Assert.Contains("Style = (Style)Application.Current.Resources[\"TvDetailsDecisionChipButtonStyle\"]", detailsSource);
        Assert.Contains("Style = (Style)Application.Current.Resources[\"TvDetailsAddToSheetOptionButtonStyle\"]", detailsSource);
        Assert.Contains("ApplyDetailsCommandFocusTreatment", detailsSource);
    }

    [Fact]
    public void Details_Source_Selection_And_Secondary_Rails_Do_Not_Use_Green_Or_Bright_Frame_Focus()
    {
        var root = FindRepositoryRoot();
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("CreateSecondaryPosterRailButton", detailsSource);
        Assert.Contains("TvPosterGridCardButtonStyle", detailsSource);
        Assert.DoesNotContain("SourceSelectionMarker", detailsSource);
        Assert.DoesNotContain("marker.Background = isSelected ? BrushResource(\"AppSecondaryBrush\")", detailsSource);
        Assert.DoesNotContain("BorderBrush = isPreview ? BrushResource(\"AppAccentBrush\") : BrushResource(\"AppHairlineBrush\")", detailsSource);
        Assert.DoesNotContain("UseSystemFocusVisuals = true", detailsSource);
    }

    [Fact]
    public void Details_Source_And_Stream_Text_Constrain_Long_Emby_Labels()
    {
        var root = FindRepositoryRoot();
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        var audioDecisionChip = SliceFrom(detailsXaml, "x:Name=\"AudioDecisionChip\"", "x:Name=\"SubtitleDecisionChip\"");
        var subtitleDecisionChip = SliceFrom(detailsXaml, "x:Name=\"SubtitleDecisionChip\"", "x:Name=\"AddToSheetRoot\"");
        Assert.Contains("MinWidth=\"188\"", audioDecisionChip);
        Assert.Contains("MaxWidth=\"240\"", audioDecisionChip);
        Assert.Contains("Padding=\"14,8\"", audioDecisionChip);
        Assert.Contains("MinWidth=\"188\"", subtitleDecisionChip);
        Assert.Contains("MaxWidth=\"240\"", subtitleDecisionChip);
        Assert.Contains("Padding=\"14,8\"", subtitleDecisionChip);
        Assert.DoesNotContain("Text=\"Audio\"", audioDecisionChip);
        Assert.DoesNotContain("Text=\"Subtitles\"", subtitleDecisionChip);

        var audioSummaryBlock = SliceFrom(detailsXaml, "x:Name=\"AudioSummaryBlock\"", "x:Name=\"SubtitleDecisionChip\"");
        var subtitleSummaryBlock = SliceFrom(detailsXaml, "x:Name=\"SubtitleSummaryBlock\"", "x:Name=\"AddToSheetRoot\"");
        Assert.Contains("MaxLines=\"1\"", audioSummaryBlock);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", audioSummaryBlock);
        Assert.Contains("TextWrapping=\"NoWrap\"", audioSummaryBlock);
        Assert.Contains("MaxLines=\"1\"", subtitleSummaryBlock);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", subtitleSummaryBlock);
        Assert.Contains("TextWrapping=\"NoWrap\"", subtitleSummaryBlock);

        var sourceButton = SliceFrom(detailsSource, "private Button CreateSourceButton(EmbyMediaSource source, int sourceCount)", "private void UpdateSourceButtonStates()");
        Assert.Contains("MaxWidth = 320", sourceButton);
        Assert.Contains("HorizontalAlignment = HorizontalAlignment.Left", sourceButton);
        Assert.Contains("Text = CreateSourceDecisionLine(source, sourceCount)", sourceButton);
        Assert.DoesNotContain("CreateSourceDetails(source)", sourceButton);

        var sourceTitleBlock = SliceFrom(detailsSource, "Text = CreateSourceDecisionLine(source, sourceCount)", "button.Content = panel;");
        Assert.Contains("TextTrimming = TextTrimming.CharacterEllipsis", sourceTitleBlock);
        Assert.Contains("MaxLines = 1", sourceTitleBlock);
        Assert.DoesNotContain("TextWrapping = TextWrapping.Wrap", sourceTitleBlock);

        var sourceDecisionLine = SliceFrom(detailsSource, "private static string CreateSourceDecisionLine", "private static IReadOnlyList<string> CreateDetailsFactLabels");
        Assert.Contains("CreateSourceDecisionDockSummary(source)", sourceDecisionLine);
        Assert.DoesNotContain("CreateSourceDecisionSummary(source)", sourceDecisionLine);
        Assert.DoesNotContain("FormatBitrate", sourceDecisionLine);

        var dockSummary = SliceFrom(detailsSource, "private static string CreateSourceDecisionDockSummary", "private static IReadOnlyList<string> CreateDetailsFactLabels");
        Assert.DoesNotContain("FormatBitrate", dockSummary);

        var sourceCountLabel = SliceFrom(detailsSource, "private static string CreateSourceCountLabel", "private static string CreateSourceDetails");
        Assert.Contains("sourceCount <= 1 ? \"Version\" : sourceCount + \" versions\"", sourceCountLabel);
        Assert.DoesNotContain("\"Source", sourceCountLabel);
    }

    [Fact]
    public void A3_Details_Playback_Controls_Are_Compact_Low_Decision_Dock()
    {
        var root = FindRepositoryRoot();
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        var detailsScrollIndex = detailsXaml.IndexOf("<ScrollViewer", StringComparison.Ordinal);
        var decisionDockIndex = detailsXaml.IndexOf("x:Name=\"DetailsDecisionDock\"", StringComparison.Ordinal);
        Assert.True(detailsScrollIndex >= 0, "Details page must keep the secondary content scroller.");
        Assert.True(decisionDockIndex > detailsScrollIndex, "The playback decision dock should sit outside the scroll content.");

        var decisionDock = SliceFrom(detailsXaml, "x:Name=\"DetailsDecisionDock\"", "x:Name=\"AddToSheetRoot\"");
        Assert.Contains("VerticalAlignment=\"Bottom\"", decisionDock);
        Assert.Contains("HorizontalAlignment=\"Left\"", decisionDock);
        Assert.Contains("Background=\"{StaticResource AppTransparentBrush}\"", decisionDock);
        Assert.Contains("BorderBrush=\"{StaticResource AppTransparentBrush}\"", decisionDock);
        Assert.Contains("BorderThickness=\"0\"", decisionDock);
        Assert.Contains("Padding=\"0\"", decisionDock);
        Assert.DoesNotContain("Background=\"{StaticResource AppPlaybackOverlayBrush}\"", decisionDock);
        Assert.DoesNotContain("MinWidth=\"{StaticResource TvDetailsContentColumnWidth}\"", decisionDock);
        Assert.Contains("MaxWidth=\"1120\"", decisionDock);
        Assert.Contains("x:Name=\"DetailsActionPanel\"", decisionDock);
        Assert.Contains("x:Name=\"PlaybackDecisionChipsPanel\"", decisionDock);
        Assert.DoesNotContain("<ColumnDefinition Width=\"*\" />", decisionDock);
        Assert.Contains("x:Name=\"VersionsPanel\"", decisionDock);
        Assert.Contains("HorizontalAlignment=\"Left\"", SliceFrom(decisionDock, "x:Name=\"PlaybackDecisionChipsPanel\"", "x:Name=\"VersionsPanel\""));
        Assert.Contains("Orientation=\"Horizontal\"", SliceFrom(decisionDock, "x:Name=\"VersionsPanel\"", "/>"));
        Assert.DoesNotContain("HorizontalScrollMode=\"Enabled\"", decisionDock);

        Assert.Contains("TvDetailsDecisionChipButtonStyle", detailsSource);
        Assert.DoesNotContain("Text = \"Versions\"", detailsSource);
    }

    [Fact]
    public void A3_Details_Title_And_Decision_Tiles_Read_As_Tv_Cinematic_Not_Toolbar()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "App.xaml"));
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        var titleBlock = SliceFrom(detailsXaml, "x:Name=\"TitleBlock\"", "x:Name=\"MetaBlock\"");
        Assert.Contains("FontSize=\"48\"", titleBlock);
        Assert.Contains("FontWeight=\"SemiBold\"", titleBlock);

        var primaryActionStyle = SliceFrom(appXaml, "x:Key=\"TvDetailsPrimaryActionButtonStyle\"", "</Style>");
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"76\" />", primaryActionStyle);
        Assert.Contains("<Setter Property=\"MinWidth\" Value=\"220\" />", primaryActionStyle);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"28,16\" />", primaryActionStyle);

        var actionStyle = SliceFrom(appXaml, "x:Key=\"TvDetailsActionButtonStyle\"", "</Style>");
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"66\" />", actionStyle);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"24,14\" />", actionStyle);

        var iconActionStyle = SliceFrom(appXaml, "x:Key=\"TvDetailsIconActionButtonStyle\"", "</Style>");
        Assert.Contains("<Setter Property=\"Width\" Value=\"66\" />", iconActionStyle);
        Assert.Contains("<Setter Property=\"Height\" Value=\"66\" />", iconActionStyle);

        var playButtonText = SliceFrom(detailsXaml, "x:Name=\"PlayButtonText\"", "/>");
        Assert.Contains("FontSize=\"20\"", playButtonText);
        var favoriteButtonText = SliceFrom(detailsXaml, "x:Name=\"FavoriteButtonText\"", "/>");
        Assert.Contains("FontSize=\"19\"", favoriteButtonText);
        var watchedButtonText = SliceFrom(detailsXaml, "x:Name=\"WatchedButtonText\"", "/>");
        Assert.Contains("FontSize=\"19\"", watchedButtonText);

        var decisionChipStyle = SliceFrom(appXaml, "x:Key=\"TvDetailsDecisionChipButtonStyle\"", "</Style>");
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"52\" />", decisionChipStyle);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"14,8\" />", decisionChipStyle);

        var audioDecisionChip = SliceFrom(detailsXaml, "x:Name=\"AudioDecisionChip\"", "x:Name=\"SubtitleDecisionChip\"");
        Assert.Contains("MinWidth=\"188\"", audioDecisionChip);
        Assert.Contains("MaxWidth=\"240\"", audioDecisionChip);
        Assert.Contains("Padding=\"14,8\"", audioDecisionChip);
        Assert.DoesNotContain("FontSize=\"13\"", audioDecisionChip);
        Assert.Contains("FontSize=\"16\"", audioDecisionChip);

        var subtitleDecisionChip = SliceFrom(detailsXaml, "x:Name=\"SubtitleDecisionChip\"", "x:Name=\"AddToSheetRoot\"");
        Assert.Contains("MinWidth=\"188\"", subtitleDecisionChip);
        Assert.Contains("MaxWidth=\"240\"", subtitleDecisionChip);
        Assert.Contains("Padding=\"14,8\"", subtitleDecisionChip);
        Assert.DoesNotContain("FontSize=\"13\"", subtitleDecisionChip);
        Assert.Contains("FontSize=\"16\"", subtitleDecisionChip);

        var sourceButton = SliceFrom(detailsSource, "private Button CreateSourceButton(EmbyMediaSource source, int sourceCount)", "private void UpdateSourceButtonStates()");
        Assert.Contains("MinWidth = 220", sourceButton);
        Assert.Contains("MaxWidth = 320", sourceButton);
        Assert.DoesNotContain("FontSize = 13", sourceButton);
        Assert.Contains("FontSize = 16", sourceButton);
    }

    [Fact]
    public void A3_Details_Decision_Tiles_Use_Material_Fill_Not_Hairline_Button_Frames()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "App.xaml"));
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("<Color x:Key=\"AppDetailsDecisionTileColor\">#B810161C</Color>", appXaml);
        Assert.Contains("<Color x:Key=\"AppDetailsDecisionTileSelectedColor\">#B8202832</Color>", appXaml);
        Assert.Contains("<Color x:Key=\"AppDetailsDecisionTileFocusedColor\">#D8202832</Color>", appXaml);
        Assert.Contains("<SolidColorBrush x:Key=\"AppDetailsDecisionTileBrush\" Color=\"{StaticResource AppDetailsDecisionTileColor}\" />", appXaml);
        Assert.Contains("<SolidColorBrush x:Key=\"AppDetailsDecisionTileSelectedBrush\" Color=\"{StaticResource AppDetailsDecisionTileSelectedColor}\" />", appXaml);
        Assert.Contains("<SolidColorBrush x:Key=\"AppDetailsDecisionTileFocusedBrush\" Color=\"{StaticResource AppDetailsDecisionTileFocusedColor}\" />", appXaml);

        var primaryActionStyle = SliceFrom(appXaml, "x:Key=\"TvDetailsPrimaryActionButtonStyle\"", "</Style>");
        Assert.Contains("<Setter Property=\"BorderBrush\" Value=\"{StaticResource AppTransparentBrush}\" />", primaryActionStyle);

        var actionStyle = SliceFrom(appXaml, "x:Key=\"TvDetailsActionButtonStyle\"", "</Style>");
        Assert.Contains("<Setter Property=\"Background\" Value=\"{StaticResource AppDetailsDecisionTileBrush}\" />", actionStyle);
        Assert.Contains("<Setter Property=\"BorderBrush\" Value=\"{StaticResource AppTransparentBrush}\" />", actionStyle);
        Assert.DoesNotContain("AppHairlineBrush", actionStyle);

        var iconActionStyle = SliceFrom(appXaml, "x:Key=\"TvDetailsIconActionButtonStyle\"", "</Style>");
        Assert.Contains("<Setter Property=\"Background\" Value=\"{StaticResource AppDetailsDecisionTileBrush}\" />", iconActionStyle);
        Assert.Contains("<Setter Property=\"BorderBrush\" Value=\"{StaticResource AppTransparentBrush}\" />", iconActionStyle);
        Assert.DoesNotContain("AppHairlineBrush", iconActionStyle);

        var decisionChipStyle = SliceFrom(appXaml, "x:Key=\"TvDetailsDecisionChipButtonStyle\"", "</Style>");
        Assert.Contains("<Setter Property=\"Background\" Value=\"{StaticResource AppDetailsDecisionTileBrush}\" />", decisionChipStyle);
        Assert.Contains("<Setter Property=\"BorderBrush\" Value=\"{StaticResource AppTransparentBrush}\" />", decisionChipStyle);

        var audioDecisionChip = SliceFrom(detailsXaml, "x:Name=\"AudioDecisionChip\"", "x:Name=\"SubtitleDecisionChip\"");
        var subtitleDecisionChip = SliceFrom(detailsXaml, "x:Name=\"SubtitleDecisionChip\"", "x:Name=\"AddToSheetRoot\"");
        Assert.Contains("Background=\"{StaticResource AppDetailsDecisionTileBrush}\"", audioDecisionChip);
        Assert.Contains("Background=\"{StaticResource AppDetailsDecisionTileBrush}\"", subtitleDecisionChip);

        var lostFocus = SliceFrom(detailsSource, "private void DetailsCommandButton_OnLostFocus", "protected override async void OnNavigatedTo");
        Assert.Contains("button.BorderBrush = BrushResource(\"AppTransparentBrush\");", lostFocus);
        Assert.Contains("button.Background = BrushResource(\"AppDetailsDecisionTileBrush\");", lostFocus);
        Assert.DoesNotContain("button.BorderBrush = BrushResource(\"AppActionBrush\");", lostFocus);
        Assert.DoesNotContain("button.Background = BrushResource(\"AppChromeBrush\");", lostFocus);

        var commandFocus = SliceFrom(detailsSource, "private void DetailsCommandButton_OnGotFocus", "private void DetailsCommandButton_OnLostFocus");
        Assert.Contains("button.Background = BrushResource(\"AppDetailsDecisionTileFocusedBrush\");", commandFocus);
        Assert.DoesNotContain("AppFocusedCardFillBrush", commandFocus);

        var sourceState = SliceFrom(detailsSource, "private void ApplySourceButtonState", "private void SourceButton_OnGotFocus");
        Assert.Contains("isSelected ? \"AppDetailsDecisionTileSelectedBrush\" : \"AppDetailsDecisionTileBrush\"", sourceState);
        Assert.DoesNotContain("AppChromePressedBrush", sourceState);
        Assert.DoesNotContain("AppChromeBrush", sourceState);

        var sourceFocus = SliceFrom(detailsSource, "private void SourceButton_OnGotFocus", "private void SourceButton_OnLostFocus");
        Assert.Contains("button.Background = BrushResource(\"AppDetailsDecisionTileFocusedBrush\");", sourceFocus);
        Assert.DoesNotContain("AppFocusedCardFillBrush", sourceFocus);
    }

    [Fact]
    public void A3_Details_Composition_Keeps_Info_And_Decisions_In_Cinematic_Band()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "App.xaml"));
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));

        Assert.Contains("<Thickness x:Key=\"TvDetailsContentMargin\">56,156,56,48</Thickness>", appXaml);
        Assert.Contains("<Thickness x:Key=\"TvDetailsDecisionDockMargin\">56,0,56,112</Thickness>", appXaml);
        Assert.Contains("Margin=\"{StaticResource TvDetailsContentMargin}\"", detailsXaml);

        var decisionDock = SliceFrom(detailsXaml, "x:Name=\"DetailsDecisionDock\"", "x:Name=\"AddToSheetRoot\"");
        Assert.Contains("VerticalAlignment=\"Bottom\"", decisionDock);
        Assert.Contains("Margin=\"{StaticResource TvDetailsDecisionDockMargin}\"", decisionDock);
        Assert.DoesNotContain("Margin=\"{StaticResource TvPageMargin}\"", decisionDock);
    }

    [Fact]
    public void A3_Details_First_Viewport_Uses_Passive_Facts_And_Credits_Not_Empty_Space()
    {
        var root = FindRepositoryRoot();
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("x:Name=\"DetailsFactChipsPanel\"", detailsXaml);
        Assert.Contains("x:Name=\"DetailsCreditsBlock\"", detailsXaml);
        Assert.True(
            detailsXaml.IndexOf("x:Name=\"MetaBlock\"", StringComparison.Ordinal) <
            detailsXaml.IndexOf("x:Name=\"DetailsFactChipsPanel\"", StringComparison.Ordinal) &&
            detailsXaml.IndexOf("x:Name=\"DetailsFactChipsPanel\"", StringComparison.Ordinal) <
            detailsXaml.IndexOf("x:Name=\"OverviewBlock\"", StringComparison.Ordinal),
            "Passive facts should sit between metadata and overview.");
        Assert.True(
            detailsXaml.IndexOf("x:Name=\"OverviewBlock\"", StringComparison.Ordinal) <
            detailsXaml.IndexOf("x:Name=\"DetailsCreditsBlock\"", StringComparison.Ordinal) &&
            detailsXaml.IndexOf("x:Name=\"DetailsCreditsBlock\"", StringComparison.Ordinal) <
            detailsXaml.IndexOf("x:Name=\"DetailsSecondaryContentSpacer\"", StringComparison.Ordinal),
            "Credits/genres should enrich the first viewport before secondary rails.");

        var factPanel = SliceFrom(detailsXaml, "x:Name=\"DetailsFactChipsPanel\"", "x:Name=\"OverviewBlock\"");
        Assert.Contains("Orientation=\"Horizontal\"", factPanel);
        Assert.Contains("MaxWidth=\"640\"", factPanel);
        Assert.Contains("Visibility=\"Collapsed\"", factPanel);
        Assert.DoesNotContain("Button", factPanel);

        var creditsBlock = SliceFrom(detailsXaml, "x:Name=\"DetailsCreditsBlock\"", "/>");
        Assert.Contains("MaxLines=\"2\"", creditsBlock);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", creditsBlock);

        Assert.Contains("RenderDetailsFirstReadFacts();", SliceFrom(detailsSource, "private void RenderItem()", "private async Task LoadDetailsAsync"));
        Assert.Contains("RenderDetailsFirstReadFacts();", SliceFrom(detailsSource, "private void RenderPlaybackInfo()", "private void AddEpisodeButton"));
        Assert.Contains("private void RenderDetailsFirstReadFacts()", detailsSource);
        Assert.Contains("CreateDetailsFactLabels(item, GetSelectedMediaSource())", detailsSource);
        Assert.Contains(".Take(5)", SliceFrom(detailsSource, "private void RenderDetailsFirstReadFacts()", "private Border CreateDetailsFactChip"));
        Assert.Contains("private static string CreateDetailsCreditsSummary(EmbyMediaItem item)", detailsSource);
        Assert.Contains("CreateDetailsDirectorSummary(item)", detailsSource);
        Assert.Contains("CreateDetailsGenreSummary(item)", detailsSource);
        Assert.Contains("CreateDetailsChannelLayoutFact(audio.ChannelLayout)", detailsSource);
        Assert.Contains("subtitle.Language.ToUpperInvariant() + \" subtitles\"", detailsSource);
    }

    [Fact]
    public void A3_Details_Source_Decision_Uses_Current_Source_Not_A_Version_List()
    {
        var root = FindRepositoryRoot();
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        var renderPlaybackInfo = SliceFrom(detailsSource, "private void RenderPlaybackInfo()", "private void AddEpisodeButton");
        Assert.Contains("var selectedSource = GetSelectedMediaSource();", renderPlaybackInfo);
        Assert.Contains("VersionsPanel.Children.Add(CreateSourceButton(selectedSource, _mediaSources.Count));", renderPlaybackInfo);
        Assert.Contains("AudioSummaryBlock.Text = CreateAudioDecisionSummary(selectedSource);", renderPlaybackInfo);
        Assert.Contains("SubtitleSummaryBlock.Text = CreateSubtitleDecisionSummary(selectedSource);", renderPlaybackInfo);
        Assert.DoesNotContain("foreach (var source in _mediaSources)", renderPlaybackInfo);

        var sourceButton = SliceFrom(detailsSource, "private Button CreateSourceButton(EmbyMediaSource source, int sourceCount)", "private void UpdateSourceButtonStates()");
        Assert.Contains("Text = CreateSourceDecisionLine(source, sourceCount)", sourceButton);
        Assert.DoesNotContain("Text = CreateSourceCountLabel(sourceCount)", sourceButton);
        Assert.DoesNotContain("Text = CreateSourceDecisionSummary(source)", sourceButton);
        Assert.DoesNotContain("SourceSelectionMarker", sourceButton);
        Assert.DoesNotContain("layout.ColumnDefinitions.Add(new ColumnDefinition", sourceButton);

        var clickHandler = SliceFrom(detailsSource, "private void SourceVersion_OnClick", "private void NavigateToPlayback");
        Assert.Contains("var sourceToSelect = ResolveClickedSourceVersion(source);", clickHandler);
        Assert.Contains("RenderPlaybackInfo();", clickHandler);
    }

    [Fact]
    public void A3_Details_Decision_Dock_Is_Anchored_To_Viewport_Not_Scroll_Content()
    {
        var root = FindRepositoryRoot();
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("x:Name=\"DetailsRoot\"", detailsXaml);
        var decisionDock = SliceFrom(detailsXaml, "x:Name=\"DetailsDecisionDock\"", "x:Name=\"AddToSheetRoot\"");
        Assert.Contains("VerticalAlignment=\"Bottom\"", decisionDock);
        Assert.Contains("HorizontalAlignment=\"Left\"", decisionDock);
        Assert.Contains("Margin=\"{StaticResource TvDetailsDecisionDockMargin}\"", decisionDock);
        Assert.DoesNotContain("MinWidth=\"{StaticResource TvDetailsContentColumnWidth}\"", decisionDock);
        Assert.DoesNotContain("Loaded += MediaDetailsPage_OnLoaded;", detailsSource);
        Assert.DoesNotContain("SizeChanged += MediaDetailsPage_OnSizeChanged;", detailsSource);
        Assert.DoesNotContain("DetailsDecisionDock.SizeChanged += DetailsDecisionDock_OnSizeChanged;", detailsSource);
        Assert.DoesNotContain("ConstrainDetailsRootToViewport();", detailsSource);
        Assert.DoesNotContain("PositionDetailsDecisionDockInViewport();", detailsSource);
        Assert.DoesNotContain("Window.Current.Bounds", detailsSource);
        Assert.DoesNotContain("DetailsRoot.Height = bounds.Height;", detailsSource);
        Assert.DoesNotContain("DetailsDecisionDockViewportTopCap", detailsSource);
        Assert.DoesNotContain("Math.Min(desiredTop", detailsSource);
        Assert.DoesNotContain("DetailsDecisionDock.VerticalAlignment = VerticalAlignment.Top;", detailsSource);
        Assert.DoesNotContain("DetailsDecisionDock.Margin = new Thickness(", detailsSource);
    }

    private static string SliceFrom(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing source marker " + startMarker);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, "Missing source marker " + endMarker);
        return source.Substring(start, end - start);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "Generate-AppIconAssets.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
