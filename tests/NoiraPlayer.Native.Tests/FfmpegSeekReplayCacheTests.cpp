#include <cassert>
#include <cstdint>
#include <vector>

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavcodec/packet.h>
}
#pragma warning(pop)

#include "Media/FfmpegSeekReplayCache.h"

using winrt::NoiraPlayer::Native::implementation::FfmpegSeekReplayCache;
using winrt::NoiraPlayer::Native::implementation::FfmpegSeekReplayCacheLimits;

namespace
{
    constexpr int32_t VideoStream = 0;
    constexpr int32_t AudioStream = 1;
    constexpr int64_t Second = 10'000'000;

    AVPacket* Packet(int32_t streamIndex, int64_t marker, bool keyFrame = false, int32_t size = 32)
    {
        auto packet = av_packet_alloc();
        assert(packet != nullptr);
        assert(av_new_packet(packet, size) == 0);
        packet->stream_index = streamIndex;
        packet->flags = keyFrame ? AV_PKT_FLAG_KEY : 0;
        packet->data[0] = static_cast<uint8_t>(marker);
        return packet;
    }

    void Observe(
        FfmpegSeekReplayCache& cache,
        int32_t streamIndex,
        int64_t positionTicks,
        int64_t marker,
        bool keyFrame = false,
        int32_t size = 32)
    {
        auto packet = Packet(streamIndex, marker, keyFrame, size);
        cache.ObservePacket(packet, positionTicks);
        av_packet_free(&packet);
    }

    FfmpegSeekReplayCache Cache(FfmpegSeekReplayCacheLimits limits = {})
    {
        FfmpegSeekReplayCache cache(limits);
        cache.Configure(true, {VideoStream, AudioStream}, VideoStream);
        return cache;
    }
}

int main()
{
    {
        auto cache = Cache();
        Observe(cache, VideoStream, 0, 10, true);
        Observe(cache, AudioStream, 0, 20);
        Observe(cache, VideoStream, Second, 11);
        Observe(cache, AudioStream, Second, 21);
        Observe(cache, VideoStream, 2 * Second, 12);
        Observe(cache, AudioStream, 2 * Second, 22);

        auto replay = cache.TryBuildReplay(Second, 2 * Second);
        assert(replay.Hit);
        assert(replay.FallbackReason.empty());
        assert(replay.PacketCount == 6);
        assert(replay.Packets.size() == 6);
        assert(replay.Packets[0].StreamIndex == VideoStream);
        assert(replay.Packets[0].Packet->data[0] == 10);
        assert(replay.Packets[1].StreamIndex == AudioStream);
        assert(replay.Packets[5].Packet->data[0] == 22);
    }

    {
        auto cache = Cache();
        Observe(cache, VideoStream, 0, 10);
        Observe(cache, AudioStream, 0, 20);
        Observe(cache, VideoStream, Second, 11);
        Observe(cache, AudioStream, Second, 21);
        auto replay = cache.TryBuildReplay(Second, Second);
        assert(!replay.Hit);
        assert(replay.FallbackReason == "video-keyframe-missing");
    }

    {
        auto cache = Cache();
        Observe(cache, VideoStream, 0, 10, true);
        Observe(cache, VideoStream, Second, 11);
        auto replay = cache.TryBuildReplay(Second, Second);
        assert(!replay.Hit);
        assert(replay.FallbackReason == "stream-coverage-missing");
    }

    {
        auto cache = Cache();
        Observe(cache, VideoStream, 4 * Second, 10, true);
        Observe(cache, AudioStream, 4 * Second, 20);
        Observe(cache, VideoStream, 5 * Second, 11);
        Observe(cache, AudioStream, 5 * Second, 21);
        auto replay = cache.TryBuildReplay(3 * Second, 5 * Second);
        assert(!replay.Hit);
        assert(replay.FallbackReason == "target-outside-window");
    }

    {
        FfmpegSeekReplayCacheLimits limits;
        limits.MaxBytes = 160;
        limits.MaxPackets = 8;
        limits.MaxDurationTicks = 3 * Second;
        auto cache = Cache(limits);
        Observe(cache, VideoStream, 0, 10, true, 40);
        Observe(cache, AudioStream, 0, 20, false, 40);
        Observe(cache, VideoStream, Second, 11, false, 40);
        Observe(cache, AudioStream, Second, 21, false, 40);
        Observe(cache, VideoStream, 2 * Second, 12, true, 40);
        Observe(cache, AudioStream, 2 * Second, 22, false, 40);

        auto snapshot = cache.Snapshot();
        assert(snapshot.PacketCount == 2);
        assert(snapshot.Bytes == 80);
        assert(snapshot.WindowDurationTicks == 0);

        auto oldReplay = cache.TryBuildReplay(Second, 2 * Second);
        assert(!oldReplay.Hit);
        assert(oldReplay.FallbackReason == "target-outside-window");

        auto currentReplay = cache.TryBuildReplay(2 * Second, 2 * Second);
        assert(currentReplay.Hit);
        assert(currentReplay.PacketCount == 2);
        assert(currentReplay.Packets[0].Packet->data[0] == 12);
        assert(currentReplay.Packets[1].Packet->data[0] == 22);
    }

    {
        FfmpegSeekReplayCacheLimits limits;
        limits.MaxBytes = 1024;
        limits.MaxPackets = 4;
        limits.MaxDurationTicks = 10 * Second;
        auto cache = Cache(limits);
        Observe(cache, VideoStream, 0, 10, true);
        Observe(cache, AudioStream, 0, 20);
        Observe(cache, VideoStream, Second, 11);
        Observe(cache, AudioStream, Second, 21);
        Observe(cache, VideoStream, 2 * Second, 12, true);
        assert(cache.Snapshot().PacketCount == 1);
        Observe(cache, AudioStream, 2 * Second, 22);
        assert(cache.TryBuildReplay(2 * Second, 2 * Second).Hit);
    }

    {
        FfmpegSeekReplayCacheLimits limits;
        limits.MaxBytes = 1024;
        limits.MaxPackets = 32;
        limits.MaxDurationTicks = Second;
        auto cache = Cache(limits);
        Observe(cache, VideoStream, 0, 10, true);
        Observe(cache, AudioStream, 0, 20);
        Observe(cache, VideoStream, 2 * Second, 12, true);
        assert(cache.Snapshot().PacketCount == 1);
        Observe(cache, AudioStream, 2 * Second, 22);
        assert(cache.TryBuildReplay(2 * Second, 2 * Second).Hit);
    }

    {
        auto cache = Cache();
        Observe(cache, VideoStream, 0, 10, true);
        Observe(cache, AudioStream, 0, 20);
        cache.Configure(false, {VideoStream, AudioStream}, VideoStream);
        auto disabled = cache.TryBuildReplay(0, 0);
        assert(!disabled.Hit);
        assert(disabled.FallbackReason == "disabled");
        assert(cache.Snapshot().PacketCount == 0);

        cache.Configure(true, {VideoStream}, VideoStream);
        Observe(cache, VideoStream, 0, 30, true);
        auto videoOnly = cache.TryBuildReplay(0, 0);
        assert(videoOnly.Hit);
        assert(videoOnly.PacketCount == 1);
    }

    {
        auto cache = Cache();
        auto packet = Packet(VideoStream, 77, true);
        cache.ObservePacket(packet, 0);
        av_packet_free(&packet);
        Observe(cache, AudioStream, 0, 88);

        auto replay = cache.TryBuildReplay(0, 0);
        assert(replay.Hit);
        assert(replay.Packets[0].Packet->data[0] == 77);
        assert(replay.Packets[1].Packet->data[0] == 88);
    }

    return 0;
}
