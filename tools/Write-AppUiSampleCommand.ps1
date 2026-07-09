param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,
    [string]$SampleId = '',
    [string]$PackagesRoot = (Join-Path $env:LOCALAPPDATA 'Packages'),
    [string]$PackageNamePrefix = 'NoiraPlayer.App_',
    [string]$SummaryPath = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    throw 'ManifestPath is required.'
}

if ([string]::IsNullOrWhiteSpace($PackagesRoot)) {
    throw 'PackagesRoot is required.'
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw ('UI sample manifest not found: ' + $ManifestPath)
}

if (-not (Test-Path -LiteralPath $PackagesRoot)) {
    throw ('Packages root not found: ' + $PackagesRoot)
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$samples = @($manifest.samples)
if ($samples.Count -eq 0) {
    throw 'UI sample manifest has no samples.'
}

if ([string]::IsNullOrWhiteSpace($SampleId)) {
    $selectedSample = $samples | Select-Object -First 1
}
else {
    $selectedSample = $samples |
        Where-Object { $_.sampleId -eq $SampleId } |
        Select-Object -First 1
}

if ($null -eq $selectedSample) {
    if ([string]::IsNullOrWhiteSpace($SampleId)) {
        throw 'UI sample manifest has no selectable sample.'
    }

    throw ('UI sample not found: ' + $SampleId)
}

$route = ([string]$selectedSample.route).Trim().ToLowerInvariant()
$allowedRoutes = @(
    'home',
    'login',
    'movies',
    'tv',
    'search',
    'settings',
    'livetv',
    'music',
    'photos',
    'playlists',
    'favorites',
    'unwatched',
    'details',
    'photo',
    'playback',
    'manual-playback',
    'quality-run'
)

if (-not ($allowedRoutes -contains $route)) {
    throw ('UI sample route is not supported: ' + $selectedSample.route)
}

function Get-SampleString([object]$Sample, [string]$Name) {
    $property = $Sample.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return ''
    }

    return ([string]$property.Value).Trim()
}

function Get-SampleLong([object]$Sample, [string]$Name) {
    $property = $Sample.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return 0
    }

    $value = 0L
    if ([long]::TryParse(([string]$property.Value), [ref]$value)) {
        return [Math]::Max(0, $value)
    }

    return 0
}

function Get-SampleBool([object]$Sample, [string]$Name) {
    $property = $Sample.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $false
    }

    if ($property.Value -is [bool]) {
        return [bool]$property.Value
    }

    $value = $false
    if ([bool]::TryParse(([string]$property.Value), [ref]$value)) {
        return $value
    }

    return $false
}

$itemId = Get-SampleString $selectedSample 'itemId'
$streamUrl = Get-SampleString $selectedSample 'streamUrl'
if (($route -eq 'details' -or $route -eq 'photo' -or $route -eq 'playback') -and
    [string]::IsNullOrWhiteSpace($itemId)) {
    throw ('UI sample route requires itemId: ' + $route)
}

if ($route -eq 'quality-run' -and
    [string]::IsNullOrWhiteSpace($itemId) -and
    [string]::IsNullOrWhiteSpace($streamUrl)) {
    throw 'UI sample route quality-run requires itemId or streamUrl.'
}

$command = [ordered]@{
    route = $route
}

$itemName = Get-SampleString $selectedSample 'itemName'
$mediaSourceId = Get-SampleString $selectedSample 'mediaSourceId'
$runId = Get-SampleString $selectedSample 'runId'

if (-not [string]::IsNullOrWhiteSpace($itemId)) {
    $command.itemId = $itemId
}

if (-not [string]::IsNullOrWhiteSpace($itemName)) {
    $command.itemName = $itemName
}

if (-not [string]::IsNullOrWhiteSpace($mediaSourceId)) {
    $command.mediaSourceId = $mediaSourceId
}

if (-not [string]::IsNullOrWhiteSpace($streamUrl)) {
    $command.streamUrl = $streamUrl
}

$startPositionTicks = Get-SampleLong $selectedSample 'startPositionTicks'
if ($startPositionTicks -gt 0) {
    $command.startPositionTicks = $startPositionTicks
}

if (Get-SampleBool $selectedSample 'forceSdrOutput') {
    $command.forceSdrOutput = $true
}

if (Get-SampleBool $selectedSample 'autoStart') {
    $command.autoStart = $true
}

$durationSeconds = Get-SampleLong $selectedSample 'durationSeconds'
if ($durationSeconds -gt 0) {
    $command.durationSeconds = $durationSeconds
}

if (-not [string]::IsNullOrWhiteSpace($runId)) {
    $command.runId = $runId
}

$expectedProperty = $selectedSample.PSObject.Properties['expected']
if ($null -ne $expectedProperty -and $null -ne $expectedProperty.Value) {
    $command.expected = $expectedProperty.Value
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
$commandJson = $command | ConvertTo-Json -Depth 16
Set-Content -LiteralPath $commandPath -Value $commandJson -Encoding UTF8

$summary = [pscustomobject]@{
    schemaVersion = 1
    sampleId = Get-SampleString $selectedSample 'sampleId'
    purpose = Get-SampleString $selectedSample 'purpose'
    packageRoot = $packageRoot.FullName
    localState = $localState
    commandPath = $commandPath
    route = $route
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
