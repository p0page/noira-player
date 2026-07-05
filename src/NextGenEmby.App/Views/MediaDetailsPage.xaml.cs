using System;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class MediaDetailsPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private EmbyMediaItem? _item;

        public MediaDetailsPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _item = e.Parameter as EmbyMediaItem;
            RenderItem();
            await LoadImagesAsync();
        }

        private void RenderItem()
        {
            if (_item == null || string.IsNullOrWhiteSpace(_item.Id))
            {
                TitleBlock.Text = "Item unavailable";
                MetaBlock.Text = "";
                OverviewBlock.Text = "";
                StatusBlock.Text = "Go back and choose another item.";
                PlayButton.IsEnabled = false;
                return;
            }

            TitleBlock.Text = string.IsNullOrWhiteSpace(_item.Name) ? _item.Id : _item.Name;
            MetaBlock.Text = CreateMeta(_item);
            OverviewBlock.Text = string.IsNullOrWhiteSpace(_item.Overview)
                ? "No overview available."
                : _item.Overview;
            StatusBlock.Text = "";
            PlayButton.IsEnabled = true;
            PlayButton.Focus(FocusState.Programmatic);
        }

        private async Task LoadImagesAsync()
        {
            if (_item == null || string.IsNullOrWhiteSpace(_item.Id))
            {
                return;
            }

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (session == null)
                {
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                using (var http = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(http, session);
                    if (!string.IsNullOrWhiteSpace(_item.PrimaryImageTag))
                    {
                        PosterImage.Source = new BitmapImage(new Uri(client.GetImageUrl(session, _item.Id, "Primary", 720)));
                        PosterFallbackBlock.Visibility = Visibility.Collapsed;
                    }

                    if (!string.IsNullOrWhiteSpace(_item.BackdropImageTag))
                    {
                        BackdropImage.Source = new BitmapImage(new Uri(client.GetImageUrl(session, _item.Id, "Backdrop", 1920)));
                    }
                }
            }
            catch
            {
                StatusBlock.Text = "Unable to load artwork.";
            }
        }

        private void Play_OnClick(object sender, RoutedEventArgs e)
        {
            if (_item == null || string.IsNullOrWhiteSpace(_item.Id))
            {
                return;
            }

            Frame.Navigate(typeof(PlaybackPage), new PlaybackLaunchRequest(_item.Id, _item.Name));
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

            return meta;
        }
    }
}
