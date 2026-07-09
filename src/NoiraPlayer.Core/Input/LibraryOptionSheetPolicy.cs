using System;

namespace NoiraPlayer.Core.Input
{
    public sealed class LibraryOptionSheetDecision
    {
        public LibraryOptionSheetDecision(int index, bool shouldReload, bool shouldClose)
        {
            Index = index;
            ShouldReload = shouldReload;
            ShouldClose = shouldClose;
        }

        public int Index { get; }

        public bool ShouldReload { get; }

        public bool ShouldClose { get; }
    }

    public static class LibraryOptionSheetPolicy
    {
        public static LibraryOptionSheetDecision Open(int optionCount, int committedIndex)
        {
            return new LibraryOptionSheetDecision(
                ClampIndex(committedIndex, optionCount),
                shouldReload: false,
                shouldClose: false);
        }

        public static int MovePreviewIndex(int currentIndex, int optionCount, int offset)
        {
            return ClampIndex(currentIndex + offset, optionCount);
        }

        public static LibraryOptionSheetDecision Confirm(int committedIndex, int previewIndex, int optionCount)
        {
            var nextIndex = ClampIndex(previewIndex, optionCount);
            return new LibraryOptionSheetDecision(
                nextIndex,
                shouldReload: nextIndex != ClampIndex(committedIndex, optionCount),
                shouldClose: true);
        }

        public static LibraryOptionSheetDecision Cancel(int committedIndex, int optionCount)
        {
            return new LibraryOptionSheetDecision(
                ClampIndex(committedIndex, optionCount),
                shouldReload: false,
                shouldClose: true);
        }

        private static int ClampIndex(int index, int optionCount)
        {
            if (optionCount <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(optionCount - 1, index));
        }
    }
}
