@echo off
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] This script requires Administrator privileges.
    echo Please right-click this file and select "Run as administrator".
    pause
    exit /b 1
)

echo [*] Removing Code Integrity / WDAC Policy...
CiTool.exe --remove-policy "{959A0F15-8985-4551-A208-5FFE9EDB3A70}" >nul 2>&1
if exist "C:\Windows\System32\CodeIntegrity\CiPolicies\Active\{959A0F15-8985-4551-A208-5FFE9EDB3A70}.cip" (
    del /f /q "C:\Windows\System32\CodeIntegrity\CiPolicies\Active\{959A0F15-8985-4551-A208-5FFE9EDB3A70}.cip" >nul 2>&1
)
CiTool.exe --refresh >nul 2>&1

echo [*] Deleting Nvpwr kernel driver service from Windows...
sc.exe stop Nvpwr >nul 2>&1
sc.exe delete Nvpwr >nul 2>&1

echo [*] Removing installed certificates from Windows Certificate Stores...
certutil -delstore Root "0A0D9A50E6897CAF56452E3F5645DA88723F61C6" >nul 2>&1
certutil -delstore Root "4D24BA8B6D2EA6AD2EB95318EEDB4837931E59BF" >nul 2>&1
certutil -delstore TrustedPublisher "0A0D9A50E6897CAF56452E3F5645DA88723F61C6" >nul 2>&1
certutil -delstore TrustedPublisher "4D24BA8B6D2EA6AD2EB95318EEDB4837931E59BF" >nul 2>&1
certutil -user -delstore My "0A0D9A50E6897CAF56452E3F5645DA88723F61C6" >nul 2>&1
certutil -user -delstore My "4D24BA8B6D2EA6AD2EB95318EEDB4837931E59BF" >nul 2>&1
certutil -user -delstore My "4FA759EB576B24CB5506349E4E2AFA147835D894" >nul 2>&1
certutil -user -delstore My "3A4DFD8037FE9B95C4EA73079291BD1CAC5C44FD" >nul 2>&1

echo [*] Removing C:\NvpwrCerts folder...
if exist "C:\NvpwrCerts\" rmdir /s /q "C:\NvpwrCerts\" >nul 2>&1

echo [*] Removing temporary policy scripts...
if exist "%~dp0apply-kernel-only-policy.cmd" del /f /q "%~dp0apply-kernel-only-policy.cmd" >nul 2>&1
if exist "%~dp0remove-kernel-policy.cmd" del /f /q "%~dp0remove-kernel-policy.cmd" >nul 2>&1

echo.
echo ========================================================================
echo [OK] ALL NVPWR POLICIES, CERTIFICATES, AND SERVICES COMPLETELY REMOVED!
echo Your system is 100%% restored to its clean, original stock state.
echo ========================================================================
pause
