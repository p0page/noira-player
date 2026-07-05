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
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class LibraryPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private LibraryNavigationRequest? _request;
        private EmbySession? _session;
        private EmbyApiClient? _client;
        private bool _isUnloaded;
        private bool _isNavigatingToDetails;
        private int _loadGeneration;

        public LibraryPage()
        {
            InitializeComponent();
            Unloaded += LibraryPage_OnUnloaded;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _isUnloaded = false;
            _isNavigatingToDetails = false;
            _request = e.Parameter as LibraryNavigationRequest;
            if (_request == null || string.IsNullOrWhiteSpace(_request.Title))
            {
                TitleBlock.Text = "Library";
                StatusBlock.Text = "Choose a library from Home.";
                ItemsGrid.Items.Clear();
                return;
            }

            TitleBlock.Text = _request.Title;
            await LoadLibraryAsync();
        }

        private void LibraryPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            _loadGeneration++;
        }

        private async void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            await LoadLibraryAsync();
        }

        private async void SortBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadLibraryAsync();
        }

        private async void FilterBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadLibraryAsync();
        }

        private async Task LoadLibraryAsync()
        {
            var request = _request;
            if (request == null)
            {
                return;
            }

            var loadGeneration = ++_loadGeneration;
            RefreshButton.IsEnabled = false;
            SortBox.IsEnabled = false;
            FilterBox.IsEnabled = false;
            StatusBlock.Text = "Loading...";

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
                    ItemsGrid.Items.Clear();
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                IReadOnlyList<EmbyMediaItem> items;
                EmbyApiClient client;
                using (var httpClient = new HttpClient())
                {
                    client = EmbyClientFactory.Create(httpClient, session);
                    items = await client.GetItemsAsync(session, new EmbyItemsQuery
                    {
                        IncludeItemTypes = request.IncludeItemTypes,
                        Recursive = true,
                        SortBy = GetSelectedTag(SortBox, "SortName"),
                        SortOrder = "Ascending",
                        Filters = GetSelectedTag(FilterBox, ""),
                        Limit = 100
                    });
                }

                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                _client = client;
                RenderItems(items);
            }
            catch
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                ItemsGrid.Items.Clear();
                StatusBlock.Text = "Unable to load library.";
            }
            finally
            {
                if (CanApplyLoad(loadGeneration))
                {
                    RefreshButton.IsEnabled = true;
                    SortBox.IsEnabled = true;
                    FilterBox.IsEnabled = true;
                }
            }
        }

        private void RenderItems(IReadOnlyList<EmbyMediaItem> items)
        {
            ItemsGrid.Items.Clear();

            if (items == null || items.Count == 0)
            {
                StatusBlock.Text = "No items found.";
                return;
            }

            foreach (var item in items)
            {
                ItemsGrid.Items.Add(CreatePosterButton(item));
            }

            StatusBlock.Text = items.Count + " items";
        }

        private Button CreatePosterButton(EmbyMediaItem item)
        {
            var button = new Button
            {
                Width = 210,
                Height = 320,
                MinWidth = 210,
                MinHeight = 320,
                Margin = new Thickness(0, 0, 18, 18),
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

            root.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(48, 0, 0, 0))
            });

            var overlay = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(224, 0, 0, 0)),
                Padding = new Thickness(14, 12, 14, 12)
            };

            overlay.Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
                        FontSize = 19,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 2,
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text = CreateMeta(item),
                        FontSize = 14,
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

        private void ItemsGrid_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var button = e.ClickedItem as Button;
            var item = button == null ? null : button.Tag as EmbyMediaItem;
            NavigateToDetails(item);
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

            if (_isNavigatingToDetails)
            {
                return;
            }

            _isNavigatingToDetails = true;
            var itemName = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(item.Id, itemName));
        }

        private bool CanApplyLoad(int loadGeneration)
        {
            return !_isUnloaded && loadGeneration == _loadGeneration;
        }

        private static string GetSelectedTag(ComboBox comboBox, string fallback)
        {
            var item = comboBox.SelectedItem as ComboBoxItem;
            var tag = item == null ? null : item.Tag as string;
            return tag ?? fallback;
        }

        private static string CreateMeta(EmbyMediaItem item)
        {
            var meta = string.IsNullOrWhiteSpace(item.Type) ? "Item" : item.Type;
            if (item.ProductionYear.HasValue)
            {
                meta += " / " + item.ProductionYear.Value;
            }

            if (item.UserData != null && item.UserData.PlaybackPositionTicks > 0)
            {
                meta += " / Resume";
            }

            return meta;
        }
    }
}
