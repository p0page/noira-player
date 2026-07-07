using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace NextGenEmby.App.Views
{
    internal static class PosterGridFocusVisuals
    {
        private const string PosterCardRootName = "PosterCardRoot";
        private const string PosterSelectedBackplateName = "PosterSelectedBackplate";
        private const string PosterArtworkFrameName = "PosterArtworkFrame";
        private const string PosterArtworkDimName = "PosterArtworkDim";
        private const string PosterFocusedItemScaleResourceKey = "TvPosterFocusedItemScale";
        private const double DefaultFocusedScale = 1.025d;

        public static void PrepareContainer(GridViewItem? container)
        {
            if (container == null)
            {
                return;
            }

            container.UseSystemFocusVisuals = false;
            container.GotFocus -= Container_OnGotFocus;
            container.LostFocus -= Container_OnLostFocus;
            container.GotFocus += Container_OnGotFocus;
            container.LostFocus += Container_OnLostFocus;
            ApplyFocus(container, isFocused: container.FocusState != FocusState.Unfocused);
        }

        private static void Container_OnGotFocus(object sender, RoutedEventArgs e)
        {
            ApplyFocus(sender as GridViewItem, isFocused: true);
        }

        private static void Container_OnLostFocus(object sender, RoutedEventArgs e)
        {
            ApplyFocus(sender as GridViewItem, isFocused: false);
        }

        private static void ApplyFocus(GridViewItem? container, bool isFocused)
        {
            if (container == null)
            {
                return;
            }

            var cardRoot = FindNamedDescendant<FrameworkElement>(container, PosterCardRootName);
            var scaleTarget = cardRoot ?? container;
            var transform = scaleTarget.RenderTransform as ScaleTransform;
            if (transform == null)
            {
                transform = new ScaleTransform();
                scaleTarget.RenderTransform = transform;
            }

            var scale = isFocused ? GetFocusedScale() : 1d;
            transform.ScaleX = scale;
            transform.ScaleY = scale;

            var backplate = FindNamedDescendant<FrameworkElement>(container, PosterSelectedBackplateName);
            if (backplate != null)
            {
                backplate.Opacity = isFocused ? 1d : 0d;
            }

            var artworkFrame = FindNamedDescendant<FrameworkElement>(container, PosterArtworkFrameName);
            if (artworkFrame != null)
            {
                artworkFrame.Opacity = isFocused ? 1d : 0.92d;
            }

            var artworkDim = FindNamedDescendant<FrameworkElement>(container, PosterArtworkDimName);
            if (artworkDim != null)
            {
                artworkDim.Opacity = isFocused ? 0.08d : 0.18d;
            }
        }

        private static double GetFocusedScale()
        {
            var resources = Application.Current.Resources;
            if (resources.ContainsKey(PosterFocusedItemScaleResourceKey) &&
                resources[PosterFocusedItemScaleResourceKey] is double value)
            {
                return value;
            }

            return DefaultFocusedScale;
        }

        private static T? FindNamedDescendant<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T element && element.Name == name)
                {
                    return element;
                }

                var nested = FindNamedDescendant<T>(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
