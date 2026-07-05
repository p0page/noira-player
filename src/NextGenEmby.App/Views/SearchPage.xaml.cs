using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class SearchPage : Page
    {
        public SearchPage()
        {
            InitializeComponent();
            Loaded += SearchPage_OnLoaded;
        }

        private void SearchPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus(FocusState.Programmatic);
        }

        private void Search_OnClick(object sender, RoutedEventArgs e)
        {
            StatusBlock.Text = "Search implementation follows in a later task.";
            ResultsPanel.Children.Clear();
        }
    }
}
