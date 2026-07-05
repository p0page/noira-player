namespace NextGenEmby.Core.Input
{
    public static class GlobalBackInputPolicy
    {
        public static bool ShouldGoBack(
            bool eventAlreadyHandled,
            bool playbackPageActive,
            bool frameCanGoBack,
            bool backKeyPressed)
        {
            return !eventAlreadyHandled &&
                !playbackPageActive &&
                frameCanGoBack &&
                backKeyPressed;
        }
    }
}
