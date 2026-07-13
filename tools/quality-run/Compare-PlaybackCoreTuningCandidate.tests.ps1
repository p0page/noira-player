$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$baselineScriptPath = Join-Path $repoRoot 'tools\quality-run\New-PlaybackCoreTuningBaseline.ps1'
$comparisonScriptPath = Join-Path $repoRoot 'tools\quality-run\Compare-PlaybackCoreTuningCandidate.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('playback-core-tuning-candidate-comparison-test-' + [Guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $manifestPath = Join-Path $tempRoot 'manifest.json'
    $fakeHarness = Join-Path $tempRoot 'fake-harness.ps1'
    $fakeHelper = Join-Path $tempRoot 'fake-helper.exe'
    $baselineRoot = Join-Path $tempRoot 'baseline'
    $candidateRoot = Join-Path $tempRoot 'candidate'
    $comparisonRoot = Join-Path $tempRoot 'comparison'
    $candidateCadenceStabilityPath = Join-Path $tempRoot 'candidate-cadence-stability.local.json'
    Set-Content -LiteralPath $fakeHelper -Value '' -Encoding ASCII

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "comparison/native-source-equivalence",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "https://media.invalid/native-source-equivalence.mp4",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "timeline" },
      "purpose": [
        "frame-pacing", "timeline"
      ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 24,
        "hdrKind": "Sdr",
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1,
        "requireValidatedConversion": false,
        "requireMatchedDisplayRefreshRate": true
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

$caseId = Get-Value '--case-id'
$reportsDir = Get-Value '--reports-dir'
$locatorHash = Get-Value '--source-locator-hash'
$scenario = Get-Value '--scenario'
$openedHash = $env:NOIRAPLAYER_COMPARISON_TEST_OPENED_HASH
$sourceRevision = $env:NOIRAPLAYER_COMPARISON_TEST_REVISION
$maxFrameGap = [double]$env:NOIRAPLAYER_COMPARISON_TEST_MAX_FRAME_GAP
$reportPath = Join-Path $reportsDir ($caseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json')
New-Item -ItemType Directory -Path (Split-Path -Parent $reportPath) -Force | Out-Null

$report = [ordered]@{
    runId = $caseId
    metricVersion = 'software-quality-v1'
    result = 'fail'
    expected = @{
        codec = 'h264'; width = 320; height = 180; frameRate = 24.0; hdrKind = 'Sdr'
        minRenderedVideoFrames = 1; requireValidatedConversion = $false
        requireMatchedDisplayRefreshRate = $true
    }
    environment = @{
        collectorVersion = 'native-comparison-fixture-v1'
        playerCoreVersion = 'comparison-test-core'
        sourceRevision = $sourceRevision
        buildConfiguration = 'Debug'
    }
    execution = @{
        attemptId = 'attempt-' + $sourceRevision
        runner = 'native-headless'
        scenario = $scenario
        evidenceLevel = 'native-playback'
        status = 'completed'
        sourceLocatorHash = $locatorHash
        openedSourceHash = $openedHash
        openedSourceHashKind = 'observed-media-signature-v1'
        startedAtUtc = '2026-07-11T00:00:00.0000000+00:00'
        durationMs = 3000.0
        requestedSampleDurationMs = [double](Get-Value '--duration-seconds') * 1000.0
        observedSampleWallClockDurationMs = [double](Get-Value '--duration-seconds') * 1000.0
        sourceOpenAttempted = $true
        sourceOpened = $true
        nativeGraphOpened = $true
        demuxStarted = $true
        decoderOpened = $true
        playbackSampleObserved = $true
    }
    source = @{
        codec = 'h264'; hasDirectStreamUrl = $true; directStreamProtocol = 'https'
        container = 'mp4'; bitrate = 1000000; durationTicks = 600000000
        containerStartTimeTicks = 0; videoStreamStartTimeTicks = 0
        width = 320; height = 180; frameRate = 24.0; hdrKind = 'Sdr'; videoRange = 'SDR'
        colorPrimaries = 'bt709'; colorTransfer = 'bt709'; colorSpace = 'bt709'
        isHdr = $false; isDirectPlayable = $true; isDolbyVision = $false
        hasHdr10BaseLayer = $false; hasHlgBaseLayer = $false
    }
    startup = @{
        commandReceivedAt = '2026-07-11T00:00:00.0000000+00:00'
        playbackStartedAt = '2026-07-11T00:00:00.3000000+00:00'
        startupDurationMs = 300.0
        stages = @(@{
            name = 'native.open'; durationMs = 300.0
            components = @(
                'ffmpeg.open-input',
                'ffmpeg.find-stream-info',
                'native.startup-seek',
                'native.first-frame.demux-read'
            ) | ForEach-Object {
                @{
                    name = $_; durationMs = 75.0; status = 'measured'
                    packetCount = 0; transportBytes = 0; packetPayloadBytes = 0
                    transportProvider = 'ffmpeg-builtin'
                    transportCallEvidenceStatus = 'unavailable'
                    transportReadCalls = $null; transportSeekCalls = $null
                    transportReadWaitMs = $null; transportSeekWaitMs = $null
                    transportSeekDistanceBytes = $null
                }
            }
        })
    }
    lifecycle = @{
        events = @(
            @{ operation = 'load'; status = 'completed'; positionTicks = 0 },
            @{ operation = 'play'; status = 'completed'; positionTicks = 0 },
            @{ operation = 'pause'; status = 'completed'; positionTicks = 10000000 },
            @{ operation = 'resume'; status = 'completed'; positionTicks = 10000000 },
            @{ operation = 'seek'; status = 'completed'; positionTicks = 10000000 },
            @{ operation = 'stop'; status = 'completed'; positionTicks = 30000000 },
            @{ operation = 'error'; status = 'not-applicable'; positionTicks = 0 }
        )
    }
    position = @{
        requestedStartPositionTicks = 0; seekTargetPositionTicks = 10000000
        seekDemuxTargetTicks = 10000000; actualPositionTicks = 10000000
        firstPresentedPositionTicks = 10000000; postSeekPositionTicks = 30000000
        postSeekAdvanced = $true; seekPositionErrorMs = 0.0
        seekOperationDurationMs = 120.0; seekRecoveryDurationMs = 150.0
        seekPacketCacheEnabled = $false; seekPacketCacheHit = $false
        seekPacketCachePacketCount = 0; seekPacketCacheBytes = 0
        seekPacketCacheWindowDurationTicks = 0; seekFallbackReason = 'disabled'
    }
    tracks = @{
        videoTrackCount = 1; audioTrackCount = 1; subtitleTrackCount = 1
        selectedVideoStreamIndex = 0; selectedAudioStreamIndex = 1
        selectedSubtitleStreamIndex = -1; isSubtitleDisabled = $true
        video = @(@{ index = 0; kind = 'Video'; codec = 'h264'; language = 'und'; isExternal = $false; isDefault = $true; isForced = $false })
        audio = @(@{ index = 1; kind = 'Audio'; codec = 'aac'; language = 'eng'; channels = 2; isExternal = $false; isDefault = $true; isForced = $false })
        subtitles = @(@{ index = 2; kind = 'Subtitle'; codec = 'srt'; language = 'eng'; isExternal = $false; isDefault = $false; isForced = $false })
    }
    runtimeMetrics = @{
        status = 'captured'; providerStatus = 'native-headless:returned-snapshot'
        reason = 'comparison fixture'; hasSnapshot = $true; hasPlaybackSample = $true
    }
    timing = @{
        decodedVideoFrames = 73; renderedVideoFrames = 72; expectedFrameDurationMs = 41.667
        renderIntervalMsP95 = 42.0; renderIntervalMsP99 = 44.0; maxFrameGapMs = $maxFrameGap
        framePacingSourceFrameRate = 24.0; lateFrameDropToleranceMs = 104.167
    }
    sync = @{ audioClockTicks = 30000000; videoPositionTicks = 30000000; audioVideoDriftMsP95 = 5.0 }
    buffers = @{ submittedAudioFrames = 72; queuedAudioBuffers = 2; videoStarvedPasses = 0; audioStarvedPasses = 0 }
    colorPipeline = @{
        actualHdrOutput = 'Sdr'; dxgiInput = 'YCBCR_STUDIO_G22_LEFT_P709'
        dxgiOutput = 'RGB_FULL_G22_NONE_P709'; swapChainFormat = 'B8G8R8A8_UNORM'
        swapChainColorSpace = 'RGB_FULL_G22_NONE_P709'; conversionStatus = 'not-required'
        isTenBitSwapChain = $false; forceSdrOutput = $false
    }
    display = @{ hdrStatus = 'Sdr'; isHdrDisplayAvailable = $false; isHdrOutputActive = $false; refreshRateHz = 24.0 }
    error = @{
        code = 'none'; message = 'no error'; operation = ''; exceptionType = ''
        failureClass = 'insufficient instrumentation'; failureArea = 'error-handling'
        isTerminal = $false; isRetriable = $false
    }
    checks = @(@{
        name = 'MaxFrameGapMs'; signal = 'timing.maxFrameGapMs'; status = 'fail'
        failureArea = 'frame-pacing'; expected = '105.000'; actual = $maxFrameGap.ToString('F3', [Globalization.CultureInfo]::InvariantCulture)
    })
}

@{
    schemaVersion = 1
    evaluationVersion = 'playback-quality-v0.12'
    caseMetadata = @{ caseId = $caseId; category = 'stable'; severity = 'high'; stability = 'stable' }
    report = $report
} | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $reportPath -Encoding UTF8
exit 0
'@ | Set-Content -LiteralPath $fakeHarness -Encoding UTF8

    $env:NOIRAPLAYER_COMPARISON_TEST_OPENED_HASH = 'sha256:' + ('a' * 64)
    $env:NOIRAPLAYER_COMPARISON_TEST_REVISION = 'baseline-test-revision'
    $env:NOIRAPLAYER_COMPARISON_TEST_MAX_FRAME_GAP = '180'
    powershell -NoProfile -ExecutionPolicy Bypass -File $baselineScriptPath `
        -PublicManifestPath $manifestPath -NoPrivateManifest -SkipNativeHeadless `
        -NativeHelperExe $fakeHelper -ManifestRunnerHarnessScriptPath $fakeHarness `
        -DurationSeconds 1 -AttemptTimeoutSeconds 5 `
        -OutputRoot $baselineRoot -SourceRevision 'baseline-test-revision'
    if ($LASTEXITCODE -ne 0) { throw 'Failed to build strict-valid native baseline fixture.' }

    $baselineSummary = Get-Content -Raw -LiteralPath (Join-Path $baselineRoot 'baseline-summary.local.json') | ConvertFrom-Json
    if ($baselineSummary.coreExecution.durationSeconds -ne 1 -or
        $baselineSummary.coreExecution.attemptTimeoutSeconds -ne 5) {
        throw 'Baseline summary must preserve the native execution observation window and timeout.'
    }

    $env:NOIRAPLAYER_COMPARISON_TEST_OPENED_HASH = 'sha256:' + ('b' * 64)
    $env:NOIRAPLAYER_COMPARISON_TEST_REVISION = 'candidate-test-revision'
    $env:NOIRAPLAYER_COMPARISON_TEST_MAX_FRAME_GAP = '120'
    powershell -NoProfile -ExecutionPolicy Bypass -File $baselineScriptPath `
        -PublicManifestPath $manifestPath -NoPrivateManifest -SkipNativeHeadless `
        -NativeHelperExe $fakeHelper -ManifestRunnerHarnessScriptPath $fakeHarness `
        -DurationSeconds 2 -AttemptTimeoutSeconds 5 `
        -OutputRoot $candidateRoot -SourceRevision 'candidate-test-revision'
    if ($LASTEXITCODE -ne 0) { throw 'Failed to build strict-valid native candidate fixture.' }

    @{
        schemaVersion = 1; kind = 'playback-cadence-stability-summary'
        minimumSamples = 3; materialityMs = 2.0; totalGroupCount = 1
        stableGroupCount = 0; unstableGroupCount = 1; insufficientSampleGroupCount = 0
        unstableCaseGroupIds = @('comparison/native-source-equivalence')
        groups = @(@{
            caseGroupId = 'comparison/native-source-equivalence'; stability = 'unstable'; sampleCount = 3
            renderIntervalP05ExpectedErrorSpreadMs = 2.1; renderIntervalP99ExpectedErrorSpreadMs = 3.2
            minFrameGapExpectedErrorSpreadMs = 2.3; maxFrameGapExpectedErrorSpreadMs = 3.2
            unstableSignals = @('framePacing.maxFrameGapExpectedErrorMs')
        })
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $candidateCadenceStabilityPath -Encoding UTF8

    powershell -NoProfile -ExecutionPolicy Bypass -File $comparisonScriptPath `
        -BaselineRoot $baselineRoot -CandidateRoot $candidateRoot -OutputRoot $comparisonRoot `
        -EvaluationScope focused `
        -CandidateCadenceStabilityPath $candidateCadenceStabilityPath
    $comparisonExitCode = $LASTEXITCODE
    if ($comparisonExitCode -ne 2) {
        throw ('Expected opened-source mismatch comparison to exit 2, actual: ' + $comparisonExitCode)
    }

    $summary = Get-Content -Raw -LiteralPath (Join-Path $comparisonRoot 'comparison-summary.local.json') | ConvertFrom-Json
    $evaluation = Get-Content -Raw -LiteralPath (Join-Path $comparisonRoot 'summaries\candidate-evaluation.local.json') | ConvertFrom-Json
    if ($summary.baselineValidation.isValid -ne $true -or $summary.candidateValidation.isValid -ne $true) {
        throw 'Expected both native fixture report sets to remain strict-valid before comparison.'
    }
    if ($summary.runConfiguration.compatible -ne $false -or
        $summary.runConfiguration.baselineDurationSeconds -ne 1 -or
        $summary.runConfiguration.candidateDurationSeconds -ne 2 -or
        -not ($summary.runConfiguration.signals -contains 'execution.requestedSampleDurationMs') -or
        -not ($summary.evaluation.blockers -contains 'comparison.incompatible-run-configuration')) {
        throw 'Mismatched observation windows must make baseline/candidate execution configuration incompatible.'
    }
    if ($summary.evaluationScope -ne 'focused' -or
        $evaluation.evaluationScope -ne 'focused' -or
        -not ($evaluation.evidenceGates | Where-Object { $_.name -eq 'manifest-coverage' -and $_.status -eq 'pass' })) {
        throw 'Focused comparison must preserve its scope and must not claim broad corpus coverage.'
    }
    if ($evaluation.suite.totalComparisonCount -ne 1 -or
        $evaluation.suite.insufficientEvidenceCount -ne 1 -or
        $evaluation.suite.improvedCount -ne 0 -or
        -not ($evaluation.suite.blockers -contains 'comparison.incompatible-inputs') -or
        -not ($evaluation.suite.signals -contains 'execution.openedSourceHash')) {
        throw 'Expected source-incompatible native reports to produce only insufficient comparison evidence.'
    }
    if ($summary.cadenceStability.candidate.present -ne $true -or
        -not ($summary.cadenceStability.candidate.unstableCaseGroupIds -contains 'comparison/native-source-equivalence')) {
        throw 'Expected comparison summary to preserve candidate cadence stability evidence.'
    }

    Write-Output 'playback-core-tuning-candidate-comparison tests ok'
}
finally {
    Remove-Item Env:NOIRAPLAYER_COMPARISON_TEST_OPENED_HASH -ErrorAction SilentlyContinue
    Remove-Item Env:NOIRAPLAYER_COMPARISON_TEST_REVISION -ErrorAction SilentlyContinue
    Remove-Item Env:NOIRAPLAYER_COMPARISON_TEST_MAX_FRAME_GAP -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
