using System;

namespace NextGenEmby.Core.Emby
{
    public static class EmbyRequestTimeoutPolicy
    {
        public static TimeSpan InteractiveRequestTimeout => TimeSpan.FromSeconds(12);
    }
}
