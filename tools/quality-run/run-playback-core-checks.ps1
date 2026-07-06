param(
    [switch]$PlanOnly,
    [switch]$SkipNativeBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

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
$nativeDisplayRefreshCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NextGenEmby.Native /Fo:C:\tmp\DisplayRefreshRatePolicyTests.obj tests\NextGenEmby.Native.Tests\DisplayRefreshRatePolicyTests.cpp /Fe:C:\tmp\DisplayRefreshRatePolicyTests.exe && C:\tmp\DisplayRefreshRatePolicyTests.exe'
$nativeDisplayRefreshSnapshotCommand = '"' + $vcvars + '" >nul && cl /nologo /std:c++20 /EHsc /I src\NextGenEmby.Native /Fo:C:\tmp\HdrDisplayRefreshRateSnapshotTests.obj tests\NextGenEmby.Native.Tests\HdrDisplayRefreshRateSnapshotTests.cpp /Fe:C:\tmp\HdrDisplayRefreshRateSnapshotTests.exe && C:\tmp\HdrDisplayRefreshRateSnapshotTests.exe'
$nativeRestoreCommand = '"' + $vcvars + '" >nul && msbuild src\NextGenEmby.Native\NextGenEmby.Native.vcxproj /t:Restore /p:RestorePackagesConfig=true /p:Configuration=Debug /p:Platform=x64 /v:minimal'
$nativeBuildCommand = '"' + $vcvars + '" >nul && msbuild src\NextGenEmby.Native\NextGenEmby.Native.vcxproj /p:Configuration=Debug /p:Platform=x64 /m /v:minimal'

$commands = @(
    New-CommandPlan `
        -Name 'script-plan-test' `
        -Description 'Validate that the playback-core check plan stays App-free before running builds or tests.' `
        -Command 'powershell' `
        -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\quality-run\run-playback-core-checks.tests.ps1')
    New-CommandPlan `
        -Name 'core-tests' `
        -Description 'Run Core playback quality and playback policy tests without building the UWP App package.' `
        -Command 'dotnet' `
        -Arguments @('test', 'tests\NextGenEmby.Core.Tests\NextGenEmby.Core.Tests.csproj', '-v', 'minimal')
    New-CommandPlan `
        -Name 'native-helper-test' `
        -Description 'Compile and run the standalone native playback quality metrics helper test.' `
        -Command 'cmd' `
        -Arguments @('/c', $nativeHelperCommand)
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
        'tools/quality-run'
    )
    excludedRoots = @('src/NextGenEmby.App')
    excludes = @('NextGenEmby.App.csproj', 'AppPackages', 'MSIX packaging')
    commands = $commands
}

if ($PlanOnly) {
    $summary | ConvertTo-Json -Depth 6
    exit 0
}

$results = @()
Push-Location $repoRoot
try {
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
    result = if ($results | Where-Object { $_.exitCode -ne 0 }) { 'fail' } else { 'pass' }
    results = $results
}

$report | ConvertTo-Json -Depth 6
if ($report.result -ne 'pass') {
    exit 1
}
