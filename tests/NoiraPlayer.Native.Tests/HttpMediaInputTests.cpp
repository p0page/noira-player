#include "pch.h"
#include "Media/HttpMediaInput.h"

#include <array>
#include <cassert>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <string>

extern "C"
{
#include <libavformat/avformat.h>
}

using winrt::NoiraPlayer::Native::implementation::HttpMediaInput;

int main()
{
    auto path = std::filesystem::temp_directory_path() / "noiraplayer-http-media-input-test.bin";
    {
        std::ofstream output(path, std::ios::binary | std::ios::trunc);
        for (auto index = uint32_t{0}; index < 256 * 1024; ++index)
        {
            output.put(static_cast<char>(index % 251));
        }
    }

    HttpMediaInput input;
    AVDictionary* options = nullptr;
    AVIOInterruptCB interrupt{};
    input.Open(path.string(), &options, interrupt);
    av_dict_free(&options);

    auto formatContext = avformat_alloc_context();
    assert(formatContext != nullptr);
    input.Attach(formatContext);
    assert((formatContext->flags & AVFMT_FLAG_CUSTOM_IO) != 0);

    std::array<uint8_t, 4096> buffer{};
    auto read = avio_read(formatContext->pb, buffer.data(), static_cast<int>(buffer.size()));
    assert(read == static_cast<int>(buffer.size()));
    assert(buffer[0] == 0);

    auto size = avio_size(formatContext->pb);
    assert(size == 256 * 1024);
    auto position = avio_seek(formatContext->pb, 192 * 1024, SEEK_SET);
    assert(position == 192 * 1024);
    read = avio_read(formatContext->pb, buffer.data(), static_cast<int>(buffer.size()));
    assert(read == static_cast<int>(buffer.size()));

    auto snapshot = input.Snapshot();
    assert(snapshot.EvidenceAvailable);
    assert(snapshot.Provider == "instrumented-ffmpeg-avio");
    assert(snapshot.ReadCalls >= 2);
    assert(snapshot.SeekCalls >= 2);
    assert(snapshot.BytesRead >= buffer.size() * 2);
    assert(snapshot.SeekDistanceBytes >= 192 * 1024);
    assert(snapshot.ReadWaitMs >= 0.0);
    assert(snapshot.SeekWaitMs >= 0.0);
    assert(snapshot.LastError == 0);

    avformat_free_context(formatContext);
    input.Close();
    input.Close();
    assert(!input.IsOpen());

    std::filesystem::remove(path);
    return 0;
}
