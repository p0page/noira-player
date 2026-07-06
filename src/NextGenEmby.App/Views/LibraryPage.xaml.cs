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
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.System;

namespace NextGenEmby.App.Views
{
    public sealed partial class LibraryPage : Page, ITvContentFocusTarget
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private const double GridItemWidth = 168d;
        private const double GridItemTrailingMargin = 14d;
        private static readonly LibraryQueryOption[] SortOptions =
        {
            new LibraryQueryOption("Title", "SortName"),
            new LibraryQueryOption("Recently added", "DateCreated"),
            new LibraryQueryOption("Year", "ProductionYear")
        };
        private static readonly LibraryQueryOption[] FilterOptions =
        {
            new LibraryQueryOption("All", ""),
            new LibraryQueryOption("Unwatched", "IsUnplayed"),
            new LibraryQueryOption("Resumable", "IsResumable")
        };
        private LibraryNavigationRequest? _request;
        private bool _isUnloaded;
        private bool _isNavigatingToDetails;
        private bool _keyHandlerAttached;
        private int _sortIndex;
        private int _filterIndex;
        private int _loadGeneration;

        public LibraryPage()
        {
            InitializeComponent();
            AddHandler(KeyDownEvent, new KeyEventHandler(Page_OnKeyDown), true);
            Loaded += LibraryPage_OnLoaded;
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
            OptionsPanel.Visibility = IsSectionRequest(_request) ? Visibility.Collapsed : Visibility.Visible;
            await LoadLibraryAsync();
        }

        private void LibraryPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachLibraryKeyHandler();
        }

        private void LibraryPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachLibraryKeyHandler();
            _isUnloaded = true;
            _loadGeneration++;
        }

        private async void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            await LoadLibraryAsync();
        }

        private async void SortButton_OnClick(object sender, RoutedEventArgs e)
        {
            _sortIndex = (_sortIndex + 1) % SortOptions.Length;
            ApplyOptionLabels();
            await LoadLibraryAsync();
        }

        private async void FilterButton_OnClick(object sender, RoutedEventArgs e)
        {
            _filterIndex = (_filterIndex + 1) % FilterOptions.Length;
            ApplyOptionLabels();
            await LoadLibraryAsync();
        }

        private void Page_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = TryRouteLibraryDirectionalKey(e.Key) || e.Handled;
        }

        private void LibraryPage_OnCoreWindowKeyDown(CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            args.Handled = TryRouteLibraryDirectionalKey(args.VirtualKey) || args.Handled;
        }

        private void AttachLibraryKeyHandler()
        {
            if (_keyHandlerAttached)
            {
                return;
            }

            Window.Current.CoreWindow.KeyDown += LibraryPage_OnCoreWindowKeyDown;
            _keyHandlerAttached = true;
        }

        private void DetachLibraryKeyHandler()
        {
            if (!_keyHandlerAttached)
            {
                return;
            }

            Window.Current.CoreWindow.KeyDown -= LibraryPage_OnCoreWindowKeyDown;
            _keyHandlerAttached = false;
        }

        public bool FocusDefaultContent()
        {
            if (FocusFirstItemNow(FocusState.Keyboard))
            {
                return true;
            }

            if (SortButton.IsEnabled && SortButton.Focus(FocusState.Keyboard))
            {
                return true;
            }

            return RefreshButton.IsEnabled && RefreshButton.Focus(FocusState.Keyboard);
        }

        private bool TryRouteLibraryDirectionalKey(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Up:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadLeftThumbstickUp:
                    int focusedGridIndex;
                    if (!TryGetFocusedGridIndex(out focusedGridIndex))
                    {
                        return false;
                    }

                    return FocusFilterControlForGridIndex(focusedGridIndex);

                case VirtualKey.Down:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadLeftThumbstickDown:
                    var focusedElement = FocusManager.GetFocusedElement();
                    if (IsFocusWithin(focusedElement, SortButton))
                    {
                        return FocusGridItem(0, FocusState.Keyboard);
                    }

                    if (IsFocusWithin(focusedElement, FilterButton))
                    {
                        return FocusGridItem(Math.Min(1, ItemsGrid.Items.Count - 1), FocusState.Keyboard);
                    }

                    if (IsFocusWithin(focusedElement, RefreshButton))
                    {
                        return FocusGridItem(
                            Math.Min(GetVisibleColumnCount() - 1, ItemsGrid.Items.Count - 1),
                            FocusState.Keyboard);
                    }

                    return false;

                default:
                    return false;
            }
        }

        private bool FocusFilterControlForGridIndex(int gridIndex)
        {
            if (IsSectionRequest(_request))
            {
                return RefreshButton.IsEnabled && RefreshButton.Focus(FocusState.Keyboard);
            }

            if (gridIndex < 0 || gridIndex >= GetVisibleColumnCount())
            {
                return false;
            }

            var middleColumn = Math.Max(1, GetVisibleColumnCount() / 2);
            if (gridIndex < middleColumn)
            {
                return SortButton.IsEnabled && SortButton.Focus(FocusState.Keyboard);
            }

            return FilterButton.IsEnabled && FilterButton.Focus(FocusState.Keyboard);
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
            SortButton.IsEnabled = false;
            FilterButton.IsEnabled = false;
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
                    IReadOnlyList<EmbyMediaItem> items;
                    if (IsSectionRequest(request))
                    {
                        items = await client.GetHomeSectionItemsAsync(session, request.SectionId, 100);
                    }
                    else
                    {
                        items = await client.GetItemsAsync(
                            session,
                            CreateItemsQuery(request, SortOptions[_sortIndex], FilterOptions[_filterIndex]));
                        items = ApplyItemTypeGuard(request, items);
                    }

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
                    SortButton.IsEnabled = !IsSectionRequest(request);
                    FilterButton.IsEnabled = !IsSectionRequest(request);

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

        private static IReadOnlyList<EmbyMediaItem> ApplyItemTypeGuard(
            LibraryNavigationRequest request,
            IReadOnlyList<EmbyMediaItem> items)
        {
            return request.Query.RequireItemTypeMatch
                ? EmbyLibraryItemTypePolicy.KeepIncludedItemTypes(items, request.IncludeItemTypes)
                : items;
        }

        private static EmbyItemsQuery CreateItemsQuery(
            LibraryNavigationRequest request,
            LibraryQueryOption sortOption,
            LibraryQueryOption filterOption)
        {
            return new EmbyItemsQuery
            {
                ParentId = request.ParentId,
                IncludeItemTypes = request.IncludeItemTypes,
                CollectionTypes = request.Query.CollectionTypes,
                MediaTypes = request.Query.MediaTypes,
                GenreIds = request.Query.GenreIds,
                PersonIds = request.Query.PersonIds,
                ArtistIds = request.Query.ArtistIds,
                AlbumArtistIds = request.Query.AlbumArtistIds,
                Ids = request.Query.Ids,
                IsFavorite = request.Query.IsFavorite,
                IsPlayed = request.Query.IsPlayed,
                IsFolder = request.Query.IsFolder,
                Recursive = true,
                SortBy = sortOption.Tag,
                SortOrder = "Ascending",
                Filters = CombineFilters(request.Query.Filters, filterOption.Tag),
                Limit = 100
            };
        }

        private static string CombineFilters(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return second ?? "";
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                return first ?? "";
            }

            return first + "," + second;
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
            var focused = false;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                focused = FocusFirstItemNow(FocusState.Keyboard);
            });

            if (focused || !CanApplyLoad(loadGeneration))
            {
                return;
            }

            await Task.Delay(75);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (CanApplyLoad(loadGeneration))
                {
                    FocusFirstItemNow(FocusState.Keyboard);
                }
            });
        }

        private bool FocusFirstItemNow(FocusState focusState)
        {
            if (ItemsGrid.Items.Count == 0)
            {
                return false;
            }

            return FocusGridItem(0, focusState);
        }

        private bool FocusGridItem(int index, FocusState focusState)
        {
            if (index < 0 || index >= ItemsGrid.Items.Count)
            {
                return false;
            }

            var firstItemData = ItemsGrid.Items[index];
            ItemsGrid.UpdateLayout();
            ItemsGrid.ScrollIntoView(firstItemData);
            ItemsGrid.UpdateLayout();

            var firstItem = ItemsGrid.ContainerFromIndex(index) as Control;
            if (firstItem != null && firstItem.Focus(focusState))
            {
                return true;
            }

            return ItemsGrid.Focus(focusState);
        }

        private bool TryGetFocusedGridIndex(out int index)
        {
            var current = FocusManager.GetFocusedElement() as DependencyObject;
            while (current != null)
            {
                var gridViewItem = current as GridViewItem;
                if (gridViewItem != null)
                {
                    index = ItemsGrid.IndexFromContainer(gridViewItem);
                    return index >= 0;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            index = -1;
            return false;
        }

        private int GetVisibleColumnCount()
        {
            var stride = GridItemWidth + GridItemTrailingMargin;
            var available = Math.Max(stride, ItemsGrid.ActualWidth + GridItemTrailingMargin);
            return Math.Max(1, (int)Math.Floor(available / stride));
        }

        private static bool IsFocusWithin(object focusedElement, DependencyObject target)
        {
            var current = focusedElement as DependencyObject;
            while (current != null)
            {
                if (current == target)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
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
                    if (!IsSectionRequest(_request))
                    {
                        SortButton.Focus(FocusState.Programmatic);
                    }
                }
            });
        }

        private bool CanApplyLoad(int loadGeneration)
        {
            return !_isUnloaded && loadGeneration == _loadGeneration;
        }

        private void ApplyOptionLabels()
        {
            SortValueBlock.Text = SortOptions[_sortIndex].Label;
            FilterValueBlock.Text = FilterOptions[_filterIndex].Label;
        }

        private static bool IsSectionRequest(LibraryNavigationRequest? request)
        {
            return request != null && !string.IsNullOrWhiteSpace(request.SectionId);
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

        private sealed class LibraryQueryOption
        {
            public LibraryQueryOption(string label, string tag)
            {
                Label = label;
                Tag = tag;
            }

            public string Label { get; }

            public string Tag { get; }
        }
    }
}
