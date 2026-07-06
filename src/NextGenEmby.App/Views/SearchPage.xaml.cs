using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Diagnostics;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Input;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class SearchPage : Page, ITvContentFocusTarget
    {
        private const string PosterCardWidthResourceKey = "TvPosterCardWidth";
        private const string PosterGridItemMarginResourceKey = "TvPosterGridItemMargin";
        private const double FallbackPosterCardWidth = 168d;
        private const double FallbackPosterCardTrailingMargin = 14d;
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private readonly RecentSearchTermStore _recentSearchTermStore = new RecentSearchTermStore();
        private readonly List<Button> _scopeButtons = new List<Button>();
        private readonly List<Button> _recentTermButtons = new List<Button>();
        private bool _isUnloaded;
        private bool _isNavigatingToDetails;
        private int _searchGeneration;
        private string _selectedScopeKey = "all";
#if DEBUG
        private static readonly IReadOnlyDictionary<string, string> DevelopmentSearchArtworkUris =
            DevelopmentSearchFixture.CreateArtworkUris();
        private SearchDevelopmentNavigationRequest? _developmentRequest;
        private IReadOnlyList<string>? _developmentRecentTerms;
#endif

        public SearchPage()
        {
            InitializeComponent();
            NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            AddHandler(KeyDownEvent, new KeyEventHandler(Page_OnKeyDown), true);
            Loaded += SearchPage_OnLoaded;
            Unloaded += SearchPage_OnUnloaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
#if DEBUG
            _developmentRequest = e.Parameter as SearchDevelopmentNavigationRequest;
            if (_developmentRequest != null)
            {
                _selectedScopeKey = EmbySearchScopePolicy.GetScope(_developmentRequest.InitialScopeKey).Key;
            }
#endif
        }

        private void SearchPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = false;
            _isNavigatingToDetails = false;
            EnsureScopeButtons();
            ApplyScopeButtonState();
            RenderRecentTerms();
#if DEBUG
            if (_developmentRequest != null && _developmentRequest.SimulateError)
            {
                SearchBox.Text = _developmentRequest.Term;
                RenderDevelopmentSearchError();
                return;
            }

            if (_developmentRequest != null && _developmentRequest.UseFixtureResults)
            {
                SearchBox.Text = _developmentRequest.Term;
                RenderDevelopmentSearchFixtureResults(SearchCompletionFocusTarget.SearchBox);
                return;
            }
#endif
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

            if (IsEmptyStateVisible() && FocusEmptyState(FocusState.Keyboard))
            {
                return true;
            }

            return SearchBox.Focus(FocusState.Keyboard);
        }

        private async void Search_OnClick(object sender, RoutedEventArgs e)
        {
            await SearchAsync();
        }

        private enum SearchCompletionFocusTarget
        {
            SearchBox,
            SelectedScope,
            FirstResult
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
                return;
            }
        }

        private void Page_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (TryMoveBetweenEmptyStateActions(e.Key))
            {
                e.Handled = true;
                return;
            }

            if (TryRouteSearchDirectionalKey(e.Key, e.OriginalSource, e.Handled))
            {
                e.Handled = true;
            }
        }

        private bool TryRouteSearchDirectionalKey(
            VirtualKey key,
            object originalSource,
            bool eventAlreadyHandled)
        {
            var focusedElement = FocusManager.GetFocusedElement();
            var focusArea = GetSearchFocusArea(originalSource, focusedElement);
            int focusedResultIndex;
            var focusedResultInFirstRow =
                TryGetFocusedResultIndex(out focusedResultIndex) &&
                focusedResultIndex < GetVisibleColumnCount();
            var decision = SearchFocusNavigationPolicy.GetDecision(
                eventAlreadyHandled,
                focusArea,
                IsUpKey(key),
                IsDownKey(key),
                IsLeftKey(key),
                IsRightKey(key),
                focusedResultInFirstRow,
                IsEmptyStateVisible(),
                IsRecentTermsVisible());

            switch (decision.Action)
            {
                case SearchFocusNavigationAction.FocusSearchBox:
                    return SearchBox.Focus(FocusState.Keyboard);

                case SearchFocusNavigationAction.FocusSelectedScope:
                    return FocusSelectedScope(FocusState.Keyboard);

                case SearchFocusNavigationAction.FocusRecentTerms:
                    return FocusFirstRecentTerm(FocusState.Keyboard);

                case SearchFocusNavigationAction.FocusFirstResult:
                    return FocusFirstResultNow(FocusState.Keyboard);

                case SearchFocusNavigationAction.FocusEmptyState:
                    return FocusEmptyState(FocusState.Keyboard);

                case SearchFocusNavigationAction.MoveScopeLeft:
                    return MoveScopeFocus(-1);

                case SearchFocusNavigationAction.MoveScopeRight:
                    return MoveScopeFocus(1);

                case SearchFocusNavigationAction.MoveRecentLeft:
                    return MoveRecentTermFocus(-1);

                case SearchFocusNavigationAction.MoveRecentRight:
                    return MoveRecentTermFocus(1);

                default:
                    return false;
            }
        }

        private SearchFocusArea GetSearchFocusArea(object originalSource, object focusedElement)
        {
            var originalArea = GetSearchFocusArea(originalSource);
            return originalArea == SearchFocusArea.Other
                ? GetSearchFocusArea(focusedElement)
                : originalArea;
        }

        private SearchFocusArea GetSearchFocusArea(object element)
        {
            if (IsFocusWithin(element, SearchBox))
            {
                return SearchFocusArea.SearchBox;
            }

            if (IsFocusWithin(element, SearchActionButton))
            {
                return SearchFocusArea.SearchAction;
            }

            if (IsFocusWithin(element, ScopesPanel))
            {
                return SearchFocusArea.ScopeRail;
            }

            if (IsFocusWithin(element, RecentSearchesPanel))
            {
                return SearchFocusArea.RecentTerms;
            }

            if (IsFocusWithin(element, ResultsGrid))
            {
                return SearchFocusArea.ResultGrid;
            }

            if (IsFocusWithin(element, EmptyStatePanel))
            {
                return SearchFocusArea.EmptyState;
            }

            return SearchFocusArea.Other;
        }

        private async Task SearchAsync(
            SearchCompletionFocusTarget completionFocusTarget = SearchCompletionFocusTarget.SearchBox)
        {
            var searchGeneration = ++_searchGeneration;
            ResultsGrid.Items.Clear();
            HideEmptyState();
            _isNavigatingToDetails = false;

            var term = (SearchBox.Text ?? "").Trim();
            var scope = EmbySearchScopePolicy.GetScope(_selectedScopeKey);
            if (string.IsNullOrWhiteSpace(term))
            {
                StatusBlock.Text = "Enter a search.";
                FocusAfterSearch(completionFocusTarget);
                return;
            }

            SaveRecentSearchTerm(term);

#if DEBUG
            if (_developmentRequest != null && _developmentRequest.SimulateError)
            {
                RenderDevelopmentSearchError();
                return;
            }

            if (_developmentRequest != null && _developmentRequest.UseFixtureResults)
            {
                RenderDevelopmentSearchFixtureResults(completionFocusTarget);
                return;
            }
#endif

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
                    FocusAfterSearch(completionFocusTarget);
                    return;
                }

                IReadOnlyList<SearchResultCard> cards;
                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    var items = await InteractiveRequestGuard.WithTimeoutAsync(
                        client.SearchItemsAsync(session, term, scope.IncludeItemTypes),
                        EmbyRequestTimeoutPolicy.InteractiveRequestTimeout);
                    var scopedItems = scope.RequireItemTypeMatch
                        ? EmbyLibraryItemTypePolicy.KeepIncludedItemTypes(items, scope.IncludeItemTypes)
                        : items;
                    cards = CreateResultCards(session, client, scopedItems);
                }

                if (!CanApplySearch(searchGeneration))
                {
                    return;
                }

                RenderResults(scope, cards, completionFocusTarget);
            }
            catch
            {
                if (!CanApplySearch(searchGeneration))
                {
                    return;
                }

                ResultsGrid.Items.Clear();
                StatusBlock.Text = "Unable to search.";
                ShowEmptyState(
                    "Unable to search",
                    "Check the server connection, then try again.",
                    showRetry: true);
                FocusAfterSearch(completionFocusTarget);
            }
        }

        private void RenderResults(
            EmbySearchScope scope,
            IReadOnlyList<SearchResultCard> cards,
            SearchCompletionFocusTarget completionFocusTarget)
        {
            ResultsGrid.Items.Clear();

            if (cards == null || cards.Count == 0)
            {
                StatusBlock.Text = scope.Key == "all"
                    ? "No results."
                    : "No results in " + scope.Label + ".";
                ShowEmptyState(
                    StatusBlock.Text.TrimEnd('.'),
                    "Try a different title, person, playlist, or song.",
                    showRetry: false);
                FocusAfterSearch(completionFocusTarget);
                return;
            }

            HideEmptyState();
            foreach (var card in cards)
            {
                ResultsGrid.Items.Add(card);
            }

            StatusBlock.Text = SearchResultStatusTextPolicy.Create(cards.Count, scope.Label);
            FocusAfterSearch(completionFocusTarget);
        }

#if DEBUG
        private void RenderDevelopmentSearchError()
        {
            ResultsGrid.Items.Clear();
            _isNavigatingToDetails = false;
            StatusBlock.Text = "Unable to search.";
            ShowEmptyState(
                "Unable to search",
                "Check the server connection, then try again.",
                showRetry: true);
            SearchBox.Focus(FocusState.Programmatic);
        }

        private void RenderDevelopmentSearchFixtureResults(SearchCompletionFocusTarget completionFocusTarget)
        {
            _isNavigatingToDetails = false;
            var scope = EmbySearchScopePolicy.GetScope(_selectedScopeKey);
            var items = DevelopmentSearchFixture.CreateItemsForScope(scope.Key);
            var cards = CreateDevelopmentResultCards(items);
            RenderResults(scope, cards, completionFocusTarget);
        }

        private static IReadOnlyList<SearchResultCard> CreateDevelopmentResultCards(
            IReadOnlyList<EmbyMediaItem> items)
        {
            var cards = new List<SearchResultCard>();
            foreach (var item in items)
            {
                cards.Add(new SearchResultCard(item, CreateDevelopmentArtworkImageSource(item)));
            }

            return cards;
        }

        private static BitmapImage? CreateDevelopmentArtworkImageSource(EmbyMediaItem item)
        {
            var key = DevelopmentSearchFixture.ArtworkKey(item.Id, "Primary");
            if (!DevelopmentSearchArtworkUris.TryGetValue(key, out var uri) ||
                string.IsNullOrWhiteSpace(uri))
            {
                return null;
            }

            return new BitmapImage(new Uri(uri));
        }
#endif

        private void FocusAfterSearch(SearchCompletionFocusTarget completionFocusTarget)
        {
            if (completionFocusTarget == SearchCompletionFocusTarget.SelectedScope &&
                FocusSelectedScope(FocusState.Keyboard))
            {
                return;
            }

            if (completionFocusTarget == SearchCompletionFocusTarget.FirstResult)
            {
                if (FocusFirstResultNow(FocusState.Keyboard) ||
                    FocusEmptyState(FocusState.Keyboard))
                {
                    return;
                }
            }

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
                button.GotFocus += ScopeButton_OnGotFocus;
                _scopeButtons.Add(button);
                ScopesPanel.Children.Add(button);
            }
        }

        private static void ScopeButton_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var target = sender as Control;
            if (target == null)
            {
                return;
            }

            target.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true,
                HorizontalAlignmentRatio = 0.5,
                HorizontalOffset = 0,
                VerticalAlignmentRatio = 0.0
            });
        }

        private IReadOnlyList<string> LoadRecentTerms()
        {
#if DEBUG
            if (_developmentRequest != null)
            {
                return _developmentRecentTerms ??
                    (_developmentRecentTerms = SearchRecentTermsPolicy.Add(
                        _developmentRequest.RecentTerms,
                        ""));
            }
#endif
            return _recentSearchTermStore.Load();
        }

        private void SaveRecentSearchTerm(string term)
        {
#if DEBUG
            if (_developmentRequest != null)
            {
                _developmentRecentTerms = SearchRecentTermsPolicy.Add(LoadRecentTerms(), term);
                RenderRecentTerms();
                return;
            }
#endif
            _recentSearchTermStore.Add(term);
            RenderRecentTerms();
        }

        private void RenderRecentTerms()
        {
            _recentTermButtons.Clear();
            RecentSearchTermsPanel.Children.Clear();

            var terms = LoadRecentTerms();
            if (terms.Count == 0)
            {
                RecentSearchesPanel.Visibility = Visibility.Collapsed;
                return;
            }

            foreach (var term in terms)
            {
                var button = new Button
                {
                    Content = term,
                    Tag = term,
                    MinHeight = 44,
                    MinWidth = 112,
                    MaxWidth = 260,
                    Padding = new Thickness(18, 7, 18, 7),
                    UseSystemFocusVisuals = true
                };
                AutomationProperties.SetName(button, "Recent search " + term);
                button.Click += RecentTerm_OnClick;
                button.GotFocus += RecentTerm_OnGotFocus;
                _recentTermButtons.Add(button);
                RecentSearchTermsPanel.Children.Add(button);
            }

            RecentSearchesPanel.Visibility = Visibility.Visible;
        }

        private static void RecentTerm_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var target = sender as Control;
            if (target == null)
            {
                return;
            }

            target.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true,
                HorizontalAlignmentRatio = 0.5,
                HorizontalOffset = 0,
                VerticalAlignmentRatio = 0.0
            });
        }

        private async void RecentTerm_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var term = button == null ? null : button.Tag as string;
            if (string.IsNullOrWhiteSpace(term))
            {
                return;
            }

            SearchBox.Text = term;
            await SearchAsync(SearchCompletionFocusTarget.FirstResult);
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

            await SearchAsync(SearchCompletionFocusTarget.SelectedScope);
        }

        private void EmptyEdit_OnClick(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus(FocusState.Keyboard);
            SearchBox.SelectAll();
        }

        private async void EmptyRetry_OnClick(object sender, RoutedEventArgs e)
        {
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

        private bool FocusFirstRecentTerm(FocusState focusState)
        {
            return _recentTermButtons.Count > 0 && _recentTermButtons[0].Focus(focusState);
        }

        private bool FocusEmptyState(FocusState focusState)
        {
            if (!IsEmptyStateVisible())
            {
                return false;
            }

            return EmptyEditButton.Focus(focusState) ||
                EmptyRetryButton.Focus(focusState);
        }

        private bool TryMoveBetweenEmptyStateActions(VirtualKey key)
        {
            var focusedElement = FocusManager.GetFocusedElement();
            if (IsRightKey(key) && IsFocusWithin(focusedElement, EmptyEditButton))
            {
                return EmptyRetryButton.Focus(FocusState.Keyboard);
            }

            if (IsLeftKey(key) && IsFocusWithin(focusedElement, EmptyRetryButton))
            {
                return EmptyEditButton.Focus(FocusState.Keyboard);
            }

            return false;
        }

        private bool IsEmptyStateVisible()
        {
            return EmptyStatePanel.Visibility == Visibility.Visible;
        }

        private bool IsRecentTermsVisible()
        {
            return RecentSearchesPanel.Visibility == Visibility.Visible &&
                _recentTermButtons.Count > 0;
        }

        private void ShowEmptyState(string title, string body, bool showRetry)
        {
            ResultsGrid.Visibility = Visibility.Collapsed;
            EmptyTitleBlock.Text = title ?? "No results";
            EmptyBodyBlock.Text = body ?? "";
            EmptyRetryButton.Visibility = showRetry ? Visibility.Visible : Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Visible;
        }

        private void HideEmptyState()
        {
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ResultsGrid.Visibility = Visibility.Visible;
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

        private bool MoveRecentTermFocus(int delta)
        {
            int index;
            if (!TryGetFocusedRecentTermIndex(out index))
            {
                return false;
            }

            return FocusRecentTerm(index + delta, FocusState.Keyboard);
        }

        private bool FocusRecentTerm(int index, FocusState focusState)
        {
            if (_recentTermButtons.Count == 0)
            {
                return false;
            }

            var clampedIndex = Math.Max(0, Math.Min(_recentTermButtons.Count - 1, index));
            return _recentTermButtons[clampedIndex].Focus(focusState);
        }

        private bool TryGetFocusedRecentTermIndex(out int index)
        {
            var focusedElement = FocusManager.GetFocusedElement();
            for (var i = 0; i < _recentTermButtons.Count; i++)
            {
                if (IsFocusWithin(focusedElement, _recentTermButtons[i]))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
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
            var trailingMargin = GetPosterGridTrailingMargin();
            var stride = GetPosterCardWidth() + trailingMargin;
            var available = Math.Max(stride, ResultsGrid.ActualWidth + trailingMargin);
            return Math.Max(1, (int)Math.Floor(available / stride));
        }

        private static double GetPosterCardWidth()
        {
            return GetDoubleResource(PosterCardWidthResourceKey, FallbackPosterCardWidth);
        }

        private static double GetPosterGridTrailingMargin()
        {
            var resources = Application.Current.Resources;
            if (resources.ContainsKey(PosterGridItemMarginResourceKey) &&
                resources[PosterGridItemMarginResourceKey] is Thickness margin)
            {
                return margin.Right;
            }

            return FallbackPosterCardTrailingMargin;
        }

        private static double GetDoubleResource(string key, double fallback)
        {
            var resources = Application.Current.Resources;
            if (resources.ContainsKey(key) && resources[key] is double value)
            {
                return value;
            }

            return fallback;
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

        private static bool IsUpKey(VirtualKey key)
        {
            return key == VirtualKey.Up ||
                key == VirtualKey.GamepadDPadUp ||
                key == VirtualKey.GamepadLeftThumbstickUp;
        }

        private static bool IsLeftKey(VirtualKey key)
        {
            return key == VirtualKey.Left ||
                key == VirtualKey.GamepadDPadLeft ||
                key == VirtualKey.GamepadLeftThumbstickLeft;
        }

        private static bool IsRightKey(VirtualKey key)
        {
            return key == VirtualKey.Right ||
                key == VirtualKey.GamepadDPadRight ||
                key == VirtualKey.GamepadLeftThumbstickRight;
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
