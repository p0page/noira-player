using System.Collections.Generic;

namespace NoiraPlayer.Core.Emby
{
    public sealed class EmbyLiveTvInfo
    {
        public bool IsEnabled { get; set; }

        public IReadOnlyList<string> EnabledUserIds { get; set; } = new List<string>();
    }
}
