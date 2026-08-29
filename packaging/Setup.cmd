@echo off
setlocal
cd /d "%~dp0"
title Installing C:Watch Storage Intelligence
echo ==========================================
echo  Installing C:Watch Storage Intelligence
echo ==========================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if %ERRORLEVEL% equ 0 (
    echo Launching C:Watch...
    start "" "%LOCALAPPDATA%\Programs\CWatch\CWatch.UI.exe"
) else (
    echo Installation failed. Press any key to exit.
    pause >nul
)
