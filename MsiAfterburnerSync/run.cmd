@echo off
cd /d "%~dp0dist"
if exist "MsiAfterburnerSync.exe" (
    start "" "MsiAfterburnerSync.exe"
) else (
    echo MsiAfterburnerSync.exe not found. Running build.ps1 first...
    powershell -ExecutionPolicy Bypass -File ..\build.ps1
    start "" "MsiAfterburnerSync.exe"
)
