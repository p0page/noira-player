namespace NoiraPlayer.Core.Input
{
    public static class MusicListFocusPolicy
    {
        public static int? GetVerticalTargetIndex(
            int currentIndex,
            int itemCount,
            bool moveDown,
            bool moveUp)
        {
            if (itemCount <= 0 || currentIndex < 0 || currentIndex >= itemCount)
            {
                return null;
            }

            if (moveDown && currentIndex + 1 < itemCount)
            {
                return currentIndex + 1;
            }

            if (moveUp && currentIndex > 0)
            {
                return currentIndex - 1;
            }

            return null;
        }
    }
}
