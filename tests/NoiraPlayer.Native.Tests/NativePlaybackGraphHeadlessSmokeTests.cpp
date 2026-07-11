#include <cassert>
#include <atomic>
#include <chrono>
#include <cstdlib>
#include <cwchar>
#include <iostream>
#include <limits>
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
        int PauseSeconds{0};
        int64_t StartPositionTicks{0};
        std::wstring Scenario{L"playback"};
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
        bool PausedSwitch{false};
        int32_t SelectedStreamIndex{-1};
        int64_t PausedPositionBeforeTicks{0};
        int64_t PausedPositionAfterTicks{0};
        int64_t PositionBeforeResumeTicks{0};
        int64_t PositionAfterResumeTicks{0};
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
            else if (std::wcscmp(argv[index], L"--pause-seconds") == 0 && index + 1 < argc)
            {
                auto parsed = std::wcstol(argv[++index], nullptr, 10);
                if (parsed >= 0 && parsed <= 900)
                {
                    options.PauseSeconds = static_cast<int>(parsed);
                }
            }
            else if (std::wcscmp(argv[index], L"--start-position-ticks") == 0 && index + 1 < argc)
            {
                auto parsed = std::wcstoll(argv[++index], nullptr, 10);
                if (parsed >= 0)
                {
                    options.StartPositionTicks = parsed;
                }
            }
            else if (std::wcscmp(argv[index], L"--scenario") == 0 && index + 1 < argc)
            {
                auto scenario = std::wstring{argv[++index]};
                if (scenario == L"playback" ||
                    scenario == L"timeline" ||
                    scenario == L"interactions" ||
                    scenario == L"pause-resume")
                {
                    options.Scenario = std::move(scenario);
                }
            }
        }

        return options;
    }

    void ReportStage(char const* stage)
    {
        std::cerr << "helperStage=" << stage << std::endl;
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
    ReportStage("started");
    winrt::init_apartment(winrt::apartment_type::multi_threaded);

    DxDeviceResources resources;
    resources.CreateSwapChain(1280, 720, false);
    assert(resources.HasRenderTarget());

    std::atomic<bool> playbackFailed{false};
    PlaybackGraph graph(
        resources,
        [&playbackFailed](auto state, winrt::hstring const&)
        {
            if (state == winrt::NoiraPlayer::Native::implementation::PlaybackGraphState::Failed)
            {
                playbackFailed.store(true, std::memory_order_relaxed);
            }
        });
    PlaybackGraphOpenRequest request{};
        request.DirectStreamUrl = options.StreamUrl;
        request.StartPositionTicks = options.StartPositionTicks;

    try
    {
        ReportStage("graph-open-started");
        graph.Open(request);
        ReportStage("graph-open-completed");
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
        ReportStage("initial-sample-captured");

        std::vector<int32_t> audioStreamIndexes;
        std::vector<int32_t> subtitleStreamIndexes;
        auto selectedAudioStreamIndex = graph.SelectedAudioStreamIndex();
        for (auto const& track : tracks)
        {
            if (track.Kind == "Audio")
            {
                audioStreamIndexes.push_back(track.StreamIndex);
            }
            else if (track.Kind == "Subtitle")
            {
                subtitleStreamIndexes.push_back(track.StreamIndex);
            }
        }

        AudioSwitchOutcome audioSwitch;
        SubtitleSwitchOutcome subtitleSwitch1;
        SubtitleSwitchOutcome subtitleSwitch2;
        SubtitleOffOutcome subtitleOff;
        SeekOutcome seek;
        seek.TargetPositionTicks = options.StartPositionTicks >=
            (std::numeric_limits<int64_t>::max)() - SeekTargetPositionTicks
            ? (std::numeric_limits<int64_t>::max)()
            : options.StartPositionTicks + SeekTargetPositionTicks;

        auto positionBeforePauseTicks = graph.CurrentPositionTicks();
        auto decodedVideoFramesBeforePause = playbackSnapshot.DecodedVideoFrames;
        auto positionAfterResumeTicks = positionBeforePauseTicks;
        auto postResumeDecodedVideoFrames = decodedVideoFramesBeforePause;
        auto postResumeRenderedVideoFrames = playbackSnapshot.RenderedVideoFrames;
        auto pauseResumeStatus = std::string{"completed"};
        auto pauseResumeRecovered = true;
        if (options.Scenario == L"pause-resume")
        {
            graph.Pause();
            ReportStage("pause-started");
            std::this_thread::sleep_for(std::chrono::milliseconds(options.PauseSeconds * 1000));
            graph.Resume();
            ReportStage("resume-completed");
            std::this_thread::sleep_for(sampleWindow);
            playbackSnapshot = graph.QualityMetricsSnapshot();
            positionAfterResumeTicks = graph.CurrentPositionTicks();
            postResumeDecodedVideoFrames = playbackSnapshot.DecodedVideoFrames;
            postResumeRenderedVideoFrames = playbackSnapshot.RenderedVideoFrames;
            auto failed = playbackFailed.load(std::memory_order_relaxed);
            pauseResumeRecovered = !failed &&
                positionAfterResumeTicks > positionBeforePauseTicks &&
                playbackSnapshot.DecodedVideoFrames > decodedVideoFramesBeforePause &&
                playbackSnapshot.RenderedVideoFrames > 0;
            pauseResumeStatus = pauseResumeRecovered ? "completed" : "failed";
        }

        if (options.Scenario == L"interactions")
        {
            if (audioStreamIndexes.size() >= 2)
            {
                audioSwitch.Attempted = true;
                audioSwitch.StreamIndex = audioStreamIndexes[1];
                audioSwitch.PositionBeforeTicks = graph.CurrentPositionTicks();
                audioSwitch.SubmittedFramesBefore = graph.QualityMetricsSnapshot().SubmittedAudioFrames;
                try
                {
                    graph.SwitchAudioStream(audioSwitch.StreamIndex);
                    std::this_thread::sleep_for(500ms);
                    selectedAudioStreamIndex = graph.SelectedAudioStreamIndex();
                    audioSwitch.PositionAfterTicks = graph.CurrentPositionTicks();
                    audioSwitch.SubmittedFramesAfter = graph.QualityMetricsSnapshot().SubmittedAudioFrames;
                    audioSwitch.Status =
                        selectedAudioStreamIndex.has_value() &&
                        selectedAudioStreamIndex.value() == audioSwitch.StreamIndex &&
                        audioSwitch.PositionAfterTicks > audioSwitch.PositionBeforeTicks &&
                        audioSwitch.SubmittedFramesAfter > audioSwitch.SubmittedFramesBefore
                            ? "completed"
                            : "failed";
                }
                catch (...)
                {
                    selectedAudioStreamIndex = graph.SelectedAudioStreamIndex();
                    audioSwitch.PositionAfterTicks = graph.CurrentPositionTicks();
                    audioSwitch.SubmittedFramesAfter = graph.QualityMetricsSnapshot().SubmittedAudioFrames;
                    audioSwitch.Status = "failed";
                }
            }
        }

        auto runSubtitleSwitch = [&graph](int32_t streamIndex, bool pauseBeforeSwitch)
        {
            SubtitleSwitchOutcome outcome;
            outcome.Attempted = true;
            outcome.StreamIndex = streamIndex;
            outcome.PausedSwitch = pauseBeforeSwitch;
            outcome.CueCountBefore = graph.SubtitleCueRenderCount();
            auto resumeAfterFailure = false;
            try
            {
                if (pauseBeforeSwitch)
                {
                    graph.Pause();
                    resumeAfterFailure = true;
                }

                graph.SwitchSubtitleStream(streamIndex);
                outcome.SelectedStreamIndex = graph.SelectedSubtitleStreamIndex().value_or(-1);
                if (pauseBeforeSwitch)
                {
                    outcome.PausedPositionBeforeTicks = graph.CurrentPositionTicks();
                    std::this_thread::sleep_for(100ms);
                    outcome.PausedPositionAfterTicks = graph.CurrentPositionTicks();
                    outcome.PositionBeforeResumeTicks = outcome.PausedPositionAfterTicks;
                    graph.Resume();
                    resumeAfterFailure = false;
                }
                else
                {
                    outcome.PositionBeforeResumeTicks = graph.CurrentPositionTicks();
                }

                std::this_thread::sleep_for(500ms);
                outcome.PositionAfterResumeTicks = graph.CurrentPositionTicks();
                outcome.CueCountAfter = graph.SubtitleCueRenderCount();
                auto pauseAndResumeObserved = !pauseBeforeSwitch ||
                    (outcome.PausedPositionAfterTicks == outcome.PausedPositionBeforeTicks &&
                        outcome.PositionAfterResumeTicks > outcome.PositionBeforeResumeTicks);
                outcome.Status =
                    outcome.SelectedStreamIndex == outcome.StreamIndex &&
                    outcome.CueCountAfter > outcome.CueCountBefore &&
                    pauseAndResumeObserved
                    ? "completed"
                    : "failed";
            }
            catch (...)
            {
                outcome.SelectedStreamIndex = graph.SelectedSubtitleStreamIndex().value_or(-1);
                outcome.CueCountAfter = graph.SubtitleCueRenderCount();
                outcome.Status = "failed";
                if (resumeAfterFailure)
                {
                    graph.Resume();
                }
            }

            return outcome;
        };

        if (options.Scenario == L"interactions" && !subtitleStreamIndexes.empty())
        {
            subtitleSwitch1 = runSubtitleSwitch(subtitleStreamIndexes[0], true);
        }

        if (options.Scenario == L"interactions" && subtitleStreamIndexes.size() >= 2)
        {
            subtitleSwitch2 = runSubtitleSwitch(subtitleStreamIndexes[1], false);
        }

        if (options.Scenario == L"interactions" && !subtitleStreamIndexes.empty())
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

        auto seekCallCompleted = false;
        auto seekPresentationBefore = graph.SeekPresentationSnapshot();
        auto seekGeneration = seekPresentationBefore.Generation;
        if (options.Scenario == L"timeline")
        {
            ReportStage("seek-started");
            seek.Attempted = true;
            try
            {
                graph.Seek(seek.TargetPositionTicks);
                auto seekPresentation = graph.SeekPresentationSnapshot();
                seek.ActualPositionTicks = seekPresentation.ActualPositionTicks;
                seekGeneration = seekPresentation.Generation;
                seekCallCompleted = seekPresentation.Generation > seekPresentationBefore.Generation;
            }
            catch (...)
            {
            }

            std::this_thread::sleep_for(sampleWindow - halfWindow);

            auto finalSeekPresentation = graph.SeekPresentationSnapshot();
            if (seekCallCompleted && finalSeekPresentation.Generation == seekGeneration)
            {
                seek.ActualPositionTicks = finalSeekPresentation.ActualPositionTicks;
            }

            auto postSeekPlaybackSnapshot = graph.QualityMetricsSnapshot();
            seek.PostSeekPlaybackPositionTicks = postSeekPlaybackSnapshot.VideoPositionTicks;
            seek.Status = seekCallCompleted && seek.ActualPositionTicks.has_value() &&
                seek.PostSeekPlaybackPositionTicks > seek.ActualPositionTicks.value()
                    ? "completed"
                    : "failed";
            ReportStage("seek-completed");
        }
        auto displayRefreshRateHz = source
            ? HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(source->FrameRate)
            : 0.0;
        auto subtitleCueRenderCount = graph.SubtitleCueRenderCount();
        auto selectedSubtitleStreamIndex = graph.SelectedSubtitleStreamIndex();
        auto timeline = graph.TimelineSnapshot();
        ReportStage("graph-stop-started");
        graph.Stop();
        ReportStage("graph-stop-completed");

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
            << " subtitleSwitch1PausedSwitch=" << (subtitleSwitch1.PausedSwitch ? 1 : 0)
            << " subtitleSwitch1SelectedStreamIndex=" << subtitleSwitch1.SelectedStreamIndex
            << " subtitleSwitch1PausedPositionBeforeTicks=" << subtitleSwitch1.PausedPositionBeforeTicks
            << " subtitleSwitch1PausedPositionAfterTicks=" << subtitleSwitch1.PausedPositionAfterTicks
            << " subtitleSwitch1PositionBeforeResumeTicks=" << subtitleSwitch1.PositionBeforeResumeTicks
            << " subtitleSwitch1PositionAfterResumeTicks=" << subtitleSwitch1.PositionAfterResumeTicks
            << " subtitleSwitch2Attempted=" << (subtitleSwitch2.Attempted ? 1 : 0)
            << " subtitleSwitch2Status=" << subtitleSwitch2.Status
            << " subtitleSwitch2StreamIndex=" << subtitleSwitch2.StreamIndex
            << " subtitleSwitch2CueCountBefore=" << subtitleSwitch2.CueCountBefore
            << " subtitleSwitch2CueCountAfter=" << subtitleSwitch2.CueCountAfter
            << " subtitleSwitch2PausedSwitch=" << (subtitleSwitch2.PausedSwitch ? 1 : 0)
            << " subtitleSwitch2SelectedStreamIndex=" << subtitleSwitch2.SelectedStreamIndex
            << " subtitleSwitch2PausedPositionBeforeTicks=" << subtitleSwitch2.PausedPositionBeforeTicks
            << " subtitleSwitch2PausedPositionAfterTicks=" << subtitleSwitch2.PausedPositionAfterTicks
            << " subtitleSwitch2PositionBeforeResumeTicks=" << subtitleSwitch2.PositionBeforeResumeTicks
            << " subtitleSwitch2PositionAfterResumeTicks=" << subtitleSwitch2.PositionAfterResumeTicks
            << " subtitleOffAttempted=" << (subtitleOff.Attempted ? 1 : 0)
            << " subtitleOffStatus=" << subtitleOff.Status
            << " subtitleOffSelectedStreamIndex=" << subtitleOff.SelectedStreamIndex
            << " seekAttempted=" << (seek.Attempted ? 1 : 0)
            << " seekStatus=" << seek.Status
            << " seekTargetPositionTicks=" << seek.TargetPositionTicks
            << " seekDemuxTargetTicks=" << timeline.LastSeekDemuxTargetTicks
            << " seekActualPositionTicks=" << seek.ActualPositionTicks.value_or(-1)
            << " postSeekPlaybackPositionTicks=" << seek.PostSeekPlaybackPositionTicks
            << " postSeekAdvanced=" << (seek.ActualPositionTicks.has_value() &&
                seek.PostSeekPlaybackPositionTicks > seek.ActualPositionTicks.value() ? 1 : 0)
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
            << " audioAheadWaitEndToPresentSampleCount=" << playbackSnapshot.AudioAheadWaitEndToPresentSampleCount
            << " audioAheadWaitEndToPresentMsP50=" << playbackSnapshot.AudioAheadWaitEndToPresentMsP50
            << " audioAheadWaitEndToPresentMsP95=" << playbackSnapshot.AudioAheadWaitEndToPresentMsP95
            << " audioAheadWaitEndToPresentMsP99=" << playbackSnapshot.AudioAheadWaitEndToPresentMsP99
            << " audioAheadWaitEndToPresentMsMax=" << playbackSnapshot.AudioAheadWaitEndToPresentMsMax
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
            << " sourceIsDolbyVision=" << (source && source->IsDolbyVision ? 1 : 0)
            << " sourceDolbyVisionProfile=" << (source ? source->DolbyVisionProfile : 0)
            << " sourceDolbyVisionCompatibilityId=" << (source ? source->DolbyVisionCompatibilityId : 0)
            << " sourceHasHdr10BaseLayer=" << (source && source->HasHdr10BaseLayer ? 1 : 0)
            << " sourceHasHlgBaseLayer=" << (source && source->HasHlgBaseLayer ? 1 : 0)
            << " containerStartTimeTicks=" << timeline.ContainerStartTimeTicks
            << " videoStreamStartTimeTicks=" << timeline.StreamStartTimeTicks
            << " logicalDurationTicks=" << timeline.LogicalDurationTicks
            << " dxgiInput=" << FormatDxgiColorSpace(resources.LastVideoProcessorInputColorSpace())
            << " dxgiOutput=" << FormatDxgiColorSpace(resources.LastVideoProcessorOutputColorSpace())
            << " conversionStatus=" << winrt::to_string(winrt::hstring(resources.LastVideoProcessorConversionStatus()))
            << " isVideoProcessorColorSpaceValidated=" << (resources.LastVideoProcessorConversionWasValidated() ? 1 : 0)
            << " displayRefreshRateHz=" << displayRefreshRateHz
            << " displayRefreshPolicy=software-only-cadence-policy"
            << " sourceTrackCount=" << tracks.size()
            << " playbackFailed=" << (playbackFailed.load(std::memory_order_relaxed) ? 1 : 0)
            << " pauseDurationSeconds=" << options.PauseSeconds
            << " positionBeforePauseTicks=" << positionBeforePauseTicks
            << " positionAfterResumeTicks=" << positionAfterResumeTicks
            << " postResumeDecodedVideoFrames=" << postResumeDecodedVideoFrames
            << " postResumeRenderedVideoFrames=" << postResumeRenderedVideoFrames
            << " pauseResumeStatus=" << pauseResumeStatus
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
        ReportStage("completed");

        assert(playbackSnapshot.DecodedVideoFrames > 1);
        assert(playbackSnapshot.RenderedVideoFrames > 1);
        return playbackFailed.load(std::memory_order_relaxed) || !pauseResumeRecovered ? 2 : 0;
    }
    catch (winrt::hresult_error const& error)
    {
        auto source = graph.VideoSourceSnapshot();
        if (source &&
            source->IsDolbyVision &&
            source->DolbyVisionProfile == 5 &&
            !source->HasHdr10BaseLayer &&
            !source->HasHlgBaseLayer)
        {
            std::cout
                << "unsupportedCode=dolby-vision-profile5-no-fallback"
                << " sourceCodec=" << source->Codec
                << " sourceWidth=" << source->Width
                << " sourceHeight=" << source->Height
                << " sourceFrameRate=" << source->FrameRate
                << " sourceHdrKind=" << source->HdrKind
                << " sourceVideoRange=" << source->VideoRange
                << " sourceColorPrimaries=" << source->ColorPrimaries
                << " sourceColorTransfer=" << source->ColorTransfer
                << " sourceColorSpace=" << source->ColorSpace
                << " sourceIsDolbyVision=1"
                << " sourceDolbyVisionProfile=" << source->DolbyVisionProfile
                << " sourceDolbyVisionCompatibilityId=" << source->DolbyVisionCompatibilityId
                << " sourceHasHdr10BaseLayer=0"
                << " sourceHasHlgBaseLayer=0"
                << " containerStartTimeTicks=0"
                << " videoStreamStartTimeTicks=0"
                << " logicalDurationTicks=0"
                << std::endl;
        }
        std::wcerr << L"native playback graph smoke failed: " << error.message().c_str() << std::endl;
        graph.Stop();
        return source &&
            source->IsDolbyVision &&
            source->DolbyVisionProfile == 5 &&
            !source->HasHdr10BaseLayer &&
            !source->HasHlgBaseLayer
            ? 3
            : 2;
    }
    catch (std::exception const& error)
    {
        std::cerr << "native playback graph smoke failed: " << error.what() << std::endl;
        graph.Stop();
        return 2;
    }
}
