@echo off
cd /d "%~dp0"
echo ===================================================
echo   Resetting RTX 4090 Mobile V/F Curve to Factory
echo ===================================================
VfCurveTuner.exe --reset
pause
