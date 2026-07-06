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

function Draw-PortalMark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect
    )

    $s = [Math]::Min($Rect.Width, $Rect.Height)
    $x = $Rect.X + (($Rect.Width - $s) / 2)
    $y = $Rect.Y + (($Rect.Height - $s) / 2)
    $radius = $s * 0.075

    $cardBack = [System.Drawing.SolidBrush]::new((New-IconColor 21 34 48 245))
    $cardMid = [System.Drawing.SolidBrush]::new((New-IconColor 29 45 63 248))
    $cardFront = [System.Drawing.SolidBrush]::new((New-IconColor 13 22 34 252))
    $cardLine = [System.Drawing.Pen]::new((New-IconColor 70 231 255 92), [Math]::Max(1.0, $s * 0.018))
    $softLine = [System.Drawing.Pen]::new((New-IconColor 170 193 214 55), [Math]::Max(1.0, $s * 0.01))
    $portalGlow = [System.Drawing.SolidBrush]::new((New-IconColor 59 213 255 54))
    $portal = [System.Drawing.SolidBrush]::new((New-IconColor 78 231 255 245))
    $portalCore = [System.Drawing.SolidBrush]::new((New-IconColor 5 8 13 255))
    $amberPen = [System.Drawing.Pen]::new((New-IconColor 217 164 65 245), [Math]::Max(2.0, $s * 0.055))

    try {
        $amberPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $amberPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

        $state = $Graphics.Save()
        $Graphics.TranslateTransform($x + ($s * 0.34), $y + ($s * 0.49))
        $Graphics.RotateTransform(-9)
        $backRect = [System.Drawing.RectangleF]::new(-$s * 0.22, -$s * 0.30, $s * 0.50, $s * 0.62)
        Fill-RoundedRect -Graphics $Graphics -Brush $cardBack -Rect $backRect -Radius $radius
        Draw-RoundedRect -Graphics $Graphics -Pen $softLine -Rect $backRect -Radius $radius
        $Graphics.Restore($state)

        $state = $Graphics.Save()
        $Graphics.TranslateTransform($x + ($s * 0.50), $y + ($s * 0.45))
        $Graphics.RotateTransform(7)
        $midRect = [System.Drawing.RectangleF]::new(-$s * 0.25, -$s * 0.33, $s * 0.52, $s * 0.66)
        Fill-RoundedRect -Graphics $Graphics -Brush $cardMid -Rect $midRect -Radius $radius
        Draw-RoundedRect -Graphics $Graphics -Pen $softLine -Rect $midRect -Radius $radius
        $Graphics.Restore($state)

        $state = $Graphics.Save()
        $Graphics.TranslateTransform($x + ($s * 0.61), $y + ($s * 0.52))
        $Graphics.RotateTransform(-2)
        $frontRect = [System.Drawing.RectangleF]::new(-$s * 0.27, -$s * 0.31, $s * 0.53, $s * 0.60)
        Fill-RoundedRect -Graphics $Graphics -Brush $cardFront -Rect $frontRect -Radius $radius
        Draw-RoundedRect -Graphics $Graphics -Pen $cardLine -Rect $frontRect -Radius $radius

        $stripePen = [System.Drawing.Pen]::new((New-IconColor 78 231 255 120), [Math]::Max(1.0, $s * 0.015))
        try {
            $Graphics.DrawLine($stripePen, -$s * 0.20, -$s * 0.14, $s * 0.17, -$s * 0.14)
            $Graphics.DrawLine($stripePen, -$s * 0.20, $s * 0.16, $s * 0.10, $s * 0.16)
        }
        finally {
            $stripePen.Dispose()
        }
        $Graphics.Restore($state)

        $glowRect = [System.Drawing.RectangleF]::new($x + ($s * 0.27), $y + ($s * 0.27), $s * 0.49, $s * 0.49)
        $portalRect = [System.Drawing.RectangleF]::new($x + ($s * 0.34), $y + ($s * 0.34), $s * 0.35, $s * 0.35)
        $Graphics.FillEllipse($portalGlow, $glowRect)
        $Graphics.FillEllipse($portal, $portalRect)

        $triangle = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($x + ($s * 0.47), $y + ($s * 0.425)),
            [System.Drawing.PointF]::new($x + ($s * 0.47), $y + ($s * 0.605)),
            [System.Drawing.PointF]::new($x + ($s * 0.615), $y + ($s * 0.515))
        )
        $Graphics.FillPolygon($portalCore, $triangle)

        $arcRect = [System.Drawing.RectangleF]::new($x + ($s * 0.20), $y + ($s * 0.77), $s * 0.60, $s * 0.25)
        $Graphics.DrawArc($amberPen, $arcRect, 197, 146)
    }
    finally {
        $cardBack.Dispose()
        $cardMid.Dispose()
        $cardFront.Dispose()
        $cardLine.Dispose()
        $softLine.Dispose()
        $portalGlow.Dispose()
        $portal.Dispose()
        $portalCore.Dispose()
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
            Draw-PortalMark -Graphics $graphics -Rect $mark
            $lanePen = [System.Drawing.Pen]::new((New-IconColor 78 231 255 80), [Math]::Max(1.0, $Height * 0.012))
            $cardBrush = [System.Drawing.SolidBrush]::new((New-IconColor 23 34 48 220))
            try {
                for ($i = 0; $i -lt 4; $i++) {
                    $card = [System.Drawing.RectangleF]::new($Width * (0.42 + ($i * 0.105)), $Height * (0.23 + (($i % 2) * 0.07)), $Width * 0.12, $Height * 0.45)
                    Fill-RoundedRect -Graphics $graphics -Brush $cardBrush -Rect $card -Radius ($Height * 0.035)
                    Draw-RoundedRect -Graphics $graphics -Pen $lanePen -Rect $card -Radius ($Height * 0.035)
                }
            }
            finally {
                $lanePen.Dispose()
                $cardBrush.Dispose()
            }
        }
        elseif ($Kind -eq "Splash") {
            $mark = [System.Drawing.RectangleF]::new($Width * 0.16, $Height * 0.18, $Height * 0.64, $Height * 0.64)
            Draw-PortalMark -Graphics $graphics -Rect $mark
            $title = [System.Drawing.RectangleF]::new($Width * 0.45, $Height * 0.25, $Width * 0.40, $Height * 0.28)
            Draw-Title -Graphics $graphics -Text "Next Gen Emby" -Rect $title -FontSize ($Height * 0.105)

            $captionFont = [System.Drawing.Font]::new("Segoe UI Variable Text", $Height * 0.05, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
            $captionBrush = [System.Drawing.SolidBrush]::new((New-IconColor 170 193 214 255))
            $format = [System.Drawing.StringFormat]::new()
            try {
                $format.Alignment = [System.Drawing.StringAlignment]::Near
                $format.LineAlignment = [System.Drawing.StringAlignment]::Near
                $captionRect = [System.Drawing.RectangleF]::new($Width * 0.45, $Height * 0.54, $Width * 0.34, $Height * 0.12)
                $graphics.DrawString("Library Portal", $captionFont, $captionBrush, $captionRect, $format)
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
            Draw-PortalMark -Graphics $graphics -Rect $mark
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

Write-Host "Generated Library Portal app icon assets in $AssetsPath"
