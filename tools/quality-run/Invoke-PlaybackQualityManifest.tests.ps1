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
    $fakeHelper = Join-Path $tempRoot 'fake-helper.exe'
    Set-Content -LiteralPath $fakeHelper -Value '' -Encoding ASCII

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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback" },
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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback" },
      "purpose": [ "sdr-smoke" ],
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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback" },
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
      "caseId": "runner/emby-unresolved",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "emby://items/missing-item",
      "itemId": "missing-item",
      "mediaSourceId": "missing-source",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback" },
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
      "caseId": "runner/quarantine-omitted",
      "category": "quarantine",
      "severity": "low",
      "stability": "flaky",
      "uri": "https://media.invalid/quarantine.mp4",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback" },
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
Add-Content -LiteralPath $logPath -Value ($caseId + '|pause=' + $pauseSeconds) -Encoding UTF8

$reportPath = Join-Path $reportsDir ($caseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json')
New-Item -ItemType Directory -Path (Split-Path -Parent $reportPath) -Force | Out-Null
@{
    schemaVersion = 1
    caseMetadata = @{ caseId = $caseId }
    report = @{ runId = $caseId; result = $(if ($caseId -like '*first-fails') { 'error' } else { 'pass' }) }
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
    & powershell -NoProfile -ExecutionPolicy Bypass -File $runner `
        -ManifestPath $manifestPath `
        -ReportsDir $reportsDir `
        -NativeHelperExe $fakeHelper `
        -HarnessScriptPath $fakeHarness `
        -SourceResolverScriptPath $fakeResolver `
        -SummaryPath $summaryPath
    $runnerExitCode = $LASTEXITCODE

    if ($runnerExitCode -eq 0) {
        throw 'Manifest runner must return non-zero when any selected case attempt fails.'
    }

    $invocations = @(Get-Content -LiteralPath $invocationLog -Encoding UTF8)
    if ($invocations.Count -ne 3 -or
        $invocations[0] -ne 'runner/first-fails|pause=1' -or
        $invocations[1] -ne 'runner/second-runs|pause=' -or
        $invocations[2] -ne 'runner/emby-resolved|pause=') {
        throw 'Manifest runner must invoke each selected stable/challenge case exactly once and preserve order.'
    }

    foreach ($caseId in @(
        'runner/first-fails',
        'runner/second-runs',
        'runner/emby-resolved',
        'runner/emby-unresolved')) {
        $reportPath = Join-Path $reportsDir ($caseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json')
        if (-not (Test-Path -LiteralPath $reportPath)) {
            throw ('Manifest runner did not preserve a per-case report for ' + $caseId)
        }
    }

    $unresolvedReportPath = Join-Path $reportsDir 'runner\emby-unresolved.json'
    $unresolvedReport = Get-Content -LiteralPath $unresolvedReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($unresolvedReport.report.result -ne 'error' -or
        $unresolvedReport.report.error.code -ne 'manifest-runner.source-resolution-failed' -or
        $unresolvedReport.report.execution.evidenceLevel -ne 'orchestration' -or
        $unresolvedReport.report.execution.sourceOpenAttempted) {
        throw 'Unresolved Emby source must produce a structured orchestration error report without claiming a source-open attempt.'
    }

    $summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($summary.selectedCaseCount -ne 4 -or
        $summary.attemptedCaseCount -ne 3 -or
        $summary.reportCount -ne 4 -or
        $summary.failedAttemptCount -ne 1 -or
        $summary.unresolvedSourceCount -ne 1 -or
        $summary.resolvedSourceCount -ne 1 -or
        $summary.missingReportCount -ne 0) {
        throw 'Manifest runner summary did not preserve selected/attempted/report/failure counts.'
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
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
