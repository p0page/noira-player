namespace NextGenEmby.Core.Playback
{
    public enum ManualDirectStreamInput
    {
        Other,
        Accept
    }

    public static class ManualDirectStreamInputPolicy
    {
        public static bool ShouldStartFromTextBox(ManualDirectStreamInput input, bool canStart)
        {
            return canStart && input == ManualDirectStreamInput.Accept;
        }
    }
}
