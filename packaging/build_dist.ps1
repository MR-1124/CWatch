# C:Watch Release Packaging & Distribution Builder
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$RootDir = "$PSScriptRoot\.."
$DistDir = "$RootDir\dist"
$PublishDir = "$RootDir\publish\win-x64"
$InstallerProj = "$RootDir\src\CWatch.Installer\CWatch.Installer.csproj"
$PayloadZip = "$RootDir\src\CWatch.Installer\payload.zip"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building C:Watch Distribution Packages v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Clean previous dist and publish directories
if (Test-Path $DistDir) { Remove-Item -Path $DistDir -Recurse -Force }
if (Test-Path $PublishDir) { Remove-Item -Path $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

$dotnet = if (Test-Path "$HOME\.dotnet\dotnet.exe") { "$HOME\.dotnet\dotnet.exe" } else { "dotnet" }

# 2. Publish main application as self-contained Windows x64 binaries
Write-Host "Publishing C:Watch UI self-contained binaries..." -ForegroundColor Yellow
& $dotnet publish "$RootDir\src\CWatch.UI\CWatch.UI.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Main UI publish failed!"
    exit 1
}

# 3. Create Payload Zip for Installer embedding
Write-Host "Compressing binaries into installer payload..." -ForegroundColor Yellow
if (Test-Path $PayloadZip) { Remove-Item $PayloadZip -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $PayloadZip -Force

# 4. Build GUI Installer as Single-File Self-Contained Setup.exe
Write-Host "Compiling standalone GUI Setup wizard (Self-Contained Single-File)..." -ForegroundColor Yellow
$InstallerPublishDir = "$RootDir\publish\installer"
if (Test-Path $InstallerPublishDir) { Remove-Item -Path $InstallerPublishDir -Recurse -Force }

& $dotnet publish $InstallerProj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    -o $InstallerPublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Installer publish failed!"
    exit 1
}

$SetupExe = "$InstallerPublishDir\Setup.exe"

# 5. Create Standalone Setup Executable in dist
Copy-Item $SetupExe -Destination "$DistDir\CWatch-Setup-v$Version.exe" -Force
Write-Host "Created Standalone Setup Wizard: $DistDir\CWatch-Setup-v$Version.exe" -ForegroundColor Green

# 6. Create Portable Zip
$PortableZip = "$DistDir\CWatch-v$Version-win-x64-Portable.zip"
Write-Host "Creating Portable archive: $PortableZip" -ForegroundColor Yellow
Compress-Archive -Path "$PublishDir\*" -DestinationPath $PortableZip -Force

# 7. Create Setup Installer Zip Package
$SetupStaging = "$DistDir\staging-setup"
if (Test-Path $SetupStaging) { Remove-Item -Path $SetupStaging -Recurse -Force }
New-Item -ItemType Directory -Path $SetupStaging -Force | Out-Null

Copy-Item -Path "$PublishDir\*" -Destination $SetupStaging -Recurse -Force
Copy-Item $SetupExe -Destination "$SetupStaging\Setup.exe" -Force
Copy-Item "$PSScriptRoot\Setup.cmd" -Destination $SetupStaging -Force
Copy-Item "$PSScriptRoot\Uninstall.cmd" -Destination $SetupStaging -Force
Copy-Item "$PSScriptRoot\install.ps1" -Destination $SetupStaging -Force
Copy-Item "$PSScriptRoot\uninstall.ps1" -Destination $SetupStaging -Force

$SetupZip = "$DistDir\CWatch-v$Version-win-x64-Setup.zip"
Write-Host "Creating Setup archive: $SetupZip" -ForegroundColor Yellow
Compress-Archive -Path "$SetupStaging\*" -DestinationPath $SetupZip -Force
Remove-Item -Path $SetupStaging -Recurse -Force

# Clean up embedded payload scratch file
if (Test-Path $PayloadZip) { Remove-Item $PayloadZip -Force }

Write-Host "`nAll release distribution packages built successfully in: $DistDir" -ForegroundColor Green
Get-ChildItem -Path $DistDir | Select-Object Name, Length, LastWriteTime
