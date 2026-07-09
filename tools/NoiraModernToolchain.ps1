$ErrorActionPreference = 'Stop'

function Test-MsBuildHasModernUwpTargets([string]$Path) {
    $binDirectory = Split-Path -Parent $Path
    $msbuildRoot = Join-Path $binDirectory '..\..'
    $targetPath = Join-Path $msbuildRoot 'Microsoft\WindowsXaml\v18.0\8.21\Microsoft.Windows.UI.Xaml.CSharp.ModernNET.targets'
    return Test-Path -LiteralPath $targetPath
}

function Resolve-ModernMsBuildPath([string]$ExplicitPath) {
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "MSBuild path not found: $ExplicitPath"
        }

        $resolved = (Resolve-Path -LiteralPath $ExplicitPath).Path
        if (-not (Test-MsBuildHasModernUwpTargets $resolved)) {
            throw "MSBuild path does not contain VS2026 modern UWP targets: $resolved"
        }

        return $resolved
    }

    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswherePath) {
        $found = & $vswherePath -version '[18.0,19.0)' -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
            Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($found) -and (Test-Path -LiteralPath $found)) {
            $resolved = (Resolve-Path -LiteralPath $found).Path
            if (Test-MsBuildHasModernUwpTargets $resolved) {
                return $resolved
            }
        }
    }

    $candidate = 'C:\Program\MSBuild\Current\Bin\MSBuild.exe'
    if (Test-Path -LiteralPath $candidate) {
        $resolved = (Resolve-Path -LiteralPath $candidate).Path
        if (Test-MsBuildHasModernUwpTargets $resolved) {
            return $resolved
        }
    }

    throw 'MSBuild.exe with VS2026 modern UWP targets was not found. Install Visual Studio 2026 UWP tools or pass -MsBuildPath.'
}

function Assert-DotNetSdkSupportsModernNet() {
    $minimumSdkVersion = [version]'10.0'
    $sdkLines = & dotnet '--list-sdks'
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "dotnet --list-sdks failed with exit code $LASTEXITCODE"
    }

    $sdkVersions = @()
    foreach ($line in $sdkLines) {
        if ($line -match '^\s*(\d+\.\d+(?:\.\d+)?(?:[-A-Za-z0-9.]+)?)\s+\[') {
            $versionText = ($Matches[1] -split '-')[0]
            try {
                $sdkVersions += [version]$versionText
            }
            catch {
                # Ignore SDK labels that cannot be represented by System.Version.
            }
        }
    }

    $hasModernSdk = $false
    foreach ($sdkVersion in $sdkVersions) {
        if ($sdkVersion -ge $minimumSdkVersion) {
            $hasModernSdk = $true
            break
        }
    }

    if (-not $hasModernSdk) {
        $installed = if ($sdkLines.Count -gt 0) { $sdkLines -join '; ' } else { '<none>' }
        throw "Modern .NET toolchain requires .NET SDK 10. Installed SDKs: $installed"
    }
}
