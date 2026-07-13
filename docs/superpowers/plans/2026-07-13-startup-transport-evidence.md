# Startup Transport Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add phase-level AVIO transport byte evidence to the real native startup path without changing playback behavior.

**Architecture:** Snapshot FFmpeg `AVIOContext::bytes_read` at startup boundaries, carry explicit counters through existing native metrics and WinRT/App/Core bridges, and annotate report component bytes with a semantic kind. The evaluator and helper parser treat the new contract as required v0.5 evidence.

**Tech Stack:** FFmpeg 8, C++/WinRT UWP native core, C# Core/App, xUnit, PowerShell native-headless runner.

## Global Constraints

- Preserve exact seek, stream discovery, reconnect policy, and decode/render behavior.
- Never infer transport bytes from packet payload, file size, probe output, or expected data.
- Every executable stable/challenge case must still enter the real native playback path.
- Use the same manifest for baseline and candidate reports.

---

### Task 1: Native AVIO phase counters

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.h`
- Modify: `src/NoiraPlayer.Native/Media/FfmpegMediaSource.cpp`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.cpp`
- Test: `tests/NoiraPlayer.Core.Tests/Design/NativeFfmpegDiagnosticsContractTests.cs`

**Interfaces:**
- Produce: `FfmpegOpenTimingSnapshot.OpenInputBytesRead`, `StreamInfoBytesRead`, and `FfmpegMediaSource::TransportBytesRead()`.
- Produce: `PlaybackQualityMetrics.FfmpegOpenInputBytesRead`, `FfmpegStreamInfoBytesRead`, and `NativeStartupSeekBytesRead`.

- [ ] Add source-contract tests requiring FFmpeg AVIO snapshots and phase-local assignments; run them and confirm they fail because the fields are absent.
- [ ] Implement a null-safe `AVIOContext::bytes_read` snapshot and saturating delta helper with diagnostic logging on counter regression.
- [ ] Snapshot before/after stream discovery and startup seek, then publish only phase-local byte deltas.
- [ ] Run focused design/native metrics tests and confirm they pass.

### Task 2: Bridge counters through native, WinRT, App, and Core

**Files:**
- Modify: `src/NoiraPlayer.Native/Media/PlaybackQualityMetrics.h`
- Modify: `src/NoiraPlayer.Native/NativePlaybackQualityMetrics.h`
- Modify: `src/NoiraPlayer.Native/NativePlaybackQualityMetrics.cpp`
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.idl`
- Modify generated C++/WinRT headers using the repository generator/build flow.
- Modify: `src/NoiraPlayer.App/Playback/WinRtNativePlaybackEngine.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityMetricsSnapshot.cs`
- Test: `tests/NoiraPlayer.Native.Tests/PlaybackQualityMetricsTests.cpp`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativeQualityMetricsBridgeContractTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/AppHostedQualityCaptureContractTests.cs`

**Interfaces:**
- Consume: native phase-local byte counters from Task 1.
- Produce: the same three `ulong` fields in `PlaybackQualityMetricsSnapshot` for both headless and App-hosted capture.

- [ ] Add failing bridge tests for all three fields at every boundary.
- [ ] Implement minimal storage, snapshot, IDL, projection, and C# mapping changes.
- [ ] Regenerate/build projections through the existing build command; do not hand-edit generated contracts unless that is the established repository flow.
- [ ] Run focused native and Core bridge tests and confirm they pass.

### Task 3: Report byte semantics and strict parsing

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityStartupEvidence.cs`
- Modify native helper output/parser files located by the existing startup metric names.
- Modify evaluator/version files located by `playback-quality-v0.4`.
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityStartupEvidenceTests.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityRuntimeEvidenceCollectorTests.cs`
- Test parser/evaluator suites adjacent to the modified code.

**Interfaces:**
- Produce: `PlaybackQualityStartupComponent.ByteKind` with exact values `avio-transport` or `demux-packet-payload`.
- Produce: evaluation version `playback-quality-v0.5` and required native startup transport fields.

- [ ] Add failing tests asserting the three transport components and the first-frame component carry exact byte values and kinds.
- [ ] Add failing parser tests showing missing v0.5 transport fields are rejected, including zero-valued fields being accepted when explicitly present.
- [ ] Implement report enrichment, serialization/parser propagation, and v0.5 validation.
- [ ] Run focused startup/parser/evaluator tests and confirm they pass.

### Task 4: Real baseline and final gates

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Keep private manifests, credentials, resolved URLs, and reports ignored.

**Interfaces:**
- Consume: v0.5 report contract from Tasks 1-3.
- Produce: one attributable same-manifest report-set and a decision on the next single-variable playback candidate.

- [ ] Run the documented public/private native-headless manifest command with credentials supplied only through process environment.
- [ ] Validate selected/attempted/reported counts and strict report identity; inspect byte evidence for every startup phase.
- [ ] Repeat representative start-at-zero and cold-resume cases to separate latency variance from stable byte volume.
- [ ] Run the full playback Core gate and complete App build.
- [ ] Run one representative App-hosted playback report and verify it contains the same v0.5 fields.
- [ ] Record evidence, limitations, and the next candidate in `STATUS.md` and `DECISIONS.md`; do not claim a Core performance improvement in this instrumentation-only slice.
