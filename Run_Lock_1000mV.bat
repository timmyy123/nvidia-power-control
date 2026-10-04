@echo off
cd /d "%~dp0"
echo ===================================================
echo   Locking RTX 4090 Mobile Voltage to 1000 mV (1.00V)
echo ===================================================
VfCurveTuner.exe --lock-volt 1000 2600
pause
