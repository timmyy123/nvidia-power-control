@echo off
cd /d "%~dp0"
echo =========================================================
echo   Applying NVIDIA Kernel Driver Voltage Limit Override
echo   Disabling VRel and VOp hard clamps in nvlddmkm.sys
echo =========================================================
echo.

:: Check for administrative rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] This script must be run as Administrator!
    echo Please right-click Apply_Voltage_Unlock.bat and choose "Run as administrator".
    echo.
    pause
    exit /b 1
)

mkdir state 2>nul
echo [*] Backing up current driver registry configuration...
reg export "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000" "state\backup-before-voltage-unlock.reg" /y >nul
if %errorlevel% equ 0 (
    echo [OK] Registry backup saved to state\backup-before-voltage-unlock.reg
) else (
    echo [WARNING] Could not create registry backup, proceeding anyway...
)

echo [*] Importing driver overrides (RmPerfLimitsOverride=0x50, RMEnableOverclockingAllPstates=1)...
reg import Unlock_Voltage_Limits.reg
if %errorlevel% equ 0 (
    echo.
    echo =========================================================
    echo   [SUCCESS] Voltage limit overrides installed into driver!
    echo =========================================================
    echo.
    echo A system REBOOT is required for nvlddmkm.sys to initialize
    echo with VRel / VOp clamps disabled.
    echo.
    echo After reboot:
    echo  1. Launch your workload / GPU-Z Render Test.
    echo  2. Use VfCurveTuner or MSI Afterburner to set 1000 mV+ target.
    echo  3. Voltage will scale past 0.940V without VRel throttling!
    echo.
) else (
    echo [ERROR] Failed to import registry overrides.
)

pause
