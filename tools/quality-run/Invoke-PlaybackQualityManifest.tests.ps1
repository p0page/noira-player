param()

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runner = Join-Path $PSScriptRoot 'Invoke-PlaybackQualityManifest.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    'playback-quality-manifest-runner-' + [Guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $manifestPath = Join-Path $tempRoot 'manifest.json'
    $reportsDir = Join-Path $tempRoot 'reports'
    $summaryPath = Join-Path $tempRoot 'summary.json'
    $invocationLog = Join-Path $tempRoot 'invocations.txt'
    $fakeHarness = Join-Path $tempRoot 'fake-harness.ps1'
    $fakeResolver = Join-Path $tempRoot 'fake-resolver.ps1'
    $resolverState = Join-Path $tempRoot 'resolver-state.txt'
    $runtimeSourceMapPath = Join-Path $tempRoot 'runtime-source-map.json'
    $fakeHelper = Join-Path $tempRoot 'fake-helper.exe'
    Set-Content -LiteralPath $fakeHelper -Value '' -Encoding ASCII
    @{ 'runner/second-runs' = 'http://127.0.0.1:54321/runtime-media.mp4' } |
        ConvertTo-Json | Set-Content -LiteralPath $runtimeSourceMapPath -Encoding UTF8

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "runner/first-fails",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "https://media.invalid/first.mp4",
      "pauseSeconds": 1,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "pause-resume" },
      "purpose": [ "error-handling" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/second-runs",
      "category": "challenge",
      "severity": "medium",
      "stability": "variable",
      "uri": "https://media.invalid/second.mp4",
      "startPositionTicks": 20000000,
      "seekTargetPositionTicks": 900000000,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "timeline" },
      "purpose": [ "timeline" ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 24,
        "hdrKind": "Hdr10",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/emby-resolved",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "emby://items/private-item",
      "itemId": "private-item",
      "mediaSourceId": "private-source",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [ "sdr-smoke" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/audio-switch",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "https://media.invalid/audio-switch.mp4",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "audio-switch" },
      "purpose": [ "tracks", "audio-switch" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/subtitle-switch",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "https://media.invalid/subtitle-switch.mp4",
      "startPositionTicks": 600000000,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "subtitle-switch" },
      "purpose": [ "subtitles", "subtitle-switch" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/stale-pass-must-not-survive",
      "category": "stable",
      "severity": "critical",
      "stability": "stable",
      "uri": "https://media.invalid/stale.mp4",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [ "error-handling" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/mismatched-attempt-must-not-pass",
      "category": "stable",
      "severity": "critical",
      "stability": "stable",
      "uri": "https://media.invalid/mismatched-attempt.mp4",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [ "error-handling" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/emby-unresolved",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "emby://items/missing-item",
      "itemId": "missing-item",
      "mediaSourceId": "missing-source",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [ "error-handling" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/runtime-source-required",
      "category": "stable",
      "severity": "critical",
      "stability": "stable",
      "uri": "local-fault://runtime-source-required",
      "pauseSeconds": 1,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "pause-resume" },
      "purpose": [ "pause-resume", "network-recovery" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/emby-delayed-recovery",
      "category": "challenge",
      "severity": "high",
      "stability": "variable",
      "uri": "emby://items/delayed-item",
      "itemId": "delayed-item",
      "mediaSourceId": "delayed-source",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [ "network-recovery" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/emby-cache-reuse",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "emby://items/private-item",
      "itemId": "private-item",
      "mediaSourceId": "private-source",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [ "source-resolution-cache" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    },
    {
      "caseId": "runner/quarantine-omitted",
      "category": "quarantine",
      "severity": "low",
      "stability": "flaky",
      "uri": "https://media.invalid/quarantine.mp4",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [ "error-handling" ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30,
        "hdrKind": "Sdr",
        "isDirectPlayable": true
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    @'
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

function Get-Value([string]$Name) {
    $index = [Array]::IndexOf($Arguments, $Name)
    if ($index -lt 0 -or $index + 1 -ge $Arguments.Count) {
        return ''
    }

    return $Arguments[$index + 1]
}

$caseId = Get-Value '--case-id'
$reportsDir = Get-Value '--reports-dir'
$logPath = $env:NOIRAPLAYER_MANIFEST_RUNNER_TEST_LOG
$pauseSeconds = Get-Value '--pause-seconds'
$startPositionTicks = Get-Value '--start-position-ticks'
$seekTargetPositionTicks = Get-Value '--seek-target-position-ticks'
$scenario = Get-Value '--scenario'
$streamUrl = Get-Value '--stream-url'
$locatorHash = Get-Value '--source-locator-hash'
$attemptId = Get-Value '--attempt-id'
$referenceCaseBase64 = Get-Value '--reference-case-base64'
$referenceCase = if ([string]::IsNullOrWhiteSpace($referenceCaseBase64)) {
    $null
} else {
    [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($referenceCaseBase64)) | ConvertFrom-Json
}
$seekPacketCacheEnabled = [Array]::IndexOf($Arguments, '--enable-seek-packet-cache') -ge 0
$referenceEvidence = if ($null -eq $referenceCase) {
    'reference=missing'
} else {
    'reference=' + $referenceCase.caseId + ',' + $referenceCase.category + ',' + $referenceCase.severity + ',' + $referenceCase.stability + ',' + $referenceCase.expected.hdrKind
}
Add-Content -LiteralPath $logPath -Value ($caseId + '|pause=' + $pauseSeconds + '|start=' + $startPositionTicks + '|seek=' + $seekTargetPositionTicks + '|scenario=' + $scenario + '|stream=' + $streamUrl + '|locator=' + $locatorHash + '|seekCache=' + $seekPacketCacheEnabled.ToString().ToLowerInvariant() + '|' + $referenceEvidence) -Encoding UTF8

$reportPath = Join-Path $reportsDir ($caseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json')
if ($caseId -like '*stale-pass-must-not-survive') {
    exit 9
}
New-Item -ItemType Directory -Path (Split-Path -Parent $reportPath) -Force | Out-Null
$reportedAttemptId = if ($caseId -like '*mismatched-attempt-must-not-pass') {
    '00000000000000000000000000000000'
} else {
    $attemptId
}
@{
    schemaVersion = 1
    caseMetadata = @{ caseId = $caseId }
    report = @{
        runId = $caseId
        result = $(if ($caseId -like '*first-fails') { 'error' } else { 'pass' })
        execution = @{ attemptId = $reportedAttemptId }
    }
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Output ('report=' + $reportPath)

if ($caseId -like '*first-fails') {
    exit 7
}

exit 0
'@ | Set-Content -LiteralPath $fakeHarness -Encoding UTF8

    @'
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
$itemIdIndex = [Array]::IndexOf($Arguments, '--item-id')
$itemId = if ($itemIdIndex -ge 0 -and $itemIdIndex + 1 -lt $Arguments.Count) {
    $Arguments[$itemIdIndex + 1]
} else {
    ''
}
$statePath = $env:NOIRAPLAYER_MANIFEST_RESOLVER_TEST_STATE
if ($itemId -in @('private-item', 'delayed-item')) {
    $priorAttempts = if (Test-Path -LiteralPath $statePath) {
        @(Get-Content -LiteralPath $statePath | Where-Object { $_ -eq $itemId }).Count
    } else {
        0
    }
    Add-Content -LiteralPath $statePath -Value $itemId
    $requiredFailures = if ($itemId -eq 'delayed-item') { 3 } else { 1 }
    if ($priorAttempts -lt $requiredFailures) {
        Write-Error 'resolver-error:transient-request-failed'
        exit 2
    }
}
if ($itemId -eq 'missing-item') {
    Write-Error 'resolver-error:media-source-not-found'
    exit 2
}
$resolved = 'https://resolved.invalid/media.mp4?api_key=private-token'
$encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($resolved))
Write-Output ('resolved-source-base64:' + $encoded)
exit 0
'@ | Set-Content -LiteralPath $fakeResolver -Encoding UTF8

    $env:NOIRAPLAYER_MANIFEST_RUNNER_TEST_LOG = $invocationLog
    $env:NOIRAPLAYER_MANIFEST_RESOLVER_TEST_STATE = $resolverState
    $staleReportPath = Join-Path $reportsDir 'runner\stale-pass-must-not-survive.json'
    New-Item -ItemType Directory -Path (Split-Path -Parent $staleReportPath) -Force | Out-Null
    @{
        schemaVersion = 1
        caseMetadata = @{ caseId = 'runner/stale-pass-must-not-survive' }
        report = @{ runId = 'runner/stale-pass-must-not-survive'; result = 'pass' }
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $staleReportPath -Encoding UTF8
    $staleStdoutPath = $staleReportPath + '.helper.stdout.log'
    $staleStderrPath = $staleReportPath + '.helper.stderr.log'
    Set-Content -LiteralPath $staleStdoutPath -Value 'stale stdout' -Encoding UTF8
    Set-Content -LiteralPath $staleStderrPath -Value 'stale stderr' -Encoding UTF8

    & powershell -NoProfile -ExecutionPolicy Bypass -File $runner `
        -ManifestPath $manifestPath `
        -ReportsDir $reportsDir `
        -NativeHelperExe $fakeHelper `
        -HarnessScriptPath $fakeHarness `
        -SourceResolverScriptPath $fakeResolver `
        -RuntimeSourceMapPath $runtimeSourceMapPath `
        -EnableSeekPacketCache `
        -SummaryPath $summaryPath
    $runnerExitCode = $LASTEXITCODE

    if ($runnerExitCode -eq 0) {
        throw 'Manifest runner must return non-zero when any selected case attempt fails.'
    }

    $invocations = @(Get-Content -LiteralPath $invocationLog -Encoding UTF8)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $locatorBytes = [Text.Encoding]::UTF8.GetBytes('https://media.invalid/second.mp4')
        $locatorDigest = $sha256.ComputeHash($locatorBytes)
        $secondLocatorHash = 'sha256:' + [BitConverter]::ToString($locatorDigest).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
    if ($invocations.Count -ne 9 -or
        $invocations[1] -ne ('runner/second-runs|pause=|start=20000000|seek=900000000|scenario=timeline|stream=http://127.0.0.1:54321/runtime-media.mp4|locator=' + $secondLocatorHash + '|seekCache=true|reference=runner/second-runs,challenge,medium,variable,Hdr10') -or
        $invocations[0] -notmatch '^runner/first-fails\|pause=1\|start=0\|seek=\|scenario=pause-resume\|.*\|seekCache=true\|reference=runner/first-fails,stable,high,stable,Sdr$' -or
        $invocations[2] -notmatch '^runner/emby-resolved\|pause=\|start=0\|seek=\|scenario=playback\|.*\|seekCache=true\|reference=runner/emby-resolved,stable,high,stable,Sdr$' -or
        $invocations[3] -notmatch '^runner/audio-switch\|pause=\|start=0\|seek=\|scenario=audio-switch\|.*\|seekCache=true\|reference=runner/audio-switch,stable,high,stable,Sdr$' -or
        $invocations[4] -notmatch '^runner/subtitle-switch\|pause=\|start=600000000\|seek=\|scenario=subtitle-switch\|.*\|seekCache=true\|reference=runner/subtitle-switch,stable,high,stable,Sdr$' -or
        $invocations[5] -notmatch '^runner/stale-pass-must-not-survive\|pause=\|start=0\|seek=\|scenario=playback\|.*\|seekCache=true\|reference=runner/stale-pass-must-not-survive,stable,critical,stable,Sdr$' -or
        $invocations[6] -notmatch '^runner/mismatched-attempt-must-not-pass\|pause=\|start=0\|seek=\|scenario=playback\|.*\|seekCache=true\|reference=runner/mismatched-attempt-must-not-pass,stable,critical,stable,Sdr$' -or
        $invocations[7] -notmatch '^runner/emby-delayed-recovery\|pause=\|start=0\|seek=\|scenario=playback\|.*\|seekCache=true\|reference=runner/emby-delayed-recovery,challenge,high,variable,Sdr$' -or
        $invocations[8] -notmatch '^runner/emby-cache-reuse\|pause=\|start=0\|seek=\|scenario=playback\|.*\|seekCache=true\|reference=runner/emby-cache-reuse,stable,high,stable,Sdr$') {
        throw 'Manifest runner must invoke each selected stable/challenge case exactly once and preserve order.'
    }

    $remainingStaleArtifacts = @($staleReportPath, $staleStdoutPath, $staleStderrPath) |
        Where-Object { Test-Path -LiteralPath $_ }
    if ($remainingStaleArtifacts.Count -gt 0) {
        throw ('A report or helper transcript left by an older run must not survive a failed current native attempt: ' +
            ($remainingStaleArtifacts -join ', '))
    }

    foreach ($caseId in @(
        'runner/first-fails',
        'runner/second-runs',
        'runner/emby-resolved',
        'runner/audio-switch',
        'runner/subtitle-switch',
        'runner/mismatched-attempt-must-not-pass',
        'runner/emby-unresolved',
        'runner/runtime-source-required',
        'runner/emby-delayed-recovery',
        'runner/emby-cache-reuse')) {
        $reportPath = Join-Path $reportsDir ($caseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json')
        if (-not (Test-Path -LiteralPath $reportPath)) {
            throw ('Manifest runner did not preserve a per-case report for ' + $caseId)
        }
    }

    $unresolvedReportPath = Join-Path $reportsDir 'runner\emby-unresolved.json'
    $unresolvedReport = Get-Content -LiteralPath $unresolvedReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($unresolvedReport.report.result -ne 'error' -or
        $unresolvedReport.report.error.code -ne 'manifest-runner.source-resolution-failed' -or
        $unresolvedReport.report.error.failureArea -ne 'unsupported-source' -or
        $unresolvedReport.report.execution.evidenceLevel -ne 'orchestration' -or
        $unresolvedReport.report.execution.scenario -ne 'playback' -or
        $unresolvedReport.report.execution.sourceOpenAttempted -or
        [string]::IsNullOrWhiteSpace($unresolvedReport.report.environment.collectorVersion) -or
        [string]::IsNullOrWhiteSpace($unresolvedReport.report.environment.playerCoreVersion) -or
        [string]::IsNullOrWhiteSpace($unresolvedReport.report.environment.sourceRevision) -or
        [string]::IsNullOrWhiteSpace($unresolvedReport.report.environment.buildConfiguration)) {
        throw 'Unresolved Emby source must produce an attributable structured orchestration error without claiming a source-open attempt.'
    }

    $runtimeSourceRequiredReport = Get-Content `
        -LiteralPath (Join-Path $reportsDir 'runner\runtime-source-required.json') `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($runtimeSourceRequiredReport.report.result -ne 'error' -or
        $runtimeSourceRequiredReport.report.error.code -ne 'manifest-runner.source-resolution-failed' -or
        $runtimeSourceRequiredReport.report.error.message -notmatch 'runtime-source-map-required' -or
        $runtimeSourceRequiredReport.report.error.failureClass -ne 'evaluation harness bug' -or
        $runtimeSourceRequiredReport.report.error.failureArea -ne 'evidence-collection' -or
        $runtimeSourceRequiredReport.report.error.operation -ne 'resolve-runtime-source' -or
        $runtimeSourceRequiredReport.report.execution.sourceOpenAttempted) {
        throw 'A local-fault locator without a runtime source map must fail before native source open with attributable evidence.'
    }

    $summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($summary.runnerVersion -ne 'native-manifest-runner-v0.4' -or
        $summary.selectedCaseCount -ne 11 -or
        $summary.attemptedCaseCount -ne 9 -or
        $summary.reportCount -ne 10 -or
        $summary.failedAttemptCount -ne 3 -or
        $summary.passReportCount -ne 6 -or
        $summary.errorReportCount -ne 3 -or
        $summary.nonPassReportCount -ne 4 -or
        $summary.unknownReportCount -ne 1 -or
        $summary.unresolvedSourceCount -ne 2 -or
        $summary.resolvedSourceCount -ne 3 -or
        $summary.uniqueSourceResolutionCount -ne 3 -or
        $summary.uniqueResolvedSourceCount -ne 2 -or
        $summary.sourceResolutionCacheHitCount -ne 1 -or
        $summary.unattributedReportCount -ne 1 -or
        $summary.missingReportCount -ne 1 -or
        $summary.seekPacketCacheEnabled -ne $true) {
        throw ('Manifest runner summary did not preserve selected/attempted/report/failure counts: ' +
            ($summary | ConvertTo-Json -Depth 8 -Compress))
    }
    $resolvedAttempt = @($summary.attempts | Where-Object caseId -eq 'runner/emby-resolved')[0]
    $unresolvedAttempt = @($summary.attempts | Where-Object caseId -eq 'runner/emby-unresolved')[0]
    $delayedRecoveryAttempt = @($summary.attempts | Where-Object caseId -eq 'runner/emby-delayed-recovery')[0]
    $cacheReuseAttempt = @($summary.attempts | Where-Object caseId -eq 'runner/emby-cache-reuse')[0]
    $secondAttempt = @($summary.attempts | Where-Object caseId -eq 'runner/second-runs')[0]
    $mismatchedAttempt = @($summary.attempts | Where-Object caseId -eq 'runner/mismatched-attempt-must-not-pass')[0]
    if ($resolvedAttempt.sourceResolutionAttemptCount -ne 2 -or
        $resolvedAttempt.sourceResolutionCacheHit -ne $false -or
        $unresolvedAttempt.sourceResolutionAttemptCount -ne 1 -or
        $delayedRecoveryAttempt.sourceResolutionAttemptCount -ne 4 -or
        $cacheReuseAttempt.sourceResolutionAttemptCount -ne 2 -or
        $cacheReuseAttempt.sourceResolutionCacheHit -ne $true) {
        throw 'Manifest runner must preserve bounded source-resolution retry evidence.'
    }
    $resolverAttempts = @(Get-Content -LiteralPath $resolverState)
    if (@($resolverAttempts | Where-Object { $_ -eq 'private-item' }).Count -ne 2 -or
        @($resolverAttempts | Where-Object { $_ -eq 'delayed-item' }).Count -ne 4) {
        throw 'Manifest runner must resolve each unique Emby source once and reuse it without re-authenticating.'
    }
    if ($secondAttempt.runnerAttemptId -notmatch '^[0-9a-f]{32}$' -or
        $secondAttempt.reportAttemptId -ne $secondAttempt.runnerAttemptId -or
        $secondAttempt.reportAttemptMatched -ne $true -or
        $mismatchedAttempt.runnerAttemptId -notmatch '^[0-9a-f]{32}$' -or
        $mismatchedAttempt.reportAttemptId -ne '00000000000000000000000000000000' -or
        $mismatchedAttempt.reportAttemptMatched -ne $false -or
        $mismatchedAttempt.status -ne 'invalid-report' -or
        $mismatchedAttempt.reportResult -ne 'unknown') {
        throw 'Manifest runner must bind each native invocation to the exact attempt identity returned by its report.'
    }

    $summaryText = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8
    if ($summaryText.Contains('resolved.invalid') -or $summaryText.Contains('private-token')) {
        throw 'Manifest runner summary must not persist resolved Emby URLs or tokens.'
    }

    $emptySummaryPath = Join-Path $tempRoot 'empty-summary.json'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $runner `
        -ManifestPath $manifestPath `
        -ReportsDir (Join-Path $tempRoot 'empty-reports') `
        -NativeHelperExe $fakeHelper `
        -HarnessScriptPath $fakeHarness `
        -CaseId 'runner/not-present' `
        -SummaryPath $emptySummaryPath
    if ($LASTEXITCODE -eq 0) {
        throw 'Manifest runner must reject an empty case selection.'
    }

    $emptySummary = Get-Content -LiteralPath $emptySummaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($emptySummary.selectedCaseCount -ne 0 -or $emptySummary.attemptedCaseCount -ne 0) {
        throw 'Empty-selection failure must preserve explicit zero selected/attempted counts.'
    }

    Write-Output 'playback-quality manifest runner tests ok'
    exit 0
}
finally {
    Remove-Item Env:NOIRAPLAYER_MANIFEST_RUNNER_TEST_LOG -ErrorAction SilentlyContinue
    Remove-Item Env:NOIRAPLAYER_MANIFEST_RESOLVER_TEST_STATE -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
