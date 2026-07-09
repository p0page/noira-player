param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64')]
    [string]$Platform = 'x64',

    [ValidateSet('Build', 'Publish')]
    [string]$Target = 'Build',

    [string]$ProjectPath = '',
    [string]$NativeProjectPath = '',
    [string]$MsBuildPath = '',
    [switch]$SkipNativeRestore,
    [switch]$DisableAot
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

if ([string]::IsNullOrWhiteSpace($NativeProjectPath)) {
    $NativeProjectPath = Join-Path $repoRoot 'src\NoiraPlayer.Native\NoiraPlayer.Native.vcxproj'
}
elseif (-not [System.IO.Path]::IsPathRooted($NativeProjectPath)) {
    $NativeProjectPath = Join-Path $repoRoot $NativeProjectPath
}

function Invoke-CheckedProcess([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

function Invoke-CapturedProcess([string]$FilePath, [string[]]$Arguments) {
    $output = [System.Collections.Generic.List[string]]::new()
    & $FilePath @Arguments 2>&1 | ForEach-Object {
        $line = $_.ToString()
        $output.Add($line)
        Write-Host $line
    }

    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }

    return $output.ToArray()
}

function Assert-NoAotOrTrimWarnings([string[]]$OutputLines) {
    $aotWarningCodePattern = 'IL2\d{3}|IL3\d{3}'
    $warnings = @($OutputLines | Where-Object {
            $_ -match '\bwarning\b' -and $_ -match $aotWarningCodePattern
        })

    if ($warnings.Count -gt 0) {
        $message = "Native AOT/trimming warnings are blockers for the modern publish path."
        throw ($message + [Environment]::NewLine + ($warnings -join [Environment]::NewLine))
    }
}

$projectFullPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$nativeProjectFullPath = (Resolve-Path -LiteralPath $NativeProjectPath).Path
Assert-DotNetSdkSupportsModernNet
$msbuild = Resolve-ModernMsBuildPath $MsBuildPath

if (-not $SkipNativeRestore) {
    Invoke-CheckedProcess $msbuild @(
        $nativeProjectFullPath,
        '/t:Restore',
        '/p:RestorePackagesConfig=true',
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        '/v:minimal'
    )
}

$buildProperties = @(
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    '/p:AppxBundle=Never',
    '/p:AppxPackageSigningEnabled=false'
)

if ($DisableAot) {
    $buildProperties += '/p:PublishAot=false'
}

if ($Target -eq 'Publish') {
    $buildProperties += "/p:PublishProfile=win-$Platform.modern.pubxml"
}

$buildArguments = @(
    $projectFullPath,
    '/restore',
    "/t:$Target",
    '/v:minimal'
) + $buildProperties

$buildOutput = Invoke-CapturedProcess $msbuild $buildArguments
if ($Target -eq 'Publish' -and -not $DisableAot) {
    Assert-NoAotOrTrimWarnings $buildOutput
}
