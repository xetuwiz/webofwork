@echo off
cd /d "%~dp0"

echo Running tree.py...
python tree.py

if errorlevel 1 (
    echo.
    echo tree.py failed. Stopping.
    pause
    exit /b 1
)

echo.
echo tree.py finished successfully.
echo Running script.py...

python script.py

if errorlevel 1 (
    echo.
    echo script.py failed.
    pause
    exit /b 1
)

echo.
echo Both scripts completed successfully.
pause