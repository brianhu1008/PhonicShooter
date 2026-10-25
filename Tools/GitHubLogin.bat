@echo off
rem Log in to GitHub on this computer (needed once before uploading).
echo ============================================================
echo  GitHub login
echo  1. When asked "Authenticate Git with your GitHub credentials?" press Enter
echo  2. Copy the 8-character code shown, then press Enter
echo  3. In the browser: paste the code, click Continue, then Authorize
echo ============================================================
echo.
gh auth logout -h github.com -u brianhu1008 >nul 2>&1
gh auth login -h github.com -p https -w
echo.
gh auth status
echo.
pause
