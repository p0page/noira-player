using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace NextGenEmby.App.Views
{
    public sealed partial class SearchPage : Page, ITvContentFocusTarget
    {
        private const double ResultCardWidth = 168d;
        private const double ResultCardTrailingMargin = 14d;
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private readonly List<Button> _scopeButtons = new List<Button>();
        private bool _isUnloaded;
        private bool _isNavigatingToDetails;
        private int _searchGeneration;
        private string _selectedScopeKey = "all";

        public SearchPage()
        {
            InitializeComponent();
            NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            AddHandler(KeyDownEvent, new KeyEventHandler(Page_OnKeyDown), true);
            Loaded += SearchPage_OnLoaded;
            Unloaded += SearchPage_OnUnloaded;
        }

        private void SearchPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = false;
            _isNavigatingToDetails = false;
            EnsureScopeButtons();
            ApplyScopeButtonState();
            SearchBox.Focus(FocusState.Programmatic);
        }

        private void SearchPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            _searchGeneration++;
        }

        public bool FocusDefaultContent()
        {
            if (FocusFirstResultNow(FocusState.Keyboard))
            {
                return true;
            }

            return SearchBox.Focus(FocusState.Keyboard);
        }

        private async void Search_OnClick(object sender, RoutedEventArgs e)
        {
            await SearchAsync();
        }

        private async void SearchBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.GamepadA)
            {
                e.Handled = true;
                await SearchAsync();
                return;
            }

            if (IsDownKey(e.Key))
            {
                e.Handled = true;
                FocusSelectedScope(FocusState.Keyboard);
            }
        }

        private void Page_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var focusedElement = FocusManager.GetFocusedElement();
            if (e.Handled && !IsFocusWithin(focusedElement, ScopesPanel))
            {
                return;
            }

            if (TryRouteSearchDirectionalKey(e.Key))
            {
                e.Handled = true;
            }
        }

        private bool TryRouteSearchDirectionalKey(VirtualKey key)
        {
            var focusedElement = FocusManager.GetFocusedElement();
            switch (key)
            {
                case VirtualKey.Up:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadLeftThumbstickUp:
                    int focusedResultIndex;
                    if (TryGetFocusedResultIndex(out focusedResultIndex) &&
                        focusedResultIndex < GetVisibleColumnCount())
                    {
                        return FocusSelectedScope(FocusState.Keyboard);
                    }

                    if (IsFocusWithin(focusedElement, ScopesPanel))
                    {
                        return SearchBox.Focus(FocusState.Keyboard);
                    }

                    return false;

                case VirtualKey.Down:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadLeftThumbstickDown:
                    if (IsFocusWithin(focusedElement, SearchBox) ||
                        IsFocusWithin(focusedElement, SearchActionButton))
                    {
                        return FocusSelectedScope(FocusState.Keyboard);
                    }

                    if (IsFocusWithin(focusedElement, ScopesPanel))
                    {
                        return FocusFirstResultNow(FocusState.Keyboard);
                    }

                    return false;

                case VirtualKey.Left:
                case VirtualKey.GamepadDPadLeft:
                case VirtualKey.GamepadLeftThumbstickLeft:
                    return MoveScopeFocus(-1);

                case VirtualKey.Right:
                case VirtualKey.GamepadDPadRight:
                case VirtualKey.GamepadLeftThumbstickRight:
                    return MoveScopeFocus(1);

                default:
                    return false;
            }
        }

        private async Task SearchAsync()
        {
            var searchGeneration = ++_searchGeneration;
            ResultsGrid.Items.Clear();
            _isNavigatingToDetails = false;

            var term = (SearchBox.Text ?? "").Trim();
            var scope = EmbySearchScopePolicy.GetScope(_selectedScopeKey);
            if (string.IsNullOrWhiteSpace(term))
            {
                StatusBlock.Text = "Enter a search.";
                SearchBox.Focus(FocusState.Programmatic);
                return;
            }

            StatusBlock.Text = "Searching " + scope.Label + "...";

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (!CanApplySearch(searchGeneration))
                {
                    return;
                }

                if (session == null)
                {
                    StatusBlock.Text = "Sign in first.";
                    SearchBox.Focus(FocusState.Programmatic);
                    return;
                }

                IReadOnlyList<SearchResultCard> cards;
                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    var items = await client.SearchItemsAsync(session, term, scope.IncludeItemTypes);
                    cards = CreateResultCards(session, client, items);
                }

                if (!CanApplySearch(searchGeneration))
                {
                    return;
                }

                RenderResults(scope, cards);
            }
            catch
            {
                if (!CanApplySearch(searchGeneration))
                {
                    return;
                }

                ResultsGrid.Items.Clear();
                StatusBlock.Text = "Unable to search.";
                SearchBox.Focus(FocusState.Programmatic);
            }
        }

        private void RenderResults(
            EmbySearchScope scope,
            IReadOnlyList<SearchResultCard> cards)
        {
            ResultsGrid.Items.Clear();

            if (cards == null || cards.Count == 0)
            {
                StatusBlock.Text = scope.Key == "all"
                    ? "No results."
                    : "No results in " + scope.Label + ".";
                SearchBox.Focus(FocusState.Programmatic);
                return;
            }

            foreach (var card in cards)
            {
                ResultsGrid.Items.Add(card);
            }

            StatusBlock.Text = cards.Count + " results / " + scope.Label;
            SearchBox.Focus(FocusState.Programmatic);
        }

        private static IReadOnlyList<SearchResultCard> CreateResultCards(
            EmbySession session,
            EmbyApiClient client,
            IReadOnlyList<EmbyMediaItem> items)
        {
            var cards = new List<SearchResultCard>();
            if (items == null)
            {
                return cards;
            }

            foreach (var item in items)
            {
                BitmapImage? imageSource = null;
                var candidate = EmbyArtworkPolicy.SelectPosterArtwork(item, 420);
                if (candidate != null)
                {
                    imageSource = new BitmapImage(new Uri(client.GetImageUrl(
                        session,
                        candidate.ItemId,
                        candidate.ImageType,
                        candidate.MaxWidth)));
                }

                cards.Add(new SearchResultCard(item, imageSource));
            }

            return cards;
        }

        private void EnsureScopeButtons()
        {
            if (_scopeButtons.Count > 0)
            {
                return;
            }

            foreach (var scope in EmbySearchScopePolicy.AllScopes)
            {
                var button = new Button
                {
                    Content = scope.Label,
                    Tag = scope,
                    MinWidth = 96,
                    Height = 46,
                    Padding = new Thickness(18, 7, 18, 7),
                    UseSystemFocusVisuals = true
                };
                button.Click += ScopeButton_OnClick;
                _scopeButtons.Add(button);
                ScopesPanel.Children.Add(button);
            }
        }

        private async void ScopeButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var scope = button == null ? null : button.Tag as EmbySearchScope;
            if (scope == null)
            {
                return;
            }

            _selectedScopeKey = scope.Key;
            ApplyScopeButtonState();
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                StatusBlock.Text = scope.Key == "all"
                    ? "Enter a search."
                    : "Search " + scope.Label + ".";
                return;
            }

            await SearchAsync();
        }

        private void ApplyScopeButtonState()
        {
            var resources = Application.Current.Resources;
            foreach (var button in _scopeButtons)
            {
                var scope = button.Tag as EmbySearchScope;
                var isSelected = scope != null &&
                    string.Equals(scope.Key, _selectedScopeKey, StringComparison.OrdinalIgnoreCase);
                button.Background = (Brush)resources[isSelected ? "AppRaisedSurfaceBrush" : "AppChromeBrush"];
                button.BorderBrush = (Brush)resources[isSelected ? "AppAccentBrush" : "AppHairlineBrush"];
                button.Foreground = (Brush)resources[isSelected ? "AppTextBrush" : "AppMutedTextBrush"];
            }
        }

        private void ResultsGrid_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var card = e.ClickedItem as SearchResultCard;
            NavigateToDetails(card == null ? null : card.Item);
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

        private bool FocusFirstResultNow(FocusState focusState)
        {
            if (ResultsGrid.Items.Count == 0)
            {
                return false;
            }

            return FocusResultItem(0, focusState);
        }

        private bool FocusResultItem(int index, FocusState focusState)
        {
            if (index < 0 || index >= ResultsGrid.Items.Count)
            {
                return false;
            }

            var itemData = ResultsGrid.Items[index];
            ResultsGrid.UpdateLayout();
            ResultsGrid.ScrollIntoView(itemData);
            ResultsGrid.UpdateLayout();

            var item = ResultsGrid.ContainerFromIndex(index) as Control;
            if (item != null && item.Focus(focusState))
            {
                return true;
            }

            return ResultsGrid.Focus(focusState);
        }

        private bool FocusSelectedScope(FocusState focusState)
        {
            foreach (var button in _scopeButtons)
            {
                var scope = button.Tag as EmbySearchScope;
                if (scope != null &&
                    string.Equals(scope.Key, _selectedScopeKey, StringComparison.OrdinalIgnoreCase))
                {
                    return button.Focus(focusState);
                }
            }

            return _scopeButtons.Count > 0 && _scopeButtons[0].Focus(focusState);
        }

        private bool MoveScopeFocus(int delta)
        {
            int index;
            if (!TryGetFocusedScopeIndex(out index))
            {
                return false;
            }

            return FocusScope(index + delta, FocusState.Keyboard);
        }

        private bool FocusScope(int index, FocusState focusState)
        {
            if (_scopeButtons.Count == 0)
            {
                return false;
            }

            var clampedIndex = Math.Max(0, Math.Min(_scopeButtons.Count - 1, index));
            return _scopeButtons[clampedIndex].Focus(focusState);
        }

        private bool TryGetFocusedScopeIndex(out int index)
        {
            var focusedElement = FocusManager.GetFocusedElement();
            for (var i = 0; i < _scopeButtons.Count; i++)
            {
                if (IsFocusWithin(focusedElement, _scopeButtons[i]))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private bool TryGetFocusedResultIndex(out int index)
        {
            var current = FocusManager.GetFocusedElement() as DependencyObject;
            while (current != null)
            {
                var gridViewItem = current as GridViewItem;
                if (gridViewItem != null)
                {
                    index = ResultsGrid.IndexFromContainer(gridViewItem);
                    return index >= 0;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            index = -1;
            return false;
        }

        private int GetVisibleColumnCount()
        {
            var stride = ResultCardWidth + ResultCardTrailingMargin;
            var available = Math.Max(stride, ResultsGrid.ActualWidth + ResultCardTrailingMargin);
            return Math.Max(1, (int)Math.Floor(available / stride));
        }

        private bool CanApplySearch(int searchGeneration)
        {
            return !_isUnloaded && searchGeneration == _searchGeneration;
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

        private static bool IsDownKey(VirtualKey key)
        {
            return key == VirtualKey.Down ||
                key == VirtualKey.GamepadDPadDown ||
                key == VirtualKey.GamepadLeftThumbstickDown;
        }

        public sealed class SearchResultCard
        {
            public SearchResultCard(EmbyMediaItem item, BitmapImage? imageSource)
            {
                Item = item;
                ImageSource = imageSource;
                Title = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
                Meta = CreateMeta(item);
                Initials = CreateInitials(Title);
            }

            public EmbyMediaItem Item { get; }

            public string Title { get; }

            public string Meta { get; }

            public string Initials { get; }

            public BitmapImage? ImageSource { get; }
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
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            return value.Trim().Substring(0, 1).ToUpperInvariant();
        }
    }
}
