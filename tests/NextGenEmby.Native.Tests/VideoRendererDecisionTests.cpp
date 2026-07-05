#include <cassert>

#include "Media/VideoRenderer.h"

using winrt::NextGenEmby::Native::implementation::CreateHlgReferenceHdr10Metadata;
using winrt::NextGenEmby::Native::implementation::ShouldOutputHdr10ForFrame;
using winrt::NextGenEmby::Native::implementation::VideoHdrKind;

int main()
{
    assert(ShouldOutputHdr10ForFrame(VideoHdrKind::Hdr10, true));
    assert(ShouldOutputHdr10ForFrame(VideoHdrKind::Hlg, true));
    assert(!ShouldOutputHdr10ForFrame(VideoHdrKind::None, true));
    assert(!ShouldOutputHdr10ForFrame(VideoHdrKind::Hdr10, false));
    assert(!ShouldOutputHdr10ForFrame(VideoHdrKind::Hlg, false));

    auto hlgMetadata = CreateHlgReferenceHdr10Metadata();
    assert(hlgMetadata.RedPrimary[0] == 34000);
    assert(hlgMetadata.RedPrimary[1] == 16000);
    assert(hlgMetadata.GreenPrimary[0] == 13250);
    assert(hlgMetadata.GreenPrimary[1] == 34500);
    assert(hlgMetadata.BluePrimary[0] == 7500);
    assert(hlgMetadata.BluePrimary[1] == 3000);
    assert(hlgMetadata.WhitePoint[0] == 15635);
    assert(hlgMetadata.WhitePoint[1] == 16450);
    assert(hlgMetadata.MaxMasteringLuminance == 1000 * 10000);
    assert(hlgMetadata.MinMasteringLuminance == 50);
    return 0;
}
