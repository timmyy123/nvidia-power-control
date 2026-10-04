@echo off
cd /d "%~dp0"
echo =========================================================
echo   Restoring NVIDIA Kernel Driver Factory Limits
echo =========================================================
echo.

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] This script must be run as Administrator!
    pause
    exit /b 1
)

reg import Restore_Stock_Limits.reg
if %errorlevel% equ 0 (
    echo [SUCCESS] Overrides removed. Reboot to restore factory driver limits.
) else (
    echo [ERROR] Failed to restore registry.
)

pause
