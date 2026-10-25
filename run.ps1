$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"

# 1. Hentikan instance lama jika sedang berjalan
$running = Get-Process -Name WindowsDynamicIsland, dotnet -ErrorAction SilentlyContinue | Where-Object {
    if ($_.ProcessName -eq "WindowsDynamicIsland") { return $true }
    try {
        $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $($_.Id)" -ErrorAction Stop
        if ($proc -and $proc.CommandLine -like "*WindowsDynamicIsland*") { return $true }
    } catch { }
    return $false
}

if ($running) {
    foreach ($p in $running) {
        Write-Host "[Info] Menutup instance Windows Dynamic Island lama (PID: $($p.Id))..." -ForegroundColor Yellow
        try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { }
    }
    Start-Sleep -Milliseconds 600
}

# 2. Rebuild agar perubahan kode terbaru selalu dikompilasi
Write-Host "[Build] Mengompilasi versi terbaru..." -ForegroundColor Cyan
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build "$PSScriptRoot\WindowsDynamicIsland.sln" --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] Kompilasi gagal! Periksa error di atas." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 3. Jalankan aplikasi
& "$PSScriptRoot\run.bat"
