param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64', 'x86')]
    [string]$Platform = 'x64',

    [string]$ProjectPath = '',
    [string]$MsBuildPath = '',
    [string]$MakeAppxPath = '',
    [string]$PackageLayoutDirectory = '',
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

function Test-MsBuildHasWindowsXamlTargets([string]$Path) {
    $binDirectory = Split-Path -Parent $Path
    $msbuildRoot = Join-Path $binDirectory '..\..'
    $targetPath = Join-Path $msbuildRoot 'Microsoft\WindowsXaml\v17.0\Microsoft.Windows.UI.Xaml.CSharp.targets'
    return Test-Path -LiteralPath $targetPath
}

function Resolve-MsBuildPath([string]$ExplicitPath) {
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "MSBuild path not found: $ExplicitPath"
        }

        $resolved = (Resolve-Path $ExplicitPath).Path
        if (-not (Test-MsBuildHasWindowsXamlTargets $resolved)) {
            throw "MSBuild path does not contain WindowsXaml v17.0 targets required by the UWP project: $resolved"
        }

        return $resolved
    }

    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswherePath) {
        $found = & $vswherePath -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($found) -and (Test-Path -LiteralPath $found)) {
            $resolved = (Resolve-Path $found).Path
            if (Test-MsBuildHasWindowsXamlTargets $resolved) {
                return $resolved
            }
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
            $resolved = (Resolve-Path $candidate).Path
            if (Test-MsBuildHasWindowsXamlTargets $resolved) {
                return $resolved
            }
        }
    }

    throw 'MSBuild.exe with WindowsXaml v17.0 targets was not found. Install Visual Studio 2022 with UWP workloads or pass -MsBuildPath.'
}

function Resolve-MakeAppxPath([string]$ExplicitPath) {
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "makeappx.exe path not found: $ExplicitPath"
        }

        return (Resolve-Path $ExplicitPath).Path
    }

    $command = Get-Command makeappx.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command -and -not [string]::IsNullOrWhiteSpace($command.Source)) {
        return $command.Source
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $kitsRoot) {
        $found = Get-ChildItem -LiteralPath $kitsRoot -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\$Platform\\makeappx\.exe$" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($null -ne $found) {
            return $found.FullName
        }
    }

    throw 'makeappx.exe was not found. Install the Windows SDK or pass -MakeAppxPath.'
}

function Invoke-CheckedProcess([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
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

function Find-PackagedMsix(
    [string]$ProjectDirectory,
    [string]$PackageName,
    [version]$PackageVersion,
    [string]$Platform,
    [string]$Configuration
) {
    $packagesRoot = Join-Path $ProjectDirectory 'AppPackages'
    if (-not (Test-Path -LiteralPath $packagesRoot)) {
        throw "AppPackages directory not found: $packagesRoot"
    }

    $expectedName = "{0}_{1}_{2}_{3}.msix" -f $PackageName, $PackageVersion, $Platform, $Configuration
    $package = Get-ChildItem -LiteralPath $packagesRoot -Recurse -Filter '*.msix' |
        Where-Object { $_.Name -eq $expectedName } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $package) {
        throw "Packaged MSIX not found under $packagesRoot. Expected $expectedName. Build without -SkipBuild first."
    }

    return $package.FullName
}

function Expand-PackagedMsixLayout(
    [string]$MakeAppx,
    [string]$PackagePath,
    [string]$ProjectDirectory,
    [string]$PackageName,
    [version]$PackageVersion,
    [string]$Platform,
    [string]$Configuration
) {
    $unpackRoot = Join-Path $ProjectDirectory 'AppPackages\_unpacked'
    $unpackDirectory = Join-Path $unpackRoot ("{0}_{1}_{2}_{3}" -f $PackageName, $PackageVersion, $Platform, $Configuration)

    if (-not (Test-Path -LiteralPath $unpackRoot)) {
        New-Item -ItemType Directory -Force -Path $unpackRoot | Out-Null
    }

    Remove-DirectoryInsideRoot $unpackDirectory $projectDirectory
    Invoke-CheckedProcess $MakeAppx @('unpack', '/p', $PackagePath, '/d', $unpackDirectory, '/o') | Out-Null

    return (Resolve-Path -LiteralPath $unpackDirectory).Path
}

function Assert-LaunchablePackageManifest([string]$ManifestPath, [string]$LayoutDirectory) {
    $manifestText = Get-Content -LiteralPath $ManifestPath -Encoding UTF8 -Raw
    if ($manifestText -match '<Path>\s*CLRHost\.dll\s*</Path>' -and
        -not (Test-Path -LiteralPath (Join-Path $LayoutDirectory 'CLRHost.dll'))) {
        throw "Selected package layout is not launchable: XAML metadata provider points to missing CLRHost.dll. Use the MSIX package layout instead of bin output."
    }
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

$binManifestPath = Join-Path $layoutDirectory 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $binManifestPath)) {
    throw "Loose AppxManifest.xml not found: $binManifestPath"
}

$binManifest = [xml](Get-Content -LiteralPath $binManifestPath -Encoding UTF8 -Raw)
$packageName = $binManifest.Package.Identity.Name
$packageVersion = [version]$binManifest.Package.Identity.Version

$packagePath = ''
$expandedPackageLayout = $false
if ([string]::IsNullOrWhiteSpace($PackageLayoutDirectory)) {
    $packagePath = Find-PackagedMsix $projectDirectory $packageName $packageVersion $Platform $Configuration
    $makeappx = Resolve-MakeAppxPath $MakeAppxPath
    $PackageLayoutDirectory = Expand-PackagedMsixLayout $makeappx $packagePath $projectDirectory $packageName $packageVersion $Platform $Configuration
    $expandedPackageLayout = $true
} elseif (-not [System.IO.Path]::IsPathRooted($PackageLayoutDirectory)) {
    $PackageLayoutDirectory = Join-Path $repoRoot $PackageLayoutDirectory
}

$resolvedLayoutDirectory = (Resolve-Path -LiteralPath $PackageLayoutDirectory).Path.TrimEnd('\')
$manifestPath = Join-Path $resolvedLayoutDirectory 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Package-layout AppxManifest.xml not found: $manifestPath"
}

Assert-LaunchablePackageManifest $manifestPath $resolvedLayoutDirectory

$manifest = [xml](Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw)
$application = @($manifest.Package.Applications.Application)[0]
$applicationId = $application.Id
if ([string]::IsNullOrWhiteSpace($applicationId)) {
    $applicationId = 'App'
}

$packageName = $manifest.Package.Identity.Name
$packageVersion = [version]$manifest.Package.Identity.Version
$displayName = $manifest.Package.Properties.DisplayName
$executable = $application.Executable

$report = [ordered]@{
    configuration = $Configuration
    platform = $Platform
    projectPath = $projectFullPath
    binLayoutDirectory = (Resolve-Path $layoutDirectory).Path
    layoutDirectory = $resolvedLayoutDirectory
    manifestPath = (Resolve-Path $manifestPath).Path
    packagePath = $packagePath
    packageName = $packageName
    displayName = $displayName
    executable = $executable
    applicationId = $applicationId
    registrationAction = ''
    registered = $false
    launched = $false
}

if ($ValidateOnly) {
    $report.registrationAction = 'validated'
    $report | ConvertTo-Json -Depth 4
    return
}

$installedPackages = @(Get-AppxPackage -Name $packageName)
$package = $installedPackages |
    Where-Object {
        if ($null -eq $_.Version -or $null -eq $_.InstallLocation) {
            return $false
        }

        $existingVersion = [version]$_.Version
        if ($existingVersion -ne $packageVersion) {
            return $false
        }

        $existingLocation = (Resolve-Path -LiteralPath $_.InstallLocation -ErrorAction SilentlyContinue).Path
        if ([string]::IsNullOrWhiteSpace($existingLocation)) {
            return $false
        }

        return [string]::Equals(
            $existingLocation.TrimEnd('\'),
            $resolvedLayoutDirectory,
            [System.StringComparison]::OrdinalIgnoreCase)
    } |
    Sort-Object Version -Descending |
    Select-Object -First 1

$registrationAction = 'reused'
if ($expandedPackageLayout -or $null -eq $package) {
    $sameVersionPackages = @($installedPackages |
        Where-Object {
            $null -ne $_.Version -and ([version]$_.Version) -eq $packageVersion
        })

    foreach ($stalePackage in $sameVersionPackages) {
        Remove-AppxPackage -Package $stalePackage.PackageFullName -PreserveApplicationData
    }

    Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown

    $package = Get-AppxPackage -Name $packageName | Sort-Object Version -Descending | Select-Object -First 1
    if ($null -eq $package) {
        throw "Package was registered but Get-AppxPackage could not find it: $packageName"
    }

    $registrationAction = if ($sameVersionPackages.Count -gt 0) { 'reregistered' } else { 'registered' }
}

$appUserModelId = "$($package.PackageFamilyName)!$applicationId"
$report.registered = $true
$report.registrationAction = $registrationAction
$report.packageFullName = $package.PackageFullName
$report.packageFamilyName = $package.PackageFamilyName
$report.appUserModelId = $appUserModelId

if ($Launch) {
    Start-Process "shell:AppsFolder\$appUserModelId"
    $report.launched = $true
}

$report | ConvertTo-Json -Depth 4
