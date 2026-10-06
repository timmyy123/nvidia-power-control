@echo off
:: Self-elevate if not running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~dpnx0\"\"' -Verb runas"
    exit /b
)

echo Registering MsiAfterburnerSync Scheduled Task...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$exe = '%~dp0dist\MsiAfterburnerSync.exe'; $u = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name; $a = New-ScheduledTaskAction -Execute $exe -Argument '--minimized'; $t = New-ScheduledTaskTrigger -AtLogOn; $p = New-ScheduledTaskPrincipal -UserId $u -LogonType Interactive -RunLevel Highest; $s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0; Register-ScheduledTask -TaskName 'MsiAfterburnerSync' -Action $a -Trigger $t -Principal $p -Settings $s -Force | Out-Null"

if %errorlevel% equ 0 (
    echo Task registered successfully! MsiAfterburnerSync will start silently at logon with 0 UAC prompts.
) else (
    echo Failed to register task. Error code: %errorlevel%
)
pause
