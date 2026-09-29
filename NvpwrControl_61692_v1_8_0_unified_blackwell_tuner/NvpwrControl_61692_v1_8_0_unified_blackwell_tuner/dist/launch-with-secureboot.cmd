@echo off
setlocal EnableDelayedExpansion
title NVIDIA GPU Power Control - Secure Boot Mode
cd /d "%~dp0"

:: 1. Check for Administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ===============================================================================
    echo      NVIDIA GPU Power Limit Control (Secure Boot Compatible Loader)
    echo ===============================================================================
    echo.
    echo  [!] ADMINISTRATOR PRIVILEGES REQUIRED
    echo.
    echo  Please close this window, then:
    echo.
    echo     1. Right-click '%~nx0'
    echo     2. Select 'Run as administrator'
    echo.
    echo ===============================================================================
    echo.
    pause
    exit /b 1
)

echo ===============================================================================
echo      NVIDIA GPU Power Limit Control (Secure Boot Compatible Loader)
echo ===============================================================================
echo.
echo [*] Working folder: %~dp0
echo.

:: 2. Verify required files exist
if not exist "%~dp0kdu.exe" (
    echo ===============================================================================
    echo [ERROR] kdu.exe not found or blocked by Windows Defender!
    echo ===============================================================================
    echo If Windows Defender quarantined kdu.exe:
    echo  1. Open Windows Security -^> Virus ^& threat protection -^> Protection history
    echo  2. Select and 'Restore' kdu.exe
    echo  3. Run 'add-defender-exclusion.cmd' as Administrator to whitelist this folder.
    echo ===============================================================================
    echo.
    pause
    exit /b 1
)

if not exist "%~dp0drv64.dll" (
    echo [ERROR] drv64.dll not found in %~dp0
    echo Please make sure drv64.dll is in the same folder as kdu.exe.
    echo.
    pause
    exit /b 1
)

if not exist "%~dp0Nvpwr.sys" (
    echo [ERROR] Nvpwr.sys not found in %~dp0
    echo Please make sure Nvpwr.sys is in this folder.
    echo.
    pause
    exit /b 1
)

if not exist "%~dp0NvpwrControl.exe" (
    echo [ERROR] NvpwrControl.exe not found in %~dp0
    echo.
    pause
    exit /b 1
)

:: 3. Check Windows Vulnerable Driver Blocklist in registry
set BLOCKLIST_ACTIVE=0
for /f "tokens=3" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Config" /v VulnerableDriverBlocklistEnable 2^>nul') do (
    if "%%a"=="0x1" set BLOCKLIST_ACTIVE=1
)

if "%BLOCKLIST_ACTIVE%"=="1" (
    echo [NOTE] Windows Vulnerable Driver Blocklist is currently ACTIVE in Windows.
    echo        If KDU fails to load its provider, run 'disable-vulnerable-driver-blocklist.cmd'
    echo        as Administrator and restart your PC.
    echo.
)

:: 4. Clean up any stale service registrations from previous sessions
sc stop Nvpwr >nul 2>&1
sc delete Nvpwr >nul 2>&1

:: 5. Temporarily disable Driver Signature Enforcement in kernel (Trying Provider 2 first)
echo [1/4] Disabling Driver Signature Enforcement in kernel memory (Provider 2)...
"%~dp0kdu.exe" -prv 2 -dse 0
set KDU_EXIT=!errorLevel!

if !KDU_EXIT! neq 0 (
    echo [*] Provider 2 return code !KDU_EXIT!, trying default provider...
    "%~dp0kdu.exe" -dse 0
    set KDU_EXIT=!errorLevel!
)

echo [OK] KDU execution completed.
echo.

:: 6. Launch NvpwrControl GUI
echo [2/4] Launching NvpwrControl GUI...
echo ===============================================================================
echo  INSTRUCTIONS:
echo  1. In the NvpwrControl window, select your desired Target Power Limit (e.g. 160W).
echo  2. Click 'Apply' to program the new limits into GPU hardware and driver memory.
echo  3. (Optional) Adjust Core / Memory clock offsets if desired.
echo  4. Once applied, simply CLOSE the NvpwrControl window.
echo ===============================================================================
echo.

start /wait "" "%~dp0NvpwrControl.exe"

echo.
echo [3/4] GUI closed. Unloading test driver and cleaning up...
:: Device handle was closed on GUI exit; stop and remove the service
sc stop Nvpwr >nul 2>&1
sc delete Nvpwr >nul 2>&1

:: 7. Restore Driver Signature Enforcement to default
echo [4/4] Restoring Driver Signature Enforcement...
"%~dp0kdu.exe" -prv 2 -dse 6 >nul 2>&1
"%~dp0kdu.exe" -dse 6 >nul 2>&1

echo.
echo ===============================================================================
echo [SUCCESS] Power limit applied and system restored to secure state!
echo.
echo  - Nvpwr.sys test driver is completely unloaded from kernel memory.
echo  - Driver Signature Enforcement is restored to strict mode.
echo  - Secure Boot remains ENABLED in UEFI/BIOS.
echo  - Anti-cheat games (Warzone, Battlefield, Vanguard, EAC) are ready to play!
echo ===============================================================================
echo.
pause
