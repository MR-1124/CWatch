# Capture screenshots for the README.

# Run the app, manually navigate to each page, then press Enter in this
# console to capture the named screenshot. Requires the app window title
# to contain "C:Watch".

param(
    [string]$OutDir = "$PSScriptRoot/assets"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$shots = @(
    @{ Name = "screenshot-dashboard"; Prompt = "Navigate to the Dashboard, then press Enter" },
    @{ Name = "screenshot-explorer";  Prompt = "Navigate to Storage Explorer (with a scan loaded), then press Enter" },
    @{ Name = "screenshot-cleanup";   Prompt = "Navigate to Cleanup with candidates listed, then press Enter" },
    @{ Name = "screenshot-history";   Prompt = "Navigate to History & Trends, then press Enter" }
)

Write-Host "This script captures the C:Watch window for the README."
Write-Host "Start the app first (dotnet run or the installed exe)."

foreach ($shot in $shots) {
    Write-Host ""
    Write-Host $shot.Prompt
    Read-Host "Press Enter when ready"

    $proc = Get-Process CWatch.UI -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1

    if (-not $proc) {
        Write-Warning "C:Watch window not found; skipping $($shot.Name)."
        continue
    }

    Add-Type @"
    using System;
    using System.Runtime.InteropServices;
    public class Win32 {
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        public struct RECT { public int Left, Top, Right, Bottom; }
    }
"@

    [Win32]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 400

    $rect = New-Object Win32+RECT
    [Win32]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top

    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)

    $path = Join-Path $OutDir "$($shot.Name).png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $bmp.Dispose()
    Write-Host "Saved $path" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Review the images, then commit them to docs/assets/."
