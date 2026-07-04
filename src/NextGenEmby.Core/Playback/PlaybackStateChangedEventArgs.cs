using System;

namespace NextGenEmby.Core.Playback
{
    public sealed class PlaybackStateChangedEventArgs : EventArgs
    {
        public PlaybackStateChangedEventArgs(PlaybackState state, string message = "")
        {
            State = state;
            Message = message ?? "";
        }

        public PlaybackState State { get; }

        public string Message { get; }
    }
}
