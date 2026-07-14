param(
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$ReportsDir,
    [Parameter(Mandatory = $true)][string]$NativeHelperExe,
    [string]$SummaryPath = '',
    [string]$HeadlessProjectPath = '',
    [string]$CliProjectPath = '',
    [string]$SourceResolverProjectPath = '',
    [string]$SourceResolverScriptPath = '',
    [string]$RuntimeSourceMapPath = '',
    [string]$HarnessScriptPath = '',
    [string[]]$CaseId = @(),
    [string[]]$Category = @('stable', 'challenge'),
    [switch]$EnableSeekPacketCache,
    [int]$DurationSeconds = 10,
    [int]$AttemptTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$modulePath = Join-Path $PSScriptRoot 'NativeHeadlessHarness.psm1'
Import-Module $modulePath -Force

function Get-PlaybackQualityReportResult([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return 'missing'
    }

    try {
        $value = [string](Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json).report.result
        $normalized = $value.Trim().ToLowerInvariant()
        if ($normalized -in @('pass', 'fail', 'error', 'skip', 'unsupported')) {
            return $normalized
        }
    }
    catch {
    }

    return 'unknown'
}

if ([string]::IsNullOrWhiteSpace($HeadlessProjectPath)) {
    $HeadlessProjectPath = Join-Path $repoRoot `
        'tools\NoiraPlayer.PlaybackQuality.Headless\NoiraPlayer.PlaybackQuality.Headless.csproj'
}

if ([string]::IsNullOrWhiteSpace($CliProjectPath)) {
    $CliProjectPath = Join-Path $repoRoot `
        'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj'
}

if ([string]::IsNullOrWhiteSpace($SourceResolverProjectPath)) {
    $SourceResolverProjectPath = Join-Path $repoRoot `
        'tools\NoiraPlayer.PlaybackQuality.Runner\NoiraPlayer.PlaybackQuality.Runner.csproj'
}

if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    $SummaryPath = Join-Path $ReportsDir 'manifest-run-summary.json'
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw ('Manifest was not found: ' + $ManifestPath)
}

if (-not (Test-Path -LiteralPath $NativeHelperExe)) {
    throw ('Native helper was not found: ' + $NativeHelperExe)
}

if (-not [string]::IsNullOrWhiteSpace($HarnessScriptPath) -and
    -not (Test-Path -LiteralPath $HarnessScriptPath)) {
    throw ('Harness script was not found: ' + $HarnessScriptPath)
}

if (-not [string]::IsNullOrWhiteSpace($SourceResolverScriptPath) -and
    -not (Test-Path -LiteralPath $SourceResolverScriptPath)) {
    throw ('Source resolver script was not found: ' + $SourceResolverScriptPath)
}

if (-not [string]::IsNullOrWhiteSpace($RuntimeSourceMapPath) -and
    -not (Test-Path -LiteralPath $RuntimeSourceMapPath)) {
    throw ('Runtime source map was not found: ' + $RuntimeSourceMapPath)
}

if ($DurationSeconds -le 0) {
    throw 'DurationSeconds must be positive.'
}
if ($AttemptTimeoutSeconds -lt $DurationSeconds -or $AttemptTimeoutSeconds -gt 1800) {
    throw 'AttemptTimeoutSeconds must be at least DurationSeconds and no greater than 1800.'
}

$manifestValidationPath = $SummaryPath + '.manifest-validation.json'
$validationOutput = @(& dotnet run --project $CliProjectPath --no-restore -- `
    validate-manifest `
    --manifest $ManifestPath `
    --output $manifestValidationPath 2>&1)
$manifestValidationExitCode = $LASTEXITCODE
$validationOutput | ForEach-Object { Write-Host $_ }
if ($manifestValidationExitCode -ne 0) {
    throw ('Manifest validation failed; see ' + $manifestValidationPath)
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) {
    throw ('Unsupported manifest schemaVersion: ' + $manifest.schemaVersion)
}

$runtimeSourceMap = if ([string]::IsNullOrWhiteSpace($RuntimeSourceMapPath)) {
    $null
}
else {
    Get-Content -LiteralPath $RuntimeSourceMapPath -Raw -Encoding UTF8 | ConvertFrom-Json
}

$categorySet = @{}
foreach ($value in $Category) {
    $normalized = ([string]$value).Trim().ToLowerInvariant()
    if (-not [string]::IsNullOrWhiteSpace($normalized)) {
        $categorySet[$normalized] = $true
    }
}

$caseIdSet = @{}
foreach ($value in $CaseId) {
    $normalized = ([string]$value).Trim()
    if (-not [string]::IsNullOrWhiteSpace($normalized)) {
        $caseIdSet[$normalized] = $true
    }
}

$selectedCases = @($manifest.cases | Where-Object {
    $currentCaseId = ([string]$_.caseId).Trim()
    $currentCategory = ([string]$_.category).Trim().ToLowerInvariant()
    $categorySet.ContainsKey($currentCategory) -and
        ($caseIdSet.Count -eq 0 -or $caseIdSet.ContainsKey($currentCaseId))
})

New-Item -ItemType Directory -Path $ReportsDir -Force | Out-Null
$attempts = [System.Collections.Generic.List[object]]::new()
$resolvedSourceCount = 0

foreach ($case in $selectedCases) {
    $currentCaseId = ([string]$case.caseId).Trim()
    $sourceLocator = ([string]$case.uri).Trim()
    $streamUrl = $sourceLocator
    $scenario = ([string]$case.executionRequirement.scenario).Trim().ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($scenario)) {
        $scenario = 'playback'
    }
    $reportPath = Get-PlaybackQualityReportPath -ReportsDir $ReportsDir -CaseId $currentCaseId
    $startedAt = [DateTimeOffset]::UtcNow
    $sourceResolutionAttemptCount = 0

    $runtimeOverride = if ($null -eq $runtimeSourceMap) {
        $null
    }
    else {
        $runtimeSourceMap.PSObject.Properties[$currentCaseId]
    }
    if ($null -ne $runtimeOverride -and
        -not [string]::IsNullOrWhiteSpace([string]$runtimeOverride.Value)) {
        $streamUrl = ([string]$runtimeOverride.Value).Trim()
    }

    if ($null -eq $runtimeOverride -and
        $sourceLocator.StartsWith('emby://', [System.StringComparison]::OrdinalIgnoreCase)) {
        $resolvedSource = Resolve-PlaybackQualityEmbySource `
            -ItemId ([string]$case.itemId) `
            -MediaSourceId ([string]$case.mediaSourceId) `
            -ResolverProjectPath $SourceResolverProjectPath `
            -ResolverScriptPath $SourceResolverScriptPath
        $sourceResolutionAttemptCount = [int]$resolvedSource.AttemptCount
        if ($resolvedSource.Succeeded) {
            $streamUrl = $resolvedSource.StreamUrl
            $resolvedSourceCount++
        }
        else {
            $errorReportExitCode = Write-PlaybackQualitySourceResolutionError `
                -CaseId $currentCaseId `
                -SourceLocator $sourceLocator `
                -ReportsDir $ReportsDir `
                -ErrorCode $resolvedSource.ErrorCode `
                -Scenario $scenario `
                -ResolverProjectPath $SourceResolverProjectPath
            $attempts.Add([pscustomobject]@{
                caseId = $currentCaseId
                status = 'unresolved-source'
                exitCode = $errorReportExitCode
                reportPresent = (Test-Path -LiteralPath $reportPath)
                reportResult = Get-PlaybackQualityReportResult $reportPath
                durationMs = 0
                errorCode = $resolvedSource.ErrorCode
                sourceResolutionAttemptCount = $sourceResolutionAttemptCount
            })
            continue
        }
    }

    if ($null -eq $runtimeOverride -and
        $sourceLocator.StartsWith('local-fault://', [System.StringComparison]::OrdinalIgnoreCase)) {
        $errorCode = 'runtime-source-map-required'
        $errorReportExitCode = Write-PlaybackQualitySourceResolutionError `
            -CaseId $currentCaseId `
            -SourceLocator $sourceLocator `
            -ReportsDir $ReportsDir `
            -ErrorCode $errorCode `
            -Scenario $scenario `
            -ResolverProjectPath $SourceResolverProjectPath
        $attempts.Add([pscustomobject]@{
            caseId = $currentCaseId
            status = 'unresolved-source'
            exitCode = $errorReportExitCode
            reportPresent = (Test-Path -LiteralPath $reportPath)
            reportResult = Get-PlaybackQualityReportResult $reportPath
            durationMs = 0
            errorCode = $errorCode
            sourceResolutionAttemptCount = 0
        })
        continue
    }

    if ([string]::IsNullOrWhiteSpace($streamUrl)) {
        $attempts.Add([pscustomobject]@{
            caseId = $currentCaseId
            status = 'unresolved-source'
            exitCode = $null
            reportPresent = (Test-Path -LiteralPath $reportPath)
            reportResult = Get-PlaybackQualityReportResult $reportPath
            durationMs = 0
            sourceResolutionAttemptCount = $sourceResolutionAttemptCount
        })
        continue
    }

    $locatorHash = Get-PlaybackQualitySourceFingerprint -Locator $sourceLocator
    $pauseSeconds = if ($null -eq $case.pauseSeconds) { 0 } else { [int]$case.pauseSeconds }
    $startPositionTicks = if ($null -eq $case.startPositionTicks) { 0 } else { [long]$case.startPositionTicks }
    if ($pauseSeconds -lt 0 -or $pauseSeconds -gt 900) {
        throw ('pauseSeconds must be between 0 and 900 for case ' + $currentCaseId)
    }
    if ($startPositionTicks -lt 0) {
        throw ('startPositionTicks must be non-negative for case ' + $currentCaseId)
    }
    $referenceCaseJson = $case | ConvertTo-Json -Depth 20 -Compress
    $referenceCaseBase64 = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($referenceCaseJson))
    $exitCode = Invoke-NativeHeadlessHarnessCase `
        -CaseId $currentCaseId `
        -StreamUrl $streamUrl `
        -SourceLocatorHash $locatorHash `
        -ReportsDir $ReportsDir `
        -NativeHelperExe $NativeHelperExe `
        -HeadlessProjectPath $HeadlessProjectPath `
        -ReferenceCaseBase64 $referenceCaseBase64 `
        -HarnessScriptPath $HarnessScriptPath `
        -DurationSeconds $DurationSeconds `
        -StartPositionTicks $startPositionTicks `
        -PauseSeconds $pauseSeconds `
        -Scenario $scenario `
        -TimeoutSeconds $AttemptTimeoutSeconds `
        -ForceSdrOutput ([bool]$case.forceSdrOutput) `
        -EnableSeekPacketCache ([bool]$EnableSeekPacketCache)
    $reportPresent = Test-Path -LiteralPath $reportPath
    $attempts.Add([pscustomobject]@{
        caseId = $currentCaseId
        status = $(if ($exitCode -eq 0) { 'completed' } else { 'failed' })
        exitCode = $exitCode
        reportPresent = $reportPresent
        reportResult = Get-PlaybackQualityReportResult $reportPath
        durationMs = [Math]::Max(0, ([DateTimeOffset]::UtcNow - $startedAt).TotalMilliseconds)
        sourceResolutionAttemptCount = $sourceResolutionAttemptCount
    })
}

$failedAttemptCount = @($attempts | Where-Object { $_.status -eq 'failed' }).Count
$unresolvedSourceCount = @($attempts | Where-Object { $_.status -eq 'unresolved-source' }).Count
$reportCount = @($attempts | Where-Object { $_.reportPresent }).Count
$missingReportCount = $attempts.Count - $reportCount
$passReportCount = @($attempts | Where-Object reportResult -eq 'pass').Count
$failReportCount = @($attempts | Where-Object reportResult -eq 'fail').Count
$errorReportCount = @($attempts | Where-Object reportResult -eq 'error').Count
$skipReportCount = @($attempts | Where-Object reportResult -eq 'skip').Count
$unsupportedReportCount = @($attempts | Where-Object reportResult -eq 'unsupported').Count
$unknownReportCount = @($attempts | Where-Object reportResult -eq 'unknown').Count
$summary = [ordered]@{
    schemaVersion = 1
    runnerVersion = 'native-manifest-runner-v0.1'
    durationSeconds = $DurationSeconds
    attemptTimeoutSeconds = $AttemptTimeoutSeconds
    seekPacketCacheEnabled = [bool]$EnableSeekPacketCache
    selectedCaseCount = $selectedCases.Count
    attemptedCaseCount = @($attempts | Where-Object { $_.status -ne 'unresolved-source' }).Count
    completedAttemptCount = @($attempts | Where-Object { $_.status -eq 'completed' }).Count
    failedAttemptCount = $failedAttemptCount
    unresolvedSourceCount = $unresolvedSourceCount
    resolvedSourceCount = $resolvedSourceCount
    reportCount = $reportCount
    missingReportCount = $missingReportCount
    passReportCount = $passReportCount
    failReportCount = $failReportCount
    errorReportCount = $errorReportCount
    skipReportCount = $skipReportCount
    unsupportedReportCount = $unsupportedReportCount
    unknownReportCount = $unknownReportCount
    nonPassReportCount = $reportCount - $passReportCount
    attempts = @($attempts)
}

$summaryDirectory = Split-Path -Parent $SummaryPath
if (-not [string]::IsNullOrWhiteSpace($summaryDirectory)) {
    New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $SummaryPath -Encoding UTF8

Write-Output ('selected=' + $summary.selectedCaseCount)
Write-Output ('attempted=' + $summary.attemptedCaseCount)
Write-Output ('reports=' + $summary.reportCount)
Write-Output ('failed=' + $summary.failedAttemptCount)
Write-Output ('unresolved=' + $summary.unresolvedSourceCount)
Write-Output ('missingReports=' + $summary.missingReportCount)

if ($selectedCases.Count -eq 0 -or
    $failedAttemptCount -gt 0 -or
    $unresolvedSourceCount -gt 0 -or
    $missingReportCount -gt 0) {
    exit 1
}

exit 0
