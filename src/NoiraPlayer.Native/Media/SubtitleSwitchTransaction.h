#pragma once

#include <cstdint>
#include <exception>
#include <functional>
#include <optional>
#include <stdexcept>

namespace winrt::NoiraPlayer::Native::implementation
{
    enum class SubtitleSwitchDisposition
    {
        Completed,
        RestoredPrevious,
        Disabled
    };

    struct SubtitleSwitchOperations
    {
        std::function<void()> DisableSelection;
        std::function<void(int32_t)> OpenDecoder;
        std::function<std::optional<int32_t>()> SelectedDecoderStream;
        std::function<bool()> ShouldRebasePlayback;
        std::function<void()> SeekVideo;
        std::function<void()> FlushAudioDecoder;
        std::function<void()> FlushSubtitleDecoder;
        std::function<void(int32_t)> SelectRenderer;
    };

    struct SubtitleSwitchResult
    {
        SubtitleSwitchDisposition Disposition{SubtitleSwitchDisposition::Completed};
        std::exception_ptr SwitchFailure;
        std::exception_ptr RestoreFailure;
    };

    inline SubtitleSwitchResult RunSubtitleSwitchTransaction(
        std::optional<int32_t> previousSelection,
        std::optional<int32_t> requestedSelection,
        SubtitleSwitchOperations const& operations)
    {
        auto applySelection = [&operations](std::optional<int32_t> selection)
        {
            operations.DisableSelection();
            if (!selection.has_value())
            {
                return;
            }

            operations.OpenDecoder(selection.value());
            if (operations.SelectedDecoderStream() != selection)
            {
                throw std::runtime_error("Subtitle decoder did not select the requested stream.");
            }

            if (operations.ShouldRebasePlayback())
            {
                operations.SeekVideo();
                operations.FlushAudioDecoder();
            }
            operations.FlushSubtitleDecoder();
            operations.SelectRenderer(selection.value());
        };

        try
        {
            applySelection(requestedSelection);
            return {};
        }
        catch (...)
        {
            auto switchFailure = std::current_exception();
            try
            {
                applySelection(previousSelection);
                return {
                    SubtitleSwitchDisposition::RestoredPrevious,
                    switchFailure,
                    nullptr};
            }
            catch (...)
            {
                auto restoreFailure = std::current_exception();
                operations.DisableSelection();
                return {
                    SubtitleSwitchDisposition::Disabled,
                    switchFailure,
                    restoreFailure};
            }
        }
    }
}
