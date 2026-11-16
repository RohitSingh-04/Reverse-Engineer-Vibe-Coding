@echo off
title Uninstall Aura Live Wallpaper
echo Launching Aura Live Wallpaper Uninstaller...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
pause
