param(
    [string]$AssetsPath = "src\NextGenEmby.App\Assets\QaHome"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$palette = @(
    [System.Drawing.ColorTranslator]::FromHtml("#3BD5FF"),
    [System.Drawing.ColorTranslator]::FromHtml("#61D47C"),
    [System.Drawing.ColorTranslator]::FromHtml("#E0B86A"),
    [System.Drawing.ColorTranslator]::FromHtml("#7FA7C7"),
    [System.Drawing.ColorTranslator]::FromHtml("#FF6B6B")
)
$canvas = [System.Drawing.ColorTranslator]::FromHtml("#050607")
$surface = [System.Drawing.ColorTranslator]::FromHtml("#101418")
$raised = [System.Drawing.ColorTranslator]::FromHtml("#1A2027")
$hairline = [System.Drawing.ColorTranslator]::FromHtml("#303842")

function New-Brush {
    param([System.Drawing.Color]$Color)
    return [System.Drawing.SolidBrush]::new($Color)
}

function Color-WithAlpha {
    param(
        [System.Drawing.Color]$Color,
        [int]$Alpha
    )
    return [System.Drawing.Color]::FromArgb($Alpha, $Color.R, $Color.G, $Color.B)
}

function Draw-RoundedRect {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Brush]$Brush,
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    $Graphics.FillPath($Brush, $path)
    $path.Dispose()
}

function New-QaArtwork {
    param(
        [int]$Width,
        [int]$Height,
        [int]$Seed,
        [string]$Path
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear($canvas)

    $accent = $palette[$Seed % $palette.Count]
    $accent2 = $palette[($Seed + 2) % $palette.Count]
    $accent3 = $palette[($Seed + 4) % $palette.Count]

    $bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Point]::new(0, 0),
        [System.Drawing.Point]::new($Width, $Height),
        $raised,
        $canvas)
    $graphics.FillRectangle($bgBrush, 0, 0, $Width, $Height)
    $bgBrush.Dispose()

    $horizon = [Math]::Round($Height * (0.42 + (($Seed % 4) * 0.06)))
    $groundBrush = New-Brush (Color-WithAlpha $surface 235)
    $graphics.FillRectangle($groundBrush, 0, $horizon, $Width, $Height - $horizon)
    $groundBrush.Dispose()

    $washBrush = New-Brush (Color-WithAlpha $accent 72)
    $graphics.FillEllipse($washBrush, -($Width * 0.22), -($Height * 0.18), $Width * 0.75, $Height * 0.55)
    $graphics.FillEllipse($washBrush, $Width * 0.56, $Height * 0.10, $Width * 0.56, $Height * 0.46)
    $washBrush.Dispose()

    $frameBrush = New-Brush (Color-WithAlpha $hairline 230)
    for ($i = 0; $i -lt 4; $i++) {
        $x = [float]($Width * (0.13 + $i * 0.17))
        $y = [float]($Height * (0.24 + (($Seed + $i) % 3) * 0.08))
        $w = [float]($Width * (0.12 + (($Seed + $i) % 2) * 0.05))
        $h = [float]($Height * (0.24 + (($Seed + $i) % 4) * 0.04))
        Draw-RoundedRect $graphics $frameBrush ([System.Drawing.RectangleF]::new($x, $y, $w, $h)) ([Math]::Max(3, $Width * 0.018))
    }
    $frameBrush.Dispose()

    $subjectBrush = New-Brush (Color-WithAlpha $accent2 218)
    $subjectX = [float]($Width * (0.45 + (($Seed % 3) * 0.08)))
    $subjectY = [float]($Height * 0.24)
    $subjectW = [float]($Width * 0.20)
    $subjectH = [float]($Height * 0.42)
    Draw-RoundedRect $graphics $subjectBrush ([System.Drawing.RectangleF]::new($subjectX, $subjectY, $subjectW, $subjectH)) ([Math]::Max(4, $Width * 0.02))
    $subjectBrush.Dispose()

    $linePen = [System.Drawing.Pen]::new((Color-WithAlpha $accent3 210), [Math]::Max(2, $Width * 0.012))
    $graphics.DrawLine($linePen, [float]($Width * 0.08), [float]($Height * 0.78), [float]($Width * 0.92), [float]($Height * 0.78))
    $linePen.Dispose()

    $scrimBrush = New-Brush ([System.Drawing.Color]::FromArgb(24, 0, 0, 0))
    $graphics.FillRectangle($scrimBrush, 0, 0, $Width, $Height)
    $scrimBrush.Dispose()

    $directory = Split-Path -Parent $Path
    if (!(Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

if (!(Test-Path -LiteralPath $AssetsPath)) {
    New-Item -ItemType Directory -Path $AssetsPath | Out-Null
}

for ($i = 1; $i -le 14; $i++) {
    $suffix = "{0:D2}" -f $i
    New-QaArtwork -Width 420 -Height 630 -Seed $i -Path (Join-Path $AssetsPath "qa-poster-$suffix.png")
    New-QaArtwork -Width 640 -Height 360 -Seed ($i + 20) -Path (Join-Path $AssetsPath "qa-wide-$suffix.png")
}

Write-Host "Generated Home QA artwork assets in $AssetsPath"
