[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$PrivateEnvPath
)

$ErrorActionPreference = 'Stop'

function Write-GuardSummary([string]$Result, [string]$Category, [int]$Count) {
    Write-Output ("result=$Result category=$Category count=$Count")
}

function Invoke-GitText(
    [string]$WorkingDirectory,
    [string]$Arguments,
    [AllowEmptyString()][string]$StandardInput = '',
    [int[]]$SuccessExitCodes = @(0)
) {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'git.exe'
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.RedirectStandardInput = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw 'Git process did not start.'
        }
        if ($StandardInput.Length -gt 0) {
            # The legacy Windows StreamWriter may emit a BOM. Sacrifice an empty
            # first line so it can never become part of a repository-relative path.
            $process.StandardInput.Write("`n" + $StandardInput)
        }
        $process.StandardInput.Close()
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $process.StandardError.ReadToEnd() | Out-Null
        $process.WaitForExit()
        if ($SuccessExitCodes -notcontains $process.ExitCode) {
            throw 'Git process failed.'
        }
        return $standardOutput
    }
    finally {
        $process.Dispose()
    }
}

function Read-PrivateValues([string]$Path) {
    $requiredNames = @('NOIRA_EMBY_SERVER', 'NOIRA_EMBY_USER', 'NOIRA_EMBY_PASSWORD')
    $parsed = @{}
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if ($line -match '^\s*\uFEFF?(?:export\s+)?(NOIRA_EMBY_SERVER|NOIRA_EMBY_USER|NOIRA_EMBY_PASSWORD)\s*=\s*(.*)$') {
            $value = $Matches[2].Trim()
            if ($value.Length -ge 2) {
                $first = $value[0]
                $last = $value[$value.Length - 1]
                if (($first -eq '"' -and $last -eq '"') -or ($first -eq "'" -and $last -eq "'")) {
                    $value = $value.Substring(1, $value.Length - 2)
                }
            }
            $parsed[$Matches[1]] = $value
        }
    }

    $values = New-Object System.Collections.Generic.List[string]
    $missingCount = 0
    foreach ($name in $requiredNames) {
        if (-not $parsed.ContainsKey($name) -or [string]::IsNullOrEmpty([string]$parsed[$name])) {
            $missingCount++
            continue
        }
        if (-not $values.Contains([string]$parsed[$name])) {
            $values.Add([string]$parsed[$name])
        }
    }

    return [pscustomobject]@{
        Values = $values.ToArray()
        MissingCount = $missingCount
    }
}

function Test-BytesContain([byte[]]$Bytes, [byte[]]$Needle) {
    if ($Needle.Length -eq 0 -or $Needle.Length -gt $Bytes.Length) {
        return $false
    }
    $limit = $Bytes.Length - $Needle.Length
    for ($offset = 0; $offset -le $limit; $offset++) {
        if ($Bytes[$offset] -ne $Needle[0]) {
            continue
        }
        $matches = $true
        for ($index = 1; $index -lt $Needle.Length; $index++) {
            if ($Bytes[$offset + $index] -ne $Needle[$index]) {
                $matches = $false
                break
            }
        }
        if ($matches) {
            return $true
        }
    }
    return $false
}

function Test-PrivateValueInFile([string]$Path, [string[]]$Values) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    foreach ($value in $Values) {
        foreach ($encoding in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode, [Text.Encoding]::BigEndianUnicode)) {
            if (Test-BytesContain $bytes ($encoding.GetBytes($value))) {
                return $true
            }
        }
    }
    return $false
}

function Test-PrivateValueInText([string]$Text, [string[]]$Values) {
    foreach ($value in $Values) {
        if ($Text.IndexOf($value, [StringComparison]::Ordinal) -ge 0) {
            return $true
        }
    }
    return $false
}

function Get-RelativePath([string]$Root, [string]$Path) {
    $rootUri = New-Object Uri (($Root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar))
    $pathUri = New-Object Uri $Path
    return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace('\', '/')
}

function Find-PrivateFiles(
    [string]$Root,
    [string[]]$RelativePaths,
    [string[]]$Values,
    [ref]$ReadErrorCount
) {
    $findingCount = 0
    foreach ($relativePath in $RelativePaths) {
        if (Test-PrivateValueInText $relativePath $Values) {
            $findingCount++
            continue
        }
        $path = Join-Path $Root ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        try {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw 'Scanned file is unavailable.'
            }
            if (Test-PrivateValueInFile $path $Values) {
                $findingCount++
            }
        }
        catch {
            $ReadErrorCount.Value++
        }
    }
    return $findingCount
}

try {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    }
    else {
        $RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    }

    if ([string]::IsNullOrWhiteSpace($PrivateEnvPath)) {
        $PrivateEnvPath = Join-Path $RepositoryRoot '.private\emby-test.local.env'
    }
    elseif (-not [IO.Path]::IsPathRooted($PrivateEnvPath)) {
        $PrivateEnvPath = Join-Path $RepositoryRoot $PrivateEnvPath
    }

    if (-not (Test-Path -LiteralPath $PrivateEnvPath)) {
        Write-GuardSummary 'skip' 'environment-missing' 3
        exit 0
    }
    if (-not (Test-Path -LiteralPath $PrivateEnvPath -PathType Leaf)) {
        Write-GuardSummary 'fail' 'environment-error' 1
        exit 1
    }

    try {
        $privateData = Read-PrivateValues $PrivateEnvPath
    }
    catch {
        Write-GuardSummary 'fail' 'environment-error' 1
        exit 1
    }

    if ($privateData.Values.Count -eq 0) {
        Write-GuardSummary 'skip' 'environment-missing' $privateData.MissingCount
        exit 0
    }
    if ($privateData.MissingCount -gt 0) {
        Write-GuardSummary 'scan' 'environment-missing' $privateData.MissingCount
    }

    $readErrorCount = 0
    try {
        $trackedOutput = Invoke-GitText $RepositoryRoot 'ls-files -z'
        $trackedPaths = @($trackedOutput -split "`0" | Where-Object { $_.Length -gt 0 })
    }
    catch {
        Write-GuardSummary 'fail' 'git-error' 1
        exit 1
    }
    $trackedCount = Find-PrivateFiles $RepositoryRoot $trackedPaths $privateData.Values ([ref]$readErrorCount)

    $distRoot = Join-Path $RepositoryRoot 'src\NoiraPlayer.Web\dist'
    $distPaths = @()
    if (Test-Path -LiteralPath $distRoot -PathType Container) {
        $distPaths = @(Get-ChildItem -LiteralPath $distRoot -File -Recurse -Force | ForEach-Object {
            Get-RelativePath $RepositoryRoot $_.FullName
        })
    }
    $distCount = Find-PrivateFiles $RepositoryRoot $distPaths $privateData.Values ([ref]$readErrorCount)

    $qaRoot = Join-Path $RepositoryRoot 'docs\qa'
    $qaPaths = @()
    if (Test-Path -LiteralPath $qaRoot -PathType Container) {
        $qaCandidates = @(Get-ChildItem -LiteralPath $qaRoot -File -Recurse -Force | ForEach-Object {
            Get-RelativePath $RepositoryRoot $_.FullName
        } | Where-Object {
            $_ -notmatch '(^|/)private(/|$)'
        })
        if ($qaCandidates.Count -gt 0) {
            try {
                $ignoredOutput = Invoke-GitText `
                    $RepositoryRoot `
                    'check-ignore --stdin' `
                    (($qaCandidates -join "`n") + "`n") `
                    @(0, 1)
                $ignoredPaths = @($ignoredOutput -split "`r?`n" | Where-Object { $_.Length -gt 0 })
                $ignoredSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
                foreach ($ignoredPath in $ignoredPaths) {
                    $ignoredSet.Add($ignoredPath.Replace('\', '/')) | Out-Null
                }
                $qaPaths = @($qaCandidates | Where-Object { -not $ignoredSet.Contains($_) })
            }
            catch {
                Write-GuardSummary 'fail' 'git-error' 1
                exit 1
            }
        }
    }
    $qaCount = Find-PrivateFiles $RepositoryRoot $qaPaths $privateData.Values ([ref]$readErrorCount)

    foreach ($category in @(
        [pscustomobject]@{ Name = 'tracked'; Count = $trackedCount },
        [pscustomobject]@{ Name = 'dist'; Count = $distCount },
        [pscustomobject]@{ Name = 'qa'; Count = $qaCount },
        [pscustomobject]@{ Name = 'read-error'; Count = $readErrorCount }
    )) {
        if ($category.Count -gt 0) {
            Write-GuardSummary 'fail' $category.Name $category.Count
        }
    }

    $totalCount = $trackedCount + $distCount + $qaCount + $readErrorCount
    if ($totalCount -gt 0) {
        Write-GuardSummary 'fail' 'total' $totalCount
        exit 1
    }

    Write-GuardSummary 'pass' 'total' 0
    exit 0
}
catch {
    Write-GuardSummary 'fail' 'guard-error' 1
    exit 1
}
