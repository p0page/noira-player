using System.Collections.Generic;

namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyLiveTvInfo
    {
        public bool IsEnabled { get; set; }

        public IReadOnlyList<string> EnabledUserIds { get; set; } = new List<string>();
    }
}
