@echo off
setlocal EnableDelayedExpansion
title NVIDIA GPU Power Control - Status (Secure Boot Mode)

cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Requesting Administrator privileges...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo [*] Reading NVIDIA GPU power status with Secure Boot ON...

sc stop Nvpwr >nul 2>&1
sc delete Nvpwr >nul 2>&1

"%~dp0kdu.exe" -dse 0 >nul 2>&1
sc create Nvpwr binPath= "%~dp0Nvpwr.sys" type= kernel start= demand >nul 2>&1
sc start Nvpwr >nul 2>&1

echo.
"%~dp0NvpwrCtl.exe" status
echo.

sc stop Nvpwr >nul 2>&1
sc delete Nvpwr >nul 2>&1
"%~dp0kdu.exe" -dse 6 >nul 2>&1

echo [*] Driver unloaded and DSE restored to 6.
pause
