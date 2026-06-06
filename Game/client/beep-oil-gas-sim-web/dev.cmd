@echo off
setlocal
cd /d "%~dp0"

set "NODE_EXE=C:\Program Files\nodejs\node.exe"
if not exist "%NODE_EXE%" set "NODE_EXE=node"

"%NODE_EXE%" "%~dp0scripts\run-vite.cjs" %*
exit /b %ERRORLEVEL%
