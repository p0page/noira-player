$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('playback-quality-cli-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    $baselinePath = Join-Path $tempRoot 'baseline.json'
    $candidatePath = Join-Path $tempRoot 'candidate.json'
    $baselineEnvelopePath = Join-Path $tempRoot 'baseline-envelope.json'
    $candidateEnvelopePath = Join-Path $tempRoot 'candidate-envelope.json'
    $analysisPath = Join-Path $tempRoot 'analysis.json'
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
    $exampleManifestPath = Join-Path $repoRoot 'docs\qa\playback-quality-reference-manifest.example.json'
    $exampleManifestValidationPath = Join-Path $tempRoot 'example-reference-manifest-validation.json'
    $exampleRunPlanPath = Join-Path $tempRoot 'example-reference-run-plan.json'
    $embyRunPlanManifestPath = Join-Path $tempRoot 'emby-run-plan-manifest.json'
    $embyRunPlanPath = Join-Path $tempRoot 'emby-run-plan.json'
    $reportSetDir = Join-Path $tempRoot 'reference-report-set'
    $reportSetValidationPath = Join-Path $tempRoot 'reference-report-set-validation.json'
    $missingSignalReportSetDir = Join-Path $tempRoot 'reference-report-set-missing-signals'
    $missingSignalReportSetValidationPath = Join-Path $tempRoot 'reference-report-set-missing-signals-validation.json'
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
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Sdr"
  },
  "timing": {
    "renderedVideoFrames": 240,
    "expectedFrameDurationMs": 41.708,
    "framePacingSourceFrameRate": 23.976,
    "lateFrameDropToleranceMs": 104.271
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
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Sdr"
  },
  "timing": {
    "renderedVideoFrames": 240,
    "expectedFrameDurationMs": 41.708,
    "framePacingSourceFrameRate": 23.976,
    "lateFrameDropToleranceMs": 104.271
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

    $baselineReportJson = Get-Content -Raw -LiteralPath $baselinePath
    $candidateReportJson = Get-Content -Raw -LiteralPath $candidatePath

    @"
{
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

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- analyze-report `
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

    New-Item -ItemType Directory -Path $analysisSetDir | Out-Null
    Copy-Item -LiteralPath $baselinePath -Destination (Join-Path $analysisSetDir 'baseline.json')
    Copy-Item -LiteralPath $candidatePath -Destination (Join-Path $analysisSetDir 'candidate.json')

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- analyze-report-set `
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
        ($_.failureAreas -contains 'frame-pacing') -and
        ($_.signals -contains 'timing.maxFrameGapMs')
    })) {
        throw 'Expected analyze-report-set output to expose analyzed candidate blockers and signals.'
    }

    New-Item -ItemType Directory -Path $analysisEnvelopeSetDir | Out-Null
    Copy-Item -LiteralPath $baselineEnvelopePath -Destination (Join-Path $analysisEnvelopeSetDir 'baseline-envelope.json')
    Copy-Item -LiteralPath $candidateEnvelopePath -Destination (Join-Path $analysisEnvelopeSetDir 'candidate-envelope.json')

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- analyze-report-set `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- analyze-report-set `
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
      "uri": "https://example.invalid/netflix/chimera-4k-2398-hdr-pq.mp4",
      "tier": 2,
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
      "uri": "https://example.invalid/jellyfin/dv-profile5-hevc-4k.mp4",
      "tier": 3,
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
    }
  ]
}
'@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- validate-manifest `
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
    if ($manifestValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI validate-manifest output to be valid.'
    }

    if ($manifestValidation.caseCount -ne 2) {
        throw 'Expected playback quality CLI validate-manifest output to include two cases.'
    }

    if (-not ($manifestValidation.purposes | Where-Object { $_ -eq 'hdr-output' })) {
        throw 'Expected playback quality CLI validate-manifest output to include hdr-output purpose.'
    }

    if (-not ($manifestValidation.cases | Where-Object {
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.tier -eq 2 -and
        $_.expected.codec -eq 'hevc' -and
        $_.expected.hdrKind -eq 'Hdr10'
    })) {
        throw 'Expected playback quality CLI validate-manifest output to include schedulable case summary.'
    }

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- plan-runs `
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

    if ($runPlan.caseCount -ne 2) {
        throw 'Expected playback quality CLI plan-runs output to include two cases.'
    }

    if ($runPlan.durationSeconds -ne 60) {
        throw 'Expected playback quality CLI plan-runs output to keep requested duration.'
    }

    if (-not ($runPlan.cases | Where-Object {
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.runId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.sourceUri -eq 'https://example.invalid/netflix/chimera-4k-2398-hdr-pq.mp4' -and
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- plan-runs `
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
      "tier": 1,
      "purpose": [
        "hdr-output"
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- plan-runs `
            --manifest $embyRunPlanManifestPath `
            --reports-dir captured-emby `
            --duration 45 `
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
  "source": {
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Hdr10"
  },
  "timing": {
    "renderedVideoFrames": 1440,
    "expectedFrameDurationMs": 41.708,
    "maxFrameGapMs": 48.0,
    "framePacingSourceFrameRate": 23.976,
    "lateFrameDropToleranceMs": 104.271
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
'@ | Set-Content -LiteralPath (Join-Path $reportSetDir 'case-b.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- validate-report-set `
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
    if ($reportSetValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI validate-report-set output to be valid.'
    }

    if ($reportSetValidation.matchedCaseCount -ne 2) {
        throw 'Expected playback quality CLI validate-report-set output to include two matched cases.'
    }

    New-Item -ItemType Directory -Path $missingSignalReportSetDir | Out-Null
    @'
{
  "runId": "netflix/chimera-4k-2398-hdr-pq",
  "metricVersion": "software-quality-v1",
  "source": {
    "codec": "hevc",
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

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- validate-report-set `
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
        ($_.codeTargets -contains 'src/NextGenEmby.Native/Media/FramePacing.h')
    })) {
        throw 'Expected playback quality CLI validate-report-set missing telemetry output to include triaged display refresh evidence.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "jellyfin/zero-starvation-buffering",
      "uri": "https://example.invalid/jellyfin/zero-starvation-buffering.mp4",
      "tier": 1,
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
  "source": {
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Hdr10"
  },
  "buffers": {
    "videoStarvedPasses": 0,
    "audioStarvedPasses": 0
  },
  "colorPipeline": {
    "conversionStatus": "validated"
  }
}
'@ | Set-Content -LiteralPath (Join-Path $zeroCounterReportSetDir 'zero-counter.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- validate-report-set `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- analyze-report `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
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
    if ($comparison.result -ne 'improved') {
        throw 'Expected playback quality CLI comparison result to be improved.'
    }

    if ($comparison.confidence.level -ne 'strong') {
        throw 'Expected playback quality CLI comparison confidence to be strong.'
    }

    if ($comparison.optimization.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI optimization action to accept candidate.'
    }

    if (-not ($comparison.improvements | Where-Object { $_.signal -eq 'timing.maxFrameGapMs' })) {
        throw 'Expected playback quality CLI comparison to include timing.maxFrameGapMs improvement.'
    }

    $incompatibleCandidate = Get-Content -Raw -LiteralPath $candidatePath | ConvertFrom-Json
    $incompatibleCandidate.source.mediaSourceId = 'source-2'
    $incompatibleCandidate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $incompatibleCandidatePath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- summarize `
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
    if ($suite.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI suite action to accept candidate.'
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare-suite `
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
    if ($suiteFromReports.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI compare-suite action to accept candidate.'
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
    if ($comparisonFromSuite.result -ne 'improved') {
        throw 'Expected playback quality CLI compare-suite comparison result to be improved.'
    }

    if ($comparisonFromSuite.caseId -ne 'case-a.json') {
        throw 'Expected playback quality CLI compare-suite comparison to include caseId.'
    }

    New-Item -ItemType Directory -Path $runIdBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $runIdCandidateDir | Out-Null
    @'
{
  "runId": "item-1/source-1",
  "metricVersion": "software-quality-v1",
  "environment": {
    "playerCoreVersion": "core-baseline",
    "sourceRevision": "baseline-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Sdr"
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
}
'@ | Set-Content -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') -Encoding UTF8
    @'
{
  "runId": "item-1/source-1",
  "metricVersion": "software-quality-v1",
  "environment": {
    "playerCoreVersion": "core-candidate",
    "sourceRevision": "candidate-revision",
    "buildConfiguration": "Debug"
  },
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Sdr"
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
}
'@ | Set-Content -LiteralPath (Join-Path $runIdCandidateDir 'candidate-renamed.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare-suite `
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
    if ($runIdSuite.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI compare-suite run-id matching action to accept candidate.'
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
      "purpose": [
        "sdr-smoke",
        "hdr-output",
        "hdr-force-sdr",
        "dv-reject",
        "dv-fallback",
        "cadence-23.976",
        "av-sync",
        "buffering",
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
'@ | Set-Content -LiteralPath $candidateEvaluationManifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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
    if ($candidateEvaluation.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI evaluate-candidate action to accept candidate.'
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
        $candidateEvaluation.activeGate.confidence.strongCount -ne 1) {
        throw 'Expected evaluate-candidate active gate to expose strong suite confidence.'
    }

    if ($null -eq $candidateEvaluation.activeGate.resultCounts -or
        $candidateEvaluation.activeGate.resultCounts.totalCount -ne 1 -or
        $candidateEvaluation.activeGate.resultCounts.improvedCount -ne 1) {
        throw 'Expected evaluate-candidate active gate to expose improved suite result counts.'
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
        $candidateEvaluation.baselineReportAnalysis.totalReportCount -ne 1 -or
        $candidateEvaluation.baselineReportAnalysis.analyzedReportCount -ne 0 -or
        $candidateEvaluation.baselineReportAnalysis.unavailableReportCount -ne 1 -or
        $candidateEvaluation.baselineReportAnalysis.blockedReportCount -ne 0) {
        throw 'Expected evaluate-candidate to summarize unavailable baseline report analysis for raw reports.'
    }

    if ($null -eq $candidateEvaluation.candidateReportAnalysis -or
        $candidateEvaluation.candidateReportAnalysis.totalReportCount -ne 1 -or
        $candidateEvaluation.candidateReportAnalysis.analyzedReportCount -ne 0 -or
        $candidateEvaluation.candidateReportAnalysis.unavailableReportCount -ne 1 -or
        $candidateEvaluation.candidateReportAnalysis.blockedReportCount -ne 0) {
        throw 'Expected evaluate-candidate to summarize unavailable candidate report analysis for raw reports.'
    }

    if (-not ($candidateEvaluation.candidateReportAnalysis.cases |
        Where-Object { $_.caseId -eq 'item-1/source-1' -and $_.status -eq 'unavailable' })) {
        throw 'Expected evaluate-candidate candidate report-analysis summary to include unavailable raw-report case.'
    }

    if ($null -eq $candidateEvaluation.evidenceGates -or $candidateEvaluation.evidenceGates.Count -ne 7) {
        throw 'Expected playback quality CLI evaluate-candidate to emit seven evidence gates.'
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

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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

    if ($null -eq $missingEnvironmentEvaluation.activeGate -or
        $missingEnvironmentEvaluation.activeGate.name -ne 'suite' -or
        $missingEnvironmentEvaluation.activeGate.status -ne 'blocked' -or
        $missingEnvironmentEvaluation.activeGate.risk -ne 'high') {
        throw 'Expected missing build identity active gate to point at blocked suite.'
    }

    if ($null -eq $missingEnvironmentEvaluation.activeGate.environment -or
        $missingEnvironmentEvaluation.activeGate.environment.missingEvidenceCount -ne 1) {
        throw 'Expected missing build identity active gate to expose environment missing evidence count.'
    }

    if (-not ($missingEnvironmentEvaluation.activeGate.signals -contains 'environment.identity')) {
        throw 'Expected missing build identity active gate to include environment identity signal.'
    }

    if (-not ($missingEnvironmentEvaluation.activeGate.blockers -contains 'comparison.environment-evidence-missing')) {
        throw 'Expected missing build identity active gate to include comparison environment blocker.'
    }

    if (-not ($missingEnvironmentEvaluation.activeGate.targetCaseIds -contains 'item-1/source-1')) {
        throw 'Expected missing build identity active gate to include target case id.'
    }

    if (-not ($missingEnvironmentEvaluation.activeGate.codeTargets -contains 'src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportMapper.cs')) {
        throw 'Expected missing build identity active gate to include evidence collection code target.'
    }

    if ($null -eq $missingEnvironmentEvaluation.activeGate.confidence -or
        $missingEnvironmentEvaluation.activeGate.confidence.level -ne 'weak' -or
        $missingEnvironmentEvaluation.activeGate.confidence.weakCount -ne 1) {
        throw 'Expected missing build identity active gate to expose weak suite confidence.'
    }

    if ($null -eq $missingEnvironmentEvaluation.activeGate.resultCounts -or
        $missingEnvironmentEvaluation.activeGate.resultCounts.totalCount -ne 1 -or
        $missingEnvironmentEvaluation.activeGate.resultCounts.improvedCount -ne 1) {
        throw 'Expected missing build identity active gate to keep suite result counts separate from evidence blockers.'
    }

    $missingEnvironmentGateNextActions = @($missingEnvironmentEvaluation.activeGate.nextActions)
    if ($missingEnvironmentGateNextActions.Count -ne 1 -or
        $missingEnvironmentGateNextActions[0].rank -ne 1 -or
        $missingEnvironmentGateNextActions[0].action -ne 'collect-comparable-evidence' -or
        $missingEnvironmentGateNextActions[0].risk -ne 'high' -or
        -not ($missingEnvironmentGateNextActions[0].caseIds -contains 'item-1/source-1') -or
        -not ($missingEnvironmentGateNextActions[0].blockers -contains 'suite.environment-evidence-missing')) {
        throw 'Expected missing build identity active gate to expose ranked suite next action.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationPartialEnvironmentBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $candidateEvaluationPartialEnvironmentCandidateDir | Out-Null
    $partialEnvironmentBaseline = Get-Content -Raw -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') | ConvertFrom-Json
    $partialEnvironmentCandidate = Get-Content -Raw -LiteralPath (Join-Path $runIdCandidateDir 'candidate-renamed.json') | ConvertFrom-Json
    $partialEnvironmentCandidate.PSObject.Properties.Remove('environment')
    $partialEnvironmentBaseline | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $candidateEvaluationPartialEnvironmentBaselineDir 'baseline-a.json') -Encoding UTF8
    $partialEnvironmentCandidate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $candidateEvaluationPartialEnvironmentCandidateDir 'candidate-a.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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

    if ($null -eq $partialEnvironmentEvaluation.activeGate -or
        $partialEnvironmentEvaluation.activeGate.name -ne 'suite' -or
        $partialEnvironmentEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected partial build identity active gate to point at blocked suite.'
    }

    if ($null -eq $partialEnvironmentEvaluation.activeGate.environment -or
        $partialEnvironmentEvaluation.activeGate.environment.partialCount -ne 1) {
        throw 'Expected partial build identity active gate to expose environment partial count.'
    }

    if (-not ($partialEnvironmentEvaluation.activeGate.blockers -contains 'suite.environment-evidence-missing')) {
        throw 'Expected partial build identity active gate to include environment evidence blocker.'
    }

    if (-not ($partialEnvironmentEvaluation.activeGate.blockers -contains 'comparison.environment-evidence-missing')) {
        throw 'Expected partial build identity active gate to include comparison environment blocker.'
    }

    $partialEnvironmentComparisonPath = Join-Path $candidateEvaluationPartialEnvironmentComparisonsDir 'item-1\source-1.json'
    if (-not (Test-Path -LiteralPath $partialEnvironmentComparisonPath)) {
        throw 'Expected partial build identity evaluate-candidate to write per-case comparison output.'
    }

    $partialEnvironmentComparison = Get-Content -Raw -LiteralPath $partialEnvironmentComparisonPath | ConvertFrom-Json
    if (-not ($partialEnvironmentComparison.optimization.blockers -contains 'comparison.environment-evidence-missing')) {
        throw 'Expected partial build identity comparison output to include machine-readable environment blocker.'
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

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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
        $sameBuildEvaluation.activeGate.environment.sameBuildCount -ne 1) {
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "width": 3840,
      "height": 2160,
      "frameRate": 23.976,
      "hdrKind": "Sdr"
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
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "width": 3840,
      "height": 2160,
      "frameRate": 23.976,
      "hdrKind": "Sdr"
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

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Hdr10"
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

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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

    if (-not ($invalidCandidateGate.codeTargets -contains 'src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs')) {
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
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "width": 3840,
      "height": 2160,
      "frameRate": 23.976,
      "hdrKind": "Sdr"
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
    "analyzerVersion": 1,
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
          "src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportComposer.cs"
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

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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
        $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.totalReportCount -ne 1 -or
        $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.analyzedReportCount -ne 1 -or
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
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "width": 3840,
      "height": 2160,
      "frameRate": 23.976,
      "hdrKind": "Sdr"
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
    "analyzerVersion": 1,
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
          "src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs"
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

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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

    if (-not ($blockedAnalysisEvaluation.activeGate.codeTargets -contains 'src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs')) {
        throw 'Expected blocked report-analysis active gate to include source classification code target.'
    }

    if (-not ($blockedAnalysisEvaluation.activeGate.suggestedNextActions -contains 'Collect comparable source metadata before optimizing playback Core.')) {
        throw 'Expected blocked report-analysis active gate to include model analysis suggested next action.'
    }

    if ($null -eq $blockedAnalysisEvaluation.candidateReportAnalysis -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.totalReportCount -ne 1 -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.analyzedReportCount -ne 1 -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.unavailableReportCount -ne 0 -or
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

    if (-not ($blockedAnalysisCase.codeTargets -contains 'src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs')) {
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

    if (-not ($blockedAnalysisGate.codeTargets -contains 'src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs')) {
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
    $candidateEvaluationStallPreviousComparisonPath = Join-Path $candidateEvaluationStallPreviousComparisonsDir 'item-1\source-1.json'
    New-Item -ItemType Directory -Path (Split-Path -Parent $candidateEvaluationStallPreviousComparisonPath) | Out-Null

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare-suite `
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
    if ($stallSuite.action -ne 'change-optimization-strategy') {
        throw 'Expected playback quality CLI compare-suite stall action to change optimization strategy.'
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
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- validate-manifest `
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

    if ($exampleManifestValidation.coverage.status -ne 'ready' -or
        $exampleManifestValidation.coverage.isCoreEvaluationReady -ne $true) {
        throw 'Expected example reference manifest coverage to be ready for Core evaluation.'
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
        'buffering'
    )
    foreach ($purpose in $requiredPurposes) {
        if (-not ($exampleManifestValidation.coverage.coveredPurposes -contains $purpose)) {
            throw ('Expected example reference manifest to cover purpose: ' + $purpose)
        }
    }

    if (-not ($exampleManifestValidation.cases | Where-Object {
        $_.caseId -eq 'jellyfin/hdr10-hevc-main10-4k60-50m' -and
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
        $_.expected.hdrOutput -eq 'Sdr'
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

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- plan-runs `
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
        $_.devCommand -eq $null -and
        ($_.requiredSignals -contains 'buffers.videoStarvedPasses') -and
        ($_.requiredSignals -contains 'buffers.audioStarvedPasses') -and
        ($_.requiredSignals -contains 'colorPipeline.actualHdrOutput') -and
        ($_.requiredSignals -contains 'display.hdrStatus') -and
        ($_.requiredSignals -contains 'colorPipeline.swapChainFormat') -and
        ($_.requiredSignals -contains 'colorPipeline.swapChainColorSpace') -and
        ($_.requiredSignals -contains 'colorPipeline.isTenBitSwapChain') -and
        ($_.requiredSignals -contains 'colorPipeline.dxgiInput')
    })) {
        throw 'Expected example reference run plan to schedule public Jellyfin media as direct-uri.'
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

    Write-Output 'playback-quality-cli smoke ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
