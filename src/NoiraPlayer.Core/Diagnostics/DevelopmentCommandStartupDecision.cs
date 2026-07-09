namespace NoiraPlayer.Core.Diagnostics
{
    public readonly struct DevelopmentCommandStartupDecision
    {
        public DevelopmentCommandStartupDecision(bool shouldNavigateHome, bool shouldRunCommand)
        {
            ShouldNavigateHome = shouldNavigateHome;
            ShouldRunCommand = shouldRunCommand;
        }

        public bool ShouldNavigateHome { get; }

        public bool ShouldRunCommand { get; }
    }
}
