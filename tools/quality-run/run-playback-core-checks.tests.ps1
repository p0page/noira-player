$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'run-playback-core-checks.ps1'
$planJson = & $scriptPath -PlanOnly
$plan = $planJson | ConvertFrom-Json

if (-not $plan.commands -or $plan.commands.Count -lt 2) {
    throw 'Expected at least two playback-core validation commands.'
}

if ($plan.scope -ne 'playback-core') {
    throw 'Expected playback-core validation scope.'
}

if (-not ($plan.includedRoots -contains 'src/NextGenEmby.Core')) {
    throw 'Expected src/NextGenEmby.Core in playback-core included roots.'
}

if (-not ($plan.includedRoots -contains 'src/NextGenEmby.Native')) {
    throw 'Expected src/NextGenEmby.Native in playback-core included roots.'
}

if (-not ($plan.excludedRoots -contains 'src/NextGenEmby.App')) {
    throw 'Expected src/NextGenEmby.App in playback-core excluded roots.'
}

$serializedCommands = $plan.commands | ConvertTo-Json -Depth 6
if ($serializedCommands -match 'NextGenEmby\.App|AppPackages|msix|NextGenEmby\.App\.csproj') {
    throw 'Playback-core validation plan must not build or package the App project.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'core-tests' })) {
    throw 'Expected core-tests command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'script-plan-test' })) {
    throw 'Expected script-plan-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-helper-test' })) {
    throw 'Expected native-helper-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-display-refresh-test' })) {
    throw 'Expected native-display-refresh-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-display-refresh-snapshot-test' })) {
    throw 'Expected native-display-refresh-snapshot-test command in playback-core validation plan.'
}

if (-not ($plan.commands | Where-Object { $_.name -eq 'native-restore' })) {
    throw 'Expected native-restore command before native-build in playback-core validation plan.'
}

$nativeRestoreIndex = [array]::IndexOf($plan.commands.name, 'native-restore')
$nativeBuildIndex = [array]::IndexOf($plan.commands.name, 'native-build')
if ($nativeBuildIndex -ge 0 -and $nativeRestoreIndex -gt $nativeBuildIndex) {
    throw 'Expected native-restore to run before native-build.'
}

Write-Output 'playback-core-checks plan ok'
