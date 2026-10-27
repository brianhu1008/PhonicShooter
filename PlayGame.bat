@echo off
rem Double-click to play in the browser (the web version is not blocked by Windows Smart App Control).
rem Builds the web version first if it has not been built yet.
cd /d "%~dp0"
if not exist "Build\WebGL\index.html" (
  call "%~dp0BuildWeb.bat"
  if not exist "Build\WebGL\index.html" exit /b 1
)
rem Start a small local web server in its own minimized window, then open the game.
start "Phonic game server (close this window to stop)" /min powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\serve_web.ps1"
timeout /t 2 /nobreak >nul
start "" "http://localhost:8080/"
