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

$script:IconTokens = @{
    Canvas = New-IconColor 3 6 10
    Surface = New-IconColor 8 13 19
    Raised = New-IconColor 15 22 31
    Shelf = New-IconColor 25 33 43
    ShelfMuted = New-IconColor 18 25 34
    Inset = New-IconColor 5 9 14
    Hairline = New-IconColor 48 63 78
    Focus = New-IconColor 59 213 255
    Play = New-IconColor 97 212 124
    PlayCut = New-IconColor 4 16 7
    Progress = New-IconColor 224 184 106
    Text = New-IconColor 246 241 232
    MutedText = New-IconColor 185 192 200
}

$script:IconGeometry = @{
    TileRadius = 0.12
    InnerMargin = 0.045
    InnerRadius = 0.08
    MarkInset = 0.07
    FocusStroke = 0.022
    HairlineStroke = 0.010
    ProgressStroke = 0.040
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

function New-TokenBrush {
    param([string]$Name)

    return [System.Drawing.SolidBrush]::new($script:IconTokens[$Name])
}

function New-TokenPen {
    param(
        [string]$Name,
        [float]$Width
    )

    $pen = [System.Drawing.Pen]::new($script:IconTokens[$Name], $Width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Draw-PlayGlyph {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect
    )

    $playBrush = New-TokenBrush "Play"
    $cutBrush = New-TokenBrush "PlayCut"
    try {
        Fill-RoundedRect -Graphics $Graphics -Brush $playBrush -Rect $Rect -Radius ($Rect.Width * 0.25)
        $points = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($Rect.Left + ($Rect.Width * 0.40), $Rect.Top + ($Rect.Height * 0.27)),
            [System.Drawing.PointF]::new($Rect.Left + ($Rect.Width * 0.40), $Rect.Bottom - ($Rect.Height * 0.27)),
            [System.Drawing.PointF]::new($Rect.Right - ($Rect.Width * 0.24), $Rect.Top + ($Rect.Height * 0.50))
        )
        $Graphics.FillPolygon($cutBrush, $points)
    }
    finally {
        $playBrush.Dispose()
        $cutBrush.Dispose()
    }
}

function Draw-CinemaShelfMark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect
    )

    $s = [Math]::Min($Rect.Width, $Rect.Height)
    $x = $Rect.X + (($Rect.Width - $s) / 2)
    $y = $Rect.Y + (($Rect.Height - $s) / 2)

    $surfaceBrush = New-TokenBrush "Raised"
    $insetBrush = New-TokenBrush "Inset"
    $shelfBrush = New-TokenBrush "Shelf"
    $mutedBrush = New-TokenBrush "ShelfMuted"
    $hairlinePen = New-TokenPen "Hairline" ([Math]::Max(1.0, $s * $script:IconGeometry.HairlineStroke))
    $focusPen = New-TokenPen "Focus" ([Math]::Max(1.0, $s * $script:IconGeometry.FocusStroke))
    $progressPen = New-TokenPen "Progress" ([Math]::Max(2.0, $s * $script:IconGeometry.ProgressStroke))

    try {
        $screen = [System.Drawing.RectangleF]::new($x + ($s * 0.105), $y + ($s * 0.145), $s * 0.790, $s * 0.560)
        Fill-RoundedRect -Graphics $Graphics -Brush $surfaceBrush -Rect $screen -Radius ($s * 0.075)
        Draw-RoundedRect -Graphics $Graphics -Pen $hairlinePen -Rect $screen -Radius ($s * 0.075)

        $guide = [System.Drawing.RectangleF]::new($screen.Left + ($s * 0.055), $screen.Top + ($s * 0.085), $s * 0.045, $screen.Height - ($s * 0.170))
        Fill-RoundedRect -Graphics $Graphics -Brush $insetBrush -Rect $guide -Radius ($s * 0.018)
        $activeGuide = [System.Drawing.RectangleF]::new($guide.Left + ($s * 0.010), $guide.Top + ($s * 0.020), $guide.Width - ($s * 0.020), $guide.Height * 0.36)
        $focusBrush = New-TokenBrush "Focus"
        try {
            Fill-RoundedRect -Graphics $Graphics -Brush $focusBrush -Rect $activeGuide -Radius ($s * 0.014)
        }
        finally {
            $focusBrush.Dispose()
        }

        $railLeft = $screen.Left + ($s * 0.155)
        $railTop = $screen.Top + ($s * 0.090)
        $railWidth = $screen.Width - ($s * 0.220)
        for ($i = 0; $i -lt 3; $i++) {
            $rail = [System.Drawing.RectangleF]::new(
                $railLeft,
                $railTop + ($i * $s * 0.145),
                $railWidth - (($i % 2) * $s * 0.080),
                $s * 0.080)
            Fill-RoundedRect -Graphics $Graphics -Brush $mutedBrush -Rect $rail -Radius ($s * 0.025)
        }

        $focusCard = [System.Drawing.RectangleF]::new($screen.Left + ($s * 0.405), $screen.Top + ($s * 0.160), $s * 0.255, $s * 0.305)
        Fill-RoundedRect -Graphics $Graphics -Brush $shelfBrush -Rect $focusCard -Radius ($s * 0.045)
        Draw-RoundedRect -Graphics $Graphics -Pen $hairlinePen -Rect $focusCard -Radius ($s * 0.045)

        $Graphics.DrawLine($focusPen, $focusCard.Left + ($s * 0.030), $focusCard.Top + ($s * 0.026), $focusCard.Right - ($s * 0.030), $focusCard.Top + ($s * 0.026))
        $Graphics.DrawLine($focusPen, $focusCard.Left + ($s * 0.028), $focusCard.Top + ($s * 0.030), $focusCard.Left + ($s * 0.028), $focusCard.Bottom - ($s * 0.040))

        $playRect = [System.Drawing.RectangleF]::new($focusCard.Left + ($s * 0.070), $focusCard.Top + ($s * 0.094), $s * 0.095, $s * 0.125)
        Draw-PlayGlyph -Graphics $Graphics -Rect $playRect

        $smallPoster = [System.Drawing.RectangleF]::new($screen.Left + ($s * 0.705), $screen.Top + ($s * 0.205), $s * 0.080, $s * 0.185)
        Fill-RoundedRect -Graphics $Graphics -Brush $mutedBrush -Rect $smallPoster -Radius ($s * 0.020)
        Draw-RoundedRect -Graphics $Graphics -Pen $hairlinePen -Rect $smallPoster -Radius ($s * 0.020)

        $Graphics.DrawLine($progressPen, $x + ($s * 0.255), $y + ($s * 0.810), $x + ($s * 0.745), $y + ($s * 0.810))
    }
    finally {
        $surfaceBrush.Dispose()
        $insetBrush.Dispose()
        $shelfBrush.Dispose()
        $mutedBrush.Dispose()
        $hairlinePen.Dispose()
        $focusPen.Dispose()
        $progressPen.Dispose()
    }
}

function Draw-WideRails {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect
    )

    $shelfBrush = New-TokenBrush "ShelfMuted"
    $activeBrush = New-TokenBrush "Shelf"
    $hairlinePen = New-TokenPen "Hairline" ([Math]::Max(1.0, $Rect.Height * 0.010))
    $focusPen = New-TokenPen "Focus" ([Math]::Max(1.0, $Rect.Height * 0.014))

    try {
        for ($row = 0; $row -lt 3; $row++) {
            $y = $Rect.Top + ($row * $Rect.Height * 0.285)
            for ($col = 0; $col -lt 4; $col++) {
                $card = [System.Drawing.RectangleF]::new(
                    $Rect.Left + ($col * $Rect.Width * 0.225),
                    $y,
                    $Rect.Width * 0.170,
                    $Rect.Height * 0.170)
                Fill-RoundedRect -Graphics $Graphics -Brush ($(if ($row -eq 0 -and $col -eq 0) { $activeBrush } else { $shelfBrush })) -Rect $card -Radius ($Rect.Height * 0.030)
                Draw-RoundedRect -Graphics $Graphics -Pen $hairlinePen -Rect $card -Radius ($Rect.Height * 0.030)
                if ($row -eq 0 -and $col -eq 0) {
                    $Graphics.DrawLine($focusPen, $card.Left + ($Rect.Height * 0.018), $card.Top + ($Rect.Height * 0.014), $card.Right - ($Rect.Height * 0.018), $card.Top + ($Rect.Height * 0.014))
                }
            }
        }
    }
    finally {
        $shelfBrush.Dispose()
        $activeBrush.Dispose()
        $hairlinePen.Dispose()
        $focusPen.Dispose()
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
    $brush = New-TokenBrush "Text"
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

function Draw-Caption {
    param(
        [System.Drawing.Graphics]$Graphics,
        [string]$Text,
        [System.Drawing.RectangleF]$Rect,
        [float]$FontSize
    )

    $font = [System.Drawing.Font]::new("Segoe UI Variable Text", $FontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-TokenBrush "MutedText"
    $format = [System.Drawing.StringFormat]::new()
    try {
        $format.Alignment = [System.Drawing.StringAlignment]::Near
        $format.LineAlignment = [System.Drawing.StringAlignment]::Near
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

    $canvas = New-TokenBrush "Canvas"
    $surface = New-TokenBrush "Surface"
    $hairline = New-TokenPen "Hairline" ([Math]::Max(1.0, [Math]::Min($Width, $Height) * $script:IconGeometry.HairlineStroke))

    try {
        $graphics.Clear((New-IconColor 0 0 0 0))
        $tileRect = [System.Drawing.RectangleF]::new(0, 0, $Width, $Height)
        $corner = [Math]::Min($Width, $Height) * $script:IconGeometry.TileRadius
        Fill-RoundedRect -Graphics $graphics -Brush $canvas -Rect $tileRect -Radius $corner

        $innerMargin = [Math]::Min($Width, $Height) * $script:IconGeometry.InnerMargin
        $inner = [System.Drawing.RectangleF]::new($innerMargin, $innerMargin, $Width - ($innerMargin * 2), $Height - ($innerMargin * 2))
        Fill-RoundedRect -Graphics $graphics -Brush $surface -Rect $inner -Radius ([Math]::Min($Width, $Height) * $script:IconGeometry.InnerRadius)
        Draw-RoundedRect -Graphics $graphics -Pen $hairline -Rect $inner -Radius ([Math]::Min($Width, $Height) * $script:IconGeometry.InnerRadius)

        if ($Kind -eq "Wide") {
            $mark = [System.Drawing.RectangleF]::new($Width * 0.035, $Height * 0.070, $Height * 0.860, $Height * 0.860)
            Draw-CinemaShelfMark -Graphics $graphics -Rect $mark
            $rails = [System.Drawing.RectangleF]::new($Width * 0.455, $Height * 0.270, $Width * 0.455, $Height * 0.470)
            Draw-WideRails -Graphics $graphics -Rect $rails
        }
        elseif ($Kind -eq "Splash") {
            $mark = [System.Drawing.RectangleF]::new($Width * 0.130, $Height * 0.150, $Height * 0.700, $Height * 0.700)
            Draw-CinemaShelfMark -Graphics $graphics -Rect $mark
            $title = [System.Drawing.RectangleF]::new($Width * 0.440, $Height * 0.270, $Width * 0.390, $Height * 0.190)
            Draw-Title -Graphics $graphics -Text "Next Gen Emby" -Rect $title -FontSize ($Height * 0.095)
            $caption = [System.Drawing.RectangleF]::new($Width * 0.445, $Height * 0.520, $Width * 0.350, $Height * 0.120)
            Draw-Caption -Graphics $graphics -Text "Couch-first media library" -Rect $caption -FontSize ($Height * 0.047)
        }
        else {
            $inset = [Math]::Min($Width, $Height) * $script:IconGeometry.MarkInset
            $mark = [System.Drawing.RectangleF]::new($inset, $inset, $Width - ($inset * 2), $Height - ($inset * 2))
            Draw-CinemaShelfMark -Graphics $graphics -Rect $mark
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

Write-Host "Generated Cinema Shelf Mark app icon assets in $AssetsPath"
