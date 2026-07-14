#pragma once

namespace winrt::NoiraPlayer::Native::implementation
{
    enum class VideoRenderPath
    {
        None,
        DirectCopy,
        VideoProcessor,
        Bgra
    };

    struct VideoRenderPhaseSample
    {
        VideoRenderPath Path{VideoRenderPath::None};
        bool PostProcessed{false};
        double ProcessorSetupCpuMs{0.0};
        double ViewTargetCpuMs{0.0};
        double ClearCpuMs{0.0};
        double BltCpuMs{0.0};
        double PostProcessCpuMs{0.0};
    };
}
