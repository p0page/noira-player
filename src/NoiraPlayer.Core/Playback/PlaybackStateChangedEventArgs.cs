using System;

namespace NoiraPlayer.Core.Playback
{
    public sealed class PlaybackStateChangedEventArgs : EventArgs
    {
        public PlaybackStateChangedEventArgs(PlaybackState state, string message = "", long? positionTicks = null)
        {
            State = state;
            Message = message ?? "";
            PositionTicks = positionTicks;
        }

        public PlaybackState State { get; }

        public string Message { get; }

        public long? PositionTicks { get; }
    }
}
