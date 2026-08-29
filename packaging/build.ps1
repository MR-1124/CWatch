# C:Watch Automated Build & Publish Script
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\publish\win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building & Publishing C:Watch Desktop" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$dotnet = if (Test-Path "$HOME\.dotnet\dotnet.exe") { "$HOME\.dotnet\dotnet.exe" } else { "dotnet" }

Write-Host "1. Restoring NuGet dependencies..." -ForegroundColor Yellow
& $dotnet restore "$PSScriptRoot\..\CWatch.sln"

Write-Host "2. Running Unit & Integration Tests..." -ForegroundColor Yellow
& $dotnet test "$PSScriptRoot\..\tests\CWatch.Tests\CWatch.Tests.csproj" -c $Configuration --no-restore

Write-Host "3. Publishing self-contained Windows application..." -ForegroundColor Yellow
& $dotnet publish "$PSScriptRoot\..\src\CWatch.UI\CWatch.UI.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -o $OutputDir

Write-Host "`nBuild succeeded! Output directory: $OutputDir" -ForegroundColor Green
