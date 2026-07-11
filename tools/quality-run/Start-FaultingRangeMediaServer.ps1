param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath,
    [Parameter(Mandatory = $true)]
    [int]$Port,
    [int64]$CutAfterBytes = 2097152,
    [int]$DelayPerChunkMilliseconds = 20,
    [int]$MaxRequests = 8
)

$ErrorActionPreference = 'Stop'
$file = Get-Item -LiteralPath $FilePath
$listener = [System.Net.Sockets.TcpListener]::new(
    [System.Net.IPAddress]::Loopback,
    $Port)
$listener.Start()
Write-Output ("faultServerReady port={0} length={1}" -f $Port, $file.Length)

try {
    for ($requestNumber = 1; $requestNumber -le $MaxRequests; $requestNumber++) {
        $client = $listener.AcceptTcpClient()
        try {
            $stream = $client.GetStream()
            $headerBytes = [System.Collections.Generic.List[byte]]::new()
            $window = [System.Collections.Generic.Queue[byte]]::new()
            while ($headerBytes.Count -lt 65536) {
                $value = $stream.ReadByte()
                if ($value -lt 0) {
                    break
                }

                $byte = [byte]$value
                $headerBytes.Add($byte)
                $window.Enqueue($byte)
                while ($window.Count -gt 4) {
                    [void]$window.Dequeue()
                }

                if ($window.Count -eq 4) {
                    $tail = $window.ToArray()
                    if ($tail[0] -eq 13 -and $tail[1] -eq 10 -and $tail[2] -eq 13 -and $tail[3] -eq 10) {
                        break
                    }
                }
            }

            $headers = [System.Text.Encoding]::ASCII.GetString($headerBytes.ToArray())
            $rangeStart = [int64]0
            $rangeMatch = [regex]::Match($headers, '(?im)^Range:\s*bytes=(\d+)-')
            if ($rangeMatch.Success) {
                $rangeStart = [int64]::Parse($rangeMatch.Groups[1].Value)
            }

            Write-Output ("request={0} rangeStart={1}" -f $requestNumber, $rangeStart)
            if ($rangeStart -lt 0 -or $rangeStart -ge $file.Length) {
                $response = "HTTP/1.1 416 Range Not Satisfiable`r`nConnection: close`r`nContent-Length: 0`r`n`r`n"
                $responseBytes = [System.Text.Encoding]::ASCII.GetBytes($response)
                $stream.Write($responseBytes, 0, $responseBytes.Length)
                continue
            }

            $remaining = $file.Length - $rangeStart
            $status = if ($rangeMatch.Success) { 'HTTP/1.1 206 Partial Content' } else { 'HTTP/1.1 200 OK' }
            $contentRange = if ($rangeMatch.Success) {
                "Content-Range: bytes $rangeStart-$($file.Length - 1)/$($file.Length)`r`n"
            }
            else {
                ''
            }
            $response =
                "$status`r`n" +
                "Content-Type: video/mp4`r`n" +
                "Accept-Ranges: bytes`r`n" +
                $contentRange +
                "Content-Length: $remaining`r`n" +
                "Connection: close`r`n`r`n"
            $responseBytes = [System.Text.Encoding]::ASCII.GetBytes($response)
            $stream.Write($responseBytes, 0, $responseBytes.Length)

            $forceReset = $requestNumber -eq 1
            $sent = [int64]0
            $buffer = New-Object byte[] 32768
            $input = [System.IO.File]::OpenRead($file.FullName)
            try {
                [void]$input.Seek($rangeStart, [System.IO.SeekOrigin]::Begin)
                while ($sent -lt $remaining) {
                    $readLength = [int][Math]::Min($buffer.Length, $remaining - $sent)
                    if ($forceReset) {
                        $readLength = [int][Math]::Min($readLength, $CutAfterBytes - $sent)
                        if ($readLength -le 0) {
                            break
                        }
                    }

                    $read = $input.Read($buffer, 0, $readLength)
                    if ($read -le 0) {
                        break
                    }

                    $stream.Write($buffer, 0, $read)
                    $stream.Flush()
                    $sent += $read
                    if ($DelayPerChunkMilliseconds -gt 0) {
                        Start-Sleep -Milliseconds $DelayPerChunkMilliseconds
                    }
                }
            }
            catch [System.IO.IOException] {
                Write-Output ("request={0} clientClosed=1 bytesSent={1}" -f $requestNumber, $sent)
            }
            finally {
                $input.Dispose()
            }

            Write-Output ("request={0} bytesSent={1} forcedReset={2}" -f $requestNumber, $sent, $forceReset)
            if ($forceReset) {
                $client.Client.LingerState = [System.Net.Sockets.LingerOption]::new($true, 0)
            }
        }
        catch [System.IO.IOException] {
            Write-Output ("request={0} connectionError=1" -f $requestNumber)
        }
        finally {
            $client.Dispose()
        }
    }
}
finally {
    $listener.Stop()
}
