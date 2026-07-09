using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using NoiraPlayer.App.Navigation;
using NoiraPlayer.App.Services;
using NoiraPlayer.App.Storage;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Input;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NoiraPlayer.App.Views
{
    public sealed partial class LiveTvPage : Page, ITvContentFocusTarget
    {
        private const int ChannelLimit = 80;
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private readonly List<Button> _channelButtons = new List<Button>();
        private Button? _firstChannelButton;
        private Button? _unsupportedReturnFocusTarget;
        private LiveTvNavigationRequest? _request;
        private readonly Dictionary<string, string> _liveProgramArtworkUris =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public LiveTvPage()
        {
            InitializeComponent();
            MatteButtonFocusVisuals.PrepareCommandButton(FallbackRetryButton);
            MatteButtonFocusVisuals.PrepareCommandButton(UnsupportedCloseButton);
            AddHandler(KeyDownEvent, new KeyEventHandler(LiveTvPage_OnKeyDown), true);
            Loaded += LiveTvPage_OnLoaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _request = e.Parameter as LiveTvNavigationRequest;
        }

        private async void LiveTvPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= LiveTvPage_OnLoaded;
            if (_request != null && !string.IsNullOrWhiteSpace(_request.UnsupportedChannelName))
            {
                StatusBlock.Text = "Browse-only preview";
                ShowUnsupportedPanel(_request.UnsupportedChannelName);
                return;
            }

            await LoadLiveTvAsync();
        }

        public bool FocusDefaultContent()
        {
            if (UnsupportedPanel.Visibility == Visibility.Visible)
            {
                return UnsupportedCloseButton.Focus(FocusState.Keyboard);
            }

            if (FallbackPanel.Visibility == Visibility.Visible)
            {
                return FallbackRetryButton.Focus(FocusState.Keyboard);
            }

            if (_firstChannelButton != null && _firstChannelButton.Focus(FocusState.Keyboard))
            {
                return true;
            }

            return RefreshButton.Focus(FocusState.Keyboard);
        }

        private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            await LoadLiveTvAsync();
        }

        private async System.Threading.Tasks.Task LoadLiveTvAsync()
        {
            FallbackPanel.Visibility = Visibility.Collapsed;
            UnsupportedPanel.Visibility = Visibility.Collapsed;
            ChannelsPanel.Children.Clear();
            _channelButtons.Clear();
            _firstChannelButton = null;
            _unsupportedReturnFocusTarget = null;
            _liveProgramArtworkUris.Clear();
            StatusBlock.Text = "Loading channels";
            ClearPreviewArtwork();
            PreviewTitleBlock.Text = "Select a channel";
            PreviewBodyBlock.Text = "Live TV channels and current programs appear here when the server exposes them.";

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (session == null)
                {
                    ShowFallback("Sign in first", "Open Settings or restart the app after signing in.");
                    return;
                }

                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    var info = await client.GetLiveTvInfoAsync(session);
                    if (!IsLiveTvAvailableForUser(info, session))
                    {
                        ShowFallback("Live TV unavailable", "This server has not enabled Live TV for the current user.");
                        return;
                    }

                    var channels = await client.GetLiveTvChannelsAsync(session, ChannelLimit);
                    if (channels.Count == 0)
                    {
                        ShowFallback("No Live TV channels", "This server did not return any channels.");
                        return;
                    }

                    RenderChannels(session, client, channels);
                    StatusBlock.Text = channels.Count == 1 ? "1 channel" : channels.Count + " channels";
                    FocusDefaultContent();
                }
            }
            catch
            {
                ShowFallback("Live TV unavailable", "This server did not return Live TV channels.");
            }
        }

        private static bool IsLiveTvAvailableForUser(EmbyLiveTvInfo info, EmbySession session)
        {
            if (!info.IsEnabled)
            {
                return false;
            }

            return info.EnabledUserIds.Count == 0 ||
                info.EnabledUserIds.Any(userId => string.Equals(userId, session.UserId, StringComparison.OrdinalIgnoreCase));
        }

        private void RenderChannels(
            EmbySession session,
            EmbyApiClient client,
            IReadOnlyList<EmbyLiveTvChannel> channels)
        {
            _channelButtons.Clear();
            _liveProgramArtworkUris.Clear();
            foreach (var channel in channels)
            {
                CacheLiveProgramArtworkUri(session, client, channel);
                var button = CreateChannelButton(session, client, channel);
                if (_firstChannelButton == null)
                {
                    _firstChannelButton = button;
                    UpdatePreview(channel);
                }

                ChannelsPanel.Children.Add(button);
                _channelButtons.Add(button);
            }
        }

        private Button CreateChannelButton(
            EmbySession session,
            EmbyApiClient client,
            EmbyLiveTvChannel channel)
        {
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["TvListButtonStyle"],
                Tag = channel,
                Content = CreateChannelButtonContent(CreateChannelLogoFrame(session, client, channel), channel)
            };
            MatteButtonFocusVisuals.PrepareListButton(button);
            AutomationProperties.SetName(button, "Channel " + CreateChannelTitle(channel));
            button.GotFocus += (sender, args) => UpdatePreview(channel);
            button.Click += ChannelButton_OnClick;
            return button;
        }

        private Border CreateChannelLogoFrame(
            EmbySession session,
            EmbyApiClient client,
            EmbyLiveTvChannel channel)
        {
            var logoFrame = new Border
            {
                Width = 76,
                Height = 48,
                Background = BrushResource("AppRaisedSurfaceBrush"),
                BorderBrush = BrushResource("AppHairlineBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };

            if (!string.IsNullOrWhiteSpace(channel.PrimaryImageTag))
            {
                logoFrame.Child = new Image
                {
                    Stretch = Stretch.Uniform,
                    Source = new BitmapImage(new Uri(client.GetImageUrl(session, channel.Id, "Primary", 180)))
                };
            }
            else
            {
                logoFrame.Child = new SymbolIcon
                {
                    Symbol = Symbol.World,
                    Foreground = BrushResource("AppMutedTextBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            return logoFrame;
        }

        private UIElement CreateChannelButtonContent(
            FrameworkElement logoFrame,
            EmbyLiveTvChannel channel)
        {
            var grid = new Grid
            {
                ColumnSpacing = 16
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(logoFrame, 0);
            grid.Children.Add(logoFrame);

            var textStack = new StackPanel
            {
                Spacing = 3,
                VerticalAlignment = VerticalAlignment.Center
            };
            textStack.Children.Add(new TextBlock
            {
                Text = CreateChannelTitle(channel),
                Style = (Style)Application.Current.Resources["TvBodyValueTextStyle"],
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            textStack.Children.Add(new TextBlock
            {
                Text = CreateProgramLine(channel.CurrentProgram),
                Style = (Style)Application.Current.Resources["TvMutedBodyTextStyle"],
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);
            return grid;
        }

        private void ChannelButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            _unsupportedReturnFocusTarget = sender as Button;
            var channel = button == null ? null : button.Tag as EmbyLiveTvChannel;
            var channelName = channel == null || string.IsNullOrWhiteSpace(channel.Name) ? "this channel" : channel.Name;
            ShowUnsupportedPanel(channelName);
        }

        private void ShowUnsupportedPanel(string channelName)
        {
            UnsupportedTitleBlock.Text = "Live TV playback unavailable";
            UnsupportedBodyBlock.Text = "Browsing works, but this build does not open live streams yet: " + channelName + ".";
            UnsupportedPanel.Visibility = Visibility.Visible;
            UnsupportedCloseButton.Focus(FocusState.Keyboard);
        }

        private void UnsupportedCloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            CloseUnsupportedPanel();
        }

        private void LiveTvPage_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (TransientLayerInputPolicy.ShouldDismiss(
                UnsupportedPanel.Visibility == Visibility.Visible,
                IsBackKey(e.Key)))
            {
                e.Handled = true;
                CloseUnsupportedPanel();
                return;
            }

            if (UnsupportedPanel.Visibility == Visibility.Visible)
            {
                return;
            }

            if ((IsDownKey(e.Key) || IsUpKey(e.Key)) &&
                TryMoveWithinChannelList(e.Key))
            {
                e.Handled = true;
            }
        }

        private bool TryMoveWithinChannelList(VirtualKey key)
        {
            var focusedButton = FocusManager.GetFocusedElement() as Button;
            if (focusedButton == null)
            {
                return false;
            }

            var currentIndex = IndexOfButton(_channelButtons, focusedButton);
            var targetIndex = MusicListFocusPolicy.GetVerticalTargetIndex(
                currentIndex,
                _channelButtons.Count,
                IsDownKey(key),
                IsUpKey(key));
            return targetIndex.HasValue &&
                _channelButtons[targetIndex.Value].Focus(FocusState.Keyboard);
        }

        private static int IndexOfButton(IReadOnlyList<Button> buttons, Button focusedButton)
        {
            for (var index = 0; index < buttons.Count; index++)
            {
                if (buttons[index] == focusedButton)
                {
                    return index;
                }
            }

            return -1;
        }

        private void CloseUnsupportedPanel()
        {
            UnsupportedPanel.Visibility = Visibility.Collapsed;
            if (FocusUnsupportedReturnTarget())
            {
                return;
            }

            FocusDefaultContent();
        }

        private bool FocusUnsupportedReturnTarget()
        {
            return _unsupportedReturnFocusTarget != null &&
                _unsupportedReturnFocusTarget.Focus(FocusState.Keyboard);
        }

        private void ShowFallback(string title, string body)
        {
            StatusBlock.Text = "Needs attention";
            FallbackTitleBlock.Text = title;
            FallbackBodyBlock.Text = body;
            FallbackPanel.Visibility = Visibility.Visible;
            FallbackRetryButton.Focus(FocusState.Keyboard);
        }

        private void UpdatePreview(EmbyLiveTvChannel channel)
        {
            UpdatePreviewArtwork(channel);
            PreviewTitleBlock.Text = CreateChannelTitle(channel);
            PreviewBodyBlock.Text = CreateProgramDescription(channel.CurrentProgram);
        }

        private void UpdatePreviewArtwork(EmbyLiveTvChannel channel)
        {
            var source = CreateProgramArtworkImageSource(channel);
            if (source == null)
            {
                ClearPreviewArtwork();
                return;
            }

            PreviewArtworkImage.Source = source;
            PreviewArtworkFrame.Visibility = Visibility.Visible;
        }

        private void ClearPreviewArtwork()
        {
            PreviewArtworkImage.Source = null;
            PreviewArtworkFrame.Visibility = Visibility.Collapsed;
        }

        private BitmapImage? CreateProgramArtworkImageSource(EmbyLiveTvChannel channel)
        {
            var program = channel.CurrentProgram;
            if (program == null || string.IsNullOrWhiteSpace(program.Id))
            {
                return null;
            }

            return _liveProgramArtworkUris.TryGetValue(channel.Id, out var uri) &&
                !string.IsNullOrWhiteSpace(uri)
                ? new BitmapImage(new Uri(uri))
                : null;
        }

        private void CacheLiveProgramArtworkUri(
            EmbySession session,
            EmbyApiClient client,
            EmbyLiveTvChannel channel)
        {
            var program = channel.CurrentProgram;
            if (program == null || string.IsNullOrWhiteSpace(program.Id))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(program.ThumbImageTag))
            {
                _liveProgramArtworkUris[channel.Id] = client.GetImageUrl(session, program.Id, "Thumb", 720);
                return;
            }

            if (!string.IsNullOrWhiteSpace(program.BackdropImageTag))
            {
                _liveProgramArtworkUris[channel.Id] = client.GetImageUrl(session, program.Id, "Backdrop", 720);
                return;
            }

            if (!string.IsNullOrWhiteSpace(program.PrimaryImageTag))
            {
                _liveProgramArtworkUris[channel.Id] = client.GetImageUrl(session, program.Id, "Primary", 720);
            }
        }

        private static string CreateChannelTitle(EmbyLiveTvChannel channel)
        {
            return string.IsNullOrWhiteSpace(channel.Number)
                ? channel.Name
                : channel.Number + "  " + channel.Name;
        }

        private static string CreateProgramLine(EmbyLiveTvProgram? program)
        {
            if (program == null || string.IsNullOrWhiteSpace(program.Name))
            {
                return "No current program";
            }

            return string.IsNullOrWhiteSpace(program.EpisodeTitle)
                ? program.Name
                : program.Name + " - " + program.EpisodeTitle;
        }

        private static string CreateProgramDescription(EmbyLiveTvProgram? program)
        {
            if (program == null || string.IsNullOrWhiteSpace(program.Name))
            {
                return "No current program was returned for this channel.";
            }

            var parts = new List<string> { CreateProgramLine(program) };
            var timeRange = CreateTimeRange(program);
            if (!string.IsNullOrWhiteSpace(timeRange))
            {
                parts.Add(timeRange);
            }

            if (!string.IsNullOrWhiteSpace(program.Overview))
            {
                parts.Add(program.Overview);
            }

            return string.Join(Environment.NewLine + Environment.NewLine, parts);
        }

        private static string CreateTimeRange(EmbyLiveTvProgram program)
        {
            if (program.StartDate == default(DateTimeOffset) || program.EndDate == default(DateTimeOffset))
            {
                return "";
            }

            var start = program.StartDate.ToLocalTime().ToString("HH:mm");
            var end = program.EndDate.ToLocalTime().ToString("HH:mm");
            return start + " - " + end;
        }

        private static bool IsBackKey(VirtualKey key)
        {
            return key == VirtualKey.GamepadB ||
                key == VirtualKey.Escape ||
                key == VirtualKey.GoBack;
        }

        private static bool IsDownKey(VirtualKey key)
        {
            return key == VirtualKey.Down ||
                key == VirtualKey.GamepadDPadDown ||
                key == VirtualKey.GamepadLeftThumbstickDown;
        }

        private static bool IsUpKey(VirtualKey key)
        {
            return key == VirtualKey.Up ||
                key == VirtualKey.GamepadDPadUp ||
                key == VirtualKey.GamepadLeftThumbstickUp;
        }

        private static Brush BrushResource(string key)
        {
            return (Brush)Application.Current.Resources[key];
        }
    }
}
