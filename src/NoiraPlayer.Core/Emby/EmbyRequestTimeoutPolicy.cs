using System;

namespace NoiraPlayer.Core.Emby
{
    public static class EmbyRequestTimeoutPolicy
    {
        public static TimeSpan InteractiveRequestTimeout => TimeSpan.FromSeconds(12);

        public static int InteractiveRequestMaxAttempts => 2;

        public static int RequiredInteractiveRequestMaxAttempts => 3;
    }
}
