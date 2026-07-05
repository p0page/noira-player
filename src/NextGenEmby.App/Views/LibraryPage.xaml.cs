using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class LibraryPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private LibraryNavigationRequest? _request;
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
            var focusFirstItem = false;
            var focusFallback = false;

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                if (session == null)
                {
                    ItemsGrid.Items.Clear();
                    StatusBlock.Text = "Sign in first.";
                    focusFallback = true;
                    return;
                }

                IReadOnlyList<LibraryGridItem> gridItems;
                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    var items = await client.GetItemsAsync(session, new EmbyItemsQuery
                    {
                        IncludeItemTypes = request.IncludeItemTypes,
                        Recursive = true,
                        SortBy = GetSelectedTag(SortBox, "SortName"),
                        SortOrder = "Ascending",
                        Filters = GetSelectedTag(FilterBox, ""),
                        Limit = 100
                    });
                    gridItems = CreateGridItems(session, client, items);
                }

                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                focusFirstItem = RenderItems(gridItems, loadGeneration);
                focusFallback = !focusFirstItem;
            }
            catch
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                ItemsGrid.Items.Clear();
                StatusBlock.Text = "Unable to load library.";
                focusFallback = true;
            }
            finally
            {
                if (CanApplyLoad(loadGeneration))
                {
                    RefreshButton.IsEnabled = true;
                    SortBox.IsEnabled = true;
                    FilterBox.IsEnabled = true;

                    if (focusFirstItem)
                    {
                        await FocusFirstItemAsync(loadGeneration);
                    }
                    else if (focusFallback)
                    {
                        await FocusFallbackControlAsync(loadGeneration);
                    }
                }
            }
        }

        private bool RenderItems(IReadOnlyList<LibraryGridItem> items, int loadGeneration)
        {
            if (!CanApplyLoad(loadGeneration))
            {
                return false;
            }

            ItemsGrid.Items.Clear();

            if (items == null || items.Count == 0)
            {
                StatusBlock.Text = "No items found.";
                return false;
            }

            foreach (var item in items)
            {
                ItemsGrid.Items.Add(item);
            }

            StatusBlock.Text = items.Count + " items";
            return true;
        }

        private static IReadOnlyList<LibraryGridItem> CreateGridItems(
            EmbySession session,
            EmbyApiClient client,
            IReadOnlyList<EmbyMediaItem> items)
        {
            var gridItems = new List<LibraryGridItem>();
            if (items == null)
            {
                return gridItems;
            }

            foreach (var item in items)
            {
                BitmapImage? imageSource = null;
                if (!string.IsNullOrWhiteSpace(item.PrimaryImageTag))
                {
                    imageSource = new BitmapImage(new Uri(client.GetImageUrl(session, item.Id, "Primary", 420)));
                }

                gridItems.Add(new LibraryGridItem(item, imageSource));
            }

            return gridItems;
        }

        private void ItemsGrid_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var gridItem = e.ClickedItem as LibraryGridItem;
            NavigateToDetails(gridItem == null ? null : gridItem.Item);
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

        private async Task FocusFirstItemAsync(int loadGeneration)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                var firstItem = ItemsGrid.ContainerFromIndex(0) as Control;
                if (firstItem != null)
                {
                    firstItem.Focus(FocusState.Programmatic);
                    return;
                }

                ItemsGrid.Focus(FocusState.Programmatic);
            });
        }

        private async Task FocusFallbackControlAsync(int loadGeneration)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                if (!RefreshButton.Focus(FocusState.Programmatic))
                {
                    SortBox.Focus(FocusState.Programmatic);
                }
            });
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

        public sealed class LibraryGridItem
        {
            public LibraryGridItem(EmbyMediaItem item, BitmapImage? imageSource)
            {
                Item = item;
                ImageSource = imageSource;
                Title = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
                Meta = CreateMeta(item);
            }

            public EmbyMediaItem Item { get; }

            public string Title { get; }

            public string Meta { get; }

            public BitmapImage? ImageSource { get; }
        }
    }
}
