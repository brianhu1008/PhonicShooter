@echo off
rem Build the web version (for iPad Safari) into Build\WebGL and zip it as Build\PhonicShooter_Web.zip
rem Upload the zip to itch.io. Takes several minutes (longer the first time).
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
set "PROJECT=%~dp0"
if "%PROJECT:~-1%"=="\" set "PROJECT=%PROJECT:~0,-1%"

if not exist "%UNITY%" (
  echo Unity 6000.6.7f1 not found at:
  echo   %UNITY%
  echo Please edit BuildWeb.bat and set UNITY to your Unity.exe path.
  pause
  exit /b 1
)

if not exist "%PROJECT%\Build" mkdir "%PROJECT%\Build"
echo Building web version... please wait.
"%UNITY%" -batchmode -quit -projectPath "%PROJECT%" -buildTarget WebGL -executeMethod BuildScript.BuildWebGL -logFile "%PROJECT%\Build\build_web.log"
if errorlevel 1 (
  echo Build FAILED. See Build\build_web.log
  echo If the project is open in the Unity Editor, close it and try again.
  pause
  exit /b 1
)
powershell -NoProfile -Command "Compress-Archive -Path '%PROJECT%\Build\WebGL\*' -DestinationPath '%PROJECT%\Build\PhonicShooter_Web.zip' -Force"
rem Copy into docs\ for GitHub Pages
robocopy "%PROJECT%\Build\WebGL" "%PROJECT%\docs" /MIR /XF .nojekyll /NFL /NDL /NJH /NJS >nul
if not exist "%PROJECT%\docs\.nojekyll" type nul > "%PROJECT%\docs\.nojekyll"
echo Done: Build\WebGL (also copied to docs\ for GitHub Pages) and Build\PhonicShooter_Web.zip
echo To publish: git add -A, git commit, git push
endlocal
