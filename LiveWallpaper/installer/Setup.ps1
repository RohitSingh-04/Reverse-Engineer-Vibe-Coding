<#
.SYNOPSIS
    Aura Live Wallpaper - Production Windows Standalone Installer
.DESCRIPTION
    Installs Aura Live Wallpaper, isolated player runtimes, wallctl CLI, and native screensaver.
#>

[CmdletBinding()]
param(
    [string]$InstallPath = "$env:LOCALAPPDATA\Programs\AuraWallpaper",
    [switch]$LaunchAfterInstall = $true,
    [switch]$Silent = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   AURA LIVE WALLPAPER - WINDOWS SETUP & INSTALLER       " -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan

# Terminate existing running instances before installation
Write-Host "[1/6] Stopping any running Aura processes..." -ForegroundColor Yellow
$procNames = @("AuraWallpaper", "AuraWallpaper.Player.Web", "AuraWallpaper.Player.Media", "AuraScreensaver", "wallctl")
foreach ($p in $procNames) {
    Get-Process -Name $p -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

# Determine source directory
$sourceDir = Join-Path $PSScriptRoot "..\dist\AuraWallpaper"
if (-not (Test-Path $sourceDir)) {
    $sourceDir = $PSScriptRoot
}

if (-not (Test-Path (Join-Path $sourceDir "AuraWallpaper.exe"))) {
    Write-Error "Source binaries not found in '$sourceDir'. Run build first."
}

# Prepare installation directory
Write-Host "[2/6] Preparing installation target: $InstallPath" -ForegroundColor Yellow
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Copy files
Write-Host "[3/6] Deploying application binaries, players, and assets..." -ForegroundColor Yellow
Copy-Item -Path "$sourceDir\*" -Destination $InstallPath -Recurse -Force

# Create Desktop and Start Menu Shortcuts
Write-Host "[4/6] Creating application shortcuts..." -ForegroundColor Yellow
$wsh = New-Object -ComObject WScript.Shell

$exePath = Join-Path $InstallPath "AuraWallpaper.exe"
$icoPath = Join-Path $InstallPath "assets\app.ico"
$iconLoc = if (Test-Path $icoPath) { "$icoPath,0" } else { "$exePath,0" }

# Desktop Shortcut
$desktopPath = [Environment]::GetFolderPath("Desktop")
$desktopLnk = $wsh.CreateShortcut((Join-Path $desktopPath "Aura Live Wallpaper.lnk"))
$desktopLnk.TargetPath = $exePath
$desktopLnk.WorkingDirectory = $InstallPath
$desktopLnk.Description = "Aura Live Wallpaper - Desktop Live Wallpaper & Screensaver Platform"
$desktopLnk.IconLocation = $iconLoc
$desktopLnk.Save()

# Start Menu Shortcut
$startMenuPath = Join-Path ([Environment]::GetFolderPath("Programs")) "Aura Live Wallpaper"
if (-not (Test-Path $startMenuPath)) {
    New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
}
$startLnk = $wsh.CreateShortcut((Join-Path $startMenuPath "Aura Live Wallpaper.lnk"))
$startLnk.TargetPath = $exePath
$startLnk.WorkingDirectory = $InstallPath
$startLnk.Description = "Aura Live Wallpaper"
$startLnk.IconLocation = $iconLoc
$startLnk.Save()

# Register wallctl in User PATH
Write-Host "[5/6] Registering 'wallctl' command in User PATH environment..." -ForegroundColor Yellow
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($currentPath -notlike "*$InstallPath*") {
    $newPath = "$currentPath;$InstallPath"
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    $env:Path = "$env:Path;$InstallPath"
}

# Register Windows Uninstall Entry in Registry
Write-Host "[6/6] Registering Windows Add/Remove Programs configuration..." -ForegroundColor Yellow
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AuraWallpaper"
if (-not (Test-Path $uninstallKey)) {
    New-Item -Path $uninstallKey -Force | Out-Null
}
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "Aura Live Wallpaper"
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "Rohit Singh Personal"
Set-ItemProperty -Path $uninstallKey -Name "Author" -Value "Rohit Singh"
Set-ItemProperty -Path $uninstallKey -Name "URLInfoAbout" -Value "https://github.com/RohitSingh-04"
Set-ItemProperty -Path $uninstallKey -Name "HelpLink" -Value "https://linkedin.com/in/rohiyaa"
Set-ItemProperty -Path $uninstallKey -Name "Comments" -Value "Built using Antigravity (Gemini 3.8 Flash)"
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $iconLoc
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $InstallPath
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$InstallPath\Uninstall.ps1`""
Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -Type DWord

# Copy Uninstaller to Install Directory
$uninstallerScript = Join-Path $PSScriptRoot "Uninstall.ps1"
if (Test-Path $uninstallerScript) {
    Copy-Item -Path $uninstallerScript -Destination (Join-Path $InstallPath "Uninstall.ps1") -Force
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   INSTALLATION COMPLETED SUCCESSFULLY!                  " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Installed to: $InstallPath" -ForegroundColor White
Write-Host "Executable:   $exePath" -ForegroundColor White
Write-Host "CLI Utility:  wallctl (available in PowerShell / CMD)" -ForegroundColor White
Write-Host "Screensaver:  $InstallPath\AuraScreensaver.scr" -ForegroundColor White

if ($LaunchAfterInstall) {
    Write-Host "Launching Aura Live Wallpaper..." -ForegroundColor Cyan
    Start-Process -FilePath $exePath -WorkingDirectory $InstallPath
}
