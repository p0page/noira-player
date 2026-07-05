#pragma once

#include "NativePlaybackStatus.g.h"

namespace winrt::NextGenEmby::Native::implementation
{
    struct NativePlaybackStatus : NativePlaybackStatusT<NativePlaybackStatus>
    {
        NativePlaybackStatus() = default;

        NextGenEmby::Native::NativeHdrStatus HdrStatus() const noexcept;
        void HdrStatus(NextGenEmby::Native::NativeHdrStatus value) noexcept;
        bool IsHdrDisplayAvailable() const noexcept;
        void IsHdrDisplayAvailable(bool value) noexcept;
        bool IsHdrOutputActive() const noexcept;
        void IsHdrOutputActive(bool value) noexcept;
        winrt::hstring Message() const;
        void Message(winrt::hstring const& value);
        winrt::hstring SwapChainFormat() const;
        void SwapChainFormat(winrt::hstring const& value);
        winrt::hstring SwapChainColorSpace() const;
        void SwapChainColorSpace(winrt::hstring const& value);
        bool IsTenBitSwapChain() const noexcept;
        void IsTenBitSwapChain(bool value) noexcept;
        bool IsVideoProcessorColorSpaceValidated() const noexcept;
        void IsVideoProcessorColorSpaceValidated(bool value) noexcept;
        winrt::hstring VideoProcessorInputColorSpace() const;
        void VideoProcessorInputColorSpace(winrt::hstring const& value);
        winrt::hstring VideoProcessorOutputColorSpace() const;
        void VideoProcessorOutputColorSpace(winrt::hstring const& value);
        winrt::hstring VideoProcessorConversionStatus() const;
        void VideoProcessorConversionStatus(winrt::hstring const& value);

    private:
        NextGenEmby::Native::NativeHdrStatus m_hdrStatus{NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unknown};
        bool m_isHdrDisplayAvailable{false};
        bool m_isHdrOutputActive{false};
        winrt::hstring m_message;
        winrt::hstring m_swapChainFormat;
        winrt::hstring m_swapChainColorSpace;
        bool m_isTenBitSwapChain{false};
        bool m_isVideoProcessorColorSpaceValidated{false};
        winrt::hstring m_videoProcessorInputColorSpace;
        winrt::hstring m_videoProcessorOutputColorSpace;
        winrt::hstring m_videoProcessorConversionStatus;
    };
}

namespace winrt::NextGenEmby::Native::factory_implementation
{
    struct NativePlaybackStatus : NativePlaybackStatusT<NativePlaybackStatus, implementation::NativePlaybackStatus>
    {
    };
}
