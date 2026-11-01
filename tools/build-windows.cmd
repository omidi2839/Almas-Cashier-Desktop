@echo off
setlocal
cd /d "%~dp0\.."
if not exist "AlmasCashierDesktop\AlmasCashierDesktop.csproj" (
  echo Files were not extracted correctly. Extract the complete ZIP to a folder first.
  pause
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-windows.ps1"
set CODE=%ERRORLEVEL%
if not "%CODE%"=="0" echo Build failed. Read the messages above.
pause
exit /b %CODE%
