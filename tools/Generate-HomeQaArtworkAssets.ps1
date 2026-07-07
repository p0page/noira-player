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

function Draw-AtmosphereTexture {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Accent,
        [int]$Seed
    )

    $random = [System.Random]::new($Seed * 97)
    $speckBrush = New-Brush (Color-WithAlpha $Accent 32)
    $mistPen = New-Pen (Color-WithAlpha $Accent 28) 1

    for ($i = 0; $i -lt 42; $i++) {
        $x = [float]$random.Next(0, $Width)
        $y = [float]$random.Next(0, $Height)
        $size = [float]$random.Next(1, [Math]::Max(2, [int]($Width * 0.012)))
        $Graphics.FillEllipse($speckBrush, $x, $y, $size, $size)
    }

    for ($i = 0; $i -lt 10; $i++) {
        $y = [float]($Height * (0.16 + ($i * 0.055)))
        $x1 = [float]$random.Next(-[int]($Width * 0.12), [int]($Width * 0.28))
        $x2 = [float]($Width * (0.74 + ($random.NextDouble() * 0.24)))
        $Graphics.DrawLine($mistPen, $x1, $y, $x2, $y + [float]$random.Next(-10, 12))
    }

    $speckBrush.Dispose()
    $mistPen.Dispose()
}

function Draw-PosterSilhouette {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Accent,
        [int]$Seed
    )

    $centerX = [float]($Width * (0.44 + (($Seed % 5) * 0.028)))
    $headY = [float]($Height * 0.18)
    $headW = [float]($Width * 0.22)
    $headH = [float]($Height * 0.17)
    $shoulderY = [float]($Height * 0.35)
    $shoulderW = [float]($Width * 0.58)
    $shoulderH = [float]($Height * 0.36)

    $shadowBrush = New-Brush (Color-WithAlpha $canvas 238)
    $Graphics.FillEllipse(
        $shadowBrush,
        $centerX - ($headW * 0.50),
        $headY,
        $headW,
        $headH)

    $coatRect = [System.Drawing.RectangleF]::new(
        $centerX - ($shoulderW * 0.50),
        $shoulderY,
        $shoulderW,
        $shoulderH)
    Draw-RoundedRect $Graphics $shadowBrush $coatRect ([Math]::Max(18, $Width * 0.08))
    $shadowBrush.Dispose()

    $rimPen = New-Pen (Color-WithAlpha $Accent 120) ([Math]::Max(2, $Width * 0.012))
    $Graphics.DrawArc(
        $rimPen,
        $centerX - ($headW * 0.50),
        $headY,
        $headW,
        $headH,
        285,
        120)
    $Graphics.DrawLine(
        $rimPen,
        $centerX + ($shoulderW * 0.16),
        $shoulderY + ($shoulderH * 0.10),
        $centerX + ($shoulderW * 0.30),
        $shoulderY + ($shoulderH * 0.78))
    $rimPen.Dispose()
}

function Draw-CinematicPosterScene {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Accent,
        [System.Drawing.Color]$Deep,
        [int]$Seed
    )

    Draw-SoftLight $Graphics $Width $Height $Accent $Seed
    Draw-AtmosphereTexture $Graphics $Width $Height $Accent $Seed

    $sceneBrush = New-Brush (Color-WithAlpha $Deep 160)
    $midBrush = New-Brush (Color-WithAlpha $surface 190)
    $accentBrush = New-Brush (Color-WithAlpha $Accent 90)

    switch ($Seed % 4) {
        0 {
            $mountainPoints = [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(0, [float]($Height * 0.46)),
                [System.Drawing.PointF]::new([float]($Width * 0.18), [float]($Height * 0.26)),
                [System.Drawing.PointF]::new([float]($Width * 0.38), [float]($Height * 0.50)),
                [System.Drawing.PointF]::new([float]($Width * 0.60), [float]($Height * 0.31)),
                [System.Drawing.PointF]::new($Width, [float]($Height * 0.54)),
                [System.Drawing.PointF]::new($Width, [float]($Height * 0.72)),
                [System.Drawing.PointF]::new(0, [float]($Height * 0.72)))
            $Graphics.FillPolygon($sceneBrush, $mountainPoints)
        }
        1 {
            for ($i = 0; $i -lt 7; $i++) {
                $x = [float]($Width * (0.05 + ($i * 0.13)))
                $buildingHeight = [float]($Height * (0.18 + (($i % 3) * 0.08)))
                $Graphics.FillRectangle($sceneBrush, $x, [float]($Height * 0.48 - $buildingHeight), [float]($Width * 0.08), $buildingHeight)
                $Graphics.FillRectangle($accentBrush, $x + 8, [float]($Height * 0.50 - $buildingHeight), [float]($Width * 0.018), [float]($buildingHeight * 0.70))
            }
        }
        2 {
            $corridorPen = New-Pen (Color-WithAlpha $Accent 90) ([Math]::Max(1, $Width * 0.006))
            for ($i = 0; $i -lt 7; $i++) {
                $offset = [float]($i * $Width * 0.075)
                $Graphics.DrawLine($corridorPen, $offset, [float]($Height * 0.18), [float]($Width * 0.50), [float]($Height * 0.58))
                $Graphics.DrawLine($corridorPen, $Width - $offset, [float]($Height * 0.18), [float]($Width * 0.50), [float]($Height * 0.58))
            }
            $corridorPen.Dispose()
        }
        default {
            $Graphics.FillEllipse($midBrush, [float]($Width * 0.10), [float]($Height * 0.18), [float]($Width * 0.80), [float]($Height * 0.28))
            $Graphics.FillRectangle($sceneBrush, 0, [float]($Height * 0.48), $Width, [float]($Height * 0.18))
        }
    }

    Draw-PosterSilhouette $Graphics $Width $Height $Accent $Seed

    $sceneBrush.Dispose()
    $midBrush.Dispose()
    $accentBrush.Dispose()
}

function Draw-FilmBillingBlock {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$TextColor,
        [System.Drawing.Color]$Accent,
        [int]$Seed
    )

    $billingFont = [System.Drawing.Font]::new("Segoe UI", [float]($Width * 0.024), [System.Drawing.FontStyle]::Regular)
    $mutedBrush = New-Brush (Color-WithAlpha $TextColor 174)
    $accentBrush = New-Brush (Color-WithAlpha $Accent 190)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Near

    $Graphics.FillRectangle(
        $accentBrush,
        [float]($Width * 0.31),
        [float]($Height * 0.912),
        [float]($Width * 0.38),
        [float]([Math]::Max(2, $Height * 0.006)))

    $billing = if (($Seed % 2) -eq 0) { "UHD  HDR  5.1  SUBTITLES  2026" } else { "ORIGINAL FEATURE  RESTORED SOUND  2026" }
    $billingRect = [System.Drawing.RectangleF]::new(
        [float]($Width * 0.08),
        [float]($Height * 0.935),
        [float]($Width * 0.84),
        [float]($Height * 0.045))
    $Graphics.DrawString($billing, $billingFont, $mutedBrush, $billingRect, $format)

    $format.Dispose()
    $billingFont.Dispose()
    $mutedBrush.Dispose()
    $accentBrush.Dispose()
}

function Draw-CinematicWideScene {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Accent,
        [System.Drawing.Color]$Deep,
        [int]$Seed
    )

    Draw-SoftLight $Graphics $Width $Height $Accent ($Seed + 9)
    Draw-AtmosphereTexture $Graphics $Width $Height $Accent ($Seed + 21)

    $groundBrush = New-Brush (Color-WithAlpha $Deep 168)
    $sceneBrush = New-Brush (Color-WithAlpha $surface 218)
    $accentBrush = New-Brush (Color-WithAlpha $Accent 120)

    switch ($Seed % 4) {
        0 {
            $Graphics.FillRectangle($groundBrush, 0, [float]($Height * 0.60), $Width, [float]($Height * 0.40))
            for ($i = 0; $i -lt 9; $i++) {
                $x = [float]($Width * (0.07 + ($i * 0.095)))
                $h = [float]($Height * (0.18 + (($i % 4) * 0.06)))
                $Graphics.FillRectangle($sceneBrush, $x, [float]($Height * 0.60 - $h), [float]($Width * 0.055), $h)
                if (($i % 2) -eq 0) {
                    $Graphics.FillRectangle($accentBrush, $x + 6, [float]($Height * 0.58 - $h), [float]($Width * 0.012), [float]($h * 0.70))
                }
            }
        }
        1 {
            $ridgePoints = [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(0, [float]($Height * 0.54)),
                [System.Drawing.PointF]::new([float]($Width * 0.22), [float]($Height * 0.30)),
                [System.Drawing.PointF]::new([float]($Width * 0.46), [float]($Height * 0.58)),
                [System.Drawing.PointF]::new([float]($Width * 0.68), [float]($Height * 0.34)),
                [System.Drawing.PointF]::new($Width, [float]($Height * 0.55)),
                [System.Drawing.PointF]::new($Width, $Height),
                [System.Drawing.PointF]::new(0, $Height))
            $Graphics.FillPolygon($groundBrush, $ridgePoints)
        }
        2 {
            $corridorPen = New-Pen (Color-WithAlpha $Accent 88) ([Math]::Max(1, $Width * 0.004))
            $Graphics.FillRectangle($groundBrush, 0, [float]($Height * 0.54), $Width, [float]($Height * 0.46))
            for ($i = 0; $i -lt 10; $i++) {
                $x = [float]($i * $Width * 0.085)
                $Graphics.DrawLine($corridorPen, $x, 0, [float]($Width * 0.58), [float]($Height * 0.64))
                $Graphics.DrawLine($corridorPen, $Width - $x, 0, [float]($Width * 0.58), [float]($Height * 0.64))
            }
            $corridorPen.Dispose()
        }
        default {
            $Graphics.FillRectangle($groundBrush, 0, [float]($Height * 0.56), $Width, [float]($Height * 0.44))
            $Graphics.FillEllipse($sceneBrush, [float]($Width * 0.46), [float]($Height * 0.10), [float]($Width * 0.34), [float]($Height * 0.62))
        }
    }

    $figureBrush = New-Brush (Color-WithAlpha $canvas 230)
    $rimPen = New-Pen (Color-WithAlpha $Accent 115) ([Math]::Max(2, $Width * 0.006))
    $figureX = [float]($Width * (0.64 + (($Seed % 3) * 0.035)))
    $Graphics.FillEllipse($figureBrush, $figureX, [float]($Height * 0.32), [float]($Width * 0.075), [float]($Height * 0.13))
    Draw-RoundedRect $Graphics $figureBrush ([System.Drawing.RectangleF]::new($figureX - $Width * 0.018, [float]($Height * 0.44), [float]($Width * 0.12), [float]($Height * 0.30))) 12
    $Graphics.DrawLine($rimPen, $figureX + $Width * 0.08, [float]($Height * 0.36), $figureX + $Width * 0.11, [float]($Height * 0.70))
    $figureBrush.Dispose()
    $rimPen.Dispose()
    $groundBrush.Dispose()
    $sceneBrush.Dispose()
    $accentBrush.Dispose()
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
    $titleBrush = New-Brush $TitleColor
    $mutedBrush = New-Brush (Color-WithAlpha $muted 205)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Near

    $titleRect = [System.Drawing.RectangleF]::new(
        [float]($Width * 0.08),
        [float]($Height * 0.665),
        [float]($Width * 0.84),
        [float]($Height * 0.16))
    $Graphics.DrawString($Title, $titleFont, $titleBrush, $titleRect, $format)

    $meta = if (($Seed % 3) -eq 0) { "A PRIVATE LIBRARY FEATURE" } elseif (($Seed % 3) -eq 1) { "DIRECTOR'S CUT" } else { "RESTORED EDITION" }
    $metaRect = [System.Drawing.RectangleF]::new(
        [float]($Width * 0.08),
        [float]($Height * 0.835),
        [float]($Width * 0.84),
        [float]($Height * 0.05))
    $Graphics.DrawString($meta, $smallFont, $mutedBrush, $metaRect, $format)

    Draw-FilmBillingBlock $Graphics $Width $Height $TitleColor $Accent $Seed

    $format.Dispose()
    $titleBrush.Dispose()
    $mutedBrush.Dispose()
    $titleFont.Dispose()
    $smallFont.Dispose()
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

    Draw-CinematicPosterScene $graphics $width $height $accent $deep $Seed

    $scrim = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, [int]($height * 0.50), $width, [int]($height * 0.50)),
        [System.Drawing.Color]::FromArgb(0, 0, 0, 0),
        [System.Drawing.Color]::FromArgb(226, 0, 0, 0),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $graphics.FillRectangle($scrim, 0, [int]($height * 0.50), $width, [int]($height * 0.50))
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

    Draw-CinematicWideScene $graphics $width $height $accent $deep $Seed

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
    $graphics.DrawString("Original feature artwork", $metaFont, $mutedBrush, [float]($width * 0.07), [float]($height * 0.80))
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
