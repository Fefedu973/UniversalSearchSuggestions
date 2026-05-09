param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Version = "1.0.0.0",

    [ValidateSet("x64", "arm64")]
    [string[]]$Platforms = @("x64", "arm64")
)

$ErrorActionPreference = "Stop"

$ProjectDir = $PSScriptRoot
$ProjectFile = Join-Path $ProjectDir "UniversalSearchSuggestions.csproj"
$InstallerDir = Join-Path $ProjectDir "bin\$Configuration\installer"
$InnoSetupPath = "${env:ProgramFiles(x86)}\Inno Setup 6\iscc.exe"
if (-not (Test-Path $InnoSetupPath)) {
    $InnoSetupPath = "${env:ProgramFiles}\Inno Setup 6\iscc.exe"
}

if (-not (Test-Path $InnoSetupPath)) {
    throw "Inno Setup 6 was not found. Install it from https://jrsoftware.org/isdl.php."
}

New-Item -ItemType Directory -Path $InstallerDir -Force | Out-Null

foreach ($Platform in $Platforms) {
    $RuntimeIdentifier = "win-$Platform"
    $PublishDir = Join-Path $ProjectDir "bin\$Configuration\$RuntimeIdentifier\publish"

    Write-Host "Publishing $RuntimeIdentifier..." -ForegroundColor Cyan
    dotnet publish $ProjectFile `
        --configuration $Configuration `
        --runtime $RuntimeIdentifier `
        --self-contained true `
        --output $PublishDir `
        -p:PublishProfile= `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=true

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $RuntimeIdentifier with exit code $LASTEXITCODE."
    }

    $SetupTemplate = Get-Content (Join-Path $ProjectDir "setup-template.iss") -Raw
    $SetupScript = $SetupTemplate `
        -replace '#define AppVersion ".*"', "#define AppVersion `"$Version`"" `
        -replace 'OutputBaseFilename=UniversalSearchSuggestions-Setup-\{#AppVersion\}', "OutputBaseFilename=UniversalSearchSuggestions-Setup-{#AppVersion}-$Platform" `
        -replace 'Source: "bin\\Release\\win-x64\\publish\\\*"', "Source: `"bin\$Configuration\$RuntimeIdentifier\publish\*`""

    if ($Platform -eq "arm64") {
        $SetupScript = $SetupScript -replace '(\[Setup\]\r?\n)', "`$1ArchitecturesAllowed=arm64`r`nArchitecturesInstallIn64BitMode=arm64`r`n"
    }
    else {
        $SetupScript = $SetupScript -replace '(\[Setup\]\r?\n)', "`$1ArchitecturesAllowed=x64compatible`r`nArchitecturesInstallIn64BitMode=x64compatible`r`n"
    }

    $SetupScriptPath = Join-Path $ProjectDir "setup-$Platform.iss"
    $SetupScript | Set-Content -Path $SetupScriptPath -Encoding UTF8

    Write-Host "Building installer for $RuntimeIdentifier..." -ForegroundColor Cyan
    & $InnoSetupPath $SetupScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed for $RuntimeIdentifier with exit code $LASTEXITCODE."
    }
}

Get-ChildItem $InstallerDir -Filter "*.exe" | Select-Object FullName, Length
