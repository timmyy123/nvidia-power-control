@echo off
net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~dpnx0\"\"' -Verb runas"
    exit /b
)

schtasks /Delete /TN "MsiAfterburnerSync" /F
echo Task MsiAfterburnerSync removed.
pause
