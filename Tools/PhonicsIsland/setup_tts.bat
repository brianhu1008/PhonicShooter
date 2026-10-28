@echo off
rem Sets up the Python environment used to record Phonics Island voices (Edge Natural voice, edge-tts).
rem Needs Python 3 installed. Only needed once per computer.
cd /d "%~dp0"
python -m venv ttsenv || (echo Python 3 is not installed. & pause & exit /b 1)
ttsenv\Scripts\python.exe -m pip install --disable-pip-version-check edge-tts
echo Done.
pause
