@echo off
setlocal EnableExtensions
REM Cursor hook host may not inherit nvm/Node PATH. Resolve node + run sibling JS.
REM %~dp0 keeps the script cwd-independent.

set "HOOK_JS=%~dp0run-hook.js"
set "NODE_EXE="

if exist "C:\nvm4w\nodejs\node.exe" set "NODE_EXE=C:\nvm4w\nodejs\node.exe"
if not defined NODE_EXE if exist "%ProgramFiles%\nodejs\node.exe" set "NODE_EXE=%ProgramFiles%\nodejs\node.exe"
if not defined NODE_EXE if exist "%LOCALAPPDATA%\Programs\node\node.exe" set "NODE_EXE=%LOCALAPPDATA%\Programs\node\node.exe"
if not defined NODE_EXE if exist "%USERPROFILE%\AppData\Local\nvm\nodejs\node.exe" set "NODE_EXE=%USERPROFILE%\AppData\Local\nvm\nodejs\node.exe"

if not defined NODE_EXE (
  where node >nul 2>&1
  if not errorlevel 1 (
    for /f "delims=" %%I in ('where node 2^>nul') do (
      set "NODE_EXE=%%I"
      goto :run
    )
  )
)

:run
if not defined NODE_EXE (
  echo {"permission":"deny","user_message":"Subagent hook: node.exe not found (PATH / nvm / Program Files).","agent_message":"Subagent hook could not start node. Do not spawn subagents; continue in the parent agent."}
  exit /b 2
)

if not exist "%HOOK_JS%" (
  echo {"permission":"deny","user_message":"Subagent hook: missing run-hook.js next to run-hook.cmd.","agent_message":"Subagent hook script missing. Do not spawn subagents; continue in the parent agent."}
  exit /b 2
)

"%NODE_EXE%" "%HOOK_JS%"
exit /b %ERRORLEVEL%
