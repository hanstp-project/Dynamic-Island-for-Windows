# publish.ps1 - Script untuk mem-build Windows Dynamic Island versi Standalone Portable (.exe)

$ErrorActionPreference = "Stop"

# Cari dotnet executable (prioritaskan SDK di LocalAppData jika ada)
$localDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (Test-Path $localDotnet) {
    $dotnetPath = $localDotnet
    $env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
    $env:PATH = "$($env:DOTNET_ROOT);$($env:PATH)"
} else {
    $dotnetPath = "dotnet"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Windows Dynamic Island - Standalone Release Publisher" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$outputDir = Join-Path $PSScriptRoot "publish"
if (Test-Path $outputDir) {
    Write-Host "[1/3] Membersihkan direktori publish lama..." -ForegroundColor Yellow
    Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "[2/3] Membangun Single-File Executable (Self-Contained win-x64)..." -ForegroundColor Green
$projectPath = Join-Path $PSScriptRoot "src\WindowsDynamicIsland\WindowsDynamicIsland.csproj"

& $dotnetPath publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] Gagal melakukan publish!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[3/3] Mengompres ke WindowsDynamicIsland-Portable.zip..." -ForegroundColor Green
$zipPath = Join-Path $PSScriptRoot "publish\WindowsDynamicIsland-Portable.zip"
$exePath = Join-Path $outputDir "WindowsDynamicIsland.exe"

if (Test-Path $exePath) {
    Compress-Archive -Path $exePath -DestinationPath $zipPath -Force
    Write-Host ""
    Write-Host "SUKSES! File siap dirilis:" -ForegroundColor Green
    Write-Host " - Executable: $exePath" -ForegroundColor White
    Write-Host " - ZIP Release: $zipPath" -ForegroundColor White
    Write-Host ""
    Write-Host "Silakan upload 'WindowsDynamicIsland-Portable.zip' atau 'WindowsDynamicIsland.exe' ke GitHub Releases!" -ForegroundColor Cyan
}
