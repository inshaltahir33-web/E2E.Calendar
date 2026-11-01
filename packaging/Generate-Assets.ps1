[CmdletBinding()]
param(
    [string]$OutputDirectory = ""
)

# Generates every MSIX/Store image asset from the single SVG placeholder design in
# packaging/Assets (150x150 viewBox). PNG is the only format MSIX manifests accept, so
# the SVG sources are rasterised here instead of being referenced by the manifest.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot "Assets"
}
if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

$accent = [System.Drawing.Color]::FromArgb(37, 99, 235)   # #2563EB
$white = [System.Drawing.Color]::White

function New-Brush([System.Drawing.Color]$color) {
    New-Object System.Drawing.SolidBrush($color)
}

# Draws the calendar glyph in the SVG's own 150x150 coordinate space, scaled to $Size.
function Add-CalendarGlyph([System.Drawing.Graphics]$graphics, [double]$size, [double]$left = 0, [double]$top = 0) {
    $state = $graphics.Save()
    $graphics.TranslateTransform([single]$left, [single]$top)
    $graphics.ScaleTransform([single]($size / 150.0), [single]($size / 150.0))

    $radius = 24.0
    $diameter = $radius * 2
    $plate = New-Object System.Drawing.Drawing2D.GraphicsPath
    $plate.AddArc(0, 0, $diameter, $diameter, 180, 90)
    $plate.AddArc(150 - $diameter, 0, $diameter, $diameter, 270, 90)
    $plate.AddArc(150 - $diameter, 150 - $diameter, $diameter, $diameter, 0, 90)
    $plate.AddArc(0, 150 - $diameter, $diameter, $diameter, 90, 90)
    $plate.CloseFigure()

    $plateBrush = New-Brush $accent
    $graphics.FillPath($plateBrush, $plate)

    $paperBrush = New-Brush $white
    $graphics.FillRectangle($paperBrush, 36, 42, 78, 72)

    $pen = New-Object System.Drawing.Pen($accent, 9)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($pen, 51, 30, 51, 54)
    $graphics.DrawLine($pen, 99, 30, 99, 54)
    $graphics.DrawLine($pen, 36, 63, 114, 63)

    foreach ($x in 57, 81, 105) {
        $graphics.FillEllipse($plateBrush, $x - 6, 75, 12, 12)
    }

    $pen.Dispose()
    $paperBrush.Dispose()
    $plateBrush.Dispose()
    $plate.Dispose()
    $graphics.Restore($state)
}

# Renders a square icon. $IconInset keeps small target sizes from clipping the glyph.
function New-SquareIcon([int]$size, [string]$path, [double]$inset = 0) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $glyph = $size - (2 * $inset)
    Add-CalendarGlyph -graphics $graphics -size $glyph -left $inset -top $inset

    $graphics.Dispose()
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

# Wide tile / splash: icon centred on a transparent canvas (manifest supplies BackgroundColor).
function New-WideIcon([int]$width, [int]$height, [string]$path, [double]$iconRatio = 0.8) {
    $bitmap = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $icon = [Math]::Floor($height * $iconRatio)
    $left = [Math]::Floor(($width - $icon) / 2)
    $top = [Math]::Floor(($height - $icon) / 2)
    Add-CalendarGlyph -graphics $graphics -size $icon -left $left -top $top

    $graphics.Dispose()
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

$written = New-Object System.Collections.Generic.List[string]

function Register-Asset([string]$name) {
    $script:written.Add($name)
    Write-Host "  $name"
}

Write-Host "Generating MSIX image assets in $OutputDirectory"

# Store listing logo (Properties/Logo)
New-SquareIcon 50 (Join-Path $OutputDirectory "StoreLogo.png")
Register-Asset "StoreLogo.png"

# Start menu / taskbar / app list / tile
New-SquareIcon 44 (Join-Path $OutputDirectory "Square44x44Logo.png")
Register-Asset "Square44x44Logo.png"

foreach ($target in 16, 24, 32, 48, 256) {
    New-SquareIcon $target (Join-Path $OutputDirectory "Square44x44Logo.targetsize-$target.png")
    Register-Asset "Square44x44Logo.targetsize-$target.png"
    New-SquareIcon $target (Join-Path $OutputDirectory "Square44x44Logo.altform-unplated_targetsize-$target.png")
    Register-Asset "Square44x44Logo.altform-unplated_targetsize-$target.png"
}

New-SquareIcon 71 (Join-Path $OutputDirectory "Square71x71Logo.png")
Register-Asset "Square71x71Logo.png"

New-SquareIcon 150 (Join-Path $OutputDirectory "Square150x150Logo.png")
Register-Asset "Square150x150Logo.png"
New-SquareIcon 300 (Join-Path $OutputDirectory "Square150x150Logo.scale-200.png")
Register-Asset "Square150x150Logo.scale-200.png"

New-SquareIcon 310 (Join-Path $OutputDirectory "Square310x310Logo.png")
Register-Asset "Square310x310Logo.png"
New-SquareIcon 620 (Join-Path $OutputDirectory "Square310x310Logo.scale-200.png")
Register-Asset "Square310x310Logo.scale-200.png"

New-WideIcon 310 150 (Join-Path $OutputDirectory "Wide310x150Logo.png")
Register-Asset "Wide310x150Logo.png"
New-WideIcon 620 300 (Join-Path $OutputDirectory "Wide310x150Logo.scale-200.png")
Register-Asset "Wide310x150Logo.scale-200.png"

New-WideIcon 620 300 (Join-Path $OutputDirectory "SplashScreen.png") 0.72
Register-Asset "SplashScreen.png"

Write-Host "Generated $($written.Count) assets."
