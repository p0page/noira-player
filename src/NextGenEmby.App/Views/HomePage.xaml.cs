using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace NextGenEmby.App.Views
{
    public sealed partial class HomePage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private EmbySession? _session;

        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_OnLoaded;
        }

        private async void HomePage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HomePage_OnLoaded;
            await LoadLatestItemsAsync();
        }

        private async Task LoadLatestItemsAsync()
        {
            StatusBlock.Text = "Loading...";
            LatestItemsPanel.Children.Clear();

            _session = await _sessionStore.LoadAsync();
            if (_session == null)
            {
                StatusBlock.Text = "Sign in first.";
                return;
            }

            try
            {
                using (var http = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(http, _session);
                    var items = await client.GetLatestItemsAsync(_session);
                    RenderLatestItems(client, _session, items);
                }
            }
            catch
            {
                StatusBlock.Text = "Unable to load library.";
            }
        }

        private void RenderLatestItems(
            EmbyApiClient client,
            EmbySession session,
            IReadOnlyList<EmbyMediaItem> items)
        {
            LatestItemsPanel.Children.Clear();
            if (items.Count == 0)
            {
                StatusBlock.Text = "No playable items found.";
                return;
            }

            StatusBlock.Text = "";
            foreach (var item in items)
            {
                LatestItemsPanel.Children.Add(CreateItemButton(client, session, item));
            }
        }

        private Button CreateItemButton(EmbyApiClient client, EmbySession session, EmbyMediaItem item)
        {
            var button = new Button
            {
                Width = 280,
                Height = 190,
                Padding = new Thickness(0),
                Tag = item,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                UseSystemFocusVisuals = true
            };
            button.Click += ItemButton_OnClick;

            var root = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 23, 34, 49))
            };

            if (!string.IsNullOrWhiteSpace(item.PrimaryImageTag))
            {
                root.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(client.GetImageUrl(session, item.Id, "Primary", 420))),
                    Stretch = Stretch.UniformToFill
                };
            }

            var overlay = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
                Padding = new Thickness(18, 14, 18, 14)
            };

            var textStack = new StackPanel
            {
                Spacing = 4
            };
            textStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
                FontSize = 21,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            });
            textStack.Children.Add(new TextBlock
            {
                Text = CreateSubtitle(item),
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 183, 198, 216)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            });

            overlay.Child = textStack;
            root.Children.Add(overlay);
            button.Content = root;
            return button;
        }

        private void ItemButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as EmbyMediaItem;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            Frame.Navigate(typeof(PlaybackPage), new PlaybackLaunchRequest(item.Id, item.Name));
        }

        private static string CreateSubtitle(EmbyMediaItem item)
        {
            if (item.ProductionYear.HasValue && !string.IsNullOrWhiteSpace(item.Type))
            {
                return item.Type + " · " + item.ProductionYear.Value;
            }

            return string.IsNullOrWhiteSpace(item.Type) ? "Item" : item.Type;
        }
    }
}
