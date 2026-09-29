@echo off
title Disable Microsoft Vulnerable Driver Blocklist
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Requesting Administrator privileges...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo ===============================================================================
echo            Configure Windows for Secure Boot GPU Power Unlock
echo ===============================================================================
echo.
echo This script configures Windows to allow KDU (Kernel Driver Utility) to load
echo its temporary driver without disabling Secure Boot in UEFI.
echo.
echo Changes to be applied:
echo  1. Disable Microsoft Vulnerable Driver Blocklist (Registry CI\Config)
echo  2. Ensure Hypervisor-Enforced Code Integrity (HVCI) is disabled
echo.
echo Secure Boot in BIOS/UEFI will remain FULLY ENABLED.
echo.

echo [*] Disabling Vulnerable Driver Blocklist in Registry...
reg add "HKLM\SYSTEM\CurrentControlSet\Control\CI\Config" /v "VulnerableDriverBlocklistEnable" /t REG_DWORD /d 0 /f
if %errorLevel% neq 0 (
    echo [ERROR] Failed to update VulnerableDriverBlocklistEnable registry key.
) else (
    echo [OK] Vulnerable Driver Blocklist disabled.
)

echo.
echo [*] Disabling Memory Integrity (HVCI) in Registry...
reg add "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity" /v "Enabled" /t REG_DWORD /d 0 /f
if %errorLevel% neq 0 (
    echo [ERROR] Failed to update HVCI registry key.
) else (
    echo [OK] HVCI disabled.
)

echo.
echo ===============================================================================
echo Configuration complete!
echo.
echo IMPORTANT: You MUST RESTART your PC for these changes to take effect!
echo After restarting:
echo  - Keep Secure Boot ENABLED in BIOS.
echo  - Keep Windows Defender Real-time Protection active, but ensure this folder
echo    is added to Windows Defender Antivirus exclusions.
echo  - Run 'launch-with-secureboot.cmd' to tune and unlock your GPU!
echo ===============================================================================
echo.
pause
