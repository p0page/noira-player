param(
    [string]$BaselineRoot = '',
    [Parameter(Mandatory = $true)]
    [string]$CandidateRoot,
    [string]$ManifestPath = '',
    [string]$OutputRoot = '',
    [ValidateSet('relative-path', 'run-id')]
    [string]$MatchBy = 'run-id',
    [string]$PreviousComparisonsDir = '',
    [int]$StallThreshold = 3,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$cliProject = Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj'

if ([string]::IsNullOrWhiteSpace($BaselineRoot)) {
    $BaselineRoot = Join-Path $repoRoot 'docs\qa\private\baselines\playback-core-tuning-baseline.local'
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'docs\qa\private\comparisons\playback-core-tuning-candidate.local'
}

function Normalize-String([object]$Value) {
    if ($null -eq $Value) {
        return ''
    }

    return ([string]$Value).Trim()
}

function Resolve-RepoPath([string]$Path) {
    $normalized = Normalize-String $Path
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return ''
    }

    if ([System.IO.Path]::IsPathRooted($normalized)) {
        return $normalized
    }

    return (Join-Path $repoRoot $normalized)
}

function Invoke-Checked(
    [string]$Command,
    [string[]]$Arguments,
    [int[]]$AllowedExitCodes = @(0)
) {
    Write-Host ('running=' + $Command + ' ' + ($Arguments -join ' '))
    & $Command @Arguments
    $exitCode = $LASTEXITCODE
    if ($AllowedExitCodes -notcontains $exitCode) {
        throw ($Command + ' failed with exit code ' + $exitCode)
    }

    return $exitCode
}

function Assert-PathExists(
    [string]$Path,
    [string]$Description
) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw ($Description + ' was not found: ' + $Path)
    }
}

function Assert-SafeCleanTarget([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $privateRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'docs\qa\private'))
    $artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())

    if ($fullPath.StartsWith($privateRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return
    }

    throw ('Refusing to clean output root outside private, artifacts, or temp roots: ' + $fullPath)
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-ManifestCaseIds([object]$Manifest) {
    @($Manifest.cases | ForEach-Object { Normalize-String $_.caseId } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique)
}

function Compare-ManifestCaseIds(
    [string]$BaselineManifestPath,
    [string]$CandidateManifestPath
) {
    $baselineManifest = Read-JsonFile $BaselineManifestPath
    $baselineIds = @(Get-ManifestCaseIds $baselineManifest)
    $candidateIds = @()
    $candidateExists = Test-Path -LiteralPath $CandidateManifestPath
    if ($candidateExists) {
        $candidateManifest = Read-JsonFile $CandidateManifestPath
        $candidateIds = @(Get-ManifestCaseIds $candidateManifest)
    }

    $missing = @($baselineIds | Where-Object { $candidateIds -notcontains $_ })
    $extra = @($candidateIds | Where-Object { $baselineIds -notcontains $_ })

    [pscustomobject][ordered]@{
        baselineCaseCount = $baselineIds.Count
        candidateCaseCount = $candidateIds.Count
        candidateManifestExists = $candidateExists
        sameCaseIds = ($candidateExists -and $missing.Count -eq 0 -and $extra.Count -eq 0)
        missingCandidateCaseIds = $missing
        extraCandidateCaseIds = $extra
    }
}

$resolvedBaselineRoot = Resolve-RepoPath $BaselineRoot
$resolvedCandidateRoot = Resolve-RepoPath $CandidateRoot
$resolvedOutputRoot = Resolve-RepoPath $OutputRoot

Assert-PathExists $resolvedBaselineRoot 'Baseline root'
Assert-PathExists $resolvedCandidateRoot 'Candidate root'

if ((Test-Path -LiteralPath $resolvedOutputRoot) -and -not $Clean) {
    throw ('Output root already exists. Re-run with -Clean or choose another -OutputRoot: ' + $resolvedOutputRoot)
}

if ((Test-Path -LiteralPath $resolvedOutputRoot) -and $Clean) {
    Assert-SafeCleanTarget $resolvedOutputRoot
    Remove-Item -LiteralPath $resolvedOutputRoot -Recurse -Force
}

$baselineReportsDir = Join-Path $resolvedBaselineRoot 'reports'
$candidateReportsDir = Join-Path $resolvedCandidateRoot 'reports'
Assert-PathExists $baselineReportsDir 'Baseline reports directory'
Assert-PathExists $candidateReportsDir 'Candidate reports directory'

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $resolvedBaselineRoot 'manifests\unified-reference-manifest.local.json'
}

$resolvedManifestPath = Resolve-RepoPath $ManifestPath
$candidateManifestPath = Join-Path $resolvedCandidateRoot 'manifests\unified-reference-manifest.local.json'
Assert-PathExists $resolvedManifestPath 'Baseline manifest'

$summariesDir = Join-Path $resolvedOutputRoot 'summaries'
$comparisonsDir = Join-Path $resolvedOutputRoot 'comparisons'
$baselineValidationPath = Join-Path $summariesDir 'baseline-report-set-validation.local.json'
$candidateValidationPath = Join-Path $summariesDir 'candidate-report-set-validation.local.json'
$baselineAnalysisPath = Join-Path $summariesDir 'baseline-report-analysis.local.json'
$candidateAnalysisPath = Join-Path $summariesDir 'candidate-report-analysis.local.json'
$evaluationPath = Join-Path $summariesDir 'candidate-evaluation.local.json'
$summaryPath = Join-Path $resolvedOutputRoot 'comparison-summary.local.json'

New-Item -ItemType Directory -Path $summariesDir -Force | Out-Null
New-Item -ItemType Directory -Path $comparisonsDir -Force | Out-Null

$manifestComparison = Compare-ManifestCaseIds $resolvedManifestPath $candidateManifestPath

$null = Invoke-Checked dotnet @(
    'run',
    '--project',
    $cliProject,
    '--',
    'validate-report-set',
    '--manifest',
    $resolvedManifestPath,
    '--reports-dir',
    $baselineReportsDir,
    '--output',
    $baselineValidationPath
)

$null = Invoke-Checked dotnet @(
    'run',
    '--project',
    $cliProject,
    '--',
    'validate-report-set',
    '--manifest',
    $resolvedManifestPath,
    '--reports-dir',
    $candidateReportsDir,
    '--output',
    $candidateValidationPath
)

$null = Invoke-Checked dotnet @(
    'run',
    '--project',
    $cliProject,
    '--',
    'analyze-report-set',
    '--reports-dir',
    $baselineReportsDir,
    '--output',
    $baselineAnalysisPath
)

$null = Invoke-Checked dotnet @(
    'run',
    '--project',
    $cliProject,
    '--',
    'analyze-report-set',
    '--reports-dir',
    $candidateReportsDir,
    '--output',
    $candidateAnalysisPath
)

$evaluateArgs = @(
    'run',
    '--project',
    $cliProject,
    '--',
    'evaluate-candidate',
    '--manifest',
    $resolvedManifestPath,
    '--baseline-dir',
    $baselineReportsDir,
    '--candidate-dir',
    $candidateReportsDir,
    '--match-by',
    $MatchBy,
    '--comparisons-dir',
    $comparisonsDir,
    '--stall-threshold',
    [string]$StallThreshold,
    '--output',
    $evaluationPath
)

if (-not [string]::IsNullOrWhiteSpace($PreviousComparisonsDir)) {
    $evaluateArgs += @('--previous-comparisons-dir', (Resolve-RepoPath $PreviousComparisonsDir))
}

$evaluationExitCode = Invoke-Checked dotnet $evaluateArgs @(0, 2)

$baselineValidation = Read-JsonFile $baselineValidationPath
$candidateValidation = Read-JsonFile $candidateValidationPath
$baselineAnalysis = Read-JsonFile $baselineAnalysisPath
$candidateAnalysis = Read-JsonFile $candidateAnalysisPath
$evaluation = Read-JsonFile $evaluationPath

$suite = $evaluation.suite
$summary = [pscustomobject][ordered]@{
    schemaVersion = 1
    kind = 'playback-core-tuning-candidate-comparison'
    baselineRoot = $resolvedBaselineRoot
    candidateRoot = $resolvedCandidateRoot
    outputRoot = $resolvedOutputRoot
    manifestPath = $resolvedManifestPath
    baselineReportsDir = $baselineReportsDir
    candidateReportsDir = $candidateReportsDir
    matchBy = $MatchBy
    evaluationExitCode = $evaluationExitCode
    manifestComparison = $manifestComparison
    baselineValidation = [pscustomobject][ordered]@{
        isValid = [bool]$baselineValidation.isValid
        matchedCaseCount = [int]$baselineValidation.matchedCaseCount
        missingReportCount = [int]$baselineValidation.missingReportCount
        errorCount = @($baselineValidation.errors).Count
    }
    candidateValidation = [pscustomobject][ordered]@{
        isValid = [bool]$candidateValidation.isValid
        matchedCaseCount = [int]$candidateValidation.matchedCaseCount
        missingReportCount = [int]$candidateValidation.missingReportCount
        errorCount = @($candidateValidation.errors).Count
    }
    baselineAnalysis = [pscustomobject][ordered]@{
        totalReportCount = [int]$baselineAnalysis.totalReportCount
        decision = Normalize-String $baselineAnalysis.decision
        action = Normalize-String $baselineAnalysis.action
        risk = Normalize-String $baselineAnalysis.risk
        playbackEvidenceScope = Normalize-String $baselineAnalysis.playbackEvidence.scope
        playbackEvidenceStatus = Normalize-String $baselineAnalysis.playbackEvidence.status
        canEvaluateNativePlayback = [bool]$baselineAnalysis.playbackEvidence.canEvaluateNativePlayback
    }
    candidateAnalysis = [pscustomobject][ordered]@{
        totalReportCount = [int]$candidateAnalysis.totalReportCount
        decision = Normalize-String $candidateAnalysis.decision
        action = Normalize-String $candidateAnalysis.action
        risk = Normalize-String $candidateAnalysis.risk
        playbackEvidenceScope = Normalize-String $candidateAnalysis.playbackEvidence.scope
        playbackEvidenceStatus = Normalize-String $candidateAnalysis.playbackEvidence.status
        canEvaluateNativePlayback = [bool]$candidateAnalysis.playbackEvidence.canEvaluateNativePlayback
    }
    evaluation = [pscustomobject][ordered]@{
        action = Normalize-String $evaluation.action
        decision = Normalize-String $evaluation.decision
        risk = Normalize-String $evaluation.risk
        blockerCount = @($evaluation.blockers).Count
        activeGateName = Normalize-String $evaluation.activeGate.name
        activeGateStatus = Normalize-String $evaluation.activeGate.status
        activeGateAction = Normalize-String $evaluation.activeGate.action
        totalComparisonCount = [int]$suite.totalComparisonCount
        improvedCount = [int]$suite.improvedCount
        regressedCount = [int]$suite.regressedCount
        mixedCount = [int]$suite.mixedCount
        unchangedCount = [int]$suite.unchangedCount
        insufficientEvidenceCount = [int]$suite.insufficientEvidenceCount
        weakConfidenceCount = [int]$suite.weakConfidenceCount
        partialConfidenceCount = [int]$suite.partialConfidenceCount
        strongConfidenceCount = [int]$suite.strongConfidenceCount
        targetFailureAreas = @($suite.targetFailureAreas)
        targetCaseIds = @($suite.targetCaseIds)
        blockers = @($evaluation.blockers)
    }
    paths = [pscustomobject][ordered]@{
        baselineValidationPath = $baselineValidationPath
        candidateValidationPath = $candidateValidationPath
        baselineAnalysisPath = $baselineAnalysisPath
        candidateAnalysisPath = $candidateAnalysisPath
        evaluationPath = $evaluationPath
        comparisonsDir = $comparisonsDir
    }
}

$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
Write-Output ('wrote playback Core tuning candidate comparison: ' + $summaryPath)
Write-Output ('evaluation.action: ' + $summary.evaluation.action)
Write-Output ('evaluation.decision: ' + $summary.evaluation.decision)
Write-Output ('evaluation.totalComparisonCount: ' + $summary.evaluation.totalComparisonCount)
Write-Output ('manifest.sameCaseIds: ' + $summary.manifestComparison.sameCaseIds)

if ($evaluationExitCode -ne 0) {
    exit $evaluationExitCode
}
