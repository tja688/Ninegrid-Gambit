@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

echo.
echo Ninegrid Gambit - Unity Editor -automated
echo Project: %ROOT%
echo.
echo Direct Unity.exe launch per AGENTS.md / unity-cli rules.
echo Optional arg: -SkipIfRunning
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\.cursor\skills\unity-automated-launch\scripts\Start-UnityAutomated.ps1" -ProjectPath "%ROOT%" %*
if errorlevel 1 goto :failed

echo.
echo Unity Editor is ready: -automated, Pipeline reachable.
exit /b 0

:failed
echo.
echo Launch failed.
pause
exit /b 1
