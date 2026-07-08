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
$materializedReportPath = Join-Path $materializedDir 'jellyfin\sdr-hevc-main10-1080p60-3m.json'
$nativeMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-sdr-smoke.json'
$sampleUrl = 'https://repo.jellyfin.org/test-videos/SDR/HEVC%2010bit/Test%20Jellyfin%201080p%20HEVC%2010bit%203M.mp4'
$nativeCaseId = 'local/native-headless-sdr-smoke'

function New-NativePlaybackSample {
    $sampleDirectory = Join-Path $smokeRoot 'samples'
    New-Item -ItemType Directory -Path $sampleDirectory -Force | Out-Null
    $samplePath = Join-Path $sampleDirectory 'native-headless-sdr-smoke.mp4'
    $ffmpeg = 'C:\Program Files\FFmpeg\bin\ffmpeg.exe'
    if (-not (Test-Path -LiteralPath $ffmpeg)) {
        throw "ffmpeg.exe was not found at $ffmpeg."
    }

    & $ffmpeg `
        -y `
        -loglevel error `
        -f lavfi `
        -i testsrc2=size=320x180:rate=30 `
        -t 3 `
        -pix_fmt yuv420p `
        -c:v libx264 `
        -g 1 `
        -bf 0 `
        -movflags +faststart `
        $samplePath
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to generate native-headless local playback sample.'
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
    --player-core-version smoke-core `
    --source-revision smoke-native-headless-import-revision `
    --build-configuration Debug `
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
        "hdrKind": "Sdr",
        "isDirectPlayable": true,
        "minRenderedVideoFrames": 1
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
    --player-core-version smoke-core `
    --source-revision smoke-native-headless-real-revision `
    --build-configuration Debug `
    --output $nativeSummaryPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected materialize-native-harness-report-set to import native helper report.'
}

if (-not (Test-Path $nativeMaterializedReportPath)) {
    throw "Expected materialized native helper report at $nativeMaterializedReportPath."
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

Write-Host 'native-headless-harness smoke ok'
