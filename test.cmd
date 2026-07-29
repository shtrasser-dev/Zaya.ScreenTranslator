@echo off
setlocal

set PLUGINS_DIR=%APPDATA%\Zaya\ScreenTranslator\plugins

echo Stopping any running ScreenTranslator instances...
taskkill /f /im Zaya.ScreenTranslator.Impl.Windows.exe 2>nul
taskkill /f /im Zaya.ScreenTranslator.Impl.Unix.exe 2>nul

echo === Building Zaya.OCR plugins ===
pushd "%~dp0..\Zaya.OCR"
call build.cmd
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%
popd

echo === Preparing plugins directory ===
if exist "%PLUGINS_DIR%" rmdir /s /q "%PLUGINS_DIR%"
mkdir "%PLUGINS_DIR%"

echo === Copying OCR plugins ===
copy /y "%~dp0..\Zaya.OCR\out\*.zip" "%PLUGINS_DIR%"
call :VerifyCopy "OCR" "%~dp0..\Zaya.OCR\out" "%PLUGINS_DIR%"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo === Building Zaya.Screenshot plugin ===
pushd "%~dp0..\Zaya.Screenshot"
call build.cmd
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%
popd

echo === Copying Screenshot plugin ===
copy /y "%~dp0..\Zaya.Screenshot\out\*.zip" "%PLUGINS_DIR%"
call :VerifyCopy "Screenshot" "%~dp0..\Zaya.Screenshot\out" "%PLUGINS_DIR%"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo === Building Zaya.Translator plugin ===
pushd "%~dp0..\Zaya.Translator"
call build.cmd
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%
popd

echo === Copying Translator plugin ===
if not exist "%~dp0..\Zaya.Translator\out\*.zip" (
    echo ERROR: No translator zip files found — Zaya.Translator\build.cmd may have failed
    dir "%~dp0..\Zaya.Translator\out"
    exit /b 1
)
copy /y "%~dp0..\Zaya.Translator\out\*.zip" "%PLUGINS_DIR%"
call :VerifyCopy "Translator" "%~dp0..\Zaya.Translator\out" "%PLUGINS_DIR%"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo === Plugins directory contents ===
dir /b "%PLUGINS_DIR%"

echo Done. Plugins in %PLUGINS_DIR%
goto :eof

:: Verify that zip files from source were copied to destination
:VerifyCopy
    if %ERRORLEVEL% neq 0 (
        echo ERROR: Copy failed for %~1 plugins
        dir "%~2\*.zip"
        exit /b %ERRORLEVEL%
    )
    for /f %%a in ('dir /b "%~3\*.zip" 2^>nul ^| find /c /v ""') do set COUNT=%%a
    echo   %~1: %COUNT% plugin file(s) in %~3
    exit /b 0
