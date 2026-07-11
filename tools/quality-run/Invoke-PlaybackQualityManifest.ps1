param(
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$ReportsDir,
    [Parameter(Mandatory = $true)][string]$NativeHelperExe,
    [string]$SummaryPath = '',
    [string]$HeadlessProjectPath = '',
    [string]$CliProjectPath = '',
    [string]$HarnessScriptPath = '',
    [string[]]$CaseId = @(),
    [string[]]$Category = @('stable', 'challenge'),
    [int]$DurationSeconds = 10
)

$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$modulePath = Join-Path $PSScriptRoot 'NativeHeadlessHarness.psm1'
Import-Module $modulePath -Force

if ([string]::IsNullOrWhiteSpace($HeadlessProjectPath)) {
    $HeadlessProjectPath = Join-Path $repoRoot `
        'tools\NoiraPlayer.PlaybackQuality.Headless\NoiraPlayer.PlaybackQuality.Headless.csproj'
}

if ([string]::IsNullOrWhiteSpace($CliProjectPath)) {
    $CliProjectPath = Join-Path $repoRoot `
        'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj'
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

if ($DurationSeconds -le 0) {
    throw 'DurationSeconds must be positive.'
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

foreach ($case in $selectedCases) {
    $currentCaseId = ([string]$case.caseId).Trim()
    $streamUrl = ([string]$case.uri).Trim()
    $reportPath = Get-PlaybackQualityReportPath -ReportsDir $ReportsDir -CaseId $currentCaseId
    $startedAt = [DateTimeOffset]::UtcNow

    if ([string]::IsNullOrWhiteSpace($streamUrl)) {
        $attempts.Add([pscustomobject]@{
            caseId = $currentCaseId
            status = 'unresolved-source'
            exitCode = $null
            reportPresent = (Test-Path -LiteralPath $reportPath)
            durationMs = 0
        })
        continue
    }

    $locatorHash = Get-PlaybackQualitySourceFingerprint -Locator $streamUrl
    $exitCode = Invoke-NativeHeadlessHarnessCase `
        -CaseId $currentCaseId `
        -StreamUrl $streamUrl `
        -SourceLocatorHash $locatorHash `
        -ReportsDir $ReportsDir `
        -NativeHelperExe $NativeHelperExe `
        -HeadlessProjectPath $HeadlessProjectPath `
        -HarnessScriptPath $HarnessScriptPath `
        -DurationSeconds $DurationSeconds `
        -ForceSdrOutput ([bool]$case.forceSdrOutput)
    $reportPresent = Test-Path -LiteralPath $reportPath
    $attempts.Add([pscustomobject]@{
        caseId = $currentCaseId
        status = $(if ($exitCode -eq 0) { 'completed' } else { 'failed' })
        exitCode = $exitCode
        reportPresent = $reportPresent
        durationMs = [Math]::Max(0, ([DateTimeOffset]::UtcNow - $startedAt).TotalMilliseconds)
    })
}

$failedAttemptCount = @($attempts | Where-Object { $_.status -eq 'failed' }).Count
$unresolvedSourceCount = @($attempts | Where-Object { $_.status -eq 'unresolved-source' }).Count
$reportCount = @($attempts | Where-Object { $_.reportPresent }).Count
$missingReportCount = $attempts.Count - $reportCount
$summary = [ordered]@{
    schemaVersion = 1
    runnerVersion = 'native-manifest-runner-v0.1'
    selectedCaseCount = $selectedCases.Count
    attemptedCaseCount = @($attempts | Where-Object { $_.status -ne 'unresolved-source' }).Count
    completedAttemptCount = @($attempts | Where-Object { $_.status -eq 'completed' }).Count
    failedAttemptCount = $failedAttemptCount
    unresolvedSourceCount = $unresolvedSourceCount
    reportCount = $reportCount
    missingReportCount = $missingReportCount
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
