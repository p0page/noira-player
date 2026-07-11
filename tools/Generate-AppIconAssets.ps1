param(
    [string]$AssetsPath = "src\NoiraPlayer.App\Assets"
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
    Canvas = New-IconColor 5 7 10
    Surface = New-IconColor 16 22 28
    Raised = New-IconColor 32 40 50
    Shelf = New-IconColor 37 45 53
    ShelfMuted = New-IconColor 16 22 28
    Inset = New-IconColor 8 13 18
    Hairline = New-IconColor 46 57 68
    Focus = New-IconColor 238 243 246
    Play = New-IconColor 37 45 53
    PlayCut = New-IconColor 139 124 246
    Progress = New-IconColor 139 124 246
    Text = New-IconColor 238 243 246
    MutedText = New-IconColor 169 179 186
}

$script:IconGeometry = @{
    TileRadius = 0.135
    InnerMargin = 0.040
    InnerRadius = 0.075
    MarkInset = 0.045
    FocusStroke = 0.030
    HairlineStroke = 0.010
    ProgressStroke = 0.052
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

function Draw-FocusPath {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Pen]$Pen,
        [System.Drawing.RectangleF]$Rect,
        [float]$Scale
    )

    $Graphics.DrawLine($Pen, $Rect.Left + ($Scale * 0.060), $Rect.Top + ($Scale * 0.060), $Rect.Left + ($Scale * 0.315), $Rect.Top + ($Scale * 0.060))
    $Graphics.DrawLine($Pen, $Rect.Left + ($Scale * 0.060), $Rect.Top + ($Scale * 0.065), $Rect.Left + ($Scale * 0.060), $Rect.Top + ($Scale * 0.335))
}

function Draw-LiftedPlaySurface {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.RectangleF]$Rect
    )

    Draw-PlayGlyph -Graphics $Graphics -Rect $Rect
}

function Draw-ProgressBase {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Pen]$Pen,
        [System.Drawing.RectangleF]$Rect,
        [float]$Scale
    )

    $Graphics.DrawLine($Pen, $Rect.Left + ($Scale * 0.200), $Rect.Bottom - ($Scale * 0.080), $Rect.Right - ($Scale * 0.200), $Rect.Bottom - ($Scale * 0.080))
}

function Draw-PlayerLiftMark {
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
        $screen = [System.Drawing.RectangleF]::new($x + ($s * 0.135), $y + ($s * 0.170), $s * 0.730, $s * 0.555)
        Fill-RoundedRect -Graphics $Graphics -Brush $surfaceBrush -Rect $screen -Radius ($s * 0.085)
        Draw-RoundedRect -Graphics $Graphics -Pen $hairlinePen -Rect $screen -Radius ($s * 0.085)

        $inner = [System.Drawing.RectangleF]::new($screen.Left + ($s * 0.085), $screen.Top + ($s * 0.105), $screen.Width - ($s * 0.170), $screen.Height - ($s * 0.205))
        Fill-RoundedRect -Graphics $Graphics -Brush $shelfBrush -Rect $inner -Radius ($s * 0.045)

        Draw-FocusPath -Graphics $Graphics -Pen $focusPen -Rect $screen -Scale $s

        $playRect = [System.Drawing.RectangleF]::new($x + ($s * 0.395), $y + ($s * 0.335), $s * 0.210, $s * 0.235)
        Draw-LiftedPlaySurface -Graphics $Graphics -Rect $playRect

        $stateBase = [System.Drawing.RectangleF]::new($x + ($s * 0.355), $y + ($s * 0.625), $s * 0.290, $s * 0.030)
        Fill-RoundedRect -Graphics $Graphics -Brush $mutedBrush -Rect $stateBase -Radius ($s * 0.012)

        Draw-ProgressBase -Graphics $Graphics -Pen $progressPen -Rect ([System.Drawing.RectangleF]::new($x, $y, $s, $s)) -Scale $s
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
            $markSize = $Height * 0.860
            $mark = [System.Drawing.RectangleF]::new(($Width - $markSize) / 2, ($Height - $markSize) / 2, $markSize, $markSize)
            Draw-PlayerLiftMark -Graphics $graphics -Rect $mark
        }
        elseif ($Kind -eq "Splash") {
            $markSize = $Height * 0.760
            $mark = [System.Drawing.RectangleF]::new(($Width - $markSize) / 2, ($Height - $markSize) / 2, $markSize, $markSize)
            Draw-PlayerLiftMark -Graphics $graphics -Rect $mark
        }
        else {
            $inset = [Math]::Min($Width, $Height) * $script:IconGeometry.MarkInset
            $mark = [System.Drawing.RectangleF]::new($inset, $inset, $Width - ($inset * 2), $Height - ($inset * 2))
            Draw-PlayerLiftMark -Graphics $graphics -Rect $mark
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

Write-Host "Generated Player Lift Mark app icon assets in $AssetsPath"
