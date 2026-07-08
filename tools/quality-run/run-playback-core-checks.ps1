param(
    [switch]$PlanOnly,
    [switch]$SkipNativeBuild,
    [string]$AppDiffBase = '94adec5'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$protectedAppRoots = @('src/NextGenEmby.App')
$allowedAppInstrumentationPaths = @(
    'src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs',
    'src/NextGenEmby.App/Navigation/PlaybackLaunchRequest.cs',
    'src/NextGenEmby.App/MainPage.xaml.cs',
    'src/NextGenEmby.App/Views/PlaybackPage.xaml.cs'
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
$nativeHelperCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NextGenEmby.Native /Fo:C:\tmp\PlaybackQualityMetricsTests.obj tests\NextGenEmby.Native.Tests\PlaybackQualityMetricsTests.cpp /Fe:C:\tmp\PlaybackQualityMetricsTests.exe && C:\tmp\PlaybackQualityMetricsTests.exe'
$nativeFramePacingCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NextGenEmby.Native /Fo:C:\tmp\FramePacingTests.obj tests\NextGenEmby.Native.Tests\FramePacingTests.cpp /Fe:C:\tmp\FramePacingTests.exe && C:\tmp\FramePacingTests.exe'
$nativeDisplayRefreshCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NextGenEmby.Native /Fo:C:\tmp\DisplayRefreshRatePolicyTests.obj tests\NextGenEmby.Native.Tests\DisplayRefreshRatePolicyTests.cpp /Fe:C:\tmp\DisplayRefreshRatePolicyTests.exe && C:\tmp\DisplayRefreshRatePolicyTests.exe'
$nativeDisplayRefreshSnapshotCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NextGenEmby.Native /Fo:C:\tmp\HdrDisplayRefreshRateSnapshotTests.obj tests\NextGenEmby.Native.Tests\HdrDisplayRefreshRateSnapshotTests.cpp /Fe:C:\tmp\HdrDisplayRefreshRateSnapshotTests.exe && C:\tmp\HdrDisplayRefreshRateSnapshotTests.exe'
$nativeDxOffscreenCommand = '"' + $vcvars + '" >nul && if not exist C:\tmp\nextgenemby-native-dx-offscreen mkdir C:\tmp\nextgenemby-native-dx-offscreen && cl /nologo /std:c++20 /EHsc /DWIN32_LEAN_AND_MEAN /DWINRT_LEAN_AND_MEAN /I src\NextGenEmby.Native /I src\NextGenEmby.Native\packages\FFmpegInteropX.FFmpegUWP.5.1.100\include /Fo:C:\tmp\nextgenemby-native-dx-offscreen\ tests\NextGenEmby.Native.Tests\DxDeviceResourcesOffscreenTests.cpp src\NextGenEmby.Native\DxDeviceResources.cpp src\NextGenEmby.Native\Media\DxgiColorSpaceMapper.cpp src\NextGenEmby.Native\Media\HdrToneMappingPass.cpp src\NextGenEmby.Native\NativePlaybackDiagnostics.cpp /Fe:C:\tmp\DxDeviceResourcesOffscreenTests.exe d3d11.lib dxgi.lib d2d1.lib dwrite.lib d3dcompiler.lib windowsapp.lib && C:\tmp\DxDeviceResourcesOffscreenTests.exe'
$nativeRestoreCommand = '"' + $vcvars + '" >nul && msbuild src\NextGenEmby.Native\NextGenEmby.Native.vcxproj /t:Restore /p:RestorePackagesConfig=true /p:Configuration=Debug /p:Platform=x64 /v:minimal'
$nativeBuildCommand = '"' + $vcvars + '" >nul && msbuild src\NextGenEmby.Native\NextGenEmby.Native.vcxproj /p:Configuration=Debug /p:Platform=x64 /m /v:minimal'

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
        -Arguments @('test', 'tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj', '--filter', $coreTestFilter, '-v', 'minimal')
    New-CommandPlan `
        -Name 'playback-quality-cli-build' `
        -Description 'Build the App-free playback quality comparison CLI used by model optimization loops.' `
        -Command 'dotnet' `
        -Arguments @('build', 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj', '-v', 'minimal')
    New-CommandPlan `
        -Name 'playback-quality-cli-smoke-test' `
        -Description 'Run an App-free JSON baseline/candidate comparison smoke test through the playback quality CLI.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\run-playback-quality-cli-smoke-test.ps1')
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
        -Name 'native-dx-offscreen-test' `
        -Description 'Compile and run the native DirectX offscreen composition swapchain test.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeDxOffscreenCommand)
    New-CommandPlan `
        -Name 'native-restore' `
        -Description 'Restore native packages.config dependencies required by the native playback project.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeRestoreCommand)
)

if (-not $SkipNativeBuild) {
    $commands += New-CommandPlan `
        -Name 'native-build' `
        -Description 'Build the native playback component only; this does not build or package the UWP App.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeBuildCommand)
}

$summary = [pscustomobject]@{
    scope = 'playback-core'
    includedRoots = @(
        'src/NextGenEmby.Core',
        'src/NextGenEmby.Native',
        'tests/NextGenEmby.Core.Tests',
        'tests/NextGenEmby.Native.Tests',
        'tools/NextGenEmby.PlaybackQuality.Cli',
        'tools/NextGenEmby.PlaybackQuality.Headless',
        'tools/quality-run'
    )
    excludedRoots = @('src/NextGenEmby.App')
    excludes = @('NextGenEmby.App.csproj', 'AppPackages', 'MSIX packaging')
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
