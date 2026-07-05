using System.Threading.Tasks;

namespace NextGenEmby.Core.Playback
{
    public interface IPlaybackStreamSwitchingBackend
    {
        Task SwitchAudioStreamAsync(int audioStreamIndex);

        Task SwitchSubtitleStreamAsync(int? subtitleStreamIndex);
    }
}
