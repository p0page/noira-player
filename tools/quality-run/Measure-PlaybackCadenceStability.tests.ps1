$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Measure-PlaybackCadenceStability.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('measure-playback-cadence-stability-test-' + [Guid]::NewGuid().ToString('N'))
$reportsRoot = Join-Path $tempRoot 'reports'
$summaryPath = Join-Path $tempRoot 'cadence-stability-summary.local.json'

function Write-TestReport {
    param(
        [string]$CaseId,
        [double]$ExpectedFrameDurationMs,
        [double]$RenderP95,
        [double]$RenderP99,
        [double]$MaxFrameGap,
        [int]$RenderedFrames
    )

    $relativePath = $CaseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json'
    $path = Join-Path $reportsRoot $relativePath
    New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null

    $report = [pscustomobject][ordered]@{
        schemaVersion = 1
        caseMetadata = [pscustomobject][ordered]@{
            caseId = $CaseId
        }
        report = [pscustomobject][ordered]@{
            runId = $CaseId
            result = 'pass'
            timing = [pscustomobject][ordered]@{
                renderedVideoFrames = $RenderedFrames
                expectedFrameDurationMs = $ExpectedFrameDurationMs
                renderIntervalMsP95 = $RenderP95
                renderIntervalMsP99 = $RenderP99
                maxFrameGapMs = $MaxFrameGap
            }
        }
    }

    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding UTF8
}

New-Item -ItemType Directory -Path $reportsRoot -Force | Out-Null

try {
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw 'Measure-PlaybackCadenceStability.ps1 must exist.'
    }

    Write-TestReport `
        -CaseId 'local/native-headless-hdr10-60-repeat-1' `
        -ExpectedFrameDurationMs 16.6667 `
        -RenderP95 19.7 `
        -RenderP99 21.0 `
        -MaxFrameGap 21.0 `
        -RenderedFrames 93
    Write-TestReport `
        -CaseId 'local/native-headless-hdr10-60-repeat-2' `
        -ExpectedFrameDurationMs 16.6667 `
        -RenderP95 20.2 `
        -RenderP99 24.3 `
        -MaxFrameGap 24.3 `
        -RenderedFrames 93
    Write-TestReport `
        -CaseId 'local/native-headless-hdr10-60-repeat-3' `
        -ExpectedFrameDurationMs 16.6667 `
        -RenderP95 19.5 `
        -RenderP99 21.7 `
        -MaxFrameGap 21.7 `
        -RenderedFrames 94

    Write-TestReport `
        -CaseId 'local/native-headless-av-smoke-repeat-1' `
        -ExpectedFrameDurationMs 33.3333 `
        -RenderP95 39.3 `
        -RenderP99 40.1 `
        -MaxFrameGap 40.1 `
        -RenderedFrames 46
    Write-TestReport `
        -CaseId 'local/native-headless-av-smoke-repeat-2' `
        -ExpectedFrameDurationMs 33.3333 `
        -RenderP95 39.7 `
        -RenderP99 40.8 `
        -MaxFrameGap 40.8 `
        -RenderedFrames 46
    Write-TestReport `
        -CaseId 'local/native-headless-av-smoke-repeat-3' `
        -ExpectedFrameDurationMs 33.3333 `
        -RenderP95 39.5 `
        -RenderP99 40.4 `
        -MaxFrameGap 40.4 `
        -RenderedFrames 46

    & $scriptPath `
        -ReportsRoot $reportsRoot `
        -OutputPath $summaryPath `
        -MinimumSamples 3 `
        -MaterialityMs 2.0
    if ($LASTEXITCODE -ne 0) {
        throw 'Measure-PlaybackCadenceStability.ps1 returned a non-zero exit code.'
    }

    $summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
    if ($summary.schemaVersion -ne 1 -or $summary.kind -ne 'playback-cadence-stability-summary') {
        throw 'Expected cadence stability summary schema and kind.'
    }

    if ($summary.totalGroupCount -ne 2 -or $summary.unstableGroupCount -ne 1) {
        throw 'Expected one unstable group and two total groups.'
    }

    $unstable = $summary.groups |
        Where-Object { $_.caseGroupId -eq 'local/native-headless-hdr10-60' } |
        Select-Object -First 1
    if ($null -eq $unstable -or $unstable.stability -ne 'unstable') {
        throw 'Expected repeated HDR10 60fps case group to be marked unstable.'
    }

    if ($unstable.renderIntervalP99ExpectedErrorSpreadMs -lt 3.0 -or
        $unstable.maxFrameGapExpectedErrorSpreadMs -lt 3.0 -or
        -not ($unstable.unstableSignals -contains 'framePacing.renderIntervalP99ExpectedErrorMs')) {
        throw 'Expected unstable group to expose expected-error spread signals.'
    }

    $stable = $summary.groups |
        Where-Object { $_.caseGroupId -eq 'local/native-headless-av-smoke' } |
        Select-Object -First 1
    if ($null -eq $stable -or $stable.stability -ne 'stable') {
        throw 'Expected repeated A/V smoke case group to be marked stable.'
    }

    if ($stable.renderIntervalP99ExpectedErrorSpreadMs -ge 2.0) {
        throw 'Expected stable group P99 expected-error spread to stay below materiality.'
    }

    Write-Output 'measure-playback-cadence-stability tests ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
