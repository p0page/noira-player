using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Diagnostics;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Input;
using NextGenEmby.Core.Media;
using Windows.UI.Core;
using Windows.UI.Text;
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
        private const string OrganizationChildItemTypes = "Movie,Series,Episode,Video,MusicVideo,Audio,Photo";
        private const double PosterCardWidth = 184d;
        private const double PosterCardHeight = 326d;
        private const double PosterArtworkWidth = 168d;
        private const double PosterArtworkHeight = 252d;
        private const double PosterMetadataHeight = 52d;
        private const double PhotoCardWidth = 322d;
        private const double PhotoCardHeight = 238d;
        private const double PhotoArtworkWidth = 306d;
        private const double PhotoArtworkHeight = 174d;
        private const double PhotoMetadataHeight = 42d;
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
        private LibraryOptionSheetKind _activeOptionSheet;
        private int _optionSheetPreviewIndex;
        private bool _optionSheetConfirming;
        private object? _optionSheetReturnFocus;
        private int _loadGeneration;

        public LibraryPage()
        {
            InitializeComponent();
            AddHandler(KeyDownEvent, new KeyEventHandler(Page_OnKeyDown), true);
            Loaded += LibraryPage_OnLoaded;
            Unloaded += LibraryPage_OnUnloaded;
            ApplyCommandButtonFocusTreatment(SortButton);
            ApplyCommandButtonFocusTreatment(FilterButton);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _isUnloaded = false;
            _isNavigatingToDetails = false;
            _request = e.Parameter as LibraryNavigationRequest;
            CloseOptionSheet(restoreFocus: false);
            if (_request == null || string.IsNullOrWhiteSpace(_request.Title))
            {
                TitleBlock.Text = "Library";
                StatusBlock.Text = "Choose a library from Home.";
                ItemsGrid.Items.Clear();
                HideEmptyState();
                return;
            }

            TitleBlock.Text = _request.Title;
            OptionsPanel.Visibility = IsReadOnlySequenceRequest(_request) ? Visibility.Collapsed : Visibility.Visible;
            ApplyOptionLabels();
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

        private void SortButton_OnClick(object sender, RoutedEventArgs e)
        {
            OpenOptionSheet(LibraryOptionSheetKind.Sort);
        }

        private void FilterButton_OnClick(object sender, RoutedEventArgs e)
        {
            OpenOptionSheet(LibraryOptionSheetKind.Filter);
        }

        private async void ClearFilters_OnClick(object sender, RoutedEventArgs e)
        {
            _filterIndex = 0;
            ApplyOptionLabels();
            await LoadLibraryAsync();
        }

        private void Page_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = TryRouteLibraryKey(e.Key) || e.Handled;
        }

        private void LibraryPage_OnCoreWindowKeyDown(CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            if (IsHorizontalKey(args.VirtualKey))
            {
                return;
            }

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

        private bool TryRouteLibraryKey(VirtualKey key)
        {
            return TryRouteOptionSheetKey(key) || TryRouteLibraryDirectionalKey(key);
        }

        private bool TryRouteOptionSheetKey(VirtualKey key)
        {
            if (_activeOptionSheet == LibraryOptionSheetKind.None)
            {
                return false;
            }

            switch (key)
            {
                case VirtualKey.Up:
                case VirtualKey.Left:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadDPadLeft:
                case VirtualKey.GamepadLeftThumbstickUp:
                case VirtualKey.GamepadLeftThumbstickLeft:
                    MoveOptionSheetSelection(-1);
                    return true;

                case VirtualKey.Down:
                case VirtualKey.Right:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadDPadRight:
                case VirtualKey.GamepadLeftThumbstickDown:
                case VirtualKey.GamepadLeftThumbstickRight:
                    MoveOptionSheetSelection(1);
                    return true;

                case VirtualKey.Enter:
                case VirtualKey.Space:
                case VirtualKey.GamepadA:
                    _ = ConfirmOptionSheetAsync();
                    return true;

                case VirtualKey.Escape:
                case VirtualKey.GoBack:
                case VirtualKey.GamepadB:
                    CancelOptionSheet();
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsHorizontalKey(VirtualKey key)
        {
            return key == VirtualKey.Left ||
                key == VirtualKey.Right ||
                key == VirtualKey.GamepadDPadLeft ||
                key == VirtualKey.GamepadDPadRight ||
                key == VirtualKey.GamepadLeftThumbstickLeft ||
                key == VirtualKey.GamepadLeftThumbstickRight;
        }

        private bool TryRouteLibraryDirectionalKey(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Left:
                case VirtualKey.GamepadDPadLeft:
                case VirtualKey.GamepadLeftThumbstickLeft:
                    return TryRouteToolbarHorizontalKey(LibraryToolbarFocusDirection.Left);

                case VirtualKey.Right:
                case VirtualKey.GamepadDPadRight:
                case VirtualKey.GamepadLeftThumbstickRight:
                    return TryRouteToolbarHorizontalKey(LibraryToolbarFocusDirection.Right);

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

        private bool TryRouteToolbarHorizontalKey(LibraryToolbarFocusDirection direction)
        {
            var current = GetFocusedToolbarTarget();
            if (current == LibraryToolbarFocusTarget.None)
            {
                return false;
            }

            var next = LibraryToolbarFocusPolicy.Move(current, direction, IsReadOnlySequenceRequest(_request));
            return FocusToolbarTarget(next);
        }

        private LibraryToolbarFocusTarget GetFocusedToolbarTarget()
        {
            var focusedElement = FocusManager.GetFocusedElement();
            if (IsFocusWithin(focusedElement, SortButton))
            {
                return LibraryToolbarFocusTarget.Sort;
            }

            if (IsFocusWithin(focusedElement, FilterButton))
            {
                return LibraryToolbarFocusTarget.Filter;
            }

            return IsFocusWithin(focusedElement, RefreshButton)
                ? LibraryToolbarFocusTarget.Refresh
                : LibraryToolbarFocusTarget.None;
        }

        private bool FocusToolbarTarget(LibraryToolbarFocusTarget target)
        {
            switch (target)
            {
                case LibraryToolbarFocusTarget.Sort:
                    return SortButton.IsEnabled && SortButton.Focus(FocusState.Keyboard);
                case LibraryToolbarFocusTarget.Filter:
                    return FilterButton.IsEnabled && FilterButton.Focus(FocusState.Keyboard);
                case LibraryToolbarFocusTarget.Refresh:
                    return RefreshButton.IsEnabled && RefreshButton.Focus(FocusState.Keyboard);
                default:
                    return false;
            }
        }

        private bool FocusFilterControlForGridIndex(int gridIndex)
        {
            if (IsReadOnlySequenceRequest(_request))
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
            EmptyRetryButton.IsEnabled = false;
            ClearFiltersButton.IsEnabled = false;
            StatusBlock.Text = "Loading...";
            HideEmptyState();
            var focusFirstItem = false;
            var focusFallback = false;
            var preferredFocusItemId = request.RestoreFocusItemId;

            try
            {
#if DEBUG
                if (request.DevelopmentItems.Count > 0)
                {
                    focusFirstItem = RenderItems(
                        CreateDevelopmentGridItems(
                            ApplyItemTypeGuard(
                                request,
                                SelectDevelopmentItemsForRequest(request)),
                            request.DevelopmentArtworkUris,
                            IsPhotoSurfaceRequest(_request)),
                        loadGeneration);
                    focusFallback = !focusFirstItem;
                    return;
                }
#endif
                var session = await _sessionStore.LoadAsync();
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                if (session == null)
                {
                    ItemsGrid.Items.Clear();
                    StatusBlock.Text = "Sign in first.";
                    HideEmptyState();
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
                    else if (IsPlaylistRequest(request))
                    {
                        items = await client.GetPlaylistItemsAsync(session, request.ParentId, 100);
                    }
                    else
                    {
                        items = await client.GetItemsAsync(
                            session,
                            CreateItemsQuery(request, SortOptions[_sortIndex], FilterOptions[_filterIndex]));
                        items = ApplyItemTypeGuard(request, items);
                    }

                    gridItems = CreateGridItems(session, client, items, IsPhotoSurfaceRequest(_request));
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
                ShowEmptyState("Unable to load library.", "Retry this library.");
                focusFallback = true;
            }
            finally
            {
                if (CanApplyLoad(loadGeneration))
                {
                    RefreshButton.IsEnabled = true;
                    SortButton.IsEnabled = !IsReadOnlySequenceRequest(request);
                    FilterButton.IsEnabled = !IsReadOnlySequenceRequest(request);
                    EmptyRetryButton.IsEnabled = true;
                    ClearFiltersButton.IsEnabled = true;

                    if (focusFirstItem)
                    {
                        await FocusPreferredItemAsync(loadGeneration, preferredFocusItemId);
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
                ShowEmptyState("No items found", "Try another filter or refresh this library.");
                return false;
            }

            HideEmptyState();
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
                Genres = request.Query.Genres,
                PersonIds = request.Query.PersonIds,
                StudioIds = request.Query.StudioIds,
                Studios = request.Query.Studios,
                Tags = request.Query.Tags,
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
            IReadOnlyList<EmbyMediaItem> items,
            bool usePhotoRecipe)
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

                gridItems.Add(new LibraryGridItem(item, imageSource, usePhotoRecipe));
            }

            return gridItems;
        }

#if DEBUG
        private static IReadOnlyList<EmbyMediaItem> SelectDevelopmentItemsForRequest(LibraryNavigationRequest request)
        {
            if (request.DevelopmentItems.Count == 0)
            {
                return request.DevelopmentItems;
            }

            if (HasMetadataFacetQuery(request.Query))
            {
                return request.DevelopmentItems
                    .Where(item => MatchesMetadataFacetQuery(item, request.Query))
                    .ToList();
            }

            var parentId = request.ParentId ?? "";
            var hasParentMetadata = false;
            foreach (var item in request.DevelopmentItems)
            {
                if (!string.IsNullOrWhiteSpace(item.ParentId))
                {
                    hasParentMetadata = true;
                    break;
                }
            }

            if (!hasParentMetadata && string.IsNullOrWhiteSpace(parentId))
            {
                return request.DevelopmentItems;
            }

            var filteredItems = new List<EmbyMediaItem>();
            foreach (var item in request.DevelopmentItems)
            {
                if (string.Equals(item.ParentId ?? "", parentId, StringComparison.Ordinal))
                {
                    filteredItems.Add(item);
                }
            }

            return filteredItems;
        }

        private static bool HasMetadataFacetQuery(LibraryNavigationQuery query)
        {
            return query != null &&
                (!string.IsNullOrWhiteSpace(query.GenreIds) ||
                    !string.IsNullOrWhiteSpace(query.Genres) ||
                    !string.IsNullOrWhiteSpace(query.StudioIds) ||
                    !string.IsNullOrWhiteSpace(query.Studios) ||
                    !string.IsNullOrWhiteSpace(query.Tags));
        }

        private static bool MatchesMetadataFacetQuery(EmbyMediaItem item, LibraryNavigationQuery query)
        {
            return MatchesReferenceIds(item.GenreItems, query.GenreIds) &&
                MatchesReferenceNames(item.GenreItems, query.Genres) &&
                MatchesReferenceIds(item.StudioItems, query.StudioIds) &&
                MatchesReferenceNames(item.StudioItems, query.Studios) &&
                MatchesReferenceNames(item.TagItems, query.Tags);
        }

        private static bool MatchesReferenceIds(IReadOnlyList<EmbyItemReference> references, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            var queryIds = SplitFacetQuery(query);
            return references != null &&
                references.Any(reference => queryIds.Contains(reference.Id ?? ""));
        }

        private static bool MatchesReferenceNames(IReadOnlyList<EmbyItemReference> references, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            var queryNames = SplitFacetQuery(query);
            return references != null &&
                references.Any(reference => queryNames.Contains(reference.Name ?? ""));
        }

        private static HashSet<string> SplitFacetQuery(string query)
        {
            return new HashSet<string>(
                (query ?? "")
                    .Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<LibraryGridItem> CreateDevelopmentGridItems(
            IReadOnlyList<EmbyMediaItem> items,
            IReadOnlyDictionary<string, string> artworkUris,
            bool usePhotoRecipe)
        {
            var gridItems = new List<LibraryGridItem>();
            if (items == null)
            {
                return gridItems;
            }

            foreach (var item in items)
            {
                BitmapImage? imageSource = null;
                var candidate = EmbyArtworkPolicy.SelectPosterArtwork(item, 420);
                if (candidate != null &&
                    artworkUris != null &&
                    artworkUris.TryGetValue(
                        DevelopmentHomeFixture.ArtworkKey(candidate.ItemId, candidate.ImageType),
                        out var uri) &&
                    !string.IsNullOrWhiteSpace(uri))
                {
                    imageSource = new BitmapImage(new Uri(uri));
                }

                gridItems.Add(new LibraryGridItem(item, imageSource, usePhotoRecipe));
            }

            return gridItems;
        }
#endif

        private void ItemsGrid_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var gridItem = e.ClickedItem as LibraryGridItem;
            ActivateLibraryItem(gridItem == null ? null : gridItem.Item);
        }

        private void ItemsGrid_OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            PosterGridFocusVisuals.PrepareContainer(args.ItemContainer as GridViewItem);
        }

        private void ActivateLibraryItem(EmbyMediaItem? item)
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
            var request = _request;
            if (request != null)
            {
                request.RestoreFocusItemId = item.Id;
            }

            var itemName = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            var route = LibraryItemActivationPolicy.ChooseRoute(item.Type);
            if (route == LibraryItemActivationRoute.PhotoViewer)
            {
                var developmentImageUri = ResolveDevelopmentPhotoUri(item);
                Frame.Navigate(typeof(PhotoViewerPage), new PhotoViewerNavigationRequest(item.Id, itemName, developmentImageUri));
                return;
            }

            if (route == LibraryItemActivationRoute.BrowseFolder)
            {
                Frame.Navigate(typeof(LibraryPage), CreateFolderNavigationRequest(item, itemName));
                return;
            }

            Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(item.Id, itemName));
        }

        private LibraryNavigationRequest CreateFolderNavigationRequest(EmbyMediaItem item, string itemName)
        {
            var request = _request;
            if (request == null)
            {
                return new LibraryNavigationRequest(
                    itemName,
                    "",
                    IsOrganizationContainer(item.Type) ? OrganizationChildItemTypes : "",
                    item.Id,
                    "",
                    LibraryNavigationQuery.Empty,
                    null,
                    null,
                    item.Type);
            }

            return new LibraryNavigationRequest(
                itemName,
                request.CollectionType,
                IsOrganizationContainer(item.Type) ? OrganizationChildItemTypes : request.IncludeItemTypes,
                item.Id,
                "",
                IsOrganizationContainer(item.Type) ? LibraryNavigationQuery.Empty : request.Query,
                request.DevelopmentItems,
                request.DevelopmentArtworkUris,
                item.Type);
        }

        private static bool IsOrganizationContainer(string itemType)
        {
            return string.Equals(itemType, "BoxSet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemType, "Playlist", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveDevelopmentPhotoUri(EmbyMediaItem item)
        {
            var request = _request;
            if (request == null || request.DevelopmentArtworkUris.Count == 0)
            {
                return "";
            }

            var candidate = EmbyArtworkPolicy.SelectPosterArtwork(item, 1920);
            if (candidate == null)
            {
                return "";
            }

            return request.DevelopmentArtworkUris.TryGetValue(
                DevelopmentHomeFixture.ArtworkKey(candidate.ItemId, candidate.ImageType),
                out var uri)
                ? uri
                : "";
        }

        private async Task FocusPreferredItemAsync(int loadGeneration, string preferredFocusItemId)
        {
            var focused = false;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                focused = FocusItemByIdNow(preferredFocusItemId, FocusState.Keyboard) ||
                    FocusFirstItemNow(FocusState.Keyboard);
            });

            if (focused || !CanApplyLoad(loadGeneration))
            {
                if (focused)
                {
                    ClearRestoreFocusItemId(preferredFocusItemId);
                }

                return;
            }

            await Task.Delay(75);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (CanApplyLoad(loadGeneration))
                {
                    if (FocusItemByIdNow(preferredFocusItemId, FocusState.Keyboard) ||
                        FocusFirstItemNow(FocusState.Keyboard))
                    {
                        ClearRestoreFocusItemId(preferredFocusItemId);
                    }
                }
            });
        }

        private void ClearRestoreFocusItemId(string itemId)
        {
            var request = _request;
            if (request != null &&
                string.Equals(request.RestoreFocusItemId, itemId ?? "", StringComparison.Ordinal))
            {
                request.RestoreFocusItemId = "";
            }
        }

        private bool FocusFirstItemNow(FocusState focusState)
        {
            if (ItemsGrid.Items.Count == 0)
            {
                return false;
            }

            return FocusGridItem(0, focusState);
        }

        private bool FocusItemByIdNow(string itemId, FocusState focusState)
        {
            var index = FindGridItemIndexById(itemId);
            return index >= 0 && FocusGridItem(index, focusState);
        }

        private int FindGridItemIndexById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return -1;
            }

            for (var i = 0; i < ItemsGrid.Items.Count; i++)
            {
                var gridItem = ItemsGrid.Items[i] as LibraryGridItem;
                if (gridItem != null &&
                    string.Equals(gridItem.Item.Id, itemId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
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
            var stride = GetGridItemWidth() + GridItemTrailingMargin;
            var available = Math.Max(stride, ItemsGrid.ActualWidth + GridItemTrailingMargin);
            return Math.Max(1, (int)Math.Floor(available / stride));
        }

        private double GetGridItemWidth()
        {
            return IsPhotoSurfaceRequest(_request) ? PhotoCardWidth : PosterCardWidth;
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

                if (ClearFiltersButton.Visibility == Visibility.Visible &&
                    ClearFiltersButton.IsEnabled &&
                    ClearFiltersButton.Focus(FocusState.Programmatic))
                {
                    return;
                }

                if (EmptyRetryButton.Visibility == Visibility.Visible &&
                    EmptyRetryButton.IsEnabled &&
                    EmptyRetryButton.Focus(FocusState.Programmatic))
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

        private void ShowEmptyState(string title, string body)
        {
            EmptyTitleBlock.Text = title;
            EmptyBodyBlock.Text = body;
            EmptyStatePanel.Visibility = Visibility.Visible;
            ClearFiltersButton.Visibility = _filterIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void HideEmptyState()
        {
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ClearFiltersButton.Visibility = Visibility.Collapsed;
        }

        private void ApplyOptionLabels()
        {
            SortValueBlock.Text = SortOptions[_sortIndex].Label;
            FilterValueBlock.Text = FilterOptions[_filterIndex].Label;
        }

        private void OpenOptionSheet(LibraryOptionSheetKind sheetKind)
        {
            if (sheetKind == LibraryOptionSheetKind.None)
            {
                return;
            }

            _activeOptionSheet = sheetKind;
            _optionSheetReturnFocus = FocusManager.GetFocusedElement();
            var decision = LibraryOptionSheetPolicy.Open(GetActiveOptionCount(), GetActiveCommittedIndex());
            _optionSheetPreviewIndex = decision.Index;

            OptionSheetTitleBlock.Text = sheetKind == LibraryOptionSheetKind.Sort ? "Sort by" : "Filter";
            OptionSheetSubtitleBlock.Text = "Current: " + GetActiveOptions()[_optionSheetPreviewIndex].Label;
            OptionSheetRoot.Visibility = Visibility.Visible;
            RenderOptionSheetOptions();
            FocusOptionSheetOption(_optionSheetPreviewIndex);
        }

        private void MoveOptionSheetSelection(int offset)
        {
            var nextIndex = LibraryOptionSheetPolicy.MovePreviewIndex(
                _optionSheetPreviewIndex,
                GetActiveOptionCount(),
                offset);

            if (nextIndex == _optionSheetPreviewIndex)
            {
                FocusOptionSheetOption(_optionSheetPreviewIndex);
                return;
            }

            _optionSheetPreviewIndex = nextIndex;
            OptionSheetSubtitleBlock.Text = "Current: " + GetActiveOptions()[_optionSheetPreviewIndex].Label;
            RenderOptionSheetOptions();
            FocusOptionSheetOption(_optionSheetPreviewIndex);
        }

        private async Task ConfirmOptionSheetAsync()
        {
            if (_activeOptionSheet == LibraryOptionSheetKind.None || _optionSheetConfirming)
            {
                return;
            }

            _optionSheetConfirming = true;
            var committedIndex = GetActiveCommittedIndex();
            var decision = LibraryOptionSheetPolicy.Confirm(
                committedIndex,
                _optionSheetPreviewIndex,
                GetActiveOptionCount());

            SetActiveCommittedIndex(decision.Index);
            ApplyOptionLabels();
            CloseOptionSheet(restoreFocus: !decision.ShouldReload);

            if (decision.ShouldReload)
            {
                await LoadLibraryAsync();
            }

            _optionSheetConfirming = false;
        }

        private void CancelOptionSheet()
        {
            if (_activeOptionSheet == LibraryOptionSheetKind.None)
            {
                return;
            }

            var decision = LibraryOptionSheetPolicy.Cancel(GetActiveCommittedIndex(), GetActiveOptionCount());
            SetActiveCommittedIndex(decision.Index);
            ApplyOptionLabels();
            CloseOptionSheet(restoreFocus: true);
        }

        private void CloseOptionSheet(bool restoreFocus)
        {
            if (OptionSheetRoot == null)
            {
                return;
            }

            var returnTarget = _optionSheetReturnFocus as Control;
            _activeOptionSheet = LibraryOptionSheetKind.None;
            _optionSheetConfirming = false;
            _optionSheetReturnFocus = null;
            OptionSheetOptionsPanel.Children.Clear();
            OptionSheetRoot.Visibility = Visibility.Collapsed;

            if (restoreFocus && returnTarget != null)
            {
                returnTarget.Focus(FocusState.Keyboard);
            }
        }

        private void RenderOptionSheetOptions()
        {
            OptionSheetOptionsPanel.Children.Clear();
            var options = GetActiveOptions();
            for (var i = 0; i < options.Length; i++)
            {
                OptionSheetOptionsPanel.Children.Add(CreateOptionSheetButton(i, options[i]));
            }
        }

        private Button CreateOptionSheetButton(int index, LibraryQueryOption option)
        {
            var isPreview = index == _optionSheetPreviewIndex;
            var button = new Button
            {
                Tag = index,
                Style = (Style)Application.Current.Resources["TvLibraryOptionSheetOptionButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinHeight = 58,
                Background = isPreview ? BrushResource("AppFocusedCardFillBrush") : BrushResource("AppChromeBrush"),
                BorderBrush = BrushResource("AppTransparentBrush"),
                UseSystemFocusVisuals = false
            };

            button.Click += OptionSheetOption_OnClick;

            var row = new Grid
            {
                ColumnSpacing = 12
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = option.Label,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushResource("AppTextBrush"),
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });

            var selectedIcon = new SymbolIcon(Symbol.Accept)
            {
                Foreground = BrushResource("AppTextBrush"),
                Visibility = isPreview ? Visibility.Visible : Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(selectedIcon, 1);
            row.Children.Add(selectedIcon);

            button.Content = row;
            return button;
        }

        private void ApplyCommandButtonFocusTreatment(Button button)
        {
            button.UseSystemFocusVisuals = false;
            ApplyCommandButtonMatteFocus(button, isFocused: false);
            button.GotFocus += CommandButton_OnGotFocus;
            button.LostFocus += CommandButton_OnLostFocus;
        }

        private void CommandButton_OnGotFocus(object sender, RoutedEventArgs e)
        {
            ApplyCommandButtonMatteFocus(sender as Button, isFocused: true);
        }

        private void CommandButton_OnLostFocus(object sender, RoutedEventArgs e)
        {
            ApplyCommandButtonMatteFocus(sender as Button, isFocused: false);
        }

        private static void ApplyCommandButtonMatteFocus(Button? button, bool isFocused)
        {
            if (button == null)
            {
                return;
            }

            button.Background = isFocused
                ? BrushResource("AppFocusedCardFillBrush")
                : BrushResource("AppChromeBrush");
            button.BorderBrush = BrushResource("AppTransparentBrush");
        }

        private void OptionSheetOption_OnClick(object sender, RoutedEventArgs e)
        {
            if (_activeOptionSheet == LibraryOptionSheetKind.None)
            {
                return;
            }

            var button = sender as Button;
            if (button != null && button.Tag is int index)
            {
                _optionSheetPreviewIndex = index;
            }

            _ = ConfirmOptionSheetAsync();
        }

        private void FocusOptionSheetOption(int index)
        {
            if (index < 0 || index >= OptionSheetOptionsPanel.Children.Count)
            {
                return;
            }

            var control = OptionSheetOptionsPanel.Children[index] as Control;
            if (control != null)
            {
                control.Focus(FocusState.Keyboard);
            }
        }

        private int GetActiveOptionCount()
        {
            return GetActiveOptions().Length;
        }

        private int GetActiveCommittedIndex()
        {
            return _activeOptionSheet == LibraryOptionSheetKind.Filter ? _filterIndex : _sortIndex;
        }

        private void SetActiveCommittedIndex(int index)
        {
            if (_activeOptionSheet == LibraryOptionSheetKind.Filter)
            {
                _filterIndex = index;
            }
            else
            {
                _sortIndex = index;
            }
        }

        private LibraryQueryOption[] GetActiveOptions()
        {
            return _activeOptionSheet == LibraryOptionSheetKind.Filter ? FilterOptions : SortOptions;
        }

        private static Brush BrushResource(string key)
        {
            return (Brush)Application.Current.Resources[key];
        }

        private static bool IsSectionRequest(LibraryNavigationRequest? request)
        {
            return request != null && !string.IsNullOrWhiteSpace(request.SectionId);
        }

        private static bool IsPlaylistRequest(LibraryNavigationRequest? request)
        {
            return request != null &&
                string.Equals(request.ContainerItemType, "Playlist", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReadOnlySequenceRequest(LibraryNavigationRequest? request)
        {
            return IsSectionRequest(request) || IsPlaylistRequest(request);
        }

        private static bool IsPhotoSurfaceRequest(LibraryNavigationRequest? request)
        {
            if (request == null)
            {
                return false;
            }

            return string.Equals(request.CollectionType, "photos", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.Query.MediaTypes, "Photo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.IncludeItemTypes, "Photo,Folder", StringComparison.OrdinalIgnoreCase);
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

        private static string CreateInitials(string value)
        {
            return PosterFallbackInitials.Create(value);
        }

        public sealed class LibraryGridItem
        {
            public LibraryGridItem(EmbyMediaItem item, BitmapImage? imageSource, bool usePhotoRecipe = false)
            {
                Item = item;
                ImageSource = imageSource;
                Title = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
                Meta = CreateMeta(item);
                Initials = CreateInitials(Title);
                CardWidth = usePhotoRecipe ? PhotoCardWidth : PosterCardWidth;
                CardHeight = usePhotoRecipe ? PhotoCardHeight : PosterCardHeight;
                ArtworkWidth = usePhotoRecipe ? PhotoArtworkWidth : PosterArtworkWidth;
                ArtworkHeight = usePhotoRecipe ? PhotoArtworkHeight : PosterArtworkHeight;
                MetadataHeight = usePhotoRecipe ? PhotoMetadataHeight : PosterMetadataHeight;
            }

            public EmbyMediaItem Item { get; }

            public string Title { get; }

            public string Meta { get; }

            public string Initials { get; }

            public BitmapImage? ImageSource { get; }

            public double CardWidth { get; }

            public double CardHeight { get; }

            public double ArtworkWidth { get; }

            public double ArtworkHeight { get; }

            public double MetadataHeight { get; }
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

        private enum LibraryOptionSheetKind
        {
            None,
            Sort,
            Filter
        }
    }
}
