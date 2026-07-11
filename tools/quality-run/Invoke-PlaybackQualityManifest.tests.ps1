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
Add-Content -LiteralPath $logPath -Value $caseId -Encoding UTF8

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

    $env:NOIRAPLAYER_MANIFEST_RUNNER_TEST_LOG = $invocationLog
    & powershell -NoProfile -ExecutionPolicy Bypass -File $runner `
        -ManifestPath $manifestPath `
        -ReportsDir $reportsDir `
        -NativeHelperExe $fakeHelper `
        -HarnessScriptPath $fakeHarness `
        -SummaryPath $summaryPath
    $runnerExitCode = $LASTEXITCODE

    if ($runnerExitCode -eq 0) {
        throw 'Manifest runner must return non-zero when any selected case attempt fails.'
    }

    $invocations = @(Get-Content -LiteralPath $invocationLog -Encoding UTF8)
    if ($invocations.Count -ne 2 -or
        $invocations[0] -ne 'runner/first-fails' -or
        $invocations[1] -ne 'runner/second-runs') {
        throw 'Manifest runner must invoke each selected stable/challenge case exactly once and preserve order.'
    }

    foreach ($caseId in @('runner/first-fails', 'runner/second-runs')) {
        $reportPath = Join-Path $reportsDir ($caseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json')
        if (-not (Test-Path -LiteralPath $reportPath)) {
            throw ('Manifest runner did not preserve a per-case report for ' + $caseId)
        }
    }

    $summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($summary.selectedCaseCount -ne 2 -or
        $summary.attemptedCaseCount -ne 2 -or
        $summary.reportCount -ne 2 -or
        $summary.failedAttemptCount -ne 1 -or
        $summary.missingReportCount -ne 0) {
        throw 'Manifest runner summary did not preserve selected/attempted/report/failure counts.'
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
