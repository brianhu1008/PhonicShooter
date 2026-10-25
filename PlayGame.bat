@echo off
rem Double-click to play. Builds the game first if it has not been built yet.
cd /d "%~dp0"
if not exist "Build\PhonicShooter.exe" (
  call "%~dp0BuildGame.bat"
  if not exist "Build\PhonicShooter.exe" exit /b 1
)
start "" "Build\PhonicShooter.exe"
