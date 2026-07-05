using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace NextGenEmby.App.Views
{
    public sealed partial class SearchPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private bool _isUnloaded;
        private bool _isNavigatingToDetails;
        private int _searchGeneration;

        public SearchPage()
        {
            InitializeComponent();
            Loaded += SearchPage_OnLoaded;
            Unloaded += SearchPage_OnUnloaded;
        }

        private void SearchPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = false;
            _isNavigatingToDetails = false;
            SearchBox.Focus(FocusState.Programmatic);
        }

        private void SearchPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            _searchGeneration++;
        }

        private async void Search_OnClick(object sender, RoutedEventArgs e)
        {
            await SearchAsync();
        }

        private async Task SearchAsync()
        {
            var searchGeneration = ++_searchGeneration;
            ResultsPanel.Children.Clear();
            _isNavigatingToDetails = false;

            var term = (SearchBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(term))
            {
                StatusBlock.Text = "Enter a title.";
                SearchBox.Focus(FocusState.Programmatic);
                return;
            }

            StatusBlock.Text = "Searching...";

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

                IReadOnlyList<EmbyMediaItem> items;
                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    items = await client.SearchItemsAsync(session, term, "Movie,Series,Episode");
                }

                if (!CanApplySearch(searchGeneration))
                {
                    return;
                }

                RenderResults(items);
            }
            catch
            {
                if (!CanApplySearch(searchGeneration))
                {
                    return;
                }

                ResultsPanel.Children.Clear();
                StatusBlock.Text = "Unable to search.";
                SearchBox.Focus(FocusState.Programmatic);
            }
        }

        private void RenderResults(IReadOnlyList<EmbyMediaItem> items)
        {
            ResultsPanel.Children.Clear();

            if (items == null || items.Count == 0)
            {
                StatusBlock.Text = "No results.";
                SearchBox.Focus(FocusState.Programmatic);
                return;
            }

            foreach (var item in items)
            {
                ResultsPanel.Children.Add(CreateResultButton(item));
            }

            StatusBlock.Text = items.Count + " results";
            var firstResult = ResultsPanel.Children.Count > 0 ? ResultsPanel.Children[0] as Control : null;
            if (firstResult != null)
            {
                firstResult.Focus(FocusState.Programmatic);
            }
        }

        private Button CreateResultButton(EmbyMediaItem item)
        {
            var title = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            var type = string.IsNullOrWhiteSpace(item.Type) ? "Item" : item.Type;

            var button = new Button
            {
                Height = 86,
                MinHeight = 86,
                Padding = new Thickness(20, 12, 20, 12),
                Tag = item,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                UseSystemFocusVisuals = true
            };
            button.Click += Result_OnClick;

            button.Content = new Grid
            {
                RowSpacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 24,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    },
                    new TextBlock
                    {
                        Text = type,
                        Margin = new Thickness(0, 34, 0, 0),
                        FontSize = 17,
                        Foreground = (Brush)Application.Current.Resources["AppMutedTextBrush"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    }
                }
            };

            return button;
        }

        private void Result_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button == null ? null : button.Tag as EmbyMediaItem;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            if (_isNavigatingToDetails)
            {
                return;
            }

            _isNavigatingToDetails = true;
            Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(item.Id, item.Name));
        }

        private bool CanApplySearch(int searchGeneration)
        {
            return !_isUnloaded && searchGeneration == _searchGeneration;
        }
    }
}
