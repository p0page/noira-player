#include <cassert>
#include <chrono>
#include <cstdlib>
#include <cwchar>
#include <iostream>
#include <optional>
#include <string>
#include <thread>
#include <vector>

#include <winrt/base.h>

#include "DxDeviceResources.h"
#include "HdrDisplayRefreshRatePolicy.h"
#include "Media/PlaybackGraph.h"

using namespace std::chrono_literals;
using winrt::NoiraPlayer::Native::implementation::DxDeviceResources;
using winrt::NoiraPlayer::Native::implementation::HdrDisplayRefreshRatePolicy;
using winrt::NoiraPlayer::Native::implementation::PlaybackGraph;
using winrt::NoiraPlayer::Native::implementation::PlaybackGraphOpenRequest;

namespace
{
    constexpr int64_t SeekTargetPositionTicks = 10'000'000;
    constexpr wchar_t const* DefaultStreamUrl =
        L"https://repo.jellyfin.org/test-videos/SDR/HEVC%2010bit/Test%20Jellyfin%201080p%20HEVC%2010bit%203M.mp4";

    struct Options
    {
        std::wstring StreamUrl{DefaultStreamUrl};
        int DurationSeconds{3};
    };

    struct AudioSwitchOutcome
    {
        bool Attempted{false};
        std::string Status{"not-attempted"};
        int32_t StreamIndex{-1};
        int64_t PositionBeforeTicks{0};
        int64_t PositionAfterTicks{0};
        uint64_t SubmittedFramesBefore{0};
        uint64_t SubmittedFramesAfter{0};
    };

    struct SubtitleSwitchOutcome
    {
        bool Attempted{false};
        std::string Status{"not-attempted"};
        int32_t StreamIndex{-1};
        uint64_t CueCountBefore{0};
        uint64_t CueCountAfter{0};
    };

    struct SubtitleOffOutcome
    {
        bool Attempted{false};
        std::string Status{"not-attempted"};
        int32_t SelectedStreamIndex{-1};
    };

    struct SeekOutcome
    {
        bool Attempted{false};
        std::string Status{"not-attempted"};
        int64_t TargetPositionTicks{SeekTargetPositionTicks};
        std::optional<int64_t> ActualPositionTicks;
        int64_t PostSeekPlaybackPositionTicks{0};
    };

    Options ParseOptions(int argc, wchar_t** argv)
    {
        Options options;
        for (auto index = 1; index < argc; ++index)
        {
            if (std::wcscmp(argv[index], L"--stream-url") == 0 && index + 1 < argc)
            {
                options.StreamUrl = argv[++index];
            }
            else if (std::wcscmp(argv[index], L"--duration-seconds") == 0 && index + 1 < argc)
            {
                auto parsed = std::wcstol(argv[++index], nullptr, 10);
                if (parsed > 0 && parsed < 120)
                {
                    options.DurationSeconds = static_cast<int>(parsed);
                }
            }
        }

        return options;
    }

    char const* FormatDxgiColorSpace(DXGI_COLOR_SPACE_TYPE colorSpace)
    {
        switch (colorSpace)
        {
        case DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709:
            return "RGB_FULL_G22_NONE_P709";
        case DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020:
            return "RGB_FULL_G2084_NONE_P2020";
        case DXGI_COLOR_SPACE_RGB_STUDIO_G2084_NONE_P2020:
            return "RGB_STUDIO_G2084_NONE_P2020";
        case DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P2020:
            return "RGB_FULL_G22_NONE_P2020";
        case DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P2020:
            return "RGB_STUDIO_G22_NONE_P2020";
        case DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P709:
            return "RGB_STUDIO_G22_NONE_P709";
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020:
            return "YCBCR_STUDIO_G2084_LEFT_P2020";
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_TOPLEFT_P2020:
            return "YCBCR_STUDIO_G2084_TOPLEFT_P2020";
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_GHLG_TOPLEFT_P2020:
            return "YCBCR_STUDIO_GHLG_TOPLEFT_P2020";
        case DXGI_COLOR_SPACE_YCBCR_FULL_GHLG_TOPLEFT_P2020:
            return "YCBCR_FULL_GHLG_TOPLEFT_P2020";
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P2020:
            return "YCBCR_STUDIO_G22_LEFT_P2020";
        case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P2020:
            return "YCBCR_FULL_G22_LEFT_P2020";
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_TOPLEFT_P2020:
            return "YCBCR_STUDIO_G22_TOPLEFT_P2020";
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P601:
            return "YCBCR_STUDIO_G22_LEFT_P601";
        case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P601:
            return "YCBCR_FULL_G22_LEFT_P601";
        case DXGI_COLOR_SPACE_YCBCR_FULL_G22_NONE_P709_X601:
            return "YCBCR_FULL_G22_NONE_P709_X601";
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709:
            return "YCBCR_STUDIO_G22_LEFT_P709";
        case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P709:
            return "YCBCR_FULL_G22_LEFT_P709";
        case DXGI_COLOR_SPACE_CUSTOM:
            return "CUSTOM";
        default:
            return "UNKNOWN";
        }
    }
}

int wmain(int argc, wchar_t** argv)
{
    auto options = ParseOptions(argc, argv);
    winrt::init_apartment(winrt::apartment_type::multi_threaded);

    DxDeviceResources resources;
    resources.CreateSwapChain(1280, 720, false);
    assert(resources.HasRenderTarget());

    PlaybackGraph graph(resources);
    PlaybackGraphOpenRequest request{};
    request.DirectStreamUrl = options.StreamUrl;

    try
    {
        graph.Open(request);
        auto sampleWindow = std::chrono::milliseconds(options.DurationSeconds * 1000);
        auto halfWindow = sampleWindow / 2;
        if (halfWindow < 500ms)
        {
            halfWindow = 500ms;
        }

        std::this_thread::sleep_for(halfWindow);
        auto playbackSnapshot = graph.QualityMetricsSnapshot();
        auto source = graph.VideoSourceSnapshot();
        auto tracks = graph.SourceTrackSnapshots();

        std::vector<int32_t> audioStreamIndexes;
        std::vector<int32_t> subtitleStreamIndexes;
        std::optional<int32_t> selectedAudioStreamIndex;
        for (auto const& track : tracks)
        {
            if (track.Kind == "Audio")
            {
                audioStreamIndexes.push_back(track.StreamIndex);
                if (!selectedAudioStreamIndex.has_value() && track.IsDefault)
                {
                    selectedAudioStreamIndex = track.StreamIndex;
                }
            }
            else if (track.Kind == "Subtitle")
            {
                subtitleStreamIndexes.push_back(track.StreamIndex);
            }
        }

        if (!selectedAudioStreamIndex.has_value() && !audioStreamIndexes.empty())
        {
            selectedAudioStreamIndex = audioStreamIndexes.front();
        }

        graph.Pause();
        std::this_thread::sleep_for(100ms);
        graph.Resume();

        AudioSwitchOutcome audioSwitch;
        if (audioStreamIndexes.size() >= 2)
        {
            audioSwitch.Attempted = true;
            audioSwitch.StreamIndex = audioStreamIndexes[1];
            audioSwitch.PositionBeforeTicks = graph.CurrentPositionTicks();
            audioSwitch.SubmittedFramesBefore = graph.QualityMetricsSnapshot().SubmittedAudioFrames;
            try
            {
                graph.SwitchAudioStream(audioSwitch.StreamIndex);
                selectedAudioStreamIndex = audioSwitch.StreamIndex;
                std::this_thread::sleep_for(500ms);
                audioSwitch.PositionAfterTicks = graph.CurrentPositionTicks();
                audioSwitch.SubmittedFramesAfter = graph.QualityMetricsSnapshot().SubmittedAudioFrames;
                audioSwitch.Status =
                    audioSwitch.PositionAfterTicks > audioSwitch.PositionBeforeTicks &&
                    audioSwitch.SubmittedFramesAfter > audioSwitch.SubmittedFramesBefore
                        ? "completed"
                        : "failed";
            }
            catch (...)
            {
                audioSwitch.PositionAfterTicks = graph.CurrentPositionTicks();
                audioSwitch.SubmittedFramesAfter = graph.QualityMetricsSnapshot().SubmittedAudioFrames;
                audioSwitch.Status = "failed";
            }
        }

        auto runSubtitleSwitch = [&graph](int32_t streamIndex)
        {
            SubtitleSwitchOutcome outcome;
            outcome.Attempted = true;
            outcome.StreamIndex = streamIndex;
            outcome.CueCountBefore = graph.SubtitleCueRenderCount();
            try
            {
                graph.SwitchSubtitleStream(streamIndex);
                std::this_thread::sleep_for(500ms);
                outcome.CueCountAfter = graph.SubtitleCueRenderCount();
                outcome.Status = outcome.CueCountAfter > outcome.CueCountBefore
                    ? "completed"
                    : "failed";
            }
            catch (...)
            {
                outcome.CueCountAfter = graph.SubtitleCueRenderCount();
                outcome.Status = "failed";
            }

            return outcome;
        };

        SubtitleSwitchOutcome subtitleSwitch1;
        SubtitleSwitchOutcome subtitleSwitch2;
        if (!subtitleStreamIndexes.empty())
        {
            subtitleSwitch1 = runSubtitleSwitch(subtitleStreamIndexes[0]);
        }

        if (subtitleStreamIndexes.size() >= 2)
        {
            subtitleSwitch2 = runSubtitleSwitch(subtitleStreamIndexes[1]);
        }

        SubtitleOffOutcome subtitleOff;
        if (!subtitleStreamIndexes.empty())
        {
            subtitleOff.Attempted = true;
            try
            {
                graph.SwitchSubtitleStream(std::nullopt);
                auto selectedSubtitleStreamIndex = graph.SelectedSubtitleStreamIndex();
                subtitleOff.SelectedStreamIndex = selectedSubtitleStreamIndex.value_or(-1);
                subtitleOff.Status = selectedSubtitleStreamIndex.has_value()
                    ? "failed"
                    : "completed";
            }
            catch (...)
            {
                subtitleOff.SelectedStreamIndex = graph.SelectedSubtitleStreamIndex().value_or(-1);
                subtitleOff.Status = "failed";
            }
        }

        SeekOutcome seek;
        seek.Attempted = true;
        auto seekCallCompleted = false;
        auto seekPresentationBefore = graph.SeekPresentationSnapshot();
        try
        {
            graph.Seek(seek.TargetPositionTicks);
            auto seekPresentation = graph.SeekPresentationSnapshot();
            seek.ActualPositionTicks = seekPresentation.ActualPositionTicks;
            seekCallCompleted =
                seekPresentation.Generation > seekPresentationBefore.Generation &&
                seekPresentation.ActualPositionTicks.has_value();
        }
        catch (...)
        {
        }

        std::this_thread::sleep_for(sampleWindow - halfWindow);

        auto postSeekPlaybackSnapshot = graph.QualityMetricsSnapshot();
        seek.PostSeekPlaybackPositionTicks = postSeekPlaybackSnapshot.VideoPositionTicks;
        seek.Status = seekCallCompleted && seek.ActualPositionTicks.has_value() &&
            seek.PostSeekPlaybackPositionTicks > seek.ActualPositionTicks.value()
                ? "completed"
                : "failed";
        auto displayRefreshRateHz = source
            ? HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(source->FrameRate)
            : 0.0;
        auto subtitleCueRenderCount = graph.SubtitleCueRenderCount();
        auto selectedSubtitleStreamIndex = graph.SelectedSubtitleStreamIndex();
        graph.Stop();

        std::cout << "decodedVideoFrames=" << playbackSnapshot.DecodedVideoFrames
            << " hardwareDecodedVideoFrames=" << playbackSnapshot.HardwareDecodedVideoFrames
            << " softwareDecodedVideoFrames=" << playbackSnapshot.SoftwareDecodedVideoFrames
            << " renderedVideoFrames=" << playbackSnapshot.RenderedVideoFrames
            << " renderPasses=" << playbackSnapshot.RenderPasses
            << " submittedAudioFrames=" << playbackSnapshot.SubmittedAudioFrames
            << " queuedAudioBuffers=" << playbackSnapshot.QueuedAudioBuffers
            << " droppedVideoFrames=" << playbackSnapshot.DroppedVideoFrames
            << " seekPrerollDroppedFrames=" << playbackSnapshot.SeekPrerollDroppedFrames
            << " videoAheadWaitCount=" << playbackSnapshot.VideoAheadWaitCount
            << " audioAheadWaitCount=" << playbackSnapshot.AudioAheadWaitCount
            << " videoClockWaitCount=" << playbackSnapshot.VideoClockWaitCount
            << " videoStarvedPasses=" << playbackSnapshot.VideoStarvedPasses
            << " audioStarvedPasses=" << playbackSnapshot.AudioStarvedPasses
            << " audioClockTicks=" << playbackSnapshot.AudioClockTicks
            << " videoPositionTicks=" << playbackSnapshot.VideoPositionTicks
            << " audioSwitchAttempted=" << (audioSwitch.Attempted ? 1 : 0)
            << " audioSwitchStatus=" << audioSwitch.Status
            << " audioSwitchStreamIndex=" << audioSwitch.StreamIndex
            << " audioSwitchPositionBeforeTicks=" << audioSwitch.PositionBeforeTicks
            << " audioSwitchPositionAfterTicks=" << audioSwitch.PositionAfterTicks
            << " audioSwitchSubmittedFramesBefore=" << audioSwitch.SubmittedFramesBefore
            << " audioSwitchSubmittedFramesAfter=" << audioSwitch.SubmittedFramesAfter
            << " subtitleSwitch1Attempted=" << (subtitleSwitch1.Attempted ? 1 : 0)
            << " subtitleSwitch1Status=" << subtitleSwitch1.Status
            << " subtitleSwitch1StreamIndex=" << subtitleSwitch1.StreamIndex
            << " subtitleSwitch1CueCountBefore=" << subtitleSwitch1.CueCountBefore
            << " subtitleSwitch1CueCountAfter=" << subtitleSwitch1.CueCountAfter
            << " subtitleSwitch2Attempted=" << (subtitleSwitch2.Attempted ? 1 : 0)
            << " subtitleSwitch2Status=" << subtitleSwitch2.Status
            << " subtitleSwitch2StreamIndex=" << subtitleSwitch2.StreamIndex
            << " subtitleSwitch2CueCountBefore=" << subtitleSwitch2.CueCountBefore
            << " subtitleSwitch2CueCountAfter=" << subtitleSwitch2.CueCountAfter
            << " subtitleOffAttempted=" << (subtitleOff.Attempted ? 1 : 0)
            << " subtitleOffStatus=" << subtitleOff.Status
            << " subtitleOffSelectedStreamIndex=" << subtitleOff.SelectedStreamIndex
            << " seekAttempted=" << (seek.Attempted ? 1 : 0)
            << " seekStatus=" << seek.Status
            << " seekTargetPositionTicks=" << seek.TargetPositionTicks
            << " seekActualPositionTicks=" << seek.ActualPositionTicks.value_or(-1)
            << " postSeekPlaybackPositionTicks=" << seek.PostSeekPlaybackPositionTicks
            << " renderIntervalMsP05=" << playbackSnapshot.RenderIntervalMsP05
            << " renderIntervalMsP50=" << playbackSnapshot.RenderIntervalMsP50
            << " renderIntervalMsP95=" << playbackSnapshot.RenderIntervalMsP95
            << " renderIntervalMsP99=" << playbackSnapshot.RenderIntervalMsP99
            << " minFrameGapMs=" << playbackSnapshot.MinFrameGapMs
            << " maxFrameGapMs=" << playbackSnapshot.MaxFrameGapMs
            << " renderIntervalSampleCount=" << playbackSnapshot.RenderIntervalSampleCount
            << " renderIntervalOverExpected2MsCount=" << playbackSnapshot.RenderIntervalOverExpected2MsCount
            << " renderIntervalOverExpected4MsCount=" << playbackSnapshot.RenderIntervalOverExpected4MsCount
            << " renderIntervalUnderExpected2MsCount=" << playbackSnapshot.RenderIntervalUnderExpected2MsCount
            << " renderIntervalUnderExpected4MsCount=" << playbackSnapshot.RenderIntervalUnderExpected4MsCount
            << " renderIntervalAfterAudioAheadWaitSampleCount=" << playbackSnapshot.RenderIntervalAfterAudioAheadWaitSampleCount
            << " renderIntervalAfterAudioAheadWaitMsP95=" << playbackSnapshot.RenderIntervalAfterAudioAheadWaitMsP95
            << " renderIntervalAfterAudioAheadWaitMsP99=" << playbackSnapshot.RenderIntervalAfterAudioAheadWaitMsP99
            << " renderIntervalAfterAudioAheadWaitMsMax=" << playbackSnapshot.RenderIntervalAfterAudioAheadWaitMsMax
            << " renderIntervalAfterNonAudioWaitSampleCount=" << playbackSnapshot.RenderIntervalAfterNonAudioWaitSampleCount
            << " renderIntervalAfterNonAudioWaitMsP95=" << playbackSnapshot.RenderIntervalAfterNonAudioWaitMsP95
            << " renderIntervalAfterNonAudioWaitMsP99=" << playbackSnapshot.RenderIntervalAfterNonAudioWaitMsP99
            << " renderIntervalAfterNonAudioWaitMsMax=" << playbackSnapshot.RenderIntervalAfterNonAudioWaitMsMax
            << " presentDurationMsP50=" << playbackSnapshot.PresentDurationMsP50
            << " presentDurationMsP95=" << playbackSnapshot.PresentDurationMsP95
            << " presentDurationMsP99=" << playbackSnapshot.PresentDurationMsP99
            << " presentDurationMsMax=" << playbackSnapshot.PresentDurationMsMax
            << " audioAheadWaitDurationMsP50=" << playbackSnapshot.AudioAheadWaitDurationMsP50
            << " audioAheadWaitDurationMsP95=" << playbackSnapshot.AudioAheadWaitDurationMsP95
            << " audioAheadWaitDurationMsP99=" << playbackSnapshot.AudioAheadWaitDurationMsP99
            << " audioAheadWaitDurationMsMax=" << playbackSnapshot.AudioAheadWaitDurationMsMax
            << " audioAheadWaitTargetMsP50=" << playbackSnapshot.AudioAheadWaitTargetMsP50
            << " audioAheadWaitTargetMsP95=" << playbackSnapshot.AudioAheadWaitTargetMsP95
            << " audioAheadWaitTargetMsP99=" << playbackSnapshot.AudioAheadWaitTargetMsP99
            << " audioAheadWaitTargetMsMax=" << playbackSnapshot.AudioAheadWaitTargetMsMax
            << " audioAheadWaitOversleepMsP50=" << playbackSnapshot.AudioAheadWaitOversleepMsP50
            << " audioAheadWaitOversleepMsP95=" << playbackSnapshot.AudioAheadWaitOversleepMsP95
            << " audioAheadWaitOversleepMsP99=" << playbackSnapshot.AudioAheadWaitOversleepMsP99
            << " audioAheadWaitOversleepMsMax=" << playbackSnapshot.AudioAheadWaitOversleepMsMax
            << " audioAheadWaitFinalDeltaAbsMsP50=" << playbackSnapshot.AudioAheadWaitFinalDeltaAbsMsP50
            << " audioAheadWaitFinalDeltaAbsMsP95=" << playbackSnapshot.AudioAheadWaitFinalDeltaAbsMsP95
            << " audioAheadWaitFinalDeltaAbsMsP99=" << playbackSnapshot.AudioAheadWaitFinalDeltaAbsMsP99
            << " audioAheadWaitFinalDeltaAbsMsMax=" << playbackSnapshot.AudioAheadWaitFinalDeltaAbsMsMax
            << " audioAheadWaitEpisodeCount=" << playbackSnapshot.AudioAheadWaitEpisodeCount
            << " audioAheadWaitPassesPerEpisodeP50=" << playbackSnapshot.AudioAheadWaitPassesPerEpisodeP50
            << " audioAheadWaitPassesPerEpisodeP95=" << playbackSnapshot.AudioAheadWaitPassesPerEpisodeP95
            << " audioAheadWaitPassesPerEpisodeP99=" << playbackSnapshot.AudioAheadWaitPassesPerEpisodeP99
            << " audioAheadWaitPassesPerEpisodeMax=" << playbackSnapshot.AudioAheadWaitPassesPerEpisodeMax
            << " audioAheadWaitPassDurationMsP50=" << playbackSnapshot.AudioAheadWaitPassDurationMsP50
            << " audioAheadWaitPassDurationMsP95=" << playbackSnapshot.AudioAheadWaitPassDurationMsP95
            << " audioAheadWaitPassDurationMsP99=" << playbackSnapshot.AudioAheadWaitPassDurationMsP99
            << " audioAheadWaitPassDurationMsMax=" << playbackSnapshot.AudioAheadWaitPassDurationMsMax
            << " audioAheadWaitPassTargetMsP50=" << playbackSnapshot.AudioAheadWaitPassTargetMsP50
            << " audioAheadWaitPassTargetMsP95=" << playbackSnapshot.AudioAheadWaitPassTargetMsP95
            << " audioAheadWaitPassTargetMsP99=" << playbackSnapshot.AudioAheadWaitPassTargetMsP99
            << " audioAheadWaitPassTargetMsMax=" << playbackSnapshot.AudioAheadWaitPassTargetMsMax
            << " audioAheadWaitPassOversleepMsP50=" << playbackSnapshot.AudioAheadWaitPassOversleepMsP50
            << " audioAheadWaitPassOversleepMsP95=" << playbackSnapshot.AudioAheadWaitPassOversleepMsP95
            << " audioAheadWaitPassOversleepMsP99=" << playbackSnapshot.AudioAheadWaitPassOversleepMsP99
            << " audioAheadWaitPassOversleepMsMax=" << playbackSnapshot.AudioAheadWaitPassOversleepMsMax
            << " framePacingSourceFrameRate=" << playbackSnapshot.FramePacingSourceFrameRate
            << " lateFrameDropToleranceMs=" << playbackSnapshot.LateFrameDropToleranceMs
            << " audioVideoDriftMsP50=" << playbackSnapshot.AudioVideoDriftMsP50
            << " audioVideoDriftMsP95=" << playbackSnapshot.AudioVideoDriftMsP95
            << " audioVideoDriftMsP99=" << playbackSnapshot.AudioVideoDriftMsP99
            << " audioVideoDriftMsMax=" << playbackSnapshot.AudioVideoDriftMsMax
            << " sourceCodec=" << (source ? source->Codec : "")
            << " sourceWidth=" << (source ? source->Width : 0)
            << " sourceHeight=" << (source ? source->Height : 0)
            << " sourceFrameRate=" << (source ? source->FrameRate : 0.0)
            << " sourceHdrKind=" << (source ? source->HdrKind : "")
            << " sourceVideoRange=" << (source ? source->VideoRange : "")
            << " sourceColorPrimaries=" << (source ? source->ColorPrimaries : "")
            << " sourceColorTransfer=" << (source ? source->ColorTransfer : "")
            << " sourceColorSpace=" << (source ? source->ColorSpace : "")
            << " dxgiInput=" << FormatDxgiColorSpace(resources.LastVideoProcessorInputColorSpace())
            << " dxgiOutput=" << FormatDxgiColorSpace(resources.LastVideoProcessorOutputColorSpace())
            << " conversionStatus=" << winrt::to_string(winrt::hstring(resources.LastVideoProcessorConversionStatus()))
            << " isVideoProcessorColorSpaceValidated=" << (resources.LastVideoProcessorConversionWasValidated() ? 1 : 0)
            << " displayRefreshRateHz=" << displayRefreshRateHz
            << " displayRefreshPolicy=software-only-cadence-policy"
            << " sourceTrackCount=" << tracks.size()
            << " subtitleCueRenderCount=" << subtitleCueRenderCount
            << " selectedAudioStreamIndex=" << selectedAudioStreamIndex.value_or(-1)
            << " selectedSubtitleStreamIndex=" << selectedSubtitleStreamIndex.value_or(-1);

        for (auto index = size_t{0}; index < tracks.size(); ++index)
        {
            auto const& track = tracks[index];
            std::cout << " track" << index << "Index=" << track.StreamIndex
                << " track" << index << "Kind=" << track.Kind
                << " track" << index << "Codec=" << track.Codec
                << " track" << index << "Language=" << track.Language
                << " track" << index << "ChannelLayout=" << track.ChannelLayout
                << " track" << index << "Channels=" << track.Channels
                << " track" << index << "IsDefault=" << (track.IsDefault ? 1 : 0)
                << " track" << index << "IsForced=" << (track.IsForced ? 1 : 0)
                << " track" << index << "RealFrameRate=" << track.RealFrameRate
                << " track" << index << "AverageFrameRate=" << track.AverageFrameRate;
        }

        std::cout << std::endl;

        assert(playbackSnapshot.DecodedVideoFrames > 1);
        assert(playbackSnapshot.RenderedVideoFrames > 1);
        return 0;
    }
    catch (winrt::hresult_error const& error)
    {
        std::wcerr << L"native playback graph smoke failed: " << error.message().c_str() << std::endl;
        graph.Stop();
        return 2;
    }
    catch (std::exception const& error)
    {
        std::cerr << "native playback graph smoke failed: " << error.what() << std::endl;
        graph.Stop();
        return 2;
    }
}
