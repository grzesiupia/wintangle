<#
.SYNOPSIS
    Builds the wintangle self-contained binary and Inno Setup installer.

.PARAMETER Version
    The application version string (default: 1.0.1).

.EXAMPLE
    .\build\build-installer.ps1 -Version 1.0.2
#>

[CmdletBinding()]
param(
    [string]$Version = "1.0.1"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

Push-Location $repoRoot
try {
    Write-Host "==> Publishing wintangle for win-x64 (Release, self-contained)..." -ForegroundColor Cyan
    $publishDir = Join-Path $repoRoot "artifacts\publish"
    
    dotnet publish "src\Wintangle.App" `
        -c Release `
        -r win-x64 `
        --self-contained `
        -p:PublishSingleFile=true `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    Write-Host "==> Locating Inno Setup compiler (ISCC.exe)..." -ForegroundColor Cyan
    $iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1

    if (-not $iscc) {
        $candidatePaths = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
            "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
        )
        foreach ($cand in $candidatePaths) {
            if (Test-Path $cand) {
                $iscc = $cand
                break
            }
        }
    }

    if (-not $iscc) {
        throw "Inno Setup compiler (ISCC.exe) not found. Please install Inno Setup 6 (e.g., 'choco install innosetup' or 'winget install JRSoftware.InnoSetup') or add ISCC.exe to PATH."
    }

    Write-Host "==> Compiling installer with $iscc (Version: $Version)..." -ForegroundColor Cyan
    $issFile = Join-Path $scriptDir "setup.iss"
    
    & $iscc $issFile "/DAppVersion=$Version" "/DSourceDir=$publishDir"

    if ($LASTEXITCODE -ne 0) {
        throw "ISCC compiler failed with exit code $LASTEXITCODE"
    }

    $outputSetup = Join-Path $scriptDir "Output\wintangle-setup.exe"
    Write-Host "==> Installer generated successfully: $outputSetup" -ForegroundColor Green
}
finally {
    Pop-Location
}
