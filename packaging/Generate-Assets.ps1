[CmdletBinding()]
param(
    [string]$OutputDirectory = "",
    [string]$AppAssetsDirectory = ""
)

# Generates every MSIX/Store image asset (and the runtime/exe icon) from the single
# 1024x1024 master artwork in packaging/appicon.png. PNG is the only format the MSIX
# manifest accepts, so the master is rasterised at each required scale/target size.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot "Assets"
}
if (-not $AppAssetsDirectory) {
    $AppAssetsDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) "src\E2E.Calendar.App\Assets"
}

$sourcePath = Join-Path $PSScriptRoot "appicon.png"
if (-not (Test-Path $sourcePath)) {
    throw "Master icon not found: $sourcePath"
}

foreach ($directory in @($OutputDirectory, $AppAssetsDirectory)) {
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }
}

# Every PNG in the manifest asset folder is generated here; drop stale/placeholder art.
Get-ChildItem -LiteralPath $OutputDirectory -Filter *.png -File | Remove-Item -Force

$source = [System.Drawing.Bitmap]::FromFile($sourcePath)

# Lock screen badges must be a monochrome glyph on transparency. Build a solid white
# silhouette from everything in the master artwork that differs from its background,
# then crop to the glyph so it stays centred and legible at 24px.
if (-not ("MonochromeBadge" -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public sealed class BadgeMask
{
    public Bitmap Bitmap;
    public Rectangle Bounds;
}

public static class MonochromeBadge
{
    public static BadgeMask Create(Bitmap source, int threshold)
    {
        Color background = source.GetPixel(source.Width / 12, source.Height / 2);
        int width = source.Width;
        int height = source.Height;
        Rectangle rect = new Rectangle(0, 0, width, height);
        Bitmap mask = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        BitmapData src = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData dst = mask.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        byte[] srcBytes = new byte[src.Stride * height];
        byte[] dstBytes = new byte[dst.Stride * height];
        Marshal.Copy(src.Scan0, srcBytes, 0, srcBytes.Length);

        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            int srcRow = y * src.Stride;
            int dstRow = y * dst.Stride;
            for (int x = 0; x < width; x++)
            {
                int si = srcRow + (x * 4);
                byte b = srcBytes[si];
                byte g = srcBytes[si + 1];
                byte r = srcBytes[si + 2];
                byte a = srcBytes[si + 3];
                if (a < 128)
                {
                    continue;
                }

                int dr = r - background.R;
                int dg = g - background.G;
                int db = b - background.B;
                double distance = Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
                if (distance < threshold)
                {
                    continue;
                }

                int di = dstRow + (x * 4);
                dstBytes[di] = 255;
                dstBytes[di + 1] = 255;
                dstBytes[di + 2] = 255;
                dstBytes[di + 3] = 255;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        Marshal.Copy(dstBytes, 0, dst.Scan0, dstBytes.Length);
        mask.UnlockBits(dst);
        source.UnlockBits(src);

        BadgeMask result = new BadgeMask();
        result.Bitmap = mask;
        result.Bounds = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return result;
    }

    public static void Whiten(Bitmap bitmap)
    {
        Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        byte[] bytes = new byte[data.Stride * bitmap.Height];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

        for (int i = 0; i < bytes.Length; i += 4)
        {
            if (bytes[i + 3] > 0)
            {
                bytes[i] = 255;
                bytes[i + 1] = 255;
                bytes[i + 2] = 255;
            }
        }

        Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        bitmap.UnlockBits(data);
    }
}
"@
}

function New-IconBitmap([int]$width, [int]$height, [double]$fill = 1.0) {
    $bitmap = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    # Preserve aspect ratio: fit the square master inside the canvas, centred.
    $icon = [Math]::Floor([Math]::Min($width, $height) * $fill)
    $left = [Math]::Floor(($width - $icon) / 2)
    $top = [Math]::Floor(($height - $icon) / 2)
    $destination = New-Object System.Drawing.Rectangle($left, $top, [int]$icon, [int]$icon)

    $attributes = New-Object System.Drawing.Imaging.ImageAttributes
    $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $graphics.DrawImage(
        $source,
        $destination,
        0,
        0,
        $source.Width,
        $source.Height,
        [System.Drawing.GraphicsUnit]::Pixel,
        $attributes)

    $attributes.Dispose()
    $graphics.Dispose()

    return $bitmap
}

function New-Png([string]$path, [int]$width, [int]$height, [double]$fill = 1.0) {
    $bitmap = New-IconBitmap $width $height $fill
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

function New-LockScreenPng([string]$path, [int]$size, [BadgeMask]$badge) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $target = [Math]::Floor($size * 0.9)
    $scale = [Math]::Min($target / $badge.Bounds.Width, $target / $badge.Bounds.Height)
    $width = [Math]::Max(1, [int][Math]::Round($badge.Bounds.Width * $scale))
    $height = [Math]::Max(1, [int][Math]::Round($badge.Bounds.Height * $scale))
    $left = [int][Math]::Floor(($size - $width) / 2)
    $top = [int][Math]::Floor(($size - $height) / 2)
    $destination = New-Object System.Drawing.Rectangle($left, $top, $width, $height)

    $graphics.DrawImage(
        $badge.Bitmap,
        $destination,
        $badge.Bounds.X,
        $badge.Bounds.Y,
        $badge.Bounds.Width,
        $badge.Bounds.Height,
        [System.Drawing.GraphicsUnit]::Pixel)

    $graphics.Dispose()
    # Interpolation leaves near-white AA pixels; force the glyph to pure #FFFFFF.
    [MonochromeBadge]::Whiten($bitmap)
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

function New-Ico([string]$path, [int[]]$sizes) {
    $images = New-Object System.Collections.Generic.List[object]

    foreach ($size in $sizes) {
        $bitmap = New-IconBitmap $size $size
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        $images.Add([pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() })
        $stream.Dispose()
    }

    $output = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($output)
    $writer.Write([UInt16]0)                       # reserved
    $writer.Write([UInt16]1)                       # type: icon
    $writer.Write([UInt16]$images.Count)

    $offset = 6 + (16 * $images.Count)
    foreach ($image in $images) {
        $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension)            # width (0 means 256)
        $writer.Write([byte]$dimension)            # height
        $writer.Write([byte]0)                     # palette size
        $writer.Write([byte]0)                     # reserved
        $writer.Write([UInt16]1)                   # colour planes
        $writer.Write([UInt16]32)                  # bits per pixel
        $writer.Write([UInt32]$image.Bytes.Length)
        $writer.Write([UInt32]$offset)
        $offset += $image.Bytes.Length
    }

    foreach ($image in $images) {
        $writer.Write([byte[]]$image.Bytes)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($path, $output.ToArray())
    $writer.Dispose()
    $output.Dispose()
}

$written = New-Object System.Collections.Generic.List[string]

function Register-Asset([string]$name) {
    $script:written.Add($name)
    Write-Host "  $name"
}

function Write-ScaledSquare([string]$baseName, [int[]]$sizes) {
    $names = @("$baseName.png", "$baseName.scale-125.png", "$baseName.scale-150.png", "$baseName.scale-200.png", "$baseName.scale-400.png")
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        New-Png (Join-Path $OutputDirectory $names[$i]) $sizes[$i] $sizes[$i]
        Register-Asset $names[$i]
    }
}

function Write-ScaledWide([string]$baseName, [int[]]$widths, [int[]]$heights, [double]$fill) {
    $names = @("$baseName.png", "$baseName.scale-125.png", "$baseName.scale-150.png", "$baseName.scale-200.png", "$baseName.scale-400.png")
    for ($i = 0; $i -lt $widths.Count; $i++) {
        New-Png (Join-Path $OutputDirectory $names[$i]) $widths[$i] $heights[$i] $fill
        Register-Asset $names[$i]
    }
}

Write-Host "Generating MSIX image assets in $OutputDirectory from $sourcePath"

# Store listing logo (Properties/Logo): 50 @ 100,125,150,200,400%
Write-ScaledSquare "StoreLogo" @(50, 63, 75, 100, 200)

# Start menu / taskbar / app list
Write-ScaledSquare "Square44x44Logo" @(44, 55, 66, 88, 176)

foreach ($target in 16, 24, 32, 48, 256) {
    New-Png (Join-Path $OutputDirectory "Square44x44Logo.targetsize-$target.png") $target $target
    Register-Asset "Square44x44Logo.targetsize-$target.png"
    New-Png (Join-Path $OutputDirectory "Square44x44Logo.altform-unplated_targetsize-$target.png") $target $target
    Register-Asset "Square44x44Logo.altform-unplated_targetsize-$target.png"
}

# Start tile sizes
Write-ScaledSquare "Square71x71Logo" @(71, 89, 107, 142, 284)
Write-ScaledSquare "Square150x150Logo" @(150, 188, 225, 300, 600)
Write-ScaledSquare "Square310x310Logo" @(310, 388, 465, 620, 1240)

# Wide tile and splash: square artwork centred on a transparent canvas.
Write-ScaledWide "Wide310x150Logo" @(310, 388, 465, 620, 1240) @(150, 188, 225, 300, 600) 0.8
Write-ScaledWide "SplashScreen" @(620, 775, 930, 1240, 2480) @(300, 375, 450, 600, 1200) 0.72

# Lock screen logo (badge): monochrome white glyph on transparency, 24 @ 100,125,150,200,400%.
$lockBadge = [MonochromeBadge]::Create($source, 45)
$lockNames = @("LockScreenLogo.png", "LockScreenLogo.scale-125.png", "LockScreenLogo.scale-150.png", "LockScreenLogo.scale-200.png", "LockScreenLogo.scale-400.png")
$lockSizes = @(24, 30, 36, 48, 96)
for ($i = 0; $i -lt $lockSizes.Count; $i++) {
    New-LockScreenPng (Join-Path $OutputDirectory $lockNames[$i]) $lockSizes[$i] $lockBadge
    Register-Asset $lockNames[$i]
}
$lockBadge.Bitmap.Dispose()

# Runtime window/title-bar/About image and the multi-resolution exe icon.
New-Png (Join-Path $AppAssetsDirectory "AppIcon.png") 256 256
New-Ico (Join-Path $AppAssetsDirectory "AppIcon.ico") @(16, 24, 32, 48, 64, 128, 256)

$source.Dispose()

Write-Host "Generated $($written.Count) MSIX assets, AppIcon.png and AppIcon.ico."
