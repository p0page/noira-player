using System;
using System.Collections.Generic;

namespace NextGenEmby.Core.Input
{
    public enum MediaDetailsActionButton
    {
        Play,
        Restart,
        Favorite,
        Watched,
        Refresh
    }

    public static class MediaDetailsActionNavigationPolicy
    {
        public static MediaDetailsActionButton? MoveHorizontal(
            MediaDetailsActionButton current,
            int delta,
            bool restartVisible)
        {
            if (delta == 0)
            {
                return null;
            }

            var actions = CreateVisibleActions(restartVisible);
            var index = actions.IndexOf(current);
            if (index < 0)
            {
                return null;
            }

            var nextIndex = index + Math.Sign(delta);
            if (nextIndex < 0 || nextIndex >= actions.Count)
            {
                return null;
            }

            return actions[nextIndex];
        }

        private static List<MediaDetailsActionButton> CreateVisibleActions(bool restartVisible)
        {
            var actions = new List<MediaDetailsActionButton>
            {
                MediaDetailsActionButton.Play
            };

            if (restartVisible)
            {
                actions.Add(MediaDetailsActionButton.Restart);
            }

            actions.Add(MediaDetailsActionButton.Favorite);
            actions.Add(MediaDetailsActionButton.Watched);
            actions.Add(MediaDetailsActionButton.Refresh);
            return actions;
        }
    }
}
