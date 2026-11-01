[CmdletBinding()]
param(
    [ValidateSet("x64", "ARM64")]
    [string]$Architecture = "x64",

    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [string]$OutputDirectory = ""
)

# Builds the unsigned E2E.Calendar MSIX for x64 and ARM64 via the Windows SDK `makeappx.exe`
# and packs both architectures into a single .msixbundle under artifacts/msix/.
# The Store upload format (.msixupload) is produced by the Partner Center pipeline, not by
# makeappx, so this script stops at the .msixbundle.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src\E2E.Calendar.App\E2E.Calendar.App.csproj"
$packaging = Join-Path $root "packaging"

$output = if ($OutputDirectory) { $OutputDirectory } else { Join-Path $root "artifacts\msix" }
$forbiddenSegmentNames = @("docs", "scripts", "tests", "Developer")

if ($Configuration -ne "Release") {
    throw "Store builds must be Release. Pass -Configuration Release."
}

function Invoke-MakeAppx {
    # Look for makeappx.exe in order of preference:
    #   1. PATH
    #   2. %USERPROFILE%\.nuget\packages\microsoft.windows.sdk.buildtools\<version>\bin\<rid>\makeappx.exe (highest version first)
    #   3. C:\Program Files (x86)\Windows Kits\10\bin\<version>\x64\makeappx.exe
    $candidates = @()

    $fromPath = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if ($null -ne $fromPath) {
        $candidates += $fromPath.Source
    }

    $nugetRoot = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools"
    if (Test-Path $nugetRoot) {
        $versions = Get-ChildItem -LiteralPath $nugetRoot -Directory | Sort-Object Name -Descending
        foreach ($version in $versions) {
            $candidate = Get-ChildItem -LiteralPath (Join-Path $version.FullName "bin") -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
                Sort-Object FullName -Descending |
                Select-Object -First 1
            if ($null -ne $candidate) {
                $candidates += $candidate.FullName
            }
        }
    }

    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $kits = Get-ChildItem -LiteralPath $kitsRoot -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($null -ne $kits) {
            $candidates += $kits.FullName
        }
    }

    $found = $candidates | Select-Object -First 1
    if ($null -ne $found -and (Test-Path $found)) {
        return $found
    }

    throw "Install the Windows 10/11 SDK"
}

function New-PackagePayload {
    param([string]$Arch)

    $publishDir = Join-Path $output $Arch
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    New-Item -ItemType Directory -Path $publishDir | Out-Null

    dotnet publish $appProject -c $Configuration -r ("win-" + $Arch.ToLowerInvariant()) --self-contained true -o $publishDir -p:Platform=$Arch

    Copy-Item (Join-Path $packaging "Package.appxmanifest") (Join-Path $publishDir "AppxManifest.xml") -Force

    & (Join-Path $packaging "Generate-Assets.ps1") -OutputDirectory (Join-Path $packaging "Assets")
    New-Item -ItemType Directory -Path (Join-Path $publishDir "Assets") | Out-Null
    Copy-Item (Join-Path $packaging "Assets\*.png") (Join-Path $publishDir "Assets") -Force

    $referenced = Select-String -Path (Join-Path $packaging "Package.appxmanifest") -Pattern 'Assets\\[A-Za-z0-9._-]+\.(png|jpg|jpeg)' -AllMatches |
        ForEach-Object { $_.Matches } |
        ForEach-Object { $_.Value } |
        Sort-Object -Unique
    $missing = $referenced | Where-Object { -not (Test-Path (Join-Path $publishDir $_)) }
    if ($missing) {
        throw "Manifest references image assets that are not in the package: $($missing -join ', ')"
    }

    return $publishDir
}

function Get-ForbiddenFiles {
    param([string]$PackageRoot)

    $forbidden = New-Object System.Collections.Generic.List[string]

    foreach ($file in (Get-ChildItem -LiteralPath $PackageRoot -Recurse -File)) {
        $segments = ($file.FullName -replace '\\', "/").Split("/")
        $hit = $segments | Where-Object { $forbiddenSegmentNames -contains $_ }

        if ($null -ne $hit) {
            $forbidden.Add($file.FullName)
            continue
        }

        if ($file.Extension -in @(".pdb", ".ps1")) {
            $forbidden.Add($file.FullName)
            continue
        }

        if ($file.Name -eq "AGENTS.md") {
            $forbidden.Add($file.FullName)
            continue
        }
    }

    return $forbidden
}

function Assert-ValidPackage {
    param([string]$PackageRoot)

    Write-Host ":: Listing MSIX package contents under '$PackageRoot'"
    Get-ChildItem -LiteralPath $PackageRoot -Recurse -File | ForEach-Object { Write-Host (":: " + $_.FullName) }

    $forbidden = Get-ForbiddenFiles -PackageRoot $PackageRoot

    if (@($forbidden).Count -gt 0) {
        throw "Refusing to pack E2E.Calendar-${Arch}: package contains forbidden files: $(@($forbidden) -join ', ')."
    }
}

if (Test-Path $output) { Remove-Item $output -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $output "x64") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $output "arm64") | Out-Null

$makeAppx = Invoke-MakeAppx
$bundles = @()

foreach ($arch in @("x64", "ARM64")) {
    Write-Host ":: Publishing and packing E2E.Calendar ($arch)"
    $packageDir = New-PackagePayload -Arch $arch
    $bundle = Join-Path $output "$arch\E2E.Calendar.$($arch).msix"
    & $makeAppx pack /d $packageDir /p $bundle /o
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx.exe failed with exit code $LASTEXITCODE while packing $arch."
    }
    Assert-ValidPackage -PackageRoot $packageDir
    $bundles += $bundle
}

Write-Host ":: Bundling x64 and ARM64 packages into E2E.Calendar.msixbundle"
& $makeAppx bundle /d $output /p (Join-Path $output "E2E.Calendar.msixbundle") /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx.exe failed with exit code $LASTEXITCODE while bundling."
}

Write-Host "Unsigned MSIX bundle created at $(Join-Path $output 'E2E.Calendar.msixbundle')"
Write-Host "MSIX per-architecture artifacts: $($bundles -join ', ')"

