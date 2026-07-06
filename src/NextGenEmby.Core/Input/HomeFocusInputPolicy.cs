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
        Row
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
            int rowCount)
        {
            if (current == null)
            {
                return null;
            }

            switch (direction)
            {
                case HomeFocusDirection.Down:
                    return MoveDown(current, libraryCount, rowCount);
                case HomeFocusDirection.Up:
                    return MoveUp(current, libraryCount);
                case HomeFocusDirection.Right:
                    return MoveRight(current, libraryCount);
                case HomeFocusDirection.Left:
                    return MoveLeft(current);
                default:
                    return null;
            }
        }

        private static HomeFocusTarget? MoveDown(HomeFocusTarget current, int libraryCount, int rowCount)
        {
            if (current.Zone == HomeFocusZone.HeroPlay || current.Zone == HomeFocusZone.HeroDetails)
            {
                if (libraryCount > 0)
                {
                    return new HomeFocusTarget(HomeFocusZone.Library, 0);
                }

                return rowCount > 0 ? new HomeFocusTarget(HomeFocusZone.Row, 0) : null;
            }

            if (current.Zone == HomeFocusZone.Library)
            {
                return rowCount > 0 ? new HomeFocusTarget(HomeFocusZone.Row, 0) : null;
            }

            if (current.Zone == HomeFocusZone.Row && current.Index < rowCount - 1)
            {
                return new HomeFocusTarget(HomeFocusZone.Row, current.Index + 1);
            }

            return null;
        }

        private static HomeFocusTarget? MoveUp(HomeFocusTarget current, int libraryCount)
        {
            if (current.Zone == HomeFocusZone.Library)
            {
                return new HomeFocusTarget(HomeFocusZone.HeroPlay, 0);
            }

            if (current.Zone != HomeFocusZone.Row)
            {
                return null;
            }

            if (current.Index > 0)
            {
                return new HomeFocusTarget(HomeFocusZone.Row, current.Index - 1);
            }

            return libraryCount > 0
                ? new HomeFocusTarget(HomeFocusZone.Library, 0)
                : new HomeFocusTarget(HomeFocusZone.HeroPlay, 0);
        }

        private static HomeFocusTarget? MoveRight(HomeFocusTarget current, int libraryCount)
        {
            if (current.Zone == HomeFocusZone.HeroPlay)
            {
                return new HomeFocusTarget(HomeFocusZone.HeroDetails, 0);
            }

            if (current.Zone == HomeFocusZone.Library && current.Index < libraryCount - 1)
            {
                return new HomeFocusTarget(HomeFocusZone.Library, current.Index + 1);
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

            return null;
        }
    }
}
