param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64')]
    [string]$Platform = 'x64',

    [string]$ProjectPath = '',
    [string]$MsBuildPath = '',
    [string]$LayoutDirectory = '',
    [switch]$SkipBuild,
    [switch]$ValidateOnly,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$modernToolchainScriptPath = Join-Path $PSScriptRoot 'NoiraModernToolchain.ps1'
. $modernToolchainScriptPath

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot 'src\NoiraPlayer.App\NoiraPlayer.App.Modern.csproj'
}
elseif (-not [System.IO.Path]::IsPathRooted($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot $ProjectPath
}

$projectFullPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$projectDirectory = Split-Path -Parent $projectFullPath
$buildScriptPath = Join-Path $repoRoot 'tools\Build-NoiraModernUwp.ps1'

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

function Copy-PackageFile([string]$SourcePath, [string]$PackagePath, [string]$DestinationRoot) {
    $resolvedSource = [System.Uri]::UnescapeDataString($SourcePath)
    if (-not (Test-Path -LiteralPath $resolvedSource)) {
        throw "Packaged source file not found: $resolvedSource"
    }

    $destinationPath = Join-Path $DestinationRoot $PackagePath
    $destinationDirectory = Split-Path -Parent $destinationPath
    if (-not (Test-Path -LiteralPath $destinationDirectory)) {
        New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    }

    Copy-Item -LiteralPath $resolvedSource -Destination $destinationPath -Force
}

function Get-MsBuildProperties(
    [string]$Project,
    [string]$Configuration,
    [string]$Platform,
    [string]$MsBuild
) {
    $arguments = @(
        $Project,
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        "/p:RuntimeIdentifier=win-$Platform",
        '/getProperty:MSBuildProjectName,OutDir,NativeOutputPath,PublishDir',
        '/nologo'
    )

    $output = & $MsBuild @arguments

    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "MSBuild property query failed with exit code $LASTEXITCODE"
    }

    return ($output | Out-String | ConvertFrom-Json).Properties
}

Assert-DotNetSdkSupportsModernNet
$resolvedMsBuildPath = Resolve-ModernMsBuildPath $MsBuildPath

if (-not $SkipBuild) {
    $buildArguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $buildScriptPath,
        '-Configuration',
        $Configuration,
        '-Platform',
        $Platform,
        '-Target',
        'Publish'
    )

    $buildArguments += @('-MsBuildPath', $resolvedMsBuildPath)

    Invoke-CheckedProcess 'powershell' $buildArguments
}

$properties = Get-MsBuildProperties $projectFullPath $Configuration $Platform $resolvedMsBuildPath
$outDir = $properties.OutDir
$nativeOutputPath = $properties.NativeOutputPath
$publishDir = $properties.PublishDir
$projectName = $properties.MSBuildProjectName

if (-not [System.IO.Path]::IsPathRooted($outDir)) {
    $outDir = Join-Path $projectDirectory $outDir
}
if (-not [System.IO.Path]::IsPathRooted($nativeOutputPath)) {
    $nativeOutputPath = Join-Path $projectDirectory $nativeOutputPath
}
if (-not [System.IO.Path]::IsPathRooted($publishDir)) {
    $publishDir = Join-Path $projectDirectory $publishDir
}

$recipePath = Join-Path $outDir "$projectName.build.appxrecipe"
if (-not (Test-Path -LiteralPath $recipePath)) {
    throw "Modern appx recipe not found: $recipePath. Build the modern project first."
}

$nativeExePath = Join-Path $nativeOutputPath 'NoiraPlayer.App.exe'
if (-not (Test-Path -LiteralPath $nativeExePath)) {
    $nativeExePath = Join-Path $publishDir 'NoiraPlayer.App.exe'
}
if (-not (Test-Path -LiteralPath $nativeExePath)) {
    throw "Native AOT executable not found under $nativeOutputPath or $publishDir."
}

if ([string]::IsNullOrWhiteSpace($LayoutDirectory)) {
    $LayoutDirectory = Join-Path $projectDirectory "bin\Modern\AppxLayouts\$Configuration\$Platform\NativeAot"
}
elseif (-not [System.IO.Path]::IsPathRooted($LayoutDirectory)) {
    $LayoutDirectory = Join-Path $repoRoot $LayoutDirectory
}

Remove-DirectoryInsideRoot $LayoutDirectory $projectDirectory
New-Item -ItemType Directory -Force -Path $LayoutDirectory | Out-Null

$recipe = [xml](Get-Content -LiteralPath $recipePath -Encoding UTF8 -Raw)
$manifestItem = @($recipe.Project.ItemGroup.AppXManifest)[0]
if ($null -eq $manifestItem) {
    throw "AppXManifest item not found in recipe: $recipePath"
}

Copy-PackageFile $manifestItem.Include 'AppxManifest.xml' $LayoutDirectory
foreach ($file in @($recipe.Project.ItemGroup.AppxPackagedFile)) {
    if ($null -eq $file -or [string]::IsNullOrWhiteSpace($file.Include)) {
        continue
    }

    $packagePath = $file.PackagePath
    if ([string]::IsNullOrWhiteSpace($packagePath)) {
        continue
    }

    Copy-PackageFile $file.Include $packagePath $LayoutDirectory
}

Copy-Item -LiteralPath $nativeExePath -Destination (Join-Path $LayoutDirectory 'NoiraPlayer.App.exe') -Force
$nativePdbPath = [System.IO.Path]::ChangeExtension($nativeExePath, '.pdb')
if (Test-Path -LiteralPath $nativePdbPath) {
    Copy-Item -LiteralPath $nativePdbPath -Destination (Join-Path $LayoutDirectory 'NoiraPlayer.App.pdb') -Force
}

$manifestPath = Join-Path $LayoutDirectory 'AppxManifest.xml'
$manifest = [xml](Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw)
$application = @($manifest.Package.Applications.Application)[0]
$applicationId = $application.Id
if ([string]::IsNullOrWhiteSpace($applicationId)) {
    $applicationId = 'App'
}

$packageName = $manifest.Package.Identity.Name
$packageVersion = [version]$manifest.Package.Identity.Version
$executable = $application.Executable
$executablePath = Join-Path $LayoutDirectory $executable
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "Manifest executable not found in staged layout: $executablePath"
}

foreach ($requiredFile in @('NoiraPlayer.Native.dll', 'NoiraPlayer.Native.winmd', 'resources.pri', 'avcodec-62.dll')) {
    $requiredPath = Join-Path $LayoutDirectory $requiredFile
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required staged package file not found: $requiredPath"
    }
}

$report = [ordered]@{
    configuration = $Configuration
    platform = $Platform
    projectPath = $projectFullPath
    recipePath = (Resolve-Path -LiteralPath $recipePath).Path
    nativeExePath = (Resolve-Path -LiteralPath $nativeExePath).Path
    layoutDirectory = (Resolve-Path -LiteralPath $LayoutDirectory).Path
    manifestPath = (Resolve-Path -LiteralPath $manifestPath).Path
    packageName = $packageName
    packageVersion = $packageVersion.ToString()
    executable = $executable
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
$sameVersionPackages = @($installedPackages |
    Where-Object { $null -ne $_.Version -and ([version]$_.Version) -eq $packageVersion })

foreach ($stalePackage in $sameVersionPackages) {
    Remove-AppxPackage -Package $stalePackage.PackageFullName -PreserveApplicationData
}

Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown

$package = Get-AppxPackage -Name $packageName | Sort-Object Version -Descending | Select-Object -First 1
if ($null -eq $package) {
    throw "Package was registered but Get-AppxPackage could not find it: $packageName"
}

$appUserModelId = "$($package.PackageFamilyName)!$applicationId"
$report.registered = $true
$report.registrationAction = if ($sameVersionPackages.Count -gt 0) { 'reregistered' } else { 'registered' }
$report.packageFullName = $package.PackageFullName
$report.packageFamilyName = $package.PackageFamilyName
$report.appUserModelId = $appUserModelId

if ($Launch) {
    Start-Process "shell:AppsFolder\$appUserModelId"
    $report.launched = $true
}

$report | ConvertTo-Json -Depth 4
