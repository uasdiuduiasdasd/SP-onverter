@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" (
    echo Release build failed with exit code %EXIT_CODE%.
) else (
    echo Release build completed successfully.
)

pause
exit /b %EXIT_CODE%
