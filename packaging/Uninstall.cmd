@echo off
setlocal
cd /d "%~dp0"
title Uninstalling C:Watch
echo ==========================================
echo  Uninstalling C:Watch
echo ==========================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
echo.
echo Press any key to exit.
pause >nul
