$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$baselineScriptPath = Join-Path $repoRoot 'tools\quality-run\New-PlaybackCoreTuningBaseline.ps1'
$comparisonScriptPath = Join-Path $repoRoot 'tools\quality-run\Compare-PlaybackCoreTuningCandidate.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('playback-core-tuning-candidate-comparison-test-' + [Guid]::NewGuid().ToString('N'))
$baselineRoot = Join-Path $tempRoot 'baseline'
$candidateRoot = Join-Path $tempRoot 'candidate'
$comparisonRoot = Join-Path $tempRoot 'comparison'
$candidateCadenceStabilityPath = Join-Path $tempRoot 'candidate-cadence-stability.local.json'

try {
    powershell -NoProfile -ExecutionPolicy Bypass -File $baselineScriptPath `
        -NoPrivateManifest `
        -SkipNativeHeadless `
        -OutputRoot $baselineRoot `
        -SourceRevision 'baseline-test-revision'
    if ($LASTEXITCODE -ne 0) {
        throw 'New-PlaybackCoreTuningBaseline.ps1 returned a non-zero exit code for baseline.'
    }

    powershell -NoProfile -ExecutionPolicy Bypass -File $baselineScriptPath `
        -NoPrivateManifest `
        -SkipNativeHeadless `
        -OutputRoot $candidateRoot `
        -SourceRevision 'candidate-test-revision'
    if ($LASTEXITCODE -ne 0) {
        throw 'New-PlaybackCoreTuningBaseline.ps1 returned a non-zero exit code for candidate.'
    }

    @'
{
  "schemaVersion": 1,
  "kind": "playback-cadence-stability-summary",
  "minimumSamples": 3,
  "materialityMs": 2.0,
  "totalGroupCount": 1,
  "stableGroupCount": 0,
  "unstableGroupCount": 1,
  "insufficientSampleGroupCount": 0,
  "unstableCaseGroupIds": [
    "local/native-headless-hdr10-60"
  ],
  "groups": [
    {
      "caseGroupId": "local/native-headless-hdr10-60",
      "stability": "unstable",
      "sampleCount": 3,
      "renderIntervalP99ExpectedErrorSpreadMs": 3.2,
      "maxFrameGapExpectedErrorSpreadMs": 3.2,
      "audioAheadWaitOversleepP95SpreadMs": 4.4,
      "audioAheadWaitFinalDeltaAbsP95SpreadMs": 5.5,
      "unstableSignals": [
        "framePacing.renderIntervalP99ExpectedErrorMs",
        "framePacing.maxFrameGapExpectedErrorMs",
        "timing.audioAheadWaitOversleepMsP95",
        "timing.audioAheadWaitFinalDeltaAbsMsP95"
      ]
    }
  ]
}
'@ | Set-Content -LiteralPath $candidateCadenceStabilityPath -Encoding UTF8

    powershell -NoProfile -ExecutionPolicy Bypass -File $comparisonScriptPath `
        -BaselineRoot $baselineRoot `
        -CandidateRoot $candidateRoot `
        -OutputRoot $comparisonRoot `
        -CandidateCadenceStabilityPath $candidateCadenceStabilityPath
    $comparisonExitCode = $LASTEXITCODE
    if ($comparisonExitCode -ne 2) {
        throw ('Expected public-only core-probe comparison to exit 2 for insufficient native playback evidence, actual: ' + $comparisonExitCode)
    }

    $summaryPath = Join-Path $comparisonRoot 'comparison-summary.local.json'
    $evaluationPath = Join-Path $comparisonRoot 'summaries\candidate-evaluation.local.json'
    $comparisonsDir = Join-Path $comparisonRoot 'comparisons'
    foreach ($path in @($summaryPath, $evaluationPath, $comparisonsDir)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw ('Expected comparison artifact was not written: ' + $path)
        }
    }

    $summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
    if ($summary.kind -ne 'playback-core-tuning-candidate-comparison') {
        throw 'Expected comparison summary kind.'
    }

    if ($summary.manifestComparison.sameCaseIds -ne $true) {
        throw 'Expected baseline and candidate manifests to have the same case IDs.'
    }

    if ($summary.baselineValidation.isValid -ne $true) {
        throw 'Expected baseline report-set validation to be valid.'
    }

    if ($summary.candidateValidation.isValid -ne $true) {
        throw 'Expected candidate report-set validation to be valid.'
    }

    if ($summary.evaluation.action -ne 'collect-comparable-evidence' -or
        $summary.evaluation.decision -ne 'collect-comparable-evidence') {
        throw 'Expected public-only core-probe candidate evaluation to request comparable native playback evidence.'
    }

    if ($summary.evaluation.totalComparisonCount -ne 0) {
        throw 'Expected candidate evaluation to skip suite comparisons when native playback evidence is insufficient.'
    }

    if ($summary.evaluation.activeGateStatus -ne 'blocked' -or $summary.evaluation.blockerCount -lt 1) {
        throw 'Expected candidate evaluation active gate to be blocked by insufficient evidence.'
    }

    if ($summary.cadenceStability.candidate.present -ne $true -or
        $summary.cadenceStability.candidate.unstableGroupCount -ne 1 -or
        -not ($summary.cadenceStability.candidate.unstableCaseGroupIds -contains 'local/native-headless-hdr10-60')) {
        throw 'Expected comparison summary to preserve candidate cadence stability evidence.'
    }

    if ($null -eq $summary.cadenceStability.attribution -or
        -not ($summary.cadenceStability.attribution.candidateUnstableCaseGroupIds -contains 'local/native-headless-hdr10-60')) {
        throw 'Expected comparison summary to expose cadence stability attribution for model consumers.'
    }

    $candidateCadenceGroup = $summary.cadenceStability.candidate.groups |
        Where-Object { $_.caseGroupId -eq 'local/native-headless-hdr10-60' } |
        Select-Object -First 1
    if ($candidateCadenceGroup.audioAheadWaitOversleepP95SpreadMs -ne 4.4) {
        throw 'Expected comparison summary to preserve A/V oversleep stability spread evidence.'
    }

    if ($candidateCadenceGroup.audioAheadWaitFinalDeltaAbsP95SpreadMs -ne 5.5) {
        throw 'Expected comparison summary to preserve A/V final delta stability spread evidence.'
    }

    if ($summary.paths.candidateCadenceStabilityPath -ne $candidateCadenceStabilityPath) {
        throw 'Expected comparison summary paths to include candidate cadence stability path.'
    }

    $evaluation = Get-Content -Raw -LiteralPath $evaluationPath | ConvertFrom-Json
    if ($evaluation.schemaVersion -ne 1 -or $evaluation.evaluationVersion -ne 'playback-quality-v0.1') {
        throw 'Expected candidate evaluation schema and version.'
    }

    if ($evaluation.baselineReportSetValidation.isValid -ne $true -or
        $evaluation.candidateReportSetValidation.isValid -ne $true) {
        throw 'Expected candidate evaluation report-set gates to be valid.'
    }

    if (-not ($evaluation.blockers -contains 'baseline-playback-evidence.insufficient') -or
        -not ($evaluation.blockers -contains 'candidate-playback-evidence.insufficient')) {
        throw 'Expected public-only core-probe comparison to preserve insufficient playback evidence blockers.'
    }

    Write-Output 'playback-core-tuning-candidate-comparison tests ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
