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

# Determine source binaries location
if (Test-Path "$PSScriptRoot\CWatch.UI.exe") {
    $SourceDir = $PSScriptRoot
} elseif (Test-Path "$PSScriptRoot\..\publish\win-x64\CWatch.UI.exe") {
    $SourceDir = "$PSScriptRoot\..\publish\win-x64"
} elseif (Test-Path "$PSScriptRoot\build.ps1") {
    Write-Host "Application binary not found in publish directory. Running build script..." -ForegroundColor Yellow
    & "$PSScriptRoot\build.ps1"
    $SourceDir = "$PSScriptRoot\..\publish\win-x64"
} else {
    Write-Error "C:Watch application binaries could not be located."
    exit 1
}

# 1. Terminate existing running instance if any
cmd.exe /c "taskkill /F /IM CWatch.UI.exe /T >nul 2>&1"
Start-Sleep -Milliseconds 600

# 2. Copy binaries to install directory
Write-Host "Installing to: $InstallDir" -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Get-ChildItem -Path $SourceDir | ForEach-Object {
    if ($_.Name -notin @("Setup.cmd", "Uninstall.cmd", "install.ps1", "uninstall.ps1", "Setup.exe")) {
        Copy-Item -Path $_.FullName -Destination $InstallDir -Recurse -Force
    }
}

# Copy uninstall script into install dir
if (Test-Path "$PSScriptRoot\uninstall.ps1") {
    Copy-Item -Path "$PSScriptRoot\uninstall.ps1" -Destination $InstallDir -Force
}
if (Test-Path "$PSScriptRoot\Uninstall.cmd") {
    Copy-Item -Path "$PSScriptRoot\Uninstall.cmd" -Destination $InstallDir -Force
}

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

# 5. Register in Windows Add/Remove Programs
try {
    $UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CWatch"
    if (-not (Test-Path $UninstallKey)) {
        New-Item -Path $UninstallKey -Force | Out-Null
    }
    Set-ItemProperty -Path $UninstallKey -Name "DisplayName" -Value "C:Watch Storage Intelligence"
    Set-ItemProperty -Path $UninstallKey -Name "DisplayVersion" -Value "1.0.0"
    Set-ItemProperty -Path $UninstallKey -Name "Publisher" -Value "MR-1124"
    Set-ItemProperty -Path $UninstallKey -Name "DisplayIcon" -Value (Join-Path $InstallDir "Assets\app.ico")
    Set-ItemProperty -Path $UninstallKey -Name "UninstallString" -Value "`"$(Join-Path $InstallDir 'uninstall.cmd')`""
    Set-ItemProperty -Path $UninstallKey -Name "InstallLocation" -Value $InstallDir
    Set-ItemProperty -Path $UninstallKey -Name "InstallDate" -Value (Get-Date -Format "yyyyMMdd")
    Set-ItemProperty -Path $UninstallKey -Name "EstimatedSize" -Value 75000
    Set-ItemProperty -Path $UninstallKey -Name "NoModify" -Value 1
    Set-ItemProperty -Path $UninstallKey -Name "NoRepair" -Value 1
} catch { }

# 6. Optional Startup registry entry
if ($EnableStartup) {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "CWatch" -Value "`"$TargetExe`" --minimized"
    Write-Host "Enabled run on Windows startup." -ForegroundColor Green
}

Write-Host "`nInstallation completed successfully! You can now launch C:Watch from the Start Menu." -ForegroundColor Green
