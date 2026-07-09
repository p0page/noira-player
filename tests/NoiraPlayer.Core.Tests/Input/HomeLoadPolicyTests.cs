using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input
{
    public sealed class HomeLoadPolicyTests
    {
        [Fact]
        public void PageLoaded_Starts_Clear_Load_When_No_Content_Has_Rendered()
        {
            var decision = HomeLoadPolicy.ForPageLoaded(
                hasRenderedContent: false,
                isLoading: false);

            Assert.True(decision.ShouldLoad);
            Assert.True(decision.ShouldClearExistingContent);
            Assert.False(decision.ShouldRestoreContentFocus);
            Assert.Equal("Loading...", decision.StatusText);
        }

        [Fact]
        public void PageLoaded_Does_Not_Reload_When_Cached_Content_Is_Visible()
        {
            var decision = HomeLoadPolicy.ForPageLoaded(
                hasRenderedContent: true,
                isLoading: false);

            Assert.False(decision.ShouldLoad);
            Assert.False(decision.ShouldClearExistingContent);
            Assert.True(decision.ShouldRestoreContentFocus);
            Assert.Equal("", decision.StatusText);
        }

        [Fact]
        public void Refresh_Preserves_Existing_Content_While_New_Data_Loads()
        {
            var decision = HomeLoadPolicy.ForRefreshRequested(hasRenderedContent: true);

            Assert.True(decision.ShouldLoad);
            Assert.False(decision.ShouldClearExistingContent);
            Assert.Equal("Refreshing...", decision.StatusText);
        }

        [Fact]
        public void Refresh_Failure_Keeps_Existing_Content_When_A_Previous_Model_Exists()
        {
            var failure = HomeLoadPolicy.ForLoadFailure(hasRenderedContent: true);

            Assert.False(failure.ShouldClearExistingContent);
            Assert.Equal("Unable to refresh home. Showing last loaded content.", failure.StatusText);
        }

        [Fact]
        public void Initial_Render_Focuses_Daily_Start()
        {
            var behavior = HomeLoadPolicy.ForRenderCompleted(
                hadRenderedContentBeforeRender: false,
                isSupplementalRender: false);

            Assert.Equal(HomeRenderFocusBehavior.FocusDailyStart, behavior);
        }

        [Fact]
        public void Supplemental_Render_Preserves_Existing_Focus()
        {
            var behavior = HomeLoadPolicy.ForRenderCompleted(
                hadRenderedContentBeforeRender: true,
                isSupplementalRender: true);

            Assert.Equal(HomeRenderFocusBehavior.RestoreExistingFocus, behavior);
        }
    }
}
