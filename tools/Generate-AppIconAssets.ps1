param(
    [string]$AssetsPath = "src\NextGenEmby.App\Assets"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function New-IconColor {
    param(
        [int]$R,
        [int]$G,
        [int]$B,
        [int]$A = 255
    )

    return [System.Drawing.Color]::FromArgb($A, $R, $G, $B)
}

function New-RoundedRectPath {
    param(
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )

    $diameter = [Math]::Max(0.1, $Radius * 2)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill-RoundedRect {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Brush]$Brush,
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )

    $path = New-RoundedRectPath -Rect $Rect -Radius $Radius
    try {
        $Graphics.FillPath($Brush, $path)
    }
    finally {
        $path.Dispose()
    }
}

function Draw-RoundedRect {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Pen]$Pen,
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )

    $path = New-RoundedRectPath -Rect $Rect -Radius $Radius
    try {
        $Graphics.DrawPath($Pen, $path)
    }
    finally {
        $path.Dispose()
    }
}

function Set-CanvasQuality {
    param([System.Drawing.Graphics]$Graphics)

    $Graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $Graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $Graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $Graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
}

function Draw-MatteSlatMark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect
    )

    $s = [Math]::Min($Rect.Width, $Rect.Height)
    $x = $Rect.X + (($Rect.Width - $s) / 2)
    $y = $Rect.Y + (($Rect.Height - $s) / 2)
    $radius = $s * 0.065

    $shadow = [System.Drawing.SolidBrush]::new((New-IconColor 0 0 0 72))
    $cardBack = [System.Drawing.SolidBrush]::new((New-IconColor 13 18 23 250))
    $cardMid = [System.Drawing.SolidBrush]::new((New-IconColor 20 26 33 252))
    $cardFront = [System.Drawing.SolidBrush]::new((New-IconColor 26 32 39 255))
    $cardInset = [System.Drawing.SolidBrush]::new((New-IconColor 16 20 25 255))
    $softLine = [System.Drawing.Pen]::new((New-IconColor 48 56 66 210), [Math]::Max(1.0, $s * 0.012))
    $focusPen = [System.Drawing.Pen]::new((New-IconColor 59 213 255 235), [Math]::Max(1.0, $s * 0.018))
    $green = [System.Drawing.SolidBrush]::new((New-IconColor 97 212 124 255))
    $greenCut = [System.Drawing.SolidBrush]::new((New-IconColor 4 16 7 255))
    $amberPen = [System.Drawing.Pen]::new((New-IconColor 224 184 106 245), [Math]::Max(2.0, $s * 0.045))

    try {
        $amberPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $amberPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $focusPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $focusPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

        $state = $Graphics.Save()
        $Graphics.TranslateTransform($x + ($s * 0.35), $y + ($s * 0.52))
        $Graphics.RotateTransform(-7)
        $backRect = [System.Drawing.RectangleF]::new(-$s * 0.24, -$s * 0.33, $s * 0.50, $s * 0.64)
        $backShadow = [System.Drawing.RectangleF]::new($backRect.X + ($s * 0.018), $backRect.Y + ($s * 0.026), $backRect.Width, $backRect.Height)
        Fill-RoundedRect -Graphics $Graphics -Brush $shadow -Rect $backShadow -Radius $radius
        Fill-RoundedRect -Graphics $Graphics -Brush $cardBack -Rect $backRect -Radius $radius
        Draw-RoundedRect -Graphics $Graphics -Pen $softLine -Rect $backRect -Radius $radius
        $Graphics.Restore($state)

        $state = $Graphics.Save()
        $Graphics.TranslateTransform($x + ($s * 0.52), $y + ($s * 0.46))
        $Graphics.RotateTransform(4)
        $midRect = [System.Drawing.RectangleF]::new(-$s * 0.25, -$s * 0.33, $s * 0.52, $s * 0.64)
        $midShadow = [System.Drawing.RectangleF]::new($midRect.X + ($s * 0.018), $midRect.Y + ($s * 0.026), $midRect.Width, $midRect.Height)
        Fill-RoundedRect -Graphics $Graphics -Brush $shadow -Rect $midShadow -Radius $radius
        Fill-RoundedRect -Graphics $Graphics -Brush $cardMid -Rect $midRect -Radius $radius
        Draw-RoundedRect -Graphics $Graphics -Pen $softLine -Rect $midRect -Radius $radius
        $Graphics.Restore($state)

        $state = $Graphics.Save()
        $Graphics.TranslateTransform($x + ($s * 0.61), $y + ($s * 0.53))
        $Graphics.RotateTransform(-1)
        $frontRect = [System.Drawing.RectangleF]::new(-$s * 0.29, -$s * 0.32, $s * 0.54, $s * 0.60)
        $frontShadow = [System.Drawing.RectangleF]::new($frontRect.X + ($s * 0.018), $frontRect.Y + ($s * 0.030), $frontRect.Width, $frontRect.Height)
        Fill-RoundedRect -Graphics $Graphics -Brush $shadow -Rect $frontShadow -Radius $radius
        Fill-RoundedRect -Graphics $Graphics -Brush $cardFront -Rect $frontRect -Radius $radius
        Draw-RoundedRect -Graphics $Graphics -Pen $softLine -Rect $frontRect -Radius $radius

        $Graphics.DrawLine($focusPen, $frontRect.Left + ($s * 0.05), $frontRect.Top + ($s * 0.035), $frontRect.Right - ($s * 0.06), $frontRect.Top + ($s * 0.035))
        $Graphics.DrawLine($focusPen, $frontRect.Left + ($s * 0.04), $frontRect.Top + ($s * 0.07), $frontRect.Left + ($s * 0.04), $frontRect.Bottom - ($s * 0.07))

        $screenRect = [System.Drawing.RectangleF]::new($frontRect.Left + ($s * 0.12), $frontRect.Top + ($s * 0.14), $frontRect.Width * 0.62, $frontRect.Height * 0.66)
        Fill-RoundedRect -Graphics $Graphics -Brush $cardInset -Rect $screenRect -Radius ($radius * 0.64)

        $playRect = [System.Drawing.RectangleF]::new($frontRect.Left + ($s * 0.20), $frontRect.Top + ($s * 0.25), $s * 0.17, $s * 0.26)
        Fill-RoundedRect -Graphics $Graphics -Brush $green -Rect $playRect -Radius ($radius * 0.56)

        $playCut = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($playRect.Left + ($playRect.Width * 0.38), $playRect.Top + ($playRect.Height * 0.25)),
            [System.Drawing.PointF]::new($playRect.Left + ($playRect.Width * 0.38), $playRect.Bottom - ($playRect.Height * 0.25)),
            [System.Drawing.PointF]::new($playRect.Right - ($playRect.Width * 0.20), $playRect.Top + ($playRect.Height * 0.50))
        )
        $Graphics.FillPolygon($greenCut, $playCut)
        $Graphics.Restore($state)

        $Graphics.DrawLine($amberPen, $x + ($s * 0.28), $y + ($s * 0.82), $x + ($s * 0.76), $y + ($s * 0.82))
    }
    finally {
        $shadow.Dispose()
        $cardBack.Dispose()
        $cardMid.Dispose()
        $cardFront.Dispose()
        $cardInset.Dispose()
        $softLine.Dispose()
        $focusPen.Dispose()
        $green.Dispose()
        $greenCut.Dispose()
        $amberPen.Dispose()
    }
}

function Draw-Title {
    param(
        [System.Drawing.Graphics]$Graphics,
        [string]$Text,
        [System.Drawing.RectangleF]$Rect,
        [float]$FontSize
    )

    $font = [System.Drawing.Font]::new("Segoe UI Variable Display", $FontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new((New-IconColor 244 248 252 255))
    $format = [System.Drawing.StringFormat]::new()
    try {
        $format.Alignment = [System.Drawing.StringAlignment]::Near
        $format.LineAlignment = [System.Drawing.StringAlignment]::Center
        $format.Trimming = [System.Drawing.StringTrimming]::EllipsisCharacter
        $Graphics.DrawString($Text, $font, $brush, $Rect, $format)
    }
    finally {
        $font.Dispose()
        $brush.Dispose()
        $format.Dispose()
    }
}

function New-AppIconBitmap {
    param(
        [int]$Width,
        [int]$Height,
        [string]$Kind
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    Set-CanvasQuality -Graphics $graphics

    $canvas = [System.Drawing.SolidBrush]::new((New-IconColor 5 8 13 255))
    $surface = [System.Drawing.SolidBrush]::new((New-IconColor 12 18 26 255))
    $hairline = [System.Drawing.Pen]::new((New-IconColor 42 59 78 255), [Math]::Max(1.0, [Math]::Min($Width, $Height) * 0.01))

    try {
        $graphics.Clear((New-IconColor 0 0 0 0))
        $tileRect = [System.Drawing.RectangleF]::new(0, 0, $Width, $Height)
        $corner = [Math]::Min($Width, $Height) * 0.12
        Fill-RoundedRect -Graphics $graphics -Brush $canvas -Rect $tileRect -Radius $corner

        $inner = [System.Drawing.RectangleF]::new($Width * 0.035, $Height * 0.04, $Width * 0.93, $Height * 0.90)
        Fill-RoundedRect -Graphics $graphics -Brush $surface -Rect $inner -Radius ([Math]::Min($Width, $Height) * 0.08)
        Draw-RoundedRect -Graphics $graphics -Pen $hairline -Rect $inner -Radius ([Math]::Min($Width, $Height) * 0.08)

        if ($Kind -eq "Wide") {
            $mark = [System.Drawing.RectangleF]::new($Width * 0.06, $Height * 0.10, $Height * 0.80, $Height * 0.80)
            Draw-MatteSlatMark -Graphics $graphics -Rect $mark
            $lanePen = [System.Drawing.Pen]::new((New-IconColor 48 56 66 210), [Math]::Max(1.0, $Height * 0.012))
            $focusPen = [System.Drawing.Pen]::new((New-IconColor 59 213 255 220), [Math]::Max(1.0, $Height * 0.015))
            $cardBrush = [System.Drawing.SolidBrush]::new((New-IconColor 20 26 33 232))
            try {
                for ($i = 0; $i -lt 4; $i++) {
                    $card = [System.Drawing.RectangleF]::new($Width * (0.42 + ($i * 0.105)), $Height * (0.23 + (($i % 2) * 0.07)), $Width * 0.12, $Height * 0.45)
                    Fill-RoundedRect -Graphics $graphics -Brush $cardBrush -Rect $card -Radius ($Height * 0.035)
                    Draw-RoundedRect -Graphics $graphics -Pen $lanePen -Rect $card -Radius ($Height * 0.035)
                    if ($i -eq 0) {
                        $Graphics.DrawLine($focusPen, $card.Left + ($Height * 0.025), $card.Top + ($Height * 0.025), $card.Right - ($Height * 0.025), $card.Top + ($Height * 0.025))
                    }
                }
            }
            finally {
                $lanePen.Dispose()
                $focusPen.Dispose()
                $cardBrush.Dispose()
            }
        }
        elseif ($Kind -eq "Splash") {
            $mark = [System.Drawing.RectangleF]::new($Width * 0.16, $Height * 0.18, $Height * 0.64, $Height * 0.64)
            Draw-MatteSlatMark -Graphics $graphics -Rect $mark
            $title = [System.Drawing.RectangleF]::new($Width * 0.45, $Height * 0.25, $Width * 0.40, $Height * 0.28)
            Draw-Title -Graphics $graphics -Text "Next Gen Emby" -Rect $title -FontSize ($Height * 0.105)

            $captionFont = [System.Drawing.Font]::new("Segoe UI Variable Text", $Height * 0.05, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
            $captionBrush = [System.Drawing.SolidBrush]::new((New-IconColor 170 193 214 255))
            $format = [System.Drawing.StringFormat]::new()
            try {
                $format.Alignment = [System.Drawing.StringAlignment]::Near
                $format.LineAlignment = [System.Drawing.StringAlignment]::Near
                $captionRect = [System.Drawing.RectangleF]::new($Width * 0.45, $Height * 0.54, $Width * 0.34, $Height * 0.12)
                $graphics.DrawString("Private media library", $captionFont, $captionBrush, $captionRect, $format)
            }
            finally {
                $captionFont.Dispose()
                $captionBrush.Dispose()
                $format.Dispose()
            }
        }
        else {
            $inset = [Math]::Min($Width, $Height) * 0.08
            $mark = [System.Drawing.RectangleF]::new($inset, $inset, $Width - ($inset * 2), $Height - ($inset * 2))
            Draw-MatteSlatMark -Graphics $graphics -Rect $mark
        }
    }
    finally {
        $graphics.Dispose()
        $canvas.Dispose()
        $surface.Dispose()
        $hairline.Dispose()
    }

    return $bitmap
}

function Save-AppIcon {
    param(
        [string]$Name,
        [int]$Width,
        [int]$Height,
        [string]$Kind
    )

    $path = Join-Path $AssetsPath $Name
    $bitmap = New-AppIconBitmap -Width $Width -Height $Height -Kind $Kind
    try {
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

if (!(Test-Path -LiteralPath $AssetsPath)) {
    throw "Assets path not found: $AssetsPath"
}

Save-AppIcon -Name "StoreLogo.png" -Width 50 -Height 50 -Kind "Square"
Save-AppIcon -Name "Square44x44Logo.png" -Width 44 -Height 44 -Kind "Square"
Save-AppIcon -Name "Square150x150Logo.png" -Width 150 -Height 150 -Kind "Square"
Save-AppIcon -Name "Wide310x150Logo.png" -Width 310 -Height 150 -Kind "Wide"
Save-AppIcon -Name "SplashScreen.png" -Width 620 -Height 300 -Kind "Splash"

Write-Host "Generated Matte Library Slat app icon assets in $AssetsPath"
