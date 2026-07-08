namespace NextGenEmby.Core.Playback
{
    public enum ManualDirectStreamInput
    {
        Other,
        Accept
    }

    public enum ManualDirectStreamInitialFocusTarget
    {
        StreamUrlBox,
        StartButton
    }

    public static class ManualDirectStreamInputPolicy
    {
        public static bool ShouldStartFromTextBox(ManualDirectStreamInput input, bool canStart)
        {
            return canStart && input == ManualDirectStreamInput.Accept;
        }

        public static ManualDirectStreamInitialFocusTarget GetInitialFocusTarget(bool canStart)
        {
            return canStart
                ? ManualDirectStreamInitialFocusTarget.StartButton
                : ManualDirectStreamInitialFocusTarget.StreamUrlBox;
        }

        public static bool ShouldKeepInitialFocusPending(
            bool applied,
            bool pageLoaded,
            int attempts,
            int maxAttempts)
        {
            if (applied)
            {
                return false;
            }

            if (!pageLoaded)
            {
                return true;
            }

            return attempts < maxAttempts;
        }
    }
}
