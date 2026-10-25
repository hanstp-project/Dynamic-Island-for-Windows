@echo off
title Windows Dynamic Island
set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "DOTNET_ROOT(x64)=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

echo [OK] Menjalankan Windows Dynamic Island...
start "" "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" "%~dp0src\WindowsDynamicIsland\bin\Debug\net10.0-windows10.0.19041.0\WindowsDynamicIsland.dll"
echo [OK] Windows Dynamic Island berhasil dijalankan!
