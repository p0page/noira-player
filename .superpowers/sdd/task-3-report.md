# Task 3 Report: native-headless A/V interaction evidence

## Status

`DONE_WITH_CONCERNS`

The native-headless smoke now generates a strict six-second dual-audio,
dual-subtitle sample, executes real track, subtitle, and non-zero seek
interactions, and preserves each observed outcome in the report lifecycle. The
smoke passes while the old playback Core is honestly reported as failing both
subtitle switches because no cue overlay draw was observed.

## Scope

Changed only the task-owned files:

- `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`
- `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`
- `.superpowers/sdd/task-3-report.md`

No Native playback policy, SubtitleRenderer, App/UI, manifest, evaluation
threshold, or general documentation file was changed.

## TDD Evidence

### RED

The smoke assertions were tightened before the sample, helper, or parser was
changed.

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-native-headless-harness-smoke-test.ps1
```

Result: expected exit code `1`.

```text
Expected native helper A/V report to include exactly two discovered audio tracks.
```

The generated old sample/report confirmed the intended missing behavior:

- sample duration: `2.5s`
- streams: `1 video / 1 audio / 1 subtitle`
- `seekTargetPositionTicks`: `0`
- lifecycle: `load, play, pause, resume, seek, stop`
- report result: `pass`

### GREEN

The same smoke command was run after implementation.

Result: exit code `0`.

```text
native-headless-harness smoke ok
```

The helper was also run directly against the generated A/V sample. It returned
exit code `0` and 172 key/value fields even though both subtitle interactions
failed:

```text
audioSwitchStatus=completed
audioSwitchStreamIndex=2
audioSwitchPositionBeforeTicks=15100000
audioSwitchPositionAfterTicks=19933333
audioSwitchSubmittedFramesBefore=81
audioSwitchSubmittedFramesAfter=116
subtitleSwitch1Status=failed
subtitleSwitch1CueCountBefore=0
subtitleSwitch1CueCountAfter=0
subtitleSwitch2Status=failed
subtitleSwitch2CueCountBefore=0
subtitleSwitch2CueCountAfter=0
subtitleOffStatus=completed
seekStatus=completed
seekTargetPositionTicks=10000000
seekActualPositionTicks=10000000
postSeekPlaybackPositionTicks=24666667
selectedAudioStreamIndex=2
selectedSubtitleStreamIndex=-1
```

The direct helper run also returned `submittedAudioFrames=81`, equal to the
audio-switch before value. This confirms that cadence, A/V, and buffering
metrics came from the snapshot taken before any interaction.

## Implementation

### Sample

- The generated MP4 maps one video, two AAC audio, and two `mov_text` subtitle
  streams.
- Audio languages/defaults are `eng/default` and `jpn/non-default`.
- Subtitle languages/defaults are `eng/default` and `spa/non-default`.
- Both SRT cues cover `00:00:00,000` through `00:00:06,000`.
- FFmpeg uses an explicit six-second output duration and no `-shortest`.
- The existing three-second A/V observation window remains separate from the
  six-second media duration.
- An ffprobe gate verifies actual duration, stream counts, languages, and
  dispositions. The final sample was `6.000000s` with `1V/2A/2S`.

### Native Helper

- `playbackSnapshot`, source metadata, and source tracks are captured before
  pause/resume or any new interaction.
- The second audio stream and both subtitle streams come from
  `SourceTrackSnapshots()`; no filename or case-id classification is used.
- Audio completion requires both position progress and increased submitted
  audio frames after the approximately 500ms wait.
- Each subtitle completion requires an increased `SubtitleCueRenderCount()`.
- Subtitle-off completion requires `SelectedSubtitleStreamIndex()` to be empty.
- Seek targets exactly `10,000,000` ticks, records immediate and post-seek
  positions, and completes only when playback advances after the landing.
- Every new operation has an independent `try/catch`; a failed item remains in
  stdout and does not make the helper process fail.

### C# Mapping

- `NativeHeadlessHelperResult` now retains audio, two subtitle, subtitle-off,
  seek, and final selected-track outcomes.
- Lifecycle events are added only for attempted operations and preserve the
  helper's exact `completed` or `failed` status.
- Messages contain stream indexes plus position, submitted-frame, cue-overlay,
  or seek landing evidence.
- `CreateDescriptor` uses the helper's final selected audio/subtitle indexes.
- Seek error is calculated from the absolute target/actual difference. The
  existing smoke threshold remains unchanged at 250ms.

## Final Report Evidence

The captured and materialized A/V reports both contain:

- tracks: `1 video / 2 audio / 2 subtitle`
- selected audio stream: `2`
- subtitles disabled and selected subtitle stream: null
- seek target/actual/error: `10000000 / 10000000 / 0ms`
- audio switch: `completed`
- subtitle switches: `failed`, `failed`
- subtitle off: `completed`
- seek: `completed`
- report/model result: `fail`
- failure area: `subtitles`
- failure reasons: subtitle streams 3 and 4 each had cue overlay render count
  `0->0`
- error code: empty

This is a playback outcome failure, not an evidence-collection or harness
failure.

## Verification

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-native-headless-harness-smoke-test.ps1
```

PASS: exit `0`, `native-headless-harness smoke ok`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.tests.ps1
```

PASS: `playback-core-checks plan ok`.

```powershell
dotnet build .\tools\NoiraPlayer.PlaybackQuality.Headless\NoiraPlayer.PlaybackQuality.Headless.csproj --no-restore -v minimal
```

PASS: 0 warnings, 0 errors.

```powershell
dotnet test .\tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~PlaybackQualityEvaluatorTests" -v minimal
```

PASS: 31 passed, 0 failed, 0 skipped.

```powershell
git diff --check
```

PASS: no whitespace errors. Git printed only line-ending conversion notices.

## Self-Review

- Pre-interaction snapshot ordering is preserved.
- The actual media duration is six seconds and is not subtitle-shortened.
- No attempted operation is labeled completed without its required observed
  condition.
- Final selected tracks match the report descriptor.
- The non-zero seek target and existing 250ms rule are unchanged in meaning.
- Individual interaction failure preserves full stdout/report and process exit
  zero.
- The diff is restricted to the four owned files.

## Concerns

- Expected old-Core limitation: both subtitle switches currently fail because
  the successful cue-overlay draw count remains `0`. This task records that
  failure and deliberately does not change PlaybackGraph, SubtitleRenderer, or
  playback policy.
- Non-blocking existing build warning: the native helper build emits Windows
  SDK generated-header warning `C4002` for the `GetCurrentTime` macro. The
  helper builds and all requested smoke behavior completes.

## Commit

The independent commit hash is returned in the task completion response because
this report is part of that commit.
