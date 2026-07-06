#include "pch.h"
#include "NativePlaybackStatus.h"
#include "NativePlaybackStatus.g.cpp"

namespace winrt::NextGenEmby::Native::implementation
{
    NextGenEmby::Native::NativeHdrStatus NativePlaybackStatus::HdrStatus() const noexcept
    {
        return m_hdrStatus;
    }

    void NativePlaybackStatus::HdrStatus(NextGenEmby::Native::NativeHdrStatus value) noexcept
    {
        m_hdrStatus = value;
    }

    bool NativePlaybackStatus::IsHdrDisplayAvailable() const noexcept
    {
        return m_isHdrDisplayAvailable;
    }

    void NativePlaybackStatus::IsHdrDisplayAvailable(bool value) noexcept
    {
        m_isHdrDisplayAvailable = value;
    }

    bool NativePlaybackStatus::IsHdrOutputActive() const noexcept
    {
        return m_isHdrOutputActive;
    }

    void NativePlaybackStatus::IsHdrOutputActive(bool value) noexcept
    {
        m_isHdrOutputActive = value;
    }

    double NativePlaybackStatus::RefreshRateHz() const noexcept
    {
        return m_refreshRateHz;
    }

    void NativePlaybackStatus::RefreshRateHz(double value) noexcept
    {
        m_refreshRateHz = value > 0.0 ? value : 0.0;
    }

    winrt::hstring NativePlaybackStatus::Message() const
    {
        return m_message;
    }

    void NativePlaybackStatus::Message(winrt::hstring const& value)
    {
        m_message = value;
    }

    winrt::hstring NativePlaybackStatus::SwapChainFormat() const
    {
        return m_swapChainFormat;
    }

    void NativePlaybackStatus::SwapChainFormat(winrt::hstring const& value)
    {
        m_swapChainFormat = value;
    }

    winrt::hstring NativePlaybackStatus::SwapChainColorSpace() const
    {
        return m_swapChainColorSpace;
    }

    void NativePlaybackStatus::SwapChainColorSpace(winrt::hstring const& value)
    {
        m_swapChainColorSpace = value;
    }

    bool NativePlaybackStatus::IsTenBitSwapChain() const noexcept
    {
        return m_isTenBitSwapChain;
    }

    void NativePlaybackStatus::IsTenBitSwapChain(bool value) noexcept
    {
        m_isTenBitSwapChain = value;
    }

    bool NativePlaybackStatus::IsVideoProcessorColorSpaceValidated() const noexcept
    {
        return m_isVideoProcessorColorSpaceValidated;
    }

    void NativePlaybackStatus::IsVideoProcessorColorSpaceValidated(bool value) noexcept
    {
        m_isVideoProcessorColorSpaceValidated = value;
    }

    winrt::hstring NativePlaybackStatus::VideoProcessorInputColorSpace() const
    {
        return m_videoProcessorInputColorSpace;
    }

    void NativePlaybackStatus::VideoProcessorInputColorSpace(winrt::hstring const& value)
    {
        m_videoProcessorInputColorSpace = value;
    }

    winrt::hstring NativePlaybackStatus::VideoProcessorOutputColorSpace() const
    {
        return m_videoProcessorOutputColorSpace;
    }

    void NativePlaybackStatus::VideoProcessorOutputColorSpace(winrt::hstring const& value)
    {
        m_videoProcessorOutputColorSpace = value;
    }

    winrt::hstring NativePlaybackStatus::VideoProcessorConversionStatus() const
    {
        return m_videoProcessorConversionStatus;
    }

    void NativePlaybackStatus::VideoProcessorConversionStatus(winrt::hstring const& value)
    {
        m_videoProcessorConversionStatus = value;
    }
}
