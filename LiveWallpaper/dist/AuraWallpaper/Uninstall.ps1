<#
.SYNOPSIS
    Aura Live Wallpaper - Production Windows Standalone Uninstaller
#>

[CmdletBinding()]
param(
    [string]$InstallPath = "$env:LOCALAPPDATA\Programs\AuraWallpaper",
    [switch]$RemoveUserData = $false
)

Write-Host "==========================================================" -ForegroundColor Red
Write-Host "   AURA LIVE WALLPAPER - UNINSTALLATION                   " -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Red

# 1. Terminate running processes
Write-Host "[1/5] Stopping Aura processes..." -ForegroundColor Yellow
$procNames = @("AuraWallpaper", "AuraWallpaper.Player.Web", "AuraWallpaper.Player.Media", "AuraScreensaver", "wallctl")
foreach ($p in $procNames) {
    Get-Process -Name $p -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

# 2. Remove Shortcuts
Write-Host "[2/5] Removing application shortcuts..." -ForegroundColor Yellow
$desktopLnk = Join-Path ([Environment]::GetFolderPath("Desktop")) "Aura Live Wallpaper.lnk"
if (Test-Path $desktopLnk) { Remove-Item $desktopLnk -Force -ErrorAction SilentlyContinue }

$startMenuFolder = Join-Path ([Environment]::GetFolderPath("Programs")) "Aura Live Wallpaper"
if (Test-Path $startMenuFolder) { Remove-Item $startMenuFolder -Recurse -Force -ErrorAction SilentlyContinue }

# 3. Clean PATH variable
Write-Host "[3/5] Cleaning PATH environment variable..." -ForegroundColor Yellow
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($currentPath -like "*$InstallPath*") {
    $cleanedPath = ($currentPath.Split(';') | Where-Object { $_ -ne $InstallPath -and $_.Trim() -ne "" }) -join ';'
    [Environment]::SetEnvironmentVariable("Path", $cleanedPath, "User")
}

# 4. Remove Windows Registry Uninstall Entry
Write-Host "[4/5] Removing Windows Registry entries..." -ForegroundColor Yellow
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AuraWallpaper"
if (Test-Path $uninstallKey) {
    Remove-Item -Path $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue
}

# Remove Startup Registry
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
if (Get-ItemProperty -Path $runKey -Name "AuraWallpaper" -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $runKey -Name "AuraWallpaper" -Force -ErrorAction SilentlyContinue
}

# 5. Remove Application Files
Write-Host "[5/5] Removing installed binaries..." -ForegroundColor Yellow
if (Test-Path $InstallPath) {
    Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction SilentlyContinue
}

if ($RemoveUserData) {
    $userDataDir = Join-Path $env:LOCALAPPDATA "AuraWallpaper"
    if (Test-Path $userDataDir) {
        Remove-Item -Path $userDataDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   UNINSTALLATION COMPLETE                                " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
