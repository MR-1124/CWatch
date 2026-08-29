# C:Watch Release Packaging & Distribution Builder
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$RootDir = "$PSScriptRoot\.."
$DistDir = "$RootDir\dist"
$PublishDir = "$RootDir\publish\win-x64"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building C:Watch Distribution Packages v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Clean previous dist directory
if (Test-Path $DistDir) {
    Remove-Item -Path $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

# 2. Publish self-contained Windows x64 binaries
Write-Host "Compiling and publishing self-contained win-x64 release..." -ForegroundColor Yellow
$dotnet = if (Test-Path "$HOME\.dotnet\dotnet.exe") { "$HOME\.dotnet\dotnet.exe" } else { "dotnet" }

& $dotnet publish "$RootDir\src\CWatch.UI\CWatch.UI.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish build failed!"
    exit 1
}

# 3. Create Portable Zip
$PortableZip = "$DistDir\CWatch-v$Version-win-x64-Portable.zip"
Write-Host "Creating Portable archive: $PortableZip" -ForegroundColor Yellow
Compress-Archive -Path "$PublishDir\*" -DestinationPath $PortableZip -Force

# 4. Create Setup Installer Package
$SetupStaging = "$DistDir\staging-setup"
if (Test-Path $SetupStaging) { Remove-Item -Path $SetupStaging -Recurse -Force }
New-Item -ItemType Directory -Path $SetupStaging -Force | Out-Null

# Copy binaries
Copy-Item -Path "$PublishDir\*" -Destination $SetupStaging -Recurse -Force

# Copy installer scripts to root of staging
Copy-Item -Path "$PSScriptRoot\Setup.cmd" -Destination $SetupStaging -Force
Copy-Item -Path "$PSScriptRoot\Uninstall.cmd" -Destination $SetupStaging -Force
Copy-Item -Path "$PSScriptRoot\install.ps1" -Destination $SetupStaging -Force
Copy-Item -Path "$PSScriptRoot\uninstall.ps1" -Destination $SetupStaging -Force

$SetupZip = "$DistDir\CWatch-v$Version-win-x64-Setup.zip"
Write-Host "Creating Setup archive: $SetupZip" -ForegroundColor Yellow
Compress-Archive -Path "$SetupStaging\*" -DestinationPath $SetupZip -Force

Remove-Item -Path $SetupStaging -Recurse -Force

Write-Host "`nRelease packages generated successfully in: $DistDir" -ForegroundColor Green
Get-ChildItem -Path $DistDir | Select-Object Name, Length, LastWriteTime
