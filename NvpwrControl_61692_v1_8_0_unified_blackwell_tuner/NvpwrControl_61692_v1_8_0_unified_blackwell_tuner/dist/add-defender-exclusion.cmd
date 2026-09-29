@echo off
title Add Windows Defender Exclusion
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ===============================================================================
    echo                     ADMINISTRATOR PERMISSION REQUIRED
    echo ===============================================================================
    echo.
    echo Please right-click 'add-defender-exclusion.cmd' and select "Run as administrator".
    echo.
    echo ===============================================================================
    pause
    exit /b 1
)

echo ===============================================================================
echo                Add Folder to Windows Defender Exclusions
echo ===============================================================================
echo.
echo Adding folder to Microsoft Defender Antivirus exclusions:
echo "%~dp0"
echo.

powershell -NoProfile -Command "Add-MpPreference -ExclusionPath '%~dp0'"
if %errorLevel% equ 0 (
    echo [OK] Successfully added "%~dp0" to Defender Exclusions!
    echo Windows Defender will no longer block KDU.exe or driver tools in this folder.
) else (
    echo [ERROR] Failed to add Defender exclusion. Please check your Administrator rights.
)

echo.
pause
