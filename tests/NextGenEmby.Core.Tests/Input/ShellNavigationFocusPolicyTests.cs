using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input
{
    public sealed class ShellNavigationFocusPolicyTests
    {
        [Fact]
        public void Back_Navigation_To_Content_Page_Prefers_Content_Focus()
        {
            var target = ShellNavigationFocusPolicy.GetFocusTarget(
                isPlaybackPage: false,
                isBackNavigation: true,
                hasContentFocusTarget: true);

            Assert.Equal(ShellNavigationFocusTarget.Content, target);
        }

        [Fact]
        public void Normal_Navigation_Uses_Shell_Focus()
        {
            var target = ShellNavigationFocusPolicy.GetFocusTarget(
                isPlaybackPage: false,
                isBackNavigation: false,
                hasContentFocusTarget: true);

            Assert.Equal(ShellNavigationFocusTarget.Shell, target);
        }

        [Fact]
        public void Login_Navigation_Prefers_Content_Focus()
        {
            var target = ShellNavigationFocusPolicy.GetFocusTarget(
                ShellContentMode.Login,
                isBackNavigation: false,
                hasContentFocusTarget: true);

            Assert.Equal(ShellNavigationFocusTarget.Content, target);
        }

        [Fact]
        public void Media_Details_Navigation_Prefers_Content_Focus()
        {
            var target = ShellNavigationFocusPolicy.GetFocusTarget(
                ShellContentMode.MediaDetails,
                isBackNavigation: false,
                hasContentFocusTarget: true);

            Assert.Equal(ShellNavigationFocusTarget.Content, target);
        }

        [Fact]
        public void Playback_Navigation_Does_Not_Move_Focus_To_Shell()
        {
            var target = ShellNavigationFocusPolicy.GetFocusTarget(
                isPlaybackPage: true,
                isBackNavigation: false,
                hasContentFocusTarget: true);

            Assert.Equal(ShellNavigationFocusTarget.None, target);
        }

        [Fact]
        public void Photo_Viewer_Navigation_Prefers_Content_Focus()
        {
            var target = ShellNavigationFocusPolicy.GetFocusTarget(
                ShellContentMode.PhotoViewer,
                isBackNavigation: false,
                hasContentFocusTarget: true);

            Assert.Equal(ShellNavigationFocusTarget.Content, target);
        }

        [Fact]
        public void Playback_Mode_Navigation_Does_Not_Move_Focus_To_Shell()
        {
            var target = ShellNavigationFocusPolicy.GetFocusTarget(
                ShellContentMode.Playback,
                isBackNavigation: false,
                hasContentFocusTarget: true);

            Assert.Equal(ShellNavigationFocusTarget.None, target);
        }
    }
}
