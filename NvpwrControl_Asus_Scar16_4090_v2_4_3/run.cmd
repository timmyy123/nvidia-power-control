@echo off
if not exist "%~dp0dist\NvpwrControl.exe" (
  echo NvpwrControl.exe is not built yet. Running build...
  call "%~dp0build.cmd"
)
if exist "%~dp0dist\NvpwrControl.exe" start "" "%~dp0dist\NvpwrControl.exe"
