namespace NextGenEmby.Core.Input
{
    public static class PhotoViewerInputPolicy
    {
        public static bool ShouldGoBack(bool frameCanGoBack, bool backKeyPressed)
        {
            return frameCanGoBack && backKeyPressed;
        }
    }
}
