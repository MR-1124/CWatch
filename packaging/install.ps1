# C:Watch Automated Installation Script
param(
    [switch]$CreateDesktopShortcut = $true,
    [switch]$EnableStartup = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Installing C:Watch Storage Intelligence" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\CWatch"
$PublishDir = "$PSScriptRoot\..\publish\win-x64"

# 1. Build if not published yet
if (-not (Test-Path "$PublishDir\CWatch.UI.exe")) {
    Write-Host "Application binary not found in publish directory. Running build script..." -ForegroundColor Yellow
    & "$PSScriptRoot\build.ps1"
}

# 2. Terminate existing running instance if any
cmd.exe /c "taskkill /F /IM CWatch.UI.exe /T >nul 2>&1"
Start-Sleep -Milliseconds 600

# 3. Copy binaries to install directory
Write-Host "Installing to: $InstallDir" -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Copy-Item -Path "$PublishDir\*" -Destination $InstallDir -Recurse -Force

$TargetExe = Join-Path $InstallDir "CWatch.UI.exe"

# 3. Create Start Menu Shortcut
$WshShell = New-Object -ComObject WScript.Shell
$StartMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$ShortcutPath = Join-Path $StartMenuDir "C-Watch.lnk"
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = $TargetExe
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.IconLocation = "$TargetExe,0"
$Shortcut.Description = "C:Watch - Windows Storage Intelligence and Safe Cleaner"
$Shortcut.Save()
Write-Host "Created Start Menu shortcut: $ShortcutPath" -ForegroundColor Green

# 4. Optional Desktop Shortcut
if ($CreateDesktopShortcut) {
    $DesktopDir = [Environment]::GetFolderPath("Desktop")
    $DesktopShortcutPath = Join-Path $DesktopDir "C-Watch.lnk"
    $DesktopShortcut = $WshShell.CreateShortcut($DesktopShortcutPath)
    $DesktopShortcut.TargetPath = $TargetExe
    $DesktopShortcut.WorkingDirectory = $InstallDir
    $DesktopShortcut.IconLocation = "$TargetExe,0"
    $DesktopShortcut.Description = "C:Watch - Storage Intelligence & Safe Cleaner"
    $DesktopShortcut.Save()
    Write-Host "Created Desktop shortcut: $DesktopShortcutPath" -ForegroundColor Green
}

# 5. Optional Startup registry entry
if ($EnableStartup) {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "CWatch" -Value "`"$TargetExe`" --minimized"
    Write-Host "Enabled run on Windows startup." -ForegroundColor Green
}

Write-Host "`nInstallation completed successfully! You can now launch C:Watch from the Start Menu." -ForegroundColor Green
