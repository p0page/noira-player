using System.Threading.Tasks;

namespace NoiraPlayer.Core.Playback
{
    public interface IPlaybackStreamSwitchingBackend
    {
        Task SwitchAudioStreamAsync(int audioStreamIndex);

        Task SwitchSubtitleStreamAsync(int? subtitleStreamIndex);
    }
}
