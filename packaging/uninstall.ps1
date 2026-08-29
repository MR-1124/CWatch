# C:Watch Automated Uninstaller Script
param(
    [switch]$RemoveUserData = $false
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Uninstalling C:Watch" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Terminate running process if any
Stop-Process -Name "CWatch.UI" -Force -ErrorAction SilentlyContinue

# 2. Remove shortcuts
$StartMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\C-Watch.lnk"
if (Test-Path $StartMenuShortcut) {
    Remove-Item -Path $StartMenuShortcut -Force
    Write-Host "Removed Start Menu shortcut." -ForegroundColor Green
}

$DesktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "C-Watch.lnk"
if (Test-Path $DesktopShortcut) {
    Remove-Item -Path $DesktopShortcut -Force
    Write-Host "Removed Desktop shortcut." -ForegroundColor Green
}

# 3. Remove startup registry
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "CWatch" -ErrorAction SilentlyContinue

# 4. Remove installation binaries
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\CWatch"
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
    Write-Host "Removed application files." -ForegroundColor Green
}

# 5. User data / database removal
if ($RemoveUserData) {
    $DataDir = Join-Path $env:LOCALAPPDATA "CWatch"
    if (Test-Path $DataDir) {
        Remove-Item -Path $DataDir -Recurse -Force
        Write-Host "Removed user database and settings." -ForegroundColor Green
    }
} else {
    Write-Host "User historical database and settings preserved under: $env:LOCALAPPDATA\CWatch" -ForegroundColor Yellow
}

Write-Host "`nC:Watch has been uninstalled." -ForegroundColor Green
