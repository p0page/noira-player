using System.Collections.Generic;

namespace NoiraPlayer.Core.Input
{
    public enum HomeFocusDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public enum HomeFocusZone
    {
        HeroPlay,
        HeroDetails,
        Library,
        Section,
        Row,
        RowMore
    }

    public sealed class HomeFocusTarget
    {
        public HomeFocusTarget(HomeFocusZone zone, int index, int itemIndex = 0)
        {
            Zone = zone;
            Index = index < 0 ? 0 : index;
            ItemIndex = itemIndex < 0 ? 0 : itemIndex;
        }

        public HomeFocusZone Zone { get; }

        public int Index { get; }

        public int ItemIndex { get; }
    }

    public static class HomeFocusInputPolicy
    {
        public static HomeFocusTarget? Move(
            HomeFocusTarget? current,
            HomeFocusDirection direction,
            int libraryCount,
            int rowCount,
            IReadOnlyList<bool>? rowHasMore = null,
            int sectionCount = 0,
            IReadOnlyList<int>? rowItemCounts = null)
        {
            if (current == null)
            {
                return null;
            }

            switch (direction)
            {
                case HomeFocusDirection.Down:
                    return MoveDown(current, libraryCount, rowCount, sectionCount, rowItemCounts);
                case HomeFocusDirection.Up:
                    return MoveUp(current, libraryCount, sectionCount, rowHasMore, rowItemCounts);
                case HomeFocusDirection.Right:
                    return MoveRight(current, libraryCount, sectionCount, rowItemCounts);
                case HomeFocusDirection.Left:
                    return MoveLeft(current);
                default:
                    return null;
            }
        }

        private static HomeFocusTarget? MoveDown(
            HomeFocusTarget current,
            int libraryCount,
            int rowCount,
            int sectionCount,
            IReadOnlyList<int>? rowItemCounts)
        {
            if (current.Zone == HomeFocusZone.HeroPlay || current.Zone == HomeFocusZone.HeroDetails)
            {
                if (libraryCount > 0)
                {
                    return new HomeFocusTarget(HomeFocusZone.Library, 0);
                }

                if (sectionCount > 0)
                {
                    return new HomeFocusTarget(HomeFocusZone.Section, 0);
                }

                return rowCount > 0 ? new HomeFocusTarget(HomeFocusZone.Row, 0) : null;
            }

            if (current.Zone == HomeFocusZone.Library)
            {
                if (sectionCount > 0)
                {
                    return new HomeFocusTarget(HomeFocusZone.Section, 0);
                }

                return rowCount > 0 ? new HomeFocusTarget(HomeFocusZone.Row, 0) : null;
            }

            if (current.Zone == HomeFocusZone.Section)
            {
                return rowCount > 0 ? new HomeFocusTarget(HomeFocusZone.Row, 0) : null;
            }

            if (current.Zone == HomeFocusZone.RowMore)
            {
                return new HomeFocusTarget(HomeFocusZone.Row, current.Index);
            }

            if (current.Zone == HomeFocusZone.Row && current.Index < rowCount - 1)
            {
                return CreateRowTarget(current.Index + 1, current.ItemIndex, rowItemCounts);
            }

            return null;
        }

        private static HomeFocusTarget? MoveUp(
            HomeFocusTarget current,
            int libraryCount,
            int sectionCount,
            IReadOnlyList<bool>? rowHasMore,
            IReadOnlyList<int>? rowItemCounts)
        {
            if (current.Zone == HomeFocusZone.Library)
            {
                return new HomeFocusTarget(HomeFocusZone.HeroPlay, 0);
            }

            if (current.Zone == HomeFocusZone.Section)
            {
                return libraryCount > 0
                    ? new HomeFocusTarget(HomeFocusZone.Library, 0)
                    : new HomeFocusTarget(HomeFocusZone.HeroPlay, 0);
            }

            if (current.Zone == HomeFocusZone.RowMore)
            {
                return MoveUpFromRowIndex(current.Index, 0, libraryCount, sectionCount, rowItemCounts);
            }

            if (current.Zone != HomeFocusZone.Row)
            {
                return null;
            }

            return HasMore(rowHasMore, current.Index)
                ? new HomeFocusTarget(HomeFocusZone.RowMore, current.Index)
                : MoveUpFromRowIndex(current.Index, current.ItemIndex, libraryCount, sectionCount, rowItemCounts);
        }

        private static HomeFocusTarget MoveUpFromRowIndex(
            int rowIndex,
            int itemIndex,
            int libraryCount,
            int sectionCount,
            IReadOnlyList<int>? rowItemCounts)
        {
            if (rowIndex > 0)
            {
                return CreateRowTarget(rowIndex - 1, itemIndex, rowItemCounts);
            }

            if (sectionCount > 0)
            {
                return new HomeFocusTarget(HomeFocusZone.Section, 0);
            }

            return libraryCount > 0
                ? new HomeFocusTarget(HomeFocusZone.Library, 0)
                : new HomeFocusTarget(HomeFocusZone.HeroPlay, 0);
        }

        private static HomeFocusTarget? MoveRight(
            HomeFocusTarget current,
            int libraryCount,
            int sectionCount,
            IReadOnlyList<int>? rowItemCounts)
        {
            if (current.Zone == HomeFocusZone.HeroPlay)
            {
                return new HomeFocusTarget(HomeFocusZone.HeroDetails, 0);
            }

            if (current.Zone == HomeFocusZone.Library && current.Index < libraryCount - 1)
            {
                return new HomeFocusTarget(HomeFocusZone.Library, current.Index + 1);
            }

            if (current.Zone == HomeFocusZone.Section && current.Index < sectionCount - 1)
            {
                return new HomeFocusTarget(HomeFocusZone.Section, current.Index + 1);
            }

            if (current.Zone == HomeFocusZone.Row && current.ItemIndex < GetRowItemCount(rowItemCounts, current.Index) - 1)
            {
                return new HomeFocusTarget(HomeFocusZone.Row, current.Index, current.ItemIndex + 1);
            }

            return null;
        }

        private static HomeFocusTarget? MoveLeft(HomeFocusTarget current)
        {
            if (current.Zone == HomeFocusZone.HeroDetails)
            {
                return new HomeFocusTarget(HomeFocusZone.HeroPlay, 0);
            }

            if (current.Zone == HomeFocusZone.Library && current.Index > 0)
            {
                return new HomeFocusTarget(HomeFocusZone.Library, current.Index - 1);
            }

            if (current.Zone == HomeFocusZone.Section && current.Index > 0)
            {
                return new HomeFocusTarget(HomeFocusZone.Section, current.Index - 1);
            }

            if (current.Zone == HomeFocusZone.Row && current.ItemIndex > 0)
            {
                return new HomeFocusTarget(HomeFocusZone.Row, current.Index, current.ItemIndex - 1);
            }

            return null;
        }

        private static bool HasMore(IReadOnlyList<bool>? rowHasMore, int rowIndex)
        {
            return rowHasMore != null &&
                rowIndex >= 0 &&
                rowIndex < rowHasMore.Count &&
                rowHasMore[rowIndex];
        }

        private static int GetRowItemCount(IReadOnlyList<int>? rowItemCounts, int rowIndex)
        {
            if (rowItemCounts == null ||
                rowIndex < 0 ||
                rowIndex >= rowItemCounts.Count)
            {
                return 1;
            }

            return rowItemCounts[rowIndex] < 1 ? 1 : rowItemCounts[rowIndex];
        }

        private static HomeFocusTarget CreateRowTarget(
            int rowIndex,
            int requestedItemIndex,
            IReadOnlyList<int>? rowItemCounts)
        {
            var maxItemIndex = GetRowItemCount(rowItemCounts, rowIndex) - 1;
            var itemIndex = requestedItemIndex > maxItemIndex ? maxItemIndex : requestedItemIndex;
            return new HomeFocusTarget(HomeFocusZone.Row, rowIndex, itemIndex);
        }
    }
}
