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
        private EmbyApiClient? _client;
        private EmbyMediaItem? _heroItem;
        private bool _isUnloaded;
        private int _loadGeneration;

        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_OnLoaded;
            Unloaded += HomePage_OnUnloaded;
        }

        private async void HomePage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HomePage_OnLoaded;
            await LoadHomeAsync();
        }

        private void HomePage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            _loadGeneration++;
        }

        private async void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            await LoadHomeAsync();
        }

        private void MoviesLibrary_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LibraryPage), new LibraryNavigationRequest("Movies", "movies", "Movie"));
        }

        private void TvLibrary_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LibraryPage), new LibraryNavigationRequest("TV", "tvshows", "Series"));
        }

        private void HeroPlay_OnClick(object sender, RoutedEventArgs e)
        {
            var item = _heroItem;
            if (item == null || !CanPlay(item))
            {
                return;
            }

            var itemName = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            var startTicks = item.UserData == null ? 0 : item.UserData.PlaybackPositionTicks;
            Frame.Navigate(typeof(PlaybackPage), new PlaybackLaunchRequest(item.Id, itemName, startTicks));
        }

        private void HeroDetails_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateToDetails(_heroItem);
        }

        private async Task LoadHomeAsync()
        {
            var loadGeneration = ++_loadGeneration;

            RefreshButton.IsEnabled = false;
            StatusBlock.Text = "Loading...";
            RowsPanel.Children.Clear();
            ClearHero();

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                _session = session;
                if (session == null)
                {
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    var continueItems = await client.GetResumeItemsAsync(session, 20);
                    var latestItems = await client.GetLatestItemsAsync(session);

                    if (!CanApplyLoad(loadGeneration))
                    {
                        return;
                    }

                    _client = client;
                    RenderHome(continueItems, latestItems);
                }
            }
            catch
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                ClearHero();
                RowsPanel.Children.Clear();
                StatusBlock.Text = "Unable to load home.";
            }
            finally
            {
                if (CanApplyLoad(loadGeneration))
                {
                    RefreshButton.IsEnabled = true;
                }
            }
        }

        private void RenderHome(IReadOnlyList<EmbyMediaItem> continueItems, IReadOnlyList<EmbyMediaItem> latestItems)
        {
            RowsPanel.Children.Clear();

            EmbyMediaItem? heroItem = null;
            if (continueItems != null && continueItems.Count > 0)
            {
                heroItem = continueItems[0];
            }
            else
            {
                heroItem = FindFirstPlayableItem(latestItems);
                if (heroItem == null && latestItems != null && latestItems.Count > 0)
                {
                    heroItem = latestItems[0];
                }
            }

            if (heroItem == null)
            {
                ClearHero();
                StatusBlock.Text = "No playable items found.";
                return;
            }

            RenderHero(heroItem);
            StatusBlock.Text = "";

            AddRow("Continue watching", continueItems);
            AddRow("Latest", latestItems);
        }

        private void RenderHero(EmbyMediaItem item)
        {
            _heroItem = item;
            HeroTitleBlock.Text = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            HeroMetaBlock.Text = CreateMeta(item);
            HeroPlayButton.IsEnabled = CanPlay(item);
            HeroDetailsButton.IsEnabled = true;
            HeroPosterImage.Source = null;
            HeroPosterFallbackBlock.Visibility = Visibility.Visible;

            if (_client != null && _session != null && !string.IsNullOrWhiteSpace(item.PrimaryImageTag))
            {
                HeroPosterImage.Source = new BitmapImage(new Uri(_client.GetImageUrl(_session, item.Id, "Primary", 520)));
                HeroPosterFallbackBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void ClearHero()
        {
            _heroItem = null;
            HeroTitleBlock.Text = "Nothing queued yet";
            HeroMetaBlock.Text = "Refresh after signing in to load your Emby home screen.";
            HeroPosterImage.Source = null;
            HeroPosterFallbackBlock.Visibility = Visibility.Visible;
            HeroPlayButton.IsEnabled = false;
            HeroDetailsButton.IsEnabled = false;
        }

        private void AddRow(string title, IReadOnlyList<EmbyMediaItem>? items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            var section = new StackPanel
            {
                Spacing = 14
            };

            section.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 28,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            });

            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollMode = ScrollMode.Disabled
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 18
            };

            foreach (var item in items)
            {
                panel.Children.Add(CreateItemButton(item));
            }

            scroller.Content = panel;
            section.Children.Add(scroller);
            RowsPanel.Children.Add(section);
        }

        private Button CreateItemButton(EmbyMediaItem item)
        {
            var button = new Button
            {
                Width = 220,
                Height = 310,
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

            if (_client != null && _session != null && !string.IsNullOrWhiteSpace(item.PrimaryImageTag))
            {
                root.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(_client.GetImageUrl(_session, item.Id, "Primary", 420))),
                    Stretch = Stretch.UniformToFill
                };
            }

            var overlay = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(225, 0, 0, 0)),
                Padding = new Thickness(16, 14, 16, 14)
            };

            overlay.Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
                        FontSize = 20,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 2
                    },
                    new TextBlock
                    {
                        Text = CreateMeta(item),
                        FontSize = 15,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 183, 198, 216)),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    }
                }
            };

            root.Children.Add(overlay);
            button.Content = root;
            return button;
        }

        private void ItemButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button == null ? null : button.Tag as EmbyMediaItem;
            NavigateToDetails(item);
        }

        private void NavigateToDetails(EmbyMediaItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            var itemName = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(item.Id, itemName));
        }

        private bool CanApplyLoad(int loadGeneration)
        {
            return !_isUnloaded && loadGeneration == _loadGeneration;
        }

        private static EmbyMediaItem? FindFirstPlayableItem(IReadOnlyList<EmbyMediaItem>? items)
        {
            if (items == null)
            {
                return null;
            }

            foreach (var item in items)
            {
                if (CanPlay(item))
                {
                    return item;
                }
            }

            return null;
        }

        private static bool CanPlay(EmbyMediaItem? item)
        {
            return item != null
                && !string.IsNullOrWhiteSpace(item.Id)
                && (string.Equals(item.Type, "Movie", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase));
        }

        private static string CreateMeta(EmbyMediaItem item)
        {
            var meta = string.IsNullOrWhiteSpace(item.Type) ? "Item" : item.Type;
            if (item.ProductionYear.HasValue)
            {
                meta += " / " + item.ProductionYear.Value;
            }

            if (item.RunTimeTicks.HasValue && item.RunTimeTicks.Value > 0)
            {
                var runtime = TimeSpan.FromTicks(item.RunTimeTicks.Value);
                if (runtime.TotalHours >= 1)
                {
                    meta += " / " + (int)runtime.TotalHours + "h " + runtime.Minutes + "m";
                }
                else
                {
                    meta += " / " + Math.Max(1, runtime.Minutes) + "m";
                }
            }

            if (item.UserData != null && item.UserData.PlaybackPositionTicks > 0)
            {
                meta += " / Resume";
            }

            return meta;
        }
    }
}
