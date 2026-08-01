@echo off
setlocal

set PLUGINS_DIR=%APPDATA%\Zaya\ScreenTranslator\plugins
set EXTRACT_DIR=%APPDATA%\Zaya\ScreenTranslator\temp\plugins

echo Stopping any running ScreenTranslator instances...
taskkill /f /im Zaya.ScreenTranslator.exe 2>nul
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
if exist "%EXTRACT_DIR%" rmdir /s /q "%EXTRACT_DIR%"
mkdir "%EXTRACT_DIR%" 2>nul

echo === Copying OCR plugins ===
copy /y "%~dp0..\Zaya.OCR\out\*.zip" "%PLUGINS_DIR%"
if %ERRORLEVEL% neq 0 (
    echo ERROR: OCR plugin copy failed
    dir "%~dp0..\Zaya.OCR\out"
    exit /b %ERRORLEVEL%
)

echo === Building Zaya.Screenshot plugin ===
pushd "%~dp0..\Zaya.Screenshot"
call build.cmd
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%
popd

echo === Copying Screenshot plugin ===
copy /y "%~dp0..\Zaya.Screenshot\out\*.zip" "%PLUGINS_DIR%"
if %ERRORLEVEL% neq 0 (
    echo ERROR: Screenshot plugin copy failed
    dir "%~dp0..\Zaya.Screenshot\out"
    exit /b %ERRORLEVEL%
)

echo === Building Zaya.Translator plugin ===
pushd "%~dp0..\Zaya.Translator"
call build.cmd
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%
popd

echo === Copying Translator plugin ===
copy /y "%~dp0..\Zaya.Translator\out\*.zip" "%PLUGINS_DIR%"
if %ERRORLEVEL% neq 0 (
    echo ERROR: Translator plugin copy failed
    dir "%~dp0..\Zaya.Translator\out"
    exit /b %ERRORLEVEL%
)

echo === Plugins directory contents ===
dir "%PLUGINS_DIR%"

echo Done. Plugins in %PLUGINS_DIR%
echo Extract cache cleared: %EXTRACT_DIR%
