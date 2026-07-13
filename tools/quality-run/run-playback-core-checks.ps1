param(
    [switch]$PlanOnly,
    [switch]$SkipNativeBuild,
    [string]$AppDiffBase = 'origin/main'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$modernToolchainScriptPath = Join-Path $repoRoot 'tools\NoiraModernToolchain.ps1'
$nativeRestoreScriptPath = Join-Path $PSScriptRoot 'Restore-NativePackages.ps1'
$nativeHttpMediaInputScriptPath = Join-Path $PSScriptRoot 'run-http-media-input-test.ps1'
$protectedAppRoots = @('src/NoiraPlayer.App')
$allowedAppInstrumentationPaths = @(
    'src/NoiraPlayer.App/Playback/WinRtNativePlaybackEngine.cs',
    'src/NoiraPlayer.App/Navigation/PlaybackLaunchRequest.cs',
    'src/NoiraPlayer.App/MainPage.xaml.cs',
    'src/NoiraPlayer.App/Views/PlaybackPage.xaml.cs'
)
$coreTestFilter = 'FullyQualifiedName~PlaybackQuality|FullyQualifiedName~Playback|FullyQualifiedName~EmbyProgress'

function Invoke-GitLines(
    [string[]]$Arguments
) {
    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ('git ' + ($Arguments -join ' ') + ' failed with exit code ' + $LASTEXITCODE)
    }

    @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Test-GitCommitExists(
    [string]$Ref
) {
    & git rev-parse --verify ($Ref + '^{commit}') > $null 2>&1
    $LASTEXITCODE -eq 0
}

function Assert-NoProtectedRootChanges(
    [string]$BaseRef,
    [string[]]$ProtectedRoots,
    [string[]]$AllowedPaths
) {
    Push-Location $repoRoot
    try {
        if (-not (Test-GitCommitExists $BaseRef)) {
            throw "App diff guard base '$BaseRef' was not found. Pass -AppDiffBase with a local base commit or branch."
        }

        $changedPaths = @()
        foreach ($root in $ProtectedRoots) {
            $changedPaths += Invoke-GitLines @('diff', '--name-only', '--', $root)
            $changedPaths += Invoke-GitLines @('diff', '--cached', '--name-only', '--', $root)
            $changedPaths += Invoke-GitLines @('diff', '--name-only', ($BaseRef + '...HEAD'), '--', $root)
        }

        $normalizedAllowedPaths = @($AllowedPaths | ForEach-Object { $_ -replace '\\', '/' })
        $uniquePaths = @($changedPaths |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { $_ -replace '\\', '/' } |
            Sort-Object -Unique)
        $blockedPaths = @($uniquePaths | Where-Object { $normalizedAllowedPaths -notcontains $_ })
        if ($blockedPaths.Count -gt 0) {
            throw ("Playback-core validation is App-free, but disallowed App changes were detected:`n" + ($blockedPaths -join "`n"))
        }
    }
    finally {
        Pop-Location
    }
}

function New-CommandPlan(
    [string]$Name,
    [string]$Description,
    [string]$Command,
    [string[]]$Arguments
) {
    [pscustomobject]@{
        name = $Name
        description = $Description
        command = $Command
        arguments = $Arguments
    }
}

$vcvars = 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat'
$nativeHelperCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\PlaybackQualityMetricsTests.obj tests\NoiraPlayer.Native.Tests\PlaybackQualityMetricsTests.cpp /Fe:C:\tmp\PlaybackQualityMetricsTests.exe && C:\tmp\PlaybackQualityMetricsTests.exe'
$nativeFramePacingCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\FramePacingTests.obj tests\NoiraPlayer.Native.Tests\FramePacingTests.cpp /Fe:C:\tmp\FramePacingTests.exe && C:\tmp\FramePacingTests.exe'
$nativeRenderLoopWaiterCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\RenderLoopWaiterTests.obj tests\NoiraPlayer.Native.Tests\RenderLoopWaiterTests.cpp /Fe:C:\tmp\RenderLoopWaiterTests.exe && C:\tmp\RenderLoopWaiterTests.exe'
$nativeSeekPresentationCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\SeekPresentationTrackerTests.obj tests\NoiraPlayer.Native.Tests\SeekPresentationTrackerTests.cpp /Fe:C:\tmp\SeekPresentationTrackerTests.exe && C:\tmp\SeekPresentationTrackerTests.exe'
$nativeMediaTimelineCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\MediaTimelineTests.obj tests\NoiraPlayer.Native.Tests\MediaTimelineTests.cpp /Fe:C:\tmp\MediaTimelineTests.exe && C:\tmp\MediaTimelineTests.exe'
$nativeAudioFramePrerollCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\AudioFramePrerollTests.obj tests\NoiraPlayer.Native.Tests\AudioFramePrerollTests.cpp /Fe:C:\tmp\AudioFramePrerollTests.exe && C:\tmp\AudioFramePrerollTests.exe'
$nativeSubtitleSwitchTransactionCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\SubtitleSwitchTransactionTests.obj tests\NoiraPlayer.Native.Tests\SubtitleSwitchTransactionTests.cpp /Fe:C:\tmp\SubtitleSwitchTransactionTests.exe && C:\tmp\SubtitleSwitchTransactionTests.exe'
$nativeSubtitleBitmapCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\SubtitleBitmapTests.obj tests\NoiraPlayer.Native.Tests\SubtitleBitmapTests.cpp /Fe:C:\tmp\SubtitleBitmapTests.exe && C:\tmp\SubtitleBitmapTests.exe'
$nativeDisplayRefreshCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\DisplayRefreshRatePolicyTests.obj tests\NoiraPlayer.Native.Tests\DisplayRefreshRatePolicyTests.cpp /Fe:C:\tmp\DisplayRefreshRatePolicyTests.exe && C:\tmp\DisplayRefreshRatePolicyTests.exe'
$nativeDisplayRefreshSnapshotCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\HdrDisplayRefreshRateSnapshotTests.obj tests\NoiraPlayer.Native.Tests\HdrDisplayRefreshRateSnapshotTests.cpp /Fe:C:\tmp\HdrDisplayRefreshRateSnapshotTests.exe && C:\tmp\HdrDisplayRefreshRateSnapshotTests.exe'
$nativeAudioFrameTimelineCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\AudioFrameTimelineTests.obj tests\NoiraPlayer.Native.Tests\AudioFrameTimelineTests.cpp /Fe:C:\tmp\AudioFrameTimelineTests.exe && C:\tmp\AudioFrameTimelineTests.exe'
$nativeAudioBufferAccumulatorCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\AudioBufferAccumulatorTests.obj tests\NoiraPlayer.Native.Tests\AudioBufferAccumulatorTests.cpp /Fe:C:\tmp\AudioBufferAccumulatorTests.exe && C:\tmp\AudioBufferAccumulatorTests.exe'
$nativeDecoderEagainRecoveryCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\DecoderEagainRecoveryTests.obj tests\NoiraPlayer.Native.Tests\DecoderEagainRecoveryTests.cpp /Fe:C:\tmp\DecoderEagainRecoveryTests.exe && C:\tmp\DecoderEagainRecoveryTests.exe'
$nativeFfmpegReadRecoveryCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /Fo:C:\tmp\FfmpegReadRecoveryTests.obj tests\NoiraPlayer.Native.Tests\FfmpegReadRecoveryTests.cpp /Fe:C:\tmp\FfmpegReadRecoveryTests.exe && C:\tmp\FfmpegReadRecoveryTests.exe'
$nativeSeekReplayCacheCommand = '"' + $vcvars + '" >nul && if not exist C:\tmp\noiraplayer-seek-replay-cache mkdir C:\tmp\noiraplayer-seek-replay-cache && cl /nologo /std:c++20 /EHsc /I src\NoiraPlayer.Native /I src\NoiraPlayer.Native\packages\FFmpegInteropX.UWP.FFmpeg.8.1.2\include /Fo:C:\tmp\noiraplayer-seek-replay-cache\ tests\NoiraPlayer.Native.Tests\FfmpegSeekReplayCacheTests.cpp src\NoiraPlayer.Native\Media\FfmpegSeekReplayCache.cpp /Fe:C:\tmp\FfmpegSeekReplayCacheTests.exe /link /LIBPATH:src\NoiraPlayer.Native\packages\FFmpegInteropX.UWP.FFmpeg.8.1.2\runtimes\win-x64\native avcodec.lib avutil.lib && set PATH=%CD%\src\NoiraPlayer.Native\packages\FFmpegInteropX.UWP.FFmpeg.8.1.2\runtimes\win-x64\native;%PATH% && C:\tmp\FfmpegSeekReplayCacheTests.exe'
$nativeDxOffscreenCommand = '"' + $vcvars + '" >nul && if not exist C:\tmp\noiraplayer-native-dx-offscreen mkdir C:\tmp\noiraplayer-native-dx-offscreen && cl /nologo /std:c++20 /EHsc /DWIN32_LEAN_AND_MEAN /DWINRT_LEAN_AND_MEAN /I src\NoiraPlayer.Native /I src\NoiraPlayer.Native\packages\FFmpegInteropX.UWP.FFmpeg.8.1.2\include /Fo:C:\tmp\noiraplayer-native-dx-offscreen\ tests\NoiraPlayer.Native.Tests\DxDeviceResourcesOffscreenTests.cpp src\NoiraPlayer.Native\DxDeviceResources.cpp src\NoiraPlayer.Native\Media\DxgiColorSpaceMapper.cpp src\NoiraPlayer.Native\Media\HdrToneMappingPass.cpp src\NoiraPlayer.Native\NativePlaybackDiagnostics.cpp /Fe:C:\tmp\DxDeviceResourcesOffscreenTests.exe d3d11.lib dxgi.lib d2d1.lib dwrite.lib d3dcompiler.lib windowsapp.lib && C:\tmp\DxDeviceResourcesOffscreenTests.exe'
$nativeBuildCommand = "& { . '$modernToolchainScriptPath'; `$msbuild = Resolve-ModernMsBuildPath ''; & `$msbuild 'src\NoiraPlayer.Native\NoiraPlayer.Native.vcxproj' '/p:Configuration=Debug' '/p:Platform=x64' '/m' '/v:minimal'; exit `$LASTEXITCODE }"

$commands = @(
    New-CommandPlan `
        -Name 'script-plan-test' `
        -Description 'Validate that the playback-core check plan stays App-free before running builds or tests.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\run-playback-core-checks.tests.ps1')
    New-CommandPlan `
        -Name 'playback-core-tests' `
        -Description 'Run playback-related Core tests without building the UWP App package or unrelated interaction policies.' `
        -Command 'dotnet' `
        -Arguments @('test', 'tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj', '--filter', $coreTestFilter, '-v', 'minimal')
    New-CommandPlan `
        -Name 'playback-quality-cli-build' `
        -Description 'Build the App-free playback quality comparison CLI used by model optimization loops.' `
        -Command 'dotnet' `
        -Arguments @('build', 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj', '-v', 'minimal')
    New-CommandPlan `
        -Name 'playback-quality-runner-build' `
        -Description 'Build the secret-safe private Emby source resolver used by the native manifest runner.' `
        -Command 'dotnet' `
        -Arguments @('build', 'tools\NoiraPlayer.PlaybackQuality.Runner\NoiraPlayer.PlaybackQuality.Runner.csproj', '-v', 'minimal')
    New-CommandPlan `
        -Name 'playback-quality-cli-smoke-test' `
        -Description 'Run an App-free JSON baseline/candidate comparison smoke test through the playback quality CLI.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\run-playback-quality-cli-smoke-test.ps1')
    New-CommandPlan `
        -Name 'playback-quality-manifest-runner-test' `
        -Description 'Verify each selected stable/challenge manifest case invokes the native harness independently and preserves per-case reports after failures.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\Invoke-PlaybackQualityManifest.tests.ps1')
    New-CommandPlan `
        -Name 'native-restore' `
        -Description 'Reuse complete global NuGet packages, then restore missing native packages.config dependencies.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nativeRestoreScriptPath)
    New-CommandPlan `
        -Name 'native-headless-harness-smoke-test' `
        -Description 'Run the App-free native-headless skip path and real native helper captured report smoke test.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\run-native-headless-harness-smoke-test.ps1')
    New-CommandPlan `
        -Name 'private-emby-reference-manifest-test' `
        -Description 'Verify secret-safe private Emby reference manifest generation from offline media-source metadata.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\New-PrivateEmbyReferenceManifest.tests.ps1')
    New-CommandPlan `
        -Name 'public-reference-media-probe-test' `
        -Description 'Verify public reference media probing reports URL reachability and expected source metadata without private Emby data.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\Test-PublicReferenceMedia.tests.ps1')
    New-CommandPlan `
        -Name 'merge-reference-manifests-test' `
        -Description 'Verify small public/private reference manifests can be merged into one local corpus without duplicate case IDs.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\Merge-ReferenceManifests.tests.ps1')
    New-CommandPlan `
        -Name 'playback-core-tuning-baseline-test' `
        -Description 'Verify the reproducible playback Core tuning baseline command can build a valid public-only report-set without private data or native-headless.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\New-PlaybackCoreTuningBaseline.tests.ps1')
    New-CommandPlan `
        -Name 'playback-core-tuning-candidate-comparison-test' `
        -Description 'Verify playback Core tuning candidates are compared against the same manifest with reproducible baseline/candidate evidence.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\Compare-PlaybackCoreTuningCandidate.tests.ps1')
    New-CommandPlan `
        -Name 'playback-cadence-stability-test' `
        -Description 'Verify repeated cadence report samples can be summarized for flake attribution without changing comparison thresholds.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\Measure-PlaybackCadenceStability.tests.ps1')
    New-CommandPlan `
        -Name 'export-app-quality-run-reports-test' `
        -Description 'Verify App-hosted quality-run captured reports can be exported from LocalState while preserving report-set relative paths.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\Export-AppQualityRunReports.tests.ps1')
    New-CommandPlan `
        -Name 'write-app-quality-run-command-test' `
        -Description 'Verify plan-runs quality-run dev commands can be written to App LocalState for App-hosted capture.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\Write-AppQualityRunCommand.tests.ps1')
    New-CommandPlan `
        -Name 'native-helper-test' `
        -Description 'Compile and run the standalone native playback quality metrics helper test.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeHelperCommand)
    New-CommandPlan `
        -Name 'native-frame-pacing-test' `
        -Description 'Compile and run the standalone native frame pacing policy test.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeFramePacingCommand)
    New-CommandPlan `
        -Name 'native-render-loop-waiter-test' `
        -Description 'Compile and run the standalone native render loop wait helper test.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeRenderLoopWaiterCommand)
    New-CommandPlan `
        -Name 'native-seek-presentation-test' `
        -Description 'Compile and run the standalone seek presented-frame generation evidence test.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeSeekPresentationCommand)
    New-CommandPlan `
        -Name 'native-media-timeline-test' `
        -Description 'Verify demux timestamps are normalized to a zero-based public timeline and seek targets restore the demux origin.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeMediaTimelineCommand)
    New-CommandPlan `
        -Name 'native-audio-frame-preroll-test' `
        -Description 'Verify audio frames before a start or seek target are dropped without publishing an earlier master-clock position.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeAudioFramePrerollCommand)
    New-CommandPlan `
        -Name 'native-subtitle-switch-transaction-test' `
        -Description 'Compile and run fault-injected transactional subtitle switch recovery tests.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeSubtitleSwitchTransactionCommand)
    New-CommandPlan `
        -Name 'native-subtitle-bitmap-test' `
        -Description 'Verify indexed FFmpeg bitmap subtitle palettes and source-canvas regions convert into premultiplied BGRA overlays.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeSubtitleBitmapCommand)
    New-CommandPlan `
        -Name 'native-display-refresh-test' `
        -Description 'Compile and run the standalone native display refresh cadence policy test.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeDisplayRefreshCommand)
    New-CommandPlan `
        -Name 'native-display-refresh-snapshot-test' `
        -Description 'Compile and run the standalone native display refresh snapshot normalization test.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeDisplayRefreshSnapshotCommand)
    New-CommandPlan `
        -Name 'native-audio-frame-timeline-test' `
        -Description 'Verify missing audio timestamps continue from the seek anchor and advance by rendered samples.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeAudioFrameTimelineCommand)
    New-CommandPlan `
        -Name 'native-audio-buffer-accumulator-test' `
        -Description 'Verify small decoded audio frames are coalesced into stable XAudio buffers without losing the timeline anchor.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeAudioBufferAccumulatorCommand)
    New-CommandPlan `
        -Name 'native-decoder-eagain-recovery-test' `
        -Description 'Verify simultaneous decoder EAGAIN recovery is bounded and resets only after progress.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeDecoderEagainRecoveryCommand)
    New-CommandPlan `
        -Name 'native-ffmpeg-read-recovery-test' `
        -Description 'Verify demux read recovery distinguishes EOF, interruption, transient errors, bounded HTTP retries, and packet progress.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeFfmpegReadRecoveryCommand)
    New-CommandPlan `
        -Name 'native-http-media-input-test' `
        -Description 'Verify the opt-in FFmpeg custom AVIO forwarding path preserves read, seek, size, ownership, and callback evidence.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nativeHttpMediaInputScriptPath)
    New-CommandPlan `
        -Name 'native-seek-replay-cache-test' `
        -Description 'Verify bounded active-stream packet replay, keyframe coverage, pruning, order, and packet ownership.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeSeekReplayCacheCommand)
    New-CommandPlan `
        -Name 'native-dx-offscreen-test' `
        -Description 'Compile and run the native DirectX offscreen composition swapchain test.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeDxOffscreenCommand)
)

if (-not $SkipNativeBuild) {
    $commands += New-CommandPlan `
        -Name 'native-build' `
        -Description 'Build the native playback component only; this does not build or package the UWP App.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $nativeBuildCommand)
}

$summary = [pscustomobject]@{
    scope = 'playback-core'
    includedRoots = @(
        'src/NoiraPlayer.Core',
        'src/NoiraPlayer.Native',
        'tests/NoiraPlayer.Core.Tests',
        'tests/NoiraPlayer.Native.Tests',
        'tools/NoiraPlayer.PlaybackQuality.Cli',
        'tools/NoiraPlayer.PlaybackQuality.Headless',
        'tools/NoiraPlayer.PlaybackQuality.Runner',
        'tools/quality-run'
    )
    excludedRoots = @('src/NoiraPlayer.App')
    excludes = @('UWP App project files', 'AppPackages', 'MSIX packaging')
    coreTestFilter = $coreTestFilter
    appDiffGuard = [pscustomobject]@{
        status = 'active'
        baseRef = $AppDiffBase
        protectedRoots = $protectedAppRoots
        allowedPaths = $allowedAppInstrumentationPaths
        checks = @('working-tree', 'index', 'branch-diff')
    }
    commands = $commands
}

if ($PlanOnly) {
    $summary | ConvertTo-Json -Depth 6
    exit 0
}

$results = @()
Push-Location $repoRoot
try {
    $guardStartedAt = [DateTimeOffset]::Now
    Write-Host 'running=app-diff-guard'
    Assert-NoProtectedRootChanges `
        -BaseRef $AppDiffBase `
        -ProtectedRoots $protectedAppRoots `
        -AllowedPaths $allowedAppInstrumentationPaths
    $guardFinishedAt = [DateTimeOffset]::Now
    $results += [pscustomobject]@{
        name = 'app-diff-guard'
        exitCode = 0
        durationMs = [math]::Round(($guardFinishedAt - $guardStartedAt).TotalMilliseconds, 0)
    }

    foreach ($item in $commands) {
        $startedAt = [DateTimeOffset]::Now
        Write-Host ("running=" + $item.name)
        & $item.command @($item.arguments)
        $exitCode = $LASTEXITCODE
        $finishedAt = [DateTimeOffset]::Now
        $results += [pscustomobject]@{
            name = $item.name
            exitCode = $exitCode
            durationMs = [math]::Round(($finishedAt - $startedAt).TotalMilliseconds, 0)
        }

        if ($exitCode -ne 0) {
            break
        }
    }
}
finally {
    Pop-Location
}

$report = [pscustomobject]@{
    scope = 'playback-core'
    includedRoots = $summary.includedRoots
    excludedRoots = $summary.excludedRoots
    excludes = $summary.excludes
    coreTestFilter = $summary.coreTestFilter
    appDiffGuard = $summary.appDiffGuard
    result = if ($results | Where-Object { $_.exitCode -ne 0 }) { 'fail' } else { 'pass' }
    results = $results
}

$report | ConvertTo-Json -Depth 6
if ($report.result -ne 'pass') {
    exit 1
}
