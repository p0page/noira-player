using System.Collections.Generic;

namespace NextGenEmby.Core.Input
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
        public HomeFocusTarget(HomeFocusZone zone, int index)
        {
            Zone = zone;
            Index = index < 0 ? 0 : index;
        }

        public HomeFocusZone Zone { get; }

        public int Index { get; }
    }

    public static class HomeFocusInputPolicy
    {
        public static HomeFocusTarget? Move(
            HomeFocusTarget? current,
            HomeFocusDirection direction,
            int libraryCount,
            int rowCount,
            IReadOnlyList<bool>? rowHasMore = null,
            int sectionCount = 0)
        {
            if (current == null)
            {
                return null;
            }

            switch (direction)
            {
                case HomeFocusDirection.Down:
                    return MoveDown(current, libraryCount, rowCount, sectionCount);
                case HomeFocusDirection.Up:
                    return MoveUp(current, libraryCount, sectionCount, rowHasMore);
                case HomeFocusDirection.Right:
                    return MoveRight(current, libraryCount, sectionCount);
                case HomeFocusDirection.Left:
                    return MoveLeft(current);
                default:
                    return null;
            }
        }

        private static HomeFocusTarget? MoveDown(HomeFocusTarget current, int libraryCount, int rowCount, int sectionCount)
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
                return new HomeFocusTarget(HomeFocusZone.Row, current.Index + 1);
            }

            return null;
        }

        private static HomeFocusTarget? MoveUp(
            HomeFocusTarget current,
            int libraryCount,
            int sectionCount,
            IReadOnlyList<bool>? rowHasMore)
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
                return MoveUpFromRowIndex(current.Index, libraryCount, sectionCount);
            }

            if (current.Zone != HomeFocusZone.Row)
            {
                return null;
            }

            return HasMore(rowHasMore, current.Index)
                ? new HomeFocusTarget(HomeFocusZone.RowMore, current.Index)
                : MoveUpFromRowIndex(current.Index, libraryCount, sectionCount);
        }

        private static HomeFocusTarget MoveUpFromRowIndex(int rowIndex, int libraryCount, int sectionCount)
        {
            if (rowIndex > 0)
            {
                return new HomeFocusTarget(HomeFocusZone.Row, rowIndex - 1);
            }

            if (sectionCount > 0)
            {
                return new HomeFocusTarget(HomeFocusZone.Section, 0);
            }

            return libraryCount > 0
                ? new HomeFocusTarget(HomeFocusZone.Library, 0)
                : new HomeFocusTarget(HomeFocusZone.HeroPlay, 0);
        }

        private static HomeFocusTarget? MoveRight(HomeFocusTarget current, int libraryCount, int sectionCount)
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

            return null;
        }

        private static bool HasMore(IReadOnlyList<bool>? rowHasMore, int rowIndex)
        {
            return rowHasMore != null &&
                rowIndex >= 0 &&
                rowIndex < rowHasMore.Count &&
                rowHasMore[rowIndex];
        }
    }
}
