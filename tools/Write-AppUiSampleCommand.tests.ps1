$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Write-AppUiSampleCommand.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('write-app-ui-sample-command-test-' + [guid]::NewGuid().ToString('N'))
$packagesRoot = Join-Path $testRoot 'Packages'
$olderPackageRoot = Join-Path $packagesRoot 'NoiraPlayer.App_oldpublisher'
$packageRoot = Join-Path $packagesRoot 'NoiraPlayer.App_testpublisher'
$manifestPath = Join-Path $testRoot 'ui-real-samples.local.json'
$summaryPath = Join-Path $testRoot 'summary.json'

New-Item -ItemType Directory -Path (Join-Path $olderPackageRoot 'LocalState') -Force | Out-Null
Start-Sleep -Milliseconds 20
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'LocalState') -Force | Out-Null

@'
{
  "schemaVersion": 1,
  "samples": [
    {
      "sampleId": "movie/multi-audio-details",
      "purpose": "Real movie details page with multiple audio choices",
      "route": "details",
      "itemId": "real-item-123",
      "itemName": "Real Movie"
    },
    {
      "sampleId": "playback/multi-subtitle",
      "purpose": "Real playback page with subtitle switching",
      "route": "playback",
      "itemId": "real-item-456",
      "itemName": "Real Playback",
      "mediaSourceId": "real-source-1",
      "startPositionTicks": 120000000,
      "forceSdrOutput": true
    }
  ]
}
'@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

try {
    & $scriptPath `
        -ManifestPath $manifestPath `
        -SampleId 'playback/multi-subtitle' `
        -PackagesRoot $packagesRoot `
        -SummaryPath $summaryPath

    $commandPath = Join-Path $packageRoot 'LocalState\dev-command.json'
    if (-not (Test-Path -LiteralPath $commandPath)) {
        throw 'Expected dev-command.json to be written to latest package LocalState.'
    }

    $command = Get-Content -LiteralPath $commandPath -Raw | ConvertFrom-Json
    if ($command.route -ne 'playback') {
        throw 'Expected selected sample route to be playback.'
    }

    if ($command.itemId -ne 'real-item-456') {
        throw 'Expected selected sample itemId to be preserved.'
    }

    if ($command.mediaSourceId -ne 'real-source-1') {
        throw 'Expected selected sample mediaSourceId to be preserved.'
    }

    if ($command.forceSdrOutput -ne $true) {
        throw 'Expected selected sample forceSdrOutput to be preserved.'
    }

    if (Test-Path -LiteralPath (Join-Path $olderPackageRoot 'LocalState\dev-command.json')) {
        throw 'Expected older package LocalState to remain unchanged.'
    }

    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    if ($summary.sampleId -ne 'playback/multi-subtitle') {
        throw 'Expected summary to record selected sampleId.'
    }

    if ($summary.route -ne 'playback') {
        throw 'Expected summary to record selected route.'
    }

    if ($summary.packageRoot -ne $packageRoot) {
        throw 'Expected summary to record latest package root.'
    }

    @'
{
  "schemaVersion": 1,
  "samples": [
    {
      "sampleId": "legacy/movies-fixture",
      "purpose": "Legacy fixture route should not be accepted",
      "route": "movies-fixture"
    }
  ]
}
'@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $fixtureRejected = $false
    try {
        & $scriptPath -ManifestPath $manifestPath -PackagesRoot $packagesRoot | Out-Null
    }
    catch {
        $fixtureRejected = $_.Exception.Message -like '*not supported*'
    }

    if (-not $fixtureRejected) {
        throw 'Expected legacy fixture route to be rejected.'
    }

    @'
{
  "schemaVersion": 1,
  "samples": [
    {
      "sampleId": "details/missing-item",
      "purpose": "Details route without item id should fail",
      "route": "details"
    }
  ]
}
'@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $missingItemRejected = $false
    try {
        & $scriptPath -ManifestPath $manifestPath -PackagesRoot $packagesRoot | Out-Null
    }
    catch {
        $missingItemRejected = $_.Exception.Message -like '*requires itemId*'
    }

    if (-not $missingItemRejected) {
        throw 'Expected details route without itemId to be rejected.'
    }
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output 'write-app-ui-sample-command tests ok'
