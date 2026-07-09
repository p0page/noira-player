$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scriptPath = Join-Path $repoRoot 'tools\Register-NoiraLooseApp.ps1'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function New-TestProjectLayout {
    $root = Join-Path ([System.IO.Path]::GetTempPath()) ("noira-register-test-" + [System.Guid]::NewGuid().ToString("N"))
    $projectDir = Join-Path $root 'src\NoiraPlayer.App'
    $layoutDir = Join-Path $projectDir 'bin\x64\Debug'
    $packageLayoutDir = Join-Path $projectDir 'AppPackages\_unpacked\NoiraPlayer.App_0.1.0.279_x64_Debug'
    New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null
    New-Item -ItemType Directory -Force -Path $packageLayoutDir | Out-Null

    $projectPath = Join-Path $projectDir 'NoiraPlayer.App.csproj'
    Set-Content -LiteralPath $projectPath -Value '<Project />' -Encoding UTF8

    $looseManifest = @'
<?xml version="1.0" encoding="utf-8"?>
<Package>
  <Identity Name="NoiraPlayer.App" Publisher="CN=NoiraPlayer" Version="0.1.0.279" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>Noira</DisplayName>
  </Properties>
  <Applications>
    <Application Id="App" Executable="NoiraPlayer.App.exe" EntryPoint="NoiraPlayer.App.App" />
  </Applications>
  <Extensions>
    <Extension Category="windows.activatableClass.inProcessServer">
      <InProcessServer>
        <Path>CLRHost.dll</Path>
        <ActivatableClass ActivatableClassId="Microsoft.UI.Xaml.Markup.ReflectionXamlMetadataProvider" ThreadingModel="both" />
      </InProcessServer>
    </Extension>
  </Extensions>
</Package>
'@
    Set-Content -LiteralPath (Join-Path $layoutDir 'AppxManifest.xml') -Value $looseManifest -Encoding UTF8

    $packageManifest = $looseManifest.Replace('<Path>CLRHost.dll</Path>', '<Path>NoiraPlayer.App.exe</Path>')
    Set-Content -LiteralPath (Join-Path $packageLayoutDir 'AppxManifest.xml') -Value $packageManifest -Encoding UTF8
    $packageDir = Join-Path $projectDir 'AppPackages\NoiraPlayer.App_0.1.0.279_x64_Debug_Test'
    New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
    Set-Content -LiteralPath (Join-Path $packageDir 'NoiraPlayer.App_0.1.0.279_x64_Debug.msix') -Value 'fake-msix' -Encoding UTF8

    [pscustomobject]@{
        Root = $root
        ProjectPath = $projectPath
        LayoutDir = $layoutDir
        PackageLayoutDir = $packageLayoutDir
    }
}

function New-FakeMakeAppx([string]$Root) {
    $manifestTemplatePath = Join-Path $Root 'package-manifest.xml'
    Set-Content -LiteralPath $manifestTemplatePath -Encoding UTF8 -Value @'
<?xml version="1.0" encoding="utf-8"?>
<Package>
  <Identity Name="NoiraPlayer.App" Publisher="CN=NoiraPlayer" Version="0.1.0.279" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>Noira</DisplayName>
  </Properties>
  <Applications>
    <Application Id="App" Executable="NoiraPlayer.App.exe" EntryPoint="NoiraPlayer.App.App" />
  </Applications>
  <Extensions>
    <Extension Category="windows.activatableClass.inProcessServer">
      <InProcessServer>
        <Path>NoiraPlayer.App.exe</Path>
        <ActivatableClass ActivatableClassId="Microsoft.UI.Xaml.Markup.ReflectionXamlMetadataProvider" ThreadingModel="both" />
      </InProcessServer>
    </Extension>
  </Extensions>
</Package>
'@

    $path = Join-Path $Root 'fake-makeappx.ps1'
    $script = @'
$destination = ''
for ($i = 0; $i -lt $args.Count; $i++) {
    if ($args[$i] -eq '/d' -and ($i + 1) -lt $args.Count) {
        $destination = $args[$i + 1]
    }
}

if ([string]::IsNullOrWhiteSpace($destination)) {
    throw 'Missing /d destination.'
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'package-manifest.xml') -Destination (Join-Path $destination 'AppxManifest.xml') -Force
Write-Output 'fake makeappx output that must not become the layout path'
'@
    Set-Content -LiteralPath $path -Value $script -Encoding UTF8
    return $path
}

function New-TestPackage([string]$InstallLocation) {
    [pscustomobject]@{
        Name = 'NoiraPlayer.App'
        Version = [version]'0.1.0.279'
        PackageFullName = 'NoiraPlayer.App_0.1.0.279_x64__hkwzw7pzpr4z0'
        PackageFamilyName = 'NoiraPlayer.App_hkwzw7pzpr4z0'
        InstallLocation = $InstallLocation
    }
}

$global:NoiraRegisterTestCurrentLayout = $null
$global:NoiraRegisterTestMockMode = ''
$global:NoiraRegisterTestAddAppxPackageCalls = 0
$global:NoiraRegisterTestRemoveAppxPackageCalls = 0
$global:NoiraRegisterTestRegisteredManifestPath = ''
$global:NoiraRegisterTestRemovedPackageFullName = ''
$global:NoiraRegisterTestStartedProcess = ''

function Get-AppxPackage {
    param([string]$Name)

    if ($Name -ne 'NoiraPlayer.App') {
        return @()
    }

    if ($global:NoiraRegisterTestMockMode -eq 'matching-package-layout') {
        return @(New-TestPackage $global:NoiraRegisterTestCurrentLayout.PackageLayoutDir)
    }

    if ($global:NoiraRegisterTestMockMode -eq 'stale-bin-layout-first') {
        if ($global:NoiraRegisterTestAddAppxPackageCalls -eq 0) {
            return @(New-TestPackage $global:NoiraRegisterTestCurrentLayout.LayoutDir)
        }

        return @(New-TestPackage $global:NoiraRegisterTestCurrentLayout.PackageLayoutDir)
    }

    if ($global:NoiraRegisterTestMockMode -eq 'no-package-first') {
        if ($global:NoiraRegisterTestAddAppxPackageCalls -eq 0) {
            return @()
        }

        return @(New-TestPackage $global:NoiraRegisterTestCurrentLayout.PackageLayoutDir)
    }

    return @()
}

function Add-AppxPackage {
    param(
        [string]$Register,
        [switch]$ForceApplicationShutdown
    )

    $global:NoiraRegisterTestAddAppxPackageCalls++
    $global:NoiraRegisterTestRegisteredManifestPath = $Register
}

function Remove-AppxPackage {
    param(
        [string]$Package,
        [switch]$PreserveApplicationData
    )

    $global:NoiraRegisterTestRemoveAppxPackageCalls++
    $global:NoiraRegisterTestRemovedPackageFullName = $Package
}

function Start-Process {
    param([string]$FilePath)
    $global:NoiraRegisterTestStartedProcess = $FilePath
}

try {
    $layout = New-TestProjectLayout
    $global:NoiraRegisterTestCurrentLayout = $layout
    $global:NoiraRegisterTestMockMode = 'matching-package-layout'

    $json = & $scriptPath -ProjectPath $layout.ProjectPath -SkipBuild -PackageLayoutDirectory $layout.PackageLayoutDir -Launch
    $report = $json | ConvertFrom-Json

    Assert-True ($global:NoiraRegisterTestAddAppxPackageCalls -eq 0) 'Expected existing same-version package layout to be reused without Add-AppxPackage.'
    Assert-True ($global:NoiraRegisterTestRemoveAppxPackageCalls -eq 0) 'Expected matching package layout to be reused without Remove-AppxPackage.'
    Assert-True ($report.registered -eq $true) 'Expected report.registered to be true.'
    Assert-True ($report.registrationAction -eq 'reused') 'Expected report.registrationAction to be reused.'
    Assert-True ($report.layoutDirectory -eq (Resolve-Path $layout.PackageLayoutDir).Path) 'Expected report.layoutDirectory to use the package layout, not bin layout.'
    Assert-True ($report.appUserModelId -eq 'NoiraPlayer.App_hkwzw7pzpr4z0!App') 'Expected AppUserModelId to use existing package family name.'
    Assert-True ($global:NoiraRegisterTestStartedProcess -eq 'shell:AppsFolder\NoiraPlayer.App_hkwzw7pzpr4z0!App') 'Expected launch to use package activation.'

    $layout2 = New-TestProjectLayout
    $global:NoiraRegisterTestCurrentLayout = $layout2
    $global:NoiraRegisterTestMockMode = 'stale-bin-layout-first'
    $global:NoiraRegisterTestAddAppxPackageCalls = 0
    $global:NoiraRegisterTestRemoveAppxPackageCalls = 0
    $global:NoiraRegisterTestRegisteredManifestPath = ''
    $global:NoiraRegisterTestRemovedPackageFullName = ''

    $json2 = & $scriptPath -ProjectPath $layout2.ProjectPath -SkipBuild -PackageLayoutDirectory $layout2.PackageLayoutDir
    $report2 = $json2 | ConvertFrom-Json

    Assert-True ($global:NoiraRegisterTestRemoveAppxPackageCalls -eq 1) 'Expected stale same-version bin layout registration to be removed first.'
    Assert-True ($global:NoiraRegisterTestRemovedPackageFullName -eq 'NoiraPlayer.App_0.1.0.279_x64__hkwzw7pzpr4z0') 'Expected stale package full name to be removed.'
    Assert-True ($global:NoiraRegisterTestAddAppxPackageCalls -eq 1) 'Expected package layout to be registered after removing stale registration.'
    Assert-True ($global:NoiraRegisterTestRegisteredManifestPath -eq (Join-Path $layout2.PackageLayoutDir 'AppxManifest.xml')) 'Expected Add-AppxPackage to register package-layout AppxManifest.xml.'
    Assert-True ($report2.registrationAction -eq 'reregistered') 'Expected report.registrationAction to be reregistered.'

    $layout3 = New-TestProjectLayout
    $global:NoiraRegisterTestCurrentLayout = $layout3
    $global:NoiraRegisterTestMockMode = 'no-package-first'
    $global:NoiraRegisterTestAddAppxPackageCalls = 0
    $global:NoiraRegisterTestRemoveAppxPackageCalls = 0
    $global:NoiraRegisterTestRegisteredManifestPath = ''

    $fakeMakeAppx = New-FakeMakeAppx $layout3.Root
    $json3 = & $scriptPath -ProjectPath $layout3.ProjectPath -SkipBuild -MakeAppxPath $fakeMakeAppx
    $report3 = $json3 | ConvertFrom-Json

    Assert-True ($global:NoiraRegisterTestAddAppxPackageCalls -eq 1) 'Expected unpacked package layout to be registered.'
    Assert-True ($report3.layoutDirectory -eq (Resolve-Path $layout3.PackageLayoutDir).Path) 'Expected makeappx stdout to be ignored and layoutDirectory to be the unpack directory.'
    Assert-True ($report3.registrationAction -eq 'registered') 'Expected first package registration to be registered.'

    $layout4 = New-TestProjectLayout
    $global:NoiraRegisterTestCurrentLayout = $layout4
    $global:NoiraRegisterTestMockMode = 'matching-package-layout'
    $global:NoiraRegisterTestAddAppxPackageCalls = 0
    $global:NoiraRegisterTestRemoveAppxPackageCalls = 0
    $global:NoiraRegisterTestRegisteredManifestPath = ''

    $fakeMakeAppx2 = New-FakeMakeAppx $layout4.Root
    $json4 = & $scriptPath -ProjectPath $layout4.ProjectPath -SkipBuild -MakeAppxPath $fakeMakeAppx2
    $report4 = $json4 | ConvertFrom-Json

    Assert-True ($global:NoiraRegisterTestRemoveAppxPackageCalls -eq 1) 'Expected existing same-layout package to be removed after refreshing unpacked layout.'
    Assert-True ($global:NoiraRegisterTestAddAppxPackageCalls -eq 1) 'Expected refreshed unpacked layout to be registered even when the path matches.'
    Assert-True ($report4.registrationAction -eq 'reregistered') 'Expected refreshed unpacked layout registrationAction to be reregistered.'

    'register-noira-loose-app tests ok'
}
finally {
    if ($null -ne $layout -and (Test-Path -LiteralPath $layout.Root)) {
        Remove-Item -LiteralPath $layout.Root -Recurse -Force
    }

    if ($null -ne $layout2 -and (Test-Path -LiteralPath $layout2.Root)) {
        Remove-Item -LiteralPath $layout2.Root -Recurse -Force
    }

    if ($null -ne $layout3 -and (Test-Path -LiteralPath $layout3.Root)) {
        Remove-Item -LiteralPath $layout3.Root -Recurse -Force
    }

    if ($null -ne $layout4 -and (Test-Path -LiteralPath $layout4.Root)) {
        Remove-Item -LiteralPath $layout4.Root -Recurse -Force
    }

    Remove-Variable -Name NoiraRegisterTestStartedProcess -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name NoiraRegisterTestCurrentLayout -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name NoiraRegisterTestMockMode -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name NoiraRegisterTestAddAppxPackageCalls -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name NoiraRegisterTestRemoveAppxPackageCalls -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name NoiraRegisterTestRegisteredManifestPath -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name NoiraRegisterTestRemovedPackageFullName -Scope Global -ErrorAction SilentlyContinue
}
