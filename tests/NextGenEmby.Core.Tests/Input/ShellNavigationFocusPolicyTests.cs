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
        public void Playback_Navigation_Does_Not_Move_Focus_To_Shell()
        {
            var target = ShellNavigationFocusPolicy.GetFocusTarget(
                isPlaybackPage: true,
                isBackNavigation: false,
                hasContentFocusTarget: true);

            Assert.Equal(ShellNavigationFocusTarget.None, target);
        }
    }
}
