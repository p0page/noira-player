$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'run-playback-core-checks.ps1'
$planJson = & $scriptPath -PlanOnly
$plan = $planJson | ConvertFrom-Json
$nativeHarnessSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'run-native-headless-harness-smoke-test.ps1')

if ($nativeHarnessSource -match 'for \(\$attempt = 1; \$attempt -le 3; \$attempt\+\+\)') {
    throw 'Native headless playback failures must not be hidden by an unreported whole-case retry loop.'
}

if (-not $plan.commands -or $plan.commands.Count -lt 2) {
    throw 'Expected at least two playback-core validation commands.'
}

foreach ($requiredNativeRegression in @(
    'native-media-timeline-test',
    'native-audio-frame-preroll-test',
    'native-audio-frame-timeline-test',
    'native-audio-buffer-accumulator-test',
    'native-decoder-eagain-recovery-test')) {
    if (-not ($plan.commands | Where-Object name -eq $requiredNativeRegression)) {
        throw ('Expected playback-core plan to include ' + $requiredNativeRegression + '.')
    }
}

if ($plan.scope -ne 'playback-core') {
    throw 'Expected playback-core validation scope.'
}

if (-not ($plan.includedRoots -contains 'src/NoiraPlayer.Core')) {
    throw 'Expected src/NoiraPlayer.Core in playback-core included roots.'
}

if (-not ($plan.includedRoots -contains 'src/NoiraPlayer.Native')) {
    throw 'Expected src/NoiraPlayer.Native in playback-core included roots.'
}

if (-not ($plan.includedRoots -contains 'tools/NoiraPlayer.PlaybackQuality.Cli')) {
    throw 'Expected playback quality CLI in playback-core included roots.'
}

if (-not ($plan.includedRoots -contains 'tools/NoiraPlayer.PlaybackQuality.Headless')) {
    throw 'Expected playback quality headless harness in playback-core included roots.'
}

if (-not ($plan.includedRoots -contains 'tools/NoiraPlayer.PlaybackQuality.Runner')) {
    throw 'Expected private source resolver runner in playback-core included roots.'
}

if (-not ($plan.excludedRoots -contains 'src/NoiraPlayer.App')) {
    throw 'Expected src/NoiraPlayer.App in playback-core excluded roots.'
}

if (-not $plan.appDiffGuard -or $plan.appDiffGuard.status -ne 'active') {
    throw 'Expected active App diff guard in playback-core validation plan.'
}

if ($plan.appDiffGuard.baseRef -eq '94adec5') {
    throw 'App diff guard default base must not use the stale pre-Noira rename commit.'
}

if ($plan.appDiffGuard.baseRef -ne 'origin/main') {
    throw ('Expected App diff guard default baseRef to be origin/main, got: ' + $plan.appDiffGuard.baseRef)
}

if (-not ($plan.appDiffGuard.protectedRoots -contains 'src/NoiraPlayer.App')) {
    throw 'Expected App diff guard to protect src/NoiraPlayer.App.'
}

$expectedAllowedAppInstrumentationPaths = @(
    'src/NoiraPlayer.App/Playback/WinRtNativePlaybackEngine.cs',
    'src/NoiraPlayer.App/Navigation/PlaybackLaunchRequest.cs',
    'src/NoiraPlayer.App/MainPage.xaml.cs',
    'src/NoiraPlayer.App/Views/PlaybackPage.xaml.cs'
)

foreach ($expectedPath in $expectedAllowedAppInstrumentationPaths) {
    if (-not ($plan.appDiffGuard.allowedPaths -contains $expectedPath)) {
        throw ('Expected App diff guard to allow playback quality instrumentation path: ' + $expectedPath)
    }
}

foreach ($allowedPath in $plan.appDiffGuard.allowedPaths) {
    if ($expectedAllowedAppInstrumentationPaths -notcontains $allowedPath) {
        throw ('Unexpected App diff guard allowlist path: ' + $allowedPath)
    }
}

if ($plan.appDiffGuard.allowedPaths -contains 'src/NoiraPlayer.App/Views/PlaybackPage.xaml' -or
    $plan.appDiffGuard.allowedPaths -contains 'src/NoiraPlayer.App/NoiraPlayer.App.Modern.csproj' -or
    $plan.appDiffGuard.allowedPaths -contains 'src/NoiraPlayer.App/Package.appxmanifest') {
    throw 'App diff guard must not allow App XAML, project, package, or packaging changes.'
}

if (-not $plan.coreTestFilter) {
    throw 'Expected playback-specific Core test filter in playback-core validation plan.'
}

if ($plan.coreTestFilter -notmatch 'PlaybackQuality' -or $plan.coreTestFilter -notmatch 'Playback') {
    throw 'Expected playback-specific Core test filter to include Playback and PlaybackQuality tests.'
}

$serializedCommands = $plan.commands | ConvertTo-Json -Depth 6
if ($serializedCommands -match 'NoiraPlayer\.App|AppPackages|msix|NoiraPlayer\.App\.csproj') {
    throw 'Playback-core validation plan must not build or package the App project.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'playback-core-tests' })) {
    throw 'Expected playback-core-tests command in playback-core validation plan.'
}

$playbackCoreTests = $plan.commands | Where-Object { $_.name -eq 'playback-core-tests' } | Select-Object -First 1
if (-not ($playbackCoreTests.arguments -contains '--filter')) {
    throw 'Expected playback-core-tests command to use a dotnet test filter.'
}

if (-not ($playbackCoreTests.arguments -contains $plan.coreTestFilter)) {
    throw 'Expected playback-core-tests command to use the plan coreTestFilter value.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'script-plan-test' })) {
    throw 'Expected script-plan-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-helper-test' })) {
    throw 'Expected native-helper-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-frame-pacing-test' })) {
    throw 'Expected native-frame-pacing-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'playback-quality-cli-build' })) {
    throw 'Expected playback-quality-cli-build command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'playback-quality-cli-smoke-test' })) {
    throw 'Expected playback-quality-cli-smoke-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-headless-harness-smoke-test' })) {
    throw 'Expected native-headless-harness-smoke-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'playback-quality-manifest-runner-test' })) {
    throw 'Expected playback-quality-manifest-runner-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'playback-quality-runner-build' })) {
    throw 'Expected playback-quality-runner-build command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-seek-presentation-test' })) {
    throw 'Expected native-seek-presentation-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-subtitle-switch-transaction-test' })) {
    throw 'Expected native-subtitle-switch-transaction-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-subtitle-bitmap-test' })) {
    throw 'Expected native-subtitle-bitmap-test command in playback-core validation plan.'
}

$nativeHeadlessSmokeScriptPath = Join-Path $PSScriptRoot 'run-native-headless-harness-smoke-test.ps1'
$nativeHeadlessSmokeScript = Get-Content -Raw -LiteralPath $nativeHeadlessSmokeScriptPath
if ($nativeHeadlessSmokeScript -notmatch '\$nativeCadenceDurationSeconds\s*=\s*5') {
    throw 'Expected native-headless cadence samples to use an explicit 5-second duration to reduce short-sample percentile instability.'
}

if ($nativeHeadlessSmokeScript -notmatch '\$nativeAvDurationSeconds\s*=\s*3') {
    throw 'Expected native-headless A/V smoke to keep its explicit 3-second duration separate from cadence-only sample tuning.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'private-emby-reference-manifest-test' })) {
    throw 'Expected private-emby-reference-manifest-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'public-reference-media-probe-test' })) {
    throw 'Expected public-reference-media-probe-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'merge-reference-manifests-test' })) {
    throw 'Expected merge-reference-manifests-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'playback-core-tuning-baseline-test' })) {
    throw 'Expected playback-core-tuning-baseline-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'playback-core-tuning-candidate-comparison-test' })) {
    throw 'Expected playback-core-tuning-candidate-comparison-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'playback-cadence-stability-test' })) {
    throw 'Expected playback-cadence-stability-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'export-app-quality-run-reports-test' })) {
    throw 'Expected export-app-quality-run-reports-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'write-app-quality-run-command-test' })) {
    throw 'Expected write-app-quality-run-command-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-display-refresh-test' })) {
    throw 'Expected native-display-refresh-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-display-refresh-snapshot-test' })) {
    throw 'Expected native-display-refresh-snapshot-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-dx-offscreen-test' })) {
    throw 'Expected native-dx-offscreen-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-restore' })) {
    throw 'Expected native-restore command before native-build in playback-core validation plan.'
}

$nativeRestore = $plan.commands | Where-Object { $_.name -eq 'native-restore' } | Select-Object -First 1
$nativeBuild = $plan.commands | Where-Object { $_.name -eq 'native-build' } | Select-Object -First 1
$serializedNativeRestore = $nativeRestore | ConvertTo-Json -Depth 6
if ($serializedNativeRestore -notmatch 'Restore-NativePackages\.ps1') {
    throw 'Native restore must reuse complete global NuGet packages before attempting a network restore.'
}

foreach ($nativeProjectCommand in @($nativeRestore, $nativeBuild)) {
    if ($null -eq $nativeProjectCommand) {
        continue
    }

    $serializedNativeProjectCommand = $nativeProjectCommand | ConvertTo-Json -Depth 6
    if ($serializedNativeProjectCommand -match 'Visual Studio\\2022|vcvars64\.bat') {
        throw 'Native project restore/build must not rely on VS2022 vcvars after the VS2026/v145 toolchain cutover.'
    }

    if ($nativeProjectCommand.name -eq 'native-build' -and
        ($serializedNativeProjectCommand -notmatch 'NoiraModernToolchain\.ps1' -or
            $serializedNativeProjectCommand -notmatch 'Resolve-ModernMsBuildPath')) {
        throw 'Native project restore/build must resolve MSBuild through NoiraModernToolchain.ps1.'
    }
}

$nativeRestoreIndex = [array]::IndexOf($plan.commands.name, 'native-restore')
$nativeBuildIndex = [array]::IndexOf($plan.commands.name, 'native-build')
if ($nativeBuildIndex -ge 0 -and $nativeRestoreIndex -gt $nativeBuildIndex) {
    throw 'Expected native-restore to run before native-build.'
}

$nativeHeadlessSmokeIndex = [array]::IndexOf($plan.commands.name, 'native-headless-harness-smoke-test')
if ($nativeHeadlessSmokeIndex -ge 0 -and $nativeRestoreIndex -gt $nativeHeadlessSmokeIndex) {
    throw 'Expected native-restore to run before native-headless-harness-smoke-test because the smoke copies FFmpeg package DLLs.'
}

Write-Output 'playback-core-checks plan ok'
