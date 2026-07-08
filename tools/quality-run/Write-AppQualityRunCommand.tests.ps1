$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Write-AppQualityRunCommand.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('write-app-quality-run-command-test-' + [guid]::NewGuid().ToString('N'))
$packagesRoot = Join-Path $testRoot 'Packages'
$olderPackageRoot = Join-Path $packagesRoot 'NoiraPlayer.App_oldpublisher'
$packageRoot = Join-Path $packagesRoot 'NoiraPlayer.App_testpublisher'
$runPlanPath = Join-Path $testRoot 'run-plan.json'
$summaryPath = Join-Path $testRoot 'summary.json'

New-Item -ItemType Directory -Path (Join-Path $olderPackageRoot 'LocalState') -Force | Out-Null
Start-Sleep -Milliseconds 20
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'LocalState') -Force | Out-Null

@'
{
  "schemaVersion": 1,
  "evaluationVersion": "playback-quality-v0.1",
  "cases": [
    {
      "caseId": "jellyfin/direct-uri-no-command",
      "runId": "jellyfin/direct-uri-no-command",
      "captureMode": "direct-uri"
    },
    {
      "caseId": "local/sdr-resume-seek-timeline",
      "runId": "local/sdr-resume-seek-timeline",
      "captureMode": "emby-item",
      "devCommand": {
        "route": "quality-run",
        "itemId": "public-placeholder-item",
        "mediaSourceId": "public-placeholder-source",
        "runId": "local/sdr-resume-seek-timeline",
        "durationSeconds": 30,
        "startPositionTicks": 120000000
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $runPlanPath -Encoding UTF8

try {
    & $scriptPath `
        -RunPlanPath $runPlanPath `
        -CaseId 'local/sdr-resume-seek-timeline' `
        -PackagesRoot $packagesRoot `
        -SummaryPath $summaryPath

    $commandPath = Join-Path $packageRoot 'LocalState\dev-command.json'
    if (-not (Test-Path -LiteralPath $commandPath)) {
        throw 'Expected dev-command.json to be written to latest package LocalState.'
    }

    $command = Get-Content -LiteralPath $commandPath -Raw | ConvertFrom-Json
    if ($command.route -ne 'quality-run') {
        throw 'Expected written dev-command route to be quality-run.'
    }

    if ($command.runId -ne 'local/sdr-resume-seek-timeline') {
        throw 'Expected written dev-command to preserve selected runId.'
    }

    if (Test-Path -LiteralPath (Join-Path $olderPackageRoot 'LocalState\dev-command.json')) {
        throw 'Expected older package LocalState to remain unchanged.'
    }

    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    if ($summary.caseId -ne 'local/sdr-resume-seek-timeline') {
        throw 'Expected summary to record selected caseId.'
    }

    if ($summary.packageRoot -ne $packageRoot) {
        throw 'Expected summary to record latest package root.'
    }

    if ($summary.route -ne 'quality-run') {
        throw 'Expected summary to record quality-run route.'
    }
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output 'write-app-quality-run-command tests ok'
