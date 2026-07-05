using NextGenEmby.App.Navigation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var request = e.Parameter as LibraryNavigationRequest;
            if (request == null || string.IsNullOrWhiteSpace(request.Title))
            {
                TitleBlock.Text = "Library";
                return;
            }

            TitleBlock.Text = request.Title;
        }
    }
}
