$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$scriptPath = Join-Path $repoRoot 'tools\quality-run\New-PlaybackCoreTuningBaseline.ps1'
$scriptSource = Get-Content -Raw -LiteralPath $scriptPath
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('playback-core-tuning-baseline-test-' + [Guid]::NewGuid().ToString('N'))

try {
    if ($scriptSource -match 'materialize-core-probe-report-set') {
        throw 'Formal playback baseline must not materialize core-probe reports for playback cases.'
    }

    if ($scriptSource -notmatch 'Invoke-PlaybackQualityManifest\.ps1') {
        throw 'Formal playback baseline must execute its core manifest through the native manifest runner.'
    }

    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $manifestPath = Join-Path $tempRoot 'manifest.json'
    $fakeHarness = Join-Path $tempRoot 'fake-harness.ps1'
    $fakeHelper = Join-Path $tempRoot 'fake-helper.exe'
    Set-Content -LiteralPath $fakeHelper -Value '' -Encoding ASCII

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "baseline/native-open-error",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "https://media.invalid/open-error.mp4",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [ "error-handling" ],
      "expected": {
        "codec": "hevc",
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
    if ($index -lt 0 -or $index + 1 -ge $Arguments.Count) { return '' }
    return $Arguments[$index + 1]
}

if ($env:NOIRAPLAYER_BASELINE_TEST_OMIT_REPORT -eq '1') {
    exit 0
}

$caseId = Get-Value '--case-id'
$runId = $caseId
$reportsDir = Get-Value '--reports-dir'
$locatorHash = if ($env:NOIRAPLAYER_BASELINE_TEST_MISMATCH_RUN_ID -eq '1') {
    'sha256:' + ('f' * 64)
} else {
    Get-Value '--source-locator-hash'
}
$scenario = Get-Value '--scenario'
$reportPath = Join-Path $reportsDir ($caseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json')
New-Item -ItemType Directory -Path (Split-Path -Parent $reportPath) -Force | Out-Null
@{
    schemaVersion = 1
    evaluationVersion = 'playback-quality-v0.3'
    caseMetadata = @{
        caseId = $caseId
        category = 'stable'
        severity = 'high'
        stability = 'stable'
    }
    report = @{
        runId = $runId
        metricVersion = 'software-quality-v1'
        result = 'error'
        environment = @{
            collectorVersion = 'baseline-test-native-harness'
            playerCoreVersion = 'baseline-test-core'
            sourceRevision = 'baseline-test-revision'
            buildConfiguration = 'Debug'
        }
        execution = @{
            attemptId = 'baseline-test-attempt'
            runner = 'native-headless'
            scenario = $scenario
            evidenceLevel = 'native-playback'
            status = 'failed'
            sourceLocatorHash = $locatorHash
            openedSourceHash = ''
            startedAtUtc = '2026-07-11T00:00:00.0000000+00:00'
            durationMs = 1.0
            sourceOpenAttempted = $true
            sourceOpened = $false
            nativeGraphOpened = $false
            demuxStarted = $false
            decoderOpened = $false
            playbackSampleObserved = $false
        }
        error = @{
            code = 'source.open.test-error'
            message = 'The fixture source failed to open.'
            operation = 'open'
            exceptionType = 'FixtureException'
            failureClass = 'sample issue'
            failureArea = 'error-handling'
            isTerminal = $true
            isRetriable = $false
        }
        lifecycle = @{
            events = @(@{
                operation = 'open'
                status = 'error'
                message = 'The fixture source failed to open.'
            })
        }
    }
} | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $reportPath -Encoding UTF8
exit 1
'@ | Set-Content -LiteralPath $fakeHarness -Encoding UTF8

    $outputRoot = Join-Path $tempRoot 'baseline-output'
    powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
        -PublicManifestPath $manifestPath `
        -NoPrivateManifest `
        -SkipNativeHeadless `
        -NativeHelperExe $fakeHelper `
        -ManifestRunnerHarnessScriptPath $fakeHarness `
        -DurationSeconds 1 `
        -AttemptTimeoutSeconds 5 `
        -SourceRevision 'baseline-test-revision' `
        -PlayerCoreVersion 'baseline-test-core' `
        -OutputRoot $outputRoot
    if ($LASTEXITCODE -ne 0) {
        throw 'New-PlaybackCoreTuningBaseline.ps1 returned a non-zero exit code.'
    }

    $summaryPath = Join-Path $outputRoot 'baseline-summary.local.json'
    $manifestOutputPath = Join-Path $outputRoot 'manifests\unified-reference-manifest.local.json'
    $validationPath = Join-Path $outputRoot 'summaries\report-set-validation.local.json'
    $analysisPath = Join-Path $outputRoot 'summaries\report-analysis-summary.local.json'
    $runnerSummaryPath = Join-Path $outputRoot 'summaries\manifest-run-summary.local.json'
    $reportPath = Join-Path $outputRoot 'reports\baseline\native-open-error.json'

    foreach ($path in @($summaryPath, $manifestOutputPath, $validationPath, $analysisPath, $runnerSummaryPath, $reportPath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw ('Expected baseline artifact was not written: ' + $path)
        }
    }

    $summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
    $validation = Get-Content -Raw -LiteralPath $validationPath | ConvertFrom-Json
    $runnerSummary = Get-Content -Raw -LiteralPath $runnerSummaryPath | ConvertFrom-Json
    $materializedReport = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    if ($summary.validation.isValid -ne $true -or
        $summary.analysis.totalReportCount -ne 1 -or
        $validation.executionValid -ne $true -or
        $validation.matchedCaseCount -ne 1 -or
        $runnerSummary.selectedCaseCount -ne 1 -or
        $runnerSummary.reportCount -ne 1 -or
        $runnerSummary.missingReportCount -ne 0 -or
        $runnerSummary.seekPacketCacheEnabled -ne $false -or
        $summary.coreExecution.seekPacketCacheEnabled -ne $false) {
        throw 'Expected baseline to contain one strict-valid native manifest-runner report.'
    }

    if ($materializedReport.report.expected.codec -ne 'hevc' -or
        $materializedReport.report.expected.width -ne 320 -or
        $materializedReport.report.expected.height -ne 180 -or
        $materializedReport.report.expected.frameRate -ne 30) {
        throw 'Final baseline report must bind manifest expected values through native report materialization.'
    }

    if (-not ($summary.warnings -contains 'native-headless local generated samples were skipped by -SkipNativeHeadless')) {
        throw 'Expected summary to record only the skipped local generated native-headless samples.'
    }

    $env:NOIRAPLAYER_BASELINE_TEST_MISMATCH_RUN_ID = '1'
    $invalidOutputRoot = Join-Path $tempRoot 'invalid-report-set-output'
    powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
        -PublicManifestPath $manifestPath `
        -NoPrivateManifest `
        -SkipNativeHeadless `
        -NativeHelperExe $fakeHelper `
        -ManifestRunnerHarnessScriptPath $fakeHarness `
        -DurationSeconds 1 `
        -AttemptTimeoutSeconds 5 `
        -OutputRoot $invalidOutputRoot 2>$null
    if ($LASTEXITCODE -eq 0) {
        throw 'Baseline must return non-zero when strict report-set validation fails.'
    }
    foreach ($path in @(
        (Join-Path $invalidOutputRoot 'baseline-summary.local.json'),
        (Join-Path $invalidOutputRoot 'summaries\report-set-validation.local.json'),
        (Join-Path $invalidOutputRoot 'summaries\report-analysis-summary.local.json'),
        (Join-Path $invalidOutputRoot 'summaries\run-plan.local.json'))) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw ('Invalid baseline must still preserve model-facing diagnostics: ' + $path)
        }
    }
    $invalidSummary = Get-Content -Raw -LiteralPath (
        Join-Path $invalidOutputRoot 'baseline-summary.local.json') | ConvertFrom-Json
    if ($invalidSummary.validation.isValid -ne $false -or
        $invalidSummary.analysis.totalReportCount -ne 1) {
        throw 'Invalid baseline summary must preserve strict validation and report analysis results.'
    }
    Remove-Item Env:NOIRAPLAYER_BASELINE_TEST_MISMATCH_RUN_ID -ErrorAction SilentlyContinue

    $env:NOIRAPLAYER_BASELINE_TEST_OMIT_REPORT = '1'
    $missingOutputRoot = Join-Path $tempRoot 'missing-report-output'
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
        -PublicManifestPath $manifestPath `
        -NoPrivateManifest `
        -SkipNativeHeadless `
        -NativeHelperExe $fakeHelper `
        -ManifestRunnerHarnessScriptPath $fakeHarness `
        -DurationSeconds 1 `
        -AttemptTimeoutSeconds 5 `
        -OutputRoot $missingOutputRoot 2>$null
    $missingReportExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    if ($missingReportExitCode -eq 0) {
        throw 'Baseline must fail when a selected stable/challenge case does not produce a raw report.'
    }

    Write-Output 'playback-core-tuning-baseline tests ok'
}
finally {
    Remove-Item Env:NOIRAPLAYER_BASELINE_TEST_OMIT_REPORT -ErrorAction SilentlyContinue
    Remove-Item Env:NOIRAPLAYER_BASELINE_TEST_MISMATCH_RUN_ID -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
