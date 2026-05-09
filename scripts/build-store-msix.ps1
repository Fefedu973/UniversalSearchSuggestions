param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$AppxPackageDir = "AppPackages"
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectDir = Join-Path $root "UniversalSearchSuggestions"
$project = Join-Path $projectDir "UniversalSearchSuggestions.csproj"
$outputRoot = Join-Path $projectDir $AppxPackageDir

foreach ($platform in @("x64", "ARM64")) {
    $platformDir = Join-Path $outputRoot $platform
    Write-Host "Building MSIX for $platform..." -ForegroundColor Cyan
    dotnet build $project `
        --configuration $Configuration `
        -p:GenerateAppxPackageOnBuild=true `
        -p:Platform=$platform `
        -p:AppxPackageDir="$platformDir\"

    if ($LASTEXITCODE -ne 0) {
        throw "MSIX build failed for $platform with exit code $LASTEXITCODE."
    }
}

$msixFiles = Get-ChildItem -Path $outputRoot -Recurse -Filter "*.msix" |
    Where-Object { $_.FullName -notmatch "\\Dependencies\\" } |
    Sort-Object FullName

if ($msixFiles.Count -ne 2) {
    throw "Expected x64 and ARM64 MSIX packages under $outputRoot."
}

$makeAppx = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $makeAppx) {
    throw "makeappx.exe was not found. Install the Windows SDK or run the bundle step on a machine that has it."
}

$version = ([xml](Get-Content $project)).Project.PropertyGroup.AppxPackageVersion | Select-Object -First 1
$bundleName = "UniversalSearchSuggestions_${version}_Bundle.msixbundle"
$bundlePath = Join-Path $projectDir $bundleName
$mappingPath = Join-Path $projectDir "bundle_mapping.txt"

$mapping = "[Files]`r`n"
foreach ($file in $msixFiles) {
    $mapping += "`"$($file.FullName)`" `"$($file.Name)`"`r`n"
}

$mapping | Set-Content -Path $mappingPath -Encoding ASCII

Write-Host "Creating $bundleName..." -ForegroundColor Cyan
& $makeAppx.FullName bundle /v /f $mappingPath /p $bundlePath
if ($LASTEXITCODE -ne 0) {
    throw "makeappx bundle failed with exit code $LASTEXITCODE."
}

Get-Item $bundlePath
