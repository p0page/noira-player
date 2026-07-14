$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$cliDll = Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\bin\Debug\net10.0\NoiraPlayer.PlaybackQuality.Cli.dll'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('playback-quality-cli-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Set-SmokeNativeExecutionEvidence {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Locator,
        [Parameter(Mandatory = $true)][string]$AttemptId,
        [Parameter(Mandatory = $true)][string]$Status,
        [ValidateSet('playback', 'timeline', 'audio-switch', 'subtitle-switch', 'pause-resume', 'end-of-stream')]
        [string]$Scenario = 'playback',
        [bool]$SourceOpened,
        [bool]$PlaybackSampleObserved,
        [double]$RequestedSampleDurationMs = 1.0
    )

    $report = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Locator))
        $fingerprint = 'sha256:' + ([System.BitConverter]::ToString($hashBytes).Replace('-', '').ToLowerInvariant())
        $openedHashBytes = $sha256.ComputeHash(
            [System.Text.Encoding]::UTF8.GetBytes('synthetic-observed-media-fixture:' + $Locator))
        $openedFingerprint = 'sha256:' + ([System.BitConverter]::ToString($openedHashBytes).Replace('-', '').ToLowerInvariant())
    }
    finally {
        $sha256.Dispose()
    }

    $payload = if ($null -ne $report.PSObject.Properties['report']) {
        $report.report
    } else {
        $report
    }
    if ($SourceOpened) {
        $payload.source | Add-Member -NotePropertyName videoMetadataProvider -NotePropertyValue 'native-playback' -Force
        $payload.source | Add-Member -NotePropertyName videoMetadataStatus -NotePropertyValue 'observed' -Force
    }
    $payload | Add-Member -NotePropertyName execution -NotePropertyValue ([pscustomobject]@{
        attemptId = $AttemptId
        runner = 'native-headless'
        scenario = $Scenario
        evidenceLevel = 'native-playback'
        status = $Status
        sourceLocatorHash = $fingerprint
        openedSourceHash = $(if ($SourceOpened) { $openedFingerprint } else { '' })
        openedSourceHashKind = $(if ($SourceOpened) { 'observed-media-signature-v2' } else { '' })
        startedAtUtc = '2026-07-08T00:00:00.0000000+00:00'
        durationMs = 1000.0
        requestedSampleDurationMs = $RequestedSampleDurationMs
        observedSampleWallClockDurationMs = $RequestedSampleDurationMs
        sourceOpenAttempted = $true
        sourceOpened = $SourceOpened
        nativeGraphOpened = $SourceOpened
        demuxStarted = $SourceOpened
        decoderOpened = $PlaybackSampleObserved
        playbackSampleObserved = $PlaybackSampleObserved
    }) -Force

    if ($PlaybackSampleObserved -and $null -ne $payload.timing -and
        $null -eq $payload.timing.PSObject.Properties['decodedVideoFrames']) {
        $decodedFrames = [Math]::Max(1, [int64]$payload.timing.renderedVideoFrames)
        $payload.timing | Add-Member -NotePropertyName decodedVideoFrames -NotePropertyValue $decodedFrames
    }

    if ($PlaybackSampleObserved) {
        $transportComponents = @(
            'ffmpeg.open-input',
            'ffmpeg.find-stream-info',
            'native.startup-seek',
            'native.first-frame.demux-read'
        ) | ForEach-Object {
            [pscustomobject]@{
                name = $_
                durationMs = 0.0
                status = 'measured'
                packetCount = [uint64]0
                transportBytes = [uint64]0
                packetPayloadBytes = [uint64]0
                transportProvider = 'ffmpeg-builtin'
                transportCallEvidenceStatus = 'unavailable'
                transportReadCalls = $null
                transportSeekCalls = $null
                transportReadWaitMs = $null
                transportSeekWaitMs = $null
                transportSeekDistanceBytes = $null
            }
        }
        $payload | Add-Member -NotePropertyName startup -NotePropertyValue ([pscustomobject]@{
            commandReceivedAt = '2026-07-08T00:00:00.0000000+00:00'
            playbackStartedAt = '2026-07-08T00:00:01.0000000+00:00'
            startupDurationMs = 1000.0
            stages = @(
                [pscustomobject]@{
                    name = 'native.open'
                    startedAt = '2026-07-08T00:00:00.0000000+00:00'
                    completedAt = '2026-07-08T00:00:01.0000000+00:00'
                    durationMs = 1000.0
                    components = $transportComponents
                }
            )
        }) -Force
    }

    if ($PlaybackSampleObserved -and
        $null -ne $payload.position -and
        $null -ne $payload.position.seekTargetPositionTicks) {
        $seekTarget = [int64]$payload.position.seekTargetPositionTicks
        $actualPosition = if ($null -ne $payload.position.actualPositionTicks) {
            [int64]$payload.position.actualPositionTicks
        } else {
            $seekTarget
        }
        $payload.source | Add-Member -NotePropertyName durationTicks -NotePropertyValue ([int64]7200000000) -Force
        $payload.source | Add-Member -NotePropertyName containerStartTimeTicks -NotePropertyValue ([int64]0) -Force
        $payload.source | Add-Member -NotePropertyName videoStreamStartTimeTicks -NotePropertyValue ([int64]0) -Force
        $payload.position | Add-Member -NotePropertyName seekDemuxTargetTicks -NotePropertyValue $seekTarget -Force
        $payload.position | Add-Member -NotePropertyName firstPresentedPositionTicks -NotePropertyValue $actualPosition -Force
        $payload.position | Add-Member -NotePropertyName postSeekPositionTicks -NotePropertyValue ($actualPosition + 10000000) -Force
        $payload.position | Add-Member -NotePropertyName postSeekAdvanced -NotePropertyValue $true -Force
        $payload.position | Add-Member -NotePropertyName seekOperationDurationMs -NotePropertyValue 120.0 -Force
        $payload.position | Add-Member -NotePropertyName seekRecoveryDurationMs -NotePropertyValue 150.0 -Force
        $payload.position | Add-Member -NotePropertyName seekPacketCacheEnabled -NotePropertyValue $false -Force
        $payload.position | Add-Member -NotePropertyName seekPacketCacheHit -NotePropertyValue $false -Force
        $payload.position | Add-Member -NotePropertyName seekPacketCachePacketCount -NotePropertyValue ([uint64]0) -Force
        $payload.position | Add-Member -NotePropertyName seekPacketCacheBytes -NotePropertyValue ([uint64]0) -Force
        $payload.position | Add-Member -NotePropertyName seekPacketCacheWindowDurationTicks -NotePropertyValue ([int64]0) -Force
        $payload.position | Add-Member -NotePropertyName seekFallbackReason -NotePropertyValue 'disabled' -Force
    }

    $report | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
    if ($SourceOpened) {
        $signaturePath = $Path + '.opened-source-signature.json'
        try {
            dotnet $cliDll compute-opened-source-signature --report $Path --output $signaturePath
            if ($LASTEXITCODE -ne 0) {
                throw 'compute-opened-source-signature returned a non-zero exit code.'
            }

            $signature = Get-Content -Raw -LiteralPath $signaturePath | ConvertFrom-Json
            $report = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
            $payload = if ($null -ne $report.PSObject.Properties['report']) {
                $report.report
            } else {
                $report
            }
            $payload.execution.openedSourceHashKind = $signature.kind
            $payload.execution.openedSourceHash = $signature.hash
            $report | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
        }
        finally {
            if (Test-Path -LiteralPath $signaturePath) {
                Remove-Item -LiteralPath $signaturePath -Force
            }
        }
    }
}

try {
    $baselinePath = Join-Path $tempRoot 'baseline.json'
    $candidatePath = Join-Path $tempRoot 'candidate.json'
    $baselineEnvelopePath = Join-Path $tempRoot 'baseline-envelope.json'
    $candidateEnvelopePath = Join-Path $tempRoot 'candidate-envelope.json'
    $analysisPath = Join-Path $tempRoot 'analysis.json'
    $skipReportPath = Join-Path $tempRoot 'skip-report.json'
    $skipAnalysisPath = Join-Path $tempRoot 'skip-analysis.json'
    $skipAnalysisSetDir = Join-Path $tempRoot 'skip-analysis-report-set'
    $skipAnalysisSetPath = Join-Path $tempRoot 'skip-analysis-report-set.json'
    $analysisSetDir = Join-Path $tempRoot 'analysis-report-set'
    $analysisSetPath = Join-Path $tempRoot 'analysis-report-set.json'
    $analysisEnvelopeSetDir = Join-Path $tempRoot 'analysis-envelope-report-set'
    $analysisEnvelopeSetPath = Join-Path $tempRoot 'analysis-envelope-report-set.json'
    $analysisStaleEnvelopeSetDir = Join-Path $tempRoot 'analysis-stale-envelope-report-set'
    $analysisStaleEnvelopeSetPath = Join-Path $tempRoot 'analysis-stale-envelope-report-set.json'
    $outputPath = Join-Path $tempRoot 'comparison.json'
    $incompatibleCandidatePath = Join-Path $tempRoot 'candidate-incompatible-source.json'
    $incompatibleOutputPath = Join-Path $tempRoot 'comparison-incompatible-source.json'
    $missingChecksBaselinePath = Join-Path $tempRoot 'baseline-missing-checks.json'
    $missingChecksOutputPath = Join-Path $tempRoot 'comparison-missing-checks.json'
    $noMatchedCandidatePath = Join-Path $tempRoot 'candidate-no-matched-signals.json'
    $noMatchedOutputPath = Join-Path $tempRoot 'comparison-no-matched-signals.json'
    $envelopeOutputPath = Join-Path $tempRoot 'comparison-envelope.json'
    $presenceBaselinePath = Join-Path $tempRoot 'baseline-presence.json'
    $presenceCandidatePath = Join-Path $tempRoot 'candidate-presence.json'
    $presenceComparisonPath = Join-Path $tempRoot 'comparison-presence.json'
    $presenceAnalysisPath = Join-Path $tempRoot 'analysis-presence.json'
    $suitePath = Join-Path $tempRoot 'suite.json'
    $baselineDir = Join-Path $tempRoot 'baseline-suite'
    $candidateDir = Join-Path $tempRoot 'candidate-suite'
    $comparisonsDir = Join-Path $tempRoot 'suite-comparisons'
    $suiteFromReportsPath = Join-Path $tempRoot 'suite-from-reports.json'
    $runIdBaselineDir = Join-Path $tempRoot 'runid-baseline-suite'
    $runIdCandidateDir = Join-Path $tempRoot 'runid-candidate-suite'
    $runIdComparisonsDir = Join-Path $tempRoot 'runid-suite-comparisons'
    $runIdSuitePath = Join-Path $tempRoot 'runid-suite.json'
    $candidateEvaluationManifestPath = Join-Path $tempRoot 'candidate-evaluation-manifest.json'
    $candidateEvaluationNarrowManifestPath = Join-Path $tempRoot 'candidate-evaluation-narrow-manifest.json'
    $candidateEvaluationNarrowCoveragePath = Join-Path $tempRoot 'candidate-evaluation-narrow-coverage.json'
    $candidateEvaluationNarrowCoverageComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-narrow-coverage-comparisons'
    $candidateEvaluationPath = Join-Path $tempRoot 'candidate-evaluation.json'
    $candidateEvaluationComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-comparisons'
    $candidateEvaluationMissingEnvironmentBaselineDir = Join-Path $tempRoot 'candidate-evaluation-missing-environment-baseline'
    $candidateEvaluationMissingEnvironmentCandidateDir = Join-Path $tempRoot 'candidate-evaluation-missing-environment-candidate'
    $candidateEvaluationMissingEnvironmentComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-missing-environment-comparisons'
    $candidateEvaluationMissingEnvironmentPath = Join-Path $tempRoot 'candidate-evaluation-missing-environment.json'
    $candidateEvaluationPartialEnvironmentBaselineDir = Join-Path $tempRoot 'candidate-evaluation-partial-environment-baseline'
    $candidateEvaluationPartialEnvironmentCandidateDir = Join-Path $tempRoot 'candidate-evaluation-partial-environment-candidate'
    $candidateEvaluationPartialEnvironmentComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-partial-environment-comparisons'
    $candidateEvaluationPartialEnvironmentPath = Join-Path $tempRoot 'candidate-evaluation-partial-environment.json'
    $candidateEvaluationSameBuildBaselineDir = Join-Path $tempRoot 'candidate-evaluation-same-build-baseline'
    $candidateEvaluationSameBuildCandidateDir = Join-Path $tempRoot 'candidate-evaluation-same-build-candidate'
    $candidateEvaluationSameBuildComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-same-build-comparisons'
    $candidateEvaluationSameBuildPath = Join-Path $tempRoot 'candidate-evaluation-same-build.json'
    $candidateEvaluationEmptyAnalysisBaselineDir = Join-Path $tempRoot 'candidate-evaluation-empty-analysis-baseline'
    $candidateEvaluationEmptyAnalysisCandidateDir = Join-Path $tempRoot 'candidate-evaluation-empty-analysis-candidate'
    $candidateEvaluationEmptyAnalysisPath = Join-Path $tempRoot 'candidate-evaluation-empty-analysis.json'
    $candidateEvaluationEmptyAnalysisComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-empty-analysis-comparisons'
    $candidateEvaluationInvalidCandidateDir = Join-Path $tempRoot 'candidate-evaluation-invalid-candidate'
    $candidateEvaluationInvalidPath = Join-Path $tempRoot 'candidate-evaluation-invalid.json'
    $candidateEvaluationInvalidComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-invalid-comparisons'
    $candidateEvaluationBlockedAnalysisCandidateDir = Join-Path $tempRoot 'candidate-evaluation-blocked-analysis-candidate'
    $candidateEvaluationBlockedAnalysisPath = Join-Path $tempRoot 'candidate-evaluation-blocked-analysis.json'
    $candidateEvaluationBlockedAnalysisComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-blocked-analysis-comparisons'
    $baselineEvaluationBlockedAnalysisBaselineDir = Join-Path $tempRoot 'baseline-evaluation-blocked-analysis-baseline'
    $baselineEvaluationBlockedAnalysisPath = Join-Path $tempRoot 'baseline-evaluation-blocked-analysis.json'
    $baselineEvaluationBlockedAnalysisComparisonsDir = Join-Path $tempRoot 'baseline-evaluation-blocked-analysis-comparisons'
    $candidateEvaluationStallBaselineDir = Join-Path $tempRoot 'candidate-evaluation-stall-baseline'
    $candidateEvaluationStallCandidateDir = Join-Path $tempRoot 'candidate-evaluation-stall-candidate'
    $candidateEvaluationStallPreviousComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-stall-previous-comparisons'
    $candidateEvaluationStallComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-stall-comparisons'
    $candidateEvaluationStallPath = Join-Path $tempRoot 'candidate-evaluation-stall.json'
    $stallBaselineDir = Join-Path $tempRoot 'stall-baseline-suite'
    $stallCandidateDir = Join-Path $tempRoot 'stall-candidate-suite'
    $previousComparisonsDir = Join-Path $tempRoot 'previous-comparisons'
    $stallComparisonsDir = Join-Path $tempRoot 'stall-suite-comparisons'
    $stallSuitePath = Join-Path $tempRoot 'stall-suite.json'
    $manifestPath = Join-Path $tempRoot 'reference-manifest.json'
    $manifestValidationPath = Join-Path $tempRoot 'reference-manifest-validation.json'
    $runPlanPath = Join-Path $tempRoot 'reference-run-plan.json'
    $filteredRunPlanPath = Join-Path $tempRoot 'reference-run-plan-filtered.json'
    $materializedBaselineDir = Join-Path $tempRoot 'materialized-baseline-report-set'
    $materializedBaselineSummaryPath = Join-Path $tempRoot 'materialized-baseline-summary.json'
    $materializedBaselineValidationPath = Join-Path $tempRoot 'materialized-baseline-validation.json'
    $materializedBaselineAnalysisPath = Join-Path $tempRoot 'materialized-baseline-analysis.json'
    $coreProbeManifestPath = Join-Path $tempRoot 'core-probe-reference-manifest.json'
    $coreProbeDir = Join-Path $tempRoot 'core-probe-report-set'
    $coreProbeSummaryPath = Join-Path $tempRoot 'core-probe-summary.json'
    $coreProbeValidationPath = Join-Path $tempRoot 'core-probe-validation.json'
    $coreProbeAnalysisPath = Join-Path $tempRoot 'core-probe-analysis.json'
    $coreProbeCandidateEvaluationPath = Join-Path $tempRoot 'core-probe-candidate-evaluation.json'
    $coreProbeCandidateEvaluationComparisonsDir = Join-Path $tempRoot 'core-probe-candidate-evaluation-comparisons'
    $archivedCoreProbeManifestPath = Join-Path $tempRoot 'archived-core-probe-reference-manifest.json'
    $archivedCoreProbeReportsDir = Join-Path $repoRoot 'docs\qa\baselines\v0.1-core-probe\reports'
    $nativeHarnessManifestPath = Join-Path $tempRoot 'native-harness-reference-manifest.json'
    $nativeHarnessDir = Join-Path $tempRoot 'native-harness-report-set'
    $nativeHarnessSummaryPath = Join-Path $tempRoot 'native-harness-summary.json'
    $nativeHarnessValidationPath = Join-Path $tempRoot 'native-harness-validation.json'
    $nativeHarnessCapturedDir = Join-Path $tempRoot 'native-harness-captured-report-set'
    $nativeHarnessImportedDir = Join-Path $tempRoot 'native-harness-imported-report-set'
    $nativeHarnessImportedSummaryPath = Join-Path $tempRoot 'native-harness-imported-summary.json'
    $nativeHarnessImportedValidationPath = Join-Path $tempRoot 'native-harness-imported-validation.json'
    $nativeHarnessImportedAnalysisPath = Join-Path $tempRoot 'native-harness-imported-analysis.json'
    $nativeHeadlessImportedDir = Join-Path $tempRoot 'native-headless-imported-report-set'
    $nativeHeadlessAnalysisPath = Join-Path $tempRoot 'native-headless-analysis.json'
    $nativeHarnessMissingCapturedDir = Join-Path $tempRoot 'native-harness-missing-captured-report-set'
    $nativeHarnessMissingImportDir = Join-Path $tempRoot 'native-harness-missing-import-report-set'
    $nativeHarnessMissingImportSummaryPath = Join-Path $tempRoot 'native-harness-missing-import-summary.json'
    $exampleManifestPath = Join-Path $repoRoot 'docs\qa\playback-quality-reference-manifest.example.json'
    $exampleManifestValidationPath = Join-Path $tempRoot 'example-reference-manifest-validation.json'
    $exampleRunPlanPath = Join-Path $tempRoot 'example-reference-run-plan.json'
    $embyRunPlanManifestPath = Join-Path $tempRoot 'emby-run-plan-manifest.json'
    $embyRunPlanPath = Join-Path $tempRoot 'emby-run-plan.json'
    $reportSetDir = Join-Path $tempRoot 'reference-report-set'
    $reportSetValidationPath = Join-Path $tempRoot 'reference-report-set-validation.json'
    $missingSignalReportSetDir = Join-Path $tempRoot 'reference-report-set-missing-signals'
    $missingSignalReportSetValidationPath = Join-Path $tempRoot 'reference-report-set-missing-signals-validation.json'
    $missingStartupTransportReportSetDir = Join-Path $tempRoot 'reference-report-set-missing-startup-transport'
    $missingStartupTransportValidationPath = Join-Path $tempRoot 'reference-report-set-missing-startup-transport-validation.json'
    $zeroCounterManifestPath = Join-Path $tempRoot 'zero-counter-reference-manifest.json'
    $zeroCounterReportSetDir = Join-Path $tempRoot 'zero-counter-reference-report-set'
    $zeroCounterReportSetValidationPath = Join-Path $tempRoot 'zero-counter-reference-report-set-validation.json'
    $zeroCounterAnalysisPath = Join-Path $tempRoot 'zero-counter-analysis.json'

    @'
{
  "runId": "baseline",
  "metricVersion": "software-quality-v1",
  "result": "fail",
  "environment": {
    "playerCoreVersion": "core-baseline",
    "sourceRevision": "baseline-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "https",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Sdr",
    "hasChapterMetadata": true,
    "chapterCount": 1,
    "chapters": [
      {
        "name": "Opening",
        "startPositionTicks": 0,
        "imageTag": "chapter-0"
      }
    ]
  },
  "timing": {
    "renderedVideoFrames": 240,
    "expectedFrameDurationMs": 41.708,
    "framePacingSourceFrameRate": 23.976,
    "lateFrameDropToleranceMs": 104.271,
    "maxFrameGapMs": 180.0
  },
  "checks": [
    {
      "name": "MaxFrameGapMs",
      "signal": "timing.maxFrameGapMs",
      "status": "fail",
      "failureArea": "frame-pacing",
      "expected": "105.000",
      "actual": "180.000"
    }
  ]
}
'@ | Set-Content -LiteralPath $baselinePath -Encoding UTF8

    @'
{
  "runId": "candidate",
  "metricVersion": "software-quality-v1",
  "result": "fail",
  "environment": {
    "playerCoreVersion": "core-candidate",
    "sourceRevision": "candidate-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "https",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Sdr",
    "hasChapterMetadata": true,
    "chapterCount": 1,
    "chapters": [
      {
        "name": "Opening",
        "startPositionTicks": 0,
        "imageTag": "chapter-0"
      }
    ]
  },
  "timing": {
    "renderedVideoFrames": 240,
    "expectedFrameDurationMs": 41.708,
    "framePacingSourceFrameRate": 23.976,
    "lateFrameDropToleranceMs": 104.271,
    "maxFrameGapMs": 120.0
  },
  "checks": [
    {
      "name": "MaxFrameGapMs",
      "signal": "timing.maxFrameGapMs",
      "status": "fail",
      "failureArea": "frame-pacing",
      "expected": "105.000",
      "actual": "120.000"
    }
  ]
}
'@ | Set-Content -LiteralPath $candidatePath -Encoding UTF8

    Set-SmokeNativeExecutionEvidence `
        -Path $baselinePath `
        -Locator 'https://example.invalid/item-1/source-1.mp4' `
        -AttemptId 'direct-compare-baseline-attempt' `
        -Status 'completed' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true
    Set-SmokeNativeExecutionEvidence `
        -Path $candidatePath `
        -Locator 'https://example.invalid/item-1/source-1.mp4' `
        -AttemptId 'direct-compare-candidate-attempt' `
        -Status 'completed' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true

    $baselineReportJson = Get-Content -Raw -LiteralPath $baselinePath
    $candidateReportJson = Get-Content -Raw -LiteralPath $candidatePath

    @"
{
  "caseMetadata": {
    "caseId": "baseline",
    "category": "stable",
    "severity": "critical",
    "stability": "variable"
  },
  "report": $baselineReportJson,
  "modelAnalysis": {}
}
"@ | Set-Content -LiteralPath $baselineEnvelopePath -Encoding UTF8

    @"
{
  "report": $candidateReportJson,
  "modelAnalysis": {}
}
"@ | Set-Content -LiteralPath $candidateEnvelopePath -Encoding UTF8

    $presenceBaseline = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json
    $presenceCandidate = Get-Content -Raw -LiteralPath $candidatePath | ConvertFrom-Json
    foreach ($presenceReport in @($presenceBaseline, $presenceCandidate)) {
        $presenceReport | Add-Member -NotePropertyName sync -NotePropertyValue ([pscustomobject]@{}) -Force
        $presenceReport | Add-Member -NotePropertyName buffers -NotePropertyValue ([pscustomobject]@{}) -Force
        $presenceReport.timing | Add-Member -NotePropertyName decodedVideoFrames -NotePropertyValue 120 -Force
        $presenceReport.timing | Add-Member -NotePropertyName hardwareDecodedVideoFrames -NotePropertyValue 120 -Force
        $presenceReport.timing | Add-Member -NotePropertyName softwareDecodedVideoFrames -NotePropertyValue 0 -Force
        $presenceReport.timing | Add-Member -NotePropertyName droppedVideoFrames -NotePropertyValue 12 -Force
        $presenceReport.timing | Add-Member -NotePropertyName audioAheadWaitPassDurationMsP95 -NotePropertyValue 8.0 -Force
        $presenceReport.timing | Add-Member -NotePropertyName audioAheadWaitFinalDeltaAbsMsP95 -NotePropertyValue 12.0 -Force
        $presenceReport.sync | Add-Member -NotePropertyName audioVideoDriftMsP95 -NotePropertyValue 40.0 -Force
        $presenceReport.buffers | Add-Member -NotePropertyName queuedAudioBuffers -NotePropertyValue 2 -Force
    }
    $presenceBaseline.checks += [pscustomobject]@{
        name = 'HardwareDecodedVideoFrames'
        signal = 'timing.hardwareDecodedVideoFrames'
        status = 'fail'
        failureArea = 'frame-pacing'
        expected = '240'
        actual = '120'
    }
    $presenceCandidate.checks += [pscustomobject]@{
        name = 'HardwareDecodedVideoFrames'
        signal = 'timing.hardwareDecodedVideoFrames'
        status = 'fail'
        failureArea = 'frame-pacing'
        expected = '240'
        actual = '60'
    }
    $presenceBaseline.checks += [pscustomobject]@{
        name = 'DroppedVideoFrames'
        signal = 'timing.droppedVideoFrames'
        status = 'fail'
        failureArea = 'frame-pacing'
        expected = '4'
        actual = '12'
    }
    $presenceCandidate.checks += [pscustomobject]@{
        name = 'DroppedVideoFrames'
        signal = 'timing.droppedVideoFrames'
        status = 'pass'
        failureArea = 'frame-pacing'
        expected = '4'
        actual = '2'
    }
    $presenceBaseline.checks += [pscustomobject]@{
        name = 'AudioVideoDriftMsP95'
        signal = 'sync.audioVideoDriftMsP95'
        status = 'fail'
        failureArea = 'av-sync'
        expected = '20.0'
        actual = '40.0'
    }
    $presenceCandidate.checks += [pscustomobject]@{
        name = 'AudioVideoDriftMsP95'
        signal = 'sync.audioVideoDriftMsP95'
        status = 'fail'
        failureArea = 'av-sync'
        expected = '20.0'
        actual = '10.0'
    }
    $presenceBaseline.checks += [pscustomobject]@{
        name = 'QueuedAudioBuffers'
        signal = 'buffers.queuedAudioBuffers'
        status = 'fail'
        failureArea = 'buffering'
        expected = '2'
        actual = '2'
    }
    $presenceCandidate.checks += [pscustomobject]@{
        name = 'QueuedAudioBuffers'
        signal = 'buffers.queuedAudioBuffers'
        status = 'fail'
        failureArea = 'buffering'
        expected = '2'
        actual = '5'
    }
    $presenceCandidate.timing.PSObject.Properties.Remove('hardwareDecodedVideoFrames')
    $presenceCandidate.timing.PSObject.Properties.Remove('softwareDecodedVideoFrames')
    $presenceCandidate.timing.PSObject.Properties.Remove('droppedVideoFrames')
    $presenceCandidate.timing.PSObject.Properties.Remove('audioAheadWaitPassDurationMsP95')
    $presenceCandidate.timing.PSObject.Properties.Remove('audioAheadWaitFinalDeltaAbsMsP95')
    $presenceCandidate.sync.PSObject.Properties.Remove('audioVideoDriftMsP95')
    $presenceCandidate.buffers.PSObject.Properties.Remove('queuedAudioBuffers')
    $presenceBaseline | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $presenceBaselinePath -Encoding UTF8
    $presenceCandidate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $presenceCandidatePath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report `
            --report $candidatePath `
            --output $analysisPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $analysis = Get-Content -Raw -LiteralPath $analysisPath | ConvertFrom-Json
    if ($analysis.runId -ne 'candidate') {
        throw 'Expected analyze-report output to preserve report runId.'
    }

    if (-not ($analysis.failureAreas -contains 'frame-pacing')) {
        throw 'Expected analyze-report output to include frame-pacing failure area.'
    }

    if (-not ($analysis.evidenceSignals -contains 'timing.maxFrameGapMs')) {
        throw 'Expected analyze-report output to include max frame gap evidence signal.'
    }

    if (-not ($analysis.evidenceSignals -contains 'timing.framePacingSourceFrameRate')) {
        throw 'Expected analyze-report output to include frame pacing source frame rate evidence signal.'
    }

    if (-not ($analysis.evidenceSignals -contains 'timing.lateFrameDropToleranceMs')) {
        throw 'Expected analyze-report output to include late frame drop tolerance evidence signal.'
    }

    if ($analysis.framePacing.lateFrameDropToleranceFrameRatio -lt 2.4 -or
        $analysis.framePacing.lateFrameDropToleranceFrameRatio -gt 2.6) {
        throw 'Expected analyze-report output to normalize late frame drop tolerance to source frames.'
    }

    if (-not ($analysis.failedChecks | Where-Object { $_.signal -eq 'timing.maxFrameGapMs' -and $_.actual -eq '120.000' })) {
        throw 'Expected analyze-report output to include failed check details.'
    }

    @'
{
  "runId": "skip-subtitle-render",
  "metricVersion": "software-quality-v1",
  "result": "skip",
  "environment": {
    "playerCoreVersion": "core-candidate",
    "sourceRevision": "candidate-revision",
    "buildConfiguration": "Debug"
  },
  "skip": {
    "code": "capability.subtitle-render.not-supported",
    "reason": "Subtitle visual render verification is outside v0.1 software evidence.",
    "operation": "subtitle-render-validation",
    "failureClass": "unsupported by current MVP",
    "failureArea": "evidence-collection",
    "isExpected": true,
    "isRetriable": false
  }
}
'@ | Set-Content -LiteralPath $skipReportPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report `
            --report $skipReportPath `
            --output $skipAnalysisPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report returned a non-zero exit code for skip report.'
        }
    }
    finally {
        Pop-Location
    }

    $skipAnalysis = Get-Content -Raw -LiteralPath $skipAnalysisPath | ConvertFrom-Json
    if ($skipAnalysis.result -ne 'skip' -or
        $skipAnalysis.skip.code -ne 'capability.subtitle-render.not-supported' -or
        $skipAnalysis.primaryFailureClass -ne 'unsupported by current MVP' -or
        $skipAnalysis.primaryFailureArea -ne 'evidence-collection') {
        throw 'Expected analyze-report output to preserve first-class skip result evidence.'
    }

    if (-not ($skipAnalysis.evidenceSignals -contains 'skip.reason') -or
        ($skipAnalysis.missingEvidence -contains 'source.codec') -or
        ($skipAnalysis.missingEvidence -contains 'timing.renderedVideoFrames')) {
        throw 'Expected analyze-report skip output to use skip evidence without playback telemetry noise.'
    }

    New-Item -ItemType Directory -Path $skipAnalysisSetDir | Out-Null
    Copy-Item -LiteralPath $skipReportPath -Destination (Join-Path $skipAnalysisSetDir 'skip-report.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report-set `
            --reports-dir $skipAnalysisSetDir `
            --output $skipAnalysisSetPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report-set skip report returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $skipAnalysisSet = Get-Content -Raw -LiteralPath $skipAnalysisSetPath | ConvertFrom-Json
    if ($skipAnalysisSet.action -ne 'collect-comparable-evidence' -or
        $skipAnalysisSet.decision -ne 'collect-comparable-evidence' -or
        $skipAnalysisSet.risk -ne 'high' -or
        $skipAnalysisSet.confidence.level -ne 'weak' -or
        -not ($skipAnalysisSet.blockers -contains 'result.skip') -or
        -not ($skipAnalysisSet.targetFailureAreas -contains 'evidence-collection') -or
        -not ($skipAnalysisSet.nextActions | Where-Object {
            $_.rank -eq 1 -and
            $_.action -eq 'collect-comparable-evidence' -and
            $_.risk -eq 'high' -and
            $_.failureArea -eq 'evidence-collection'
        })) {
        throw 'Expected analyze-report-set skip-only output to require evidence collection.'
    }

    if (-not ($skipAnalysisSet.capabilityCoverage | Where-Object {
        $_.capability -eq 'runtime-metrics' -and
        $_.status -eq 'not-observed' -and
        $_.missingCaseCount -eq 0
    })) {
        throw 'Expected analyze-report-set capability coverage not to require runtime metrics for skip-only reports.'
    }

    New-Item -ItemType Directory -Path $analysisSetDir | Out-Null
    Copy-Item -LiteralPath $baselinePath -Destination (Join-Path $analysisSetDir 'baseline.json')
    Copy-Item -LiteralPath $candidatePath -Destination (Join-Path $analysisSetDir 'candidate.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report-set `
            --reports-dir $analysisSetDir `
            --output $analysisSetPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report-set returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $analysisSet = Get-Content -Raw -LiteralPath $analysisSetPath | ConvertFrom-Json
    if ($analysisSet.schemaVersion -ne 1) {
        throw 'Expected analyze-report-set output schemaVersion 1.'
    }

    if ($analysisSet.evaluationVersion -ne 'playback-quality-v0.15') {
        throw 'Expected analyze-report-set output evaluationVersion playback-quality-v0.15.'
    }

    if ($analysisSet.action -ne 'fix-report-analysis') {
        throw 'Expected analyze-report-set output to expose a direct blocked action.'
    }

    if ($analysisSet.decision -ne 'fix-report-analysis') {
        throw 'Expected analyze-report-set output to expose a direct blocked decision.'
    }

    if ($analysisSet.risk -ne 'high') {
        throw 'Expected analyze-report-set output to expose high risk for blocked analysis.'
    }

    if ($analysisSet.confidence.level -ne 'weak') {
        throw 'Expected analyze-report-set output to expose weak confidence for blocked analysis.'
    }

    if (-not ($analysisSet.nextActions | Where-Object {
        $_.rank -eq 1 -and
        $_.action -eq 'fix-report-analysis' -and
        $_.risk -eq 'high' -and
        $_.failureArea -eq 'frame-pacing' -and
        ($_.caseIds -contains 'candidate') -and
        ($_.signals -contains 'timing.maxFrameGapMs') -and
        ($_.blockers -contains 'missingEvidence') -and
        ($_.codeTargets -contains 'src/NoiraPlayer.Native/Media/FramePacing.h')
    })) {
        throw 'Expected analyze-report-set output to expose ranked next action context.'
    }

    if ($analysisSet.totalReportCount -ne 2 -or $analysisSet.analyzedReportCount -ne 2) {
        throw 'Expected analyze-report-set output to analyze both raw reports.'
    }

    if ($analysisSet.unavailableReportCount -ne 0) {
        throw 'Expected analyze-report-set output to avoid unavailable analysis for raw reports.'
    }

    if (-not ($analysisSet.blockers -contains 'missingEvidence')) {
        throw 'Expected analyze-report-set output to aggregate blockers.'
    }

    if (-not ($analysisSet.signals -contains 'timing.maxFrameGapMs')) {
        throw 'Expected analyze-report-set output to aggregate evidence signals.'
    }

    if (-not ($analysisSet.capabilityCoverage | Where-Object {
        $_.capability -eq 'metadata-duration' -and
        ($_.evidenceSignals -contains 'source.hasChapterMetadata') -and
        ($_.evidenceSignals -contains 'source.chapterCount') -and
        ($_.evidenceSignals -contains 'source.chapters.startPositionTicks') -and
        ($_.evidenceSignals -contains 'source.chapters.name')
    })) {
        throw 'Expected analyze-report-set output to expose chapter metadata evidence in metadata-duration coverage.'
    }

    if (-not ($analysisSet.capabilityCoverage | Where-Object {
        $_.capability -eq 'frame-pacing' -and
        $_.status -eq 'blocked' -and
        ($_.caseIds -contains 'candidate') -and
        ($_.evidenceSignals -contains 'timing.maxFrameGapMs') -and
        ($_.evidenceSignals -contains 'timing.framePacingSourceFrameRate') -and
        ($_.blockers -contains 'missingEvidence')
    })) {
        throw 'Expected analyze-report-set output to expose blocked frame-pacing capability coverage.'
    }

    if (-not ($analysisSet.capabilityCoverage | Where-Object {
        $_.capability -eq 'metadata-duration' -and
        $_.status -eq 'evidence-present' -and
        $_.blockedCaseCount -eq 0 -and
        ($_.evidenceSignals -contains 'source.hasChapterMetadata')
    })) {
        throw 'A frame-pacing blocker must not contaminate unrelated metadata-duration evidence.'
    }

    if (-not ($analysisSet.capabilityCoverage | Where-Object {
        $_.capability -eq 'runtime-metrics' -and
        $_.status -eq 'missing-evidence' -and
        ($_.missingSignals -contains 'runtimeMetrics.status') -and
        ($_.suggestedNextActions -contains 'Collect runtime metrics provider evidence before optimizing playback Core.')
    })) {
        throw 'Expected analyze-report-set output to expose missing runtime metrics capability coverage.'
    }

    if (-not ($analysisSet.failureAreas -contains 'frame-pacing')) {
        throw 'Expected analyze-report-set output to aggregate failure areas.'
    }

    if (-not ($analysisSet.targetFailureAreas -contains 'frame-pacing')) {
        throw 'Expected analyze-report-set output to expose target failure area.'
    }

    if (-not ($analysisSet.targetCaseIds -contains 'candidate')) {
        throw 'Expected analyze-report-set output to expose target case id.'
    }

    if (-not ($analysisSet.cases | Where-Object {
        $_.caseId -eq 'candidate' -and
        $_.hasModelAnalysis -eq $true -and
        $_.isBlocked -eq $true -and
        $_.expectedBehavior -eq 'timing.maxFrameGapMs expected 105.000.' -and
        $_.actualBehavior -eq 'timing.maxFrameGapMs actual 120.000.' -and
        $_.primaryFailureClass -eq 'player-core bug' -and
        ($_.failureAreas -contains 'frame-pacing') -and
        ($_.signals -contains 'timing.maxFrameGapMs')
    })) {
        throw 'Expected analyze-report-set output to expose analyzed candidate behavior summary, primary failure class, blockers, and signals.'
    }

    New-Item -ItemType Directory -Path $analysisEnvelopeSetDir | Out-Null
    Copy-Item -LiteralPath $baselineEnvelopePath -Destination (Join-Path $analysisEnvelopeSetDir 'baseline-envelope.json')
    Copy-Item -LiteralPath $candidateEnvelopePath -Destination (Join-Path $analysisEnvelopeSetDir 'candidate-envelope.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report-set `
            --reports-dir $analysisEnvelopeSetDir `
            --output $analysisEnvelopeSetPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report-set envelope refresh returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $analysisEnvelopeSet = Get-Content -Raw -LiteralPath $analysisEnvelopeSetPath | ConvertFrom-Json
    if ($analysisEnvelopeSet.schemaVersion -ne 1) {
        throw 'Expected analyze-report-set envelope output schemaVersion 1.'
    }

    if (-not ($analysisEnvelopeSet.cases | Where-Object {
        $_.caseId -eq 'candidate' -and
        $_.hasModelAnalysis -eq $true -and
        ($_.failureAreas -contains 'frame-pacing') -and
        ($_.signals -contains 'timing.maxFrameGapMs')
    })) {
        throw 'Expected analyze-report-set output to refresh empty envelope modelAnalysis.'
    }

    New-Item -ItemType Directory -Path $analysisStaleEnvelopeSetDir | Out-Null
    @"
{
  "report": $candidateReportJson,
  "modelAnalysis": {
    "runId": "candidate",
    "result": "pass",
    "evidenceSignals": [],
    "optimizationGate": {
      "status": "ready",
      "canOptimizePlaybackCore": true,
      "targetFailureAreas": []
    }
  }
}
"@ | Set-Content -LiteralPath (Join-Path $analysisStaleEnvelopeSetDir 'candidate-stale-envelope.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report-set `
            --reports-dir $analysisStaleEnvelopeSetDir `
            --output $analysisStaleEnvelopeSetPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report-set stale envelope refresh returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $analysisStaleEnvelopeSet = Get-Content -Raw -LiteralPath $analysisStaleEnvelopeSetPath | ConvertFrom-Json
    if (-not ($analysisStaleEnvelopeSet.cases | Where-Object {
        $_.caseId -eq 'candidate' -and
        $_.hasModelAnalysis -eq $true -and
        ($_.failureAreas -contains 'frame-pacing') -and
        ($_.signals -contains 'timing.maxFrameGapMs')
    })) {
        throw 'Expected analyze-report-set output to refresh stale envelope modelAnalysis.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "netflix/chimera-4k-2398-hdr-pq",
      "severity": "high",
      "stability": "variable",
      "uri": "https://example.invalid/netflix/chimera-4k-2398-hdr-pq.mp4",
      "tier": 2,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [
        "hdr-output",
        "cadence-23.976"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Hdr10"
      }
    },
    {
      "caseId": "jellyfin/dv-profile5-hevc-4k",
      "severity": "medium",
      "stability": "stable",
      "uri": "https://example.invalid/jellyfin/dv-profile5-hevc-4k.mp4",
      "tier": 3,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [
        "dv-reject"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "DolbyVisionUnsupported"
      }
    },
    {
      "caseId": "local/missing-file-error-handling",
      "category": "stable",
      "severity": "medium",
      "stability": "stable",
      "uri": "emby://quality-cases/missing-file-error-handling",
      "itemId": "quality-case-missing-file-error-handling",
      "mediaSourceId": "quality-source-missing-file-error-handling",
      "tier": 1,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [
        "error-handling"
      ],
      "expected": {
        "codec": "hevc",
        "width": 1920,
        "height": 1080,
        "frameRate": 60.0,
        "hdrKind": "Sdr"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-manifest `
            --manifest $manifestPath `
            --output $manifestValidationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI validate-manifest returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $manifestValidation = Get-Content -Raw -LiteralPath $manifestValidationPath | ConvertFrom-Json
    if ($manifestValidation.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI validate-manifest output schemaVersion 1.'
    }

    if ($manifestValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI validate-manifest output to be valid.'
    }

    if ($manifestValidation.caseCount -ne 3) {
        throw 'Expected playback quality CLI validate-manifest output to include three cases.'
    }

    if (-not ($manifestValidation.purposes | Where-Object { $_ -eq 'hdr-output' })) {
        throw 'Expected playback quality CLI validate-manifest output to include hdr-output purpose.'
    }

    if (-not ($manifestValidation.cases | Where-Object {
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.tier -eq 2 -and
        $_.severity -eq 'high' -and
        $_.stability -eq 'variable' -and
        $_.expected.codec -eq 'hevc' -and
        $_.expected.hdrKind -eq 'Hdr10'
    })) {
        throw 'Expected playback quality CLI validate-manifest output to include schedulable case summary.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            plan-runs `
            --manifest $manifestPath `
            --reports-dir captured-baseline `
            --duration 60 `
            --output $runPlanPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI plan-runs returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $runPlan = Get-Content -Raw -LiteralPath $runPlanPath | ConvertFrom-Json
    if ($runPlan.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI plan-runs output schemaVersion 1.'
    }

    if ($runPlan.evaluationVersion -ne 'playback-quality-v0.15') {
        throw 'Expected playback quality CLI plan-runs output evaluationVersion playback-quality-v0.15.'
    }

    if ($runPlan.caseCount -ne 3) {
        throw 'Expected playback quality CLI plan-runs output to include three cases.'
    }

    if ($runPlan.durationSeconds -ne 60) {
        throw 'Expected playback quality CLI plan-runs output to keep requested duration.'
    }

    if (-not ($runPlan.evidenceRequirements -contains 'capture environment.playerCoreVersion and environment.sourceRevision for every report')) {
        throw 'Expected playback quality CLI plan-runs output to require player identity evidence.'
    }

    if (-not ($runPlan.cases | Where-Object {
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.runId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.sourceUri -eq 'https://example.invalid/netflix/chimera-4k-2398-hdr-pq.mp4' -and
        $_.severity -eq 'high' -and
        $_.stability -eq 'variable' -and
        $_.reportRelativePath -eq 'netflix/chimera-4k-2398-hdr-pq.json' -and
        $_.durationSeconds -eq 60 -and
        $_.expected.hdrKind -eq 'Hdr10' -and
        ($_.requiredSignals -contains 'source.codec') -and
        ($_.requiredSignals -contains 'source.hdrKind')
    })) {
        throw 'Expected playback quality CLI plan-runs output to include a runnable HDR case.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            materialize-baseline-report-set `
            --manifest $manifestPath `
            --reports-dir $materializedBaselineDir `
            --source-revision smoke-baseline-revision `
            --player-core-version smoke-core `
            --build-configuration Debug `
            --output $materializedBaselineSummaryPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI materialize-baseline-report-set returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $materializedBaselineSummary = Get-Content -Raw -LiteralPath $materializedBaselineSummaryPath | ConvertFrom-Json
    if ($materializedBaselineSummary.schemaVersion -ne 1 -or
        $materializedBaselineSummary.evaluationVersion -ne 'playback-quality-v0.15' -or
        $materializedBaselineSummary.caseCount -ne 3 -or
        $materializedBaselineSummary.reportsDirectory -ne $materializedBaselineDir) {
        throw 'Expected materialize-baseline-report-set summary to describe generated reports.'
    }

    if (-not ($materializedBaselineSummary.limitations -contains 'source-only: playback execution was not run by this command')) {
        throw 'Expected materialize-baseline-report-set summary to expose source-only limitation.'
    }

    if (-not ($materializedBaselineSummary.cases | Where-Object {
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.severity -eq 'high' -and
        $_.stability -eq 'variable'
    })) {
        throw 'Expected materialize-baseline-report-set summary to preserve case severity and stability.'
    }

    $materializedBaselineReportPath = Join-Path $materializedBaselineDir 'netflix\chimera-4k-2398-hdr-pq.json'
    if (-not (Test-Path -LiteralPath $materializedBaselineReportPath)) {
        throw 'Expected materialize-baseline-report-set to write run-id based report path.'
    }

    $materializedBaselineReport = Get-Content -Raw -LiteralPath $materializedBaselineReportPath | ConvertFrom-Json
    if ($materializedBaselineReport.schemaVersion -ne 1 -or
        $materializedBaselineReport.caseMetadata.caseId -ne 'netflix/chimera-4k-2398-hdr-pq' -or
        $materializedBaselineReport.caseMetadata.category -ne 'stable' -or
        $materializedBaselineReport.caseMetadata.severity -ne 'high' -or
        $materializedBaselineReport.caseMetadata.stability -ne 'variable' -or
        $materializedBaselineReport.report.runId -ne 'netflix/chimera-4k-2398-hdr-pq' -or
        $materializedBaselineReport.report.environment.sourceRevision -ne 'smoke-baseline-revision' -or
        $materializedBaselineReport.report.source.codec -ne 'hevc' -or
        $materializedBaselineReport.report.runtimeMetrics.status -ne 'unavailable' -or
        $materializedBaselineReport.report.runtimeMetrics.providerStatus -ne 'source-only' -or
        $materializedBaselineReport.modelAnalysis.runtimeMetrics.status -ne 'unavailable' -or
        $materializedBaselineReport.modelAnalysis.runtimeMetrics.providerStatus -ne 'source-only' -or
        -not ($materializedBaselineReport.modelAnalysis.evidenceSignals -contains 'runtimeMetrics.status') -or
        $materializedBaselineReport.modelAnalysis.runId -ne 'netflix/chimera-4k-2398-hdr-pq') {
        throw 'Expected materialize-baseline-report-set to write PlaybackQualityRunResult envelope with case metadata, source, environment, and source-only runtime metrics evidence.'
    }

    if (-not ($materializedBaselineReport.report.limitations -contains 'source-only: playback execution was not run by this command')) {
        throw 'Expected materialized baseline report to carry source-only limitation.'
    }

    $materializedBaselineErrorReportPath = Join-Path $materializedBaselineDir 'local\missing-file-error-handling.json'
    if (-not (Test-Path -LiteralPath $materializedBaselineErrorReportPath)) {
        throw 'Expected materialize-baseline-report-set to write an error-handling report.'
    }

    $materializedBaselineErrorReport = Get-Content -Raw -LiteralPath $materializedBaselineErrorReportPath | ConvertFrom-Json
    if ($materializedBaselineErrorReport.report.result -ne 'error' -or
        $materializedBaselineErrorReport.report.error.code -ne 'source-only.error-case' -or
        $materializedBaselineErrorReport.report.error.failureArea -ne 'error-handling' -or
        $materializedBaselineErrorReport.modelAnalysis.result -ne 'error') {
        throw 'Expected source-only materializer to emit a first-class error envelope for error-handling cases.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-report-set `
            --manifest $manifestPath `
            --reports-dir $materializedBaselineDir `
            --output $materializedBaselineValidationPath
        if ($LASTEXITCODE -ne 2) {
            throw 'Expected materialized source-only baseline to fail report-set validation for missing telemetry.'
        }
    }
    finally {
        Pop-Location
    }

    $materializedBaselineValidation = Get-Content -Raw -LiteralPath $materializedBaselineValidationPath | ConvertFrom-Json
    if ($materializedBaselineValidation.expectedCaseCount -ne 3 -or
        $materializedBaselineValidation.reportCount -ne 3) {
        throw 'Expected materialized source-only baseline validation to cover every manifest case.'
    }

    if ($materializedBaselineValidation.errors | Where-Object { $_.code -eq 'report.missing' }) {
        throw 'Expected materialized source-only baseline to avoid missing report errors.'
    }

    if (-not ($materializedBaselineValidation.errors | Where-Object {
        $_.code -eq 'report.requiredSignal.missing' -and
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.failureClass -eq 'insufficient instrumentation'
    })) {
        throw 'Expected materialized source-only baseline to expose missing telemetry as insufficient instrumentation.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report-set `
            --reports-dir $materializedBaselineDir `
            --output $materializedBaselineAnalysisPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report-set source-only baseline returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $materializedBaselineAnalysis = Get-Content -Raw -LiteralPath $materializedBaselineAnalysisPath | ConvertFrom-Json
    if (-not ($materializedBaselineAnalysis.evidenceSources -contains 'source-only')) {
        throw 'Expected source-only analyze-report-set summary to expose source-only evidence source.'
    }

    if ($materializedBaselineAnalysis.evidenceSources -contains 'unknown') {
        throw 'Expected source-only analyze-report-set summary not to aggregate unknown evidence source.'
    }

    if (-not ($materializedBaselineAnalysis.limitations -contains 'source-only: playback execution was not run by this command')) {
        throw 'Expected source-only analyze-report-set summary to expose source-only limitation.'
    }

    if ($materializedBaselineAnalysis.playbackEvidence.scope -ne 'source-only' -or
        $materializedBaselineAnalysis.playbackEvidence.status -ne 'missing' -or
        $materializedBaselineAnalysis.playbackEvidence.canEvaluateNativePlayback -ne $false -or
        $materializedBaselineAnalysis.playbackEvidence.canEvaluateOrchestration -ne $false) {
        throw 'Expected source-only analyze-report-set summary to mark playback evidence as missing source-only evidence.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "local/core-probe-sdr-timeline-tracks",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "emby://quality-cases/core-probe-sdr-timeline-tracks",
      "itemId": "quality-case-core-probe",
      "mediaSourceId": "quality-source-core-probe",
      "startPositionTicks": 600000000,
      "tier": 1,
      "executionRequirement": {
        "minimumEvidenceLevel": "native-playback",
        "scenario": "timeline"
      },
      "purpose": [
        "sdr-smoke",
        "timeline",
        "tracks",
        "subtitles",
        "frame-pacing",
        "av-sync",
        "buffering"
      ],
      "expected": {
        "codec": "hevc",
        "width": 1920,
        "height": 1080,
        "frameRate": 60.0,
        "hdrKind": "Sdr",
        "isHdr": false,
        "isDirectPlayable": true,
        "hdrOutput": "Sdr",
        "dxgiInput": "YCBCR_STUDIO_G22_LEFT_P709",
        "dxgiOutput": "RGB_FULL_G22_NONE_P709",
        "maxStartupDurationMs": 5000.0,
        "minRenderedVideoFrames": 120,
        "maxDroppedFrames": 0,
        "maxFrameGapMs": 40.0,
        "maxRenderIntervalMsP95": 20.0,
        "maxRenderIntervalMsP99": 25.0,
        "maxAudioVideoDriftMsP95": 80.0,
        "maxSeekPositionErrorMs": 500.0,
        "maxVideoStarvedPasses": 0,
        "maxAudioStarvedPasses": 0,
        "requireValidatedConversion": true
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $coreProbeManifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            materialize-evaluator-self-test-report-set `
            --manifest $coreProbeManifestPath `
            --reports-dir $coreProbeDir `
            --source-revision smoke-core-probe-revision `
            --player-core-version smoke-core `
            --build-configuration Debug `
            --output $coreProbeSummaryPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI materialize-evaluator-self-test-report-set returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $coreProbeSummary = Get-Content -Raw -LiteralPath $coreProbeSummaryPath | ConvertFrom-Json
    if ($coreProbeSummary.schemaVersion -ne 1 -or
        $coreProbeSummary.caseCount -ne 1 -or
        $coreProbeSummary.reportsDirectory -ne $coreProbeDir) {
        throw 'Expected evaluator self-test report-set summary to describe generated reports.'
    }

    if (-not ($coreProbeSummary.limitations -contains 'core-probe: native playback graph, decoder, renderer, network I/O, and HDMI output were not opened')) {
        throw 'Expected evaluator self-test report-set summary to expose core-probe limitation.'
    }

    $coreProbeReportPath = Join-Path $coreProbeDir 'local\core-probe-sdr-timeline-tracks.json'
    if (-not (Test-Path -LiteralPath $coreProbeReportPath)) {
        throw 'Expected evaluator self-test report-set to write run-id based report path.'
    }

    $coreProbeReport = Get-Content -Raw -LiteralPath $coreProbeReportPath | ConvertFrom-Json
    if ($coreProbeReport.schemaVersion -ne 1 -or
        $coreProbeReport.caseMetadata.caseId -ne 'local/core-probe-sdr-timeline-tracks' -or
        $coreProbeReport.report.result -ne 'pass' -or
        $coreProbeReport.report.position.requestedStartPositionTicks -ne 600000000 -or
        $coreProbeReport.report.position.seekTargetPositionTicks -ne 900000000 -or
        $coreProbeReport.report.position.actualPositionTicks -ne 900000000 -or
        $coreProbeReport.report.tracks.audioTrackCount -ne 2 -or
        $coreProbeReport.report.tracks.subtitleTrackCount -ne 1 -or
        $null -ne $coreProbeReport.report.tracks.selectedAudioStreamIndex -or
        $null -ne $coreProbeReport.report.tracks.selectedSubtitleStreamIndex -or
        $coreProbeReport.report.environment.sourceRevision -ne 'smoke-core-probe-revision') {
        throw ('Expected evaluator self-test report-set to write a model-consumable core probe envelope: ' +
            ([ordered]@{
                schemaVersion = $coreProbeReport.schemaVersion
                caseId = $coreProbeReport.caseMetadata.caseId
                result = $coreProbeReport.report.result
                scenario = $coreProbeReport.report.execution.scenario
                requestedStart = $coreProbeReport.report.position.requestedStartPositionTicks
                seekTarget = $coreProbeReport.report.position.seekTargetPositionTicks
                actualPosition = $coreProbeReport.report.position.actualPositionTicks
                selectedAudio = $coreProbeReport.report.tracks.selectedAudioStreamIndex
                selectedSubtitle = $coreProbeReport.report.tracks.selectedSubtitleStreamIndex
                sourceRevision = $coreProbeReport.report.environment.sourceRevision
            } | ConvertTo-Json -Compress))
    }

    if (-not ($coreProbeReport.report.limitations -contains 'core-probe: native playback graph, decoder, renderer, network I/O, and HDMI output were not opened')) {
        throw 'Expected core probe report to carry native graph limitation.'
    }

    $coreProbeLifecycleOperations = @(
        $coreProbeReport.report.lifecycle.events |
            Where-Object { $_.status -eq 'observed' } |
            ForEach-Object { $_.operation }
    )
    foreach ($operation in @('load', 'play', 'pause', 'resume', 'seek', 'stop')) {
        if (-not ($coreProbeLifecycleOperations -contains $operation)) {
            throw "Expected core probe report to expose observed lifecycle operation '$operation'."
        }
    }

    if (-not ($coreProbeReport.modelAnalysis.evidenceSignals -contains 'position.seekPositionErrorMs') -or
        -not ($coreProbeReport.modelAnalysis.evidenceSignals -contains 'tracks.audioTrackCount') -or
        -not ($coreProbeReport.modelAnalysis.evidenceSignals -contains 'tracks.subtitleTrackCount') -or
        -not ($coreProbeReport.modelAnalysis.evidenceSignals -contains 'lifecycle.load') -or
        -not ($coreProbeReport.modelAnalysis.evidenceSignals -contains 'lifecycle.play') -or
        -not ($coreProbeReport.modelAnalysis.evidenceSignals -contains 'lifecycle.pause') -or
        -not ($coreProbeReport.modelAnalysis.evidenceSignals -contains 'lifecycle.resume') -or
        -not ($coreProbeReport.modelAnalysis.evidenceSignals -contains 'lifecycle.seek') -or
        -not ($coreProbeReport.modelAnalysis.evidenceSignals -contains 'lifecycle.stop')) {
        throw 'Expected core probe model analysis to expose lifecycle, timeline, and track evidence signals.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-report-set `
            --manifest $coreProbeManifestPath `
            --reports-dir $coreProbeDir `
            --output $coreProbeValidationPath
        if ($LASTEXITCODE -ne 2) {
            throw 'Expected strict report-set validation to reject orchestration-only core-probe evidence.'
        }
    }
    finally {
        Pop-Location
    }

    $coreProbeValidation = Get-Content -Raw -LiteralPath $coreProbeValidationPath | ConvertFrom-Json
    if ($coreProbeValidation.isValid -ne $false -or
        $coreProbeValidation.structureValid -ne $false -or
        $coreProbeValidation.executionValid -ne $false -or
        $coreProbeValidation.matchedCaseCount -ne 0 -or
        $coreProbeValidation.cases[0].status -ne 'mismatch' -or
        -not ($coreProbeValidation.errors | Where-Object {
            $_.code -eq 'report.execution.evidence-level.insufficient' -and
            $_.signal -eq 'execution.evidenceLevel' -and
            $_.expected -eq 'native-playback' -and
            $_.actual -eq 'orchestration'
        }) -or
        -not ($coreProbeValidation.errors | Where-Object {
            $_.code -eq 'report.requiredSignal.missing' -and
            $_.signal -eq 'position.seekOperationDurationMs'
        }) -or
        -not ($coreProbeValidation.errors | Where-Object {
            $_.code -eq 'report.requiredSignal.missing' -and
            $_.signal -eq 'position.seekRecoveryDurationMs'
        })) {
        throw 'Expected evaluator self-test probe to fail both native execution and real seek-latency evidence gates.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report-set `
            --reports-dir $coreProbeDir `
            --output $coreProbeAnalysisPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report-set core-probe returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $coreProbeAnalysis = Get-Content -Raw -LiteralPath $coreProbeAnalysisPath | ConvertFrom-Json
    if (-not ($coreProbeAnalysis.evidenceSources -contains 'core-probe:returned-snapshot')) {
        throw 'Expected core-probe analyze-report-set summary to expose core-probe evidence source.'
    }

    if ($coreProbeAnalysis.evidenceSources -contains 'unknown') {
        throw 'Expected core-probe analyze-report-set summary not to aggregate unknown evidence source.'
    }

    if (-not ($coreProbeAnalysis.limitations -contains 'core-probe: native playback graph, decoder, renderer, network I/O, and HDMI output were not opened')) {
        throw 'Expected core-probe analyze-report-set summary to expose core-probe limitation.'
    }

    if ($coreProbeAnalysis.playbackEvidence.scope -ne 'orchestration-only' -or
        $coreProbeAnalysis.playbackEvidence.status -ne 'limited' -or
        $coreProbeAnalysis.playbackEvidence.canEvaluateNativePlayback -ne $false -or
        $coreProbeAnalysis.playbackEvidence.canEvaluateOrchestration -ne $true) {
        throw 'Expected core-probe analyze-report-set summary to mark playback evidence as orchestration-only.'
    }

    $archivedCoreProbeManifest = Get-Content -Raw -LiteralPath $exampleManifestPath | ConvertFrom-Json
    $archivedCoreProbeManifest.cases = @($archivedCoreProbeManifest.cases | Where-Object {
        $_.caseId -ne 'w3c/ui-freeze-regression-sintel'
    })
    $archivedCoreProbeManifest.cases[0].purpose = @(
        @($archivedCoreProbeManifest.cases[0].purpose) +
        @('sdr-smoke', 'cadence-23.976', 'frame-pacing', 'av-sync', 'buffering', 'tracks', 'subtitles', 'end-of-stream') |
            Select-Object -Unique
    )
    $archivedCoreProbeManifest.cases[0].executionRequirement = [pscustomobject]@{
        minimumEvidenceLevel = 'native-playback'
        scenario = 'end-of-stream'
    }
    $archivedTimelineCase = @($archivedCoreProbeManifest.cases | Where-Object {
        @($_.purpose) -contains 'timeline'
    })[0]
    $archivedTimelineCase.executionRequirement = [pscustomobject]@{
        minimumEvidenceLevel = 'native-playback'
        scenario = 'timeline'
    }
    $archivedTimelineCase.category = 'challenge'
    $archivedErrorCase = @($archivedCoreProbeManifest.cases | Where-Object {
        @($_.purpose) -contains 'error-handling'
    })[0]
    $archivedErrorCase.category = 'challenge'
    $archivedCoreProbeManifest | ConvertTo-Json -Depth 100 |
        Set-Content -LiteralPath $archivedCoreProbeManifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $archivedCoreProbeManifestPath `
            --baseline-dir $archivedCoreProbeReportsDir `
            --candidate-dir $archivedCoreProbeReportsDir `
            --match-by run-id `
            --comparisons-dir $coreProbeCandidateEvaluationComparisonsDir `
            --output $coreProbeCandidateEvaluationPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected core-probe candidate evaluation to reject non-native playback evidence.'
        }
    }
    finally {
        Pop-Location
    }

    $coreProbeCandidateEvaluation = Get-Content -Raw -LiteralPath $coreProbeCandidateEvaluationPath | ConvertFrom-Json
    if ($coreProbeCandidateEvaluation.activeGate.name -ne 'baseline-report-set' -or
        $coreProbeCandidateEvaluation.activeGate.status -ne 'blocked' -or
        -not ($coreProbeCandidateEvaluation.activeGate.blockers -contains 'baseline-report-set.invalid') -or
        -not ($coreProbeCandidateEvaluation.activeGate.signals -contains 'execution.evidenceLevel')) {
        throw ('Expected core-probe candidate evaluation to block at baseline report-set execution evidence gate. Actual: ' +
            ($coreProbeCandidateEvaluation.activeGate | ConvertTo-Json -Depth 8 -Compress))
    }

    if (Test-Path -LiteralPath $coreProbeCandidateEvaluationComparisonsDir) {
        throw 'Expected core-probe playback-evidence gate to skip comparison output.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "local/native-harness-sdr-smoke",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "file:///quality-cases/native-harness-sdr-smoke.mp4",
      "tier": 1,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [
        "sdr-smoke",
        "frame-pacing",
        "av-sync",
        "buffering"
      ],
      "expected": {
        "codec": "hevc",
        "width": 1920,
        "height": 1080,
        "frameRate": 60.0,
        "hdrKind": "Sdr",
        "isHdr": false,
        "isDirectPlayable": true,
        "hdrOutput": "Sdr",
        "maxStartupDurationMs": 5000.0,
        "minRenderedVideoFrames": 120,
        "maxDroppedFrames": 0,
        "maxFrameGapMs": 40.0,
        "maxAudioVideoDriftMsP95": 80.0,
        "maxVideoStarvedPasses": 0,
        "maxAudioStarvedPasses": 0,
        "requireValidatedConversion": false,
        "requireMatchedDisplayRefreshRate": true
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $nativeHarnessManifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            materialize-native-harness-report-set `
            --manifest $nativeHarnessManifestPath `
            --reports-dir $nativeHarnessDir `
            --source-revision smoke-native-harness-revision `
            --player-core-version smoke-core `
            --build-configuration Debug `
            --output $nativeHarnessSummaryPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI materialize-native-harness-report-set returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $nativeHarnessSummary = Get-Content -Raw -LiteralPath $nativeHarnessSummaryPath | ConvertFrom-Json
    if ($nativeHarnessSummary.schemaVersion -ne 1 -or
        $nativeHarnessSummary.caseCount -ne 1 -or
        $nativeHarnessSummary.reportsDirectory -ne $nativeHarnessDir) {
        throw 'Expected materialize-native-harness-report-set summary to describe generated reports.'
    }

    if (-not ($nativeHarnessSummary.limitations -contains 'native-harness: native playback graph was not opened by this command')) {
        throw 'Expected materialize-native-harness-report-set summary to expose native-harness limitation.'
    }

    $nativeHarnessReportPath = Join-Path $nativeHarnessDir 'local\native-harness-sdr-smoke.json'
    if (-not (Test-Path -LiteralPath $nativeHarnessReportPath)) {
        throw 'Expected materialize-native-harness-report-set to write run-id based report path.'
    }

    $nativeHarnessReport = Get-Content -Raw -LiteralPath $nativeHarnessReportPath | ConvertFrom-Json
    if ($nativeHarnessReport.schemaVersion -ne 1 -or
        $nativeHarnessReport.caseMetadata.caseId -ne 'local/native-harness-sdr-smoke' -or
        $nativeHarnessReport.report.result -ne 'skip' -or
        $nativeHarnessReport.report.skip.code -ne 'native-harness.not-implemented' -or
        $nativeHarnessReport.report.skip.failureClass -ne 'insufficient instrumentation' -or
        $nativeHarnessReport.report.skip.failureArea -ne 'evidence-collection' -or
        $nativeHarnessReport.report.environment.sourceRevision -ne 'smoke-native-harness-revision') {
        throw 'Expected materialize-native-harness-report-set to write a standard native harness skip envelope.'
    }

    if (-not ($nativeHarnessReport.report.limitations -contains 'native-harness: native playback graph was not opened by this command') -or
        -not ($nativeHarnessReport.modelAnalysis.evidenceSignals -contains 'skip.code') -or
        -not ($nativeHarnessReport.modelAnalysis.evidenceSignals -contains 'lifecycle.skip')) {
        throw 'Expected native harness skip report to carry limitation and skip evidence signals.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-report-set `
            --manifest $nativeHarnessManifestPath `
            --reports-dir $nativeHarnessDir `
            --output $nativeHarnessValidationPath
        if ($LASTEXITCODE -ne 2) {
            throw 'Expected strict report-set validation to reject the non-executing native harness skip.'
        }
    }
    finally {
        Pop-Location
    }

    $nativeHarnessValidation = Get-Content -Raw -LiteralPath $nativeHarnessValidationPath | ConvertFrom-Json
    if ($nativeHarnessValidation.isValid -ne $false -or
        $nativeHarnessValidation.structureValid -ne $true -or
        $nativeHarnessValidation.executionValid -ne $false -or
        $nativeHarnessValidation.matchedCaseCount -ne 0 -or
        -not ($nativeHarnessValidation.errors | Where-Object {
            $_.signal -eq 'execution.evidenceLevel'
        })) {
        throw 'Expected native harness self-test skip to remain structurally readable while failing execution coverage.'
    }

    $nativeHarnessCapturedReportDir = Join-Path $nativeHarnessCapturedDir 'local'
    New-Item -ItemType Directory -Path $nativeHarnessCapturedReportDir | Out-Null
    @'
{
  "runId": "local/native-harness-sdr-smoke",
  "metricVersion": "software-quality-v1",
  "result": "pass",
  "execution": {
    "attemptId": "smoke-native-captured-attempt",
    "runner": "native-headless",
    "scenario": "playback",
    "evidenceLevel": "native-playback",
    "status": "completed",
    "sourceLocatorHash": "sha256:5073a766f7c829219a03780802afc5037a5b56f172f64ff87b162fc01ffdec69",
    "openedSourceHash": "sha256:6073a766f7c829219a03780802afc5037a5b56f172f64ff87b162fc01ffdec69",
    "openedSourceHashKind": "observed-media-signature-v2",
    "startedAtUtc": "2026-07-08T00:00:00.0000000+00:00",
    "durationMs": 12000.0,
    "requestedSampleDurationMs": 1000.0,
    "observedSampleWallClockDurationMs": 1000.0,
    "sourceOpenAttempted": true,
    "sourceOpened": true,
    "nativeGraphOpened": true,
    "demuxStarted": true,
    "decoderOpened": true,
    "playbackSampleObserved": true
  },
  "environment": {
    "collectorVersion": "app-native-harness-v0",
    "playerCoreVersion": "captured-core",
    "sourceRevision": "captured-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "codec": "hevc",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "https",
    "width": 1920,
    "height": 1080,
    "frameRate": 60.0,
    "hdrKind": "Sdr",
    "isHdr": false,
    "isDirectPlayable": true
  },
  "startup": {
    "commandReceivedAt": "2026-07-08T00:00:00.000Z",
    "playbackStartedAt": "2026-07-08T00:00:00.300Z",
    "startupDurationMs": 300.0
  },
  "lifecycle": {
    "events": [
      { "operation": "load", "status": "observed", "state": "opening", "positionTicks": 0 },
      { "operation": "play", "status": "observed", "state": "playing", "positionTicks": 0 },
      { "operation": "pause", "status": "observed", "state": "paused", "positionTicks": 30000000 },
      { "operation": "resume", "status": "observed", "state": "playing", "positionTicks": 30000000 },
      { "operation": "stop", "status": "observed", "state": "stopped", "positionTicks": 120000000 }
    ]
  },
  "runtimeMetrics": {
    "status": "captured",
    "providerStatus": "native-winrt:returned-snapshot",
    "reason": "captured by smoke native harness fixture",
    "hasSnapshot": true,
    "hasPlaybackSample": true
  },
  "timing": {
    "decodedVideoFrames": 241,
    "renderedVideoFrames": 240,
    "droppedVideoFrames": 0,
    "expectedFrameDurationMs": 16.667,
    "renderIntervalMsP95": 16.9,
    "renderIntervalMsP99": 18.0,
    "maxFrameGapMs": 22.0,
    "framePacingSourceFrameRate": 60.0,
    "lateFrameDropToleranceMs": 41.667
  },
  "sync": {
    "audioClockTicks": 120000000,
    "videoPositionTicks": 120000000,
    "audioVideoDriftMsP95": 12.0
  },
  "buffers": {
    "videoStarvedPasses": 0,
    "audioStarvedPasses": 0
  },
  "colorPipeline": {
    "actualHdrOutput": "Sdr",
    "swapChainFormat": "B8G8R8A8_UNORM",
    "swapChainColorSpace": "RGB_FULL_G22_NONE_P709",
    "conversionStatus": "not-required"
  },
  "display": {
    "hdrStatus": "Sdr",
    "isHdrDisplayAvailable": false,
    "isHdrOutputActive": false,
    "refreshRateHz": 60.0
  },
  "checks": [
    {
      "name": "RenderedVideoFrames",
      "signal": "timing.renderedVideoFrames",
      "status": "pass",
      "failureArea": "frame-pacing",
      "expected": ">= 120",
      "actual": "240"
    }
  ]
}
'@ | Set-Content -LiteralPath (Join-Path $nativeHarnessCapturedReportDir 'native-harness-sdr-smoke.json') -Encoding UTF8

    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $nativeHarnessCapturedReportDir 'native-harness-sdr-smoke.json') `
        -Locator 'file:///quality-cases/native-harness-sdr-smoke.mp4' `
        -AttemptId 'smoke-native-captured-attempt' `
        -Status 'completed' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            materialize-native-harness-report-set `
            --manifest $nativeHarnessManifestPath `
            --captured-reports-dir $nativeHarnessCapturedDir `
            --reports-dir $nativeHarnessImportedDir `
            --source-revision smoke-native-harness-import-revision `
            --player-core-version smoke-core `
            --build-configuration Debug `
            --output $nativeHarnessImportedSummaryPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI materialize-native-harness-report-set import returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $nativeHarnessImportedSummary = Get-Content -Raw -LiteralPath $nativeHarnessImportedSummaryPath | ConvertFrom-Json
    if ($nativeHarnessImportedSummary.schemaVersion -ne 1 -or
        $nativeHarnessImportedSummary.caseCount -ne 1 -or
        $nativeHarnessImportedSummary.reportsDirectory -ne $nativeHarnessImportedDir) {
        throw 'Expected materialize-native-harness-report-set import summary to describe generated reports.'
    }

    if (-not ($nativeHarnessImportedSummary.limitations -contains 'native-harness: imported captured playback evidence; CLI did not open native playback graph')) {
        throw 'Expected materialize-native-harness-report-set import summary to expose import limitation.'
    }

    $nativeHarnessImportedReportPath = Join-Path $nativeHarnessImportedDir 'local\native-harness-sdr-smoke.json'
    if (-not (Test-Path -LiteralPath $nativeHarnessImportedReportPath)) {
        throw 'Expected materialize-native-harness-report-set import to write run-id based report path.'
    }

    $nativeHarnessImportedReport = Get-Content -Raw -LiteralPath $nativeHarnessImportedReportPath | ConvertFrom-Json
    if ($nativeHarnessImportedReport.schemaVersion -ne 1 -or
        $nativeHarnessImportedReport.caseMetadata.caseId -ne 'local/native-harness-sdr-smoke' -or
        $nativeHarnessImportedReport.report.result -ne 'pass' -or
        $nativeHarnessImportedReport.report.environment.sourceRevision -ne 'smoke-native-harness-import-revision' -or
        $nativeHarnessImportedReport.report.runtimeMetrics.providerStatus -ne 'native-winrt:returned-snapshot') {
        throw 'Expected materialize-native-harness-report-set import to normalize captured native playback evidence.'
    }

    if (-not ($nativeHarnessImportedReport.report.checks | Where-Object {
            $_.name -eq 'DisplayRefreshRateHz' -and
            $_.signal -eq 'display.refreshRateHz' -and
            $_.status -eq 'pass'
        }) -or
        -not ($nativeHarnessImportedReport.report.checks | Where-Object {
            $_.name -eq 'RenderIntervalMsP95Cadence' -and
            $_.signal -eq 'timing.renderIntervalMsP95' -and
            $_.status -eq 'pass'
        })) {
        throw 'Expected imported native harness report to be re-evaluated against manifest playback thresholds.'
    }

    if (-not ($nativeHarnessImportedReport.modelAnalysis.evidenceSignals -contains 'runtimeMetrics.providerStatus') -or
        -not ($nativeHarnessImportedReport.modelAnalysis.evidenceSignals -contains 'timing.renderedVideoFrames') -or
        -not ($nativeHarnessImportedReport.report.limitations -contains 'native-harness: imported captured playback evidence; CLI did not open native playback graph')) {
        throw 'Expected imported native harness report to carry runtime evidence and import limitation.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-report-set `
            --manifest $nativeHarnessManifestPath `
            --reports-dir $nativeHarnessImportedDir `
            --output $nativeHarnessImportedValidationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Expected imported native harness report-set to pass validation.'
        }
    }
    finally {
        Pop-Location
    }

    $nativeHarnessImportedValidation = Get-Content -Raw -LiteralPath $nativeHarnessImportedValidationPath | ConvertFrom-Json
    if ($nativeHarnessImportedValidation.isValid -ne $true -or
        $nativeHarnessImportedValidation.matchedCaseCount -ne 1) {
        throw 'Expected imported native harness validation to match every case.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report-set `
            --reports-dir $nativeHarnessImportedDir `
            --output $nativeHarnessImportedAnalysisPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report-set imported native harness returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $nativeHarnessImportedAnalysis = Get-Content -Raw -LiteralPath $nativeHarnessImportedAnalysisPath | ConvertFrom-Json
    if ($nativeHarnessImportedAnalysis.playbackEvidence.scope -ne 'native-software' -or
        $nativeHarnessImportedAnalysis.playbackEvidence.status -ne 'available' -or
        $nativeHarnessImportedAnalysis.playbackEvidence.canEvaluateNativePlayback -ne $true) {
        throw 'Expected imported native harness analyze-report-set summary to mark native software playback evidence as available.'
    }

    $rawCadenceManifestPath = Join-Path $tempRoot 'native-raw-cadence-manifest.json'
    $rawCadenceBaselineDir = Join-Path $tempRoot 'native-raw-cadence-baseline'
    $rawCadenceCandidateDir = Join-Path $tempRoot 'native-raw-cadence-candidate'
    $rawCadenceComparisonsDir = Join-Path $tempRoot 'native-raw-cadence-comparisons'
    $rawCadenceEvaluationPath = Join-Path $tempRoot 'native-raw-cadence-evaluation.json'
    New-Item -ItemType Directory -Path (Join-Path $rawCadenceBaselineDir 'local') | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $rawCadenceCandidateDir 'local') | Out-Null
    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "local/native-raw-cadence-24",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "file:///quality-cases/native-raw-cadence-24.mp4",
      "tier": 1,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [
        "sdr-smoke",
        "frame-pacing",
        "cadence-24"
      ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 24.0,
        "hdrKind": "Sdr",
        "minRenderedVideoFrames": 1,
        "requireValidatedConversion": false,
        "requireMatchedDisplayRefreshRate": true
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $rawCadenceManifestPath -Encoding UTF8

    @'
{
  "runId": "local/native-raw-cadence-24",
  "metricVersion": "software-quality-v1",
  "result": "pass",
  "expected": {
    "codec": "h264",
    "width": 320,
    "height": 180,
    "frameRate": 24.0,
    "hdrKind": "Sdr",
    "minRenderedVideoFrames": 1,
    "requireValidatedConversion": false,
    "requireMatchedDisplayRefreshRate": true
  },
  "execution": {
    "attemptId": "raw-cadence-baseline-attempt",
    "runner": "native-headless",
    "scenario": "playback",
    "evidenceLevel": "native-playback",
    "status": "completed",
    "sourceLocatorHash": "sha256:a583a300e4b10ff6e8942470c73eca9cd9407eaed9e2deea38754cfeba8962e6",
    "openedSourceHash": "sha256:b583a300e4b10ff6e8942470c73eca9cd9407eaed9e2deea38754cfeba8962e6",
    "openedSourceHashKind": "observed-media-signature-v2",
    "startedAtUtc": "2026-07-08T00:00:00.0000000+00:00",
    "durationMs": 3000.0,
    "requestedSampleDurationMs": 1000.0,
    "observedSampleWallClockDurationMs": 1000.0,
    "sourceOpenAttempted": true,
    "sourceOpened": true,
    "nativeGraphOpened": true,
    "demuxStarted": true,
    "decoderOpened": true,
    "playbackSampleObserved": true
  },
  "environment": {
    "collectorVersion": "native-headless-harness-v0.1",
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "raw-cadence-baseline",
    "buildConfiguration": "Debug"
  },
  "source": {
    "codec": "h264",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "file",
    "width": 320,
    "height": 180,
    "frameRate": 24.0,
    "hdrKind": "Sdr",
    "isHdr": false,
    "isDirectPlayable": true,
    "isDolbyVision": false,
    "hasHdr10BaseLayer": false,
    "hasHlgBaseLayer": false
  },
  "startup": {
    "commandReceivedAt": "2026-07-08T00:00:00.000Z",
    "playbackStartedAt": "2026-07-08T00:00:00.300Z",
    "startupDurationMs": 300.0
  },
  "lifecycle": {
    "events": [
      { "operation": "load", "status": "completed", "positionTicks": 0 },
      { "operation": "play", "status": "completed", "positionTicks": 0 },
      { "operation": "pause", "status": "completed", "positionTicks": 10000000 },
      { "operation": "resume", "status": "completed", "positionTicks": 10000000 },
      { "operation": "seek", "status": "completed", "positionTicks": 0 },
      { "operation": "stop", "status": "completed", "positionTicks": 30000000 },
      { "operation": "error", "status": "not-applicable", "positionTicks": 0 }
    ]
  },
  "runtimeMetrics": {
    "status": "captured",
    "providerStatus": "native-headless:returned-snapshot",
    "reason": "raw cadence baseline fixture",
    "hasSnapshot": true,
    "hasPlaybackSample": true
  },
  "timing": {
    "decodedVideoFrames": 73,
    "renderedVideoFrames": 72,
    "expectedFrameDurationMs": 41.667,
    "renderIntervalMsP95": 16.7,
    "renderIntervalMsP99": 17.0,
    "maxFrameGapMs": 17.0,
    "framePacingSourceFrameRate": 24.0,
    "lateFrameDropToleranceMs": 104.167
  },
  "display": {
    "hdrStatus": "Off",
    "refreshRateHz": 24.0
  },
  "colorPipeline": {
    "dxgiInput": "YCBCR_STUDIO_G22_LEFT_P709",
    "dxgiOutput": "RGB_FULL_G22_NONE_P709",
    "conversionStatus": "not-required"
  },
  "error": {
    "code": "none",
    "message": "no error",
    "operation": "",
    "exceptionType": "",
    "failureClass": "insufficient instrumentation",
    "failureArea": "error-handling",
    "isTerminal": false,
    "isRetriable": false
  },
  "checks": []
}
'@ | Set-Content -LiteralPath (Join-Path $rawCadenceBaselineDir 'local\native-raw-cadence-24.json') -Encoding UTF8

    @'
{
  "runId": "local/native-raw-cadence-24",
  "metricVersion": "software-quality-v1",
  "result": "pass",
  "expected": {
    "codec": "h264",
    "width": 320,
    "height": 180,
    "frameRate": 24.0,
    "hdrKind": "Sdr",
    "minRenderedVideoFrames": 1,
    "requireValidatedConversion": false,
    "requireMatchedDisplayRefreshRate": true
  },
  "execution": {
    "attemptId": "raw-cadence-candidate-attempt",
    "runner": "native-headless",
    "scenario": "playback",
    "evidenceLevel": "native-playback",
    "status": "completed",
    "sourceLocatorHash": "sha256:a583a300e4b10ff6e8942470c73eca9cd9407eaed9e2deea38754cfeba8962e6",
    "openedSourceHash": "sha256:b583a300e4b10ff6e8942470c73eca9cd9407eaed9e2deea38754cfeba8962e6",
    "openedSourceHashKind": "observed-media-signature-v2",
    "startedAtUtc": "2026-07-08T00:00:00.0000000+00:00",
    "durationMs": 1500.0,
    "requestedSampleDurationMs": 1000.0,
    "observedSampleWallClockDurationMs": 1000.0,
    "sourceOpenAttempted": true,
    "sourceOpened": true,
    "nativeGraphOpened": true,
    "demuxStarted": true,
    "decoderOpened": true,
    "playbackSampleObserved": true
  },
  "environment": {
    "collectorVersion": "native-headless-harness-v0.1",
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "raw-cadence-candidate",
    "buildConfiguration": "Debug"
  },
  "source": {
    "codec": "h264",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "file",
    "width": 320,
    "height": 180,
    "frameRate": 24.0,
    "hdrKind": "Sdr",
    "isHdr": false,
    "isDirectPlayable": true,
    "isDolbyVision": false,
    "hasHdr10BaseLayer": false,
    "hasHlgBaseLayer": false
  },
  "startup": {
    "commandReceivedAt": "2026-07-08T00:00:00.000Z",
    "playbackStartedAt": "2026-07-08T00:00:00.300Z",
    "startupDurationMs": 300.0
  },
  "lifecycle": {
    "events": [
      { "operation": "load", "status": "completed", "positionTicks": 0 },
      { "operation": "play", "status": "completed", "positionTicks": 0 },
      { "operation": "pause", "status": "completed", "positionTicks": 10000000 },
      { "operation": "resume", "status": "completed", "positionTicks": 10000000 },
      { "operation": "seek", "status": "completed", "positionTicks": 0 },
      { "operation": "stop", "status": "completed", "positionTicks": 15000000 },
      { "operation": "error", "status": "not-applicable", "positionTicks": 0 }
    ]
  },
  "runtimeMetrics": {
    "status": "captured",
    "providerStatus": "native-headless:returned-snapshot",
    "reason": "raw cadence candidate fixture",
    "hasSnapshot": true,
    "hasPlaybackSample": true
  },
  "timing": {
    "decodedVideoFrames": 37,
    "renderedVideoFrames": 36,
    "expectedFrameDurationMs": 41.667,
    "renderIntervalMsP95": 48.0,
    "renderIntervalMsP99": 48.2,
    "maxFrameGapMs": 48.2,
    "framePacingSourceFrameRate": 24.0,
    "lateFrameDropToleranceMs": 104.167
  },
  "display": {
    "hdrStatus": "Off",
    "refreshRateHz": 24.0
  },
  "colorPipeline": {
    "dxgiInput": "YCBCR_STUDIO_G22_LEFT_P709",
    "dxgiOutput": "RGB_FULL_G22_NONE_P709",
    "conversionStatus": "not-required"
  },
  "error": {
    "code": "none",
    "message": "no error",
    "operation": "",
    "exceptionType": "",
    "failureClass": "insufficient instrumentation",
    "failureArea": "error-handling",
    "isTerminal": false,
    "isRetriable": false
  },
  "checks": []
}
'@ | Set-Content -LiteralPath (Join-Path $rawCadenceCandidateDir 'local\native-raw-cadence-24.json') -Encoding UTF8

    $rawCadenceLocator = 'file:///quality-cases/native-raw-cadence-24.mp4'
    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $rawCadenceBaselineDir 'local\native-raw-cadence-24.json') `
        -Locator $rawCadenceLocator `
        -AttemptId 'raw-cadence-baseline-attempt' `
        -Status 'completed' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true `
        -RequestedSampleDurationMs 1000.0
    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $rawCadenceCandidateDir 'local\native-raw-cadence-24.json') `
        -Locator $rawCadenceLocator `
        -AttemptId 'raw-cadence-candidate-attempt' `
        -Status 'completed' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true `
        -RequestedSampleDurationMs 1000.0

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare `
            --baseline (Join-Path $rawCadenceBaselineDir 'local\native-raw-cadence-24.json') `
            --candidate (Join-Path $rawCadenceCandidateDir 'local\native-raw-cadence-24.json') `
            --output $rawCadenceEvaluationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Expected raw native cadence candidate evaluation to pass.'
        }
    }
    finally {
        Pop-Location
    }

    $rawCadenceEvaluation = Get-Content -Raw -LiteralPath $rawCadenceEvaluationPath | ConvertFrom-Json
    if ($rawCadenceEvaluation.result -ne 'improved' -or
        @($rawCadenceEvaluation.regressions).Count -ne 0) {
        throw 'Expected raw native cadence candidate evaluation to detect improvement from current evaluator rules.'
    }

    New-Item -ItemType Directory -Path (Join-Path $nativeHeadlessImportedDir 'local') | Out-Null
    $nativeHeadlessReport = Get-Content -Raw -LiteralPath $nativeHarnessImportedReportPath | ConvertFrom-Json
    $nativeHeadlessReport.report.runtimeMetrics.providerStatus = 'native-headless:returned-snapshot'
    $nativeHeadlessReport.modelAnalysis.runtimeMetrics.providerStatus = 'native-headless:returned-snapshot'
    $nativeHeadlessReport | ConvertTo-Json -Depth 30 |
        Set-Content -LiteralPath (Join-Path $nativeHeadlessImportedDir 'local\native-harness-sdr-smoke.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report-set `
            --reports-dir $nativeHeadlessImportedDir `
            --output $nativeHeadlessAnalysisPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI analyze-report-set native-headless import returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $nativeHeadlessAnalysis = Get-Content -Raw -LiteralPath $nativeHeadlessAnalysisPath | ConvertFrom-Json
    if ($nativeHeadlessAnalysis.playbackEvidence.scope -ne 'native-software' -or
        $nativeHeadlessAnalysis.playbackEvidence.status -ne 'available' -or
        $nativeHeadlessAnalysis.playbackEvidence.canEvaluateNativePlayback -ne $true -or
        -not ($nativeHeadlessAnalysis.evidenceSources -contains 'native-headless:returned-snapshot')) {
        throw 'Expected native-headless runtime evidence to be recognized as App-free native software playback evidence.'
    }

    New-Item -ItemType Directory -Path $nativeHarnessMissingCapturedDir | Out-Null
    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            materialize-native-harness-report-set `
            --manifest $nativeHarnessManifestPath `
            --captured-reports-dir $nativeHarnessMissingCapturedDir `
            --reports-dir $nativeHarnessMissingImportDir `
            --source-revision smoke-native-harness-missing-import-revision `
            --player-core-version smoke-core `
            --build-configuration Debug `
            --output $nativeHarnessMissingImportSummaryPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI materialize-native-harness-report-set missing import returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $nativeHarnessMissingImportReportPath = Join-Path $nativeHarnessMissingImportDir 'local\native-harness-sdr-smoke.json'
    $nativeHarnessMissingImportReport = Get-Content -Raw -LiteralPath $nativeHarnessMissingImportReportPath | ConvertFrom-Json
    if ($nativeHarnessMissingImportReport.report.result -ne 'skip' -or
        $nativeHarnessMissingImportReport.report.skip.code -ne 'native-harness.capture-missing' -or
        $nativeHarnessMissingImportReport.report.skip.failureClass -ne 'insufficient instrumentation' -or
        $nativeHarnessMissingImportReport.report.skip.failureArea -ne 'evidence-collection' -or
        $nativeHarnessMissingImportReport.report.environment.sourceRevision -ne 'smoke-native-harness-missing-import-revision') {
        throw 'Expected native harness import to materialize a structured skip when captured evidence is missing.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            plan-runs `
            --manifest $manifestPath `
            --reports-dir captured-hdr-smoke `
            --duration 60 `
            --purpose hdr-output `
            --max-tier 2 `
            --output $filteredRunPlanPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI filtered plan-runs returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $filteredRunPlan = Get-Content -Raw -LiteralPath $filteredRunPlanPath | ConvertFrom-Json
    if ($filteredRunPlan.caseCount -ne 1) {
        throw 'Expected filtered plan-runs output to include one planned case.'
    }

    if (-not ($filteredRunPlan.filters.purposes -contains 'hdr-output')) {
        throw 'Expected filtered plan-runs output to record purpose filter.'
    }

    if ($filteredRunPlan.filters.maxTier -ne 2) {
        throw 'Expected filtered plan-runs output to record max tier filter.'
    }

    if (-not ($filteredRunPlan.cases | Where-Object { $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' })) {
        throw 'Expected filtered plan-runs output to include HDR case.'
    }

    if ($filteredRunPlan.cases | Where-Object { $_.caseId -eq 'jellyfin/dv-profile5-hevc-4k' }) {
        throw 'Expected filtered plan-runs output to exclude non-matching DV case.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "emby/007-hdr10",
      "uri": "emby://items/item-007",
      "itemId": "item-007",
      "mediaSourceId": "source-hdr10",
      "startPositionTicks": 123,
      "forceSdrOutput": true,
      "pauseSeconds": 12,
      "tier": 1,
      "executionRequirement": { "minimumEvidenceLevel": "app-hosted", "scenario": "pause-resume" },
      "purpose": [
        "pause-resume"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Hdr10"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $embyRunPlanManifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            plan-runs `
            --manifest $embyRunPlanManifestPath `
            --reports-dir captured-emby `
            --duration 45 `
            --source-revision smoke-app-source-revision `
            --output $embyRunPlanPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI plan-runs returned a non-zero exit code for Emby item manifest.'
        }
    }
    finally {
        Pop-Location
    }

    $embyRunPlan = Get-Content -Raw -LiteralPath $embyRunPlanPath | ConvertFrom-Json
    if (-not ($embyRunPlan.cases | Where-Object {
        $_.caseId -eq 'emby/007-hdr10' -and
        $_.captureMode -eq 'emby-item' -and
        $_.devCommand.route -eq 'quality-run' -and
        $_.devCommand.itemId -eq 'item-007' -and
        $_.devCommand.mediaSourceId -eq 'source-hdr10' -and
        $_.devCommand.runId -eq 'emby/007-hdr10' -and
        $_.scenario -eq 'pause-resume' -and
        $_.pauseSeconds -eq 12 -and
        $_.sourceRevision -eq 'smoke-app-source-revision' -and
        $_.devCommand.scenario -eq 'pause-resume' -and
        $_.devCommand.pauseSeconds -eq 12 -and
        $_.devCommand.sourceLocator -eq 'emby://items/item-007' -and
        $_.devCommand.sourceRevision -eq 'smoke-app-source-revision' -and
        $_.devCommand.durationSeconds -eq 45 -and
        $_.devCommand.startPositionTicks -eq 123 -and
        $_.devCommand.forceSdrOutput -eq $true -and
        $_.devCommand.expected.hdrKind -eq 'Hdr10' -and
        ($_.requiredSignals -contains 'colorPipeline.forceSdrOutput')
    })) {
        throw 'Expected playback quality CLI plan-runs output to include Emby quality-run dev command.'
    }

    New-Item -ItemType Directory -Path $reportSetDir | Out-Null
    @'
{
  "runId": "netflix/chimera-4k-2398-hdr-pq",
  "metricVersion": "software-quality-v1",
  "result": "pass",
  "execution": {
    "attemptId": "report-set-hdr-attempt",
    "runner": "native-headless",
    "scenario": "playback",
    "evidenceLevel": "native-playback",
    "status": "completed",
    "sourceLocatorHash": "sha256:6d6dcdf267881094dc407e3b909ee6f37e3da9a9606c4e112c244ed053d978f3",
    "openedSourceHash": "sha256:7d6dcdf267881094dc407e3b909ee6f37e3da9a9606c4e112c244ed053d978f3",
    "openedSourceHashKind": "observed-media-signature-v2",
    "startedAtUtc": "2026-07-08T00:00:00.0000000+00:00",
    "durationMs": 60000.0,
    "requestedSampleDurationMs": 60000.0,
    "observedSampleWallClockDurationMs": 60000.0,
    "sourceOpenAttempted": true,
    "sourceOpened": true,
    "nativeGraphOpened": true,
    "demuxStarted": true,
    "decoderOpened": true,
    "playbackSampleObserved": true
  },
  "environment": {
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "smoke-report-set-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "codec": "hevc",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "https",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Hdr10"
  },
  "lifecycle": {
    "events": [
      { "operation": "load", "status": "observed" },
      { "operation": "play", "status": "observed" },
      { "operation": "pause", "status": "observed" },
      { "operation": "resume", "status": "observed" },
      { "operation": "stop", "status": "observed" }
    ]
  },
  "timing": {
    "decodedVideoFrames": 1441,
    "renderedVideoFrames": 1440,
    "expectedFrameDurationMs": 41.708,
    "maxFrameGapMs": 48.0,
    "framePacingSourceFrameRate": 23.976,
    "lateFrameDropToleranceMs": 104.271
  },
  "runtimeMetrics": {
    "status": "captured",
    "providerStatus": "native-winrt:returned-snapshot",
    "reason": "Runtime metrics snapshot contains playback sample evidence.",
    "hasSnapshot": true,
    "hasPlaybackSample": true
  },
  "colorPipeline": {
    "conversionStatus": "validated"
  },
  "display": {
    "refreshRateHz": 23.976
  }
}
'@ | Set-Content -LiteralPath (Join-Path $reportSetDir 'case-a.json') -Encoding UTF8
    @'
{
  "runId": "jellyfin/dv-profile5-hevc-4k",
  "metricVersion": "software-quality-v1",
  "result": "unsupported",
  "execution": {
    "attemptId": "report-set-dv-attempt",
    "runner": "native-headless",
    "scenario": "playback",
    "evidenceLevel": "native-playback",
    "status": "unsupported",
    "sourceLocatorHash": "sha256:6ff500115e21a7d612e1d98387f7a2eab7777ef97cfa0ab39cd96ac3ffce69a1",
    "openedSourceHash": "sha256:7ff500115e21a7d612e1d98387f7a2eab7777ef97cfa0ab39cd96ac3ffce69a1",
    "openedSourceHashKind": "observed-media-signature-v2",
    "startedAtUtc": "2026-07-08T00:00:00.0000000+00:00",
    "durationMs": 500.0,
    "sourceOpenAttempted": true,
    "sourceOpened": true,
    "nativeGraphOpened": true,
    "demuxStarted": true,
    "decoderOpened": false,
    "playbackSampleObserved": false
  },
  "environment": {
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "smoke-report-set-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "codec": "hevc",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "https",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "DolbyVisionUnsupported"
  },
  "colorPipeline": {
    "conversionStatus": "not-applicable"
  }
}
'@ | Set-Content -LiteralPath (Join-Path $reportSetDir 'case-b.json') -Encoding UTF8
    @'
{
  "runId": "local/missing-file-error-handling",
  "metricVersion": "software-quality-v1",
  "result": "error",
  "execution": {
    "attemptId": "report-set-missing-file-attempt",
    "runner": "native-headless",
    "scenario": "playback",
    "evidenceLevel": "native-playback",
    "status": "failed",
    "sourceLocatorHash": "sha256:4e0e8c8b6060256c02a67f8a9734a5c3d1c90c983fbdae10451f840ef9ed1cff",
    "openedSourceHash": "",
    "startedAtUtc": "2026-07-08T00:00:00.0000000+00:00",
    "durationMs": 10.0,
    "sourceOpenAttempted": true,
    "sourceOpened": false,
    "nativeGraphOpened": false,
    "demuxStarted": false,
    "decoderOpened": false,
    "playbackSampleObserved": false
  },
  "environment": {
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "smoke-report-set-revision",
    "buildConfiguration": "Debug"
  },
  "error": {
    "code": "source.open.missing-file",
    "message": "The media file was not found.",
    "operation": "open",
    "exceptionType": "FileNotFoundException",
    "failureClass": "sample issue",
    "failureArea": "error-handling",
    "isTerminal": true,
    "isRetriable": false
  },
  "lifecycle": {
    "events": [
      {
        "operation": "open",
        "status": "error",
        "message": "The media file was not found."
      }
    ]
  }
}
'@ | Set-Content -LiteralPath (Join-Path $reportSetDir 'case-error.json') -Encoding UTF8

    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $reportSetDir 'case-a.json') `
        -Locator 'https://example.invalid/netflix/chimera-4k-2398-hdr-pq.mp4' `
        -AttemptId 'report-set-hdr-attempt' `
        -Status 'completed' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true

    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $reportSetDir 'case-b.json') `
        -Locator 'https://example.invalid/jellyfin/dv-profile5-hevc-4k.mp4' `
        -AttemptId 'report-set-dv-attempt' `
        -Status 'unsupported' `
        -SourceOpened $true `
        -PlaybackSampleObserved $false

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-report-set `
            --manifest $manifestPath `
            --reports-dir $reportSetDir `
            --output $reportSetValidationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI validate-report-set returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $reportSetValidation = Get-Content -Raw -LiteralPath $reportSetValidationPath | ConvertFrom-Json
    if ($reportSetValidation.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI validate-report-set output schemaVersion 1.'
    }

    if ($reportSetValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI validate-report-set output to be valid.'
    }

    if ($reportSetValidation.matchedCaseCount -ne 3) {
        throw 'Expected playback quality CLI validate-report-set output to include three matched cases.'
    }

    Copy-Item -LiteralPath $reportSetDir -Destination $missingStartupTransportReportSetDir -Recurse
    $missingStartupTransportReportPath = Join-Path $missingStartupTransportReportSetDir 'case-a.json'
    $missingStartupTransportReport =
        Get-Content -Raw -LiteralPath $missingStartupTransportReportPath | ConvertFrom-Json
    $startupSeekComponent = $missingStartupTransportReport.startup.stages[0].components |
        Where-Object { $_.name -eq 'native.startup-seek' } |
        Select-Object -First 1
    $startupSeekComponent.PSObject.Properties.Remove('transportSeekWaitMs')
    $missingStartupTransportReport |
        ConvertTo-Json -Depth 100 |
        Set-Content -LiteralPath $missingStartupTransportReportPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-report-set `
            --manifest $manifestPath `
            --reports-dir $missingStartupTransportReportSetDir `
            --output $missingStartupTransportValidationPath
        if ($LASTEXITCODE -ne 2) {
            throw 'Expected validate-report-set to reject a missing startup transport-call JSON field.'
        }
    }
    finally {
        Pop-Location
    }

    $missingStartupTransportValidation =
        Get-Content -Raw -LiteralPath $missingStartupTransportValidationPath | ConvertFrom-Json
    if (-not ($missingStartupTransportValidation.errors | Where-Object {
        $_.code -eq 'report.requiredSignal.missing' -and
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.signal -eq 'startup.stage.native.open.component.native.startup-seek.transportSeekWaitMs'
    })) {
        throw 'Expected direct JSON validation to report the deleted startup transport-call field.'
    }

    New-Item -ItemType Directory -Path $missingSignalReportSetDir | Out-Null
    @'
{
  "runId": "netflix/chimera-4k-2398-hdr-pq",
  "metricVersion": "software-quality-v1",
  "result": "pass",
  "environment": {
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "smoke-missing-signal-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "codec": "hevc",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "https",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Hdr10"
  }
}
'@ | Set-Content -LiteralPath (Join-Path $missingSignalReportSetDir 'case-a.json') -Encoding UTF8
    @'
{
  "runId": "jellyfin/dv-profile5-hevc-4k",
  "metricVersion": "software-quality-v1",
  "result": "unsupported",
  "environment": {
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "smoke-missing-signal-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "DolbyVisionUnsupported"
  },
  "colorPipeline": {
    "conversionStatus": "not-applicable"
  }
}
'@ | Set-Content -LiteralPath (Join-Path $missingSignalReportSetDir 'case-b.json') -Encoding UTF8
    @'
{
  "runId": "local/missing-file-error-handling",
  "metricVersion": "software-quality-v1",
  "result": "error",
  "environment": {
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "smoke-missing-signal-revision",
    "buildConfiguration": "Debug"
  },
  "error": {
    "code": "source.open.missing-file",
    "message": "The media file was not found.",
    "operation": "open",
    "exceptionType": "FileNotFoundException",
    "failureClass": "sample issue",
    "failureArea": "error-handling",
    "isTerminal": true,
    "isRetriable": false
  }
}
'@ | Set-Content -LiteralPath (Join-Path $missingSignalReportSetDir 'case-error.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-report-set `
            --manifest $manifestPath `
            --reports-dir $missingSignalReportSetDir `
            --output $missingSignalReportSetValidationPath
        if ($LASTEXITCODE -ne 2) {
            throw 'Expected playback quality CLI validate-report-set to return 2 for missing required telemetry.'
        }
    }
    finally {
        Pop-Location
    }

    $missingSignalReportSetValidation = Get-Content -Raw -LiteralPath $missingSignalReportSetValidationPath | ConvertFrom-Json
    if ($missingSignalReportSetValidation.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI invalid validate-report-set output schemaVersion 1.'
    }

    if ($missingSignalReportSetValidation.isValid -ne $false) {
        throw 'Expected playback quality CLI validate-report-set missing telemetry output to be invalid.'
    }

    if (-not ($missingSignalReportSetValidation.errors | Where-Object {
        $_.code -eq 'report.requiredSignal.missing' -and
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.signal -eq 'timing.expectedFrameDurationMs'
    })) {
        throw 'Expected playback quality CLI validate-report-set missing telemetry output to include missing timing evidence.'
    }

    if (-not ($missingSignalReportSetValidation.errors | Where-Object {
        $_.code -eq 'report.requiredSignal.missing' -and
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.signal -eq 'display.refreshRateHz' -and
        $_.failureArea -eq 'frame-pacing' -and
        $_.failureClass -eq 'insufficient instrumentation' -and
        ($_.codeTargets -contains 'src/NoiraPlayer.Native/Media/FramePacing.h')
    })) {
        throw 'Expected playback quality CLI validate-report-set missing telemetry output to include triaged display refresh evidence and failure class.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "jellyfin/zero-starvation-buffering",
      "uri": "https://example.invalid/jellyfin/zero-starvation-buffering.mp4",
      "tier": 1,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [
        "buffering"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Hdr10",
        "maxVideoStarvedPasses": 0,
        "maxAudioStarvedPasses": 0
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $zeroCounterManifestPath -Encoding UTF8
    New-Item -ItemType Directory -Path $zeroCounterReportSetDir | Out-Null
    @'
{
  "runId": "jellyfin/zero-starvation-buffering",
  "metricVersion": "software-quality-v1",
  "result": "pass",
  "execution": {
    "attemptId": "zero-counter-native-attempt",
    "runner": "native-headless",
    "scenario": "playback",
    "evidenceLevel": "native-playback",
    "status": "completed",
    "sourceLocatorHash": "sha256:42369d6a58bde4131352982fddaefd4a639b1e9487e8db91eefc5f314782de13",
    "openedSourceHash": "sha256:52369d6a58bde4131352982fddaefd4a639b1e9487e8db91eefc5f314782de13",
    "openedSourceHashKind": "observed-media-signature-v2",
    "startedAtUtc": "2026-07-08T00:00:00.0000000+00:00",
    "durationMs": 1000.0,
    "sourceOpenAttempted": true,
    "sourceOpened": true,
    "nativeGraphOpened": true,
    "demuxStarted": true,
    "decoderOpened": true,
    "playbackSampleObserved": true
  },
  "environment": {
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "smoke-zero-counter-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "codec": "hevc",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "https",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Hdr10"
  },
  "lifecycle": {
    "events": [
      { "operation": "load", "status": "observed" },
      { "operation": "play", "status": "observed" },
      { "operation": "pause", "status": "observed" },
      { "operation": "resume", "status": "observed" },
      { "operation": "stop", "status": "observed" }
    ]
  },
  "buffers": {
    "videoStarvedPasses": 0,
    "audioStarvedPasses": 0
  },
  "timing": {
    "decodedVideoFrames": 1,
    "renderedVideoFrames": 1
  },
  "runtimeMetrics": {
    "status": "captured",
    "providerStatus": "native-winrt:returned-snapshot",
    "reason": "Runtime metrics snapshot contains playback sample evidence.",
    "hasSnapshot": true,
    "hasPlaybackSample": true
  },
  "colorPipeline": {
    "conversionStatus": "validated"
  }
}
'@ | Set-Content -LiteralPath (Join-Path $zeroCounterReportSetDir 'zero-counter.json') -Encoding UTF8

    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $zeroCounterReportSetDir 'zero-counter.json') `
        -Locator 'https://example.invalid/jellyfin/zero-starvation-buffering.mp4' `
        -AttemptId 'zero-counter-native-attempt' `
        -Status 'completed' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-report-set `
            --manifest $zeroCounterManifestPath `
            --reports-dir $zeroCounterReportSetDir `
            --output $zeroCounterReportSetValidationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Expected playback quality CLI validate-report-set to accept explicit zero required counters when JSON fields are present.'
        }
    }
    finally {
        Pop-Location
    }

    $zeroCounterReportSetValidation = Get-Content -Raw -LiteralPath $zeroCounterReportSetValidationPath | ConvertFrom-Json
    if ($zeroCounterReportSetValidation.isValid -ne $true -or
        $zeroCounterReportSetValidation.matchedCaseCount -ne 1) {
        throw 'Expected playback quality CLI validate-report-set zero-counter output to be valid.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            analyze-report `
            --report (Join-Path $zeroCounterReportSetDir 'zero-counter.json') `
            --output $zeroCounterAnalysisPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Expected playback quality CLI analyze-report to accept explicit zero required counters.'
        }
    }
    finally {
        Pop-Location
    }

    $zeroCounterAnalysis = Get-Content -Raw -LiteralPath $zeroCounterAnalysisPath | ConvertFrom-Json
    if ($zeroCounterAnalysis.buffering.status -ne 'stable') {
        throw 'Expected playback quality CLI analyze-report zero-counter buffering status to be stable.'
    }

    if (-not ($zeroCounterAnalysis.buffering.signals -contains 'buffers.videoStarvedPasses') -or
        -not ($zeroCounterAnalysis.buffering.signals -contains 'buffers.audioStarvedPasses')) {
        throw 'Expected playback quality CLI analyze-report zero-counter buffering signals to include explicit zero counters.'
    }

    if ($zeroCounterAnalysis.missingEvidence -contains 'buffers.queuedAudioBuffers') {
        throw 'Expected playback quality CLI analyze-report zero-counter output not to treat explicit zero starvation counters as missing buffer evidence.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare `
            --baseline $baselinePath `
            --candidate $candidatePath `
            --output $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $outputPath)) {
        throw 'Expected playback quality CLI to write comparison output.'
    }

    $comparison = Get-Content -Raw -LiteralPath $outputPath | ConvertFrom-Json
    if ($comparison.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI comparison output schemaVersion 1.'
    }

    if ($comparison.result -ne 'improved') {
        throw 'Expected playback quality CLI comparison result to be improved.'
    }

    if ($comparison.confidence.level -ne 'strong') {
        throw 'Expected playback quality CLI comparison confidence to be strong.'
    }

    if ($comparison.optimization.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI optimization action to accept candidate.'
    }

    if (-not ($comparison.codeTargets -contains 'src/NoiraPlayer.Native/Media/FramePacing.h')) {
        throw 'Expected playback quality CLI comparison output to include frame-pacing code target.'
    }

    if (-not ($comparison.nextActions | Where-Object {
        $_.rank -eq 1 -and
        $_.action -eq 'accept-candidate' -and
        $_.risk -eq 'low' -and
        $_.failureArea -eq 'frame-pacing' -and
        ($_.caseIds -contains 'candidate') -and
        ($_.signals -contains 'timing.maxFrameGapMs') -and
        ($_.codeTargets -contains 'src/NoiraPlayer.Native/Media/FramePacing.h')
    })) {
        throw 'Expected playback quality CLI comparison output to include ranked next action context.'
    }

    if (-not ($comparison.improvements | Where-Object { $_.signal -eq 'timing.maxFrameGapMs' })) {
        throw 'Expected playback quality CLI comparison to include timing.maxFrameGapMs improvement.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare `
            --baseline $presenceBaselinePath `
            --candidate $presenceCandidatePath `
            --output $presenceComparisonPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI presence comparison returned a non-zero exit code.'
        }

        dotnet $cliDll `
            analyze-report `
            --report $presenceCandidatePath `
            --output $presenceAnalysisPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI presence analysis returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $presenceComparison = Get-Content -Raw -LiteralPath $presenceComparisonPath | ConvertFrom-Json
    $presenceAnalysis = Get-Content -Raw -LiteralPath $presenceAnalysisPath | ConvertFrom-Json
    $baselineOnlySignals = @(
        'timing.hardwareDecodedVideoFrames',
        'timing.softwareDecodedVideoFrames',
        'timing.droppedVideoFrames',
        'framePacing.droppedVideoFramePercent',
        'timing.audioAheadWaitPassDurationMsP95',
        'timing.audioAheadWaitFinalDeltaAbsMsP95',
        'sync.audioVideoDriftMsP95',
        'buffers.queuedAudioBuffers'
    )
    foreach ($signal in $baselineOnlySignals) {
        if ($presenceComparison.coverage.matchedSignals -contains $signal -or
            -not ($presenceComparison.coverage.unmatchedBaselineSignals -contains $signal) -or
            $presenceComparison.improvements.signal -contains $signal -or
            $presenceComparison.regressions.signal -contains $signal) {
            throw ("Expected presence comparison to classify $signal as baseline-only evidence without a quality delta. " +
                "matched=$($presenceComparison.coverage.matchedSignals -contains $signal) " +
                "baselineOnly=$($presenceComparison.coverage.unmatchedBaselineSignals -contains $signal) " +
                "improvement=$($presenceComparison.improvements.signal -contains $signal) " +
                "regression=$($presenceComparison.regressions.signal -contains $signal)")
        }
    }

    if ($presenceComparison.confidence.level -ne 'partial' -or
        -not ($presenceComparison.confidence.reasons -contains 'unmatched comparison signals are present')) {
        throw 'Expected one-sided runtime signal presence to lower comparison confidence.'
    }

    if ($presenceAnalysis.evidenceSignals -contains 'timing.hardwareDecodedVideoFrames' -or
        $presenceAnalysis.evidenceSignals -contains 'timing.softwareDecodedVideoFrames') {
        throw 'Expected analyzer not to infer missing decode-mode counters from decodedVideoFrames.'
    }

    $incompatibleCandidate = Get-Content -Raw -LiteralPath $candidatePath | ConvertFrom-Json
    $incompatibleCandidate.source.mediaSourceId = 'source-2'
    $incompatibleCandidate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $incompatibleCandidatePath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare `
            --baseline $baselinePath `
            --candidate $incompatibleCandidatePath `
            --output $incompatibleOutputPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI incompatible comparison returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $incompatibleComparison = Get-Content -Raw -LiteralPath $incompatibleOutputPath | ConvertFrom-Json
    if ($incompatibleComparison.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI incompatible comparison output schemaVersion 1.'
    }

    if ($incompatibleComparison.result -ne 'insufficient-evidence') {
        throw 'Expected playback quality CLI incompatible comparison to be insufficient evidence.'
    }

    if (-not ($incompatibleComparison.optimization.blockers -contains 'comparison.incompatible-inputs')) {
        throw 'Expected incompatible comparison output to include machine-readable incompatibility blocker.'
    }

    if (-not ($incompatibleComparison.optimization.signals -contains 'source.mediaSourceId')) {
        throw 'Expected incompatible comparison output to include mismatched source signal.'
    }

    $missingChecksBaseline = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json
    $missingChecksBaseline.checks = @()
    $missingChecksBaseline | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $missingChecksBaselinePath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare `
            --baseline $missingChecksBaselinePath `
            --candidate $candidatePath `
            --output $missingChecksOutputPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI missing-checks comparison returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $missingChecksComparison = Get-Content -Raw -LiteralPath $missingChecksOutputPath | ConvertFrom-Json
    if ($missingChecksComparison.result -ne 'insufficient-evidence') {
        throw 'Expected playback quality CLI missing-checks comparison to be insufficient evidence.'
    }

    if (-not ($missingChecksComparison.optimization.blockers -contains 'comparison.missing-checks')) {
        throw 'Expected missing-checks comparison output to include machine-readable coverage blocker.'
    }

    $noMatchedCandidate = Get-Content -Raw -LiteralPath $candidatePath | ConvertFrom-Json
    $noMatchedCandidate.checks = @([pscustomobject]@{
        name = 'ActualHdrOutput'
        signal = 'colorPipeline.actualHdrOutput'
        status = 'fail'
        failureArea = 'color-pipeline'
        expected = 'Hdr10'
        actual = 'Sdr'
    })
    $noMatchedCandidate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $noMatchedCandidatePath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare `
            --baseline $baselinePath `
            --candidate $noMatchedCandidatePath `
            --output $noMatchedOutputPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI no-matched-signals comparison returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $noMatchedComparison = Get-Content -Raw -LiteralPath $noMatchedOutputPath | ConvertFrom-Json
    if ($noMatchedComparison.result -ne 'insufficient-evidence') {
        throw 'Expected playback quality CLI no-matched-signals comparison to be insufficient evidence.'
    }

    if (-not ($noMatchedComparison.optimization.blockers -contains 'comparison.no-matched-signals')) {
        throw 'Expected no-matched-signals comparison output to include machine-readable coverage blocker.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare `
            --baseline $baselineEnvelopePath `
            --candidate $candidateEnvelopePath `
            --output $envelopeOutputPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI envelope comparison returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $envelopeComparison = Get-Content -Raw -LiteralPath $envelopeOutputPath | ConvertFrom-Json
    if ($envelopeComparison.result -ne 'improved') {
        throw 'Expected playback quality CLI envelope comparison result to be improved.'
    }

    if (-not ($envelopeComparison.improvements | Where-Object { $_.signal -eq 'timing.maxFrameGapMs' })) {
        throw 'Expected playback quality CLI envelope comparison to include timing.maxFrameGapMs improvement.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            summarize `
            --comparison $outputPath `
            --output $suitePath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI summarize returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $suitePath)) {
        throw 'Expected playback quality CLI to write suite output.'
    }

    $suite = Get-Content -Raw -LiteralPath $suitePath | ConvertFrom-Json
    if ($suite.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI suite output schemaVersion 1.'
    }

    if ($suite.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI suite action to accept candidate.'
    }

    if ($suite.decision -ne 'keep-candidate') {
        throw 'Expected playback quality CLI suite decision to keep candidate.'
    }

    if ($suite.totalComparisonCount -ne 1) {
        throw 'Expected playback quality CLI suite to include one comparison.'
    }

    New-Item -ItemType Directory -Path $baselineDir | Out-Null
    New-Item -ItemType Directory -Path $candidateDir | Out-Null
    Copy-Item -LiteralPath $baselineEnvelopePath -Destination (Join-Path $baselineDir 'case-a.json')
    Copy-Item -LiteralPath $candidateEnvelopePath -Destination (Join-Path $candidateDir 'case-a.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare-suite `
            --baseline-dir $baselineDir `
            --candidate-dir $candidateDir `
            --comparisons-dir $comparisonsDir `
            --output $suiteFromReportsPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI compare-suite returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $suiteFromReportsPath)) {
        throw 'Expected playback quality CLI compare-suite to write suite output.'
    }

    $suiteFromReports = Get-Content -Raw -LiteralPath $suiteFromReportsPath | ConvertFrom-Json
    if ($suiteFromReports.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI compare-suite output schemaVersion 1.'
    }

    if ($suiteFromReports.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI compare-suite action to accept candidate.'
    }

    if ($suiteFromReports.decision -ne 'keep-candidate') {
        throw 'Expected playback quality CLI compare-suite decision to keep candidate.'
    }

    if ($suiteFromReports.totalComparisonCount -ne 1) {
        throw 'Expected playback quality CLI compare-suite to include one comparison.'
    }

    if (-not ($suiteFromReports.cases | Where-Object { $_.caseId -eq 'case-a.json' -and $_.action -eq 'accept-candidate' })) {
        throw 'Expected playback quality CLI compare-suite to include case summary for case-a.json.'
    }

    $comparisonFromSuitePath = Join-Path $comparisonsDir 'case-a.json'
    if (-not (Test-Path -LiteralPath $comparisonFromSuitePath)) {
        throw 'Expected playback quality CLI compare-suite to write individual comparison output.'
    }

    $comparisonFromSuite = Get-Content -Raw -LiteralPath $comparisonFromSuitePath | ConvertFrom-Json
    if ($comparisonFromSuite.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI compare-suite comparison output schemaVersion 1.'
    }

    if ($comparisonFromSuite.result -ne 'improved') {
        throw 'Expected playback quality CLI compare-suite comparison result to be improved.'
    }

    if ($comparisonFromSuite.caseId -ne 'case-a.json') {
        throw 'Expected playback quality CLI compare-suite comparison to include caseId.'
    }

    $materializedRunResultPath = Join-Path $tempRoot 'materialized-run-result.json'
    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            materialize-run-result `
            --report $baselineEnvelopePath `
            --output $materializedRunResultPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI materialize-run-result returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $materializedRunResult = Get-Content -Raw -LiteralPath $materializedRunResultPath | ConvertFrom-Json
    if ($materializedRunResult.schemaVersion -ne 1) {
        throw 'Expected materialize-run-result to write PlaybackQualityRunResult schemaVersion 1.'
    }

    if ($materializedRunResult.report.runId -ne 'baseline' -or
        $materializedRunResult.caseMetadata.caseId -ne 'baseline' -or
        $materializedRunResult.caseMetadata.category -ne 'stable' -or
        $materializedRunResult.caseMetadata.severity -ne 'critical' -or
        $materializedRunResult.caseMetadata.stability -ne 'variable' -or
        $materializedRunResult.modelAnalysis.runId -ne 'baseline' -or
        $materializedRunResult.modelAnalysis.result -ne 'fail' -or
        $materializedRunResult.modelAnalysis.analyzerVersion -lt 1) {
        throw 'Expected materialize-run-result to preserve case metadata and report while regenerating model analysis.'
    }

    New-Item -ItemType Directory -Path $runIdBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $runIdCandidateDir | Out-Null
    @'
{
  "runId": "item-1/source-1",
  "metricVersion": "software-quality-v1",
  "result": "fail",
  "environment": {
    "playerCoreVersion": "core-baseline",
    "sourceRevision": "baseline-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "https",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Sdr"
  },
  "lifecycle": {
    "events": [
      { "operation": "load", "status": "observed" },
      { "operation": "play", "status": "observed" },
      { "operation": "pause", "status": "observed" },
      { "operation": "resume", "status": "observed" },
      { "operation": "seek", "status": "observed" },
      { "operation": "endOfStream", "status": "observed" },
      { "operation": "stop", "status": "observed" }
    ]
  },
  "tracks": {
    "videoTrackCount": 1,
    "audioTrackCount": 1,
    "subtitleTrackCount": 1,
    "selectedVideoStreamIndex": 0,
    "selectedAudioStreamIndex": 1,
    "isSubtitleDisabled": true,
    "video": [
      {
        "index": 0,
        "kind": "Video",
        "codec": "hevc",
        "language": "und",
        "isExternal": false,
        "isDefault": true,
        "isForced": false
      }
    ],
    "audio": [
      {
        "index": 1,
        "kind": "Audio",
        "codec": "aac",
        "language": "eng",
        "channelLayout": "2.0",
        "channels": 2,
        "isExternal": false,
        "isDefault": true,
        "isForced": false
      }
    ],
    "subtitles": [
      {
        "index": 2,
        "kind": "Subtitle",
        "codec": "srt",
        "language": "eng",
        "isExternal": false,
        "isDefault": false,
        "isForced": false
      }
    ]
  },
  "position": {
    "requestedStartPositionTicks": 600000000,
    "seekTargetPositionTicks": 600000000,
    "actualPositionTicks": 600000000,
    "seekPositionErrorMs": 0
  },
  "timing": {
    "renderPasses": 1440,
    "renderedVideoFrames": 1440,
    "expectedFrameDurationMs": 41.708,
    "renderIntervalMsP95": 43.0,
    "renderIntervalMsP99": 45.0,
    "maxFrameGapMs": 180.0,
    "framePacingSourceFrameRate": 23.976,
    "lateFrameDropToleranceMs": 104.271
  },
  "sync": {
    "audioVideoDriftMsP95": 20.0
  },
  "buffers": {
    "submittedAudioFrames": 48000,
    "queuedAudioBuffers": 4,
    "videoStarvedPasses": 0,
    "audioStarvedPasses": 0
  },
  "runtimeMetrics": {
    "status": "captured",
    "providerStatus": "native-winrt:returned-snapshot",
    "reason": "Runtime metrics snapshot contains playback sample evidence.",
    "hasSnapshot": true,
    "hasPlaybackSample": true
  },
  "colorPipeline": {
    "conversionStatus": "validated",
    "forceSdrOutput": true
  },
  "display": {
    "refreshRateHz": 23.976
  },
  "checks": [
    {
      "name": "MaxFrameGapMs",
      "signal": "timing.maxFrameGapMs",
      "status": "fail",
      "failureArea": "frame-pacing",
      "expected": "105.000",
      "actual": "180.000"
    }
  ]
}
'@ | Set-Content -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') -Encoding UTF8
    @'
{
  "runId": "item-1/source-1",
  "metricVersion": "software-quality-v1",
  "result": "fail",
  "environment": {
    "playerCoreVersion": "core-candidate",
    "sourceRevision": "candidate-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "hasDirectStreamUrl": true,
    "directStreamProtocol": "https",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Sdr"
  },
  "lifecycle": {
    "events": [
      { "operation": "load", "status": "observed" },
      { "operation": "play", "status": "observed" },
      { "operation": "pause", "status": "observed" },
      { "operation": "resume", "status": "observed" },
      { "operation": "seek", "status": "observed" },
      { "operation": "endOfStream", "status": "observed" },
      { "operation": "stop", "status": "observed" }
    ]
  },
  "tracks": {
    "videoTrackCount": 1,
    "audioTrackCount": 1,
    "subtitleTrackCount": 1,
    "selectedVideoStreamIndex": 0,
    "selectedAudioStreamIndex": 1,
    "isSubtitleDisabled": true,
    "video": [
      {
        "index": 0,
        "kind": "Video",
        "codec": "hevc",
        "language": "und",
        "isExternal": false,
        "isDefault": true,
        "isForced": false
      }
    ],
    "audio": [
      {
        "index": 1,
        "kind": "Audio",
        "codec": "aac",
        "language": "eng",
        "channelLayout": "2.0",
        "channels": 2,
        "isExternal": false,
        "isDefault": true,
        "isForced": false
      }
    ],
    "subtitles": [
      {
        "index": 2,
        "kind": "Subtitle",
        "codec": "srt",
        "language": "eng",
        "isExternal": false,
        "isDefault": false,
        "isForced": false
      }
    ]
  },
  "position": {
    "requestedStartPositionTicks": 600000000,
    "seekTargetPositionTicks": 600000000,
    "actualPositionTicks": 600000000,
    "seekPositionErrorMs": 0
  },
  "timing": {
    "renderPasses": 1440,
    "renderedVideoFrames": 1440,
    "expectedFrameDurationMs": 41.708,
    "renderIntervalMsP95": 42.0,
    "renderIntervalMsP99": 44.0,
    "maxFrameGapMs": 120.0,
    "framePacingSourceFrameRate": 23.976,
    "lateFrameDropToleranceMs": 104.271
  },
  "sync": {
    "audioVideoDriftMsP95": 18.0
  },
  "buffers": {
    "submittedAudioFrames": 48000,
    "queuedAudioBuffers": 4,
    "videoStarvedPasses": 0,
    "audioStarvedPasses": 0
  },
  "runtimeMetrics": {
    "status": "captured",
    "providerStatus": "native-winrt:returned-snapshot",
    "reason": "Runtime metrics snapshot contains playback sample evidence.",
    "hasSnapshot": true,
    "hasPlaybackSample": true
  },
  "colorPipeline": {
    "conversionStatus": "validated",
    "forceSdrOutput": true
  },
  "display": {
    "refreshRateHz": 23.976
  },
  "checks": [
    {
      "name": "MaxFrameGapMs",
      "signal": "timing.maxFrameGapMs",
      "status": "fail",
      "failureArea": "frame-pacing",
      "expected": "105.000",
      "actual": "120.000"
    }
  ]
}
'@ | Set-Content -LiteralPath (Join-Path $runIdCandidateDir 'candidate-renamed.json') -Encoding UTF8

    @'
{
  "runId": "errors/missing-file",
  "metricVersion": "software-quality-v1",
  "result": "error",
  "environment": {
    "playerCoreVersion": "core-baseline",
    "sourceRevision": "baseline-revision",
    "buildConfiguration": "Debug"
  },
  "error": {
    "code": "source.open.missing-file",
    "message": "The media file was not found.",
    "operation": "open",
    "exceptionType": "FileNotFoundException",
    "failureClass": "sample issue",
    "failureArea": "error-handling",
    "isTerminal": true,
    "isRetriable": false
  },
  "lifecycle": {
    "events": [
      {
        "operation": "open",
        "status": "error",
        "message": "The media file was not found."
      }
    ]
  },
  "checks": [
    {
      "name": "PlaybackRuntimeError",
      "signal": "error.code",
      "status": "fail",
      "failureArea": "error-handling",
      "failureClass": "sample issue",
      "expected": "playback operation completed",
      "actual": "source.open.missing-file"
    }
  ]
}
'@ | Set-Content -LiteralPath (Join-Path $runIdBaselineDir 'error-baseline.json') -Encoding UTF8

    @'
{
  "runId": "errors/missing-file",
  "metricVersion": "software-quality-v1",
  "result": "error",
  "environment": {
    "playerCoreVersion": "core-candidate",
    "sourceRevision": "candidate-revision",
    "buildConfiguration": "Debug"
  },
  "error": {
    "code": "source.open.missing-file",
    "message": "The media file was not found.",
    "operation": "open",
    "exceptionType": "FileNotFoundException",
    "failureClass": "sample issue",
    "failureArea": "error-handling",
    "isTerminal": true,
    "isRetriable": false
  },
  "lifecycle": {
    "events": [
      {
        "operation": "open",
        "status": "error",
        "message": "The media file was not found."
      }
    ]
  },
  "checks": [
    {
      "name": "PlaybackRuntimeError",
      "signal": "error.code",
      "status": "fail",
      "failureArea": "error-handling",
      "failureClass": "sample issue",
      "expected": "playback operation completed",
      "actual": "source.open.missing-file"
    }
  ]
}
'@ | Set-Content -LiteralPath (Join-Path $runIdCandidateDir 'error-candidate.json') -Encoding UTF8

    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $runIdBaselineDir 'baseline-a.json') `
        -Locator 'https://example.invalid/item-1/source-1.mp4' `
        -AttemptId 'candidate-eval-item-baseline' `
        -Status 'completed' `
        -Scenario 'timeline' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true
    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $runIdCandidateDir 'candidate-renamed.json') `
        -Locator 'https://example.invalid/item-1/source-1.mp4' `
        -AttemptId 'candidate-eval-item-candidate' `
        -Status 'completed' `
        -Scenario 'timeline' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true
    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $runIdBaselineDir 'error-baseline.json') `
        -Locator 'https://example.invalid/errors/missing-file.mp4' `
        -AttemptId 'candidate-eval-error-baseline' `
        -Status 'failed' `
        -SourceOpened $false `
        -PlaybackSampleObserved $false
    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $runIdCandidateDir 'error-candidate.json') `
        -Locator 'https://example.invalid/errors/missing-file.mp4' `
        -AttemptId 'candidate-eval-error-candidate' `
        -Status 'failed' `
        -SourceOpened $false `
        -PlaybackSampleObserved $false

    $endOfStreamRunId = 'item-1/source-1-end-of-stream'
    $endOfStreamLocator = 'https://example.invalid/item-1/source-1-end-of-stream.mp4'
    $endOfStreamBaselinePath = Join-Path $runIdBaselineDir 'end-of-stream-baseline.json'
    $endOfStreamCandidatePath = Join-Path $runIdCandidateDir 'end-of-stream-candidate.json'
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') `
        -Destination $endOfStreamBaselinePath
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') `
        -Destination $endOfStreamCandidatePath
    foreach ($endOfStreamReportSpec in @(
        [pscustomobject]@{
            Path = $endOfStreamBaselinePath
            PlayerCoreVersion = 'core-baseline'
            SourceRevision = 'baseline-revision'
            AttemptId = 'candidate-eval-eos-baseline'
        },
        [pscustomobject]@{
            Path = $endOfStreamCandidatePath
            PlayerCoreVersion = 'core-candidate'
            SourceRevision = 'candidate-revision'
            AttemptId = 'candidate-eval-eos-candidate'
        }
    )) {
        $endOfStreamReport = Get-Content -LiteralPath $endOfStreamReportSpec.Path -Raw |
            ConvertFrom-Json
        $endOfStreamReport.runId = $endOfStreamRunId
        $endOfStreamReport.environment.playerCoreVersion = $endOfStreamReportSpec.PlayerCoreVersion
        $endOfStreamReport.environment.sourceRevision = $endOfStreamReportSpec.SourceRevision
        $endOfStreamReport.lifecycle.events = @($endOfStreamReport.lifecycle.events) + @(
            [pscustomobject]@{
                operation = 'endOfStream'
                status = 'completed'
                state = 'Stopped'
                positionTicks = 600000000
                message = 'native PlaybackGraph reported Playback ended.'
            }
        )
        $endOfStreamReport | ConvertTo-Json -Depth 100 |
            Set-Content -LiteralPath $endOfStreamReportSpec.Path -Encoding UTF8
        Set-SmokeNativeExecutionEvidence `
            -Path $endOfStreamReportSpec.Path `
            -Locator $endOfStreamLocator `
            -AttemptId $endOfStreamReportSpec.AttemptId `
            -Status 'completed' `
            -Scenario 'end-of-stream' `
            -SourceOpened $true `
            -PlaybackSampleObserved $true
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare-suite `
            --baseline-dir $runIdBaselineDir `
            --candidate-dir $runIdCandidateDir `
            --match-by run-id `
            --comparisons-dir $runIdComparisonsDir `
            --output $runIdSuitePath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI compare-suite run-id matching returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $runIdSuite = Get-Content -Raw -LiteralPath $runIdSuitePath | ConvertFrom-Json
    if ($runIdSuite.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI compare-suite run-id matching output schemaVersion 1.'
    }

    if ($runIdSuite.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI compare-suite run-id matching action to accept candidate.'
    }

    if ($runIdSuite.decision -ne 'keep-candidate') {
        throw 'Expected playback quality CLI compare-suite run-id matching decision to keep candidate.'
    }

    if (-not ($runIdSuite.cases | Where-Object { $_.caseId -eq 'item-1/source-1' -and $_.action -eq 'accept-candidate' })) {
        throw 'Expected playback quality CLI compare-suite run-id matching to use run-id case summary.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "item-1/source-1",
      "uri": "https://example.invalid/item-1/source-1.mp4",
      "tier": 2,
      "executionRequirement": {
        "minimumEvidenceLevel": "native-playback",
        "scenario": "timeline"
      },
      "purpose": [
        "sdr-smoke",
        "hdr-output",
        "hdr-force-sdr",
        "dv-reject",
        "dv-fallback",
        "cadence-23.976",
        "av-sync",
        "buffering",
        "frame-pacing",
        "timeline",
        "tracks",
        "subtitles",
        "subtitles"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Sdr"
      }
    },
    {
      "caseId": "item-1/source-1-end-of-stream",
      "uri": "https://example.invalid/item-1/source-1-end-of-stream.mp4",
      "tier": 1,
      "executionRequirement": {
        "minimumEvidenceLevel": "native-playback",
        "scenario": "end-of-stream"
      },
      "purpose": [
        "end-of-stream"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Sdr"
      }
    },
    {
      "caseId": "errors/missing-file",
      "uri": "https://example.invalid/errors/missing-file.mp4",
      "tier": 1,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [
        "error-handling"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Sdr"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $candidateEvaluationManifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $runIdBaselineDir `
            --candidate-dir $runIdCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationComparisonsDir `
            --output $candidateEvaluationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI evaluate-candidate returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $candidateEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationPath | ConvertFrom-Json
    if ($candidateEvaluation.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI evaluate-candidate output schemaVersion 1.'
    }

    if ($candidateEvaluation.evaluationVersion -ne 'playback-quality-v0.15') {
        throw 'Expected playback quality CLI evaluate-candidate output evaluationVersion playback-quality-v0.15.'
    }

    if ($candidateEvaluation.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI evaluate-candidate action to accept candidate.'
    }

    if ($candidateEvaluation.decision -ne 'keep-candidate') {
        throw 'Expected playback quality CLI evaluate-candidate decision to keep candidate.'
    }

    if ($candidateEvaluation.manifestValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI evaluate-candidate manifest validation to be valid.'
    }

    if ($candidateEvaluation.baselineReportSetValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI evaluate-candidate baseline report set validation to be valid.'
    }

    if ($candidateEvaluation.candidateReportSetValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI evaluate-candidate candidate report set validation to be valid.'
    }

    if ($candidateEvaluation.suite.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI evaluate-candidate suite action to accept candidate.'
    }

    if ($null -eq $candidateEvaluation.activeGate -or
        $candidateEvaluation.activeGate.name -ne 'suite' -or
        $candidateEvaluation.activeGate.status -ne 'pass' -or
        $candidateEvaluation.activeGate.action -ne 'accept-candidate' -or
        $candidateEvaluation.activeGate.risk -ne 'low') {
        throw 'Expected evaluate-candidate active gate to point at passing suite decision.'
    }

    if ($null -eq $candidateEvaluation.activeGate.confidence -or
        $candidateEvaluation.activeGate.confidence.level -ne 'strong' -or
        $candidateEvaluation.activeGate.confidence.strongCount -ne 3) {
        throw 'Expected evaluate-candidate active gate to expose strong suite confidence.'
    }

    if ($null -eq $candidateEvaluation.activeGate.resultCounts -or
        $candidateEvaluation.activeGate.resultCounts.totalCount -ne 3 -or
        $candidateEvaluation.activeGate.resultCounts.improvedCount -ne 1 -or
        $candidateEvaluation.activeGate.resultCounts.unchangedCount -ne 2) {
        throw 'Expected evaluate-candidate active gate to expose improved suite result counts.'
    }

    if (-not ($candidateEvaluation.activeGate.signalSummaries | Where-Object {
        $_.signal -eq 'timing.maxFrameGapMs' -and
        $_.failureArea -eq 'frame-pacing' -and
        $_.outcome -eq 'improved' -and
        $_.improvementCount -eq 1 -and
        ($_.caseIds -contains 'item-1/source-1')
    })) {
        throw 'Expected evaluate-candidate active gate to expose suite signal summaries.'
    }

    $candidateGateNextActions = @($candidateEvaluation.activeGate.nextActions)
    if ($candidateGateNextActions.Count -ne 1 -or
        $candidateGateNextActions[0].rank -ne 1 -or
        $candidateGateNextActions[0].action -ne 'accept-candidate' -or
        $candidateGateNextActions[0].risk -ne 'low' -or
        -not ($candidateGateNextActions[0].caseIds -contains 'item-1/source-1')) {
        throw 'Expected evaluate-candidate active gate to expose ranked suite next action.'
    }

    if ($null -eq $candidateEvaluation.baselineReportAnalysis -or
        $candidateEvaluation.baselineReportAnalysis.totalReportCount -ne 3 -or
        $candidateEvaluation.baselineReportAnalysis.analyzedReportCount -ne 0 -or
        $candidateEvaluation.baselineReportAnalysis.unavailableReportCount -ne 3 -or
        $candidateEvaluation.baselineReportAnalysis.blockedReportCount -ne 0) {
        throw 'Expected evaluate-candidate to summarize unavailable baseline report analysis for raw reports.'
    }

    if ($null -eq $candidateEvaluation.candidateReportAnalysis -or
        $candidateEvaluation.candidateReportAnalysis.totalReportCount -ne 3 -or
        $candidateEvaluation.candidateReportAnalysis.analyzedReportCount -ne 0 -or
        $candidateEvaluation.candidateReportAnalysis.unavailableReportCount -ne 3 -or
        $candidateEvaluation.candidateReportAnalysis.blockedReportCount -ne 0) {
        throw 'Expected evaluate-candidate to summarize unavailable candidate report analysis for raw reports.'
    }

    if (-not ($candidateEvaluation.candidateReportAnalysis.cases |
        Where-Object { $_.caseId -eq 'item-1/source-1' -and $_.status -eq 'unavailable' })) {
        throw 'Expected evaluate-candidate candidate report-analysis summary to include unavailable raw-report case.'
    }

    if ($null -eq $candidateEvaluation.evidenceGates -or $candidateEvaluation.evidenceGates.Count -ne 9) {
        throw 'Expected playback quality CLI evaluate-candidate to emit nine evidence gates.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'manifest' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate manifest evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'manifest-coverage' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate manifest coverage evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'baseline-report-set' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate baseline report-set evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'candidate-report-set' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate candidate report-set evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'baseline-report-analysis' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate baseline report-analysis evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'candidate-report-analysis' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate candidate report-analysis evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'baseline-playback-evidence' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate baseline playback evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'candidate-playback-evidence' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate candidate playback evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'suite' -and $_.status -eq 'pass' -and $_.action -eq 'accept-candidate' })) {
        throw 'Expected evaluate-candidate suite evidence gate to pass with accept-candidate action.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationMissingEnvironmentBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $candidateEvaluationMissingEnvironmentCandidateDir | Out-Null
    $baselineWithoutEnvironment = Get-Content -Raw -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') | ConvertFrom-Json
    $candidateWithoutEnvironment = Get-Content -Raw -LiteralPath (Join-Path $runIdCandidateDir 'candidate-renamed.json') | ConvertFrom-Json
    $baselineWithoutEnvironment.PSObject.Properties.Remove('environment')
    $candidateWithoutEnvironment.PSObject.Properties.Remove('environment')
    $baselineWithoutEnvironment | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $candidateEvaluationMissingEnvironmentBaselineDir 'baseline-a.json') -Encoding UTF8
    $candidateWithoutEnvironment | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $candidateEvaluationMissingEnvironmentCandidateDir 'candidate-a.json') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'error-baseline.json') -Destination (Join-Path $candidateEvaluationMissingEnvironmentBaselineDir 'error-baseline.json')
    Copy-Item -LiteralPath (Join-Path $runIdCandidateDir 'error-candidate.json') -Destination (Join-Path $candidateEvaluationMissingEnvironmentCandidateDir 'error-candidate.json')
    Copy-Item -LiteralPath $endOfStreamBaselinePath -Destination (Join-Path $candidateEvaluationMissingEnvironmentBaselineDir 'end-of-stream-baseline.json')
    Copy-Item -LiteralPath $endOfStreamCandidatePath -Destination (Join-Path $candidateEvaluationMissingEnvironmentCandidateDir 'end-of-stream-candidate.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $candidateEvaluationMissingEnvironmentBaselineDir `
            --candidate-dir $candidateEvaluationMissingEnvironmentCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationMissingEnvironmentComparisonsDir `
            --output $candidateEvaluationMissingEnvironmentPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject missing build identity evidence.'
        }
    }
    finally {
        Pop-Location
    }

    $missingEnvironmentEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationMissingEnvironmentPath | ConvertFrom-Json
    if ($missingEnvironmentEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected missing build identity evaluate-candidate output to collect comparable evidence.'
    }

    if ($missingEnvironmentEvaluation.decision -ne 'collect-comparable-evidence') {
        throw 'Expected missing build identity evaluate-candidate decision to collect comparable evidence.'
    }

    if ($missingEnvironmentEvaluation.baselineReportSetValidation.isValid -ne $false) {
        throw 'Expected missing build identity baseline report-set validation to fail.'
    }

    if ($missingEnvironmentEvaluation.candidateReportSetValidation.isValid -ne $false) {
        throw 'Expected missing build identity candidate report-set validation to fail.'
    }

    if (-not ($missingEnvironmentEvaluation.blockers -contains 'baseline-report-set.invalid') -or
        -not ($missingEnvironmentEvaluation.blockers -contains 'candidate-report-set.invalid')) {
        throw 'Expected missing build identity evaluate-candidate output to include report-set blockers.'
    }

    if (-not ($missingEnvironmentEvaluation.baselineReportSetValidation.errors | Where-Object {
        $_.code -eq 'report.environment.missing' -and
        $_.signal -eq 'environment.playerCoreVersion' -and
        $_.failureClass -eq 'insufficient instrumentation'
    })) {
        throw 'Expected missing build identity baseline report-set validation to require playerCoreVersion.'
    }

    if (-not ($missingEnvironmentEvaluation.baselineReportSetValidation.errors | Where-Object {
        $_.code -eq 'report.environment.missing' -and
        $_.signal -eq 'environment.sourceRevision' -and
        $_.failureClass -eq 'insufficient instrumentation'
    })) {
        throw 'Expected missing build identity baseline report-set validation to require sourceRevision.'
    }

    if ($null -eq $missingEnvironmentEvaluation.activeGate -or
        $missingEnvironmentEvaluation.activeGate.name -ne 'baseline-report-set' -or
        $missingEnvironmentEvaluation.activeGate.status -ne 'blocked' -or
        $missingEnvironmentEvaluation.activeGate.risk -ne 'high') {
        throw 'Expected missing build identity active gate to point at blocked baseline report-set.'
    }

    if (-not ($missingEnvironmentEvaluation.activeGate.signals -contains 'environment.playerCoreVersion') -or
        -not ($missingEnvironmentEvaluation.activeGate.signals -contains 'environment.sourceRevision')) {
        throw 'Expected missing build identity active gate to include player identity signals.'
    }

    if (-not ($missingEnvironmentEvaluation.activeGate.blockers -contains 'baseline-report-set.invalid')) {
        throw 'Expected missing build identity active gate to include baseline report-set blocker.'
    }

    if (-not ($missingEnvironmentEvaluation.activeGate.caseIds -contains 'item-1/source-1')) {
        throw 'Expected missing build identity active gate to include affected case id.'
    }

    $missingEnvironmentBaselineGate = $missingEnvironmentEvaluation.evidenceGates |
        Where-Object { $_.name -eq 'baseline-report-set' } |
        Select-Object -First 1
    if ($null -eq $missingEnvironmentBaselineGate -or $missingEnvironmentBaselineGate.status -ne 'blocked') {
        throw 'Expected missing build identity baseline report-set gate to be blocked.'
    }

    $missingEnvironmentCandidateGate = $missingEnvironmentEvaluation.evidenceGates |
        Where-Object { $_.name -eq 'candidate-report-set' } |
        Select-Object -First 1
    if ($null -eq $missingEnvironmentCandidateGate -or $missingEnvironmentCandidateGate.status -ne 'blocked') {
        throw 'Expected missing build identity candidate report-set gate to be blocked.'
    }

    if (-not ($missingEnvironmentEvaluation.evidenceGates | Where-Object { $_.name -eq 'suite' -and $_.status -eq 'skipped' })) {
        throw 'Expected missing build identity evaluate-candidate suite evidence gate to be skipped.'
    }

    if (Test-Path -LiteralPath $candidateEvaluationMissingEnvironmentComparisonsDir) {
        throw 'Expected missing build identity evaluate-candidate evidence to skip comparison output.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationPartialEnvironmentBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $candidateEvaluationPartialEnvironmentCandidateDir | Out-Null
    $partialEnvironmentBaseline = Get-Content -Raw -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') | ConvertFrom-Json
    $partialEnvironmentCandidate = Get-Content -Raw -LiteralPath (Join-Path $runIdCandidateDir 'candidate-renamed.json') | ConvertFrom-Json
    $partialEnvironmentCandidate.PSObject.Properties.Remove('environment')
    $partialEnvironmentBaseline | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $candidateEvaluationPartialEnvironmentBaselineDir 'baseline-a.json') -Encoding UTF8
    $partialEnvironmentCandidate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $candidateEvaluationPartialEnvironmentCandidateDir 'candidate-a.json') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'error-baseline.json') -Destination (Join-Path $candidateEvaluationPartialEnvironmentBaselineDir 'error-baseline.json')
    Copy-Item -LiteralPath (Join-Path $runIdCandidateDir 'error-candidate.json') -Destination (Join-Path $candidateEvaluationPartialEnvironmentCandidateDir 'error-candidate.json')
    Copy-Item -LiteralPath $endOfStreamBaselinePath -Destination (Join-Path $candidateEvaluationPartialEnvironmentBaselineDir 'end-of-stream-baseline.json')
    Copy-Item -LiteralPath $endOfStreamCandidatePath -Destination (Join-Path $candidateEvaluationPartialEnvironmentCandidateDir 'end-of-stream-candidate.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $candidateEvaluationPartialEnvironmentBaselineDir `
            --candidate-dir $candidateEvaluationPartialEnvironmentCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationPartialEnvironmentComparisonsDir `
            --output $candidateEvaluationPartialEnvironmentPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject partial build identity evidence.'
        }
    }
    finally {
        Pop-Location
    }

    $partialEnvironmentEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationPartialEnvironmentPath | ConvertFrom-Json
    if ($partialEnvironmentEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected partial build identity evaluate-candidate output to collect comparable evidence.'
    }

    if ($partialEnvironmentEvaluation.baselineReportSetValidation.isValid -ne $true) {
        throw 'Expected partial build identity baseline report-set validation to pass.'
    }

    if ($partialEnvironmentEvaluation.candidateReportSetValidation.isValid -ne $false) {
        throw 'Expected partial build identity candidate report-set validation to fail.'
    }

    if (-not ($partialEnvironmentEvaluation.blockers -contains 'candidate-report-set.invalid')) {
        throw 'Expected partial build identity evaluate-candidate output to include candidate report-set blocker.'
    }

    if (-not ($partialEnvironmentEvaluation.candidateReportSetValidation.errors | Where-Object {
        $_.code -eq 'report.environment.missing' -and
        $_.signal -eq 'environment.playerCoreVersion' -and
        $_.failureClass -eq 'insufficient instrumentation'
    })) {
        throw 'Expected partial build identity candidate report-set validation to require playerCoreVersion.'
    }

    if (-not ($partialEnvironmentEvaluation.candidateReportSetValidation.errors | Where-Object {
        $_.code -eq 'report.environment.missing' -and
        $_.signal -eq 'environment.sourceRevision' -and
        $_.failureClass -eq 'insufficient instrumentation'
    })) {
        throw 'Expected partial build identity candidate report-set validation to require sourceRevision.'
    }

    if ($null -eq $partialEnvironmentEvaluation.activeGate -or
        $partialEnvironmentEvaluation.activeGate.name -ne 'candidate-report-set' -or
        $partialEnvironmentEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected partial build identity active gate to point at blocked candidate report-set.'
    }

    if (-not ($partialEnvironmentEvaluation.activeGate.signals -contains 'environment.playerCoreVersion') -or
        -not ($partialEnvironmentEvaluation.activeGate.signals -contains 'environment.sourceRevision')) {
        throw 'Expected partial build identity active gate to include player identity signals.'
    }

    if (-not ($partialEnvironmentEvaluation.activeGate.blockers -contains 'candidate-report-set.invalid')) {
        throw 'Expected partial build identity active gate to include candidate report-set blocker.'
    }

    if (-not ($partialEnvironmentEvaluation.activeGate.caseIds -contains 'item-1/source-1')) {
        throw 'Expected partial build identity active gate to include affected case id.'
    }

    if (-not ($partialEnvironmentEvaluation.evidenceGates | Where-Object { $_.name -eq 'suite' -and $_.status -eq 'skipped' })) {
        throw 'Expected partial build identity evaluate-candidate suite evidence gate to be skipped.'
    }

    if (Test-Path -LiteralPath $candidateEvaluationPartialEnvironmentComparisonsDir) {
        throw 'Expected partial build identity evaluate-candidate evidence to skip comparison output.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationSameBuildBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $candidateEvaluationSameBuildCandidateDir | Out-Null
    $sameBuildBaseline = Get-Content -Raw -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') | ConvertFrom-Json
    $sameBuildCandidate = Get-Content -Raw -LiteralPath (Join-Path $runIdCandidateDir 'candidate-renamed.json') | ConvertFrom-Json
    $sameBuildCandidate.environment.playerCoreVersion = $sameBuildBaseline.environment.playerCoreVersion
    $sameBuildCandidate.environment.sourceRevision = $sameBuildBaseline.environment.sourceRevision
    $sameBuildCandidate.environment.buildConfiguration = $sameBuildBaseline.environment.buildConfiguration
    $sameBuildBaseline | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $candidateEvaluationSameBuildBaselineDir 'baseline-a.json') -Encoding UTF8
    $sameBuildCandidate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $candidateEvaluationSameBuildCandidateDir 'candidate-a.json') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'error-baseline.json') -Destination (Join-Path $candidateEvaluationSameBuildBaselineDir 'error-baseline.json')
    Copy-Item -LiteralPath (Join-Path $runIdCandidateDir 'error-candidate.json') -Destination (Join-Path $candidateEvaluationSameBuildCandidateDir 'error-candidate.json')
    Copy-Item -LiteralPath $endOfStreamBaselinePath -Destination (Join-Path $candidateEvaluationSameBuildBaselineDir 'end-of-stream-baseline.json')
    Copy-Item -LiteralPath $endOfStreamBaselinePath -Destination (Join-Path $candidateEvaluationSameBuildCandidateDir 'end-of-stream-candidate.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $candidateEvaluationSameBuildBaselineDir `
            --candidate-dir $candidateEvaluationSameBuildCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationSameBuildComparisonsDir `
            --output $candidateEvaluationSameBuildPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject same-build identity evidence.'
        }
    }
    finally {
        Pop-Location
    }

    $sameBuildEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationSameBuildPath | ConvertFrom-Json
    if ($sameBuildEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected same-build evaluate-candidate output to collect comparable evidence.'
    }

    if ($null -eq $sameBuildEvaluation.activeGate -or
        $sameBuildEvaluation.activeGate.name -ne 'suite' -or
        $sameBuildEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected same-build active gate to point at blocked suite.'
    }

    if ($null -eq $sameBuildEvaluation.activeGate.environment -or
        $sameBuildEvaluation.activeGate.environment.sameBuildCount -ne 2) {
        throw 'Expected same-build active gate to expose environment same-build count.'
    }

    if (-not ($sameBuildEvaluation.activeGate.blockers -contains 'suite.environment-same-build')) {
        throw 'Expected same-build active gate to include environment same-build blocker.'
    }

    if (-not ($sameBuildEvaluation.activeGate.blockers -contains 'comparison.environment-same-build')) {
        throw 'Expected same-build active gate to include comparison environment blocker.'
    }

    if (-not ($sameBuildEvaluation.activeGate.targetCaseIds -contains 'item-1/source-1')) {
        throw 'Expected same-build active gate to include target case id.'
    }

    $sameBuildComparisonPath = Join-Path $candidateEvaluationSameBuildComparisonsDir 'item-1\source-1.json'
    if (-not (Test-Path -LiteralPath $sameBuildComparisonPath)) {
        throw 'Expected same-build evaluate-candidate to write per-case comparison output.'
    }

    $sameBuildComparison = Get-Content -Raw -LiteralPath $sameBuildComparisonPath | ConvertFrom-Json
    if (-not ($sameBuildComparison.optimization.blockers -contains 'comparison.environment-same-build')) {
        throw 'Expected same-build comparison output to include machine-readable environment blocker.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "item-1/source-1",
      "uri": "https://example.invalid/item-1/source-1.mp4",
      "tier": 2,
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
      "purpose": [
        "frame-pacing"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Sdr"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $candidateEvaluationNarrowManifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationNarrowManifestPath `
            --baseline-dir $runIdBaselineDir `
            --candidate-dir $runIdCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationNarrowCoverageComparisonsDir `
            --output $candidateEvaluationNarrowCoveragePath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject incomplete manifest coverage.'
        }
    }
    finally {
        Pop-Location
    }

    $narrowCoverageEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationNarrowCoveragePath | ConvertFrom-Json
    if ($narrowCoverageEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected incomplete manifest coverage evaluation to collect comparable evidence.'
    }

    if (-not ($narrowCoverageEvaluation.blockers -contains 'manifest-coverage.incomplete')) {
        throw 'Expected incomplete manifest coverage evaluation to include manifest coverage blocker.'
    }

    if ($null -eq $narrowCoverageEvaluation.activeGate -or
        $narrowCoverageEvaluation.activeGate.name -ne 'manifest-coverage' -or
        $narrowCoverageEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected incomplete manifest coverage active gate to block manifest coverage.'
    }

    if (-not ($narrowCoverageEvaluation.activeGate.signals -contains 'sdr-smoke')) {
        throw 'Expected incomplete manifest coverage active gate to include missing purpose signal.'
    }

    if (-not ($narrowCoverageEvaluation.activeGate.suggestedNextActions -contains 'Add reference cases for missing playback quality purposes before relying on broad Core candidate evaluation.')) {
        throw 'Expected incomplete manifest coverage active gate to include suggested next action.'
    }

    $narrowCoverageGateNextActions = @($narrowCoverageEvaluation.activeGate.nextActions)
    if ($narrowCoverageGateNextActions.Count -ne 1 -or
        $narrowCoverageGateNextActions[0].rank -ne 1 -or
        $narrowCoverageGateNextActions[0].action -ne 'collect-comparable-evidence' -or
        $narrowCoverageGateNextActions[0].risk -ne 'high' -or
        -not ($narrowCoverageGateNextActions[0].signals -contains 'sdr-smoke') -or
        -not ($narrowCoverageGateNextActions[0].blockers -contains 'manifest-coverage.incomplete') -or
        -not ($narrowCoverageGateNextActions[0].codeTargets -contains 'src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportMapper.cs')) {
        throw 'Expected incomplete manifest coverage active gate to expose ranked next action.'
    }

    if (-not ($narrowCoverageEvaluation.evidenceGates | Where-Object { $_.name -eq 'suite' -and $_.status -eq 'skipped' })) {
        throw 'Expected incomplete manifest coverage evidence to skip suite comparison.'
    }

    if (Test-Path -LiteralPath $candidateEvaluationNarrowCoverageComparisonsDir) {
        throw 'Expected incomplete manifest coverage evidence to skip comparison output.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationEmptyAnalysisBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $candidateEvaluationEmptyAnalysisCandidateDir | Out-Null
    @'
{
  "report": {
    "runId": "item-1/source-1",
    "metricVersion": "software-quality-v1",
    "result": "fail",
    "environment": {
      "playerCoreVersion": "smoke-core",
      "sourceRevision": "smoke-empty-analysis-baseline-revision",
      "buildConfiguration": "Debug"
    },
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "hasDirectStreamUrl": true,
      "directStreamProtocol": "https",
      "width": 3840,
      "height": 2160,
      "frameRate": 23.976,
      "hdrKind": "Sdr"
    },
    "lifecycle": {
      "events": [
        { "operation": "load", "status": "observed" },
        { "operation": "play", "status": "observed" },
        { "operation": "pause", "status": "observed" },
        { "operation": "resume", "status": "observed" },
        { "operation": "seek", "status": "observed" },
        { "operation": "endOfStream", "status": "observed" },
        { "operation": "stop", "status": "observed" }
      ]
    },
    "runtimeMetrics": {
      "status": "captured",
      "providerStatus": "native-winrt:returned-snapshot",
      "reason": "Runtime metrics snapshot contains playback sample evidence.",
      "hasSnapshot": true,
      "hasPlaybackSample": true
    },
    "tracks": {
      "videoTrackCount": 1,
      "audioTrackCount": 1,
      "subtitleTrackCount": 1,
      "isSubtitleDisabled": true,
      "video": [
        {
          "index": 0,
          "kind": "Video",
          "codec": "hevc",
          "language": "und",
          "isExternal": false,
          "isDefault": true,
          "isForced": false
        }
      ],
      "audio": [
        {
          "index": 1,
          "kind": "Audio",
          "codec": "aac",
          "language": "eng",
          "channelLayout": "2.0",
          "channels": 2,
          "isExternal": false,
          "isDefault": true,
          "isForced": false
        }
      ],
      "subtitles": [
        {
          "index": 2,
          "kind": "Subtitle",
          "codec": "srt",
          "language": "eng",
          "isExternal": false,
          "isDefault": false,
          "isForced": false
        }
      ]
    },
    "position": {
      "requestedStartPositionTicks": 600000000,
      "seekTargetPositionTicks": 600000000,
      "actualPositionTicks": 600000000,
      "seekPositionErrorMs": 0
    },
    "timing": {
      "renderPasses": 1440,
      "renderedVideoFrames": 1440,
      "expectedFrameDurationMs": 41.708,
      "renderIntervalMsP95": 43.0,
      "renderIntervalMsP99": 45.0,
      "maxFrameGapMs": 180.0,
      "framePacingSourceFrameRate": 23.976,
      "lateFrameDropToleranceMs": 104.271
    },
    "sync": {
      "audioVideoDriftMsP95": 20.0
    },
    "buffers": {
      "submittedAudioFrames": 48000,
      "queuedAudioBuffers": 4,
      "videoStarvedPasses": 0,
      "audioStarvedPasses": 0
    },
    "colorPipeline": {
      "conversionStatus": "validated",
      "forceSdrOutput": true
    },
    "display": {
      "refreshRateHz": 23.976
    },
    "checks": [
      {
        "name": "MaxFrameGapMs",
        "signal": "timing.maxFrameGapMs",
        "status": "fail",
        "failureArea": "frame-pacing",
        "expected": "105.000",
        "actual": "180.000"
      }
    ]
  },
  "modelAnalysis": {}
}
'@ | Set-Content -LiteralPath (Join-Path $candidateEvaluationEmptyAnalysisBaselineDir 'baseline-empty-analysis.json') -Encoding UTF8
    @'
{
  "report": {
    "runId": "item-1/source-1",
    "metricVersion": "software-quality-v1",
    "result": "fail",
    "environment": {
      "playerCoreVersion": "smoke-core",
      "sourceRevision": "smoke-empty-analysis-candidate-revision",
      "buildConfiguration": "Debug"
    },
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "hasDirectStreamUrl": true,
      "directStreamProtocol": "https",
      "width": 3840,
      "height": 2160,
      "frameRate": 23.976,
      "hdrKind": "Sdr"
    },
    "lifecycle": {
      "events": [
        { "operation": "load", "status": "observed" },
        { "operation": "play", "status": "observed" },
        { "operation": "pause", "status": "observed" },
        { "operation": "resume", "status": "observed" },
        { "operation": "seek", "status": "observed" },
        { "operation": "endOfStream", "status": "observed" },
        { "operation": "stop", "status": "observed" }
      ]
    },
    "runtimeMetrics": {
      "status": "captured",
      "providerStatus": "native-winrt:returned-snapshot",
      "reason": "Runtime metrics snapshot contains playback sample evidence.",
      "hasSnapshot": true,
      "hasPlaybackSample": true
    },
    "tracks": {
      "videoTrackCount": 1,
      "audioTrackCount": 1,
      "subtitleTrackCount": 1,
      "isSubtitleDisabled": true,
      "video": [
        {
          "index": 0,
          "kind": "Video",
          "codec": "hevc",
          "language": "und",
          "isExternal": false,
          "isDefault": true,
          "isForced": false
        }
      ],
      "audio": [
        {
          "index": 1,
          "kind": "Audio",
          "codec": "aac",
          "language": "eng",
          "channelLayout": "2.0",
          "channels": 2,
          "isExternal": false,
          "isDefault": true,
          "isForced": false
        }
      ],
      "subtitles": [
        {
          "index": 2,
          "kind": "Subtitle",
          "codec": "srt",
          "language": "eng",
          "isExternal": false,
          "isDefault": false,
          "isForced": false
        }
      ]
    },
    "position": {
      "requestedStartPositionTicks": 600000000,
      "seekTargetPositionTicks": 600000000,
      "actualPositionTicks": 600000000,
      "seekPositionErrorMs": 0
    },
    "timing": {
      "renderPasses": 1440,
      "renderedVideoFrames": 1440,
      "expectedFrameDurationMs": 41.708,
      "renderIntervalMsP95": 42.0,
      "renderIntervalMsP99": 44.0,
      "maxFrameGapMs": 120.0,
      "framePacingSourceFrameRate": 23.976,
      "lateFrameDropToleranceMs": 104.271
    },
    "sync": {
      "audioVideoDriftMsP95": 18.0
    },
    "buffers": {
      "submittedAudioFrames": 48000,
      "queuedAudioBuffers": 4,
      "videoStarvedPasses": 0,
      "audioStarvedPasses": 0
    },
    "colorPipeline": {
      "conversionStatus": "validated",
      "forceSdrOutput": true
    },
    "display": {
      "refreshRateHz": 23.976
    },
    "checks": [
      {
        "name": "MaxFrameGapMs",
        "signal": "timing.maxFrameGapMs",
        "status": "fail",
        "failureArea": "frame-pacing",
        "expected": "105.000",
        "actual": "120.000"
      }
    ]
  },
  "modelAnalysis": {}
}
'@ | Set-Content -LiteralPath (Join-Path $candidateEvaluationEmptyAnalysisCandidateDir 'candidate-empty-analysis.json') -Encoding UTF8
    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $candidateEvaluationEmptyAnalysisBaselineDir 'baseline-empty-analysis.json') `
        -Locator 'https://example.invalid/item-1/source-1.mp4' `
        -AttemptId 'empty-analysis-baseline-attempt' `
        -Status 'completed' `
        -Scenario 'timeline' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true
    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $candidateEvaluationEmptyAnalysisCandidateDir 'candidate-empty-analysis.json') `
        -Locator 'https://example.invalid/item-1/source-1.mp4' `
        -AttemptId 'empty-analysis-candidate-attempt' `
        -Status 'completed' `
        -Scenario 'timeline' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'error-baseline.json') -Destination (Join-Path $candidateEvaluationEmptyAnalysisBaselineDir 'error-baseline.json')
    Copy-Item -LiteralPath (Join-Path $runIdCandidateDir 'error-candidate.json') -Destination (Join-Path $candidateEvaluationEmptyAnalysisCandidateDir 'error-candidate.json')
    Copy-Item -LiteralPath $endOfStreamBaselinePath -Destination (Join-Path $candidateEvaluationEmptyAnalysisBaselineDir 'end-of-stream-baseline.json')
    Copy-Item -LiteralPath $endOfStreamCandidatePath -Destination (Join-Path $candidateEvaluationEmptyAnalysisCandidateDir 'end-of-stream-candidate.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $candidateEvaluationEmptyAnalysisBaselineDir `
            --candidate-dir $candidateEvaluationEmptyAnalysisCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationEmptyAnalysisComparisonsDir `
            --output $candidateEvaluationEmptyAnalysisPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to refresh and reject incomplete model analysis.'
        }
    }
    finally {
        Pop-Location
    }

    $emptyAnalysisEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationEmptyAnalysisPath | ConvertFrom-Json
    if ($null -eq $emptyAnalysisEvaluation.activeGate -or
        $emptyAnalysisEvaluation.activeGate.name -ne 'baseline-report-analysis' -or
        $emptyAnalysisEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected incomplete model analysis evaluate-candidate active gate to block baseline report-analysis.'
    }

    if ($null -eq $emptyAnalysisEvaluation.activeGate.confidence -or
        $emptyAnalysisEvaluation.activeGate.confidence.level -ne 'weak') {
        throw 'Expected blocked report-analysis active gate to expose weak evidence confidence.'
    }

    if ($null -eq $emptyAnalysisEvaluation.baselineReportAnalysis -or
        $emptyAnalysisEvaluation.baselineReportAnalysis.analyzedReportCount -ne 1 -or
        $emptyAnalysisEvaluation.baselineReportAnalysis.blockedReportCount -ne 1) {
        throw 'Expected incomplete baseline model analysis to be refreshed and blocked.'
    }

    if (-not ($emptyAnalysisEvaluation.baselineReportAnalysis.blockers -contains 'missingEvidence')) {
        throw 'Expected refreshed incomplete baseline analysis to expose missing evidence blocker.'
    }

    if (-not ($emptyAnalysisEvaluation.baselineReportAnalysis.targetCaseIds -contains 'item-1/source-1')) {
        throw 'Expected refreshed incomplete baseline analysis to expose target case id.'
    }

    if (Test-Path -LiteralPath $candidateEvaluationEmptyAnalysisComparisonsDir) {
        throw 'Expected incomplete model analysis evaluate-candidate to skip comparison output.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationInvalidCandidateDir | Out-Null
    @'
{
  "runId": "item-1/source-1",
  "metricVersion": "software-quality-v1",
  "result": "fail",
  "environment": {
    "playerCoreVersion": "smoke-core",
    "sourceRevision": "smoke-invalid-candidate-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Hdr10"
  },
  "tracks": {
    "videoTrackCount": 1,
    "audioTrackCount": 1,
    "subtitleTrackCount": 1,
    "isSubtitleDisabled": true,
    "video": [
      {
        "index": 0,
        "kind": "Video",
        "codec": "hevc",
        "language": "und",
        "isExternal": false,
        "isDefault": true,
        "isForced": false
      }
    ],
    "audio": [
      {
        "index": 1,
        "kind": "Audio",
        "codec": "aac",
        "language": "eng",
        "channelLayout": "2.0",
        "channels": 2,
        "isExternal": false,
        "isDefault": true,
        "isForced": false
      }
    ],
    "subtitles": [
      {
        "index": 2,
        "kind": "Subtitle",
        "codec": "srt",
        "language": "eng",
        "isExternal": false,
        "isDefault": false,
        "isForced": false
      }
    ]
  },
  "position": {
    "requestedStartPositionTicks": 600000000,
    "seekTargetPositionTicks": 600000000,
    "actualPositionTicks": 600000000,
    "seekPositionErrorMs": 0
  },
  "checks": [
    {
      "name": "MaxFrameGapMs",
      "signal": "timing.maxFrameGapMs",
      "status": "fail",
      "failureArea": "frame-pacing",
      "expected": "105.000",
      "actual": "120.000"
    }
  ]
}
'@ | Set-Content -LiteralPath (Join-Path $candidateEvaluationInvalidCandidateDir 'candidate-wrong-source.json') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $runIdCandidateDir 'error-candidate.json') -Destination (Join-Path $candidateEvaluationInvalidCandidateDir 'error-candidate.json')
    Copy-Item -LiteralPath $endOfStreamCandidatePath -Destination (Join-Path $candidateEvaluationInvalidCandidateDir 'end-of-stream-candidate.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $runIdBaselineDir `
            --candidate-dir $candidateEvaluationInvalidCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationInvalidComparisonsDir `
            --output $candidateEvaluationInvalidPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject invalid candidate evidence.'
        }
    }
    finally {
        Pop-Location
    }

    $invalidCandidateEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationInvalidPath | ConvertFrom-Json
    if ($invalidCandidateEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected invalid evaluate-candidate output to collect comparable evidence.'
    }

    if ($invalidCandidateEvaluation.candidateReportSetValidation.isValid -ne $false) {
        throw 'Expected invalid evaluate-candidate candidate report set validation to fail.'
    }

    if (-not ($invalidCandidateEvaluation.blockers -contains 'candidate-report-set.invalid')) {
        throw 'Expected invalid evaluate-candidate output to include candidate report set blocker.'
    }

    if ($null -eq $invalidCandidateEvaluation.activeGate -or
        $invalidCandidateEvaluation.activeGate.name -ne 'candidate-report-set' -or
        $invalidCandidateEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected invalid evaluate-candidate active gate to point at blocked candidate report-set gate.'
    }

    if (Test-Path -LiteralPath $candidateEvaluationInvalidComparisonsDir) {
        throw 'Expected invalid evaluate-candidate evidence to skip comparison output.'
    }

    $invalidCandidateGate = $invalidCandidateEvaluation.evidenceGates |
        Where-Object { $_.name -eq 'candidate-report-set' } |
        Select-Object -First 1
    if ($null -eq $invalidCandidateGate -or $invalidCandidateGate.status -ne 'blocked') {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to be blocked.'
    }

    if (-not ($invalidCandidateGate.blockers -contains 'candidate-report-set.invalid')) {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to include blocker.'
    }

    if (-not ($invalidCandidateGate.signals -contains 'source.hdrKind')) {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to include mismatched source signal.'
    }

    if (-not ($invalidCandidateGate.codeTargets -contains 'src/NoiraPlayer.Core/Playback/HdrPlaybackProfileClassifier.cs')) {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to include source classification code target.'
    }

    if (-not ($invalidCandidateGate.suggestedNextActions -contains 'Verify media source selection, codec support, HDR/Dolby Vision classification, and fallback policy before tuning playback timing.')) {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to include suggested next action.'
    }

    if (-not ($invalidCandidateGate.caseIds -contains 'item-1/source-1')) {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to include affected case id.'
    }

    if (-not ($invalidCandidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'suite' -and $_.status -eq 'skipped' })) {
        throw 'Expected invalid evaluate-candidate suite evidence gate to be skipped.'
    }

    New-Item -ItemType Directory -Path $baselineEvaluationBlockedAnalysisBaselineDir | Out-Null
    @'
{
  "report": {
    "runId": "item-1/source-1",
    "metricVersion": "software-quality-v1",
    "result": "fail",
    "environment": {
      "playerCoreVersion": "smoke-core",
      "sourceRevision": "smoke-blocked-analysis-baseline-revision",
      "buildConfiguration": "Debug"
    },
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "hasDirectStreamUrl": true,
      "directStreamProtocol": "https",
      "width": 3840,
      "height": 2160,
      "frameRate": 23.976,
      "hdrKind": "Sdr"
    },
    "lifecycle": {
      "events": [
        { "operation": "load", "status": "observed" },
        { "operation": "play", "status": "observed" },
        { "operation": "pause", "status": "observed" },
        { "operation": "resume", "status": "observed" },
        { "operation": "seek", "status": "observed" },
        { "operation": "endOfStream", "status": "observed" },
        { "operation": "stop", "status": "observed" }
      ]
    },
    "runtimeMetrics": {
      "status": "captured",
      "providerStatus": "native-winrt:returned-snapshot",
      "reason": "Runtime metrics snapshot contains playback sample evidence.",
      "hasSnapshot": true,
      "hasPlaybackSample": true
    },
    "tracks": {
      "videoTrackCount": 1,
      "audioTrackCount": 1,
      "subtitleTrackCount": 1,
      "isSubtitleDisabled": true,
      "video": [
        {
          "index": 0,
          "kind": "Video",
          "codec": "hevc",
          "language": "und",
          "isExternal": false,
          "isDefault": true,
          "isForced": false
        }
      ],
      "audio": [
        {
          "index": 1,
          "kind": "Audio",
          "codec": "aac",
          "language": "eng",
          "channelLayout": "2.0",
          "channels": 2,
          "isExternal": false,
          "isDefault": true,
          "isForced": false
        }
      ],
      "subtitles": [
        {
          "index": 2,
          "kind": "Subtitle",
          "codec": "srt",
          "language": "eng",
          "isExternal": false,
          "isDefault": false,
          "isForced": false
        }
      ]
    },
    "position": {
      "requestedStartPositionTicks": 600000000,
      "seekTargetPositionTicks": 600000000,
      "actualPositionTicks": 600000000,
      "seekPositionErrorMs": 0
    },
    "timing": {
      "renderPasses": 1440,
      "renderedVideoFrames": 1440,
      "expectedFrameDurationMs": 41.708,
      "renderIntervalMsP95": 43.0,
      "renderIntervalMsP99": 45.0,
      "maxFrameGapMs": 180.0,
      "framePacingSourceFrameRate": 23.976,
      "lateFrameDropToleranceMs": 104.271
    },
    "sync": {
      "audioVideoDriftMsP95": 20.0
    },
    "buffers": {
      "submittedAudioFrames": 48000,
      "queuedAudioBuffers": 4,
      "videoStarvedPasses": 0,
      "audioStarvedPasses": 0
    },
    "colorPipeline": {
      "conversionStatus": "validated",
      "forceSdrOutput": true
    },
    "display": {
      "refreshRateHz": 23.976
    },
    "checks": [
      {
        "name": "RenderedVideoFrames",
        "signal": "timing.renderedVideoFrames",
        "status": "fail",
        "failureArea": "startup",
        "expected": "1",
        "actual": "0"
      }
    ]
  },
  "modelAnalysis": {
    "analyzerVersion": 5,
    "runId": "item-1/source-1",
    "result": "fail",
    "suggestedNextAction": "Collect a longer playback sample before optimizing playback Core.",
    "triageSteps": [
      {
        "rank": 1,
        "kind": "blocker",
        "failureArea": "startup",
        "suggestedAction": "Collect enough rendered-frame and startup readiness evidence before tuning playback Core behavior.",
        "signals": [
          "sample.status"
        ],
        "codeTargets": [
          "src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportComposer.cs"
        ]
      }
    ],
    "optimizationGate": {
      "status": "blocked",
      "canOptimizePlaybackCore": false,
      "blockers": [
        "sample.insufficient"
      ],
      "blockerSignals": [
        "sample.status"
      ],
      "targetFailureAreas": []
    }
  }
}
'@ | Set-Content -LiteralPath (Join-Path $baselineEvaluationBlockedAnalysisBaselineDir 'baseline-blocked-analysis.json') -Encoding UTF8
    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $baselineEvaluationBlockedAnalysisBaselineDir 'baseline-blocked-analysis.json') `
        -Locator 'https://example.invalid/item-1/source-1.mp4' `
        -AttemptId 'blocked-analysis-baseline-attempt' `
        -Status 'completed' `
        -Scenario 'timeline' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'error-baseline.json') -Destination (Join-Path $baselineEvaluationBlockedAnalysisBaselineDir 'error-baseline.json')
    Copy-Item -LiteralPath $endOfStreamBaselinePath -Destination (Join-Path $baselineEvaluationBlockedAnalysisBaselineDir 'end-of-stream-baseline.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $baselineEvaluationBlockedAnalysisBaselineDir `
            --candidate-dir $runIdCandidateDir `
            --match-by run-id `
            --comparisons-dir $baselineEvaluationBlockedAnalysisComparisonsDir `
            --output $baselineEvaluationBlockedAnalysisPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject blocked baseline report analysis.'
        }
    }
    finally {
        Pop-Location
    }

    $blockedBaselineAnalysisEvaluation = Get-Content -Raw -LiteralPath $baselineEvaluationBlockedAnalysisPath | ConvertFrom-Json
    if ($blockedBaselineAnalysisEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected blocked baseline report-analysis output to collect comparable evidence.'
    }

    if ($null -eq $blockedBaselineAnalysisEvaluation.activeGate -or
        $blockedBaselineAnalysisEvaluation.activeGate.name -ne 'baseline-report-analysis' -or
        $blockedBaselineAnalysisEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected blocked baseline report-analysis active gate to point at baseline report-analysis gate.'
    }

    if (-not ($blockedBaselineAnalysisEvaluation.blockers -contains 'baseline-report-analysis.blocked')) {
        throw 'Expected blocked baseline report-analysis output to include baseline report-analysis blocker.'
    }

    if ($null -eq $blockedBaselineAnalysisEvaluation.baselineReportAnalysis -or
        $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.totalReportCount -ne 3 -or
        $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.analyzedReportCount -ne 1 -or
        $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.unavailableReportCount -ne 2 -or
        $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.blockedReportCount -ne 1) {
        throw 'Expected blocked baseline report-analysis output to summarize analyzed blocked baseline report.'
    }

    $blockedBaselineAnalysisCase = $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.cases |
        Where-Object { $_.caseId -eq 'item-1/source-1' } |
        Select-Object -First 1
    if ($null -eq $blockedBaselineAnalysisCase -or $blockedBaselineAnalysisCase.status -ne 'blocked') {
        throw 'Expected blocked baseline report-analysis summary to include blocked baseline case.'
    }

    if (-not ($blockedBaselineAnalysisCase.blockers -contains 'sample.insufficient')) {
        throw 'Expected blocked baseline report-analysis summary case to include model analysis blocker.'
    }

    if (-not ($blockedBaselineAnalysisCase.signals -contains 'sample.status')) {
        throw 'Expected blocked baseline report-analysis summary case to include model analysis blocker signal.'
    }

    if (Test-Path -LiteralPath $baselineEvaluationBlockedAnalysisComparisonsDir) {
        throw 'Expected blocked baseline report-analysis evidence to skip comparison output.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationBlockedAnalysisCandidateDir | Out-Null
    @'
{
  "report": {
    "runId": "item-1/source-1",
    "metricVersion": "software-quality-v1",
    "result": "fail",
    "environment": {
      "playerCoreVersion": "smoke-core",
      "sourceRevision": "smoke-blocked-analysis-candidate-revision",
      "buildConfiguration": "Debug"
    },
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "hasDirectStreamUrl": true,
      "directStreamProtocol": "https",
      "width": 3840,
      "height": 2160,
      "frameRate": 23.976,
      "hdrKind": "Sdr"
    },
    "lifecycle": {
      "events": [
        { "operation": "load", "status": "observed" },
        { "operation": "play", "status": "observed" },
        { "operation": "pause", "status": "observed" },
        { "operation": "resume", "status": "observed" },
        { "operation": "seek", "status": "observed" },
        { "operation": "endOfStream", "status": "observed" },
        { "operation": "stop", "status": "observed" }
      ]
    },
    "runtimeMetrics": {
      "status": "captured",
      "providerStatus": "native-winrt:returned-snapshot",
      "reason": "Runtime metrics snapshot contains playback sample evidence.",
      "hasSnapshot": true,
      "hasPlaybackSample": true
    },
    "tracks": {
      "videoTrackCount": 1,
      "audioTrackCount": 1,
      "subtitleTrackCount": 1,
      "isSubtitleDisabled": true,
      "video": [
        {
          "index": 0,
          "kind": "Video",
          "codec": "hevc",
          "language": "und",
          "isExternal": false,
          "isDefault": true,
          "isForced": false
        }
      ],
      "audio": [
        {
          "index": 1,
          "kind": "Audio",
          "codec": "aac",
          "language": "eng",
          "channelLayout": "2.0",
          "channels": 2,
          "isExternal": false,
          "isDefault": true,
          "isForced": false
        }
      ],
      "subtitles": [
        {
          "index": 2,
          "kind": "Subtitle",
          "codec": "srt",
          "language": "eng",
          "isExternal": false,
          "isDefault": false,
          "isForced": false
        }
      ]
    },
    "position": {
      "requestedStartPositionTicks": 600000000,
      "seekTargetPositionTicks": 600000000,
      "actualPositionTicks": 600000000,
      "seekPositionErrorMs": 0
    },
    "timing": {
      "renderPasses": 1440,
      "renderedVideoFrames": 1440,
      "expectedFrameDurationMs": 41.708,
      "renderIntervalMsP95": 42.0,
      "renderIntervalMsP99": 44.0,
      "maxFrameGapMs": 120.0,
      "framePacingSourceFrameRate": 23.976,
      "lateFrameDropToleranceMs": 104.271
    },
    "sync": {
      "audioVideoDriftMsP95": 18.0
    },
    "buffers": {
      "submittedAudioFrames": 48000,
      "queuedAudioBuffers": 4,
      "videoStarvedPasses": 0,
      "audioStarvedPasses": 0
    },
    "colorPipeline": {
      "conversionStatus": "validated",
      "forceSdrOutput": true
    },
    "display": {
      "refreshRateHz": 23.976
    },
    "checks": [
      {
        "name": "MaxFrameGapMs",
        "signal": "timing.maxFrameGapMs",
        "status": "fail",
        "failureArea": "frame-pacing",
        "expected": "105.000",
        "actual": "120.000"
      }
    ]
  },
  "modelAnalysis": {
    "analyzerVersion": 5,
    "runId": "item-1/source-1",
    "result": "fail",
    "suggestedNextAction": "Collect comparable source metadata before optimizing playback Core.",
    "triageSteps": [
      {
        "rank": 1,
        "kind": "blocker",
        "failureArea": "unsupported-source",
        "suggestedAction": "Verify media source selection, codec support, HDR/Dolby Vision classification, and fallback policy before tuning playback timing.",
        "signals": [
          "source.hdrKind"
        ],
        "codeTargets": [
          "src/NoiraPlayer.Core/Playback/HdrPlaybackProfileClassifier.cs"
        ]
      }
    ],
    "optimizationGate": {
      "status": "blocked",
      "canOptimizePlaybackCore": false,
      "blockers": [
        "source.mismatch"
      ],
      "blockerSignals": [
        "source.hdrKind"
      ],
      "targetFailureAreas": []
    }
  }
}
'@ | Set-Content -LiteralPath (Join-Path $candidateEvaluationBlockedAnalysisCandidateDir 'candidate-blocked-analysis.json') -Encoding UTF8
    Set-SmokeNativeExecutionEvidence `
        -Path (Join-Path $candidateEvaluationBlockedAnalysisCandidateDir 'candidate-blocked-analysis.json') `
        -Locator 'https://example.invalid/item-1/source-1.mp4' `
        -AttemptId 'blocked-analysis-candidate-attempt' `
        -Status 'completed' `
        -Scenario 'timeline' `
        -SourceOpened $true `
        -PlaybackSampleObserved $true
    Copy-Item -LiteralPath (Join-Path $runIdCandidateDir 'error-candidate.json') -Destination (Join-Path $candidateEvaluationBlockedAnalysisCandidateDir 'error-candidate.json')
    Copy-Item -LiteralPath $endOfStreamCandidatePath -Destination (Join-Path $candidateEvaluationBlockedAnalysisCandidateDir 'end-of-stream-candidate.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $runIdBaselineDir `
            --candidate-dir $candidateEvaluationBlockedAnalysisCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationBlockedAnalysisComparisonsDir `
            --output $candidateEvaluationBlockedAnalysisPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject blocked candidate report analysis.'
        }
    }
    finally {
        Pop-Location
    }

    $blockedAnalysisEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationBlockedAnalysisPath | ConvertFrom-Json
    if ($blockedAnalysisEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected blocked report-analysis evaluate-candidate output to collect comparable evidence.'
    }

    if (-not ($blockedAnalysisEvaluation.reasons -contains 'candidate evaluation has blocked evidence gates')) {
        throw 'Expected blocked report-analysis evaluate-candidate output to explain blocked evidence gates.'
    }

    if ($blockedAnalysisEvaluation.reasons -contains 'candidate evaluation has invalid manifest or report-set evidence') {
        throw 'Expected blocked report-analysis evaluate-candidate output not to blame manifest or report-set evidence.'
    }

    if (-not ($blockedAnalysisEvaluation.blockers -contains 'candidate-report-analysis.blocked')) {
        throw 'Expected blocked report-analysis evaluate-candidate output to include candidate report-analysis blocker.'
    }

    if ($null -eq $blockedAnalysisEvaluation.activeGate -or
        $blockedAnalysisEvaluation.activeGate.name -ne 'candidate-report-analysis' -or
        $blockedAnalysisEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected blocked report-analysis active gate to point at blocked candidate report-analysis gate.'
    }

    if (-not ($blockedAnalysisEvaluation.activeGate.codeTargets -contains 'src/NoiraPlayer.Core/Playback/HdrPlaybackProfileClassifier.cs')) {
        throw 'Expected blocked report-analysis active gate to include source classification code target.'
    }

    if (-not ($blockedAnalysisEvaluation.activeGate.suggestedNextActions -contains 'Collect comparable source metadata before optimizing playback Core.')) {
        throw 'Expected blocked report-analysis active gate to include model analysis suggested next action.'
    }

    if ($null -eq $blockedAnalysisEvaluation.candidateReportAnalysis -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.totalReportCount -ne 3 -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.analyzedReportCount -ne 1 -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.unavailableReportCount -ne 2 -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.blockedReportCount -ne 1) {
        throw 'Expected blocked report-analysis evaluate-candidate output to summarize analyzed blocked candidate report.'
    }

    $blockedAnalysisCase = $blockedAnalysisEvaluation.candidateReportAnalysis.cases |
        Where-Object { $_.caseId -eq 'item-1/source-1' } |
        Select-Object -First 1
    if ($null -eq $blockedAnalysisCase -or $blockedAnalysisCase.status -ne 'blocked') {
        throw 'Expected blocked report-analysis summary to include blocked candidate case.'
    }

    if (-not ($blockedAnalysisCase.blockers -contains 'source.mismatch')) {
        throw 'Expected blocked report-analysis summary case to include model analysis blocker.'
    }

    if (-not ($blockedAnalysisCase.signals -contains 'source.hdrKind')) {
        throw 'Expected blocked report-analysis summary case to include model analysis blocker signal.'
    }

    if (-not ($blockedAnalysisCase.codeTargets -contains 'src/NoiraPlayer.Core/Playback/HdrPlaybackProfileClassifier.cs')) {
        throw 'Expected blocked report-analysis summary case to include source classification code target.'
    }

    if (-not ($blockedAnalysisCase.suggestedNextActions -contains 'Verify media source selection, codec support, HDR/Dolby Vision classification, and fallback policy before tuning playback timing.')) {
        throw 'Expected blocked report-analysis summary case to include triage suggested action.'
    }

    if (Test-Path -LiteralPath $candidateEvaluationBlockedAnalysisComparisonsDir) {
        throw 'Expected blocked report-analysis evidence to skip comparison output.'
    }

    $blockedAnalysisGate = $blockedAnalysisEvaluation.evidenceGates |
        Where-Object { $_.name -eq 'candidate-report-analysis' } |
        Select-Object -First 1
    if ($null -eq $blockedAnalysisGate -or $blockedAnalysisGate.status -ne 'blocked') {
        throw 'Expected candidate report-analysis gate to be blocked.'
    }

    if (-not ($blockedAnalysisGate.blockers -contains 'source.mismatch')) {
        throw 'Expected candidate report-analysis gate to include model analysis blocker.'
    }

    if (-not ($blockedAnalysisGate.signals -contains 'source.hdrKind')) {
        throw 'Expected candidate report-analysis gate to include model analysis blocker signal.'
    }

    if (-not ($blockedAnalysisGate.caseIds -contains 'item-1/source-1')) {
        throw 'Expected candidate report-analysis gate to include affected case id.'
    }

    if (-not ($blockedAnalysisGate.codeTargets -contains 'src/NoiraPlayer.Core/Playback/HdrPlaybackProfileClassifier.cs')) {
        throw 'Expected candidate report-analysis gate to include source classification code target.'
    }

    if (-not ($blockedAnalysisGate.suggestedNextActions -contains 'Collect comparable source metadata before optimizing playback Core.')) {
        throw 'Expected candidate report-analysis gate to include model analysis suggested next action.'
    }

    if (-not ($blockedAnalysisEvaluation.evidenceGates | Where-Object { $_.name -eq 'suite' -and $_.status -eq 'skipped' })) {
        throw 'Expected blocked report-analysis evaluate-candidate suite evidence gate to be skipped.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationStallBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $candidateEvaluationStallCandidateDir | Out-Null
    New-Item -ItemType Directory -Path $candidateEvaluationStallPreviousComparisonsDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') -Destination (Join-Path $candidateEvaluationStallBaselineDir 'baseline-a.json')
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') -Destination (Join-Path $candidateEvaluationStallCandidateDir 'candidate-a.json')
    Copy-Item -LiteralPath (Join-Path $runIdBaselineDir 'error-baseline.json') -Destination (Join-Path $candidateEvaluationStallBaselineDir 'error-baseline.json')
    Copy-Item -LiteralPath (Join-Path $runIdCandidateDir 'error-candidate.json') -Destination (Join-Path $candidateEvaluationStallCandidateDir 'error-candidate.json')
    Copy-Item -LiteralPath $endOfStreamBaselinePath -Destination (Join-Path $candidateEvaluationStallBaselineDir 'end-of-stream-baseline.json')
    Copy-Item -LiteralPath $endOfStreamBaselinePath -Destination (Join-Path $candidateEvaluationStallCandidateDir 'end-of-stream-candidate.json')
    $candidateEvaluationStallPreviousComparisonPath = Join-Path $candidateEvaluationStallPreviousComparisonsDir 'item-1\source-1.json'
    New-Item -ItemType Directory -Path (Split-Path -Parent $candidateEvaluationStallPreviousComparisonPath) | Out-Null

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare `
            --baseline (Join-Path $candidateEvaluationStallBaselineDir 'baseline-a.json') `
            --candidate (Join-Path $candidateEvaluationStallCandidateDir 'candidate-a.json') `
            --output $candidateEvaluationStallPreviousComparisonPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI evaluate-candidate previous stalled comparison generation returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $candidateEvaluationStallBaselineDir `
            --candidate-dir $candidateEvaluationStallCandidateDir `
            --match-by run-id `
            --previous-comparisons-dir $candidateEvaluationStallPreviousComparisonsDir `
            --comparisons-dir $candidateEvaluationStallComparisonsDir `
            --stall-threshold 2 `
            --output $candidateEvaluationStallPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject stalled suite evidence.'
        }
    }
    finally {
        Pop-Location
    }

    $stalledCandidateEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationStallPath | ConvertFrom-Json
    if ($stalledCandidateEvaluation.action -ne 'change-optimization-strategy') {
        throw 'Expected stalled evaluate-candidate output to change optimization strategy.'
    }

    if ($null -eq $stalledCandidateEvaluation.activeGate -or
        $stalledCandidateEvaluation.activeGate.name -ne 'suite' -or
        $stalledCandidateEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected stalled evaluate-candidate active gate to point at blocked suite.'
    }

    if (-not ($stalledCandidateEvaluation.activeGate.targetFailureAreas -contains 'frame-pacing')) {
        throw 'Expected stalled evaluate-candidate active suite gate to include target failure area.'
    }

    if (-not ($stalledCandidateEvaluation.activeGate.targetCaseIds -contains 'item-1/source-1')) {
        throw 'Expected stalled evaluate-candidate active suite gate to include target case id.'
    }

    New-Item -ItemType Directory -Path $stallBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $stallCandidateDir | Out-Null
    New-Item -ItemType Directory -Path $previousComparisonsDir | Out-Null
    Copy-Item -LiteralPath $baselineEnvelopePath -Destination (Join-Path $stallBaselineDir 'case-stall.json')
    Copy-Item -LiteralPath $baselineEnvelopePath -Destination (Join-Path $stallCandidateDir 'case-stall.json')

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare `
            --baseline $baselineEnvelopePath `
            --candidate $baselineEnvelopePath `
            --output (Join-Path $previousComparisonsDir 'case-stall.json')
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI previous comparison generation returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            compare-suite `
            --baseline-dir $stallBaselineDir `
            --candidate-dir $stallCandidateDir `
            --previous-comparisons-dir $previousComparisonsDir `
            --comparisons-dir $stallComparisonsDir `
            --stall-threshold 2 `
            --output $stallSuitePath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI compare-suite with previous comparisons returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $stallSuite = Get-Content -Raw -LiteralPath $stallSuitePath | ConvertFrom-Json
    if ($stallSuite.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI compare-suite stall output schemaVersion 1.'
    }

    if ($stallSuite.action -ne 'change-optimization-strategy') {
        throw 'Expected playback quality CLI compare-suite stall action to change optimization strategy.'
    }

    if ($stallSuite.decision -ne 'change-optimization-strategy') {
        throw 'Expected playback quality CLI compare-suite stall decision to change optimization strategy.'
    }

    if (-not ($stallSuite.failureAreas -contains 'frame-pacing')) {
        throw 'Expected playback quality CLI compare-suite stall suite to include persisting failure area.'
    }

    if (-not ($stallSuite.targetFailureAreas -contains 'frame-pacing')) {
        throw 'Expected playback quality CLI compare-suite stall suite to include target failure area.'
    }

    if (-not ($stallSuite.targetCaseIds -contains 'case-stall.json')) {
        throw 'Expected playback quality CLI compare-suite stall suite to include target case id.'
    }

    if (-not ($stallSuite.signals -contains 'timing.maxFrameGapMs')) {
        throw 'Expected playback quality CLI compare-suite stall suite to include persisting failure signal.'
    }

    $stallCase = $stallSuite.cases | Select-Object -First 1
    if ($null -eq $stallCase -or -not ($stallCase.failureAreas -contains 'frame-pacing')) {
        throw 'Expected playback quality CLI compare-suite stall case to include persisting failure area.'
    }

    if (-not ($stallCase.signals -contains 'timing.maxFrameGapMs')) {
        throw 'Expected playback quality CLI compare-suite stall case to include persisting failure signal.'
    }

    $stallComparison = Get-Content -Raw -LiteralPath (Join-Path $stallComparisonsDir 'case-stall.json') | ConvertFrom-Json
    if ($stallComparison.optimization.action -ne 'change-optimization-strategy') {
        throw 'Expected playback quality CLI compare-suite stall comparison action to change optimization strategy.'
    }

    if (-not ($stallComparison.optimization.blockers | Where-Object { $_ -eq 'iteration.stalled' })) {
        throw 'Expected playback quality CLI compare-suite stall comparison to include iteration.stalled blocker.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            validate-manifest `
            --manifest $exampleManifestPath `
            --output $exampleManifestValidationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI validate-manifest returned a non-zero exit code for the example reference manifest.'
        }
    }
    finally {
        Pop-Location
    }

    $exampleManifestValidation = Get-Content -Raw -LiteralPath $exampleManifestValidationPath | ConvertFrom-Json
    if ($exampleManifestValidation.isValid -ne $true) {
        throw 'Expected example reference manifest validation to be valid.'
    }

    if ($exampleManifestValidation.caseCount -lt 7) {
        throw 'Expected example reference manifest to include the public/core reference case set.'
    }

    if ($exampleManifestValidation.coverage.status -ne 'incomplete' -or
        $exampleManifestValidation.coverage.isCoreEvaluationReady -ne $false) {
        throw 'Expected the public example manifest to remain incomplete until executable lifecycle and track fixtures are supplied.'
    }

    $requiredPurposes = @(
        'sdr-smoke',
        'hdr-output',
        'hdr-force-sdr',
        'dv-reject',
        'dv-fallback',
        'cadence-23.976',
        'frame-pacing',
        'av-sync',
        'buffering',
        'timeline',
        'tracks',
        'subtitles',
        'end-of-stream',
        'error-handling'
    )
    $expectedMissingPurposes = @(
        'cadence-23.976',
        'av-sync',
        'timeline',
        'tracks',
        'subtitles',
        'end-of-stream',
        'error-handling'
    )
    foreach ($purpose in $requiredPurposes) {
        $isMissing = $expectedMissingPurposes -contains $purpose
        if ($isMissing -ne ($exampleManifestValidation.coverage.missingPurposes -contains $purpose)) {
            throw ('Unexpected public example manifest coverage for purpose: ' + $purpose)
        }
    }

    if (-not ($exampleManifestValidation.categories -contains 'stable') -or
        -not ($exampleManifestValidation.categories -contains 'challenge')) {
        throw 'Expected example reference manifest validation to expose stable and challenge case categories.'
    }

    if (-not ($exampleManifestValidation.severities -contains 'critical') -or
        -not ($exampleManifestValidation.stabilities -contains 'variable')) {
        throw 'Expected example reference manifest validation to expose severity and stability dimensions.'
    }

    if (-not ($exampleManifestValidation.cases | Where-Object {
        $_.caseId -eq 'jellyfin/hdr10-hevc-main10-4k60-50m' -and
        $_.category -eq 'challenge' -and
        $_.severity -eq 'high' -and
        $_.stability -eq 'variable' -and
        $_.uri -eq 'https://repo.jellyfin.org/test-videos/HDR/HDR10/HEVC/Test%20Jellyfin%204K%20HEVC%20HDR10%2050M.mp4' -and
        $_.expected.codec -eq 'hevc' -and
        $_.expected.width -eq 3840 -and
        $_.expected.height -eq 2160 -and
        $_.expected.frameRate -eq 60 -and
        $_.expected.hdrKind -eq 'Hdr10' -and
        $_.expected.hdrOutput -eq 'Hdr10' -and
        $_.expected.dxgiInput -eq 'YCBCR_STUDIO_G2084_TOPLEFT_P2020'
    })) {
        throw 'Expected example reference manifest to include the verified Jellyfin 4K60 HDR10 50M case.'
    }

    if (-not ($exampleManifestValidation.cases | Where-Object {
        $_.caseId -eq 'jellyfin/hdr10-hevc-main10-1080p60-10m-force-sdr' -and
        $_.forceSdrOutput -eq $true -and
        $_.expected.hdrKind -eq 'Hdr10' -and
        $_.expected.hdrOutput -eq 'Hdr10' -and
        $_.expected.sdrDisplayFallback.hdrOutput -eq 'Sdr' -and
        $_.expected.sdrDisplayFallback.requiredConversionStatus -eq 'tone-mapped-hable'
    })) {
        throw 'Expected example reference manifest to include an HDR force-SDR validation case.'
    }

    if (-not ($exampleManifestValidation.cases | Where-Object {
        $_.caseId -eq 'jellyfin/dv-profile5-hevc-4k60' -and
        $_.expected.hdrKind -eq 'DolbyVisionUnsupported' -and
        $_.expected.hdrPlaybackStrategy -eq 'Dolby Vision unsupported' -and
        $_.expected.isDirectPlayable -eq $false -and
        $_.expected.dolbyVisionProfile -eq 5
    })) {
        throw 'Expected example reference manifest to include a Dolby Vision Profile 5 reject case.'
    }

    if (-not ($exampleManifestValidation.cases | Where-Object {
        $_.caseId -eq 'jellyfin/dv-profile8-1-hevc-4k60-hdr10-fallback' -and
        $_.expected.hdrKind -eq 'DolbyVisionWithHdr10Fallback' -and
        $_.expected.hdrPlaybackStrategy -eq 'HDR10 fallback from Dolby Vision' -and
        $_.expected.isDirectPlayable -eq $true -and
        $_.expected.dolbyVisionProfile -eq 8 -and
        $_.expected.dolbyVisionCompatibilityId -eq 1 -and
        $_.expected.hasHdr10BaseLayer -eq $true
    })) {
        throw 'Expected example reference manifest to include a Dolby Vision Profile 8.1 HDR10 fallback case.'
    }

    if (-not ($exampleManifestValidation.cases | Where-Object {
        $_.caseId -eq 'local/sdr-resume-seek-timeline' -and
        $_.startPositionTicks -eq 600000000 -and
        $_.expected.maxSeekPositionErrorMs -eq 500
    })) {
        throw 'Expected example reference manifest to include a local resume/seek timeline case.'
    }

    if (-not ($exampleManifestValidation.cases | Where-Object {
        $_.caseId -eq 'local/missing-file-error-handling' -and
        ($_.purpose -contains 'error-handling')
    })) {
        throw 'Expected example reference manifest to include a local error-handling case.'
    }

    Push-Location $repoRoot
    try {
        dotnet $cliDll `
            plan-runs `
            --manifest $exampleManifestPath `
            --reports-dir captured-example `
            --duration 30 `
            --output $exampleRunPlanPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI plan-runs returned a non-zero exit code for the example reference manifest.'
        }
    }
    finally {
        Pop-Location
    }

    $exampleRunPlan = Get-Content -Raw -LiteralPath $exampleRunPlanPath | ConvertFrom-Json
    if ($exampleRunPlan.caseCount -ne $exampleManifestValidation.caseCount) {
        throw 'Expected example reference run plan case count to match manifest validation.'
    }

    if (-not ($exampleRunPlan.cases | Where-Object {
        $_.caseId -eq 'jellyfin/hdr10-hevc-main10-4k60-50m' -and
        $_.captureMode -eq 'direct-uri' -and
        $_.devCommand.route -eq 'quality-run' -and
        $_.devCommand.streamUrl -eq 'https://repo.jellyfin.org/test-videos/HDR/HDR10/HEVC/Test%20Jellyfin%204K%20HEVC%20HDR10%2050M.mp4' -and
        $_.devCommand.runId -eq 'jellyfin/hdr10-hevc-main10-4k60-50m' -and
        ($_.requiredSignals -contains 'buffers.videoStarvedPasses') -and
        ($_.requiredSignals -contains 'buffers.audioStarvedPasses') -and
        ($_.requiredSignals -contains 'colorPipeline.actualHdrOutput') -and
        ($_.requiredSignals -contains 'display.hdrStatus') -and
        ($_.requiredSignals -contains 'colorPipeline.swapChainFormat') -and
        ($_.requiredSignals -contains 'colorPipeline.swapChainColorSpace') -and
        ($_.requiredSignals -contains 'colorPipeline.isTenBitSwapChain') -and
        ($_.requiredSignals -contains 'colorPipeline.dxgiInput') -and
        $_.devCommand.expected.sdrDisplayFallback.hdrOutput -eq 'Sdr' -and
        ($_.devCommand.expected.sdrDisplayFallback.dxgiInputAnyOf -contains 'YCBCR_STUDIO_G22_LEFT_P2020') -and
        $_.devCommand.expected.sdrDisplayFallback.requiredConversionStatus -eq 'tone-mapped-hable'
    })) {
        throw 'Expected example reference run plan to schedule public Jellyfin direct-uri through a quality-run dev command.'
    }

    if (-not ($exampleRunPlan.cases | Where-Object {
        $_.caseId -eq 'local/chimera-23976-hdr10-cadence' -and
        $_.captureMode -eq 'emby-item' -and
        $_.devCommand.route -eq 'quality-run' -and
        $_.devCommand.itemId -eq 'quality-case-chimera-23976-hdr10' -and
        $_.expected.frameRate -eq 23.976 -and
        $_.expected.requireMatchedDisplayRefreshRate -eq $true -and
        ($_.requiredSignals -contains 'display.refreshRateHz') -and
        ($_.requiredSignals -contains 'timing.framePacingSourceFrameRate') -and
        ($_.requiredSignals -contains 'timing.lateFrameDropToleranceMs') -and
        ($_.requiredSignals -contains 'sync.audioVideoDriftMsP95')
    })) {
        throw 'Expected example reference run plan to schedule the local 23.976 cadence case through an Emby quality-run command.'
    }

    if (-not ($exampleRunPlan.cases | Where-Object {
        $_.caseId -eq 'local/sdr-resume-seek-timeline' -and
        $_.captureMode -eq 'emby-item' -and
        $_.devCommand.route -eq 'quality-run' -and
        $_.devCommand.startPositionTicks -eq 600000000 -and
        ($_.requiredSignals -contains 'lifecycle.seek') -and
        ($_.requiredSignals -contains 'position.seekTargetPositionTicks') -and
        ($_.requiredSignals -contains 'position.actualPositionTicks') -and
        ($_.requiredSignals -contains 'position.seekPositionErrorMs')
    })) {
        throw 'Expected example reference run plan to schedule the local timeline case with position required signals.'
    }

    if (-not ($exampleRunPlan.cases | Where-Object {
        $_.caseId -eq 'jellyfin/sdr-hevc-main10-1080p60-3m' -and
        $_.category -eq 'stable' -and
        $_.severity -eq 'high' -and
        $_.stability -eq 'stable' -and
        ($_.requiredSignals -contains 'lifecycle.load') -and
        ($_.requiredSignals -contains 'lifecycle.play') -and
        -not ($_.requiredSignals -contains 'lifecycle.pause') -and
        -not ($_.requiredSignals -contains 'lifecycle.resume') -and
        ($_.requiredSignals -contains 'lifecycle.stop') -and
        ($_.requiredSignals -contains 'timing.renderedVideoFrames') -and
        ($_.requiredSignals -contains 'timing.framePacingSourceFrameRate') -and
        -not ($_.requiredSignals -contains 'tracks.audioTrackCount') -and
        -not ($_.requiredSignals -contains 'tracks.subtitleTrackCount')
    })) {
        throw 'Expected the public SDR video-only case to require only evidence it can actually provide.'
    }

    Write-Output 'playback-quality-cli smoke ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
