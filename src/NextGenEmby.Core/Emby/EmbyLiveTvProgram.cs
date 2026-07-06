using System;

namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyLiveTvProgram
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public string EpisodeTitle { get; set; } = "";

        public string Overview { get; set; } = "";

        public string OfficialRating { get; set; } = "";

        public long? RunTimeTicks { get; set; }

        public DateTimeOffset StartDate { get; set; }

        public DateTimeOffset EndDate { get; set; }

        public bool IsMovie { get; set; }

        public bool IsSports { get; set; }

        public bool IsNews { get; set; }

        public bool IsKids { get; set; }

        public bool IsSeries { get; set; }

        public string ChannelId { get; set; } = "";
    }
}
