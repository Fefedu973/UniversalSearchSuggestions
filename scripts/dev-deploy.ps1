param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",

    [switch]$SkipBuild,
    [switch]$KeepRunningExtension,
    [switch]$SkipReload
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $root "UniversalSearchSuggestions\UniversalSearchSuggestions.csproj"

if (-not $KeepRunningExtension) {
    Get-Process -Name "UniversalSearchSuggestions" -ErrorAction SilentlyContinue | Stop-Process -Force
}

Get-AppxPackage -Name "UniversalSearchSuggestions" -ErrorAction SilentlyContinue |
    Remove-AppxPackage -PreserveApplicationData

if (-not $SkipBuild) {
    dotnet build $project `
        -c $Configuration `
        -p:Platform=$Platform `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageSigningEnabled=false

    if ($LASTEXITCODE -ne 0) {
        throw "MSIX package build failed with exit code $LASTEXITCODE."
    }
}

$rid = if ($Platform -eq "ARM64") { "win-arm64" } else { "win-x64" }
$appPackagesRoot = Join-Path $root "UniversalSearchSuggestions\bin\$Platform\$Configuration\net9.0-windows10.0.22621.0\$rid\AppPackages"
$msix = Get-ChildItem -Path $appPackagesRoot -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $msix -or -not (Test-Path $msix)) {
    throw "MSIX package not found. Run this script without -SkipBuild first."
}

$unpacked = Join-Path (Split-Path $msix -Parent) "Unpacked"
if (Test-Path $unpacked) {
    $resolvedUnpacked = (Resolve-Path $unpacked).Path
    if (-not $resolvedUnpacked.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unpacked directory outside workspace: $resolvedUnpacked"
    }

    Remove-Item -LiteralPath $resolvedUnpacked -Recurse -Force
}

New-Item -ItemType Directory -Path $unpacked | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($msix, $unpacked)
$manifest = Join-Path $unpacked "AppxManifest.xml"

Add-AppxPackage -Register $manifest -ForceApplicationShutdown -ForceUpdateFromAnyVersion -ErrorAction Stop

if (-not $SkipReload) {
    try {
        Start-Process "x-cmdpal://reload"
    }
    catch {
        Write-Warning "Package registered, but Command Palette reload URI could not be opened. Reload Command Palette manually."
    }
}

Write-Host "Universal Search Suggestions registered from $manifest"
