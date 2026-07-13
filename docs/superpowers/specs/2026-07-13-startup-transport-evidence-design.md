# Startup Transport Evidence Design

## Goal

Make remote startup failures attributable to transport work instead of inferring their cause from duration alone. The report must distinguish AVIO transport bytes consumed by FFmpeg startup phases from demux packet payload bytes consumed while producing the first frame.

## Evidence Gap

The v0.4 reports already measure `avformat_open_input`, `avformat_find_stream_info`, startup seek, and first-frame work. Remote cases show repeatable 8-21 second startup, but the first three phases report no byte evidence. A slow phase can therefore mean connection latency, a large probe, a Range request, or low throughput, and the evaluator cannot tell these apart.

Kodi, mpv, and VLC retain stream discovery for normal media rather than globally weakening probe correctness. mpv and VLC additionally expose or build caching and byte-accounting around demux. The first change should therefore improve attribution, not tune `probesize`, `analyzeduration`, seek precision, or HTTP connection behavior.

## Contract

Add phase-local AVIO byte deltas for:

- `ffmpeg.open-input`
- `ffmpeg.find-stream-info`
- `native.startup-seek`
- `native.first-frame.demux-read`

Startup components expose separate `transportBytes` and `packetPayloadBytes` fields. The first three phases populate transport bytes only. `native.first-frame.demux-read` populates both the `AVIOContext::bytes_read` delta and accumulated `AVPacket::size`, so network/cache reads cannot be confused with compressed packet payload.

Zero is valid when FFmpeg exposes no AVIO context or the phase performs no transport read. Counter regression is not silently subtracted: the delta is zero and the native diagnostic log records the regression. Reports without the new fields are rejected by the updated native helper parser rather than reconstructed from timing, expected values, or probe metadata.

## Boundaries

- No playback policy changes in this slice.
- No custom AVIO, persistent HTTP session, global probe limit, or inaccurate seek.
- Preserve manifest-to-execution-to-report identity and strict real-native evidence rules.
- Bump the evaluation contract to `playback-quality-v0.5` because startup evidence semantics become stricter.
- After implementation, run the same public/private manifest and compare repeated phase durations and byte counts before selecting one tuning candidate.

## Validation

Tests must prove native counter snapshots and saturating deltas, C++/WinRT/App/Core propagation, startup component byte semantics, parser rejection of missing evidence, and report serialization. Completion requires focused tests, the full playback Core gate, a real native-headless report-set, a full App build, and one representative App-hosted playback report.
