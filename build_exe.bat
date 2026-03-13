@echo off
setlocal
:: Set paths
set PYTHON_EXE=%~dp0.venv\Scripts\python.exe
set SCRIPT_NAME=main.py
set EXE_NAME=StS2_Helper
set CYTHON_SRC=%~dp0.venv\Lib\site-packages\Cython\Utility
set CYTHON_DEST=%~dp0dist\%EXE_NAME%\_internal\Cython\Utility

echo [*] Checking environment...
if not exist "%PYTHON_EXE%" (
    echo [ERROR] Virtual environment not found at %PYTHON_EXE%
    pause
    exit /b 1
)

echo [*] Installing PyInstaller...
"%PYTHON_EXE%" -m pip install -U pyinstaller

echo [*] Starting PyInstaller build (One single line to avoid errors)...
"%PYTHON_EXE%" -m PyInstaller --noconfirm --clean --windowed --name "%EXE_NAME%" --add-data "result_cleaned.csv;." --collect-all paddleocr --collect-all paddle --collect-all framework --collect-all PyQt6 --collect-all setuptools --collect-all pyclipper --collect-all shapely --collect-data Cython "%SCRIPT_NAME%"
if %errorlevel% neq 0 (
    echo [ERROR] PyInstaller failed.
    pause
    exit /b 1
)

echo [*] Applying Cython patch...
if exist "%CYTHON_SRC%" (
    if not exist "%CYTHON_DEST%" mkdir "%CYTHON_DEST%"
    xcopy /E /I /Y "%CYTHON_SRC%" "%CYTHON_DEST%"
    echo [OK] Patch applied successfully.
) else (
    echo [WARNING] Cython Utility folder not found.
)

echo.
echo [COMPLETE] Your exe is at dist\%EXE_NAME%\%EXE_NAME%.exe
pause