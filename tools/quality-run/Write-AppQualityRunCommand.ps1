param(
    [Parameter(Mandatory = $true)]
    [string]$RunPlanPath,
    [string]$CaseId = '',
    [string]$PackagesRoot = (Join-Path $env:LOCALAPPDATA 'Packages'),
    [string]$PackageNamePrefix = 'NoiraPlayer.App_',
    [string]$SummaryPath = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RunPlanPath)) {
    throw 'RunPlanPath is required.'
}

if ([string]::IsNullOrWhiteSpace($PackagesRoot)) {
    throw 'PackagesRoot is required.'
}

if (-not (Test-Path -LiteralPath $RunPlanPath)) {
    throw ('Run plan not found: ' + $RunPlanPath)
}

if (-not (Test-Path -LiteralPath $PackagesRoot)) {
    throw ('Packages root not found: ' + $PackagesRoot)
}

$runPlan = Get-Content -LiteralPath $RunPlanPath -Raw | ConvertFrom-Json
$cases = @($runPlan.cases)
if ($cases.Count -eq 0) {
    throw 'Run plan has no cases.'
}

if ([string]::IsNullOrWhiteSpace($CaseId)) {
    $selectedCase = $cases |
        Where-Object { $null -ne $_.devCommand } |
        Select-Object -First 1
}
else {
    $selectedCase = $cases |
        Where-Object { $_.caseId -eq $CaseId } |
        Select-Object -First 1
}

if ($null -eq $selectedCase) {
    if ([string]::IsNullOrWhiteSpace($CaseId)) {
        throw 'Run plan has no case with devCommand.'
    }

    throw ('Run plan case not found: ' + $CaseId)
}

if ($null -eq $selectedCase.devCommand) {
    throw ('Run plan case has no devCommand: ' + $selectedCase.caseId)
}

$packageRoot = Get-ChildItem -LiteralPath $PackagesRoot -Directory |
    Where-Object { $_.Name -like ($PackageNamePrefix + '*') } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $packageRoot) {
    throw ('Package folder not found under: ' + $PackagesRoot)
}

$localState = Join-Path $packageRoot.FullName 'LocalState'
New-Item -ItemType Directory -Path $localState -Force | Out-Null

$commandPath = Join-Path $localState 'dev-command.json'
$commandJson = $selectedCase.devCommand | ConvertTo-Json -Depth 16
Set-Content -LiteralPath $commandPath -Value $commandJson -Encoding UTF8

$summary = [pscustomobject]@{
    schemaVersion = 1
    caseId = $selectedCase.caseId
    runId = $selectedCase.runId
    packageRoot = $packageRoot.FullName
    localState = $localState
    commandPath = $commandPath
    route = $selectedCase.devCommand.route
}

$summaryJson = $summary | ConvertTo-Json -Depth 6
if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    Write-Output $summaryJson
}
else {
    $summaryDirectory = Split-Path -Parent $SummaryPath
    if (-not [string]::IsNullOrWhiteSpace($summaryDirectory)) {
        New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null
    }

    Set-Content -LiteralPath $SummaryPath -Value $summaryJson -Encoding UTF8
}
