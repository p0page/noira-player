#include <cassert>

#include "Media/PlaybackGraph.h"

using winrt::NextGenEmby::Native::implementation::ResolveHdrOutputDecisionForFrame;
using winrt::NextGenEmby::Native::implementation::VideoHdrKind;

int main()
{
    auto firstSdr = ResolveHdrOutputDecisionForFrame(false, false, VideoHdrKind::None, true);
    assert(firstSdr.ShouldRequestDisplayChange);
    assert(!firstSdr.DesiredHdrOutput);

    auto firstHdr10 = ResolveHdrOutputDecisionForFrame(false, false, VideoHdrKind::Hdr10, true);
    assert(firstHdr10.ShouldRequestDisplayChange);
    assert(firstHdr10.DesiredHdrOutput);

    auto firstHdrWithoutTenBit =
        ResolveHdrOutputDecisionForFrame(false, false, VideoHdrKind::Hdr10, false);
    assert(firstHdrWithoutTenBit.ShouldRequestDisplayChange);
    assert(!firstHdrWithoutTenBit.DesiredHdrOutput);

    auto sameHdr = ResolveHdrOutputDecisionForFrame(true, true, VideoHdrKind::Hdr10, true);
    assert(!sameHdr.ShouldRequestDisplayChange);
    assert(sameHdr.DesiredHdrOutput);

    auto hdrToSdr = ResolveHdrOutputDecisionForFrame(true, true, VideoHdrKind::None, true);
    assert(hdrToSdr.ShouldRequestDisplayChange);
    assert(!hdrToSdr.DesiredHdrOutput);

    return 0;
}
