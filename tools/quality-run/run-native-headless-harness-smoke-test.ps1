param(
    [string]$PlayerCoreVersion = 'smoke-core',
    [string]$SourceRevision = 'smoke-native-headless-real-revision',
    [string]$ImportSourceRevision = 'smoke-native-headless-import-revision',
    [string]$BuildConfiguration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$smokeRoot = Join-Path $repoRoot 'artifacts\quality-run\native-headless-smoke'
$capturedDir = Join-Path $smokeRoot 'captured'
$materializedDir = Join-Path $smokeRoot 'materialized'
$nativeCapturedDir = Join-Path $smokeRoot 'native-captured'
$nativeMaterializedDir = Join-Path $smokeRoot 'native-materialized'
$nativeHelperRoot = Join-Path $smokeRoot 'native-helper'
$manifestPath = Join-Path $smokeRoot 'manifest.json'
$nativeManifestPath = Join-Path $smokeRoot 'native-manifest.json'
$summaryPath = Join-Path $smokeRoot 'materialized-summary.json'
$nativeSummaryPath = Join-Path $smokeRoot 'native-materialized-summary.json'
$validationPath = Join-Path $smokeRoot 'validation.json'
$nativeValidationPath = Join-Path $smokeRoot 'native-validation.json'
$analysisPath = Join-Path $smokeRoot 'analysis.json'
$nativeAnalysisPath = Join-Path $smokeRoot 'native-analysis.json'
$reportPath = Join-Path $capturedDir 'jellyfin\sdr-hevc-main10-1080p60-3m.json'
$nativeReportPath = Join-Path $nativeCapturedDir 'local\native-headless-sdr-smoke.json'
$nativeAvReportPath = Join-Path $nativeCapturedDir 'local\native-headless-av-smoke.json'
$materializedReportPath = Join-Path $materializedDir 'jellyfin\sdr-hevc-main10-1080p60-3m.json'
$nativeMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-sdr-smoke.json'
$nativeAvMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-av-smoke.json'
$sampleUrl = 'https://repo.jellyfin.org/test-videos/SDR/HEVC%2010bit/Test%20Jellyfin%201080p%20HEVC%2010bit%203M.mp4'
$nativeCaseId = 'local/native-headless-sdr-smoke'
$nativeAvCaseId = 'local/native-headless-av-smoke'
$nativeSdr23976CaseId = 'local/native-headless-sdr-23976'
$nativeSdr24CaseId = 'local/native-headless-sdr-24'
$nativeSdr60CaseId = 'local/native-headless-sdr-60'
$nativeHdr1023976CaseId = 'local/native-headless-hdr10-23976'
$nativeHdr1024CaseId = 'local/native-headless-hdr10-24'
$nativeHdr1030CaseId = 'local/native-headless-hdr10-30'
$nativeHdr1060CaseId = 'local/native-headless-hdr10-60'

function Get-QualityReportPath {
    param(
        [string]$Root,
        [string]$CaseId
    )

    $relativePath = $CaseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json'
    return Join-Path $Root $relativePath
}

function New-NativePlaybackSample {
    return New-NativePlaybackSdrSample -Name 'native-headless-sdr-smoke' -Rate '30'
}

function New-NativePlaybackSdrSample {
    param(
        [string]$Name,
        [string]$Rate
    )

    $sampleDirectory = Join-Path $smokeRoot 'samples'
    New-Item -ItemType Directory -Path $sampleDirectory -Force | Out-Null
    $samplePath = Join-Path $sampleDirectory ($Name + '.mp4')
    $ffmpeg = 'C:\Program Files\FFmpeg\bin\ffmpeg.exe'
    if (-not (Test-Path -LiteralPath $ffmpeg)) {
        throw "ffmpeg.exe was not found at $ffmpeg."
    }

    & $ffmpeg `
        -y `
        -loglevel error `
        -f lavfi `
        -i "testsrc2=size=320x180:rate=$Rate" `
        -t 3 `
        -vf "setparams=range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709" `
        -pix_fmt yuv420p `
        -c:v libx264 `
        -g 1 `
        -bf 0 `
        -movflags +faststart `
        $samplePath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to generate native-headless local SDR playback sample $Name."
    }

    return ([System.Uri](Resolve-Path $samplePath).Path).AbsoluteUri
}

function New-NativePlaybackHdr10Sample {
    param(
        [string]$Name,
        [string]$Rate
    )

    $sampleDirectory = Join-Path $smokeRoot 'samples'
    New-Item -ItemType Directory -Path $sampleDirectory -Force | Out-Null
    $samplePath = Join-Path $sampleDirectory ($Name + '.mp4')
    $ffmpeg = 'C:\Program Files\FFmpeg\bin\ffmpeg.exe'
    if (-not (Test-Path -LiteralPath $ffmpeg)) {
        throw "ffmpeg.exe was not found at $ffmpeg."
    }

    & $ffmpeg `
        -y `
        -loglevel error `
        -f lavfi `
        -i "testsrc2=size=320x180:rate=$Rate" `
        -t 3 `
        -vf "format=yuv420p10le,setparams=range=tv:color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc" `
        -pix_fmt yuv420p10le `
        -c:v libx265 `
        -tag:v hvc1 `
        -x265-params "log-level=error:hdr10=1:repeat-headers=1:colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc:master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,50):max-cll=1000,400" `
        -movflags +faststart `
        $samplePath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to generate native-headless local HDR10 playback sample $Name."
    }

    return ([System.Uri](Resolve-Path $samplePath).Path).AbsoluteUri
}

function New-NativePlaybackAvSample {
    $sampleDirectory = Join-Path $smokeRoot 'samples'
    New-Item -ItemType Directory -Path $sampleDirectory -Force | Out-Null
    $samplePath = Join-Path $sampleDirectory 'native-headless-av-smoke.mp4'
    $subtitlePath = Join-Path $sampleDirectory 'native-headless-av-smoke.srt'
    @"
1
00:00:00,000 --> 00:00:02,500
Native headless subtitle smoke

"@ | Set-Content -LiteralPath $subtitlePath -Encoding UTF8
    $ffmpeg = 'C:\Program Files\FFmpeg\bin\ffmpeg.exe'
    if (-not (Test-Path -LiteralPath $ffmpeg)) {
        throw "ffmpeg.exe was not found at $ffmpeg."
    }

    & $ffmpeg `
        -y `
        -loglevel error `
        -f lavfi `
        -i testsrc2=size=320x180:rate=30 `
        -f lavfi `
        -i sine=frequency=1000:sample_rate=48000 `
        -i $subtitlePath `
        -map 0:v:0 `
        -map 1:a:0 `
        -map 2:s:0 `
        -t 3 `
        -vf "setparams=range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709" `
        -pix_fmt yuv420p `
        -c:v libx264 `
        -g 1 `
        -bf 0 `
        -c:a aac `
        -b:a 128k `
        -c:s mov_text `
        -metadata:s:s:0 language=eng `
        -shortest `
        -movflags +faststart `
        $samplePath
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to generate native-headless local A/V playback sample.'
    }

    return ([System.Uri](Resolve-Path $samplePath).Path).AbsoluteUri
}

function Get-AppRuntimePath {
    $candidates = @(
        $env:NEXTGENEMBY_VCRUNTIME140_APP_PATH,
        'C:\Program Files (x86)\Microsoft SDKs\UWPNuGetPackages\microsoft.net.native.compiler\1.7.6\tools\x64\ilc\lib\MSCRT\vcruntime140_app.dll',
        'C:\Users\yqzzx\.nuget\packages\microsoft.web.webview2\1.0.2849.39\tools\wv2winrt\vcruntime140_app.dll',
        'C:\Program Files\PowerToys\KeyboardManagerEditor\vcruntime140_app.dll',
        'C:\Program Files\Microsoft OneDrive\26.113.0614.0004\vcruntime140_app.dll'
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw 'Could not locate vcruntime140_app.dll required by the FFmpegInteropX UWP native DLLs.'
}

function Build-NativePlaybackGraphHelper {
    param(
        [string]$OutputDirectory
    )

    $vcvars = 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat'
    if (-not (Test-Path -LiteralPath $vcvars)) {
        throw "Visual Studio vcvars64.bat was not found at $vcvars."
    }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $ffmpegPackage = Join-Path $repoRoot 'src\NextGenEmby.Native\packages\FFmpegInteropX.FFmpegUWP.5.1.100'
    Copy-Item -Path (Join-Path $ffmpegPackage 'runtimes\win10-x64\native\*.dll') -Destination $OutputDirectory -Force
    Copy-Item -LiteralPath (Get-AppRuntimePath) -Destination $OutputDirectory -Force

    $include = '/I src\NextGenEmby.Native /I src\NextGenEmby.Native\packages\FFmpegInteropX.FFmpegUWP.5.1.100\include'
    $sources = @(
        'tests\NextGenEmby.Native.Tests\NativePlaybackGraphHeadlessSmokeTests.cpp',
        'src\NextGenEmby.Native\DxDeviceResources.cpp',
        'src\NextGenEmby.Native\NativePlaybackDiagnostics.cpp',
        'src\NextGenEmby.Native\Media\DxgiColorSpaceMapper.cpp',
        'src\NextGenEmby.Native\Media\HdrToneMappingPass.cpp',
        'src\NextGenEmby.Native\Media\HttpMediaInput.cpp',
        'src\NextGenEmby.Native\Media\FfmpegMediaSource.cpp',
        'src\NextGenEmby.Native\Media\VideoDecoder.cpp',
        'src\NextGenEmby.Native\Media\AudioDecoder.cpp',
        'src\NextGenEmby.Native\Media\AudioRenderer.cpp',
        'src\NextGenEmby.Native\Media\VideoRenderer.cpp',
        'src\NextGenEmby.Native\Media\SubtitleDecoder.cpp',
        'src\NextGenEmby.Native\Media\SubtitleRenderer.cpp',
        'src\NextGenEmby.Native\Media\PlaybackGraph.cpp'
    ) -join ' '
    $libs = 'd3d11.lib dxgi.lib d2d1.lib dwrite.lib d3dcompiler.lib xaudio2.lib windowsapp.lib avcodec.lib avformat.lib avutil.lib swresample.lib swscale.lib'
    $libPath = 'src\NextGenEmby.Native\packages\FFmpegInteropX.FFmpegUWP.5.1.100\runtimes\win10-x64\native'
    $helperExe = Join-Path $OutputDirectory 'NativePlaybackGraphHeadlessSmokeTests.exe'
    $command = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /DWIN32_LEAN_AND_MEAN /DWINRT_LEAN_AND_MEAN ' + $include + ' /Fo:"' + $OutputDirectory + '\\" ' + $sources + ' /Fe:"' + $helperExe + '" /link /LIBPATH:"' + $libPath + '" ' + $libs

    Push-Location $repoRoot
    try {
        cmd /c $command | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to build native playback graph helper.'
        }
    }
    finally {
        Pop-Location
    }

    return $helperExe
}

function Invoke-NativeHeadlessHelperCase {
    param(
        [string]$CaseId,
        [string]$StreamUrl,
        [string]$ReportsDir,
        [string]$NativeHelperExe,
        [int]$DurationSeconds = 3
    )

    $exitCode = 1
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Headless\NextGenEmby.PlaybackQuality.Headless.csproj') -- `
            --case-id $CaseId `
            --stream-url $StreamUrl `
            --duration-seconds $DurationSeconds `
            --reports-dir $ReportsDir `
            --native-helper-exe $NativeHelperExe
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 0) {
            break
        }

        Start-Sleep -Seconds 2
    }

    if ($exitCode -ne 0) {
        throw "Expected native-headless harness to run the App-free native helper for $CaseId."
    }

    $reportPath = Get-QualityReportPath -Root $ReportsDir -CaseId $CaseId
    if (-not (Test-Path -LiteralPath $reportPath)) {
        throw "Expected native helper captured report at $reportPath."
    }

    return Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
}

function Assert-NativeDisplayRefreshEvidence {
    param(
        [object]$Report,
        [string]$CaseId
    )

    if ($Report.report.display.refreshRateHz -le 0) {
        throw "Expected $CaseId to include software display refresh policy evidence."
    }

    if (-not ($Report.report.limitations -contains 'native-headless: display refresh is a software policy snapshot; HDMI/display output is not verified')) {
        throw "Expected $CaseId to disclose that display refresh is software policy evidence, not HDMI output verification."
    }

    if ($Report.report.timing.renderedVideoFrames -le 0) {
        throw "Expected $CaseId to include rendered frame evidence."
    }

    if ($Report.report.timing.renderIntervalMsP95 -le 0 -or
        $Report.report.timing.maxFrameGapMs -le 0 -or
        $Report.report.timing.lateFrameDropToleranceMs -le 0) {
        throw "Expected $CaseId to include frame interval and drop-threshold evidence."
    }

    if ($Report.report.timing.presentDurationMsP95 -le 0 -or
        $Report.report.timing.presentDurationMsMax -le 0) {
        throw "Expected $CaseId to include swapchain Present duration evidence."
    }
}

if (Test-Path $smokeRoot) {
    $resolvedSmokeRoot = (Resolve-Path $smokeRoot).Path
    if (-not $resolvedSmokeRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove smoke output outside repo root: $resolvedSmokeRoot"
    }

    Remove-Item -LiteralPath $smokeRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $smokeRoot | Out-Null

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Headless\NextGenEmby.PlaybackQuality.Headless.csproj') -- `
    --case-id jellyfin/sdr-hevc-main10-1080p60-3m `
    --stream-url $sampleUrl `
    --duration-seconds 5 `
    --reports-dir $capturedDir
if ($LASTEXITCODE -ne 0) {
    throw 'Expected native-headless harness command to return exit code 0.'
}

if (-not (Test-Path $reportPath)) {
    throw "Expected native headless harness report at $reportPath."
}

$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json

if ($report.caseMetadata.caseId -ne 'jellyfin/sdr-hevc-main10-1080p60-3m') {
    throw "Expected caseMetadata.caseId to match requested case id."
}

if ($report.report.runId -ne 'jellyfin/sdr-hevc-main10-1080p60-3m') {
    throw "Expected report.runId to match requested case id."
}

if ($report.report.result -notin @('skip', 'error', 'pass')) {
    throw "Expected report.result to be skip, error, or pass."
}

if ($report.report.environment.collectorVersion -ne 'native-headless-harness-v0.1') {
    throw "Expected native headless collector version."
}

if ([string]::IsNullOrWhiteSpace($report.report.environment.playerCoreVersion)) {
    throw "Expected playerCoreVersion to be present."
}

if ([string]::IsNullOrWhiteSpace($report.report.environment.sourceRevision)) {
    throw "Expected sourceRevision to be present."
}

if ($report.report.result -eq 'skip') {
    if ($report.report.skip.code -ne 'native-headless.native-link-blocked') {
        throw "Expected structured native-headless linkage blocker while real native open is not wired."
    }

    if ($report.report.skip.failureClass -ne 'insufficient instrumentation') {
        throw "Expected initial structured skip to be classified as insufficient instrumentation."
    }

    if ($report.report.skip.reason -notmatch 'Windows Store C\+\+/WinRT' -or
        $report.report.skip.reason -notmatch 'SwapChainPanel') {
        throw "Expected native-headless blocker reason to name the current native linkage and surface dependency."
    }

    if ($report.report.limitations -contains 'native-headless: command shape exists, but real App-free native open is not wired yet') {
        throw "Headless harness must now report the exact native linkage blocker, not the initial command-shape limitation."
    }

    if (-not ($report.report.limitations -contains 'native-headless: current NextGenEmby.Native build is a Windows Store C++/WinRT component with public playback entrypoints bound to UWP projection')) {
        throw "Expected explicit native-headless WinRT linkage limitation."
    }

    if ($report.report.limitations -contains 'native-headless: real App-free playback evidence requires a native graph host or render-surface abstraction before this runner can open PlaybackGraph') {
        throw "Headless harness must not keep reporting render-surface abstraction as an unproven blocker after the offscreen swapchain smoke test."
    }

    if (-not ($report.report.limitations -contains 'native-headless: offscreen DirectX composition swapchain is smoke-tested, but this runner still lacks a native PlaybackGraph host and lifecycle bridge')) {
        throw "Expected explicit native-headless graph host limitation."
    }
}

@"
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "jellyfin/sdr-hevc-main10-1080p60-3m",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "$sampleUrl",
      "purpose": [
        "sdr-smoke",
        "frame-pacing"
      ],
      "expected": {
        "codec": "hevc",
        "width": 1920,
        "height": 1080,
        "frameRate": 60.0,
        "hdrKind": "Sdr",
        "isDirectPlayable": true,
        "maxStartupDurationMs": 2000,
        "minRenderedVideoFrames": 120
      }
    }
  ]
}
"@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj') -- `
    materialize-native-harness-report-set `
    --manifest $manifestPath `
    --captured-reports-dir $capturedDir `
    --reports-dir $materializedDir `
    --collector-version native-headless-harness-v0.1 `
    --player-core-version $PlayerCoreVersion `
    --source-revision $ImportSourceRevision `
    --build-configuration $BuildConfiguration `
    --output $summaryPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected materialize-native-harness-report-set to import native-headless captured report.'
}

if (-not (Test-Path $materializedReportPath)) {
    throw "Expected materialized native-headless report at $materializedReportPath."
}

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj') -- `
    validate-report-set `
    --manifest $manifestPath `
    --reports-dir $materializedDir `
    --output $validationPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected materialized native-headless report-set to pass validation.'
}

$validation = Get-Content -LiteralPath $validationPath -Raw | ConvertFrom-Json
if ($validation.isValid -ne $true -or $validation.matchedCaseCount -ne 1) {
    throw 'Expected materialized native-headless validation to match the captured case.'
}

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj') -- `
    analyze-report-set `
    --reports-dir $materializedDir `
    --output $analysisPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected analyze-report-set to consume materialized native-headless report-set.'
}

$analysis = Get-Content -LiteralPath $analysisPath -Raw | ConvertFrom-Json
if ($analysis.playbackEvidence.canEvaluateNativePlayback -ne $false) {
    throw 'Skip-only native-headless blocker report must not be treated as native playback evidence.'
}

if (-not ($analysis.limitations -contains 'native-headless: current NextGenEmby.Native build is a Windows Store C++/WinRT component with public playback entrypoints bound to UWP projection')) {
    throw 'Expected analyze-report-set summary to preserve native-headless linkage limitation.'
}

if (-not ($analysis.limitations -contains 'native-headless: offscreen DirectX composition swapchain is smoke-tested, but this runner still lacks a native PlaybackGraph host and lifecycle bridge')) {
    throw 'Expected analyze-report-set summary to preserve the narrowed native graph host limitation.'
}

$nativeHelperExe = Build-NativePlaybackGraphHelper -OutputDirectory $nativeHelperRoot
$nativeSampleUrl = New-NativePlaybackSample
$nativeAvSampleUrl = New-NativePlaybackAvSample
$nativeSdr23976SampleUrl = New-NativePlaybackSdrSample -Name 'native-headless-sdr-23976' -Rate '24000/1001'
$nativeSdr24SampleUrl = New-NativePlaybackSdrSample -Name 'native-headless-sdr-24' -Rate '24'
$nativeSdr60SampleUrl = New-NativePlaybackSdrSample -Name 'native-headless-sdr-60' -Rate '60'
$nativeHdr1023976SampleUrl = New-NativePlaybackHdr10Sample -Name 'native-headless-hdr10-23976' -Rate '24000/1001'
$nativeHdr1024SampleUrl = New-NativePlaybackHdr10Sample -Name 'native-headless-hdr10-24' -Rate '24'
$nativeHdr1030SampleUrl = New-NativePlaybackHdr10Sample -Name 'native-headless-hdr10-30' -Rate '30'
$nativeHdr1060SampleUrl = New-NativePlaybackHdr10Sample -Name 'native-headless-hdr10-60' -Rate '60'

$nativeHelperExitCode = 1
for ($attempt = 1; $attempt -le 3; $attempt++) {
    dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Headless\NextGenEmby.PlaybackQuality.Headless.csproj') -- `
        --case-id $nativeCaseId `
        --stream-url $nativeSampleUrl `
        --duration-seconds 3 `
        --reports-dir $nativeCapturedDir `
        --native-helper-exe $nativeHelperExe
    $nativeHelperExitCode = $LASTEXITCODE
    if ($nativeHelperExitCode -eq 0) {
        break
    }

    Start-Sleep -Seconds 2
}

if ($nativeHelperExitCode -ne 0) {
    throw 'Expected native-headless harness to run the App-free native helper.'
}

if (-not (Test-Path $nativeReportPath)) {
    throw "Expected native helper captured report at $nativeReportPath."
}

$nativeReport = Get-Content -LiteralPath $nativeReportPath -Raw | ConvertFrom-Json
if ($nativeReport.report.runtimeMetrics.providerStatus -ne 'native-headless:returned-snapshot') {
    throw 'Expected native helper report to carry native-headless runtime metrics evidence.'
}

if ($nativeReport.report.timing.decodedVideoFrames -le 0) {
    throw 'Expected native helper report to include decoded video frames.'
}

if ($nativeReport.report.timing.renderedVideoFrames -le 0) {
    throw 'Expected native helper report to include rendered video frames.'
}

if ($nativeReport.report.source.videoRange -ne 'SDR' -or
    $nativeReport.report.source.colorPrimaries -ne 'bt709' -or
    $nativeReport.report.source.colorTransfer -ne 'bt709' -or
    $nativeReport.report.source.colorSpace -ne 'bt709') {
    throw 'Expected native helper report to include parsed source color metadata.'
}

if ($nativeReport.report.colorPipeline.dxgiInput -ne 'YCBCR_STUDIO_G22_LEFT_P709' -or
    $nativeReport.report.colorPipeline.dxgiOutput -ne 'RGB_FULL_G22_NONE_P709' -or
    $nativeReport.report.colorPipeline.conversionStatus -eq 'native-headless helper does not yet expose color conversion validation') {
    throw 'Expected native helper report to include native DXGI color pipeline instrumentation.'
}

if ($nativeReport.report.display.refreshRateHz -le 0) {
    throw 'Expected native helper report to include software display refresh policy evidence.'
}

if (-not ($nativeReport.report.limitations -contains 'native-headless: display refresh is a software policy snapshot; HDMI/display output is not verified')) {
    throw 'Expected native helper report to disclose that display refresh is software policy evidence, not HDMI output verification.'
}

$nativeAvHelperExitCode = 1
for ($attempt = 1; $attempt -le 3; $attempt++) {
    dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Headless\NextGenEmby.PlaybackQuality.Headless.csproj') -- `
        --case-id $nativeAvCaseId `
        --stream-url $nativeAvSampleUrl `
        --duration-seconds 3 `
        --reports-dir $nativeCapturedDir `
        --native-helper-exe $nativeHelperExe
    $nativeAvHelperExitCode = $LASTEXITCODE
    if ($nativeAvHelperExitCode -eq 0) {
        break
    }

    Start-Sleep -Seconds 2
}

if ($nativeAvHelperExitCode -ne 0) {
    throw 'Expected native-headless harness to run the App-free native helper for the A/V sample.'
}

if (-not (Test-Path $nativeAvReportPath)) {
    throw "Expected native helper A/V captured report at $nativeAvReportPath."
}

$nativeAvReport = Get-Content -LiteralPath $nativeAvReportPath -Raw | ConvertFrom-Json
if ($nativeAvReport.report.tracks.audioTrackCount -lt 1) {
    throw 'Expected native helper A/V report to include discovered audio tracks.'
}

if ($nativeAvReport.report.tracks.subtitleTrackCount -lt 1 -or
    $nativeAvReport.report.tracks.subtitles.Count -lt 1) {
    throw 'Expected native helper A/V report to include discovered subtitle tracks.'
}

if ($nativeAvReport.report.buffers.submittedAudioFrames -le 0) {
    throw 'Expected native helper A/V report to include submitted audio frames.'
}

if ($nativeAvReport.report.timing.presentDurationMsP95 -le 0 -or
    $nativeAvReport.report.timing.presentDurationMsMax -le 0) {
    throw 'Expected native helper A/V report to include swapchain Present duration evidence.'
}

if ($nativeAvReport.report.timing.audioAheadWaitDurationMsP95 -le 0 -or
    $nativeAvReport.report.timing.audioAheadWaitDurationMsMax -le 0) {
    throw 'Expected native helper A/V report to include audio-ahead wait duration evidence.'
}

if ($nativeAvReport.report.timing.audioAheadWaitTargetMsP95 -le 0 -or
    $nativeAvReport.report.timing.audioAheadWaitTargetMsMax -le 0) {
    throw 'Expected native helper A/V report to include audio-ahead wait target evidence.'
}

if ($nativeAvReport.report.timing.audioAheadWaitOversleepMsP95 -le 0 -or
    $nativeAvReport.report.timing.audioAheadWaitOversleepMsMax -le 0) {
    throw 'Expected native helper A/V report to include audio-ahead wait oversleep evidence.'
}

if ($nativeAvReport.report.runtimeMetrics.processWallClockMs -le 0 -or
    $nativeAvReport.report.runtimeMetrics.processCpuTimeMs -le 0 -or
    $nativeAvReport.report.runtimeMetrics.processCpuUtilizationRatio -le 0) {
    throw 'Expected native helper A/V report to include process CPU and wall-clock cost evidence.'
}

if ($nativeAvReport.report.sync.audioClockTicks -le 0 -or
    $nativeAvReport.report.sync.videoPositionTicks -le 0 -or
    $nativeAvReport.report.sync.audioVideoDriftMsP95 -le 0) {
    throw 'Expected native helper A/V report to include native A/V sync telemetry.'
}

$nativeMatrixReports = @(
    [pscustomobject]@{
        CaseId = $nativeSdr23976CaseId
        Report = Invoke-NativeHeadlessHelperCase -CaseId $nativeSdr23976CaseId -StreamUrl $nativeSdr23976SampleUrl -ReportsDir $nativeCapturedDir -NativeHelperExe $nativeHelperExe
    },
    [pscustomobject]@{
        CaseId = $nativeSdr24CaseId
        Report = Invoke-NativeHeadlessHelperCase -CaseId $nativeSdr24CaseId -StreamUrl $nativeSdr24SampleUrl -ReportsDir $nativeCapturedDir -NativeHelperExe $nativeHelperExe
    },
    [pscustomobject]@{
        CaseId = $nativeSdr60CaseId
        Report = Invoke-NativeHeadlessHelperCase -CaseId $nativeSdr60CaseId -StreamUrl $nativeSdr60SampleUrl -ReportsDir $nativeCapturedDir -NativeHelperExe $nativeHelperExe
    },
    [pscustomobject]@{
        CaseId = $nativeHdr1023976CaseId
        Report = Invoke-NativeHeadlessHelperCase -CaseId $nativeHdr1023976CaseId -StreamUrl $nativeHdr1023976SampleUrl -ReportsDir $nativeCapturedDir -NativeHelperExe $nativeHelperExe
    },
    [pscustomobject]@{
        CaseId = $nativeHdr1024CaseId
        Report = Invoke-NativeHeadlessHelperCase -CaseId $nativeHdr1024CaseId -StreamUrl $nativeHdr1024SampleUrl -ReportsDir $nativeCapturedDir -NativeHelperExe $nativeHelperExe
    },
    [pscustomobject]@{
        CaseId = $nativeHdr1030CaseId
        Report = Invoke-NativeHeadlessHelperCase -CaseId $nativeHdr1030CaseId -StreamUrl $nativeHdr1030SampleUrl -ReportsDir $nativeCapturedDir -NativeHelperExe $nativeHelperExe
    },
    [pscustomobject]@{
        CaseId = $nativeHdr1060CaseId
        Report = Invoke-NativeHeadlessHelperCase -CaseId $nativeHdr1060CaseId -StreamUrl $nativeHdr1060SampleUrl -ReportsDir $nativeCapturedDir -NativeHelperExe $nativeHelperExe
    }
)

foreach ($matrixItem in $nativeMatrixReports) {
    Assert-NativeDisplayRefreshEvidence -Report $matrixItem.Report -CaseId $matrixItem.CaseId

    if ([string]::IsNullOrWhiteSpace($matrixItem.Report.report.colorPipeline.dxgiInput) -or
        [string]::IsNullOrWhiteSpace($matrixItem.Report.report.colorPipeline.dxgiOutput)) {
        throw "Expected $($matrixItem.CaseId) to include DXGI color mapping evidence."
    }

    if ($matrixItem.Report.report.timing.droppedVideoFrames -lt 0 -or
        $matrixItem.Report.report.timing.videoAheadWaitCount -lt 0 -or
        $matrixItem.Report.report.buffers.videoStarvedPasses -lt 0 -or
        $matrixItem.Report.report.buffers.audioStarvedPasses -lt 0) {
        throw "Expected $($matrixItem.CaseId) to include non-negative dropped/wait/starvation counters."
    }
}

$nativeHdr10CaseIds = @(
    $nativeHdr1023976CaseId,
    $nativeHdr1024CaseId,
    $nativeHdr1030CaseId,
    $nativeHdr1060CaseId
)

foreach ($hdr10CaseId in $nativeHdr10CaseIds) {
    $nativeHdr10Report = ($nativeMatrixReports | Where-Object { $_.CaseId -eq $hdr10CaseId } | Select-Object -First 1).Report
    if ($nativeHdr10Report.report.source.hdrKind -ne 'Hdr10' -or
        $nativeHdr10Report.report.source.videoRange -ne 'HDR10' -or
        $nativeHdr10Report.report.source.colorPrimaries -ne 'bt2020' -or
        $nativeHdr10Report.report.source.colorTransfer -ne 'smpte2084' -or
        $nativeHdr10Report.report.source.colorSpace -ne 'bt2020nc') {
        throw "Expected generated HDR10 sample $hdr10CaseId to expose parsed HDR10 source color metadata."
    }
}

@"
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "$nativeCaseId",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeSampleUrl",
      "purpose": [
        "sdr-smoke",
        "frame-pacing"
      ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30.0,
        "videoRange": "SDR",
        "colorPrimaries": "bt709",
        "colorTransfer": "bt709",
        "colorSpace": "bt709",
        "hdrKind": "Sdr",
        "dxgiInput": "YCBCR_STUDIO_G22_LEFT_P709",
        "dxgiOutput": "RGB_FULL_G22_NONE_P709",
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1
      }
    },
    {
      "caseId": "$nativeAvCaseId",
      "category": "challenge",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeAvSampleUrl",
      "purpose": [
        "audio-switch",
        "av-sync",
        "buffering",
        "subtitles"
      ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 30.0,
        "videoRange": "SDR",
        "colorPrimaries": "bt709",
        "colorTransfer": "bt709",
        "colorSpace": "bt709",
        "hdrKind": "Sdr",
        "dxgiInput": "YCBCR_STUDIO_G22_LEFT_P709",
        "dxgiOutput": "RGB_FULL_G22_NONE_P709",
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1,
        "maxAudioVideoDriftMsP95": 80.0
      }
    },
    {
      "caseId": "$nativeSdr23976CaseId",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeSdr23976SampleUrl",
      "purpose": [
        "sdr-smoke",
        "frame-pacing",
        "cadence-23.976"
      ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 23.976,
        "videoRange": "SDR",
        "colorPrimaries": "bt709",
        "colorTransfer": "bt709",
        "colorSpace": "bt709",
        "hdrKind": "Sdr",
        "dxgiInput": "YCBCR_STUDIO_G22_LEFT_P709",
        "dxgiOutput": "RGB_FULL_G22_NONE_P709",
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1,
        "requireMatchedDisplayRefreshRate": true
      }
    },
    {
      "caseId": "$nativeSdr24CaseId",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeSdr24SampleUrl",
      "purpose": [
        "sdr-smoke",
        "frame-pacing",
        "cadence-24"
      ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 24.0,
        "videoRange": "SDR",
        "colorPrimaries": "bt709",
        "colorTransfer": "bt709",
        "colorSpace": "bt709",
        "hdrKind": "Sdr",
        "dxgiInput": "YCBCR_STUDIO_G22_LEFT_P709",
        "dxgiOutput": "RGB_FULL_G22_NONE_P709",
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1,
        "requireMatchedDisplayRefreshRate": true
      }
    },
    {
      "caseId": "$nativeSdr60CaseId",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeSdr60SampleUrl",
      "purpose": [
        "sdr-smoke",
        "frame-pacing",
        "cadence-60"
      ],
      "expected": {
        "codec": "h264",
        "width": 320,
        "height": 180,
        "frameRate": 60.0,
        "videoRange": "SDR",
        "colorPrimaries": "bt709",
        "colorTransfer": "bt709",
        "colorSpace": "bt709",
        "hdrKind": "Sdr",
        "dxgiInput": "YCBCR_STUDIO_G22_LEFT_P709",
        "dxgiOutput": "RGB_FULL_G22_NONE_P709",
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1,
        "requireMatchedDisplayRefreshRate": true
      }
    },
    {
      "caseId": "$nativeHdr1023976CaseId",
      "category": "challenge",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeHdr1023976SampleUrl",
      "purpose": [
        "hdr10",
        "color-pipeline",
        "frame-pacing",
        "cadence-23.976"
      ],
      "expected": {
        "codec": "hevc",
        "width": 320,
        "height": 180,
        "frameRate": 23.976,
        "videoRange": "HDR10",
        "colorPrimaries": "bt2020",
        "colorTransfer": "smpte2084",
        "colorSpace": "bt2020nc",
        "hdrKind": "Hdr10",
        "isHdr": true,
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1,
        "requireMatchedDisplayRefreshRate": true
      }
    },
    {
      "caseId": "$nativeHdr1024CaseId",
      "category": "challenge",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeHdr1024SampleUrl",
      "purpose": [
        "hdr10",
        "color-pipeline",
        "frame-pacing",
        "cadence-24"
      ],
      "expected": {
        "codec": "hevc",
        "width": 320,
        "height": 180,
        "frameRate": 24.0,
        "videoRange": "HDR10",
        "colorPrimaries": "bt2020",
        "colorTransfer": "smpte2084",
        "colorSpace": "bt2020nc",
        "hdrKind": "Hdr10",
        "isHdr": true,
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1,
        "requireMatchedDisplayRefreshRate": true
      }
    },
    {
      "caseId": "$nativeHdr1030CaseId",
      "category": "challenge",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeHdr1030SampleUrl",
      "purpose": [
        "hdr10",
        "color-pipeline",
        "frame-pacing",
        "cadence-30"
      ],
      "expected": {
        "codec": "hevc",
        "width": 320,
        "height": 180,
        "frameRate": 30.0,
        "videoRange": "HDR10",
        "colorPrimaries": "bt2020",
        "colorTransfer": "smpte2084",
        "colorSpace": "bt2020nc",
        "hdrKind": "Hdr10",
        "isHdr": true,
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1,
        "requireMatchedDisplayRefreshRate": true
      }
    },
    {
      "caseId": "$nativeHdr1060CaseId",
      "category": "challenge",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeHdr1060SampleUrl",
      "purpose": [
        "hdr10",
        "color-pipeline",
        "frame-pacing",
        "cadence-60"
      ],
      "expected": {
        "codec": "hevc",
        "width": 320,
        "height": 180,
        "frameRate": 60.0,
        "videoRange": "HDR10",
        "colorPrimaries": "bt2020",
        "colorTransfer": "smpte2084",
        "colorSpace": "bt2020nc",
        "hdrKind": "Hdr10",
        "isHdr": true,
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1,
        "requireMatchedDisplayRefreshRate": true
      }
    }
  ]
}
"@ | Set-Content -LiteralPath $nativeManifestPath -Encoding UTF8

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj') -- `
    materialize-native-harness-report-set `
    --manifest $nativeManifestPath `
    --captured-reports-dir $nativeCapturedDir `
    --reports-dir $nativeMaterializedDir `
    --collector-version native-headless-harness-v0.1 `
    --player-core-version $PlayerCoreVersion `
    --source-revision $SourceRevision `
    --build-configuration $BuildConfiguration `
    --output $nativeSummaryPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected materialize-native-harness-report-set to import native helper report.'
}

if (-not (Test-Path $nativeMaterializedReportPath)) {
    throw "Expected materialized native helper report at $nativeMaterializedReportPath."
}

if (-not (Test-Path $nativeAvMaterializedReportPath)) {
    throw "Expected materialized native helper A/V report at $nativeAvMaterializedReportPath."
}

$nativeMaterializedReport = Get-Content -LiteralPath $nativeMaterializedReportPath -Raw | ConvertFrom-Json
if ($nativeMaterializedReport.report.environment.playerCoreVersion -ne $PlayerCoreVersion -or
    $nativeMaterializedReport.report.environment.sourceRevision -ne $SourceRevision -or
    $nativeMaterializedReport.report.environment.buildConfiguration -ne $BuildConfiguration) {
    throw 'Expected materialized native helper report to use the requested build identity.'
}

if ($nativeMaterializedReport.modelAnalysis.avSync.status -ne 'not-applicable') {
    throw 'Expected video-only native helper report to mark A/V sync as not-applicable.'
}

if ($nativeMaterializedReport.modelAnalysis.cadence.status -ne 'matched') {
    throw 'Expected materialized native helper report to include matched cadence evidence.'
}

if ($nativeMaterializedReport.modelAnalysis.missingEvidence -contains 'display.refreshRateHz') {
    throw 'Expected materialized native helper report to stop treating display refresh as missing evidence.'
}

$nativeAvMaterializedReport = Get-Content -LiteralPath $nativeAvMaterializedReportPath -Raw | ConvertFrom-Json
if ($nativeAvMaterializedReport.report.tracks.audioTrackCount -lt 1 -or
    $nativeAvMaterializedReport.report.tracks.subtitleTrackCount -lt 1 -or
    $nativeAvMaterializedReport.modelAnalysis.avSync.status -ne 'synced') {
    throw 'Expected materialized native helper A/V report to preserve audio/subtitle track and A/V sync evidence.'
}

if ($nativeAvMaterializedReport.report.position.seekPositionErrorMs -gt 250.0) {
    throw 'Expected materialized native helper A/V report to capture immediate seek position evidence.'
}

foreach ($matrixItem in $nativeMatrixReports) {
    $materializedPath = Get-QualityReportPath -Root $nativeMaterializedDir -CaseId $matrixItem.CaseId
    if (-not (Test-Path -LiteralPath $materializedPath)) {
        throw "Expected materialized native helper matrix report at $materializedPath."
    }

    $materializedReport = Get-Content -LiteralPath $materializedPath -Raw | ConvertFrom-Json
    if ($materializedReport.modelAnalysis.cadence.status -ne 'matched') {
        throw "Expected $($matrixItem.CaseId) materialized report to include matched cadence evidence."
    }

    if ($materializedReport.modelAnalysis.missingEvidence -contains 'display.refreshRateHz') {
        throw "Expected $($matrixItem.CaseId) materialized report to include display.refreshRateHz evidence."
    }

    if ($materializedReport.report.display.refreshRateHz -le 0 -or
        -not ($materializedReport.modelAnalysis.cadence.signals -contains 'display.refreshRateHz')) {
        throw "Expected $($matrixItem.CaseId) model analysis cadence section to expose display.refreshRateHz evidence."
    }

    if (-not ($materializedReport.report.limitations -contains 'native-headless: display refresh is a software policy snapshot; HDMI/display output is not verified')) {
        throw "Expected $($matrixItem.CaseId) materialized report to preserve software-only display refresh limitation."
    }
}

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj') -- `
    validate-report-set `
    --manifest $nativeManifestPath `
    --reports-dir $nativeMaterializedDir `
    --output $nativeValidationPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected materialized native helper report-set to pass validation.'
}

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj') -- `
    analyze-report-set `
    --reports-dir $nativeMaterializedDir `
    --output $nativeAnalysisPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected analyze-report-set to consume materialized native helper report-set.'
}

$nativeAnalysis = Get-Content -LiteralPath $nativeAnalysisPath -Raw | ConvertFrom-Json
if ($nativeAnalysis.playbackEvidence.canEvaluateNativePlayback -ne $true) {
    throw 'Expected native helper report to be treated as App-free native software playback evidence.'
}

if ($nativeAnalysis.totalReportCount -ne 9) {
    throw 'Expected native helper report-set to include 9 reports: SDR 23.976/24/30/60, HDR10 23.976/24/30/60, and A/V challenge.'
}

$nativeAvSyncCoverage = $nativeAnalysis.capabilityCoverage | Where-Object { $_.capability -eq 'av-sync' } | Select-Object -First 1
if ($null -eq $nativeAvSyncCoverage -or $nativeAvSyncCoverage.status -ne 'evidence-present') {
    throw 'Expected native helper A/V report to claim A/V sync capability evidence.'
}

$nativeSubtitleCoverage = $nativeAnalysis.capabilityCoverage | Where-Object { $_.capability -eq 'subtitles' } | Select-Object -First 1
if ($null -eq $nativeSubtitleCoverage -or $nativeSubtitleCoverage.status -ne 'evidence-present') {
    throw 'Expected native helper A/V report to claim subtitle discovery capability evidence.'
}

$nativeFramePacingCoverage = $nativeAnalysis.capabilityCoverage | Where-Object { $_.capability -eq 'frame-pacing' } | Select-Object -First 1
if ($null -eq $nativeFramePacingCoverage -or
    $nativeFramePacingCoverage.status -ne 'evidence-present' -or
    $nativeFramePacingCoverage.evidenceCaseCount -lt 8 -or
    ($nativeFramePacingCoverage.missingSignals -contains 'display.refreshRateHz')) {
    throw 'Expected native helper matrix to claim frame-pacing evidence without missing display refresh.'
}

$nativeColorCoverage = $nativeAnalysis.capabilityCoverage | Where-Object { $_.capability -eq 'color' } | Select-Object -First 1
if ($null -eq $nativeColorCoverage -or
    $nativeColorCoverage.status -ne 'evidence-present') {
    throw 'Expected native helper matrix to include HDR10 color-pipeline evidence.'
}

foreach ($hdr10CaseId in $nativeHdr10CaseIds) {
    if (-not ($nativeColorCoverage.caseIds -contains $hdr10CaseId)) {
        throw "Expected native helper matrix to include HDR10 color-pipeline evidence for $hdr10CaseId."
    }
}

Write-Host 'native-headless-harness smoke ok'
