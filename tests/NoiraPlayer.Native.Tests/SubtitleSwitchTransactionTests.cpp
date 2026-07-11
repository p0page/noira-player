#include <array>
#include <cassert>
#include <optional>
#include <stdexcept>

#include "Media/SubtitleSwitchTransaction.h"

using winrt::NoiraPlayer::Native::implementation::RunSubtitleSwitchTransaction;
using winrt::NoiraPlayer::Native::implementation::SubtitleSwitchDisposition;
using winrt::NoiraPlayer::Native::implementation::SubtitleSwitchOperations;

namespace
{
    enum class FaultStage
    {
        None,
        Open,
        Seek,
        FlushAudio,
        FlushSubtitle,
        SelectRenderer
    };

    struct Harness
    {
        std::optional<int32_t> DecoderSelection{1};
        std::optional<int32_t> RendererSelection{1};
        std::optional<int32_t> ActiveSelection{1};
        FaultStage TargetFault{FaultStage::None};
        bool FailRestoreOpen{false};
        bool GraphPaused{true};
        bool AudioStarted{true};
        bool AudioPaused{true};

        SubtitleSwitchOperations Operations()
        {
            SubtitleSwitchOperations operations;
            operations.DisableSelection = [this]
            {
                DecoderSelection.reset();
                RendererSelection.reset();
                ActiveSelection.reset();
            };
            operations.OpenDecoder = [this](int32_t streamIndex)
            {
                ActiveSelection = streamIndex;
                if ((streamIndex == 2 && TargetFault == FaultStage::Open) ||
                    (streamIndex == 1 && FailRestoreOpen))
                {
                    throw std::runtime_error("open fault");
                }

                DecoderSelection = streamIndex;
            };
            operations.SelectedDecoderStream = [this]
            {
                return DecoderSelection;
            };
            operations.SeekVideo = [this]
            {
                ThrowTargetFault(FaultStage::Seek);
            };
            operations.FlushAudioDecoder = [this]
            {
                ThrowTargetFault(FaultStage::FlushAudio);
            };
            operations.FlushSubtitleDecoder = [this]
            {
                ThrowTargetFault(FaultStage::FlushSubtitle);
            };
            operations.SelectRenderer = [this](int32_t streamIndex)
            {
                ThrowTargetFault(FaultStage::SelectRenderer);
                RendererSelection = streamIndex;
            };
            return operations;
        }

        void ThrowTargetFault(FaultStage stage) const
        {
            if (ActiveSelection == 2 && TargetFault == stage)
            {
                throw std::runtime_error("target fault");
            }
        }
    };

    void AssertLifecycleUnchanged(Harness const& harness)
    {
        assert(harness.GraphPaused);
        assert(harness.AudioStarted);
        assert(harness.AudioPaused);
    }
}

int main()
{
    constexpr std::array targetFaults{
        FaultStage::Open,
        FaultStage::Seek,
        FaultStage::FlushAudio,
        FaultStage::FlushSubtitle,
        FaultStage::SelectRenderer};

    for (auto fault : targetFaults)
    {
        Harness harness;
        harness.TargetFault = fault;

        auto result = RunSubtitleSwitchTransaction(1, 2, harness.Operations());

        assert(result.Disposition == SubtitleSwitchDisposition::RestoredPrevious);
        assert(result.SwitchFailure != nullptr);
        assert(result.RestoreFailure == nullptr);
        assert(harness.DecoderSelection == 1);
        assert(harness.RendererSelection == 1);
        AssertLifecycleUnchanged(harness);
    }

    {
        Harness harness;
        harness.TargetFault = FaultStage::Seek;
        harness.FailRestoreOpen = true;

        auto result = RunSubtitleSwitchTransaction(1, 2, harness.Operations());

        assert(result.Disposition == SubtitleSwitchDisposition::Disabled);
        assert(result.SwitchFailure != nullptr);
        assert(result.RestoreFailure != nullptr);
        assert(!harness.DecoderSelection.has_value());
        assert(!harness.RendererSelection.has_value());
        AssertLifecycleUnchanged(harness);
    }

    {
        Harness harness;

        auto result = RunSubtitleSwitchTransaction(1, 2, harness.Operations());

        assert(result.Disposition == SubtitleSwitchDisposition::Completed);
        assert(result.SwitchFailure == nullptr);
        assert(result.RestoreFailure == nullptr);
        assert(harness.DecoderSelection == 2);
        assert(harness.RendererSelection == 2);
        AssertLifecycleUnchanged(harness);
    }

    {
        Harness harness;

        auto result = RunSubtitleSwitchTransaction(1, std::nullopt, harness.Operations());

        assert(result.Disposition == SubtitleSwitchDisposition::Completed);
        assert(!harness.DecoderSelection.has_value());
        assert(!harness.RendererSelection.has_value());
        AssertLifecycleUnchanged(harness);
    }

    return 0;
}
