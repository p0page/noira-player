param(
    [string]$AssetsPath = "src\NextGenEmby.App\Assets\QaHome"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$canvas = [System.Drawing.ColorTranslator]::FromHtml("#05070A")
$surface = [System.Drawing.ColorTranslator]::FromHtml("#10161C")
$raised = [System.Drawing.ColorTranslator]::FromHtml("#202832")
$hairline = [System.Drawing.ColorTranslator]::FromHtml("#2E3944")
$text = [System.Drawing.ColorTranslator]::FromHtml("#EEF3F6")
$muted = [System.Drawing.ColorTranslator]::FromHtml("#A9B3BA")

$posterPalettes = @(
    @("#17212A", "#6E8796", "#D6DADF", "#0B1015"),
    @("#1E1817", "#8C7661", "#E2D4C4", "#0A0808"),
    @("#111D1A", "#7A8B6D", "#D8E0D0", "#060A09"),
    @("#1C1720", "#8A6F7C", "#E3D3DE", "#09070A"),
    @("#121A24", "#5F7D8A", "#DAE5EA", "#06080B"),
    @("#201D15", "#9A8A68", "#E6DDC8", "#090806")
)

$posterTitles = @(
    "AURORA PROTOCOL",
    "MIDNIGHT SIGNAL",
    "HARBOR RUN",
    "AFTERIMAGE",
    "QUIET ORBIT",
    "SUMMIT LINE",
    "NORTHLINE",
    "ROOM TONE",
    "HORIZON HOUSE",
    "SIGNAL ROOM",
    "ROOM TONE S2",
    "OCEAN ARCHIVE",
    "CITY AT NIGHT",
    "SOUND ROOM"
)

function New-Brush {
    param([System.Drawing.Color]$Color)
    return [System.Drawing.SolidBrush]::new($Color)
}

function New-Pen {
    param(
        [System.Drawing.Color]$Color,
        [float]$Width = 1
    )
    return [System.Drawing.Pen]::new($Color, $Width)
}

function Color-WithAlpha {
    param(
        [System.Drawing.Color]$Color,
        [int]$Alpha
    )
    return [System.Drawing.Color]::FromArgb($Alpha, $Color.R, $Color.G, $Color.B)
}

function Convert-Color {
    param([string]$Value)
    return [System.Drawing.ColorTranslator]::FromHtml($Value)
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

function Draw-SoftLight {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Accent,
        [int]$Seed
    )

    $lightBrush = New-Brush (Color-WithAlpha $Accent 56)
    $Graphics.FillEllipse(
        $lightBrush,
        [float]($Width * (-0.16 + (($Seed % 3) * 0.12))),
        [float](-$Height * 0.10),
        [float]($Width * 0.82),
        [float]($Height * 0.48))
    $Graphics.FillEllipse(
        $lightBrush,
        [float]($Width * 0.48),
        [float]($Height * (0.16 + (($Seed % 4) * 0.04))),
        [float]($Width * 0.62),
        [float]($Height * 0.44))
    $lightBrush.Dispose()
}

function Draw-CinematicSubject {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Accent,
        [System.Drawing.Color]$Skin,
        [int]$Seed
    )

    $subjectX = [float]($Width * (0.36 + (($Seed % 4) * 0.035)))
    $headY = [float]($Height * 0.17)
    $headW = [float]($Width * (0.28 + (($Seed % 2) * 0.04)))
    $headH = [float]($Height * 0.22)
    $bodyY = [float]($Height * 0.37)
    $bodyW = [float]($Width * 0.52)
    $bodyH = [float]($Height * 0.36)

    $shadowBrush = New-Brush (Color-WithAlpha $canvas 190)
    $Graphics.FillEllipse($shadowBrush, $subjectX - $Width * 0.05, $headY + $Height * 0.02, $headW, $headH)
    $shadowBrush.Dispose()

    $skinBrush = New-Brush (Color-WithAlpha $Skin 230)
    $Graphics.FillEllipse($skinBrush, $subjectX, $headY, $headW, $headH)
    $skinBrush.Dispose()

    $hairBrush = New-Brush (Color-WithAlpha $canvas 220)
    $Graphics.FillPie($hairBrush, $subjectX - 4, $headY - 4, $headW + 8, $headH * 0.72, 188, 198)
    $hairBrush.Dispose()

    $coatBrush = New-Brush (Color-WithAlpha $raised 238)
    $bodyRect = [System.Drawing.RectangleF]::new($subjectX - $Width * 0.12, $bodyY, $bodyW, $bodyH)
    Draw-RoundedRect $Graphics $coatBrush $bodyRect ([Math]::Max(8, $Width * 0.05))
    $coatBrush.Dispose()

    $accentBrush = New-Brush (Color-WithAlpha $Accent 176)
    $Graphics.FillRectangle(
        $accentBrush,
        [float]($subjectX + $bodyW * 0.34),
        [float]($bodyY + $bodyH * 0.12),
        [float]($Width * 0.055),
        [float]($bodyH * 0.72))
    $accentBrush.Dispose()

    $eyePen = New-Pen (Color-WithAlpha $canvas 200) ([Math]::Max(1, $Width * 0.006))
    $Graphics.DrawLine($eyePen, $subjectX + $headW * 0.30, $headY + $headH * 0.48, $subjectX + $headW * 0.42, $headY + $headH * 0.46)
    $Graphics.DrawLine($eyePen, $subjectX + $headW * 0.58, $headY + $headH * 0.46, $subjectX + $headW * 0.70, $headY + $headH * 0.48)
    $eyePen.Dispose()
}

function Draw-PosterTypography {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [string]$Title,
        [System.Drawing.Color]$TitleColor,
        [System.Drawing.Color]$Accent,
        [int]$Seed
    )

    $titleFont = [System.Drawing.Font]::new("Segoe UI Semibold", [float]($Width * 0.068), [System.Drawing.FontStyle]::Bold)
    $smallFont = [System.Drawing.Font]::new("Segoe UI", [float]($Width * 0.036), [System.Drawing.FontStyle]::Regular)
    $billingFont = [System.Drawing.Font]::new("Segoe UI", [float]($Width * 0.026), [System.Drawing.FontStyle]::Regular)
    $titleBrush = New-Brush $TitleColor
    $mutedBrush = New-Brush (Color-WithAlpha $muted 205)
    $accentBrush = New-Brush (Color-WithAlpha $Accent 190)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Near

    $titleRect = [System.Drawing.RectangleF]::new(
        [float]($Width * 0.08),
        [float]($Height * 0.665),
        [float]($Width * 0.84),
        [float]($Height * 0.17))
    $Graphics.DrawString($Title, $titleFont, $titleBrush, $titleRect, $format)

    $meta = if (($Seed % 3) -eq 0) { "A PRIVATE LIBRARY FEATURE" } elseif (($Seed % 3) -eq 1) { "DIRECTOR'S CUT" } else { "RESTORED EDITION" }
    $metaRect = [System.Drawing.RectangleF]::new(
        [float]($Width * 0.08),
        [float]($Height * 0.845),
        [float]($Width * 0.84),
        [float]($Height * 0.05))
    $Graphics.DrawString($meta, $smallFont, $mutedBrush, $metaRect, $format)

    $Graphics.FillRectangle(
        $accentBrush,
        [float]($Width * 0.32),
        [float]($Height * 0.925),
        [float]($Width * 0.36),
        [float]([Math]::Max(2, $Height * 0.006)))

    $billingRect = [System.Drawing.RectangleF]::new(
        [float]($Width * 0.10),
        [float]($Height * 0.94),
        [float]($Width * 0.80),
        [float]($Height * 0.04))
    $Graphics.DrawString("HD 5.1  SUBTITLES  2026", $billingFont, $mutedBrush, $billingRect, $format)

    $format.Dispose()
    $titleBrush.Dispose()
    $mutedBrush.Dispose()
    $accentBrush.Dispose()
    $titleFont.Dispose()
    $smallFont.Dispose()
    $billingFont.Dispose()
}

function New-QaPosterArtwork {
    param(
        [int]$Seed,
        [string]$Title,
        [string]$Path
    )

    $width = 420
    $height = 630
    $palette = $posterPalettes[($Seed - 1) % $posterPalettes.Count]
    $bg = Convert-Color $palette[0]
    $accent = Convert-Color $palette[1]
    $titleColor = Convert-Color $palette[2]
    $deep = Convert-Color $palette[3]
    $skin = Convert-Color @( "#B98B70", "#C0A086", "#9F7564", "#D0B095" )[($Seed - 1) % 4]

    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Point]::new(0, 0),
        [System.Drawing.Point]::new($width, $height),
        $bg,
        $deep)
    $graphics.FillRectangle($background, 0, 0, $width, $height)
    $background.Dispose()

    Draw-SoftLight $graphics $width $height $accent $Seed
    Draw-CinematicSubject $graphics $width $height $accent $skin $Seed

    $scrim = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, [int]($height * 0.56), $width, [int]($height * 0.44)),
        [System.Drawing.Color]::FromArgb(0, 0, 0, 0),
        [System.Drawing.Color]::FromArgb(214, 0, 0, 0),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $graphics.FillRectangle($scrim, 0, [int]($height * 0.56), $width, [int]($height * 0.44))
    $scrim.Dispose()

    Draw-PosterTypography $graphics $width $height $Title $titleColor $accent $Seed

    $directory = Split-Path -Parent $Path
    if (!(Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function New-QaWideArtwork {
    param(
        [int]$Seed,
        [string]$Title,
        [string]$Path
    )

    $width = 640
    $height = 360
    $palette = $posterPalettes[($Seed - 1) % $posterPalettes.Count]
    $bg = Convert-Color $palette[0]
    $accent = Convert-Color $palette[1]
    $titleColor = Convert-Color $palette[2]
    $deep = Convert-Color $palette[3]

    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Point]::new(0, 0),
        [System.Drawing.Point]::new($width, $height),
        $bg,
        $deep)
    $graphics.FillRectangle($background, 0, 0, $width, $height)
    $background.Dispose()

    Draw-SoftLight $graphics $width $height $accent ($Seed + 9)

    $horizonBrush = New-Brush (Color-WithAlpha $surface 230)
    $graphics.FillRectangle($horizonBrush, 0, [int]($height * 0.58), $width, [int]($height * 0.42))
    $horizonBrush.Dispose()

    $linePen = New-Pen (Color-WithAlpha $hairline 230) ([Math]::Max(2, $width * 0.006))
    $graphics.DrawLine($linePen, 0, [float]($height * 0.58), $width, [float]($height * 0.58))
    $linePen.Dispose()

    $figureBrush = New-Brush (Color-WithAlpha $raised 238)
    Draw-RoundedRect $graphics $figureBrush ([System.Drawing.RectangleF]::new([float]($width * 0.66), [float]($height * 0.24), [float]($width * 0.16), [float]($height * 0.48))) 14
    Draw-RoundedRect $graphics $figureBrush ([System.Drawing.RectangleF]::new([float]($width * 0.22), [float]($height * 0.30), [float]($width * 0.12), [float]($height * 0.38))) 10
    $figureBrush.Dispose()

    $accentBrush = New-Brush (Color-WithAlpha $accent 180)
    $graphics.FillRectangle($accentBrush, [float]($width * 0.72), [float]($height * 0.34), [float]($width * 0.035), [float]($height * 0.30))
    $accentBrush.Dispose()

    $scrim = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, 0, $width, $height),
        [System.Drawing.Color]::FromArgb(0, 0, 0, 0),
        [System.Drawing.Color]::FromArgb(198, 0, 0, 0),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $graphics.FillRectangle($scrim, 0, 0, $width, $height)
    $scrim.Dispose()

    $titleFont = [System.Drawing.Font]::new("Segoe UI Semibold", [float]($height * 0.092), [System.Drawing.FontStyle]::Bold)
    $metaFont = [System.Drawing.Font]::new("Segoe UI", [float]($height * 0.045), [System.Drawing.FontStyle]::Regular)
    $titleBrush = New-Brush $titleColor
    $mutedBrush = New-Brush (Color-WithAlpha $muted 205)
    $graphics.DrawString($Title, $titleFont, $titleBrush, [float]($width * 0.07), [float]($height * 0.68))
    $graphics.DrawString("Watch-ready wide artwork", $metaFont, $mutedBrush, [float]($width * 0.07), [float]($height * 0.80))
    $titleBrush.Dispose()
    $mutedBrush.Dispose()
    $titleFont.Dispose()
    $metaFont.Dispose()

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
    $title = $posterTitles[$i - 1]
    New-QaPosterArtwork -Seed $i -Title $title -Path (Join-Path $AssetsPath "qa-poster-$suffix.png")
    New-QaWideArtwork -Seed $i -Title $title -Path (Join-Path $AssetsPath "qa-wide-$suffix.png")
}

Write-Host "Generated fictional movie-like QA artwork assets in $AssetsPath"
