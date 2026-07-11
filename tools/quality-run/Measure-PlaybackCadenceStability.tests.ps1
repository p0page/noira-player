$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Measure-PlaybackCadenceStability.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('measure-playback-cadence-stability-test-' + [Guid]::NewGuid().ToString('N'))
$reportsRoot = Join-Path $tempRoot 'reports'
$summaryPath = Join-Path $tempRoot 'cadence-stability-summary.local.json'

function Write-TestReport {
    param(
        [string]$CaseId,
        [double]$ExpectedFrameDurationMs,
        [double]$RenderP05,
        [double]$RenderP95,
        [double]$RenderP99,
        [double]$MinFrameGap,
        [double]$MaxFrameGap,
        [int]$RenderedFrames,
        [double]$AudioAheadWaitOversleepP95 = 0,
        [double]$AudioAheadWaitOversleepP99 = 0,
        [double]$AudioAheadWaitFinalDeltaAbsP95 = 0,
        [double]$AudioAheadWaitFinalDeltaAbsP99 = 0,
        [double]$AudioAheadWaitPassDurationP95 = 0,
        [double]$AudioAheadWaitPassDurationP99 = 0,
        [double]$AudioAheadWaitPassTargetP95 = 0,
        [double]$AudioAheadWaitPassTargetP99 = 0,
        [double]$AudioAheadWaitPassOversleepP95 = 0,
        [double]$AudioAheadWaitPassOversleepP99 = 0,
        [int]$RenderAfterAudioAheadSampleCount = 0,
        [double]$RenderAfterAudioAheadP95 = 0,
        [double]$RenderAfterAudioAheadP99 = 0,
        [double]$RenderAfterAudioAheadMax = 0,
        [int]$AudioAheadWaitEndToPresentSampleCount = 0,
        [double]$AudioAheadWaitEndToPresentP50 = 0,
        [double]$AudioAheadWaitEndToPresentP95 = 0,
        [double]$AudioAheadWaitEndToPresentP99 = 0,
        [double]$AudioAheadWaitEndToPresentMax = 0,
        [int]$RenderAfterNonAudioSampleCount = 0,
        [double]$RenderAfterNonAudioP95 = 0,
        [double]$RenderAfterNonAudioP99 = 0,
        [double]$RenderAfterNonAudioMax = 0,
        [double]$AudioVideoDriftP95 = 0,
        [double]$AudioVideoDriftP99 = 0,
        [switch]$OmitShortIntervalEvidence,
        [switch]$OmitAudioAheadWaitEndToPresentEvidence
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
                audioAheadWaitOversleepMsP95 = $AudioAheadWaitOversleepP95
                audioAheadWaitOversleepMsP99 = $AudioAheadWaitOversleepP99
                audioAheadWaitFinalDeltaAbsMsP95 = $AudioAheadWaitFinalDeltaAbsP95
                audioAheadWaitFinalDeltaAbsMsP99 = $AudioAheadWaitFinalDeltaAbsP99
                audioAheadWaitPassDurationMsP95 = $AudioAheadWaitPassDurationP95
                audioAheadWaitPassDurationMsP99 = $AudioAheadWaitPassDurationP99
                audioAheadWaitPassTargetMsP95 = $AudioAheadWaitPassTargetP95
                audioAheadWaitPassTargetMsP99 = $AudioAheadWaitPassTargetP99
                audioAheadWaitPassOversleepMsP95 = $AudioAheadWaitPassOversleepP95
                audioAheadWaitPassOversleepMsP99 = $AudioAheadWaitPassOversleepP99
                renderIntervalAfterAudioAheadWaitSampleCount = $RenderAfterAudioAheadSampleCount
                renderIntervalAfterAudioAheadWaitMsP95 = $RenderAfterAudioAheadP95
                renderIntervalAfterAudioAheadWaitMsP99 = $RenderAfterAudioAheadP99
                renderIntervalAfterAudioAheadWaitMsMax = $RenderAfterAudioAheadMax
                audioAheadWaitEndToPresentSampleCount = $AudioAheadWaitEndToPresentSampleCount
                audioAheadWaitEndToPresentMsP50 = $AudioAheadWaitEndToPresentP50
                audioAheadWaitEndToPresentMsP95 = $AudioAheadWaitEndToPresentP95
                audioAheadWaitEndToPresentMsP99 = $AudioAheadWaitEndToPresentP99
                audioAheadWaitEndToPresentMsMax = $AudioAheadWaitEndToPresentMax
                renderIntervalAfterNonAudioWaitSampleCount = $RenderAfterNonAudioSampleCount
                renderIntervalAfterNonAudioWaitMsP95 = $RenderAfterNonAudioP95
                renderIntervalAfterNonAudioWaitMsP99 = $RenderAfterNonAudioP99
                renderIntervalAfterNonAudioWaitMsMax = $RenderAfterNonAudioMax
            }
            sync = [pscustomobject][ordered]@{
                audioVideoDriftMsP95 = $AudioVideoDriftP95
                audioVideoDriftMsP99 = $AudioVideoDriftP99
            }
        }
    }

    if (-not $OmitShortIntervalEvidence) {
        $report.report.timing | Add-Member -NotePropertyName renderIntervalMsP05 -NotePropertyValue $RenderP05
        $report.report.timing | Add-Member -NotePropertyName minFrameGapMs -NotePropertyValue $MinFrameGap
        $report.report.timing | Add-Member -NotePropertyName renderIntervalUnderExpected2MsCount -NotePropertyValue 2
        $report.report.timing | Add-Member -NotePropertyName renderIntervalUnderExpected4MsCount -NotePropertyValue 1
    }

    if ($OmitAudioAheadWaitEndToPresentEvidence) {
        @(
            'audioAheadWaitEndToPresentSampleCount',
            'audioAheadWaitEndToPresentMsP50',
            'audioAheadWaitEndToPresentMsP95',
            'audioAheadWaitEndToPresentMsP99',
            'audioAheadWaitEndToPresentMsMax'
        ) | ForEach-Object { $report.report.timing.PSObject.Properties.Remove($_) }
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
        -RenderP05 15.9 `
        -RenderP95 19.7 `
        -RenderP99 21.0 `
        -MinFrameGap 15.6 `
        -MaxFrameGap 21.0 `
        -RenderedFrames 93
    Write-TestReport `
        -CaseId 'local/native-headless-hdr10-60-repeat-2' `
        -ExpectedFrameDurationMs 16.6667 `
        -RenderP05 13.4 `
        -RenderP95 20.2 `
        -RenderP99 24.3 `
        -MinFrameGap 13.0 `
        -MaxFrameGap 24.3 `
        -RenderedFrames 93
    Write-TestReport `
        -CaseId 'local/native-headless-hdr10-60-repeat-3' `
        -ExpectedFrameDurationMs 16.6667 `
        -RenderP05 16.1 `
        -RenderP95 19.5 `
        -RenderP99 21.7 `
        -MinFrameGap 15.8 `
        -MaxFrameGap 21.7 `
        -RenderedFrames 94

    Write-TestReport `
        -CaseId 'local/native-headless-av-smoke-repeat-1' `
        -ExpectedFrameDurationMs 33.3333 `
        -RenderP05 27.0 `
        -RenderP95 39.3 `
        -RenderP99 40.1 `
        -MinFrameGap 26.8 `
        -MaxFrameGap 40.1 `
        -RenderedFrames 46 `
        -AudioAheadWaitFinalDeltaAbsP95 10.0 `
        -AudioAheadWaitFinalDeltaAbsP99 10.0 `
        -RenderAfterAudioAheadSampleCount 45 `
        -RenderAfterAudioAheadP95 39.3 `
        -RenderAfterAudioAheadP99 40.1 `
        -RenderAfterAudioAheadMax 40.1 `
        -AudioAheadWaitEndToPresentSampleCount 45 `
        -AudioAheadWaitEndToPresentP50 2.0 `
        -AudioAheadWaitEndToPresentP95 3.0 `
        -AudioAheadWaitEndToPresentP99 4.0 `
        -AudioAheadWaitEndToPresentMax 4.0 `
        -RenderAfterNonAudioSampleCount 0
    Write-TestReport `
        -CaseId 'local/native-headless-av-smoke-repeat-2' `
        -ExpectedFrameDurationMs 33.3333 `
        -RenderP05 26.9 `
        -RenderP95 39.7 `
        -RenderP99 40.8 `
        -MinFrameGap 26.7 `
        -MaxFrameGap 40.8 `
        -RenderedFrames 46 `
        -AudioAheadWaitFinalDeltaAbsP95 10.0 `
        -AudioAheadWaitFinalDeltaAbsP99 10.0 `
        -RenderAfterAudioAheadSampleCount 45 `
        -RenderAfterAudioAheadP95 39.7 `
        -RenderAfterAudioAheadP99 40.8 `
        -RenderAfterAudioAheadMax 40.8 `
        -AudioAheadWaitEndToPresentSampleCount 45 `
        -AudioAheadWaitEndToPresentP50 2.1 `
        -AudioAheadWaitEndToPresentP95 3.4 `
        -AudioAheadWaitEndToPresentP99 4.3 `
        -AudioAheadWaitEndToPresentMax 4.3 `
        -RenderAfterNonAudioSampleCount 0
    Write-TestReport `
        -CaseId 'local/native-headless-av-smoke-repeat-3' `
        -ExpectedFrameDurationMs 33.3333 `
        -RenderP05 27.1 `
        -RenderP95 39.5 `
        -RenderP99 40.4 `
        -MinFrameGap 26.9 `
        -MaxFrameGap 40.4 `
        -RenderedFrames 46 `
        -AudioAheadWaitFinalDeltaAbsP95 10.0 `
        -AudioAheadWaitFinalDeltaAbsP99 10.0 `
        -RenderAfterAudioAheadSampleCount 45 `
        -RenderAfterAudioAheadP95 39.5 `
        -RenderAfterAudioAheadP99 40.4 `
        -RenderAfterAudioAheadMax 40.4 `
        -AudioAheadWaitEndToPresentSampleCount 45 `
        -AudioAheadWaitEndToPresentP50 2.0 `
        -AudioAheadWaitEndToPresentP95 3.2 `
        -AudioAheadWaitEndToPresentP99 4.1 `
        -AudioAheadWaitEndToPresentMax 4.1 `
        -RenderAfterNonAudioSampleCount 0

    Write-TestReport `
        -CaseId 'local/native-headless-av-oversleep-repeat-1' `
        -ExpectedFrameDurationMs 33.3333 `
        -RenderP05 27.0 `
        -RenderP95 39.3 `
        -RenderP99 40.1 `
        -MinFrameGap 26.8 `
        -MaxFrameGap 40.1 `
        -RenderedFrames 46 `
        -AudioAheadWaitOversleepP95 4.0 `
        -AudioAheadWaitOversleepP99 7.0 `
        -AudioAheadWaitFinalDeltaAbsP95 10.0 `
        -AudioAheadWaitFinalDeltaAbsP99 10.0 `
        -AudioAheadWaitPassDurationP95 33.8 `
        -AudioAheadWaitPassDurationP99 34.0 `
        -AudioAheadWaitPassTargetP95 33.3 `
        -AudioAheadWaitPassTargetP99 33.3 `
        -AudioAheadWaitPassOversleepP95 0.5 `
        -AudioAheadWaitPassOversleepP99 0.7 `
        -RenderAfterAudioAheadSampleCount 45 `
        -RenderAfterAudioAheadP95 39.3 `
        -RenderAfterAudioAheadP99 40.1 `
        -RenderAfterAudioAheadMax 40.1 `
        -AudioAheadWaitEndToPresentSampleCount 45 `
        -AudioAheadWaitEndToPresentP50 2.0 `
        -AudioAheadWaitEndToPresentP95 3.0 `
        -AudioAheadWaitEndToPresentP99 4.0 `
        -AudioAheadWaitEndToPresentMax 4.0 `
        -RenderAfterNonAudioSampleCount 0 `
        -AudioVideoDriftP95 10.0 `
        -AudioVideoDriftP99 12.0
    Write-TestReport `
        -CaseId 'local/native-headless-av-oversleep-repeat-2' `
        -ExpectedFrameDurationMs 33.3333 `
        -RenderP05 24.0 `
        -RenderP95 39.4 `
        -RenderP99 40.2 `
        -MinFrameGap 23.8 `
        -MaxFrameGap 40.2 `
        -RenderedFrames 46 `
        -AudioAheadWaitOversleepP95 7.2 `
        -AudioAheadWaitOversleepP99 11.0 `
        -AudioAheadWaitFinalDeltaAbsP95 13.5 `
        -AudioAheadWaitFinalDeltaAbsP99 13.5 `
        -AudioAheadWaitPassDurationP95 39.2 `
        -AudioAheadWaitPassDurationP99 43.0 `
        -AudioAheadWaitPassTargetP95 33.3 `
        -AudioAheadWaitPassTargetP99 33.3 `
        -AudioAheadWaitPassOversleepP95 5.9 `
        -AudioAheadWaitPassOversleepP99 9.7 `
        -RenderAfterAudioAheadSampleCount 45 `
        -RenderAfterAudioAheadP95 43.2 `
        -RenderAfterAudioAheadP99 45.0 `
        -RenderAfterAudioAheadMax 45.0 `
        -AudioAheadWaitEndToPresentSampleCount 45 `
        -AudioAheadWaitEndToPresentP50 2.5 `
        -AudioAheadWaitEndToPresentP95 7.0 `
        -AudioAheadWaitEndToPresentP99 8.0 `
        -AudioAheadWaitEndToPresentMax 8.0 `
        -RenderAfterNonAudioSampleCount 0 `
        -AudioVideoDriftP95 10.0 `
        -AudioVideoDriftP99 12.0
    Write-TestReport `
        -CaseId 'local/native-headless-av-oversleep-repeat-3' `
        -ExpectedFrameDurationMs 33.3333 `
        -RenderP05 27.2 `
        -RenderP95 39.5 `
        -RenderP99 40.3 `
        -MinFrameGap 26.9 `
        -MaxFrameGap 40.3 `
        -RenderedFrames 46 `
        -AudioAheadWaitOversleepP95 4.1 `
        -AudioAheadWaitOversleepP99 7.4 `
        -AudioAheadWaitFinalDeltaAbsP95 10.2 `
        -AudioAheadWaitFinalDeltaAbsP99 10.2 `
        -AudioAheadWaitPassDurationP95 34.0 `
        -AudioAheadWaitPassDurationP99 34.2 `
        -AudioAheadWaitPassTargetP95 33.3 `
        -AudioAheadWaitPassTargetP99 33.3 `
        -AudioAheadWaitPassOversleepP95 0.7 `
        -AudioAheadWaitPassOversleepP99 0.9 `
        -RenderAfterAudioAheadSampleCount 45 `
        -RenderAfterAudioAheadP95 39.5 `
        -RenderAfterAudioAheadP99 40.3 `
        -RenderAfterAudioAheadMax 40.3 `
        -AudioAheadWaitEndToPresentSampleCount 45 `
        -AudioAheadWaitEndToPresentP50 2.1 `
        -AudioAheadWaitEndToPresentP95 3.2 `
        -AudioAheadWaitEndToPresentP99 4.2 `
        -AudioAheadWaitEndToPresentMax 4.2 `
        -RenderAfterNonAudioSampleCount 0 `
        -AudioVideoDriftP95 10.0 `
        -AudioVideoDriftP99 12.0

    Write-TestReport `
        -CaseId 'local/native-headless-old-report-repeat-1' `
        -ExpectedFrameDurationMs 16.6667 `
        -RenderP95 17.0 `
        -RenderP99 17.2 `
        -MaxFrameGap 17.2 `
        -RenderedFrames 94 `
        -OmitShortIntervalEvidence `
        -OmitAudioAheadWaitEndToPresentEvidence
    Write-TestReport `
        -CaseId 'local/native-headless-old-report-repeat-2' `
        -ExpectedFrameDurationMs 16.6667 `
        -RenderP95 17.1 `
        -RenderP99 17.3 `
        -MaxFrameGap 17.3 `
        -RenderedFrames 94 `
        -OmitShortIntervalEvidence `
        -OmitAudioAheadWaitEndToPresentEvidence
    Write-TestReport `
        -CaseId 'local/native-headless-old-report-repeat-3' `
        -ExpectedFrameDurationMs 16.6667 `
        -RenderP95 17.2 `
        -RenderP99 17.4 `
        -MaxFrameGap 17.4 `
        -RenderedFrames 94 `
        -OmitShortIntervalEvidence `
        -OmitAudioAheadWaitEndToPresentEvidence

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

    if ($summary.totalGroupCount -ne 4 -or $summary.unstableGroupCount -ne 2) {
        throw 'Expected two unstable groups and four total groups.'
    }

    $unstable = $summary.groups |
        Where-Object { $_.caseGroupId -eq 'local/native-headless-hdr10-60' } |
        Select-Object -First 1
    if ($null -eq $unstable -or $unstable.stability -ne 'unstable') {
        throw 'Expected repeated HDR10 60fps case group to be marked unstable.'
    }

    if ($unstable.renderIntervalP99ExpectedErrorSpreadMs -lt 3.0 -or
        $unstable.maxFrameGapExpectedErrorSpreadMs -lt 3.0 -or
        $unstable.renderIntervalP05ExpectedErrorSpreadMs -lt 2.0 -or
        $unstable.minFrameGapExpectedErrorSpreadMs -lt 2.0 -or
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

    if ($stable.renderIntervalAfterAudioAheadWaitP95SpreadMs -ge 1.0 -or
        $stable.renderIntervalAfterAudioAheadWaitSampleCountSpread -ne 0 -or
        $stable.audioAheadWaitEndToPresentP95SpreadMs -ge 1.0 -or
        $stable.audioAheadWaitEndToPresentSampleCountSpread -ne 0 -or
        $stable.renderIntervalAfterNonAudioWaitSampleCountMax -ne 0) {
        throw 'Expected stable A/V group to expose stable render-after-audio-ahead bucket evidence and zero non-audio bucket count.'
    }

    $oldReportGroup = $summary.groups |
        Where-Object { $_.caseGroupId -eq 'local/native-headless-old-report' } |
        Select-Object -First 1
    if ($null -eq $oldReportGroup -or $oldReportGroup.stability -ne 'stable') {
        throw 'Expected old report group without short-interval evidence to remain stable.'
    }

    if ($null -ne $oldReportGroup.renderIntervalP05ExpectedErrorSpreadMs -or
        $null -ne $oldReportGroup.minFrameGapExpectedErrorSpreadMs -or
        $null -ne $oldReportGroup.audioAheadWaitEndToPresentP95SpreadMs -or
        $null -ne $oldReportGroup.audioAheadWaitEndToPresentSampleCountSpread) {
        throw 'Expected old reports without newer timing evidence to keep corresponding spread fields null.'
    }

    $oversleepUnstable = $summary.groups |
        Where-Object { $_.caseGroupId -eq 'local/native-headless-av-oversleep' } |
        Select-Object -First 1
    if ($null -eq $oversleepUnstable -or $oversleepUnstable.stability -ne 'unstable') {
        throw 'Expected repeated A/V oversleep case group to be marked unstable.'
    }

    if ($oversleepUnstable.audioAheadWaitOversleepP95SpreadMs -lt 3.0 -or
        -not ($oversleepUnstable.unstableSignals -contains 'timing.audioAheadWaitOversleepMsP95')) {
        throw 'Expected A/V oversleep group to expose audio-ahead oversleep spread signals.'
    }

    if ($oversleepUnstable.audioAheadWaitFinalDeltaAbsP95SpreadMs -lt 3.0 -or
        -not ($oversleepUnstable.unstableSignals -contains 'timing.audioAheadWaitFinalDeltaAbsMsP95')) {
        throw 'Expected A/V oversleep group to expose audio-ahead final delta spread signals.'
    }

    if ($oversleepUnstable.audioAheadWaitPassOversleepP95SpreadMs -lt 5.0 -or
        -not ($oversleepUnstable.unstableSignals -contains 'timing.audioAheadWaitPassOversleepMsP95')) {
        throw 'Expected A/V oversleep group to expose per-pass audio-ahead oversleep spread signals.'
    }

    if ($oversleepUnstable.audioAheadWaitPassDurationP95SpreadMs -lt 5.0 -or
        $oversleepUnstable.audioAheadWaitPassTargetP95SpreadMs -ne 0) {
        throw 'Expected per-pass duration spread to separate actual wait duration from stable wait target.'
    }

    if ($oversleepUnstable.renderIntervalAfterAudioAheadWaitP95SpreadMs -lt 3.0 -or
        -not ($oversleepUnstable.unstableSignals -contains 'timing.renderIntervalAfterAudioAheadWaitMsP95')) {
        throw 'Expected unstable group to expose render interval after audio-ahead wait spread.'
    }

    if ($oversleepUnstable.audioAheadWaitEndToPresentP95SpreadMs -lt 3.0 -or
        -not ($oversleepUnstable.unstableSignals -contains 'timing.audioAheadWaitEndToPresentMsP95')) {
        throw 'Expected unstable group to expose audio-ahead wait end-to-present spread.'
    }

    Write-Output 'measure-playback-cadence-stability tests ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
