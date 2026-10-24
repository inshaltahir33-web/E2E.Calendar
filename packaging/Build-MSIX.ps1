[CmdletBinding()]
param(
    [ValidateSet("x64", "ARM64")]
    [string]$Architecture = "x64",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src\E2E.Calendar.App\E2E.Calendar.App.csproj"
$output = if ($OutputDirectory) { $OutputDirectory } else { Join-Path $root "artifacts\msix\$Architecture" }
$package = Join-Path $output "package"
$bundle = Join-Path $output "E2E.Calendar-$Architecture.msix"

if (Test-Path $output) { Remove-Item $output -Recurse -Force }
New-Item -ItemType Directory -Path $package | Out-Null

dotnet publish $appProject -c $Configuration -r ("win-" + $Architecture.ToLowerInvariant()) --self-contained true -o $package -p:Platform=$Architecture
Copy-Item (Join-Path $PSScriptRoot "Package.appxmanifest") (Join-Path $package "AppxManifest.xml") -Force
$assets = Join-Path $package "Assets"
New-Item -ItemType Directory -Path $assets | Out-Null
Add-Type -AssemblyName System.Drawing
foreach ($size in @(44, 50, 150)) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(37, 99, 235))
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $blue = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(37, 99, 235), [Math]::Max(1, [int]($size / 14)))
    $inset = [int]($size * 0.24)
    $graphics.FillRectangle($white, $inset, $inset, $size - (2 * $inset), $size - $inset - $inset)
    $graphics.DrawLine($blue, $inset, [int]($size * 0.42), $size - $inset, [int]($size * 0.42))
    $graphics.DrawLine($blue, [int]($size * 0.38), [int]($size * 0.2), [int]($size * 0.38), [int]($size * 0.38))
    $graphics.DrawLine($blue, [int]($size * 0.62), [int]($size * 0.2), [int]($size * 0.62), [int]($size * 0.38))
    $name = switch ($size) { 44 { "Square44x44Logo.png" } 50 { "StoreLogo.png" } default { "Square150x150Logo.png" } }
    $bitmap.Save((Join-Path $assets $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $blue.Dispose()
    $white.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
if ($null -eq $makeAppx) {
    $makeAppx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
}
if ($null -eq $makeAppx) {
    throw "makeappx.exe was not found. Install the Windows SDK before packaging."
}

& $makeAppx.FullName pack /d $package /p $bundle /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx.exe failed with exit code $LASTEXITCODE."
}
Write-Host "Unsigned MSIX created at $bundle"
