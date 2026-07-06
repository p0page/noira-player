namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentCommandStartupPolicy
    {
        public static DevelopmentCommandStartupDecision Decide(bool hasSession, bool hasCommandFile)
        {
            return new DevelopmentCommandStartupDecision(
                shouldNavigateHome: hasSession,
                shouldRunCommand: hasCommandFile);
        }
    }
}
