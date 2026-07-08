param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64', 'x86')]
    [string]$Platform = 'x64',

    [string]$ProjectPath = '',
    [string]$MsBuildPath = '',
    [switch]$SkipBuild,
    [switch]$SkipClean,
    [switch]$ValidateOnly,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot 'src\NoiraPlayer.App\NoiraPlayer.App.csproj'
} elseif (-not [System.IO.Path]::IsPathRooted($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot $ProjectPath
}

$projectFullPath = (Resolve-Path $ProjectPath).Path
$projectDirectory = Split-Path -Parent $projectFullPath
$layoutDirectory = Join-Path $projectDirectory "bin\$Platform\$Configuration"

function Resolve-MsBuildPath([string]$ExplicitPath) {
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "MSBuild path not found: $ExplicitPath"
        }

        return (Resolve-Path $ExplicitPath).Path
    }

    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswherePath) {
        $found = & $vswherePath -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($found) -and (Test-Path -LiteralPath $found)) {
            return (Resolve-Path $found).Path
        }
    }

    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw 'MSBuild.exe was not found. Install Visual Studio 2022 with MSBuild/UWP workloads or pass -MsBuildPath.'
}

function Invoke-CheckedProcess([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

function Remove-DirectoryInsideRoot([string]$Path, [string]$RootPath) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $resolvedRoot = (Resolve-Path -LiteralPath $RootPath).Path.TrimEnd('\')
    $rootPrefix = $resolvedRoot + '\'

    if (-not $resolvedPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete path outside project directory: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

if (-not $SkipBuild) {
    $msbuild = Resolve-MsBuildPath $MsBuildPath
    if (-not $SkipClean) {
        Invoke-CheckedProcess $msbuild @(
            $projectFullPath,
            '/restore',
            '/t:Clean',
            "/p:Configuration=$Configuration",
            "/p:Platform=$Platform",
            '/v:minimal'
        )

        Remove-DirectoryInsideRoot $layoutDirectory $projectDirectory
    }

    Invoke-CheckedProcess $msbuild @(
        $projectFullPath,
        '/restore',
        '/t:Build',
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        '/p:AppxBundle=Never',
        '/p:AppxPackageSigningEnabled=false',
        '/v:minimal'
    )
}

$manifestPath = Join-Path $layoutDirectory 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Loose AppxManifest.xml not found: $manifestPath"
}

$manifest = [xml](Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw)
$application = @($manifest.Package.Applications.Application)[0]
$applicationId = $application.Id
if ([string]::IsNullOrWhiteSpace($applicationId)) {
    $applicationId = 'App'
}

$packageName = $manifest.Package.Identity.Name
$displayName = $manifest.Package.Properties.DisplayName
$executable = $application.Executable

$report = [ordered]@{
    configuration = $Configuration
    platform = $Platform
    projectPath = $projectFullPath
    layoutDirectory = (Resolve-Path $layoutDirectory).Path
    manifestPath = (Resolve-Path $manifestPath).Path
    packageName = $packageName
    displayName = $displayName
    executable = $executable
    applicationId = $applicationId
    registered = $false
    launched = $false
}

if ($ValidateOnly) {
    $report | ConvertTo-Json -Depth 4
    return
}

Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown

$package = Get-AppxPackage -Name $packageName | Sort-Object Version -Descending | Select-Object -First 1
if ($null -eq $package) {
    throw "Package was registered but Get-AppxPackage could not find it: $packageName"
}

$appUserModelId = "$($package.PackageFamilyName)!$applicationId"
$report.registered = $true
$report.packageFullName = $package.PackageFullName
$report.packageFamilyName = $package.PackageFamilyName
$report.appUserModelId = $appUserModelId

if ($Launch) {
    Start-Process "shell:AppsFolder\$appUserModelId"
    $report.launched = $true
}

$report | ConvertTo-Json -Depth 4
