# ==============================================================================
# Script dong goi ban build CadElectricalToolkit de dang len GitHub Releases
# Cach dung: 
#   powershell -ExecutionPolicy Bypass -File tools/package.ps1
#   hoac truyen version: powershell -ExecutionPolicy Bypass -File tools/package.ps1 -Version "1.0.0"
# ==============================================================================

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$DistDir = Join-Path $RootDir "dist"

Write-Host "`n[1/3] Dang compile CadElectricalToolkit (Cau hinh Release)..." -ForegroundColor Cyan
dotnet build "$RootDir\src\CadElectricalToolkit\CadElectricalToolkit.csproj" -c Release

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

$BundleSource = Join-Path $RootDir "bundle\CadElectricalToolkit.bundle"
$ZipTarget = Join-Path $DistDir "CadElectricalToolkit-v$Version-bundle.zip"
$DllSource = Join-Path $RootDir "src\CadElectricalToolkit\bin\Release\net48\CadElectricalToolkit.dll"
$DllTarget = Join-Path $DistDir "CadElectricalToolkit.dll"

Write-Host "[2/3] Dang dong goi Bundle thanh file zip: $ZipTarget ..." -ForegroundColor Cyan
if (Test-Path $ZipTarget) { Remove-Item $ZipTarget -Force }
Compress-Archive -Path $BundleSource -DestinationPath $ZipTarget -Force

Write-Host "[3/3] Dang sao chep DLL doc lap: $DllTarget ..." -ForegroundColor Cyan
Copy-Item $DllSource -Destination $DllTarget -Force

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host " HOAN TAT DONG GOI! Thu muc xuat file: $DistDir" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host "Cac file san sang de keo-tha len GitHub Releases:" -ForegroundColor Yellow
Write-Host "  1. CadElectricalToolkit-v$Version-bundle.zip (Tu dong load qua ApplicationPlugins)" -ForegroundColor White
Write-Host "  2. CadElectricalToolkit.dll (Dung lenh NETLOAD thu cong)" -ForegroundColor White
Write-Host "`nHuong dan: Mo https://github.com/2Naq/Automation-AutoCad/releases -> Create Release -> Keo 2 file tren vao.`n" -ForegroundColor Cyan
