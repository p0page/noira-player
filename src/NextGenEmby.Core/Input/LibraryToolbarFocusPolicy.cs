namespace NextGenEmby.Core.Input
{
    public enum LibraryToolbarFocusDirection
    {
        Left,
        Right
    }

    public enum LibraryToolbarFocusTarget
    {
        None,
        Sort,
        Filter,
        Refresh
    }

    public static class LibraryToolbarFocusPolicy
    {
        public static LibraryToolbarFocusTarget Move(
            LibraryToolbarFocusTarget current,
            LibraryToolbarFocusDirection direction,
            bool sectionRequest)
        {
            if (sectionRequest)
            {
                return LibraryToolbarFocusTarget.None;
            }

            if (direction == LibraryToolbarFocusDirection.Right)
            {
                switch (current)
                {
                    case LibraryToolbarFocusTarget.Sort:
                        return LibraryToolbarFocusTarget.Filter;
                    case LibraryToolbarFocusTarget.Filter:
                        return LibraryToolbarFocusTarget.Refresh;
                    default:
                        return LibraryToolbarFocusTarget.None;
                }
            }

            switch (current)
            {
                case LibraryToolbarFocusTarget.Refresh:
                    return LibraryToolbarFocusTarget.Filter;
                case LibraryToolbarFocusTarget.Filter:
                    return LibraryToolbarFocusTarget.Sort;
                default:
                    return LibraryToolbarFocusTarget.None;
            }
        }
    }
}
