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

$manifestPath = Join-Path $PSScriptRoot "Package.appxmanifest"
# makeappx treats the package as neutral unless the manifest declares an architecture,
# so stamp the target here; the source manifest stays architecture-agnostic.
$packageArchitecture = if ($Architecture -eq "ARM64") { "arm64" } else { "x64" }
$manifest = Get-Content $manifestPath -Raw
$manifest = $manifest -replace '<Identity Name=', "<Identity ProcessorArchitecture=`"$packageArchitecture`" Name="
Set-Content (Join-Path $package "AppxManifest.xml") $manifest -Encoding UTF8

# Image assets live next to the manifest and are produced from the master artwork
# (packaging/appicon.png) by Generate-Assets.ps1. Only the PNGs are packaged.
& (Join-Path $PSScriptRoot "Generate-Assets.ps1")
$assets = Join-Path $package "Assets"
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot "Assets\*.png") $assets -Force

# Fail the build instead of shipping a package whose manifest points at missing images.
$referenced = Select-String -Path $manifestPath -Pattern 'Assets\\[A-Za-z0-9._-]+\.(png|jpg|jpeg)' -AllMatches |
    ForEach-Object { $_.Matches } |
    ForEach-Object { $_.Value } |
    Sort-Object -Unique
$missing = $referenced | Where-Object { -not (Test-Path (Join-Path $package $_)) }
if ($missing) {
    throw "Manifest references image assets that are not in the package: $($missing -join ', ')"
}
Write-Host "Validated $($referenced.Count) manifest image references."

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
