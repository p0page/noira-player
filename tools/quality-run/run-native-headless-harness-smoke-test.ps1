param(
    [string]$PlayerCoreVersion = 'smoke-core',
    [string]$SourceRevision = 'smoke-native-headless-real-revision',
    [string]$ImportSourceRevision = 'smoke-native-headless-import-revision',
    [string]$BuildConfiguration = 'Debug',
    [switch]$ParserContractOnly,
    [switch]$DemuxReadRecoveryOnly,
    [switch]$ExpectDemuxReadRecoveryFailure
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
$nativeManifestValidationPath = Join-Path $smokeRoot 'native-manifest-validation.json'
$analysisPath = Join-Path $smokeRoot 'analysis.json'
$nativeAnalysisPath = Join-Path $smokeRoot 'native-analysis.json'
$reportPath = Join-Path $capturedDir 'jellyfin\sdr-hevc-main10-1080p60-3m.json'
$nativeReportPath = Join-Path $nativeCapturedDir 'local\native-headless-sdr-smoke.json'
$nativeEndOfStreamReportPath = Join-Path $nativeCapturedDir 'local\native-headless-end-of-stream.json'
$nativeAvReportPath = Join-Path $nativeCapturedDir 'local\native-headless-av-smoke.json'
$nativeSubtitleReportPath = Join-Path $nativeCapturedDir 'local\native-headless-subtitle-switch.json'
$materializedReportPath = Join-Path $materializedDir 'jellyfin\sdr-hevc-main10-1080p60-3m.json'
$nativeMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-sdr-smoke.json'
$nativeEndOfStreamMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-end-of-stream.json'
$nativeAvMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-av-smoke.json'
$nativeSubtitleMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-subtitle-switch.json'
$nativeNetworkMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\network-reconnect-pause-resume.json'
$nativeLongPauseNetworkMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\long-pause-network-recovery.json'
$nativeDemuxReadRecoveryMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\demux-read-error-recovery-after-pause.json'
$nativeNonZeroTimelineMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-nonzero-start-timeline.json'
$nativeResumeSeekTimelineMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-resume-seek-timeline.json'
$nativeMissingFileMaterializedReportPath = Join-Path $nativeMaterializedDir 'local\native-headless-missing-file-error.json'
$sampleUrl = 'https://repo.jellyfin.org/test-videos/SDR/HEVC%2010bit/Test%20Jellyfin%201080p%20HEVC%2010bit%203M.mp4'
$nativeCaseId = 'local/native-headless-sdr-smoke'
$nativeEndOfStreamCaseId = 'local/native-headless-end-of-stream'
$nativeAvCaseId = 'local/native-headless-av-smoke'
$nativeSubtitleCaseId = 'local/native-headless-subtitle-switch'
$nativeNonZeroTimelineCaseId = 'local/native-headless-nonzero-start-timeline'
$nativeResumeSeekTimelineCaseId = 'local/native-headless-resume-seek-timeline'
$nativeMissingFileCaseId = 'local/native-headless-missing-file-error'
$nativeSdr23976CaseId = 'local/native-headless-sdr-23976'
$nativeSdr24CaseId = 'local/native-headless-sdr-24'
$nativeSdr60CaseId = 'local/native-headless-sdr-60'
$nativeHdr1023976CaseId = 'local/native-headless-hdr10-23976'
$nativeHdr1024CaseId = 'local/native-headless-hdr10-24'
$nativeHdr1030CaseId = 'local/native-headless-hdr10-30'
$nativeHdr1060CaseId = 'local/native-headless-hdr10-60'
$nativeCadenceDurationSeconds = 5
$nativeAvDurationSeconds = 3
$nativeAvSampleDurationSeconds = 6
$nativeAvMinimumRenderedVideoFrames = 40

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
        -t $nativeCadenceDurationSeconds `
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
        -t $nativeCadenceDurationSeconds `
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
    $subtitle1Path = Join-Path $sampleDirectory 'native-headless-av-smoke-eng.srt'
    $subtitle2Path = Join-Path $sampleDirectory 'native-headless-av-smoke-spa.srt'
    @"
1
00:00:00,000 --> 00:00:06,000
Native headless English subtitle smoke

"@ | Set-Content -LiteralPath $subtitle1Path -Encoding UTF8
    @"
1
00:00:00,000 --> 00:00:06,000
Native headless Spanish subtitle smoke

"@ | Set-Content -LiteralPath $subtitle2Path -Encoding UTF8
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
        -f lavfi `
        -i sine=frequency=500:sample_rate=48000 `
        -i $subtitle1Path `
        -i $subtitle2Path `
        -map 0:v:0 `
        -map 1:a:0 `
        -map 2:a:0 `
        -map 3:s:0 `
        -map 4:s:0 `
        -t $nativeAvSampleDurationSeconds `
        -vf "setparams=range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709" `
        -pix_fmt yuv420p `
        -c:v libx264 `
        -g 1 `
        -bf 0 `
        -c:a aac `
        -b:a 128k `
        -c:s mov_text `
        -metadata:s:a:0 language=eng `
        -metadata:s:a:1 language=jpn `
        -metadata:s:s:0 language=eng `
        -metadata:s:s:1 language=spa `
        -disposition:a:0 default `
        -disposition:a:1 0 `
        -disposition:s:0 default `
        -disposition:s:1 0 `
        -movflags +faststart `
        $samplePath
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to generate native-headless local A/V playback sample.'
    }

    return ([System.Uri](Resolve-Path $samplePath).Path).AbsoluteUri
}

function Assert-NativePlaybackAvSample {
    param([string]$SampleUrl)

    $ffprobe = 'C:\Program Files\FFmpeg\bin\ffprobe.exe'
    if (-not (Test-Path -LiteralPath $ffprobe)) {
        throw "ffprobe.exe was not found at $ffprobe."
    }

    $samplePath = ([System.Uri]$SampleUrl).LocalPath
    $probeJson = & $ffprobe `
        -v error `
        -show_entries 'format=duration:stream=index,codec_type:stream_tags=language:stream_disposition=default' `
        -of json `
        $samplePath
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to inspect native-headless local A/V playback sample.'
    }

    $probe = $probeJson | ConvertFrom-Json
    $videoStreams = @($probe.streams | Where-Object { $_.codec_type -eq 'video' })
    $audioStreams = @($probe.streams | Where-Object { $_.codec_type -eq 'audio' })
    $subtitleStreams = @($probe.streams | Where-Object { $_.codec_type -eq 'subtitle' })
    if ([Math]::Abs(([double]$probe.format.duration) - $nativeAvSampleDurationSeconds) -gt 0.001 -or
        $videoStreams.Count -ne 1 -or
        $audioStreams.Count -ne 2 -or
        $subtitleStreams.Count -ne 2) {
        throw 'Expected the native-headless A/V sample to be exactly 6 seconds with 1 video, 2 audio, and 2 subtitle streams.'
    }

    if ($audioStreams[0].tags.language -ne 'eng' -or
        $audioStreams[0].disposition.default -ne 1 -or
        $audioStreams[1].tags.language -ne 'jpn' -or
        $audioStreams[1].disposition.default -ne 0 -or
        $subtitleStreams[0].tags.language -ne 'eng' -or
        $subtitleStreams[0].disposition.default -ne 1 -or
        $subtitleStreams[1].tags.language -ne 'spa' -or
        $subtitleStreams[1].disposition.default -ne 0) {
        throw 'Expected the native-headless A/V sample to preserve audio/subtitle language and default dispositions.'
    }
}

function Get-AppRuntimePath {
    $candidates = @(
        $env:NOIRAPLAYER_VCRUNTIME140_APP_PATH,
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

function Resolve-NativeFfmpegPackagePath {
    $packagesConfigPath = Join-Path $repoRoot 'src\NoiraPlayer.Native\packages.config'
    [xml]$packagesConfig = Get-Content -LiteralPath $packagesConfigPath -Raw
    $package = @($packagesConfig.packages.package) |
        Where-Object { $_.id -eq 'FFmpegInteropX.UWP.FFmpeg' } |
        Select-Object -First 1
    if ($null -eq $package -or [string]::IsNullOrWhiteSpace([string]$package.version)) {
        throw 'FFmpegInteropX.UWP.FFmpeg is not declared in Native packages.config.'
    }

    $version = [string]$package.version
    $globalPackagesRoot = if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages'
    }
    else {
        $env:NUGET_PACKAGES
    }
    $candidates = @(
        (Join-Path $repoRoot "src\NoiraPlayer.Native\packages\FFmpegInteropX.UWP.FFmpeg.$version"),
        (Join-Path $globalPackagesRoot "ffmpeginteropx.uwp.ffmpeg\$version")
    )

    foreach ($candidate in $candidates) {
        if ((Test-Path -LiteralPath (Join-Path $candidate 'include\libavcodec\avcodec.h')) -and
            (Test-Path -LiteralPath (Join-Path $candidate 'runtimes\win-x64\native\avcodec.lib'))) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw ('Could not locate FFmpegInteropX.UWP.FFmpeg ' + $version + ' in repo-local or global NuGet package roots.')
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
    $ffmpegPackage = Resolve-NativeFfmpegPackagePath
    Copy-Item -Path (Join-Path $ffmpegPackage 'runtimes\win-x64\native\*.dll') -Destination $OutputDirectory -Force
    Copy-Item -LiteralPath (Get-AppRuntimePath) -Destination $OutputDirectory -Force

    $include = '/I src\NoiraPlayer.Native /I "' + (Join-Path $ffmpegPackage 'include') + '"'
    $sources = @(
        'tests\NoiraPlayer.Native.Tests\NativePlaybackGraphHeadlessSmokeTests.cpp',
        'src\NoiraPlayer.Native\DxDeviceResources.cpp',
        'src\NoiraPlayer.Native\NativePlaybackDiagnostics.cpp',
        'src\NoiraPlayer.Native\Media\DxgiColorSpaceMapper.cpp',
        'src\NoiraPlayer.Native\Media\HdrToneMappingPass.cpp',
        'src\NoiraPlayer.Native\Media\HttpMediaInput.cpp',
        'src\NoiraPlayer.Native\Media\FfmpegMediaSource.cpp',
        'src\NoiraPlayer.Native\Media\FfmpegSeekReplayCache.cpp',
        'src\NoiraPlayer.Native\Media\D3D11SharedDecodeBridge.cpp',
        'src\NoiraPlayer.Native\Media\VideoDecoder.cpp',
        'src\NoiraPlayer.Native\Media\VideoDecodeWorker.cpp',
        'src\NoiraPlayer.Native\Media\AudioDecoder.cpp',
        'src\NoiraPlayer.Native\Media\AudioRenderer.cpp',
        'src\NoiraPlayer.Native\Media\VideoRenderer.cpp',
        'src\NoiraPlayer.Native\Media\SubtitleDecoder.cpp',
        'src\NoiraPlayer.Native\Media\SubtitleRenderer.cpp',
        'src\NoiraPlayer.Native\Media\PlaybackGraph.cpp'
    ) -join ' '
    $libs = 'd3d11.lib dxgi.lib d2d1.lib dwrite.lib d3dcompiler.lib xaudio2.lib windowsapp.lib avcodec.lib avformat.lib avutil.lib swresample.lib swscale.lib'
    $libPath = Join-Path $ffmpegPackage 'runtimes\win-x64\native'
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

function Assert-NativeNetworkReconnectRecovery {
    param(
        [string]$NativeHelperExe,
        [string]$SampleUrl,
        [switch]$LongPause,
        [switch]$DemuxReadRecovery,
        [switch]$ExpectFailure
    )

    if ($LongPause -and $DemuxReadRecovery) {
        throw 'LongPause and DemuxReadRecovery are separate deterministic scenarios.'
    }

    $samplePath = ([System.Uri]$SampleUrl).LocalPath
    $sample = Get-Item -LiteralPath $samplePath
    $pauseSeconds = if ($LongPause) { 30 } else { 1 }
    $artifactName = if ($DemuxReadRecovery) {
        'demux-read-error-recovery'
    } elseif ($LongPause) {
        'long-pause-network-recovery'
    } else {
        'network-reconnect'
    }
    $networkCaseId = if ($DemuxReadRecovery) {
        'local/demux-read-error-recovery-after-pause'
    } elseif ($LongPause) {
        'local/long-pause-network-recovery'
    } else {
        'local/network-reconnect-pause-resume'
    }
    $networkLocator = if ($DemuxReadRecovery) {
        'local-fault://demux-read-error-recovery-after-pause'
    } elseif ($LongPause) {
        'local-fault://long-pause-network-recovery'
    } else {
        'local-fault://network-reconnect-pause-resume'
    }
    $cutAfterBytes = [Math]::Max(
        65536,
        [int64]($sample.Length * $(if ($LongPause) { 2.0 / 3.0 } else { 1.0 / 3.0 })))
    $networkRoot = Join-Path $smokeRoot ($artifactName + '-formal')
    $pauseMarkerPath = Join-Path $networkRoot 'native-pause.marker'
    New-Item -ItemType Directory -Path $networkRoot -Force | Out-Null
    Remove-Item -LiteralPath $pauseMarkerPath -Force -ErrorAction SilentlyContinue
    $portProbe = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback,
        0)
    $portProbe.Start()
    $port = ([System.Net.IPEndPoint]$portProbe.LocalEndpoint).Port
    $portProbe.Stop()

    $serverScript = Join-Path $PSScriptRoot 'Start-FaultingRangeMediaServer.ps1'
    $serverStdout = Join-Path $smokeRoot ($artifactName + '-server.out.log')
    $serverStderr = Join-Path $smokeRoot ($artifactName + '-server.err.log')
    Remove-Item -LiteralPath $serverStdout, $serverStderr -Force -ErrorAction SilentlyContinue
    $serverArguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        ('"' + $serverScript + '"'),
        '-FilePath',
        ('"' + $samplePath + '"'),
        '-Port',
        $port,
        '-CutAfterBytes',
        $cutAfterBytes,
        '-DelayPerChunkMilliseconds',
        20)
    if ($LongPause -or $DemuxReadRecovery) {
        $serverArguments += @(
            '-WaitForPauseMarkerPath',
            ('"' + $pauseMarkerPath + '"'),
            '-PauseMarkerTimeoutSeconds',
            90)
    }
    if ($DemuxReadRecovery) {
        $serverArguments += @(
            '-ResetRequestCount',
            3,
            '-ImmediateResetFromRequest',
            2,
            '-MaxRequests',
            16)
    }
    $server = Start-Process powershell `
        -ArgumentList $serverArguments `
        -WindowStyle Hidden `
        -PassThru `
        -RedirectStandardOutput $serverStdout `
        -RedirectStandardError $serverStderr

    try {
        $ready = $false
        for ($attempt = 0; $attempt -lt 50; $attempt++) {
            if ($server.HasExited) {
                break
            }

            $readyOutput = if (Test-Path -LiteralPath $serverStdout) {
                Get-Content -LiteralPath $serverStdout -Raw | Out-String
            }
            else {
                ''
            }
            if (-not [string]::IsNullOrWhiteSpace($readyOutput) -and
                $readyOutput -match 'faultServerReady') {
                $ready = $true
                break
            }

            Start-Sleep -Milliseconds 100
        }

        if (-not $ready) {
            throw 'Deterministic network reconnect server did not become ready.'
        }

        $networkManifestPath = Join-Path $networkRoot 'manifest.json'
        $networkReportsDir = Join-Path $networkRoot 'reports'
        $networkMaterializedDir = Join-Path $networkRoot 'materialized'
        $networkSummaryPath = Join-Path $networkRoot 'runner-summary.json'
        $networkMaterializedSummaryPath = Join-Path $networkRoot 'materialized-summary.json'
        $networkValidationPath = Join-Path $networkRoot 'validation.json'
        $networkRuntimeSourceMapPath = Join-Path $networkRoot 'runtime-source-map.json'
        $networkUrl = "http://127.0.0.1:{0}/media.mp4" -f $port
        $networkExpected = [ordered]@{
            codec = 'h264'
            width = 320
            height = 180
            frameRate = 30
            hdrKind = 'Sdr'
            isDirectPlayable = $true
            minRenderedVideoFrames = 10
        }
        if ($DemuxReadRecovery) {
            $networkExpected['readRecovery'] = [ordered]@{
                required = $true
                minReadErrors = 1
                minRecoveries = 1
                maxRetries = 10
            }
        }
        [ordered]@{
            schemaVersion = 1
            cases = @(
                [ordered]@{
                    caseId = $networkCaseId
                    category = if ($LongPause -or $DemuxReadRecovery) { 'challenge' } else { 'stable' }
                    severity = 'critical'
                    stability = 'stable'
                    uri = $networkLocator
                    pauseSeconds = $pauseSeconds
                    executionRequirement = [ordered]@{
                        minimumEvidenceLevel = 'native-playback'
                        scenario = 'pause-resume'
                    }
                    purpose = if ($DemuxReadRecovery) {
                        @('pause-resume', 'network-recovery', 'demux-read-error-recovery')
                    } elseif ($LongPause) {
                        @('pause-resume', 'network-recovery', 'long-pause')
                    } else {
                        @('buffering', 'pause-resume', 'network-recovery')
                    }
                    expected = $networkExpected
                }
            )
        } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $networkManifestPath -Encoding UTF8
        @{ $networkCaseId = $networkUrl } |
            ConvertTo-Json | Set-Content -LiteralPath $networkRuntimeSourceMapPath -Encoding UTF8

        $pauseMarkerEnvironmentName = 'NOIRAPLAYER_NATIVE_PAUSE_MARKER_PATH'
        $requireReadRecoveryEnvironmentName = 'NOIRAPLAYER_NATIVE_REQUIRE_DEMUX_READ_RECOVERY'
        $previousPauseMarkerPath = [Environment]::GetEnvironmentVariable($pauseMarkerEnvironmentName, 'Process')
        $previousRequireReadRecovery = [Environment]::GetEnvironmentVariable(
            $requireReadRecoveryEnvironmentName,
            'Process')
        try {
            if ($LongPause -or $DemuxReadRecovery) {
                [Environment]::SetEnvironmentVariable(
                    $pauseMarkerEnvironmentName,
                    $pauseMarkerPath,
                    'Process')
            }
            if ($DemuxReadRecovery) {
                [Environment]::SetEnvironmentVariable(
                    $requireReadRecoveryEnvironmentName,
                    '1',
                    'Process')
            }
            & powershell -NoProfile -ExecutionPolicy Bypass `
                -File (Join-Path $PSScriptRoot 'Invoke-PlaybackQualityManifest.ps1') `
                -ManifestPath $networkManifestPath `
                -ReportsDir $networkReportsDir `
                -NativeHelperExe $NativeHelperExe `
                -RuntimeSourceMapPath $networkRuntimeSourceMapPath `
                -SummaryPath $networkSummaryPath `
                -DurationSeconds 3
        }
        finally {
            [Environment]::SetEnvironmentVariable(
                $pauseMarkerEnvironmentName,
                $previousPauseMarkerPath,
                'Process')
            [Environment]::SetEnvironmentVariable(
                $requireReadRecoveryEnvironmentName,
                $previousRequireReadRecovery,
                'Process')
        }
        $networkRunnerExitCode = $LASTEXITCODE
        if ($ExpectFailure) {
            if ($networkRunnerExitCode -eq 0) {
                throw 'Expected disabled demux read recovery baseline to fail native playback.'
            }
        }
        elseif ($networkRunnerExitCode -ne 0) {
            throw 'Expected formal network reconnect manifest runner to complete.'
        }

        $networkReportPath = Get-QualityReportPath -Root $networkReportsDir -CaseId $networkCaseId
        $networkReport = Get-Content -LiteralPath $networkReportPath -Raw | ConvertFrom-Json
        $pauseEvent = @($networkReport.report.lifecycle.events | Where-Object operation -eq 'pause') | Select-Object -First 1
        $resumeEvent = @($networkReport.report.lifecycle.events | Where-Object operation -eq 'resume') | Select-Object -First 1
        if ($ExpectFailure) {
            if ($networkReport.report.result -notin @('error', 'fail') -or
                $networkReport.report.execution.status -ne 'failed' -or
                $networkReport.report.readRecovery.readErrorCount -lt 1 -or
                $networkReport.report.readRecovery.fatalReadErrorCode -ge 0 -or
                $networkReport.report.error.message -notmatch 'av_read_frame|I/O error') {
                throw 'Disabled demux read recovery baseline did not preserve a real fatal native read report.'
            }

            dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
                materialize-native-harness-report-set `
                --manifest $networkManifestPath `
                --captured-reports-dir $networkReportsDir `
                --reports-dir $networkMaterializedDir `
                --collector-version native-headless-harness-v0.1 `
                --player-core-version NoiraPlayer.Core `
                --source-revision $SourceRevision `
                --build-configuration Debug `
                --output $networkMaterializedSummaryPath
            if ($LASTEXITCODE -ne 0) {
                throw 'Expected disabled demux read recovery baseline report materialization to complete.'
            }

            $script:demuxReadRecoveryManifestCase =
                (Get-Content -LiteralPath $networkManifestPath -Raw | ConvertFrom-Json).cases[0]
            $script:demuxReadRecoveryCapturedReportPath = $networkReportPath
            Write-Output 'native demux read recovery disabled baseline captured a real fatal read report'
            return
        }

        if ($networkReport.report.result -ne 'pass' -or
            $networkReport.report.execution.status -ne 'completed' -or
            $networkReport.report.execution.evidenceLevel -ne 'native-playback' -or
            $networkReport.report.timing.decodedVideoFrames -le 0 -or
            $networkReport.report.timing.renderedVideoFrames -le 0 -or
            $pauseEvent.status -ne 'completed' -or
            $resumeEvent.status -ne 'completed' -or
            $resumeEvent.positionTicks -le $pauseEvent.positionTicks -or
            $resumeEvent.message -notmatch 'playback failed False') {
            throw 'Formal network reconnect report did not preserve complete pause/resume playback evidence.'
        }

        dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
            materialize-native-harness-report-set `
            --manifest $networkManifestPath `
            --captured-reports-dir $networkReportsDir `
            --reports-dir $networkMaterializedDir `
            --collector-version native-headless-harness-v0.1 `
            --player-core-version NoiraPlayer.Core `
            --source-revision $(if ($DemuxReadRecovery) { $SourceRevision } else { 'network-reconnect-smoke' }) `
            --build-configuration Debug `
            --output $networkMaterializedSummaryPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Expected formal network reconnect report materialization to complete.'
        }

        dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
            validate-report-set `
            --manifest $networkManifestPath `
            --reports-dir $networkMaterializedDir `
            --output $networkValidationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Expected formal network reconnect report-set to pass strict validation.'
        }

        $networkValidation = Get-Content -LiteralPath $networkValidationPath -Raw | ConvertFrom-Json
        if ($networkValidation.isValid -ne $true -or
            $networkValidation.executionValid -ne $true -or
            $networkValidation.executionCoverage.completedCaseCount -ne 1 -or
            $networkValidation.executionCoverage.renderedCaseCount -ne 1) {
            throw 'Formal network reconnect strict validation did not count one completed/rendered case.'
        }

        Start-Sleep -Milliseconds 500
        $serverOutput = Get-Content -LiteralPath $serverStdout -Raw

        if (-not [regex]::IsMatch($serverOutput, 'request=2 rangeStart=[1-9][0-9]*')) {
            throw "Expected forced disconnect recovery to issue a ranged second request.`n$serverOutput"
        }

        if ($LongPause) {
            $pauseMarkerIndex = $serverOutput.IndexOf('pauseMarkerObserved=1', [StringComparison]::Ordinal)
            $forcedResetIndex = $serverOutput.IndexOf('forcedReset=True', [StringComparison]::Ordinal)
            $secondRequestIndex = $serverOutput.IndexOf('request=2 ', [StringComparison]::Ordinal)
            if ($pauseMarkerIndex -lt 0 -or
                $forcedResetIndex -le $pauseMarkerIndex -or
                $secondRequestIndex -le $forcedResetIndex) {
                throw "Long pause fault order was not pause marker -> forced reset -> second range request.`n$serverOutput"
            }
            $script:longPauseNetworkManifestCase =
                (Get-Content -LiteralPath $networkManifestPath -Raw | ConvertFrom-Json).cases[0]
            $script:longPauseNetworkCapturedReportPath = $networkReportPath
        }
        elseif ($DemuxReadRecovery) {
            if ($networkReport.report.readRecovery.readErrorCount -lt 1 -or
                $networkReport.report.readRecovery.readRetryCount -lt 1 -or
                $networkReport.report.readRecovery.readRecoveryCount -lt 1 -or
                $networkReport.report.readRecovery.fatalReadErrorCode -ne 0) {
                throw 'Demux read recovery challenge did not preserve successful bounded recovery evidence.'
            }
            $script:demuxReadRecoveryManifestCase =
                (Get-Content -LiteralPath $networkManifestPath -Raw | ConvertFrom-Json).cases[0]
            $script:demuxReadRecoveryCapturedReportPath = $networkReportPath
        }
        else {
            $script:networkReconnectManifestCase =
                (Get-Content -LiteralPath $networkManifestPath -Raw | ConvertFrom-Json).cases[0]
            $script:networkReconnectCapturedReportPath = $networkReportPath
        }

        Write-Output ("native {0} formal case passed: pauseSeconds={1} strict=true request=2" -f $artifactName, $pauseSeconds)
    }
    finally {
        if (-not $server.HasExited) {
            Stop-Process -Id $server.Id -Force
        }
    }
}

function Assert-NativeLongPauseNetworkRecovery {
    param(
        [string]$NativeHelperExe,
        [string]$SampleUrl
    )

    Assert-NativeNetworkReconnectRecovery `
        -NativeHelperExe $NativeHelperExe `
        -SampleUrl $SampleUrl `
        -LongPause
}

function Assert-NativeDemuxReadRecovery {
    param(
        [string]$NativeHelperExe,
        [string]$SampleUrl,
        [switch]$ExpectFailure
    )

    Assert-NativeNetworkReconnectRecovery `
        -NativeHelperExe $NativeHelperExe `
        -SampleUrl $SampleUrl `
        -DemuxReadRecovery `
        -ExpectFailure:$ExpectFailure
}

function Invoke-NativeHeadlessHelperCase {
    param(
        [string]$CaseId,
        [string]$StreamUrl,
        [string]$ReportsDir,
        [string]$NativeHelperExe,
        [int]$DurationSeconds = $script:nativeCadenceDurationSeconds,
        [long]$StartPositionTicks = 0,
        [ValidateSet('playback', 'timeline', 'audio-switch', 'subtitle-switch', 'end-of-stream')]
        [string]$Scenario = 'playback',
        [switch]$ExpectError
    )

    dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Headless\NoiraPlayer.PlaybackQuality.Headless.csproj') -- `
        --case-id $CaseId `
        --stream-url $StreamUrl `
        --duration-seconds $DurationSeconds `
        --start-position-ticks $StartPositionTicks `
        --scenario $Scenario `
        --reports-dir $ReportsDir `
        --native-helper-exe $NativeHelperExe
    $exitCode = $LASTEXITCODE

    $expectedExitCode = if ($ExpectError) { 1 } else { 0 }
    if ($exitCode -ne $expectedExitCode) {
        throw "Expected native-headless harness exit code $expectedExitCode for $CaseId, got $exitCode."
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
        $Report.report.timing.renderIntervalMsP05 -le 0 -or
        $Report.report.timing.minFrameGapMs -le 0 -or
        $Report.report.timing.lateFrameDropToleranceMs -le 0) {
        throw "Expected $CaseId to include frame interval and drop-threshold evidence."
    }

    if ($Report.report.timing.presentDurationMsP95 -le 0 -or
        $Report.report.timing.presentDurationMsMax -le 0) {
        throw "Expected $CaseId to include swapchain Present duration evidence."
    }
}

function Build-NativeHeadlessParserFixtureHelper {
    param([string]$Root)

    $projectDirectory = Join-Path $Root 'helper-project'
    $outputDirectory = Join-Path $Root 'helper-bin'
    New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $projectPath = Join-Path $projectDirectory 'NativeHeadlessParserFixture.csproj'
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>NativeHeadlessParserFixture</AssemblyName>
    <UseAppHost>true</UseAppHost>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath $projectPath -Encoding UTF8
    @'
using System;
using System.IO;

var outputPath = Path.Combine(AppContext.BaseDirectory, "fixture-output.txt");
Console.Write(File.ReadAllText(outputPath));
var errorPath = Path.Combine(AppContext.BaseDirectory, "fixture-error.txt");
if (File.Exists(errorPath))
{
    Console.Error.Write(File.ReadAllText(errorPath));
}

var exitCodePath = Path.Combine(AppContext.BaseDirectory, "fixture-exit-code.txt");
return File.Exists(exitCodePath) && int.TryParse(File.ReadAllText(exitCodePath), out var exitCode)
    ? exitCode
    : 0;
'@ | Set-Content -LiteralPath (Join-Path $projectDirectory 'Program.cs') -Encoding UTF8

    dotnet build $projectPath --nologo -v quiet -o $outputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to build native-headless parser fixture helper.'
    }

    return [pscustomobject]@{
        ExePath = Join-Path $outputDirectory 'NativeHeadlessParserFixture.exe'
        OutputPath = Join-Path $outputDirectory 'fixture-output.txt'
        ErrorPath = Join-Path $outputDirectory 'fixture-error.txt'
        ExitCodePath = Join-Path $outputDirectory 'fixture-exit-code.txt'
    }
}

function New-NativeHeadlessParserFixtureOutput {
    param(
        [hashtable]$Overrides = @{},
        [string[]]$Omit = @()
    )

    $values = [ordered]@{
        decodedVideoFrames = '2'
        hardwareDecodedVideoFrames = '2'
        softwareDecodedVideoFrames = '0'
        renderedVideoFrames = '2'
        renderPasses = '3'
        submittedAudioFrames = '1'
        queuedAudioBuffers = '1'
        droppedVideoFrames = '0'
        seekPrerollDroppedFrames = '0'
        videoAheadWaitCount = '0'
        audioAheadWaitCount = '0'
        videoClockWaitCount = '0'
        videoStarvedPasses = '0'
        audioStarvedPasses = '0'
        audioClockTicks = '4900000'
        videoPositionTicks = '5000000'
        readErrorCount = '0'
        readRetryCount = '0'
        readRecoveryCount = '0'
        maxConsecutiveReadErrors = '0'
        lastReadErrorCode = '0'
        fatalReadErrorCode = '0'
        lastReadRecoveryDurationMs = '0'
        nativeGraphOpenDurationMs = '350'
        ffmpegOpenInputDurationMs = '200'
        ffmpegStreamInfoDurationMs = '100'
        nativeStartupSeekDurationMs = '25'
        ffmpegOpenInputBytesRead = '65536'
        ffmpegStreamInfoBytesRead = '1048576'
        nativeStartupSeekBytesRead = '16777216'
        nativeFirstFrameTransportBytesRead = '20000000'
        startupTransportProvider = 'ffmpeg-builtin'
        startupTransportCallEvidenceAvailable = '0'
        ffmpegOpenInputTransportProvider = 'ffmpeg-builtin'
        ffmpegOpenInputTransportCallEvidenceAvailable = '0'
        ffmpegOpenInputTransportReadCalls = '0'
        ffmpegOpenInputTransportSeekCalls = '0'
        ffmpegOpenInputTransportReadWaitMs = '0'
        ffmpegOpenInputTransportSeekWaitMs = '0'
        ffmpegOpenInputTransportSeekDistanceBytes = '0'
        ffmpegStreamInfoTransportProvider = 'ffmpeg-builtin'
        ffmpegStreamInfoTransportCallEvidenceAvailable = '0'
        ffmpegStreamInfoTransportReadCalls = '0'
        ffmpegStreamInfoTransportSeekCalls = '0'
        ffmpegStreamInfoTransportReadWaitMs = '0'
        ffmpegStreamInfoTransportSeekWaitMs = '0'
        ffmpegStreamInfoTransportSeekDistanceBytes = '0'
        nativeStartupSeekTransportProvider = 'ffmpeg-builtin'
        nativeStartupSeekTransportCallEvidenceAvailable = '0'
        nativeStartupSeekTransportReadCalls = '0'
        nativeStartupSeekTransportSeekCalls = '0'
        nativeStartupSeekTransportReadWaitMs = '0'
        nativeStartupSeekTransportSeekWaitMs = '0'
        nativeStartupSeekTransportSeekDistanceBytes = '0'
        nativeFirstFrameTransportProvider = 'ffmpeg-builtin'
        nativeFirstFrameTransportCallEvidenceAvailable = '0'
        nativeFirstFrameTransportReadCalls = '0'
        nativeFirstFrameTransportSeekCalls = '0'
        nativeFirstFrameTransportReadWaitMs = '0'
        nativeFirstFrameTransportSeekWaitMs = '0'
        nativeFirstFrameTransportSeekDistanceBytes = '0'
        nativeFirstFrameDurationMs = '15'
        nativeFirstFrameDemuxReadDurationMs = '8'
        nativeFirstFramePresentDurationMs = '1'
        nativeFirstFrameDemuxPacketCount = '12'
        nativeFirstFrameDemuxBytes = '65536'
        playbackDemuxReadDurationMs = '250'
        playbackDemuxPacketCount = '120'
        playbackDemuxBytes = '1048576'
        playbackTransportProvider = 'ffmpeg-builtin'
        playbackTransportCallEvidenceAvailable = '0'
        playbackTransportReadCalls = '0'
        playbackTransportSeekCalls = '0'
        playbackTransportReadWaitMs = '0'
        playbackTransportSeekWaitMs = '0'
        playbackTransportSeekDistanceBytes = '0'
        renderIntervalMsP05 = '30'
        renderIntervalMsP50 = '33'
        renderIntervalMsP95 = '34'
        renderIntervalMsP99 = '35'
        minFrameGapMs = '30'
        maxFrameGapMs = '35'
        renderIntervalSampleCount = '2'
        renderIntervalOverExpected2MsCount = '1'
        renderIntervalOverExpected4MsCount = '0'
        renderIntervalUnderExpected2MsCount = '0'
        renderIntervalUnderExpected4MsCount = '0'
        renderIntervalAfterAudioAheadWaitSampleCount = '0'
        renderIntervalAfterAudioAheadWaitMsP95 = '0'
        renderIntervalAfterAudioAheadWaitMsP99 = '0'
        renderIntervalAfterAudioAheadWaitMsMax = '0'
        audioAheadWaitEndToPresentSampleCount = '0'
        audioAheadWaitEndToPresentMsP50 = '0'
        audioAheadWaitEndToPresentMsP95 = '0'
        audioAheadWaitEndToPresentMsP99 = '0'
        audioAheadWaitEndToPresentMsMax = '0'
        renderIntervalAfterNonAudioWaitSampleCount = '2'
        renderIntervalAfterNonAudioWaitMsP95 = '34'
        renderIntervalAfterNonAudioWaitMsP99 = '35'
        renderIntervalAfterNonAudioWaitMsMax = '35'
        presentDurationMsP50 = '0.1'
        presentDurationMsP95 = '0.2'
        presentDurationMsP99 = '0.3'
        presentDurationMsMax = '0.4'
        videoDecodeDurationMsP50 = '1.1'
        videoDecodeDurationMsP95 = '2.2'
        videoDecodeDurationMsP99 = '3.3'
        videoDecodeDurationMsMax = '4.4'
        videoDecodeDeviceMode = 'independent-d3d11'
        videoDecodeSynchronizationMode = 'shared-fence'
        videoDecodeWorkerActive = '1'
        videoDecodeQueueCapacity = '3'
        videoDecodeQueueMaxDepth = '3'
        videoDecodeQueueProducerWaitCount = '10'
        videoDecodePacketReadDurationMsP50 = '0.1'
        videoDecodePacketReadDurationMsP95 = '0.2'
        videoDecodeSendPacketDurationMsP50 = '0.1'
        videoDecodeSendPacketDurationMsP95 = '0.2'
        videoDecodeReceiveFrameDurationMsP50 = '0.5'
        videoDecodeReceiveFrameDurationMsP95 = '1.5'
        videoDecodeFrameMaterializeDurationMsP50 = '0.2'
        videoDecodeFrameMaterializeDurationMsP95 = '0.8'
        videoRenderDurationMsP50 = '0.5'
        videoRenderDurationMsP95 = '1.5'
        videoRenderDurationMsP99 = '2.5'
        videoRenderDurationMsMax = '3.5'
        audioAheadWaitDurationMsP50 = '0'
        audioAheadWaitDurationMsP95 = '0'
        audioAheadWaitDurationMsP99 = '0'
        audioAheadWaitDurationMsMax = '0'
        audioAheadWaitTargetMsP50 = '0'
        audioAheadWaitTargetMsP95 = '0'
        audioAheadWaitTargetMsP99 = '0'
        audioAheadWaitTargetMsMax = '0'
        audioAheadWaitOversleepMsP50 = '0'
        audioAheadWaitOversleepMsP95 = '0'
        audioAheadWaitOversleepMsP99 = '0'
        audioAheadWaitOversleepMsMax = '0'
        audioAheadWaitFinalDeltaAbsMsP50 = '0'
        audioAheadWaitFinalDeltaAbsMsP95 = '0'
        audioAheadWaitFinalDeltaAbsMsP99 = '0'
        audioAheadWaitFinalDeltaAbsMsMax = '0'
        audioAheadWaitEpisodeCount = '0'
        audioAheadWaitPassesPerEpisodeP50 = '0'
        audioAheadWaitPassesPerEpisodeP95 = '0'
        audioAheadWaitPassesPerEpisodeP99 = '0'
        audioAheadWaitPassesPerEpisodeMax = '0'
        audioAheadWaitPassDurationMsP50 = '0'
        audioAheadWaitPassDurationMsP95 = '0'
        audioAheadWaitPassDurationMsP99 = '0'
        audioAheadWaitPassDurationMsMax = '0'
        audioAheadWaitPassTargetMsP50 = '0'
        audioAheadWaitPassTargetMsP95 = '0'
        audioAheadWaitPassTargetMsP99 = '0'
        audioAheadWaitPassTargetMsMax = '0'
        audioAheadWaitPassOversleepMsP50 = '0'
        audioAheadWaitPassOversleepMsP95 = '0'
        audioAheadWaitPassOversleepMsP99 = '0'
        audioAheadWaitPassOversleepMsMax = '0'
        framePacingSourceFrameRate = '30'
        lateFrameDropToleranceMs = '100'
        audioVideoDriftMsP50 = '1'
        audioVideoDriftMsP95 = '2'
        audioVideoDriftMsP99 = '3'
        audioVideoDriftMsMax = '4'
        sourceCodec = 'h264'
        sourceWidth = '320'
        sourceHeight = '180'
        sourceFrameRate = '30'
        sourceHdrKind = 'Sdr'
        sourceVideoRange = 'SDR'
        sourceColorPrimaries = 'bt709'
        sourceColorTransfer = 'bt709'
        sourceColorSpace = 'bt709'
        containerStartTimeTicks = '0'
        videoStreamStartTimeTicks = '0'
        logicalDurationTicks = '60000000'
        displayRefreshRateHz = '60'
        displayRefreshPolicy = 'software-only-cadence-policy'
        sourceTrackCount = '4'
        track0Index = '0'
        track0Kind = 'Video'
        track0Codec = 'h264'
        track0Language = 'und'
        track0IsDefault = '1'
        track1Index = '1'
        track1Kind = 'Audio'
        track1Codec = 'aac'
        track1Language = 'eng'
        track1Channels = '1'
        track1IsDefault = '1'
        track2Index = '2'
        track2Kind = 'Audio'
        track2Codec = 'aac'
        track2Language = 'jpn'
        track2Channels = '1'
        track2IsDefault = '0'
        track3Index = '4'
        track3Kind = 'Subtitle'
        track3Codec = 'mov_text'
        track3Language = 'spa'
        track3IsDefault = '0'
        audioSwitchAttempted = '0'
        audioSwitchOperationDurationMs = '0'
        audioSwitchLockWaitDurationMs = '0'
        audioSwitchExecutionDurationMs = '0'
        audioSwitchQuiesceDurationMs = '0'
        audioSwitchSeekDurationMs = '0'
        audioSwitchDecoderOpenDurationMs = '0'
        audioSwitchRendererOpenDurationMs = '0'
        audioSwitchPacketCacheHit = '0'
        audioSwitchPacketCacheEnabled = '1'
        audioSwitchPacketCachePacketCount = '0'
        audioSwitchPacketCacheBytes = '0'
        audioSwitchPacketCacheWindowDurationTicks = '0'
        audioSwitchRecoveryDurationMs = '0'
        subtitleSwitch1Attempted = '0'
        subtitleSwitch1OperationDurationMs = '0'
        subtitleSwitch1LockWaitDurationMs = '0'
        subtitleSwitch1ExecutionDurationMs = '0'
        subtitleSwitch1QuiesceDurationMs = '0'
        subtitleSwitch1SeekDurationMs = '0'
        subtitleSwitch1DecoderOpenDurationMs = '0'
        subtitleSwitch1RendererOpenDurationMs = '0'
        subtitleSwitch1PacketCacheHit = '0'
        subtitleSwitch1PacketCacheEnabled = '1'
        subtitleSwitch1PacketCachePacketCount = '0'
        subtitleSwitch1PacketCacheBytes = '0'
        subtitleSwitch1PacketCacheWindowDurationTicks = '0'
        subtitleSwitch1RecoveryDurationMs = '0'
        subtitleSwitch1CueRenderDurationMs = '0'
        subtitleSwitch1RenderedFramesBefore = '0'
        subtitleSwitch1RenderedFramesAfter = '0'
        subtitleSwitch1PausedSwitch = '0'
        subtitleSwitch1SelectedStreamIndex = '-1'
        subtitleSwitch1PausedPositionBeforeTicks = '0'
        subtitleSwitch1PausedPositionAfterTicks = '0'
        subtitleSwitch1PositionBeforeResumeTicks = '0'
        subtitleSwitch1PositionAfterResumeTicks = '0'
        subtitleSwitch2Attempted = '0'
        subtitleSwitch2OperationDurationMs = '0'
        subtitleSwitch2LockWaitDurationMs = '0'
        subtitleSwitch2ExecutionDurationMs = '0'
        subtitleSwitch2QuiesceDurationMs = '0'
        subtitleSwitch2SeekDurationMs = '0'
        subtitleSwitch2DecoderOpenDurationMs = '0'
        subtitleSwitch2RendererOpenDurationMs = '0'
        subtitleSwitch2PacketCacheHit = '0'
        subtitleSwitch2PacketCacheEnabled = '1'
        subtitleSwitch2PacketCachePacketCount = '0'
        subtitleSwitch2PacketCacheBytes = '0'
        subtitleSwitch2PacketCacheWindowDurationTicks = '0'
        subtitleSwitch2RecoveryDurationMs = '0'
        subtitleSwitch2CueRenderDurationMs = '0'
        subtitleSwitch2RenderedFramesBefore = '0'
        subtitleSwitch2RenderedFramesAfter = '0'
        subtitleSwitch2PausedSwitch = '0'
        subtitleSwitch2SelectedStreamIndex = '-1'
        subtitleSwitch2PausedPositionBeforeTicks = '0'
        subtitleSwitch2PausedPositionAfterTicks = '0'
        subtitleSwitch2PositionBeforeResumeTicks = '0'
        subtitleSwitch2PositionAfterResumeTicks = '0'
        subtitleOffAttempted = '0'
        seekAttempted = '0'
        seekTargetPositionTicks = '10000000'
        seekDemuxTargetTicks = '10000000'
        seekOperationDurationMs = '0'
        seekRecoveryDurationMs = '0'
        seekPacketCacheEnabled = '0'
        seekPacketCacheHit = '0'
        seekPacketCachePacketCount = '0'
        seekPacketCacheBytes = '0'
        seekPacketCacheWindowDurationTicks = '0'
        seekFallbackReason = 'disabled'
        postSeekAdvanced = '1'
        seekResetRuntimeMetrics = '1'
        preSeekRenderedVideoFrames = '45'
        preSeekDroppedVideoFrames = '0'
        selectedAudioStreamIndex = '1'
        selectedSubtitleStreamIndex = '-1'
        endOfStreamAttempted = '0'
        endOfStreamObserved = '0'
        endOfStreamStatus = 'not-attempted'
        endOfStreamPositionTicks = '-1'
        observedSampleWallClockDurationMs = '1000'
    }

    foreach ($key in $Overrides.Keys) {
        $values[$key] = [string]$Overrides[$key]
    }

    foreach ($key in $Omit) {
        $values.Remove($key)
    }

    return (($values.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ' ')
}

function Invoke-NativeHeadlessParserFixtureCase {
    param(
        [object]$FixtureHelper,
        [string]$HeadlessDll,
        [string]$Root,
        [string]$Name,
        [string]$HelperOutput,
        [string]$HelperError = '',
        [int]$HelperExitCode = 0,
        [string]$StreamUrl = '',
        [string]$Scenario = 'playback',
        [int]$PauseSeconds = 0
    )

    Set-Content -LiteralPath $FixtureHelper.OutputPath -Value $HelperOutput -Encoding UTF8
    Set-Content -LiteralPath $FixtureHelper.ErrorPath -Value $HelperError -Encoding UTF8
    Set-Content -LiteralPath $FixtureHelper.ExitCodePath -Value ([string]$HelperExitCode) -Encoding ASCII
    $reportsDirectory = Join-Path $Root 'reports'
    $caseId = 'local/native-headless-parser-' + $Name
    $fixtureStreamUrl = if ([string]::IsNullOrWhiteSpace($StreamUrl)) {
        ([System.Uri](Join-Path $Root 'fixture.mp4')).AbsoluteUri
    } else {
        $StreamUrl
    }
    $arguments = @(
        $HeadlessDll,
        '--case-id', $caseId,
        '--stream-url', $fixtureStreamUrl,
        '--duration-seconds', '1',
        '--scenario', $Scenario,
        '--reports-dir', $reportsDirectory,
        '--native-helper-exe', $FixtureHelper.ExePath)
    if ($PauseSeconds -gt 0) {
        $arguments += @('--pause-seconds', [string]$PauseSeconds)
    }
    & dotnet @arguments | ForEach-Object { Write-Host $_ }
    $exitCode = $LASTEXITCODE
    $reportPath = Get-QualityReportPath -Root $reportsDirectory -CaseId $caseId
    $report = if (Test-Path -LiteralPath $reportPath) {
        Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    } else {
        $null
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Report = $report
        ReportPath = $reportPath
    }
}

function Assert-NativeHeadlessParserContracts {
    param([string]$Root)

    $headlessProject = Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Headless\NoiraPlayer.PlaybackQuality.Headless.csproj'
    dotnet build $headlessProject --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to build native-headless harness for parser contract fixtures.'
    }

    $headlessDll = Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Headless\bin\Debug\net10.0\NoiraPlayer.PlaybackQuality.Headless.dll'
    $fixtureHelper = Build-NativeHeadlessParserFixtureHelper -Root $Root
    $failures = [System.Collections.Generic.List[string]]::new()

    $selectionOutput = New-NativeHeadlessParserFixtureOutput -Overrides @{
        subtitleOffAttempted = '1'
        subtitleOffStatus = 'failed'
        subtitleOffSelectedStreamIndex = '4'
        selectedSubtitleStreamIndex = '4'
    }
    $selection = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'final-subtitle-selection' `
        -HelperOutput $selectionOutput
    if ($selection.ExitCode -ne 0 -or
        $selection.Report.report.tracks.selectedSubtitleStreamIndex -ne 4 -or
        $selection.Report.report.tracks.isSubtitleDisabled -ne $false) {
        $failures.Add('Final selected subtitle stream 4 was not preserved after a failed subtitle-off interaction.')
    }

    if (@($selection.Report.report.lifecycle.events | Where-Object {
        $_.operation -in @('audio-switch', 'subtitle-switch', 'seek')
    }).Count -ne 0) {
        $failures.Add('attempted=false fixture operations unexpectedly produced lifecycle events.')
    }

    $endOfStream = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'end-of-stream-completed' `
        -Scenario 'end-of-stream' `
        -HelperOutput (New-NativeHeadlessParserFixtureOutput -Overrides @{
            endOfStreamAttempted = '1'
            endOfStreamObserved = '1'
            endOfStreamStatus = 'completed'
            endOfStreamPositionTicks = '60000000'
        })
    $endOfStreamEvents = @($endOfStream.Report.report.lifecycle.events | Where-Object {
        $_.operation -eq 'endOfStream'
    })
    if ($endOfStream.ExitCode -ne 0 -or
        $endOfStreamEvents.Count -ne 1 -or
        $endOfStreamEvents[0].status -ne 'completed' -or
        $endOfStreamEvents[0].positionTicks -ne 60000000) {
        $failures.Add('Completed native end-of-stream evidence did not produce exactly one lifecycle.endOfStream event.')
    }

    $pauseResumeOutput = New-NativeHeadlessParserFixtureOutput -Overrides @{
        pauseDurationSeconds = '30'
        positionBeforePauseTicks = '10000000'
        positionAfterResumeTicks = '12000000'
        decodedVideoFramesBeforePause = '90'
        renderedVideoFramesBeforePause = '88'
        postResumeDecodedVideoFrames = '96'
        postResumeRenderedVideoFrames = '94'
        actualPauseDurationMs = '30001.5'
        resumeRecoveryDurationMs = '325.25'
        pauseResumeStatus = 'completed'
        playbackFailed = '0'
    }
    $pauseResume = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'long-pause-resume-completed' `
        -Scenario 'pause-resume' `
        -PauseSeconds 30 `
        -HelperOutput $pauseResumeOutput
    $pauseEvent = @($pauseResume.Report.report.lifecycle.events | Where-Object operation -eq 'pause') | Select-Object -First 1
    $resumeEvent = @($pauseResume.Report.report.lifecycle.events | Where-Object operation -eq 'resume') | Select-Object -First 1
    if ($pauseResume.ExitCode -ne 0 -or
        $pauseEvent.status -ne 'completed' -or
        $resumeEvent.status -ne 'completed' -or
        $resumeEvent.message -notmatch 'decoded 90->96' -or
        $resumeEvent.message -notmatch 'rendered 88->94' -or
        $resumeEvent.message -notmatch 'actual pause 30001.500 ms' -or
        $resumeEvent.message -notmatch 'recovery 325.250 ms') {
        $failures.Add('Completed long pause/resume evidence did not preserve actual hold time, frame deltas, and recovery latency.')
    }

    $pauseNetworkFailure = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'long-pause-resume-network-failure' `
        -Scenario 'pause-resume' `
        -PauseSeconds 30 `
        -StreamUrl 'https://example.invalid/media.mkv' `
        -HelperOutput (New-NativeHeadlessParserFixtureOutput -Overrides @{
            pauseDurationSeconds = '30'
            positionBeforePauseTicks = '10000000'
            positionAfterResumeTicks = '10000000'
            decodedVideoFramesBeforePause = '90'
            renderedVideoFramesBeforePause = '88'
            postResumeDecodedVideoFrames = '90'
            postResumeRenderedVideoFrames = '88'
            actualPauseDurationMs = '30002'
            resumeRecoveryDurationMs = '5000'
            pauseResumeStatus = 'failed'
            playbackFailed = '1'
        }) `
        -HelperError 'av_read_frame failed: I/O error' `
        -HelperExitCode 2
    if ($pauseNetworkFailure.ExitCode -ne 1 -or
        $pauseNetworkFailure.Report.report.error.code -ne 'native-headless.network-io-failed' -or
        $pauseNetworkFailure.Report.report.error.operation -ne 'resume') {
        $failures.Add('A pause/resume HTTP I/O failure was not attributed to the resume operation.')
    }

    foreach ($field in @(
        'decodedVideoFramesBeforePause',
        'renderedVideoFramesBeforePause',
        'actualPauseDurationMs',
        'resumeRecoveryDurationMs'
    )) {
        $missingPauseEvidence = Invoke-NativeHeadlessParserFixtureCase `
            -FixtureHelper $fixtureHelper `
            -HeadlessDll $headlessDll `
            -Root $Root `
            -Name ('long-pause-missing-' + $field) `
            -Scenario 'pause-resume' `
            -PauseSeconds 30 `
            -HelperOutput (New-NativeHeadlessParserFixtureOutput -Overrides @{
                pauseDurationSeconds = '30'
                positionBeforePauseTicks = '10000000'
                positionAfterResumeTicks = '12000000'
                decodedVideoFramesBeforePause = '90'
                renderedVideoFramesBeforePause = '88'
                postResumeDecodedVideoFrames = '96'
                postResumeRenderedVideoFrames = '94'
                actualPauseDurationMs = '30001'
                resumeRecoveryDurationMs = '325'
                pauseResumeStatus = 'completed'
                playbackFailed = '0'
            } -Omit @($field))
        if ($missingPauseEvidence.ExitCode -ne 1 -or
            $missingPauseEvidence.Report.report.error.message -notmatch [regex]::Escape($field)) {
            $failures.Add("Missing long pause field '$field' was not rejected explicitly.")
        }
    }

    foreach ($invalidPause in @(
        [pscustomobject]@{ Name = 'no-rendered-progress'; ActualPauseMs = '30001'; RenderedAfter = '88' },
        [pscustomobject]@{ Name = 'short-hold'; ActualPauseMs = '1000'; RenderedAfter = '94' }
    )) {
        $invalidPauseEvidence = Invoke-NativeHeadlessParserFixtureCase `
            -FixtureHelper $fixtureHelper `
            -HeadlessDll $headlessDll `
            -Root $Root `
            -Name ('long-pause-' + $invalidPause.Name) `
            -Scenario 'pause-resume' `
            -PauseSeconds 30 `
            -HelperOutput (New-NativeHeadlessParserFixtureOutput -Overrides @{
                pauseDurationSeconds = '30'
                positionBeforePauseTicks = '10000000'
                positionAfterResumeTicks = '12000000'
                decodedVideoFramesBeforePause = '90'
                renderedVideoFramesBeforePause = '88'
                postResumeDecodedVideoFrames = '96'
                postResumeRenderedVideoFrames = $invalidPause.RenderedAfter
                actualPauseDurationMs = $invalidPause.ActualPauseMs
                resumeRecoveryDurationMs = '325'
                pauseResumeStatus = 'completed'
                playbackFailed = '0'
            })
        if ($invalidPauseEvidence.ExitCode -ne 1 -or
            $invalidPauseEvidence.Report.report.error.message -notmatch 'hold the requested pause') {
            $failures.Add("Invalid long pause fixture '$($invalidPause.Name)' was accepted as completed.")
        }
    }

    $lateFailure = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'late-helper-failure' `
        -HelperOutput (New-NativeHeadlessParserFixtureOutput) `
        -HelperExitCode 9
    if ($lateFailure.ExitCode -ne 1 -or
        $lateFailure.Report.report.result -ne 'error' -or
        $lateFailure.Report.report.error.code -ne 'native-headless.helper-failed' -or
        $lateFailure.Report.report.timing.decodedVideoFrames -ne 2 -or
        $lateFailure.Report.report.timing.renderedVideoFrames -ne 2 -or
        -not $lateFailure.Report.report.execution.sourceOpened -or
        -not $lateFailure.Report.report.execution.demuxStarted -or
        -not $lateFailure.Report.report.execution.playbackSampleObserved) {
        $failures.Add('A non-zero helper exit after valid telemetry did not preserve decoded/rendered native playback evidence in the error report.')
    }

    $networkFailure = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'late-http-seek-failure' `
        -StreamUrl 'https://example.invalid/media.mkv' `
        -HelperOutput (New-NativeHeadlessParserFixtureOutput -Overrides @{
            seekAttempted = '1'
            seekStatus = 'failed'
            seekActualPositionTicks = '-1'
            postSeekPlaybackPositionTicks = '10000000'
            postSeekAdvanced = '0'
            seekOperationDurationMs = '40000'
            seekRecoveryDurationMs = '0'
        }) `
        -HelperError "Error reading HTTP response: End of file`nWill reconnect at 94461602" `
        -HelperExitCode 9
    if ($networkFailure.ExitCode -ne 1 -or
        $networkFailure.Report.report.error.code -ne 'native-headless.network-io-failed' -or
        $networkFailure.Report.report.error.failureClass -ne 'external service/protocol issue' -or
        $networkFailure.Report.report.error.failureArea -ne 'error-handling') {
        $failures.Add('A late HTTP seek failure was not classified as an external service/protocol error.')
    }

    $openFailureMessage = 'avformat_open_input failed: No such file or directory'
    $openFailure = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'source-open-io-failure' `
        -HelperOutput '' `
        -HelperError $openFailureMessage `
        -HelperExitCode 2
    if ($openFailure.ExitCode -ne 1 -or
        $openFailure.Report.report.result -ne 'error' -or
        $openFailure.Report.report.error.code -ne 'source.open.missing-file' -or
        $openFailure.Report.report.error.operation -ne 'open' -or
        $openFailure.Report.report.error.failureClass -ne 'sample issue' -or
        $openFailure.Report.report.error.failureArea -ne 'error-handling' -or
        $openFailure.Report.report.error.isRetriable -ne $false -or
        $openFailure.Report.report.error.message -notmatch [regex]::Escape($openFailureMessage) -or
        $openFailure.Report.report.error.message -match 'decodedVideoFrames') {
        $failures.Add('A missing local media source was not preserved as a non-retriable sample error from the native open boundary.')
    }

    $unsupported = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'unsupported-dv-profile5' `
        -HelperOutput ('unsupportedCode=dolby-vision-profile5-no-fallback ' +
            'sourceCodec=hevc sourceWidth=3840 sourceHeight=2160 sourceFrameRate=60 ' +
            'sourceHdrKind=DolbyVisionUnsupported sourceVideoRange=Dolby_Vision ' +
            'sourceColorPrimaries=bt2020 sourceColorTransfer=smpte2084 sourceColorSpace=bt2020nc ' +
            'sourceIsDolbyVision=1 sourceDolbyVisionProfile=5 ' +
            'sourceDolbyVisionCompatibilityId=0 sourceHasHdr10BaseLayer=0 sourceHasHlgBaseLayer=0 ' +
            'containerStartTimeTicks=0 videoStreamStartTimeTicks=0 logicalDurationTicks=299500000') `
        -HelperExitCode 3
    if ($unsupported.ExitCode -ne 0 -or
        $unsupported.Report.report.result -ne 'unsupported' -or
        $unsupported.Report.report.execution.status -ne 'unsupported' -or
        $unsupported.Report.report.source.hdrKind -ne 'DolbyVisionUnsupported' -or
        $unsupported.Report.report.source.dolbyVisionProfile -ne 5 -or
        $unsupported.Report.report.source.isDirectPlayable -ne $false -or
        -not $unsupported.Report.report.execution.sourceOpened -or
        $unsupported.Report.report.execution.decoderOpened) {
        $failures.Add('Structured Dolby Vision Profile 5 rejection was not preserved as attributable unsupported-source evidence.')
    }
    $lateFailureStdoutPath = $lateFailure.ReportPath + '.helper.stdout.log'
    $lateFailureStderrPath = $lateFailure.ReportPath + '.helper.stderr.log'
    if (-not (Test-Path -LiteralPath $lateFailureStdoutPath) -or
        -not (Test-Path -LiteralPath $lateFailureStderrPath) -or
        [string]::IsNullOrWhiteSpace((Get-Content -LiteralPath $lateFailureStdoutPath -Raw))) {
        $failures.Add('Native helper stdout/stderr evidence was not archived next to the report.')
    }

    $audioSwitchOutput = New-NativeHeadlessParserFixtureOutput -Overrides @{
        audioSwitchAttempted = '1'
        audioSwitchStatus = 'completed'
        audioSwitchStreamIndex = '2'
        audioSwitchPositionBeforeTicks = '1000000'
        audioSwitchPositionAfterTicks = '2000000'
        audioSwitchSubmittedFramesBefore = '1'
        audioSwitchSubmittedFramesAfter = '2'
        audioSwitchOperationDurationMs = '310'
        audioSwitchRecoveryDurationMs = '5009'
        selectedAudioStreamIndex = '2'
    }
    $audioSwitch = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'completed-audio-switch' `
        -HelperOutput $audioSwitchOutput
    if ($audioSwitch.ExitCode -ne 0 -or
        $audioSwitch.Report.report.tracks.selectedAudioStreamIndex -ne 2 -or
        $audioSwitch.Report.report.interaction.scenario -ne 'audio-switch' -or
        $audioSwitch.Report.report.interaction.operationDurationMs -ne 310 -or
        $audioSwitch.Report.report.interaction.recoveryDurationMs -ne 5009 -or
        $audioSwitch.Report.report.interaction.positionDeltaTicks -ne 1000000 -or
        $audioSwitch.Report.report.interaction.submittedAudioFrameDelta -ne 1 -or
        -not ($audioSwitch.Report.report.lifecycle.events | Where-Object {
            $_.operation -eq 'audio-switch' -and $_.status -eq 'completed'
        })) {
        $failures.Add('Completed audio switch evidence was not preserved from the selected target and advancing playback counters.')
    }

    $seekOutput = New-NativeHeadlessParserFixtureOutput -Overrides @{
        seekAttempted = '1'
        seekStatus = 'completed'
        seekActualPositionTicks = '10000000'
        seekDemuxTargetTicks = '-1'
        postSeekPlaybackPositionTicks = '20000000'
        seekOperationDurationMs = '4800.5'
        seekRecoveryDurationMs = '5100.25'
        seekPacketCacheEnabled = '1'
        seekPacketCacheHit = '1'
        seekPacketCachePacketCount = '128'
        seekPacketCacheBytes = '1048576'
        seekPacketCacheWindowDurationTicks = '80000000'
        seekFallbackReason = 'none'
    }
    $seek = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'completed-seek-latency' `
        -HelperOutput $seekOutput
    if ($seek.ExitCode -ne 0 -or
        $seek.Report.report.position.seekOperationDurationMs -ne 4800.5 -or
        $seek.Report.report.position.seekRecoveryDurationMs -ne 5100.25 -or
        -not $seek.Report.report.position.seekPacketCacheEnabled -or
        -not $seek.Report.report.position.seekPacketCacheHit -or
        $seek.Report.report.position.seekPacketCachePacketCount -ne 128 -or
        $seek.Report.report.position.seekPacketCacheBytes -ne 1048576 -or
        $seek.Report.report.position.seekPacketCacheWindowDurationTicks -ne 80000000 -or
        $seek.Report.report.position.seekFallbackReason -ne 'none') {
        $failures.Add('Completed seek did not preserve structured latency and packet-cache evidence.')
    }

    $pausedSubtitleOutput = New-NativeHeadlessParserFixtureOutput -Overrides @{
        subtitleSwitch1Attempted = '1'
        subtitleSwitch1Status = 'completed'
        subtitleSwitch1StreamIndex = '4'
        subtitleSwitch1CueCountBefore = '1'
        subtitleSwitch1CueCountAfter = '2'
        subtitleSwitch1PausedSwitch = '1'
        subtitleSwitch1SelectedStreamIndex = '4'
        subtitleSwitch1PausedPositionBeforeTicks = '1000000'
        subtitleSwitch1PausedPositionAfterTicks = '1000000'
        subtitleSwitch1PositionBeforeResumeTicks = '1000000'
        subtitleSwitch1PositionAfterResumeTicks = '2000000'
        subtitleSwitch1OperationDurationMs = '125'
        subtitleSwitch1LockWaitDurationMs = '100'
        subtitleSwitch1ExecutionDurationMs = '25'
        subtitleSwitch1QuiesceDurationMs = '2'
        subtitleSwitch1SeekDurationMs = '10'
        subtitleSwitch1DecoderOpenDurationMs = '8'
        subtitleSwitch1RendererOpenDurationMs = '5'
        subtitleSwitch1PacketCacheHit = '1'
        subtitleSwitch1PacketCacheEnabled = '1'
        subtitleSwitch1PacketCachePacketCount = '120'
        subtitleSwitch1PacketCacheBytes = '524288'
        subtitleSwitch1PacketCacheWindowDurationTicks = '150000000'
        subtitleSwitch1RecoveryDurationMs = '875'
        subtitleSwitch1CueRenderDurationMs = '5009'
        subtitleSwitch1RenderedFramesBefore = '10'
        subtitleSwitch1RenderedFramesAfter = '34'
    }
    $pausedSubtitle = Invoke-NativeHeadlessParserFixtureCase `
        -FixtureHelper $fixtureHelper `
        -HeadlessDll $headlessDll `
        -Root $Root `
        -Name 'completed-paused-subtitle-switch' `
        -HelperOutput $pausedSubtitleOutput
    $pausedSubtitleEvent = @($pausedSubtitle.Report.report.lifecycle.events | Where-Object {
        $_.operation -eq 'subtitle-switch'
    }) | Select-Object -First 1
    if ($pausedSubtitle.ExitCode -ne 0 -or
        $pausedSubtitleEvent.status -ne 'completed' -or
        $pausedSubtitle.Report.report.interaction.scenario -ne 'subtitle-switch' -or
        $pausedSubtitle.Report.report.interaction.operationDurationMs -ne 125 -or
        $pausedSubtitle.Report.report.interaction.lockWaitDurationMs -ne 100 -or
        $pausedSubtitle.Report.report.interaction.executionDurationMs -ne 25 -or
        $pausedSubtitle.Report.report.interaction.quiesceDurationMs -ne 2 -or
        $pausedSubtitle.Report.report.interaction.seekDurationMs -ne 10 -or
        $pausedSubtitle.Report.report.interaction.decoderOpenDurationMs -ne 8 -or
        $pausedSubtitle.Report.report.interaction.rendererOpenDurationMs -ne 5 -or
        -not $pausedSubtitle.Report.report.interaction.packetCacheHit -or
        $pausedSubtitle.Report.report.interaction.packetCachePacketCount -ne 120 -or
        $pausedSubtitle.Report.report.interaction.packetCacheBytes -ne 524288 -or
        $pausedSubtitle.Report.report.interaction.packetCacheWindowDurationTicks -ne 150000000 -or
        $pausedSubtitle.Report.report.interaction.recoveryDurationMs -ne 875 -or
        $pausedSubtitle.Report.report.interaction.cueRenderDurationMs -ne 5009 -or
        $pausedSubtitle.Report.report.interaction.renderedVideoFrameDelta -ne 24 -or
        $pausedSubtitle.Report.report.interaction.subtitleCueRenderCountDelta -ne 1 -or
        $pausedSubtitle.Report.report.interaction.positionDeltaTicks -ne 1000000 -or
        $pausedSubtitleEvent.message -notmatch 'paused position 1000000->1000000; resumed position 1000000->2000000') {
        $failures.Add('Completed paused subtitle switch evidence did not preserve pause and resume progress observations.')
    }

    $negativeCases = @(
        [pscustomobject]@{
            Name = 'missing-video-decode-packet-read-stage'
            ExpectedField = 'videoDecodePacketReadDurationMsP95'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('videoDecodePacketReadDurationMsP95')
        },
        [pscustomobject]@{
            Name = 'missing-video-decode-send-packet-stage'
            ExpectedField = 'videoDecodeSendPacketDurationMsP95'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('videoDecodeSendPacketDurationMsP95')
        },
        [pscustomobject]@{
            Name = 'missing-video-decode-receive-frame-stage'
            ExpectedField = 'videoDecodeReceiveFrameDurationMsP95'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('videoDecodeReceiveFrameDurationMsP95')
        },
        [pscustomobject]@{
            Name = 'missing-video-decode-frame-materialize-stage'
            ExpectedField = 'videoDecodeFrameMaterializeDurationMsP95'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('videoDecodeFrameMaterializeDurationMsP95')
        },
        [pscustomobject]@{
            Name = 'missing-audio-switch-recovery-duration'
            ExpectedField = 'audioSwitchRecoveryDurationMs'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                audioSwitchAttempted = '1'
                audioSwitchStatus = 'completed'
                audioSwitchStreamIndex = '2'
                audioSwitchPositionBeforeTicks = '1000000'
                audioSwitchPositionAfterTicks = '2000000'
                audioSwitchSubmittedFramesBefore = '1'
                audioSwitchSubmittedFramesAfter = '2'
                selectedAudioStreamIndex = '2'
            } -Omit @('audioSwitchRecoveryDurationMs')
        },
        [pscustomobject]@{
            Name = 'nan-subtitle-switch-operation-duration'
            ExpectedField = 'subtitleSwitch1OperationDurationMs'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                subtitleSwitch1Attempted = '1'
                subtitleSwitch1Status = 'completed'
                subtitleSwitch1StreamIndex = '4'
                subtitleSwitch1CueCountBefore = '1'
                subtitleSwitch1CueCountAfter = '2'
                subtitleSwitch1OperationDurationMs = 'NaN'
            }
        },
        [pscustomobject]@{
            Name = 'missing-audio-position'
            ExpectedField = 'audioSwitchPositionAfterTicks'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                audioSwitchAttempted = '1'
                audioSwitchStatus = 'completed'
                audioSwitchStreamIndex = '2'
                audioSwitchPositionBeforeTicks = '1000000'
                audioSwitchSubmittedFramesBefore = '1'
                audioSwitchSubmittedFramesAfter = '2'
                selectedAudioStreamIndex = '2'
            } -Omit @('audioSwitchPositionAfterTicks')
        },
        [pscustomobject]@{
            Name = 'invalid-subtitle-cue'
            ExpectedField = 'subtitleSwitch1CueCountAfter'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                subtitleSwitch1Attempted = '1'
                subtitleSwitch1Status = 'completed'
                subtitleSwitch1StreamIndex = '4'
                subtitleSwitch1CueCountBefore = '0'
                subtitleSwitch1CueCountAfter = 'invalid'
            }
        },
        [pscustomobject]@{
            Name = 'invalid-attempted'
            ExpectedField = 'subtitleSwitch2Attempted'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                subtitleSwitch2Attempted = '2'
            }
        },
        [pscustomobject]@{
            Name = 'invalid-status'
            ExpectedField = 'seekStatus'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'
                seekStatus = 'observed'
                seekActualPositionTicks = '10000000'
                postSeekPlaybackPositionTicks = '20000000'
            }
        },
        [pscustomobject]@{
            Name = 'completed-seek-without-presented-frame'
            ExpectedField = 'seekActualPositionTicks'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'
                seekStatus = 'completed'
                seekActualPositionTicks = '-1'
                postSeekPlaybackPositionTicks = '20000000'
            }
        },
        [pscustomobject]@{
            Name = 'completed-seek-without-operation-duration'
            ExpectedField = 'seekOperationDurationMs'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'
                seekStatus = 'completed'
                seekActualPositionTicks = '10000000'
                postSeekPlaybackPositionTicks = '20000000'
                seekRecoveryDurationMs = '150'
            } -Omit @('seekOperationDurationMs')
        },
        [pscustomobject]@{
            Name = 'completed-seek-without-recovery-duration'
            ExpectedField = 'seekRecoveryDurationMs'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'
                seekStatus = 'completed'
                seekActualPositionTicks = '10000000'
                postSeekPlaybackPositionTicks = '20000000'
                seekOperationDurationMs = '120'
            } -Omit @('seekRecoveryDurationMs')
        },
        [pscustomobject]@{
            Name = 'completed-seek-without-cache-enabled'
            ExpectedField = 'seekPacketCacheEnabled'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'; seekStatus = 'completed'; seekActualPositionTicks = '10000000'; postSeekPlaybackPositionTicks = '20000000'
            } -Omit @('seekPacketCacheEnabled')
        },
        [pscustomobject]@{
            Name = 'completed-seek-without-cache-hit'
            ExpectedField = 'seekPacketCacheHit'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'; seekStatus = 'completed'; seekActualPositionTicks = '10000000'; postSeekPlaybackPositionTicks = '20000000'
            } -Omit @('seekPacketCacheHit')
        },
        [pscustomobject]@{
            Name = 'completed-seek-without-cache-packet-count'
            ExpectedField = 'seekPacketCachePacketCount'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'; seekStatus = 'completed'; seekActualPositionTicks = '10000000'; postSeekPlaybackPositionTicks = '20000000'
            } -Omit @('seekPacketCachePacketCount')
        },
        [pscustomobject]@{
            Name = 'completed-seek-without-cache-bytes'
            ExpectedField = 'seekPacketCacheBytes'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'; seekStatus = 'completed'; seekActualPositionTicks = '10000000'; postSeekPlaybackPositionTicks = '20000000'
            } -Omit @('seekPacketCacheBytes')
        },
        [pscustomobject]@{
            Name = 'completed-seek-without-cache-window'
            ExpectedField = 'seekPacketCacheWindowDurationTicks'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'; seekStatus = 'completed'; seekActualPositionTicks = '10000000'; postSeekPlaybackPositionTicks = '20000000'
            } -Omit @('seekPacketCacheWindowDurationTicks')
        },
        [pscustomobject]@{
            Name = 'completed-seek-without-fallback-reason'
            ExpectedField = 'seekFallbackReason'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                seekAttempted = '1'; seekStatus = 'completed'; seekActualPositionTicks = '10000000'; postSeekPlaybackPositionTicks = '20000000'
            } -Omit @('seekFallbackReason')
        },
        [pscustomobject]@{
            Name = 'missing-dropped-video-frames'
            ExpectedField = 'droppedVideoFrames'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('droppedVideoFrames')
        },
        [pscustomobject]@{
            Name = 'missing-ffmpeg-open-input-bytes'
            ExpectedField = 'ffmpegOpenInputBytesRead'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('ffmpegOpenInputBytesRead')
        },
        [pscustomobject]@{
            Name = 'missing-ffmpeg-stream-info-bytes'
            ExpectedField = 'ffmpegStreamInfoBytesRead'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('ffmpegStreamInfoBytesRead')
        },
        [pscustomobject]@{
            Name = 'missing-native-startup-seek-bytes'
            ExpectedField = 'nativeStartupSeekBytesRead'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('nativeStartupSeekBytesRead')
        },
        [pscustomobject]@{
            Name = 'missing-native-first-frame-transport-bytes'
            ExpectedField = 'nativeFirstFrameTransportBytesRead'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('nativeFirstFrameTransportBytesRead')
        },
        [pscustomobject]@{
            Name = 'missing-audio-ahead-end-to-present-count'
            ExpectedField = 'audioAheadWaitEndToPresentSampleCount'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('audioAheadWaitEndToPresentSampleCount')
        },
        [pscustomobject]@{
            Name = 'nan-audio-ahead-end-to-present-p95'
            ExpectedField = 'audioAheadWaitEndToPresentMsP95'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                audioAheadWaitEndToPresentMsP95 = 'NaN'
            }
        },
        [pscustomobject]@{
            Name = 'negative-video-starvation'
            ExpectedField = 'videoStarvedPasses'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                videoStarvedPasses = '-1'
            }
        },
        [pscustomobject]@{
            Name = 'invalid-audio-starvation'
            ExpectedField = 'audioStarvedPasses'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                audioStarvedPasses = 'invalid'
            }
        },
        [pscustomobject]@{
            Name = 'nan-av-drift'
            ExpectedField = 'audioVideoDriftMsP95'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                audioVideoDriftMsP95 = 'NaN'
            }
        },
        [pscustomobject]@{
            Name = 'infinite-present-percentile'
            ExpectedField = 'presentDurationMsP95'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                presentDurationMsP95 = 'Infinity'
            }
        },
        [pscustomobject]@{
            Name = 'missing-video-decode-percentile'
            ExpectedField = 'videoDecodeDurationMsP95'
            Output = New-NativeHeadlessParserFixtureOutput -Omit @('videoDecodeDurationMsP95')
        },
        [pscustomobject]@{
            Name = 'infinite-video-render-percentile'
            ExpectedField = 'videoRenderDurationMsP95'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                videoRenderDurationMsP95 = 'Infinity'
            }
        },
        [pscustomobject]@{
            Name = 'completed-audio-switch-with-wrong-selection'
            ExpectedField = 'selectedAudioStreamIndex'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                audioSwitchAttempted = '1'
                audioSwitchStatus = 'completed'
                audioSwitchStreamIndex = '2'
                audioSwitchPositionBeforeTicks = '1000000'
                audioSwitchPositionAfterTicks = '2000000'
                audioSwitchSubmittedFramesBefore = '1'
                audioSwitchSubmittedFramesAfter = '2'
                selectedAudioStreamIndex = '1'
            }
        },
        [pscustomobject]@{
            Name = 'completed-audio-switch-without-position-progress'
            ExpectedField = 'audioSwitchPositionAfterTicks'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                audioSwitchAttempted = '1'
                audioSwitchStatus = 'completed'
                audioSwitchStreamIndex = '2'
                audioSwitchPositionBeforeTicks = '1000000'
                audioSwitchPositionAfterTicks = '1000000'
                audioSwitchSubmittedFramesBefore = '1'
                audioSwitchSubmittedFramesAfter = '2'
                selectedAudioStreamIndex = '2'
            }
        },
        [pscustomobject]@{
            Name = 'completed-audio-switch-without-submitted-progress'
            ExpectedField = 'audioSwitchSubmittedFramesAfter'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                audioSwitchAttempted = '1'
                audioSwitchStatus = 'completed'
                audioSwitchStreamIndex = '2'
                audioSwitchPositionBeforeTicks = '1000000'
                audioSwitchPositionAfterTicks = '2000000'
                audioSwitchSubmittedFramesBefore = '1'
                audioSwitchSubmittedFramesAfter = '1'
                selectedAudioStreamIndex = '2'
            }
        },
        [pscustomobject]@{
            Name = 'completed-paused-subtitle-switch-with-wrong-selection'
            ExpectedField = 'subtitleSwitch1SelectedStreamIndex'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                subtitleSwitch1Attempted = '1'
                subtitleSwitch1Status = 'completed'
                subtitleSwitch1StreamIndex = '4'
                subtitleSwitch1CueCountBefore = '1'
                subtitleSwitch1CueCountAfter = '2'
                subtitleSwitch1PausedSwitch = '1'
                subtitleSwitch1SelectedStreamIndex = '5'
                subtitleSwitch1PausedPositionBeforeTicks = '1000000'
                subtitleSwitch1PausedPositionAfterTicks = '1000000'
                subtitleSwitch1PositionBeforeResumeTicks = '1000000'
                subtitleSwitch1PositionAfterResumeTicks = '2000000'
            }
        },
        [pscustomobject]@{
            Name = 'completed-paused-subtitle-switch-without-paused-state'
            ExpectedField = 'subtitleSwitch1PausedPositionAfterTicks'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                subtitleSwitch1Attempted = '1'
                subtitleSwitch1Status = 'completed'
                subtitleSwitch1StreamIndex = '4'
                subtitleSwitch1CueCountBefore = '1'
                subtitleSwitch1CueCountAfter = '2'
                subtitleSwitch1PausedSwitch = '1'
                subtitleSwitch1SelectedStreamIndex = '4'
                subtitleSwitch1PausedPositionBeforeTicks = '1000000'
                subtitleSwitch1PausedPositionAfterTicks = '1100000'
                subtitleSwitch1PositionBeforeResumeTicks = '1100000'
                subtitleSwitch1PositionAfterResumeTicks = '2000000'
            }
        },
        [pscustomobject]@{
            Name = 'completed-paused-subtitle-switch-without-resume-progress'
            ExpectedField = 'subtitleSwitch1PositionAfterResumeTicks'
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
                subtitleSwitch1Attempted = '1'
                subtitleSwitch1Status = 'completed'
                subtitleSwitch1StreamIndex = '4'
                subtitleSwitch1CueCountBefore = '1'
                subtitleSwitch1CueCountAfter = '2'
                subtitleSwitch1PausedSwitch = '1'
                subtitleSwitch1SelectedStreamIndex = '4'
                subtitleSwitch1PausedPositionBeforeTicks = '1000000'
                subtitleSwitch1PausedPositionAfterTicks = '1000000'
                subtitleSwitch1PositionBeforeResumeTicks = '1000000'
                subtitleSwitch1PositionAfterResumeTicks = '1000000'
            }
        }
    )

    $requiredTransportCallFields = @(
        'startupTransportProvider',
        'startupTransportCallEvidenceAvailable',
        'ffmpegOpenInputTransportProvider',
        'ffmpegOpenInputTransportCallEvidenceAvailable',
        'ffmpegOpenInputTransportReadCalls',
        'ffmpegOpenInputTransportSeekCalls',
        'ffmpegOpenInputTransportReadWaitMs',
        'ffmpegOpenInputTransportSeekWaitMs',
        'ffmpegOpenInputTransportSeekDistanceBytes',
        'ffmpegStreamInfoTransportProvider',
        'ffmpegStreamInfoTransportCallEvidenceAvailable',
        'ffmpegStreamInfoTransportReadCalls',
        'ffmpegStreamInfoTransportSeekCalls',
        'ffmpegStreamInfoTransportReadWaitMs',
        'ffmpegStreamInfoTransportSeekWaitMs',
        'ffmpegStreamInfoTransportSeekDistanceBytes',
        'nativeStartupSeekTransportProvider',
        'nativeStartupSeekTransportCallEvidenceAvailable',
        'nativeStartupSeekTransportReadCalls',
        'nativeStartupSeekTransportSeekCalls',
        'nativeStartupSeekTransportReadWaitMs',
        'nativeStartupSeekTransportSeekWaitMs',
        'nativeStartupSeekTransportSeekDistanceBytes',
        'nativeFirstFrameTransportProvider',
        'nativeFirstFrameTransportCallEvidenceAvailable',
        'nativeFirstFrameTransportReadCalls',
        'nativeFirstFrameTransportSeekCalls',
        'nativeFirstFrameTransportReadWaitMs',
        'nativeFirstFrameTransportSeekWaitMs',
        'nativeFirstFrameTransportSeekDistanceBytes',
        'playbackTransportProvider',
        'playbackTransportCallEvidenceAvailable',
        'playbackTransportReadCalls',
        'playbackTransportSeekCalls',
        'playbackTransportReadWaitMs',
        'playbackTransportSeekWaitMs',
        'playbackTransportSeekDistanceBytes'
    )
    foreach ($field in $requiredTransportCallFields) {
        $negativeCases += [pscustomobject]@{
            Name = 'missing-' + ($field -creplace '([a-z0-9])([A-Z])', '$1-$2').ToLowerInvariant()
            ExpectedField = $field
            Output = New-NativeHeadlessParserFixtureOutput -Omit @($field)
        }
    }
    foreach ($field in @('lastReadErrorCode', 'fatalReadErrorCode')) {
        $negativeCases += [pscustomobject]@{
            Name = 'positive-' + ($field -creplace '([a-z0-9])([A-Z])', '$1-$2').ToLowerInvariant()
            ExpectedField = $field
            Output = New-NativeHeadlessParserFixtureOutput -Overrides @{ $field = '1' }
        }
    }
    foreach ($field in @(
        'observedSampleWallClockDurationMs',
        'videoDecodeDeviceMode',
        'videoDecodeSynchronizationMode',
        'videoDecodeWorkerActive',
        'videoDecodeQueueCapacity',
        'videoDecodeQueueMaxDepth',
        'videoDecodeQueueProducerWaitCount',
        'playbackDemuxReadDurationMs',
        'playbackDemuxPacketCount',
        'playbackDemuxBytes',
        'readErrorCount',
        'readRetryCount',
        'readRecoveryCount',
        'maxConsecutiveReadErrors',
        'lastReadErrorCode',
        'fatalReadErrorCode',
        'lastReadRecoveryDurationMs'
    )) {
        $negativeCases += [pscustomobject]@{
            Name = 'missing-' + ($field -creplace '([a-z0-9])([A-Z])', '$1-$2').ToLowerInvariant()
            ExpectedField = $field
            Output = New-NativeHeadlessParserFixtureOutput -Omit @($field)
        }
    }
    $negativeCases += [pscustomobject]@{
        Name = 'builtin-provider-with-measured-call-evidence'
        ExpectedField = 'startupTransportCallEvidenceAvailable'
        Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
            startupTransportProvider = 'ffmpeg-builtin'
            startupTransportCallEvidenceAvailable = '1'
        }
    }
    $negativeCases += [pscustomobject]@{
        Name = 'instrumented-provider-with-unavailable-call-evidence'
        ExpectedField = 'startupTransportCallEvidenceAvailable'
        Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
            startupTransportProvider = 'instrumented-ffmpeg-avio'
            startupTransportCallEvidenceAvailable = '0'
        }
    }
    foreach ($field in @(
        'endOfStreamAttempted',
        'endOfStreamObserved',
        'endOfStreamStatus',
        'endOfStreamPositionTicks'
    )) {
        $negativeCases += [pscustomobject]@{
            Name = 'missing-' + ($field -creplace '([a-z0-9])([A-Z])', '$1-$2').ToLowerInvariant()
            ExpectedField = $field
            Output = New-NativeHeadlessParserFixtureOutput -Omit @($field)
        }
    }
    $negativeCases += [pscustomobject]@{
        Name = 'end-of-stream-completed-without-observation'
        ExpectedField = 'endOfStreamObserved'
        Output = New-NativeHeadlessParserFixtureOutput -Overrides @{
            endOfStreamAttempted = '1'
            endOfStreamObserved = '0'
            endOfStreamStatus = 'completed'
            endOfStreamPositionTicks = '60000000'
        }
    }

    foreach ($negativeCase in $negativeCases) {
        $result = Invoke-NativeHeadlessParserFixtureCase `
            -FixtureHelper $fixtureHelper `
            -HeadlessDll $headlessDll `
            -Root $Root `
            -Name $negativeCase.Name `
            -HelperOutput $negativeCase.Output
        if ($result.ExitCode -ne 1 -or
            $result.Report.report.error.code -ne 'native-headless.helper-failed' -or
            $result.Report.report.error.failureArea -ne 'evidence-collection' -or
            $result.Report.report.error.message -notmatch [regex]::Escape($negativeCase.ExpectedField) -or
            @($result.Report.report.lifecycle.events | Where-Object {
                $_.operation -in @('audio-switch', 'subtitle-switch', 'subtitle-off', 'seek', 'endOfStream')
            }).Count -ne 0 -or
            @($result.Report.report.lifecycle.events | Where-Object {
                $_.status -eq 'completed'
            }).Count -ne 0 -or
            $result.Report.report.runtimeMetrics.status -eq 'captured') {
            $failures.Add("Parser fixture '$($negativeCase.Name)' did not fail explicitly on $($negativeCase.ExpectedField).")
        }
    }

    if ($failures.Count -gt 0) {
        throw ("Native-headless parser contract failures:`n- " + ($failures -join "`n- "))
    }

    Write-Host 'native-headless-parser contracts ok'
}

if (Test-Path $smokeRoot) {
    $resolvedSmokeRoot = (Resolve-Path $smokeRoot).Path
    if (-not $resolvedSmokeRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove smoke output outside repo root: $resolvedSmokeRoot"
    }

    Remove-Item -LiteralPath $smokeRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $smokeRoot | Out-Null

Assert-NativeHeadlessParserContracts -Root (Join-Path $smokeRoot 'parser-fixtures')
if ($ParserContractOnly) {
    Write-Host 'native-headless-parser contract smoke ok'
    exit 0
}

if ($DemuxReadRecoveryOnly) {
    $nativeHelperExe = Build-NativePlaybackGraphHelper -OutputDirectory $nativeHelperRoot
    $nativeAvSampleUrl = New-NativePlaybackAvSample
    Assert-NativePlaybackAvSample -SampleUrl $nativeAvSampleUrl
    Assert-NativeDemuxReadRecovery `
        -NativeHelperExe $nativeHelperExe `
        -SampleUrl $nativeAvSampleUrl `
        -ExpectFailure:$ExpectDemuxReadRecoveryFailure
    $archiveName = $SourceRevision -replace '[^A-Za-z0-9._-]', '_'
    $archiveRoot = Join-Path $repoRoot (Join-Path 'artifacts\quality-run\demux-read-recovery-v0.9' $archiveName)
    if (Test-Path -LiteralPath $archiveRoot) {
        Remove-Item -LiteralPath $archiveRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $archiveRoot | Out-Null
    Copy-Item `
        -LiteralPath (Join-Path $smokeRoot 'demux-read-error-recovery-formal') `
        -Destination $archiveRoot `
        -Recurse
    foreach ($logName in @(
        'demux-read-error-recovery-server.out.log',
        'demux-read-error-recovery-server.err.log')) {
        $logPath = Join-Path $smokeRoot $logName
        if (Test-Path -LiteralPath $logPath) {
            Copy-Item -LiteralPath $logPath -Destination $archiveRoot
        }
    }
    [ordered]@{
        sourceRevision = $SourceRevision
        expectFailure = [bool]$ExpectDemuxReadRecoveryFailure
        recoveryDisabled = [Environment]::GetEnvironmentVariable(
            'NOIRAPLAYER_QA_DISABLE_DEMUX_READ_RECOVERY',
            'Process') -eq '1'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $archiveRoot 'run-metadata.json') -Encoding UTF8
    Write-Host "native demux read recovery evidence archived: $archiveRoot"
    Write-Host 'native demux read recovery focused smoke ok'
    exit 0
}

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Headless\NoiraPlayer.PlaybackQuality.Headless.csproj') -- `
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

    if (-not ($report.report.limitations -contains 'native-headless: current NoiraPlayer.Native build is a Windows Store C++/WinRT component with public playback entrypoints bound to UWP projection')) {
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
      "executionRequirement": {
        "minimumEvidenceLevel": "native-playback",
        "scenario": "playback"
      },
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

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
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

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
    validate-report-set `
    --manifest $manifestPath `
    --reports-dir $materializedDir `
    --output $validationPath
if ($LASTEXITCODE -eq 0) {
    throw 'A stable native-playback case must not accept an orchestration-only skip report.'
}

function New-NativePlaybackNonZeroStartSample {
    $sampleDirectory = Join-Path $smokeRoot 'samples'
    New-Item -ItemType Directory -Path $sampleDirectory -Force | Out-Null
    $samplePath = Join-Path $sampleDirectory 'native-headless-nonzero-start.ts'
    $ffmpeg = 'C:\Program Files\FFmpeg\bin\ffmpeg.exe'

    & $ffmpeg `
        -y `
        -loglevel error `
        -f lavfi `
        -i 'testsrc2=size=320x180:rate=30:duration=6' `
        -f lavfi `
        -i 'sine=frequency=1000:sample_rate=48000:duration=6' `
        -vf 'setparams=range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709' `
        -pix_fmt yuv420p `
        -c:v libx264 `
        -g 1 `
        -bf 0 `
        -c:a aac `
        -f mpegts `
        $samplePath
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to generate the non-zero-start native timeline sample.'
    }

    $ffprobe = 'C:\Program Files\FFmpeg\bin\ffprobe.exe'
    $probeJson = & $ffprobe `
        -v error `
        -show_entries 'format=start_time,duration:stream=index,codec_type,start_time,duration' `
        -of json `
        $samplePath
    $probe = $probeJson | ConvertFrom-Json
    $video = @($probe.streams | Where-Object codec_type -eq 'video') | Select-Object -First 1
    if ($LASTEXITCODE -ne 0 -or
        [double]$probe.format.start_time -lt 1.0 -or
        [double]$video.start_time -le [double]$probe.format.start_time -or
        [Math]::Abs([double]$video.duration - 6.0) -gt 0.001) {
        throw 'Expected a deterministic six-second MPEG-TS sample with a non-zero container/stream start time.'
    }

    return ([System.Uri](Resolve-Path $samplePath).Path).AbsoluteUri
}

$validation = Get-Content -LiteralPath $validationPath -Raw | ConvertFrom-Json
if ($validation.isValid -ne $false -or
    $validation.executionValid -ne $false -or
    $validation.matchedCaseCount -ne 0 -or
    -not (@($validation.errors.code) -contains 'report.execution.evidence-level.insufficient')) {
    throw 'Expected strict validation to reject the orchestration-only stable skip report.'
}

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
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

if (-not ($analysis.limitations -contains 'native-headless: current NoiraPlayer.Native build is a Windows Store C++/WinRT component with public playback entrypoints bound to UWP projection')) {
    throw 'Expected analyze-report-set summary to preserve native-headless linkage limitation.'
}

if (-not ($analysis.limitations -contains 'native-headless: offscreen DirectX composition swapchain is smoke-tested, but this runner still lacks a native PlaybackGraph host and lifecycle bridge')) {
    throw 'Expected analyze-report-set summary to preserve the narrowed native graph host limitation.'
}

$nativeHelperExe = Build-NativePlaybackGraphHelper -OutputDirectory $nativeHelperRoot
$nativeSampleUrl = New-NativePlaybackSample
$nativeAvSampleUrl = New-NativePlaybackAvSample
$nativeNonZeroTimelineSampleUrl = New-NativePlaybackNonZeroStartSample
$nativeMissingFileUrl = ([System.Uri](Join-Path $smokeRoot 'samples\intentionally-missing-media.mp4')).AbsoluteUri
if (Test-Path -LiteralPath ([System.Uri]$nativeMissingFileUrl).LocalPath) {
    throw 'The native missing-file case source must not exist.'
}
Assert-NativePlaybackAvSample -SampleUrl $nativeAvSampleUrl
Assert-NativeNetworkReconnectRecovery -NativeHelperExe $nativeHelperExe -SampleUrl $nativeAvSampleUrl
Assert-NativeLongPauseNetworkRecovery -NativeHelperExe $nativeHelperExe -SampleUrl $nativeAvSampleUrl
Assert-NativeDemuxReadRecovery -NativeHelperExe $nativeHelperExe -SampleUrl $nativeAvSampleUrl
$nativeNonZeroTimelineReport = Invoke-NativeHeadlessHelperCase `
    -CaseId $nativeNonZeroTimelineCaseId `
    -StreamUrl $nativeNonZeroTimelineSampleUrl `
    -ReportsDir $nativeCapturedDir `
    -NativeHelperExe $nativeHelperExe `
    -DurationSeconds 3 `
    -Scenario timeline
$timelinePauseCount = @($nativeNonZeroTimelineReport.report.lifecycle.events |
    Where-Object operation -eq 'pause').Count
if ($timelinePauseCount -ne 0 -or
    $nativeNonZeroTimelineReport.report.source.containerStartTimeTicks -lt 10000000 -or
    $nativeNonZeroTimelineReport.report.source.videoStreamStartTimeTicks -le $nativeNonZeroTimelineReport.report.source.containerStartTimeTicks -or
    [Math]::Abs($nativeNonZeroTimelineReport.report.source.durationTicks - 60000000) -gt 10000 -or
    $nativeNonZeroTimelineReport.report.position.seekDemuxTargetTicks -ne
        ($nativeNonZeroTimelineReport.report.position.seekTargetPositionTicks +
            $nativeNonZeroTimelineReport.report.source.containerStartTimeTicks) -or
    $nativeNonZeroTimelineReport.report.position.firstPresentedPositionTicks -ne
        $nativeNonZeroTimelineReport.report.position.actualPositionTicks -or
    $nativeNonZeroTimelineReport.report.position.seekPositionErrorMs -gt 100.0 -or
    $nativeNonZeroTimelineReport.report.position.postSeekAdvanced -ne $true -or
    $nativeNonZeroTimelineReport.report.position.postSeekPositionTicks -le
        $nativeNonZeroTimelineReport.report.position.firstPresentedPositionTicks) {
    throw 'Non-zero-start timeline evidence was not normalized or seek did not land and continue on the public timeline.'
}
$nativeResumeSeekTimelineReport = Invoke-NativeHeadlessHelperCase `
    -CaseId $nativeResumeSeekTimelineCaseId `
    -StreamUrl $nativeNonZeroTimelineSampleUrl `
    -ReportsDir $nativeCapturedDir `
    -NativeHelperExe $nativeHelperExe `
    -DurationSeconds 3 `
    -StartPositionTicks 20000000 `
    -Scenario timeline
$resumePauseCount = @($nativeResumeSeekTimelineReport.report.lifecycle.events |
    Where-Object operation -eq 'pause').Count
if ($resumePauseCount -ne 0 -or
    $nativeResumeSeekTimelineReport.report.position.requestedStartPositionTicks -ne 20000000 -or
    $nativeResumeSeekTimelineReport.report.position.seekTargetPositionTicks -ne 30000000 -or
    $nativeResumeSeekTimelineReport.report.position.seekDemuxTargetTicks -ne 44000000 -or
    $nativeResumeSeekTimelineReport.report.position.seekPositionErrorMs -gt 100.0 -or
    $nativeResumeSeekTimelineReport.report.position.postSeekAdvanced -ne $true) {
    throw 'Resume plus seek did not preserve the requested public timeline through native open, demux seek, presentation, and advancement.'
}
$nativeMissingFileReport = Invoke-NativeHeadlessHelperCase `
    -CaseId $nativeMissingFileCaseId `
    -StreamUrl $nativeMissingFileUrl `
    -ReportsDir $nativeCapturedDir `
    -NativeHelperExe $nativeHelperExe `
    -DurationSeconds 1 `
    -ExpectError
if ($nativeMissingFileReport.report.result -ne 'error' -or
    $nativeMissingFileReport.report.execution.evidenceLevel -ne 'native-playback' -or
    $nativeMissingFileReport.report.execution.sourceOpenAttempted -ne $true -or
    $nativeMissingFileReport.report.execution.sourceOpened -ne $false -or
    $nativeMissingFileReport.report.error.code -ne 'source.open.missing-file' -or
    $nativeMissingFileReport.report.error.operation -ne 'open' -or
    $nativeMissingFileReport.report.error.failureClass -ne 'sample issue' -or
    $nativeMissingFileReport.report.error.failureArea -ne 'error-handling' -or
    $nativeMissingFileReport.report.error.isRetriable -ne $false) {
    throw 'Missing local media did not produce a real, classified native open-boundary error report.'
}
$nativeSdr23976SampleUrl = New-NativePlaybackSdrSample -Name 'native-headless-sdr-23976' -Rate '24000/1001'
$nativeSdr24SampleUrl = New-NativePlaybackSdrSample -Name 'native-headless-sdr-24' -Rate '24'
$nativeSdr60SampleUrl = New-NativePlaybackSdrSample -Name 'native-headless-sdr-60' -Rate '60'
$nativeHdr1023976SampleUrl = New-NativePlaybackHdr10Sample -Name 'native-headless-hdr10-23976' -Rate '24000/1001'
$nativeHdr1024SampleUrl = New-NativePlaybackHdr10Sample -Name 'native-headless-hdr10-24' -Rate '24'
$nativeHdr1030SampleUrl = New-NativePlaybackHdr10Sample -Name 'native-headless-hdr10-30' -Rate '30'
$nativeHdr1060SampleUrl = New-NativePlaybackHdr10Sample -Name 'native-headless-hdr10-60' -Rate '60'

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Headless\NoiraPlayer.PlaybackQuality.Headless.csproj') -- `
    --case-id $nativeCaseId `
    --stream-url $nativeSampleUrl `
    --duration-seconds $nativeCadenceDurationSeconds `
    --force-sdr-output `
    --reports-dir $nativeCapturedDir `
    --native-helper-exe $nativeHelperExe
$nativeHelperExitCode = $LASTEXITCODE

if ($nativeHelperExitCode -ne 0) {
    throw 'Expected native-headless harness to run the App-free native helper.'
}

if (-not (Test-Path $nativeReportPath)) {
    throw "Expected native helper captured report at $nativeReportPath."
}
$nativeHelperStderrPath = $nativeReportPath + '.helper.stderr.log'
if (-not (Test-Path -LiteralPath $nativeHelperStderrPath) -or
    -not (Select-String -LiteralPath $nativeHelperStderrPath -SimpleMatch 'helperStage=graph-open-completed' -Quiet) -or
    -not (Select-String -LiteralPath $nativeHelperStderrPath -SimpleMatch 'helperStage=completed' -Quiet)) {
    throw 'Expected native helper transcript to identify completed graph-open and helper stages.'
}

$nativeReport = Get-Content -LiteralPath $nativeReportPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($nativeReport.report.execution.attemptId) -or
    $nativeReport.report.execution.runner -ne 'native-headless' -or
    $nativeReport.report.execution.evidenceLevel -ne 'native-playback' -or
    $nativeReport.report.execution.status -ne 'completed' -or
    [string]::IsNullOrWhiteSpace($nativeReport.report.execution.sourceLocatorHash) -or
    [string]::IsNullOrWhiteSpace($nativeReport.report.execution.openedSourceHash) -or
    $nativeReport.report.execution.sourceOpenAttempted -ne $true -or
    $nativeReport.report.execution.sourceOpened -ne $true -or
    $nativeReport.report.execution.nativeGraphOpened -ne $true -or
    $nativeReport.report.execution.demuxStarted -ne $true -or
    $nativeReport.report.execution.decoderOpened -ne $true -or
    $nativeReport.report.execution.playbackSampleObserved -ne $true) {
    throw 'Expected native helper report to carry complete native-playback execution provenance.'
}

if ($nativeReport.report.runtimeMetrics.providerStatus -ne 'native-headless:returned-snapshot') {
    throw 'Expected native helper report to carry native-headless runtime metrics evidence.'
}

if ($nativeReport.report.timing.decodedVideoFrames -le 0) {
    throw 'Expected native helper report to include decoded video frames.'
}

if (($nativeReport.report.timing.hardwareDecodedVideoFrames + $nativeReport.report.timing.softwareDecodedVideoFrames) -ne
    $nativeReport.report.timing.decodedVideoFrames) {
    throw 'Expected native helper report to split decoded frames into hardware/software decode-mode counts.'
}

if ($nativeReport.report.timing.renderedVideoFrames -le 0) {
    throw 'Expected native helper report to include rendered video frames.'
}

if ($nativeReport.report.colorPipeline.forceSdrOutput -ne $true) {
    throw 'Expected native-headless --force-sdr-output to populate colorPipeline.forceSdrOutput evidence.'
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

$nativeEndOfStreamReport = Invoke-NativeHeadlessHelperCase `
    -CaseId $nativeEndOfStreamCaseId `
    -StreamUrl $nativeSampleUrl `
    -ReportsDir $nativeCapturedDir `
    -NativeHelperExe $nativeHelperExe `
    -DurationSeconds 12 `
    -Scenario end-of-stream
$nativeEndOfStreamEvents = @($nativeEndOfStreamReport.report.lifecycle.events |
    Where-Object operation -eq 'endOfStream')
if ($nativeEndOfStreamReport.report.execution.scenario -ne 'end-of-stream' -or
    $nativeEndOfStreamReport.report.execution.status -ne 'completed' -or
    $nativeEndOfStreamEvents.Count -ne 1 -or
    $nativeEndOfStreamEvents[0].status -ne 'completed' -or
    $nativeEndOfStreamEvents[0].positionTicks -lt 0) {
    throw 'Expected the stable native end-of-stream case to preserve one natural PlaybackGraph completion event.'
}
if (-not (Select-String -LiteralPath ($nativeEndOfStreamReportPath + '.helper.stderr.log') `
    -SimpleMatch 'helperStage=end-of-stream-observed' -Quiet)) {
    throw 'Expected native helper stderr to prove that PlaybackGraph natural end-of-stream was observed.'
}

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Headless\NoiraPlayer.PlaybackQuality.Headless.csproj') -- `
    --case-id $nativeAvCaseId `
    --stream-url $nativeAvSampleUrl `
    --duration-seconds $nativeAvDurationSeconds `
    --scenario audio-switch `
    --reports-dir $nativeCapturedDir `
    --native-helper-exe $nativeHelperExe
$nativeAvHelperExitCode = $LASTEXITCODE

if ($nativeAvHelperExitCode -ne 0) {
    throw 'Expected native-headless harness to run the App-free native helper for the A/V sample.'
}

if (-not (Test-Path $nativeAvReportPath)) {
    throw "Expected native helper A/V captured report at $nativeAvReportPath."
}

$nativeAvReport = Get-Content -LiteralPath $nativeAvReportPath -Raw | ConvertFrom-Json

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Headless\NoiraPlayer.PlaybackQuality.Headless.csproj') -- `
    --case-id $nativeSubtitleCaseId `
    --stream-url $nativeAvSampleUrl `
    --duration-seconds $nativeAvDurationSeconds `
    --scenario subtitle-switch `
    --reports-dir $nativeCapturedDir `
    --native-helper-exe $nativeHelperExe
$nativeSubtitleHelperExitCode = $LASTEXITCODE

if ($nativeSubtitleHelperExitCode -ne 0 -or -not (Test-Path $nativeSubtitleReportPath)) {
    throw 'Expected native-headless harness to capture the independent subtitle-switch case.'
}

$nativeSubtitleReport = Get-Content -LiteralPath $nativeSubtitleReportPath -Raw | ConvertFrom-Json
if ($nativeAvReport.report.tracks.audioTrackCount -ne 2 -or
    $nativeAvReport.report.tracks.audio.Count -ne 2) {
    throw 'Expected native helper A/V report to include exactly two discovered audio tracks.'
}

if ($nativeSubtitleReport.report.tracks.subtitleTrackCount -ne 2 -or
    $nativeSubtitleReport.report.tracks.subtitles.Count -ne 2) {
    throw 'Expected native helper A/V report to include exactly two discovered subtitle tracks.'
}

if ($null -ne $nativeAvReport.report.position.seekTargetPositionTicks -or
    @($nativeAvReport.report.lifecycle.events | Where-Object operation -eq 'seek').Count -ne 0) {
    throw 'Interaction-only native helper case must not mix seek evidence into track/subtitle results.'
}

$nativeAvLifecycleEvents = @($nativeAvReport.report.lifecycle.events)
$audioSwitchEvents = @($nativeAvLifecycleEvents | Where-Object { $_.operation -eq 'audio-switch' })
if ($audioSwitchEvents.Count -ne 1 -or
    $audioSwitchEvents[0].status -ne 'completed' -or
    [string]::IsNullOrWhiteSpace($audioSwitchEvents[0].message)) {
    throw 'Expected one completed audio-switch lifecycle event with real interaction evidence.'
}

if ($nativeAvReport.report.interaction.seekDurationMs -ne 0) {
    throw 'Cached audio-switch case must not perform a demux/video seek.'
}
if (-not $nativeAvReport.report.interaction.packetCacheHit -or
    $nativeAvReport.report.interaction.packetCachePacketCount -gt 4096 -or
    $nativeAvReport.report.interaction.packetCacheBytes -gt 8388608 -or
    $nativeAvReport.report.interaction.packetCacheWindowDurationTicks -gt 300000000) {
    throw 'Audio switch packet cache evidence must prove a bounded cache hit.'
}

$nativeSubtitleLifecycleEvents = @($nativeSubtitleReport.report.lifecycle.events)
$subtitleSwitchEvents = @($nativeSubtitleLifecycleEvents | Where-Object { $_.operation -eq 'subtitle-switch' })
if ($subtitleSwitchEvents.Count -ne 1 -or
    @($subtitleSwitchEvents | Where-Object { $_.status -ne 'completed' }).Count -ne 0 -or
    @($subtitleSwitchEvents | Where-Object { [string]::IsNullOrWhiteSpace($_.message) }).Count -ne 0) {
    throw 'Expected one completed subtitle-switch event with cue-render evidence.'
}

if ($nativeSubtitleReport.report.interaction.seekDurationMs -ne 0) {
    throw 'Cached subtitle-switch case must not perform a demux/video seek.'
}
if (-not $nativeSubtitleReport.report.interaction.packetCacheHit -or
    $nativeSubtitleReport.report.interaction.packetCachePacketCount -gt 4096 -or
    $nativeSubtitleReport.report.interaction.packetCacheBytes -gt 8388608 -or
    $nativeSubtitleReport.report.interaction.packetCacheWindowDurationTicks -gt 300000000) {
    throw 'Subtitle switch packet cache evidence must prove a bounded cache hit.'
}

foreach ($subtitleSwitchEvent in $subtitleSwitchEvents) {
    $cueCountMatch = [regex]::Match(
        $subtitleSwitchEvent.message,
        '^subtitle stream index \d+; cue overlay render count (\d+)->(\d+);')
    if (-not $cueCountMatch.Success -or
        [uint64]$cueCountMatch.Groups[2].Value -le [uint64]$cueCountMatch.Groups[1].Value) {
        throw 'Expected each completed subtitle-switch event to report a real cue-render count increase.'
    }
}

$pausedSwitchMatch = [regex]::Match(
    $subtitleSwitchEvents[0].message,
    '; selected subtitle stream index \d+; paused switch 1; paused position (\d+)->(\d+); resumed position (\d+)->(\d+)$')
if (-not $pausedSwitchMatch.Success -or
    [int64]$pausedSwitchMatch.Groups[2].Value -ne [int64]$pausedSwitchMatch.Groups[1].Value -or
    [int64]$pausedSwitchMatch.Groups[4].Value -le [int64]$pausedSwitchMatch.Groups[3].Value) {
    throw 'Expected the first subtitle switch to remain paused, then resume with advancing playback.'
}

$subtitleOffEvents = @($nativeSubtitleLifecycleEvents | Where-Object { $_.operation -eq 'subtitle-off' })
if ($subtitleOffEvents.Count -ne 0) {
    throw 'Subtitle-switch case must not mix subtitle-off behavior into the same attempt.'
}

$seekEvents = @($nativeAvLifecycleEvents | Where-Object { $_.operation -eq 'seek' })
if ($seekEvents.Count -ne 0) {
    throw 'Interaction-only native helper case unexpectedly emitted a seek lifecycle event.'
}

if ($nativeAvReport.report.tracks.selectedAudioStreamIndex -ne $nativeAvReport.report.tracks.audio[1].index -or
    $nativeSubtitleReport.report.tracks.selectedSubtitleStreamIndex -ne $nativeSubtitleReport.report.tracks.subtitles[0].index -or
    $nativeSubtitleReport.report.tracks.isSubtitleDisabled -ne $false) {
    throw 'Expected independent audio/subtitle reports to preserve their selected-track evidence.'
}

$subtitleFailures = @($nativeSubtitleReport.report.checks | Where-Object {
    $_.status -eq 'fail' -and $_.failureArea -eq 'subtitles'
})
$evidenceCollectionFailures = @($nativeAvReport.report.checks | Where-Object {
    $_.status -eq 'fail' -and $_.failureArea -eq 'evidence-collection'
})
if ($nativeAvReport.report.result -ne 'pass' -or
    $nativeSubtitleReport.report.result -ne 'pass' -or
    @($nativeAvReport.report.failureReasons).Count -ne 0 -or
    @($nativeSubtitleReport.report.failureReasons).Count -ne 0 -or
    $subtitleFailures.Count -ne 0 -or
    $evidenceCollectionFailures.Count -ne 0 -or
    -not [string]::IsNullOrWhiteSpace($nativeAvReport.report.error.code) -or
    -not [string]::IsNullOrWhiteSpace($nativeSubtitleReport.report.error.code)) {
    throw 'Expected independent A/V and subtitle cases to pass without evidence-collection failures.'
}

if ($nativeAvReport.report.buffers.submittedAudioFrames -le 0) {
    throw 'Expected native helper A/V report to include submitted audio frames.'
}

if ($nativeAvReport.report.timing.presentDurationMsP95 -le 0 -or
    $nativeAvReport.report.timing.presentDurationMsMax -le 0) {
    throw 'Expected native helper A/V report to include swapchain Present duration evidence.'
}

if ($nativeAvReport.report.timing.renderIntervalMsP05 -le 0 -or
    $nativeAvReport.report.timing.minFrameGapMs -le 0 -or
    $nativeAvReport.report.timing.renderIntervalUnderExpected2MsCount -lt 0 -or
    $nativeAvReport.report.timing.renderIntervalUnderExpected4MsCount -lt 0) {
    throw 'Expected native helper A/V report to include render short-interval compensation evidence.'
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

if ($nativeAvReport.report.timing.audioAheadWaitFinalDeltaAbsMsP95 -le 0 -or
    $nativeAvReport.report.timing.audioAheadWaitFinalDeltaAbsMsMax -le 0) {
    throw 'Expected native helper A/V report to include audio-ahead wait final delta evidence.'
}

if ($nativeAvReport.report.timing.audioAheadWaitEpisodeCount -le 0 -or
    $nativeAvReport.report.timing.audioAheadWaitPassesPerEpisodeP95 -le 0 -or
    $nativeAvReport.report.timing.audioAheadWaitPassesPerEpisodeMax -le 0) {
    throw 'Expected native helper A/V report to include audio-ahead wait episode/pass evidence.'
}

if ($nativeAvReport.report.timing.audioAheadWaitPassDurationMsP95 -le 0 -or
    $nativeAvReport.report.timing.audioAheadWaitPassTargetMsP95 -le 0 -or
    $nativeAvReport.report.timing.audioAheadWaitPassOversleepMsP95 -le 0) {
    throw 'Expected native helper A/V report to include audio-ahead wait pass duration/target/oversleep evidence.'
}

if ($nativeAvReport.report.timing.audioAheadWaitCount -le 0 -or
    $nativeAvReport.report.timing.videoClockWaitCount -lt 0 -or
    $nativeAvReport.report.timing.videoAheadWaitCount -lt $nativeAvReport.report.timing.audioAheadWaitCount) {
    throw 'Expected native helper A/V report to include split wait reason counters.'
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
        $matrixItem.Report.report.timing.audioAheadWaitCount -lt 0 -or
        $matrixItem.Report.report.timing.videoClockWaitCount -lt 0 -or
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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
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
      "caseId": "$nativeEndOfStreamCaseId",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeSampleUrl",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "end-of-stream" },
      "purpose": [
        "end-of-stream"
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
      "executionRequirement": {
        "minimumEvidenceLevel": "native-playback",
        "scenario": "audio-switch"
      },
      "purpose": [
        "tracks",
        "audio-switch",
        "av-sync",
        "buffering"
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
        "maxInteractionRecoveryDurationMs": 2000,
        "minRenderedVideoFrames": $nativeAvMinimumRenderedVideoFrames,
        "maxAudioVideoDriftMsP95": 80.0
      }
    },
    {
      "caseId": "$nativeSubtitleCaseId",
      "category": "challenge",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeAvSampleUrl",
      "executionRequirement": {
        "minimumEvidenceLevel": "native-playback",
        "scenario": "subtitle-switch"
      },
      "purpose": [
        "subtitles",
        "subtitle-switch"
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
        "maxInteractionRecoveryDurationMs": 2000,
        "minRenderedVideoFrames": 1
      }
    },
    {
      "caseId": "$nativeSdr23976CaseId",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "$nativeSdr23976SampleUrl",
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
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
      "executionRequirement": { "minimumEvidenceLevel": "native-playback", "scenario": "playback" },
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

$nativeManifest = Get-Content -LiteralPath $nativeManifestPath -Raw | ConvertFrom-Json
foreach ($nativeHdrCase in @($nativeManifest.cases | Where-Object { $_.expected.hdrKind -eq 'Hdr10' })) {
    Add-Member -InputObject $nativeHdrCase.expected -NotePropertyName 'hdrOutput' `
        -NotePropertyValue 'Hdr10' -Force
    Add-Member -InputObject $nativeHdrCase.expected -NotePropertyName 'dxgiInput' `
        -NotePropertyValue 'YCBCR_STUDIO_G2084_TOPLEFT_P2020' -Force
    Add-Member -InputObject $nativeHdrCase.expected -NotePropertyName 'dxgiOutput' `
        -NotePropertyValue 'RGB_FULL_G2084_NONE_P2020' -Force
    Add-Member -InputObject $nativeHdrCase.expected -NotePropertyName 'sdrDisplayFallback' `
        -NotePropertyValue ([ordered]@{
            hdrOutput = 'Sdr'
            dxgiInputAnyOf = @(
                'YCBCR_STUDIO_G22_LEFT_P2020',
                'YCBCR_STUDIO_G22_TOPLEFT_P2020'
            )
            dxgiOutput = 'RGB_FULL_G22_NONE_P709'
            requireValidatedConversion = $true
            requiredConversionStatus = 'tone-mapped-hable'
        }) -Force
}
$nativeManifest.cases = @($nativeManifest.cases) + @(
    $script:networkReconnectManifestCase,
    $script:longPauseNetworkManifestCase,
    $script:demuxReadRecoveryManifestCase,
    [pscustomobject]@{
        caseId = $nativeNonZeroTimelineCaseId
        category = 'stable'
        severity = 'high'
        stability = 'stable'
        uri = $nativeNonZeroTimelineSampleUrl
        executionRequirement = [pscustomobject]@{
            minimumEvidenceLevel = 'native-playback'
            scenario = 'timeline'
        }
        purpose = @('timeline', 'seek')
        expected = [pscustomobject]@{
            codec = 'h264'
            width = 320
            height = 180
            frameRate = 30.0
            videoRange = 'SDR'
            colorPrimaries = 'bt709'
            colorTransfer = 'bt709'
            colorSpace = 'bt709'
            hdrKind = 'Sdr'
            dxgiInput = 'YCBCR_STUDIO_G22_LEFT_P709'
            dxgiOutput = 'RGB_FULL_G22_NONE_P709'
            isDirectPlayable = $true
            minRenderedVideoFrames = 1
            maxSeekPositionErrorMs = 100.0
            maxSeekRecoveryDurationMs = 2000.0
        }
    },
    [pscustomobject]@{
        caseId = $nativeResumeSeekTimelineCaseId
        category = 'stable'
        severity = 'critical'
        stability = 'stable'
        uri = $nativeNonZeroTimelineSampleUrl
        startPositionTicks = 20000000
        executionRequirement = [pscustomobject]@{
            minimumEvidenceLevel = 'native-playback'
            scenario = 'timeline'
        }
        purpose = @('timeline', 'resume', 'seek')
        expected = [pscustomobject]@{
            codec = 'h264'
            width = 320
            height = 180
            frameRate = 30.0
            videoRange = 'SDR'
            colorPrimaries = 'bt709'
            colorTransfer = 'bt709'
            colorSpace = 'bt709'
            hdrKind = 'Sdr'
            dxgiInput = 'YCBCR_STUDIO_G22_LEFT_P709'
            dxgiOutput = 'RGB_FULL_G22_NONE_P709'
            isDirectPlayable = $true
            minRenderedVideoFrames = 1
            maxSeekPositionErrorMs = 100.0
            maxSeekRecoveryDurationMs = 2000.0
        }
    },
    [pscustomobject]@{
        caseId = $nativeMissingFileCaseId
        category = 'stable'
        severity = 'medium'
        stability = 'stable'
        uri = $nativeMissingFileUrl
        executionRequirement = [pscustomobject]@{
            minimumEvidenceLevel = 'native-playback'
            scenario = 'playback'
        }
        purpose = @('error-handling', 'source-open')
        expected = [pscustomobject]@{
            codec = 'h264'
            width = 320
            height = 180
            frameRate = 30.0
            videoRange = 'SDR'
            colorPrimaries = 'bt709'
            colorTransfer = 'bt709'
            colorSpace = 'bt709'
            hdrKind = 'Sdr'
            dxgiInput = 'YCBCR_STUDIO_G22_LEFT_P709'
            dxgiOutput = 'RGB_FULL_G22_NONE_P709'
            isDirectPlayable = $true
            requireValidatedConversion = $false
        }
    })
$nativeManifest | ConvertTo-Json -Depth 12 |
    Set-Content -LiteralPath $nativeManifestPath -Encoding UTF8

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
    validate-manifest `
    --manifest $nativeManifestPath `
    --output $nativeManifestValidationPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected formal native manifest to pass structural validation.'
}
$nativeManifestValidation = Get-Content -LiteralPath $nativeManifestValidationPath -Raw | ConvertFrom-Json
if ($nativeManifestValidation.coverage.missingPurposes -contains 'timeline' -or
    $nativeManifestValidation.coverage.missingPurposes -contains 'error-handling') {
    throw 'Formal native manifest must cover timeline and error-handling with real native cases.'
}
$nativeNetworkCapturedReportPath = Get-QualityReportPath `
    -Root $nativeCapturedDir `
    -CaseId $script:networkReconnectManifestCase.caseId
New-Item -ItemType Directory -Path (Split-Path -Parent $nativeNetworkCapturedReportPath) -Force |
    Out-Null
Copy-Item `
    -LiteralPath $script:networkReconnectCapturedReportPath `
    -Destination $nativeNetworkCapturedReportPath `
    -Force
$nativeLongPauseNetworkCapturedReportPath = Get-QualityReportPath `
    -Root $nativeCapturedDir `
    -CaseId $script:longPauseNetworkManifestCase.caseId
New-Item -ItemType Directory -Path (Split-Path -Parent $nativeLongPauseNetworkCapturedReportPath) -Force |
    Out-Null
Copy-Item `
    -LiteralPath $script:longPauseNetworkCapturedReportPath `
    -Destination $nativeLongPauseNetworkCapturedReportPath `
    -Force
$nativeDemuxReadRecoveryCapturedReportPath = Get-QualityReportPath `
    -Root $nativeCapturedDir `
    -CaseId $script:demuxReadRecoveryManifestCase.caseId
New-Item -ItemType Directory -Path (Split-Path -Parent $nativeDemuxReadRecoveryCapturedReportPath) -Force |
    Out-Null
Copy-Item `
    -LiteralPath $script:demuxReadRecoveryCapturedReportPath `
    -Destination $nativeDemuxReadRecoveryCapturedReportPath `
    -Force

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
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

if (-not (Test-Path $nativeEndOfStreamMaterializedReportPath)) {
    throw "Expected materialized native end-of-stream report at $nativeEndOfStreamMaterializedReportPath."
}

$nativeEndOfStreamMaterializedReport = Get-Content `
    -LiteralPath $nativeEndOfStreamMaterializedReportPath `
    -Raw | ConvertFrom-Json
if (-not ($nativeEndOfStreamMaterializedReport.modelAnalysis.evidenceSignals -contains 'lifecycle.endOfStream')) {
    throw 'Expected materialized native end-of-stream report to expose lifecycle.endOfStream evidence.'
}

if (-not (Test-Path $nativeAvMaterializedReportPath)) {
    throw "Expected materialized native helper A/V report at $nativeAvMaterializedReportPath."
}

if (-not (Test-Path $nativeSubtitleMaterializedReportPath)) {
    throw "Expected materialized native helper subtitle report at $nativeSubtitleMaterializedReportPath."
}

if (-not (Test-Path $nativeNetworkMaterializedReportPath)) {
    throw "Expected materialized native network reconnect report at $nativeNetworkMaterializedReportPath."
}

if (-not (Test-Path $nativeLongPauseNetworkMaterializedReportPath)) {
    throw "Expected materialized native long pause network report at $nativeLongPauseNetworkMaterializedReportPath."
}

if (-not (Test-Path $nativeDemuxReadRecoveryMaterializedReportPath)) {
    throw "Expected materialized native demux read recovery report at $nativeDemuxReadRecoveryMaterializedReportPath."
}

foreach ($requiredReportPath in @(
    $nativeNonZeroTimelineMaterializedReportPath,
    $nativeResumeSeekTimelineMaterializedReportPath,
    $nativeMissingFileMaterializedReportPath)) {
    if (-not (Test-Path -LiteralPath $requiredReportPath)) {
        throw "Expected materialized native timeline/error report at $requiredReportPath."
    }
}

$nativeMissingFileMaterializedReport = Get-Content -LiteralPath $nativeMissingFileMaterializedReportPath -Raw | ConvertFrom-Json
if ($nativeMissingFileMaterializedReport.report.result -ne 'error' -or
    $nativeMissingFileMaterializedReport.report.error.code -ne 'source.open.missing-file' -or
    $nativeMissingFileMaterializedReport.report.error.failureClass -ne 'sample issue' -or
    $nativeMissingFileMaterializedReport.report.error.failureArea -ne 'error-handling' -or
    $nativeMissingFileMaterializedReport.report.execution.sourceOpenAttempted -ne $true -or
    $nativeMissingFileMaterializedReport.report.execution.sourceOpened -ne $false) {
    throw 'Materialized missing-file report did not preserve the real native open-boundary failure.'
}

foreach ($timelineReportPath in @(
    $nativeNonZeroTimelineMaterializedReportPath,
    $nativeResumeSeekTimelineMaterializedReportPath)) {
    $timelineReport = Get-Content -LiteralPath $timelineReportPath -Raw | ConvertFrom-Json
    if ($timelineReport.report.result -ne 'pass' -or
        $timelineReport.report.execution.evidenceLevel -ne 'native-playback' -or
        $timelineReport.report.position.seekPositionErrorMs -gt 100.0 -or
        $timelineReport.report.position.seekRecoveryDurationMs -gt 2000.0 -or
        $timelineReport.report.position.postSeekAdvanced -ne $true) {
        throw "Materialized timeline report did not preserve completed seek evidence at $timelineReportPath."
    }
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
$nativeSubtitleMaterializedReport = Get-Content -LiteralPath $nativeSubtitleMaterializedReportPath -Raw | ConvertFrom-Json
$nativeNetworkMaterializedReport = Get-Content -LiteralPath $nativeNetworkMaterializedReportPath -Raw | ConvertFrom-Json
$nativeLongPauseNetworkMaterializedReport = Get-Content -LiteralPath $nativeLongPauseNetworkMaterializedReportPath -Raw | ConvertFrom-Json
$nativeDemuxReadRecoveryMaterializedReport = Get-Content -LiteralPath $nativeDemuxReadRecoveryMaterializedReportPath -Raw | ConvertFrom-Json
if ($nativeNetworkMaterializedReport.report.execution.openedSourceHashKind -ne 'observed-media-signature-v2' -or
    $nativeNetworkMaterializedReport.report.execution.openedSourceHash -eq
        $nativeNetworkMaterializedReport.report.execution.sourceLocatorHash -or
    $nativeNetworkMaterializedReport.report.execution.status -ne 'completed' -or
    $nativeNetworkMaterializedReport.report.timing.renderedVideoFrames -le 0) {
    throw 'Expected unified network reconnect report to preserve observed media identity and completed native playback evidence.'
}
if ($nativeLongPauseNetworkMaterializedReport.caseMetadata.category -ne 'challenge' -or
    $nativeLongPauseNetworkMaterializedReport.report.execution.status -ne 'completed' -or
    $nativeLongPauseNetworkMaterializedReport.report.timing.renderedVideoFrames -le 0) {
    throw 'Expected unified long pause network report to preserve challenge classification and completed native playback evidence.'
}
if ($nativeDemuxReadRecoveryMaterializedReport.caseMetadata.category -ne 'challenge' -or
    $nativeDemuxReadRecoveryMaterializedReport.report.result -ne 'pass' -or
    $nativeDemuxReadRecoveryMaterializedReport.report.execution.status -ne 'completed' -or
    $nativeDemuxReadRecoveryMaterializedReport.report.readRecovery.readErrorCount -lt 1 -or
    $nativeDemuxReadRecoveryMaterializedReport.report.readRecovery.readRetryCount -lt 1 -or
    $nativeDemuxReadRecoveryMaterializedReport.report.readRecovery.readRecoveryCount -lt 1 -or
    $nativeDemuxReadRecoveryMaterializedReport.report.readRecovery.fatalReadErrorCode -ne 0) {
    throw 'Expected unified demux read recovery challenge to preserve real bounded recovery evidence.'
}
$longPauseEvent = @($nativeLongPauseNetworkMaterializedReport.report.lifecycle.events |
    Where-Object operation -eq 'pause') | Select-Object -First 1
$longResumeEvent = @($nativeLongPauseNetworkMaterializedReport.report.lifecycle.events |
    Where-Object operation -eq 'resume') | Select-Object -First 1
if ($longPauseEvent.message -notmatch 'duration 30 seconds' -or
    $longResumeEvent.status -ne 'completed' -or
    $longResumeEvent.message -notmatch 'decoded [0-9]+->[0-9]+' -or
    $longResumeEvent.message -notmatch 'rendered [0-9]+->[0-9]+' -or
    $longResumeEvent.message -notmatch 'actual pause 3[0-9]{4}\.[0-9]{3} ms' -or
    $longResumeEvent.message -notmatch 'recovery [0-9]+\.[0-9]{3} ms') {
    throw 'Expected long pause lifecycle evidence to preserve requested duration, frame deltas, and recovery latency.'
}
if ($nativeAvMaterializedReport.report.tracks.audioTrackCount -ne 2 -or
    $nativeAvMaterializedReport.modelAnalysis.avSync.status -ne 'synced') {
    throw 'Expected materialized native helper A/V report to preserve audio-track and A/V sync evidence.'
}

$nativeAvMaterializedSubtitleFailures = @($nativeSubtitleMaterializedReport.report.checks | Where-Object {
    $_.status -eq 'fail' -and $_.failureArea -eq 'subtitles'
})
$nativeAvMaterializedEvidenceCollectionFailures = @($nativeAvMaterializedReport.report.checks | Where-Object {
    $_.status -eq 'fail' -and $_.failureArea -eq 'evidence-collection'
})
if ($nativeAvMaterializedReport.report.result -ne 'pass' -or
    $nativeSubtitleMaterializedReport.report.result -ne 'pass' -or
    $nativeAvMaterializedReport.modelAnalysis.result -ne 'pass' -or
    $nativeSubtitleMaterializedReport.modelAnalysis.result -ne 'pass' -or
    @($nativeAvMaterializedReport.report.failureReasons).Count -ne 0 -or
    @($nativeSubtitleMaterializedReport.report.failureReasons).Count -ne 0 -or
    $nativeAvMaterializedSubtitleFailures.Count -ne 0 -or
    $nativeAvMaterializedEvidenceCollectionFailures.Count -ne 0 -or
    -not [string]::IsNullOrWhiteSpace($nativeAvMaterializedReport.report.error.code) -or
    -not [string]::IsNullOrWhiteSpace($nativeSubtitleMaterializedReport.report.error.code)) {
    throw 'Expected independent materialized A/V and subtitle reports to remain successful.'
}

if ($nativeAvMaterializedReport.report.position.seekPositionErrorMs -gt 250.0) {
    throw 'Expected materialized native helper A/V report to capture immediate seek position evidence.'
}

if ($nativeAvMaterializedReport.report.expected.minRenderedVideoFrames -lt $nativeAvMinimumRenderedVideoFrames) {
    throw "Expected materialized native helper A/V manifest to require at least $nativeAvMinimumRenderedVideoFrames rendered frames."
}

if (-not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.videoAheadWaitCount') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitCount') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.videoClockWaitCount')) {
    throw 'Expected materialized native helper A/V report to expose split wait reason signals, including zero-valued video-clock wait evidence.'
}

if (-not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.renderIntervalMsP05') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.minFrameGapMs') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.renderIntervalUnderExpected2MsCount') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.renderIntervalUnderExpected4MsCount')) {
    throw 'Expected materialized native helper A/V report to expose short-interval compensation evidence signals.'
}

if (-not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitEpisodeCount') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitPassesPerEpisodeP95') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitPassesPerEpisodeMax')) {
    throw 'Expected materialized native helper A/V report to expose audio-ahead episode/pass evidence signals.'
}

if (-not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitPassDurationMsP95') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitPassTargetMsP95') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitPassOversleepMsP95')) {
    throw 'Expected materialized native helper A/V report to expose audio-ahead pass duration/target/oversleep evidence signals.'
}

if (-not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.renderIntervalAfterAudioAheadWaitSampleCount') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.renderIntervalAfterAudioAheadWaitMsP95') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.renderIntervalAfterNonAudioWaitSampleCount')) {
    throw 'Expected materialized native helper A/V report to expose render-interval buckets grouped by the preceding wait reason.'
}

if (-not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitEndToPresentSampleCount') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitEndToPresentMsP50') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitEndToPresentMsP95') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitEndToPresentMsP99') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.audioAheadWaitEndToPresentMsMax') -or
    $nativeAvMaterializedReport.report.timing.audioAheadWaitEndToPresentSampleCount -le 0 -or
    $nativeAvMaterializedReport.report.timing.audioAheadWaitEndToPresentMsP95 -le 0) {
    throw 'Expected materialized native helper A/V report to expose non-zero audio-ahead wait end-to-present evidence.'
}

if (-not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.hardwareDecodedVideoFrames') -or
    -not ($nativeAvMaterializedReport.modelAnalysis.evidenceSignals -contains 'timing.softwareDecodedVideoFrames')) {
    throw 'Expected materialized native helper A/V report to expose hardware/software decode-mode evidence.'
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

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
    validate-report-set `
    --manifest $nativeManifestPath `
    --reports-dir $nativeMaterializedDir `
    --output $nativeValidationPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected materialized native helper report-set to pass validation.'
}

dotnet run --project (Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj') -- `
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

if ($nativeAnalysis.totalReportCount -ne 17) {
    throw 'Expected native helper report-set to include 17 reports: cadence/color matrix, A/V, subtitles, end-of-stream, network recovery, two timeline cases, and real missing-file handling.'
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
