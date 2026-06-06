@echo off
setlocal
cd /d "%~dp0"

set "NODE_EXE=C:\Program Files\nodejs\node.exe"
if not exist "%NODE_EXE%" set "NODE_EXE=node"

"%NODE_EXE%" "%~dp0scripts\run-tsc.cjs" -b
if errorlevel 1 exit /b 1
"%NODE_EXE%" "%~dp0scripts\run-vite.cjs" build
exit /b %ERRORLEVEL%
