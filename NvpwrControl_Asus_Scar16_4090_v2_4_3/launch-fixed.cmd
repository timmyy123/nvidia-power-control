@echo off
echo Updating and launching NvpwrControl...
taskkill /f /im NvpwrControl.exe >nul 2>&1
timeout /t 1 /nobreak >nul
if exist "%~dp0dist\NvpwrControl_Universal.exe" (
    copy /y "%~dp0dist\NvpwrControl_Universal.exe" "%~dp0dist\NvpwrControl.exe" >nul
    start "" "%~dp0dist\NvpwrControl.exe"
) else if exist "%~dp0NvpwrControl_Universal.exe" (
    copy /y "%~dp0NvpwrControl_Universal.exe" "%~dp0NvpwrControl.exe" >nul
    start "" "%~dp0NvpwrControl.exe"
) else (
    start "" "%~dp0dist\NvpwrControl.exe"
)
