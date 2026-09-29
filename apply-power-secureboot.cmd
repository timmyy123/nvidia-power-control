@echo off
setlocal EnableDelayedExpansion
title NVIDIA GPU Power Control - CLI Power Setter (Secure Boot Mode)
cd /d "%~dp0"

:: 1. Check for Administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ===============================================================================
    echo   NVIDIA GPU Power Limit Control - CLI Setter (Secure Boot Mode)
    echo ===============================================================================
    echo.
    echo  [!] ADMINISTRATOR PRIVILEGES REQUIRED
    echo.
    echo  Please close this window, then:
    echo     1. Right-click '%~nx0'
    echo     2. Select 'Run as administrator'
    echo.
    echo ===============================================================================
    echo.
    pause
    exit /b 1
)

set PROFILE=%~1
set WATTS=%~2

if "%PROFILE%"=="" (
    echo ===============================================================================
    echo      NVIDIA GPU Power Limit Control - CLI Setter (Secure Boot Mode)
    echo ===============================================================================
    echo Supported GPU profiles:
    echo   Blackwell (50 Series): 5050, 5060, 5070, 5070ti, 5080, 5090
    echo   Ada (40 Series)      : 4050, 4060, 4070, 4080, 4090
    echo.
    set /p PROFILE="Enter GPU profile (e.g. 5070ti): "
)

if "%WATTS%"=="" (
    echo.
    echo Valid ranges (Step 5W):
    echo   5070ti    : 145 - 180 W
    echo   5070/5060 : 120 - 140 W
    echo   5080/5090 : 175 - 225 W
    echo   4090      : 150 - 250 W
    echo   4080      : 150 - 225 W
    echo   4070/4060 : 120 - 150 W
    echo.
    set /p WATTS="Enter Target Power in Watts (e.g. 160): "
)

if "%PROFILE%"=="" (
    echo [ERROR] No GPU profile provided.
    pause
    exit /b 1
)
if "%WATTS%"=="" (
    echo [ERROR] No wattage provided.
    pause
    exit /b 1
)

echo.
echo [*] Applying %WATTS%W power limit for profile %PROFILE%...

:: Verify binaries
if not exist "%~dp0kdu.exe" (
    echo [ERROR] kdu.exe not found or blocked by Windows Defender in %~dp0
    pause
    exit /b 1
)
if not exist "%~dp0drv64.dll" (
    echo [ERROR] drv64.dll not found in %~dp0
    pause
    exit /b 1
)
if not exist "%~dp0Nvpwr.sys" (
    echo [ERROR] Nvpwr.sys not found in %~dp0
    pause
    exit /b 1
)
if not exist "%~dp0NvpwrCtl.exe" (
    echo [ERROR] NvpwrCtl.exe not found in %~dp0
    pause
    exit /b 1
)

:: Clean up any existing service
sc stop Nvpwr >nul 2>&1
sc delete Nvpwr >nul 2>&1

:: Disable DSE
echo [1/5] Temporarily disabling DSE (DSE 0)...
"%~dp0kdu.exe" -dse 0
if %errorLevel% neq 0 (
    echo [ERROR] KDU failed to disable DSE. Ensure Memory Integrity is OFF and blocklist is disabled.
    "%~dp0kdu.exe" -dse 6 >nul 2>&1
    pause
    exit /b 1
)

:: Create & Start service
echo [2/5] Starting Nvpwr kernel service...
sc create Nvpwr binPath= "%~dp0Nvpwr.sys" type= kernel start= demand >nul 2>&1
sc start Nvpwr >nul 2>&1

:: Apply Power Setting via CLI
echo [3/5] Applying power limit via NvpwrCtl...
"%~dp0NvpwrCtl.exe" set %PROFILE% %WATTS%
set SET_EXIT=%errorLevel%

:: Stop and delete service
echo [4/5] Unloading Nvpwr kernel driver...
sc stop Nvpwr >nul 2>&1
sc delete Nvpwr >nul 2>&1

:: Restore DSE
echo [5/5] Restoring Driver Signature Enforcement (DSE 6)...
"%~dp0kdu.exe" -dse 6

echo.
if %SET_EXIT% equ 0 (
    echo ===============================================================================
    echo [SUCCESS] Power limit applied successfully!
    echo Driver unloaded, DSE restored, Secure Boot remains ON.
    echo ===============================================================================
) else (
    echo [WARNING] NvpwrCtl reported an error (code %SET_EXIT%).
)

pause
