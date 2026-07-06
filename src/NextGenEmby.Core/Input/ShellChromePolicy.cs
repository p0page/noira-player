namespace NextGenEmby.Core.Input
{
    public enum ShellContentMode
    {
        Standard,
        Login,
        MediaDetails,
        Playback,
        PhotoViewer
    }

    public sealed class ShellChromeDecision
    {
        public ShellChromeDecision(
            bool isGuideVisible,
            bool isContentImmersive,
            bool blocksGlobalBack,
            bool suppressGuideNavigation)
        {
            IsGuideVisible = isGuideVisible;
            IsContentImmersive = isContentImmersive;
            BlocksGlobalBack = blocksGlobalBack;
            SuppressGuideNavigation = suppressGuideNavigation;
        }

        public bool IsGuideVisible { get; }

        public bool IsContentImmersive { get; }

        public bool BlocksGlobalBack { get; }

        public bool SuppressGuideNavigation { get; }
    }

    public static class ShellChromePolicy
    {
        public static ShellChromeDecision GetDecision(ShellContentMode mode)
        {
            if (mode == ShellContentMode.Playback)
            {
                return new ShellChromeDecision(
                    isGuideVisible: false,
                    isContentImmersive: true,
                    blocksGlobalBack: true,
                    suppressGuideNavigation: true);
            }

            if (mode == ShellContentMode.PhotoViewer)
            {
                return new ShellChromeDecision(
                    isGuideVisible: false,
                    isContentImmersive: true,
                    blocksGlobalBack: false,
                    suppressGuideNavigation: true);
            }

            return new ShellChromeDecision(
                isGuideVisible: true,
                isContentImmersive: false,
                blocksGlobalBack: false,
                suppressGuideNavigation: false);
        }
    }
}
