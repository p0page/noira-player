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
    Canvas = New-IconColor 5 6 7
    Surface = New-IconColor 16 20 24
    Raised = New-IconColor 26 32 39
    Shelf = New-IconColor 26 32 39
    ShelfMuted = New-IconColor 16 20 24
    Inset = New-IconColor 9 12 15
    Hairline = New-IconColor 48 56 66
    Focus = New-IconColor 59 213 255
    Play = New-IconColor 97 212 124
    PlayCut = New-IconColor 4 16 7
    Progress = New-IconColor 224 184 106
    Text = New-IconColor 246 241 232
    MutedText = New-IconColor 185 192 200
}

$script:IconGeometry = @{
    TileRadius = 0.135
    InnerMargin = 0.040
    InnerRadius = 0.075
    MarkInset = 0.060
    FocusStroke = 0.024
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

function Draw-PlayerFocusMark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect
    )

    $s = [Math]::Min($Rect.Width, $Rect.Height)
    $x = $Rect.X + (($Rect.Width - $s) / 2)
    $y = $Rect.Y + (($Rect.Height - $s) / 2)

    $surfaceBrush = New-TokenBrush "Raised"
    $shelfBrush = New-TokenBrush "Shelf"
    $mutedBrush = New-TokenBrush "ShelfMuted"
    $hairlinePen = New-TokenPen "Hairline" ([Math]::Max(1.0, $s * $script:IconGeometry.HairlineStroke))
    $focusPen = New-TokenPen "Focus" ([Math]::Max(1.0, $s * $script:IconGeometry.FocusStroke))
    $progressPen = New-TokenPen "Progress" ([Math]::Max(2.0, $s * $script:IconGeometry.ProgressStroke))

    try {
        $screen = [System.Drawing.RectangleF]::new($x + ($s * 0.170), $y + ($s * 0.210), $s * 0.660, $s * 0.455)
        Fill-RoundedRect -Graphics $Graphics -Brush $surfaceBrush -Rect $screen -Radius ($s * 0.075)
        Draw-RoundedRect -Graphics $Graphics -Pen $hairlinePen -Rect $screen -Radius ($s * 0.075)

        $Graphics.DrawLine($focusPen, $screen.Left + ($s * 0.055), $screen.Top + ($s * 0.050), $screen.Left + ($s * 0.250), $screen.Top + ($s * 0.050))
        $Graphics.DrawLine($focusPen, $screen.Left + ($s * 0.055), $screen.Top + ($s * 0.055), $screen.Left + ($s * 0.055), $screen.Top + ($s * 0.245))

        $sideMeter = [System.Drawing.RectangleF]::new($screen.Right - ($s * 0.135), $screen.Top + ($s * 0.115), $s * 0.045, $s * 0.210)
        Fill-RoundedRect -Graphics $Graphics -Brush $shelfBrush -Rect $sideMeter -Radius ($s * 0.018)

        $playRect = [System.Drawing.RectangleF]::new($x + ($s * 0.425), $y + ($s * 0.345), $s * 0.150, $s * 0.175)
        Draw-PlayGlyph -Graphics $Graphics -Rect $playRect

        $subtitleLine = [System.Drawing.RectangleF]::new($screen.Left + ($s * 0.225), $screen.Bottom - ($s * 0.092), $s * 0.260, $s * 0.028)
        $audioLine = [System.Drawing.RectangleF]::new($screen.Left + ($s * 0.525), $screen.Bottom - ($s * 0.092), $s * 0.105, $s * 0.028)
        Fill-RoundedRect -Graphics $Graphics -Brush $mutedBrush -Rect $subtitleLine -Radius ($s * 0.012)
        Fill-RoundedRect -Graphics $Graphics -Brush $mutedBrush -Rect $audioLine -Radius ($s * 0.012)

        $Graphics.DrawLine($progressPen, $x + ($s * 0.250), $y + ($s * 0.790), $x + ($s * 0.750), $y + ($s * 0.790))
    }
    finally {
        $surfaceBrush.Dispose()
        $shelfBrush.Dispose()
        $mutedBrush.Dispose()
        $hairlinePen.Dispose()
        $focusPen.Dispose()
        $progressPen.Dispose()
    }
}

function Draw-WidePlayerSignals {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect
    )

    $mutedBrush = New-TokenBrush "ShelfMuted"
    $shelfBrush = New-TokenBrush "Shelf"
    $progressBrush = New-TokenBrush "Progress"
    $hairlinePen = New-TokenPen "Hairline" ([Math]::Max(1.0, $Rect.Height * 0.010))

    try {
        $track = [System.Drawing.RectangleF]::new($Rect.Left, $Rect.Top + ($Rect.Height * 0.080), $Rect.Width * 0.760, $Rect.Height * 0.090)
        $played = [System.Drawing.RectangleF]::new($track.Left, $track.Top, $track.Width * 0.520, $track.Height)
        Fill-RoundedRect -Graphics $Graphics -Brush $mutedBrush -Rect $track -Radius ($Rect.Height * 0.028)
        Fill-RoundedRect -Graphics $Graphics -Brush $progressBrush -Rect $played -Radius ($Rect.Height * 0.028)

        $subtitleA = [System.Drawing.RectangleF]::new($Rect.Left, $Rect.Top + ($Rect.Height * 0.360), $Rect.Width * 0.660, $Rect.Height * 0.080)
        $subtitleB = [System.Drawing.RectangleF]::new($Rect.Left, $Rect.Top + ($Rect.Height * 0.500), $Rect.Width * 0.460, $Rect.Height * 0.080)
        Fill-RoundedRect -Graphics $Graphics -Brush $shelfBrush -Rect $subtitleA -Radius ($Rect.Height * 0.026)
        Fill-RoundedRect -Graphics $Graphics -Brush $mutedBrush -Rect $subtitleB -Radius ($Rect.Height * 0.026)

        for ($i = 0; $i -lt 5; $i++) {
            $barHeight = $Rect.Height * (0.110 + (0.042 * (($i + 1) % 3)))
            $bar = [System.Drawing.RectangleF]::new(
                $Rect.Left + ($i * $Rect.Width * 0.120),
                $Rect.Bottom - $barHeight,
                $Rect.Width * 0.070,
                $barHeight)
            Fill-RoundedRect -Graphics $Graphics -Brush $mutedBrush -Rect $bar -Radius ($Rect.Height * 0.020)
            Draw-RoundedRect -Graphics $Graphics -Pen $hairlinePen -Rect $bar -Radius ($Rect.Height * 0.020)
        }
    }
    finally {
        $mutedBrush.Dispose()
        $shelfBrush.Dispose()
        $progressBrush.Dispose()
        $hairlinePen.Dispose()
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
            Draw-PlayerFocusMark -Graphics $graphics -Rect $mark
            $rails = [System.Drawing.RectangleF]::new($Width * 0.455, $Height * 0.270, $Width * 0.455, $Height * 0.470)
            Draw-WidePlayerSignals -Graphics $graphics -Rect $rails
        }
        elseif ($Kind -eq "Splash") {
            $markSize = $Height * 0.760
            $mark = [System.Drawing.RectangleF]::new(($Width - $markSize) / 2, ($Height - $markSize) / 2, $markSize, $markSize)
            Draw-PlayerFocusMark -Graphics $graphics -Rect $mark
        }
        else {
            $inset = [Math]::Min($Width, $Height) * $script:IconGeometry.MarkInset
            $mark = [System.Drawing.RectangleF]::new($inset, $inset, $Width - ($inset * 2), $Height - ($inset * 2))
            Draw-PlayerFocusMark -Graphics $graphics -Rect $mark
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

Write-Host "Generated Player Focus Mark app icon assets in $AssetsPath"
