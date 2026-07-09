using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace NoiraPlayer.App.Views
{
    internal static class MatteButtonFocusVisuals
    {
        public static void PrepareListButton(Button? button)
        {
            if (button == null)
            {
                return;
            }

            button.UseSystemFocusVisuals = false;
            button.GotFocus -= ListButton_OnGotFocus;
            button.LostFocus -= ListButton_OnLostFocus;
            button.GotFocus += ListButton_OnGotFocus;
            button.LostFocus += ListButton_OnLostFocus;
            ApplyListButton(button, isFocused: button.FocusState != FocusState.Unfocused);
        }

        public static void PrepareCommandButton(Button? button)
        {
            if (button == null)
            {
                return;
            }

            button.UseSystemFocusVisuals = false;
            button.GotFocus -= CommandButton_OnGotFocus;
            button.LostFocus -= CommandButton_OnLostFocus;
            button.GotFocus += CommandButton_OnGotFocus;
            button.LostFocus += CommandButton_OnLostFocus;
            ApplyCommandButton(button, isFocused: button.FocusState != FocusState.Unfocused);
        }

        public static void PrepareDangerButton(Button? button)
        {
            if (button == null)
            {
                return;
            }

            button.UseSystemFocusVisuals = false;
            button.GotFocus -= DangerButton_OnGotFocus;
            button.LostFocus -= DangerButton_OnLostFocus;
            button.GotFocus += DangerButton_OnGotFocus;
            button.LostFocus += DangerButton_OnLostFocus;
            ApplyDangerButton(button, isFocused: button.FocusState != FocusState.Unfocused);
        }

        private static void ListButton_OnGotFocus(object sender, RoutedEventArgs e)
        {
            ApplyListButton(sender as Button, isFocused: true);
        }

        private static void ListButton_OnLostFocus(object sender, RoutedEventArgs e)
        {
            ApplyListButton(sender as Button, isFocused: false);
        }

        private static void CommandButton_OnGotFocus(object sender, RoutedEventArgs e)
        {
            ApplyCommandButton(sender as Button, isFocused: true);
        }

        private static void CommandButton_OnLostFocus(object sender, RoutedEventArgs e)
        {
            ApplyCommandButton(sender as Button, isFocused: false);
        }

        private static void DangerButton_OnGotFocus(object sender, RoutedEventArgs e)
        {
            ApplyDangerButton(sender as Button, isFocused: true);
        }

        private static void DangerButton_OnLostFocus(object sender, RoutedEventArgs e)
        {
            ApplyDangerButton(sender as Button, isFocused: false);
        }

        private static void ApplyListButton(Button? button, bool isFocused)
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

        private static void ApplyCommandButton(Button? button, bool isFocused)
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

        private static void ApplyDangerButton(Button? button, bool isFocused)
        {
            if (button == null)
            {
                return;
            }

            button.Background = isFocused
                ? BrushResource("AppDangerFocusedBrush")
                : BrushResource("AppDangerBrush");
            button.BorderBrush = BrushResource("AppTransparentBrush");
        }

        private static Brush BrushResource(string key)
        {
            return (Brush)Application.Current.Resources[key];
        }
    }
}
