$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$baselineScriptPath = Join-Path $repoRoot 'tools\quality-run\New-PlaybackCoreTuningBaseline.ps1'
$comparisonScriptPath = Join-Path $repoRoot 'tools\quality-run\Compare-PlaybackCoreTuningCandidate.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('playback-core-tuning-candidate-comparison-test-' + [Guid]::NewGuid().ToString('N'))
$baselineRoot = Join-Path $tempRoot 'baseline'
$candidateRoot = Join-Path $tempRoot 'candidate'
$comparisonRoot = Join-Path $tempRoot 'comparison'

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

    powershell -NoProfile -ExecutionPolicy Bypass -File $comparisonScriptPath `
        -BaselineRoot $baselineRoot `
        -CandidateRoot $candidateRoot `
        -OutputRoot $comparisonRoot
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
