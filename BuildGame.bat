@echo off
rem Build the game into Build\PhonicShooter.exe (takes a few minutes the first time)
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
set "PROJECT=%~dp0"
if "%PROJECT:~-1%"=="\" set "PROJECT=%PROJECT:~0,-1%"

if not exist "%UNITY%" (
  echo Unity 6000.6.7f1 not found at:
  echo   %UNITY%
  echo Please edit BuildGame.bat and set UNITY to your Unity.exe path.
  pause
  exit /b 1
)

if not exist "%PROJECT%\Build" mkdir "%PROJECT%\Build"
echo Building Phonic Shooter... please wait.
"%UNITY%" -batchmode -quit -projectPath "%PROJECT%" -executeMethod BuildScript.BuildWindows -logFile "%PROJECT%\Build\build.log"
if errorlevel 1 (
  echo Build FAILED. See Build\build.log
  echo If the project is open in the Unity Editor, close it and try again.
  pause
  exit /b 1
)
echo Build finished: Build\PhonicShooter.exe
endlocal
