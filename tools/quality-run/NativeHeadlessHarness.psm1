function Get-PlaybackQualitySourceFingerprint {
    param([AllowEmptyString()][string]$Locator)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($(if ($null -eq $Locator) { '' } else { $Locator }))
        $hash = $sha256.ComputeHash($bytes)
        return 'sha256:' + ([System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant())
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-PlaybackQualityReportPath {
    param(
        [Parameter(Mandatory = $true)][string]$ReportsDir,
        [Parameter(Mandatory = $true)][string]$CaseId
    )

    return Join-Path $ReportsDir (
        $CaseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json')
}

function Invoke-NativeHeadlessHarnessCase {
    param(
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$StreamUrl,
        [Parameter(Mandatory = $true)][string]$SourceLocatorHash,
        [Parameter(Mandatory = $true)][string]$ReportsDir,
        [Parameter(Mandatory = $true)][string]$NativeHelperExe,
        [Parameter(Mandatory = $true)][string]$HeadlessProjectPath,
        [string]$HarnessScriptPath = '',
        [int]$DurationSeconds = 10,
        [int]$PauseSeconds = 0,
        [bool]$ForceSdrOutput = $false
    )

    $harnessArguments = @(
        '--case-id', $CaseId,
        '--stream-url', $StreamUrl,
        '--source-locator-hash', $SourceLocatorHash,
        '--duration-seconds', ([string]$DurationSeconds),
        '--reports-dir', $ReportsDir,
        '--native-helper-exe', $NativeHelperExe
    )
    if ($ForceSdrOutput) {
        $harnessArguments += '--force-sdr-output'
    }
    if ($PauseSeconds -gt 0) {
        $harnessArguments += @('--pause-seconds', ([string]$PauseSeconds))
    }

    if (-not [string]::IsNullOrWhiteSpace($HarnessScriptPath)) {
        $commandOutput = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $HarnessScriptPath @harnessArguments)
        $exitCode = $LASTEXITCODE
        $commandOutput | ForEach-Object { Write-Host $_ }
        return [int]$exitCode
    }

    $commandOutput = @(& dotnet run --project $HeadlessProjectPath --no-restore -- @harnessArguments)
    $exitCode = $LASTEXITCODE
    $commandOutput | ForEach-Object { Write-Host $_ }
    return [int]$exitCode
}

Export-ModuleMember -Function `
    Get-PlaybackQualitySourceFingerprint, `
    Get-PlaybackQualityReportPath, `
    Invoke-NativeHeadlessHarnessCase
