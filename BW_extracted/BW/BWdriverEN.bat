@echo off
chcp 65001 > nul
:: Check for administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
echo Please run this file as Administrator!
pause
exit /b
)

echo [1/4] Loading overclocking driver into kernel memory...
cd /d "C:\BW"
kdu.exe -dse 0

echo.
echo [2/4] Launching graphical overclocking utility...
echo CONFIGURE FREQUENCIES AND POWER IN THE INTERFACE.
echo ONCE YOU APPLY THE SETTINGS, SIMPLY CLOSE THE OVERCLOCKING UTILITY.
echo.

:: Launch the program and WAIT for it to close (/wait flag)
start /wait NvpwrControl.exe

echo.
echo [3/4] Program closed. Unloading driver...
:: If the driver supports standard service-based unloading:
sc stop BlackwellDriver >nul 2>&1
sc delete BlackwellDriver >nul 2>&1
kdu.exe -dse 6

:: Cleaning up cache and traces...
echo [4/4] Memory cleared. System ready to launch the game.
echo You may now launch the anti-cheat.
timeout /t 10