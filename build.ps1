<#
.SYNOPSIS
    Builds, tests and packages Click to Call for Snom.

.EXAMPLE
    ./build.ps1                    # version from the .csproj
    ./build.ps1 -Version 1.2.0     # explicit version (used by the release workflow)
    ./build.ps1 -SkipTests -SkipInstaller
#>
param(
    [string]$Version,
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\ClickToCallForSnom\ClickToCallForSnom.csproj"
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"

if (-not $Version) {
    $Version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
Write-Host "Version $Version" -ForegroundColor Cyan

if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }

if (-not $SkipTests) {
    dotnet test (Join-Path $root "ClickToCallForSnom.slnx") -c Release
    if ($LASTEXITCODE) { throw "Tests failed" }
}

# Self-contained: users don't need to install .NET
dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish `
    -p:Version=$Version -p:DebugType=none
if ($LASTEXITCODE) { throw "Publish failed" }

Compress-Archive -Path "$publish\*" -DestinationPath (Join-Path $artifacts "ClickToCallForSnom-$Version-portable.zip")

if (-not $SkipInstaller) {
    $iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw "Inno Setup 6 not found (winget install JRSoftware.InnoSetup)" }

    & $iscc "/DAppVersion=$Version" "/DSourceDir=$publish" (Join-Path $root "installer\setup.iss")
    if ($LASTEXITCODE) { throw "Installer build failed" }
}

Write-Host "`nDone:" -ForegroundColor Green
Get-ChildItem $artifacts -File | ForEach-Object { "  {0} ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB) }
