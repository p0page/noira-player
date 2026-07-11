# Task 1 Report

## Outcome

`DONE`

Failed lifecycle operations are now formal evaluator failures. Each `failed` or
`error` event produces a failed `PlaybackQualityCheck` with signal
`lifecycle.<operation>`, the required failure-area mapping, and either the
original event message or a stable fallback message. Other statuses, including
`completed` and `skipped`, remain non-failing.

## RED

Command:

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityEvaluatorTests
```

Result: expected failure, with 5 failed and 24 passed tests out of 29.

Failure reason: every new `failed`/`error` lifecycle case expected report result
`fail`, but the evaluator returned `pass` because it did not inspect
`PlaybackQualityLifecycle.Events`. The new `completed` and `skipped` cases
passed during RED, confirming that the test boundary was specific to failure
statuses.

## GREEN

Targeted command:

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityEvaluatorTests
```

Result: PASS, 29 passed, 0 failed, 0 skipped.

Full Core command:

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj
```

Result: PASS, 792 passed, 0 failed, 0 skipped.

## Modified Files

- `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityEvaluatorTests.cs`
- `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityEvaluator.cs`
- `.superpowers/sdd/task-1-report.md`

## Commit

- Baseline: `9a8d250`
- Message: `test: fail reports on lifecycle operation errors`
- The final commit hash is reported in the task completion response because
  this report is part of that commit.

## Self-Review

- The lifecycle gate runs after threshold checks and before failure-class
  assignment.
- Status handling is limited to exact `failed` and `error` values.
- Failure areas cover `audio-switch -> tracks`, `subtitle-switch` and
  `subtitle-off -> subtitles`, `seek -> timeline`, and the
  `playback-lifecycle` fallback.
- Event messages are preserved; blank messages use a deterministic fallback.
- No App, manifest, threshold, or other source files were changed.

## Concerns

No implementation concerns. A pre-existing modification to
`docs/superpowers/plans/2026-07-11-native-interaction-evidence-and-subtitle-resync.md`
belongs to another worker and is intentionally left untouched and excluded from
this commit.

## Review Follow-Up: Evaluator Early Returns

Two review findings were reproduced and fixed after the original Task 1
commit. The lifecycle gate had been placed after both the `Expected == null`
early return and the expected-unsupported early return, allowing lifecycle
failures to be reported as `observed` or `unsupported`.

### Follow-Up RED

First command, after adding only the `Expected == null` regression test:

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityEvaluatorTests
```

Result: expected failure, 1 failed and 29 passed out of 30. The new test
expected `fail` but received `observed`.

Second command, after separately adding the expected-unsupported regression
test:

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityEvaluatorTests
```

Result: expected failure, 2 failed and 29 passed out of 31. The two new tests
expected `fail` but received `observed` and `unsupported`, respectively.

### Follow-Up Fix

`CheckFailedLifecycleOperations` now runs before evaluator early returns. When
`Expected` is null and lifecycle failures exist, the evaluator assigns failure
classes, returns `fail`, and performs failure analysis. With no lifecycle
failure, the existing `observed` behavior is unchanged. The existing
expected-unsupported branch now sees lifecycle failure reasons and returns
`fail`; without lifecycle failures it still returns `unsupported`.

### Follow-Up GREEN

Targeted command:

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj --filter FullyQualifiedName~PlaybackQualityEvaluatorTests
```

Result: PASS, 31 passed, 0 failed, 0 skipped.

Full Core command:

```powershell
dotnet test tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj
```

Result: PASS, 794 passed, 0 failed, 0 skipped.

### Follow-Up Commit And Concerns

- Parent commit: `c310f2e` (`docs: keep subtitle selection state canonical`)
- Commit message: `fix: honor lifecycle failures before evaluator early returns`
- Modified files remain limited to the evaluator, its tests, and this report.
- No implementation concerns. The `c310f2e` docs commit remains intact and was
  not modified or reverted.
