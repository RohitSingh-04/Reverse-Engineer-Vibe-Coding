<#
.SYNOPSIS
    Packages the Aura Live Wallpaper Release into Portable ZIP and Setup archives.
#>

$ProgressPreference = 'SilentlyContinue'
$distPath = Join-Path $PSScriptRoot "dist\AuraWallpaper"
$zipPortable = Join-Path $PSScriptRoot "dist\AuraWallpaper_v1.0.0_Portable_x64.zip"
$zipSetup = Join-Path $PSScriptRoot "dist\AuraWallpaper_v1.0.0_Setup.zip"
$stagingPath = Join-Path $PSScriptRoot "dist\staging_pkg"

if (Test-Path $stagingPath) { Remove-Item $stagingPath -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null

# Copy to staging directory (robocopy handles shared-read locks gracefully)
& robocopy $distPath $stagingPath /E /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null

if (Test-Path $zipPortable) { Remove-Item $zipPortable -Force }
if (Test-Path $zipSetup) { Remove-Item $zipSetup -Force }

Write-Host "Creating Portable ZIP package: $zipPortable" -ForegroundColor Cyan
Compress-Archive -Path "$stagingPath\*" -DestinationPath $zipPortable -CompressionLevel Optimal

Write-Host "Creating Standalone Setup ZIP package: $zipSetup" -ForegroundColor Cyan
Compress-Archive -Path "$stagingPath\*" -DestinationPath $zipSetup -CompressionLevel Optimal

Remove-Item $stagingPath -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Distribution packages generated successfully!" -ForegroundColor Green
Get-Item $zipPortable, $zipSetup | Select-Object Name, Length, LastWriteTime
