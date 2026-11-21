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
Get-ChildItem -Path $sourceDir | ForEach-Object {
    try {
        Copy-Item -Path $_.FullName -Destination $InstallPath -Recurse -Force -ErrorAction Stop
    } catch {
        if ($_.PSIsContainer) {
            $subName = $_.Name
            $targetSub = Join-Path $InstallPath $subName
            if (-not (Test-Path $targetSub)) { New-Item -ItemType Directory -Path $targetSub -Force | Out-Null }
            Get-ChildItem -Path $_.FullName -Recurse | ForEach-Object {
                $relPath = $_.FullName.Substring($_.FullName.IndexOf($subName))
                $destFile = Join-Path $InstallPath $relPath
                $destParent = Split-Path $destFile -Parent
                if (-not (Test-Path $destParent)) { New-Item -ItemType Directory -Path $destParent -Force | Out-Null }
                try { Copy-Item -Path $_.FullName -Destination $destFile -Force -ErrorAction SilentlyContinue } catch {}
            }
        }
    }
}

# Create Desktop and Start Menu Shortcuts
Write-Host "[4/6] Creating application shortcuts..." -ForegroundColor Yellow
$wsh = New-Object -ComObject WScript.Shell

$exePath = Join-Path $InstallPath "AuraWallpaper.exe"
$icoPath = Join-Path $InstallPath "assets\app.ico"
$iconLoc = if (Test-Path $icoPath) { "$icoPath,0" } else { "$exePath,0" }

# Desktop Shortcut
try {
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    $desktopLnkPath = Join-Path $desktopPath "Aura Live Wallpaper.lnk"
    if (Test-Path $desktopLnkPath) { Remove-Item $desktopLnkPath -Force -ErrorAction SilentlyContinue }
    $desktopLnk = $wsh.CreateShortcut($desktopLnkPath)
    $desktopLnk.TargetPath = $exePath
    $desktopLnk.WorkingDirectory = $InstallPath
    $desktopLnk.Description = "Aura Live Wallpaper - Desktop Live Wallpaper & Screensaver Platform"
    $desktopLnk.IconLocation = $iconLoc
    $desktopLnk.Save()
} catch {}

# Start Menu Shortcut
try {
    $startMenuPath = Join-Path ([Environment]::GetFolderPath("Programs")) "Aura Live Wallpaper"
    if (-not (Test-Path $startMenuPath)) {
        New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
    }
    $startLnkPath = Join-Path $startMenuPath "Aura Live Wallpaper.lnk"
    if (Test-Path $startLnkPath) { Remove-Item $startLnkPath -Force -ErrorAction SilentlyContinue }
    $startLnk = $wsh.CreateShortcut($startLnkPath)
    $startLnk.TargetPath = $exePath
    $startLnk.WorkingDirectory = $InstallPath
    $startLnk.Description = "Aura Live Wallpaper"
    $startLnk.IconLocation = $iconLoc
    $startLnk.Save()
} catch {}

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
try {
    $uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AuraWallpaper"
    if (-not (Test-Path $uninstallKey)) {
        New-Item -Path $uninstallKey -Force | Out-Null
    }
    Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "Aura Live Wallpaper" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.0" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "Rohit Singh Personal" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "Author" -Value "Rohit Singh" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "URLInfoAbout" -Value "https://github.com/RohitSingh-04" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "HelpLink" -Value "https://linkedin.com/in/rohiyaa" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "Comments" -Value "Built using Antigravity (Gemini 3.8 Flash)" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $iconLoc -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $InstallPath -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$InstallPath\Uninstall.ps1`"" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -Type DWord -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -Type DWord -ErrorAction SilentlyContinue
} catch {}

# Copy Uninstaller to Install Directory
try {
    $uninstallerScript = Join-Path $PSScriptRoot "Uninstall.ps1"
    if (Test-Path $uninstallerScript) {
        $destUninst = Join-Path $InstallPath "Uninstall.ps1"
        if (Test-Path $destUninst) {
            Set-ItemProperty $destUninst -Name Attributes -Value Normal -ErrorAction SilentlyContinue
        }
        Copy-Item -Path $uninstallerScript -Destination $destUninst -Force -ErrorAction SilentlyContinue
    }
} catch {}

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
