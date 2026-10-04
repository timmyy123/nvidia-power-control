@echo off
cd /d "%~dp0"
echo ===================================================
echo   Locking RTX 4090 Mobile Voltage to 1050 mV (1.05V)
echo ===================================================
VfCurveTuner.exe --lock-volt 1050 2650
pause
