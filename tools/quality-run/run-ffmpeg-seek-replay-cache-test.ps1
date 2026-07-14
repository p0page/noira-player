$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outputDirectory = 'C:\tmp\noiraplayer-seek-replay-cache'
$outputExecutable = Join-Path $outputDirectory 'FfmpegSeekReplayCacheTests.exe'

function Resolve-VcVars64Path {
    foreach ($candidate in @(
        $env:NOIRAPLAYER_VCVARS64_PATH,
        'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat')) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw 'Could not locate vcvars64.bat required to compile FfmpegSeekReplayCacheTests.'
}

function Resolve-NativeFfmpegPath {
    [xml]$packagesConfig = Get-Content -Raw -LiteralPath (
        Join-Path $repoRoot 'src\NoiraPlayer.Native\packages.config')
    $package = @($packagesConfig.packages.package) |
        Where-Object id -eq 'FFmpegInteropX.UWP.FFmpeg' |
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
    foreach ($candidate in @(
        (Join-Path $repoRoot "src\NoiraPlayer.Native\packages\FFmpegInteropX.UWP.FFmpeg.$version"),
        (Join-Path $globalPackagesRoot "ffmpeginteropx.uwp.ffmpeg\$version"))) {
        $nativePath = Join-Path $candidate 'runtimes\win-x64\native'
        if (Test-Path -LiteralPath (Join-Path $nativePath 'avcodec.lib')) {
            return [pscustomobject]@{
                IncludePath = Join-Path $candidate 'include'
                NativePath = $nativePath
            }
        }
    }

    throw "Could not locate restored FFmpegInteropX.UWP.FFmpeg $version package."
}

function Resolve-AppRuntimePath {
    $candidates = [System.Collections.Generic.List[string]]::new()
    foreach ($candidate in @(
        $env:NOIRAPLAYER_VCRUNTIME140_APP_PATH,
        'C:\Program Files (x86)\Microsoft SDKs\UWPNuGetPackages\microsoft.net.native.compiler\1.7.6\tools\x64\ilc\lib\MSCRT\vcruntime140_app.dll',
        'C:\Program Files\PowerToys\KeyboardManagerEditor\vcruntime140_app.dll')) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $candidates.Add($candidate)
        }
    }

    foreach ($root in @(
        (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages\microsoft.web.webview2'),
        'C:\Program Files\Microsoft OneDrive',
        'C:\Windows\SystemApps')) {
        if (Test-Path -LiteralPath $root) {
            Get-ChildItem -Path $root -Recurse -Filter 'vcruntime140_app.dll' -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty FullName |
                ForEach-Object { $candidates.Add($_) }
        }
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw 'Could not locate vcruntime140_app.dll required by the FFmpegInteropX UWP native DLLs.'
}

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$vcvars = Resolve-VcVars64Path
$ffmpeg = Resolve-NativeFfmpegPath
$compileCommand = ('call "{0}" >nul && cl /nologo /std:c++20 /EHsc /utf-8 /I src\NoiraPlayer.Native /I "{1}" /Fo:{2}\ tests\NoiraPlayer.Native.Tests\FfmpegSeekReplayCacheTests.cpp src\NoiraPlayer.Native\Media\FfmpegSeekReplayCache.cpp /Fe:"{3}" /link /LIBPATH:"{4}" avcodec.lib avutil.lib' -f
    $vcvars,
    $ffmpeg.IncludePath,
    $outputDirectory,
    $outputExecutable,
    $ffmpeg.NativePath)

Push-Location $repoRoot
try {
    & cmd.exe /d /c $compileCommand
    if ($LASTEXITCODE -ne 0) {
        throw "FfmpegSeekReplayCacheTests compilation failed with exit code $LASTEXITCODE."
    }

    Copy-Item -Force -LiteralPath (Resolve-AppRuntimePath) -Destination (
        Join-Path $outputDirectory 'vcruntime140_app.dll')
    $env:PATH = $ffmpeg.NativePath + ';' + $outputDirectory + ';' + $env:PATH
    & $outputExecutable
    if ($LASTEXITCODE -ne 0) {
        throw "FfmpegSeekReplayCacheTests execution failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Output 'FfmpegSeekReplayCacheTests passed.'
