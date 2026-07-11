param(
    [string]$ProjectPath = 'src\NoiraPlayer.Native\NoiraPlayer.Native.vcxproj',
    [string]$Configuration = 'Debug',
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$resolvedProjectPath = if ([System.IO.Path]::IsPathRooted($ProjectPath)) {
    $ProjectPath
}
else {
    Join-Path $repoRoot $ProjectPath
}
$projectDirectory = Split-Path $resolvedProjectPath -Parent
$packagesConfigPath = Join-Path $projectDirectory 'packages.config'
[xml]$packagesConfig = Get-Content -LiteralPath $packagesConfigPath -Raw
$packages = @($packagesConfig.packages.package)
if ($packages.Count -eq 0) {
    throw "No packages were declared in $packagesConfigPath."
}

$localPackagesRoot = Join-Path $projectDirectory 'packages'
$globalPackagesRoot = if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages'
}
else {
    $env:NUGET_PACKAGES
}
New-Item -ItemType Directory -Path $localPackagesRoot -Force | Out-Null

$missingPackages = [System.Collections.Generic.List[string]]::new()
foreach ($package in $packages) {
    $id = [string]$package.id
    $version = [string]$package.version
    $localPath = Join-Path $localPackagesRoot ($id + '.' + $version)
    if (Test-Path -LiteralPath $localPath) {
        continue
    }

    $globalPath = Join-Path $globalPackagesRoot (($id.ToLowerInvariant()) + '\' + $version)
    if (Test-Path -LiteralPath $globalPath) {
        New-Item -ItemType Junction -Path $localPath -Target $globalPath | Out-Null
        Write-Output ("linked native package cache: $id $version")
        continue
    }

    $missingPackages.Add($id + ' ' + $version)
}

if ($missingPackages.Count -eq 0) {
    Write-Output 'native packages are available from repo-local or global NuGet cache'
    exit 0
}

. (Join-Path $repoRoot 'tools\NoiraModernToolchain.ps1')
$msbuild = Resolve-ModernMsBuildPath ''
& $msbuild $resolvedProjectPath `
    '/t:Restore' `
    '/p:RestorePackagesConfig=true' `
    "/p:Configuration=$Configuration" `
    "/p:Platform=$Platform" `
    '/v:minimal'
exit $LASTEXITCODE
