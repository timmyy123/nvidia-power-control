@echo off
setlocal
title NVIDIA Power Control - Install 250W & MSI Center Auto-Reapply Autostart
cd /d "%~dp0"

:: Request elevation if not admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~dpnx0\"\"' -Verb runas"
    exit /b
)

echo ===============================================================================
echo     Registering MsiAfterburnerSync (250W + OC Auto-Apply & Auto-Reapply)
echo ===============================================================================
echo.

:: 1. Remove obsolete tasks
schtasks /Delete /TN "NvpwrControlBlackwell" /F >nul 2>&1

:: 2. Target executable
set "EXE_PATH=%~dp0MsiAfterburnerSync.exe"
if not exist "%EXE_PATH%" set "EXE_PATH=%~dp0MsiAfterburnerSync\dist\MsiAfterburnerSync.exe"

echo [*] Target executable: %EXE_PATH%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$exe = '%EXE_PATH%';" ^
    "$u = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name;" ^
    "$a = New-ScheduledTaskAction -Execute $exe -Argument '--minimized';" ^
    "$t = New-ScheduledTaskTrigger -AtLogOn;" ^
    "$p = New-ScheduledTaskPrincipal -UserId $u -LogonType Interactive -RunLevel Highest;" ^
    "$s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0;" ^
    "Register-ScheduledTask -TaskName 'MsiAfterburnerSync' -Action $a -Trigger $t -Principal $p -Settings $s -Force | Out-Null;"

if %errorlevel% equ 0 (
    echo ===============================================================================
    echo [SUCCESS] Task 'MsiAfterburnerSync' registered successfully!
    echo.
    echo  - Runs at logon with Highest privileges (0 UAC prompts).
    echo  - Automatically applies 250W Power Limit and MSI Afterburner Profile 1 on boot.
    echo  - Automatically reapplies 250W and Profile 1 on MSI Center power mode switches.
    echo  - 0%% CPU usage (event-driven via registry kernel notifications).
    echo  - Fast 11-12s reboot is preserved.
    echo ===============================================================================
    echo.
    echo Starting MsiAfterburnerSync now in background...
    start "" "%EXE_PATH%" --minimized
) else (
    echo [ERROR] Failed to register task. Code: %errorlevel%
)

echo.
pause
