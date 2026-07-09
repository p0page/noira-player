using System;

namespace NoiraPlayer.Core.Emby
{
    public static class EmbyRequestTimeoutPolicy
    {
        public static TimeSpan InteractiveRequestTimeout => TimeSpan.FromSeconds(12);
    }
}
