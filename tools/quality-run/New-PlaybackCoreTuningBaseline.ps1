param(
    [string]$PublicManifestPath = '',
    [string[]]$PrivateManifestPath = @(),
    [string[]]$AdditionalManifestPath = @(),
    [switch]$NoPrivateManifest,
    [switch]$RequirePrivateManifest,
    [switch]$SkipNativeHeadless,
    [string]$OutputRoot = '',
    [switch]$Clean,
    [string]$PlayerCoreVersion = 'NoiraPlayer.Core',
    [string]$BuildConfiguration = 'Debug',
    [string]$SourceRevision = '',
    [string]$NativeHelperExe = '',
    [string]$ManifestRunnerHarnessScriptPath = '',
    [string]$SourceResolverScriptPath = '',
    [switch]$EnableSeekPacketCache,
    [int]$DurationSeconds = 10,
    [int]$AttemptTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$cliProject = Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj'
$mergeScript = Join-Path $repoRoot 'tools\quality-run\Merge-ReferenceManifests.ps1'
$manifestRunnerScript = Join-Path $repoRoot 'tools\quality-run\Invoke-PlaybackQualityManifest.ps1'
$nativeHeadlessScript = Join-Path $repoRoot 'tools\quality-run\run-native-headless-harness-smoke-test.ps1'
$nativeSmokeRoot = Join-Path $repoRoot 'artifacts\quality-run\native-headless-smoke'
$nativeManifestPath = Join-Path $nativeSmokeRoot 'native-manifest.json'
$nativeMaterializedDir = Join-Path $nativeSmokeRoot 'native-materialized'
$generatedNativeHelperExe = Join-Path $nativeSmokeRoot 'native-helper\NativePlaybackGraphHeadlessSmokeTests.exe'

if ([string]::IsNullOrWhiteSpace($PublicManifestPath)) {
    $PublicManifestPath = Join-Path $repoRoot 'docs\qa\playback-quality-reference-manifest.example.json'
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'docs\qa\private\baselines\playback-core-tuning-baseline.local'
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
    [string[]]$Arguments
) {
    Write-Host ('running=' + $Command + ' ' + ($Arguments -join ' '))
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ($Command + ' failed with exit code ' + $LASTEXITCODE)
    }
}

function Get-SourceRevision() {
    $explicit = Normalize-String $SourceRevision
    if (-not [string]::IsNullOrWhiteSpace($explicit)) {
        return $explicit
    }

    Push-Location $repoRoot
    try {
        $revision = (& git rev-parse --short HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($revision)) {
            return 'unknown'
        }

        $status = & git status --porcelain
        if ($LASTEXITCODE -eq 0 -and @($status).Count -gt 0) {
            return $revision + '-dirty'
        }

        return $revision
    }
    finally {
        Pop-Location
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

function Add-ManifestIfExists(
    [System.Collections.Generic.List[string]]$Manifests,
    [string]$Path,
    [bool]$Required,
    [System.Collections.Generic.List[string]]$Warnings
) {
    $resolved = Resolve-RepoPath $Path
    if ([string]::IsNullOrWhiteSpace($resolved)) {
        return
    }

    if (Test-Path -LiteralPath $resolved) {
        $Manifests.Add((Resolve-Path -LiteralPath $resolved).Path)
        return
    }

    if ($Required) {
        throw ('Required manifest was not found: ' + $resolved)
    }

    $Warnings.Add('manifest not found and skipped: ' + $resolved)
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Copy-DirectoryContents(
    [string]$Source,
    [string]$Destination
) {
    if (-not (Test-Path -LiteralPath $Source)) {
        throw ('Source directory was not found: ' + $Source)
    }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

$resolvedOutputRoot = Resolve-RepoPath $OutputRoot
if ((Test-Path -LiteralPath $resolvedOutputRoot) -and -not $Clean) {
    throw ('Output root already exists. Re-run with -Clean or choose another -OutputRoot: ' + $resolvedOutputRoot)
}

if ((Test-Path -LiteralPath $resolvedOutputRoot) -and $Clean) {
    Assert-SafeCleanTarget $resolvedOutputRoot
    Remove-Item -LiteralPath $resolvedOutputRoot -Recurse -Force
}

$manifestsDir = Join-Path $resolvedOutputRoot 'manifests'
$reportsDir = Join-Path $resolvedOutputRoot 'reports'
$capturedReportsDir = Join-Path $resolvedOutputRoot 'captured-reports'
$summariesDir = Join-Path $resolvedOutputRoot 'summaries'
$coreManifestPath = Join-Path $manifestsDir 'core-reference-manifest.local.json'
$executedCoreManifestPath = Join-Path $manifestsDir 'executed-core-manifest.local.json'
$unifiedManifestPath = Join-Path $manifestsDir 'unified-reference-manifest.local.json'
$manifestRunSummaryPath = Join-Path $summariesDir 'manifest-run-summary.local.json'
$coreMaterializedSummaryPath = Join-Path $summariesDir 'core-materialized-summary.local.json'
$validationPath = Join-Path $summariesDir 'report-set-validation.local.json'
$analysisPath = Join-Path $summariesDir 'report-analysis-summary.local.json'
$runPlanPath = Join-Path $summariesDir 'run-plan.local.json'
$summaryPath = Join-Path $resolvedOutputRoot 'baseline-summary.local.json'

New-Item -ItemType Directory -Path $manifestsDir -Force | Out-Null
New-Item -ItemType Directory -Path $reportsDir -Force | Out-Null
New-Item -ItemType Directory -Path $capturedReportsDir -Force | Out-Null
New-Item -ItemType Directory -Path $summariesDir -Force | Out-Null

$warnings = [System.Collections.Generic.List[string]]::new()
$coreManifestInputs = [System.Collections.Generic.List[string]]::new()
Add-ManifestIfExists $coreManifestInputs $PublicManifestPath $true $warnings

if (-not $NoPrivateManifest) {
    if ($PrivateManifestPath.Count -eq 0) {
        $PrivateManifestPath = @(Join-Path $repoRoot 'docs\qa\private\emby-reference-manifest.local.json')
    }

    foreach ($manifestPath in $PrivateManifestPath) {
        Add-ManifestIfExists $coreManifestInputs $manifestPath ([bool]$RequirePrivateManifest) $warnings
    }
}

foreach ($manifestPath in $AdditionalManifestPath) {
    Add-ManifestIfExists $coreManifestInputs $manifestPath $true $warnings
}

if ($coreManifestInputs.Count -eq 0) {
    throw 'No public, private, or additional manifests were available.'
}

Invoke-Checked powershell @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $mergeScript,
    '-ManifestPath',
    ($coreManifestInputs -join ','),
    '-OutputPath',
    $coreManifestPath,
    '-DuplicateCaseIdMode',
    'skip'
)

$baselineSourceRevision = Get-SourceRevision
$nativeIncluded = $false
if (-not $SkipNativeHeadless) {
    Invoke-Checked powershell @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $nativeHeadlessScript,
        '-PlayerCoreVersion',
        $PlayerCoreVersion,
        '-SourceRevision',
        $baselineSourceRevision,
        '-BuildConfiguration',
        $BuildConfiguration
    )

    if ([string]::IsNullOrWhiteSpace($NativeHelperExe)) {
        $NativeHelperExe = $generatedNativeHelperExe
    }
}
else {
    $warnings.Add('native-headless local generated samples were skipped by -SkipNativeHeadless')
}

$resolvedNativeHelperExe = Resolve-RepoPath $NativeHelperExe
if ([string]::IsNullOrWhiteSpace($resolvedNativeHelperExe) -or
    -not (Test-Path -LiteralPath $resolvedNativeHelperExe)) {
    throw 'A native helper executable is required to execute every baseline manifest case.'
}
$resolvedNativeHelperExe = (Resolve-Path -LiteralPath $resolvedNativeHelperExe).Path

$manifestRunnerArguments = @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $manifestRunnerScript,
    '-ManifestPath',
    $coreManifestPath,
    '-ReportsDir',
    $capturedReportsDir,
    '-NativeHelperExe',
    $resolvedNativeHelperExe,
    '-SummaryPath',
    $manifestRunSummaryPath,
    '-DurationSeconds',
    ([string]$DurationSeconds),
    '-AttemptTimeoutSeconds',
    ([string]$AttemptTimeoutSeconds),
    '-SourceRevision',
    $baselineSourceRevision
)
if (-not [string]::IsNullOrWhiteSpace($ManifestRunnerHarnessScriptPath)) {
    $manifestRunnerArguments += @(
        '-HarnessScriptPath',
        (Resolve-RepoPath $ManifestRunnerHarnessScriptPath)
    )
}
if (-not [string]::IsNullOrWhiteSpace($SourceResolverScriptPath)) {
    $manifestRunnerArguments += @(
        '-SourceResolverScriptPath',
        (Resolve-RepoPath $SourceResolverScriptPath)
    )
}
if ($EnableSeekPacketCache) {
    $manifestRunnerArguments += '-EnableSeekPacketCache'
}

Write-Host ('running=powershell ' + ($manifestRunnerArguments -join ' '))
& powershell @manifestRunnerArguments
$manifestRunnerExitCode = $LASTEXITCODE
if (-not (Test-Path -LiteralPath $manifestRunSummaryPath)) {
    throw 'Native manifest runner did not write its execution summary.'
}
$manifestRunSummary = Read-JsonFile $manifestRunSummaryPath
$manifestRunnerVersion = ([string]$manifestRunSummary.runnerVersion).Trim()
if ($manifestRunnerVersion -ne 'native-manifest-runner-v0.4') {
    throw ('Unsupported native manifest runner summary version: ' + $manifestRunnerVersion)
}
if ([int]$manifestRunSummary.durationSeconds -ne $DurationSeconds -or
    [int]$manifestRunSummary.attemptTimeoutSeconds -ne $AttemptTimeoutSeconds) {
    throw 'Native manifest runner summary does not match the requested observation window and timeout.'
}
if ([int]$manifestRunSummary.selectedCaseCount -le 0) {
    throw 'Native manifest runner selected no stable/challenge playback cases.'
}
if ([int]$manifestRunSummary.missingReportCount -ne 0 -or
    [int]$manifestRunSummary.reportCount -ne [int]$manifestRunSummary.selectedCaseCount) {
    throw ('Native manifest runner did not produce one report per selected case. selected=' +
        $manifestRunSummary.selectedCaseCount + '; reports=' + $manifestRunSummary.reportCount +
        '; missing=' + $manifestRunSummary.missingReportCount)
}
if ([int]$manifestRunSummary.unattributedReportCount -ne 0 -or
    [int]$manifestRunSummary.invalidReportCount -ne 0) {
    throw ('Native manifest runner produced reports that cannot be attributed to the current attempts. unattributed=' +
        $manifestRunSummary.unattributedReportCount + '; invalid=' +
        $manifestRunSummary.invalidReportCount)
}
if ($manifestRunnerExitCode -ne 0) {
    $warnings.Add('manifest runner captured non-success playback outcomes; strict report validation remains authoritative')
}

$selectedCaseIds = @($manifestRunSummary.attempts | ForEach-Object { [string]$_.caseId })
$selectedCaseIdSet = @{}
foreach ($selectedCaseId in $selectedCaseIds) {
    $selectedCaseIdSet[$selectedCaseId] = $true
}
$coreManifest = Read-JsonFile $coreManifestPath
$executedCoreManifest = [pscustomobject][ordered]@{
    schemaVersion = $coreManifest.schemaVersion
    cases = @($coreManifest.cases | Where-Object {
        $selectedCaseIdSet.ContainsKey([string]$_.caseId)
    })
}
if (@($executedCoreManifest.cases).Count -ne [int]$manifestRunSummary.selectedCaseCount) {
    throw 'Executed manifest does not match the native manifest runner selection.'
}
$executedCoreManifest | ConvertTo-Json -Depth 20 |
    Set-Content -LiteralPath $executedCoreManifestPath -Encoding UTF8

Invoke-Checked dotnet @(
    'run',
    '--project',
    $cliProject,
    '--',
    'materialize-native-harness-report-set',
    '--manifest',
    $executedCoreManifestPath,
    '--captured-reports-dir',
    $capturedReportsDir,
    '--reports-dir',
    $reportsDir,
    '--collector-version',
    $manifestRunnerVersion,
    '--player-core-version',
    $PlayerCoreVersion,
    '--source-revision',
    $baselineSourceRevision,
    '--build-configuration',
    $BuildConfiguration,
    '--output',
    $coreMaterializedSummaryPath
)

$finalManifestInputs = [System.Collections.Generic.List[string]]::new()
$finalManifestInputs.Add($executedCoreManifestPath)
if (-not $SkipNativeHeadless) {

    if (-not (Test-Path -LiteralPath $nativeManifestPath)) {
        throw ('Native-headless manifest was not produced: ' + $nativeManifestPath)
    }

    if (-not (Test-Path -LiteralPath $nativeMaterializedDir)) {
        throw ('Native-headless materialized report directory was not produced: ' + $nativeMaterializedDir)
    }

    $finalManifestInputs.Add((Resolve-Path -LiteralPath $nativeManifestPath).Path)
    Copy-DirectoryContents $nativeMaterializedDir $reportsDir
    $nativeIncluded = $true
}

Invoke-Checked powershell @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $mergeScript,
    '-ManifestPath',
    ($finalManifestInputs -join ','),
    '-OutputPath',
    $unifiedManifestPath,
    '-DuplicateCaseIdMode',
    'skip'
)

$validationArguments = @(
    'run',
    '--project',
    $cliProject,
    '--',
    'validate-report-set',
    '--manifest',
    $unifiedManifestPath,
    '--reports-dir',
    $reportsDir,
    '--output',
    $validationPath
)
Write-Host ('running=dotnet ' + ($validationArguments -join ' '))
& dotnet @validationArguments
$validationExitCode = $LASTEXITCODE
if (-not (Test-Path -LiteralPath $validationPath)) {
    throw 'Strict report-set validation did not write its result.'
}
if ($validationExitCode -ne 0) {
    $warnings.Add('strict report-set validation failed; analysis and run plan were still generated')
}

Invoke-Checked dotnet @(
    'run',
    '--project',
    $cliProject,
    '--',
    'analyze-report-set',
    '--reports-dir',
    $reportsDir,
    '--output',
    $analysisPath
)

Invoke-Checked dotnet @(
    'run',
    '--project',
    $cliProject,
    '--',
    'plan-runs',
    '--manifest',
    $unifiedManifestPath,
    '--reports-dir',
    $reportsDir,
    '--duration',
    '60',
    '--output',
    $runPlanPath
)

$validation = Read-JsonFile $validationPath
$analysis = Read-JsonFile $analysisPath
$summary = [pscustomobject][ordered]@{
    schemaVersion = 1
    kind = 'playback-core-tuning-baseline'
    sourceRevision = $baselineSourceRevision
    playerCoreVersion = $PlayerCoreVersion
    buildConfiguration = $BuildConfiguration
    outputRoot = $resolvedOutputRoot
    publicManifestPath = (Resolve-Path -LiteralPath (Resolve-RepoPath $PublicManifestPath)).Path
    privateManifestPaths = @($coreManifestInputs | Where-Object {
        $_.IndexOf('\docs\qa\private\', [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    additionalManifestPaths = @($AdditionalManifestPath)
    coreExecution = [pscustomobject][ordered]@{
        runner = $manifestRunnerVersion
        durationSeconds = [int]$manifestRunSummary.durationSeconds
        attemptTimeoutSeconds = [int]$manifestRunSummary.attemptTimeoutSeconds
        seekPacketCacheEnabled = [bool]$manifestRunSummary.seekPacketCacheEnabled
        summaryPath = $manifestRunSummaryPath
        selectedCaseCount = [int]$manifestRunSummary.selectedCaseCount
        attemptedCaseCount = [int]$manifestRunSummary.attemptedCaseCount
        reportCount = [int]$manifestRunSummary.reportCount
        failedAttemptCount = [int]$manifestRunSummary.failedAttemptCount
        invalidReportCount = [int]$manifestRunSummary.invalidReportCount
        unattributedReportCount = [int]$manifestRunSummary.unattributedReportCount
        unresolvedSourceCount = [int]$manifestRunSummary.unresolvedSourceCount
        missingReportCount = [int]$manifestRunSummary.missingReportCount
    }
    nativeHeadless = [pscustomobject][ordered]@{
        included = $nativeIncluded
        manifestPath = if ($nativeIncluded) { $nativeManifestPath } else { '' }
        materializedReportsDir = if ($nativeIncluded) { $nativeMaterializedDir } else { '' }
    }
    unifiedManifestPath = $unifiedManifestPath
    reportsDir = $reportsDir
    validationPath = $validationPath
    analysisPath = $analysisPath
    runPlanPath = $runPlanPath
    validation = [pscustomobject][ordered]@{
        isValid = [bool]$validation.isValid
        matchedCaseCount = [int]$validation.matchedCaseCount
        missingReportCount = [int]$validation.missingReportCount
        errorCount = @($validation.errors).Count
    }
    analysis = [pscustomobject][ordered]@{
        totalReportCount = [int]$analysis.totalReportCount
        decision = Normalize-String $analysis.decision
        action = Normalize-String $analysis.action
        risk = Normalize-String $analysis.risk
        playbackEvidenceScope = Normalize-String $analysis.playbackEvidence.scope
        playbackEvidenceStatus = Normalize-String $analysis.playbackEvidence.status
        canEvaluateNativePlayback = [bool]$analysis.playbackEvidence.canEvaluateNativePlayback
    }
    warnings = @($warnings)
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
Write-Output ('wrote playback Core tuning baseline: ' + $summaryPath)
Write-Output ('reports: ' + $summary.analysis.totalReportCount)
Write-Output ('validation.isValid: ' + $summary.validation.isValid)
Write-Output ('nativeHeadless.included: ' + $summary.nativeHeadless.included)

if ($validationExitCode -ne 0 -or -not [bool]$validation.isValid) {
    exit 1
}
