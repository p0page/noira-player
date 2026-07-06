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
    }
}
