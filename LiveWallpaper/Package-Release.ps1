<#
.SYNOPSIS
    Packages the Aura Live Wallpaper Release into Portable ZIP and Setup archives.
#>

$distPath = Join-Path $PSScriptRoot "dist\AuraWallpaper"
$zipPortable = Join-Path $PSScriptRoot "dist\AuraWallpaper_v1.0.0_Portable_x64.zip"
$zipSetup = Join-Path $PSScriptRoot "dist\AuraWallpaper_v1.0.0_Setup.zip"

if (Test-Path $zipPortable) { Remove-Item $zipPortable -Force }
if (Test-Path $zipSetup) { Remove-Item $zipSetup -Force }

Write-Host "Creating Portable ZIP package: $zipPortable" -ForegroundColor Cyan
Compress-Archive -Path "$distPath\*" -DestinationPath $zipPortable -CompressionLevel Optimal

Write-Host "Creating Standalone Setup ZIP package: $zipSetup" -ForegroundColor Cyan
Compress-Archive -Path "$distPath\*" -DestinationPath $zipSetup -CompressionLevel Optimal

Write-Host "Distribution packages generated successfully!" -ForegroundColor Green
Get-Item $zipPortable, $zipSetup | Select-Object Name, Length, LastWriteTime
