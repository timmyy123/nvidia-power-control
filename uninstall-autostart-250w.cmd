@echo off
setlocal
title NVIDIA Power Control - Remove Autostart
cd /d "%~dp0"

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~dpnx0\"\"' -Verb runas"
    exit /b
)

schtasks /Delete /TN "NvpwrControlBlackwell" /F >nul 2>&1
schtasks /Delete /TN "MsiAfterburnerSync" /F >nul 2>&1

echo [OK] All power autostart tasks removed.
pause
