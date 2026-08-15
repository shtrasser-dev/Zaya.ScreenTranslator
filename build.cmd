@echo off
setlocal enabledelayedexpansion

set ROOT=%~dp0
set STAGEDIR=%TEMP%\Zaya.ScreenTranslator\plugin-staging

if "%CI%"=="true" (
    set BUILD_CONFIG=Release
) else (
    set BUILD_CONFIG=Release
)

echo === Building Zaya.ScreenTranslator.Layout.Impl (%BUILD_CONFIG%) ===

dotnet build "%ROOT%src\Zaya.ScreenTranslator.Layout.Impl\Zaya.ScreenTranslator.Layout.Impl.csproj" -c %BUILD_CONFIG%
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo === Publishing Zaya.ScreenTranslator.Impl.Windows (%BUILD_CONFIG%) ===

dotnet publish "%ROOT%src\Zaya.ScreenTranslator.Impl.Windows\Zaya.ScreenTranslator.Impl.Windows.csproj" -c %BUILD_CONFIG% -o "%ROOT%out"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo === Detecting version ===

for /f "usebackq delims=" %%a in (`dotnet msbuild "%ROOT%src\Zaya.ScreenTranslator.Impl.Windows\Zaya.ScreenTranslator.Impl.Windows.csproj" -getProperty:Version -nologo -v:q`) do set VER=%%a
set VER=!VER: =!
if "!VER!"=="" set VER=1.0.0

for /f "tokens=1,2,3 delims=." %%a in ("!VER!") do (
    set VER_MAJOR=%%a
    set VER_MINOR=%%b
    set VER_PATCH=%%c
)
set CHANNEL=!VER_MAJOR!.!VER_MINOR!
echo   Version=!VER!  Channel=!CHANNEL!

for /f "usebackq delims=" %%a in (`dotnet msbuild "%ROOT%src\Zaya.ScreenTranslator.Layout\Zaya.ScreenTranslator.Layout.csproj" -getProperty:Version -nologo -v:q`) do set IFACE_LAYOUT=%%a
set IFACE_LAYOUT=!IFACE_LAYOUT: =!
if "!IFACE_LAYOUT!"=="" set IFACE_LAYOUT=1.0.0

for /f "usebackq delims=" %%a in (`dotnet msbuild "%ROOT%src\Zaya.ScreenTranslator.Layout.Impl\Zaya.ScreenTranslator.Layout.Impl.csproj" -getProperty:Version -nologo -v:q`) do set VER_LAYOUT=%%a
set VER_LAYOUT=!VER_LAYOUT: =!
if "!VER_LAYOUT!"=="" set VER_LAYOUT=!IFACE_LAYOUT!

echo !VER!>"%ROOT%out\version.txt"
echo !CHANNEL!>"%ROOT%out\channel.txt"

echo === Creating Zaya.ScreenTranslator.Layout.Impl plugin.zip ===
call :MakeZip ScreenOverlay overlaylayout "%ROOT%src\Zaya.ScreenTranslator.Layout.Impl\bin\%BUILD_CONFIG%\net8.0" Zaya.ScreenTranslator.Layout.Impl.dll Zaya.ScreenTranslator.Layout.Impl.zip !VER_LAYOUT! Zaya.ScreenTranslator.Layout.Impl.ScreenOverlayLayoutService !IFACE_LAYOUT!
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo === Creating Zaya.ScreenTranslator-!VER!.zip ===
powershell -NoProfile -Command "Compress-Archive -Path '%ROOT%out\Zaya.ScreenTranslator.exe' -DestinationPath '%ROOT%out\Zaya.ScreenTranslator-!VER!.zip' -Force"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo === Done: out\Zaya.ScreenTranslator-!VER!.zip ===
goto :eof

:MakeZip
    set ZIP_ID=%~1
    set ZIP_TYPE=%~2
    set ZIP_TFM=%~3
    set ZIP_DLL=%~4
    set ZIP_NAME=%~5
    set ZIP_PVER=%~6
    set ZIP_ENTRY=%~7
    set ZIP_IFACE=%~8

    rmdir /s /q "%STAGEDIR%" 2>nul
    mkdir "%STAGEDIR%"

    copy /y "%ZIP_TFM%\%ZIP_DLL%" "%STAGEDIR%"
    if %ERRORLEVEL% neq 0 (
        echo ERROR: Could not find %ZIP_DLL%
        exit /b 1
    )

    call :CopySatellites "%ZIP_TFM%" "%STAGEDIR%" "%ZIP_DLL%"

    set PLUGIN_JSON=%STAGEDIR%\plugin.json
    echo {>"%PLUGIN_JSON%"
    echo   "id": "!ZIP_ID!",>>"%PLUGIN_JSON%"
    echo   "type": "!ZIP_TYPE!",>>"%PLUGIN_JSON%"
    echo   "interface": "Zaya.ScreenTranslator.Layout",>>"%PLUGIN_JSON%"
    echo   "interfaceVersion": "!ZIP_IFACE!",>>"%PLUGIN_JSON%"
    echo   "pluginVersion": "!ZIP_PVER!",>>"%PLUGIN_JSON%"
    echo   "entryPoint": "!ZIP_ENTRY!">>"%PLUGIN_JSON%"
    echo }>>"%PLUGIN_JSON%"

    powershell -Command "Compress-Archive -Path '%STAGEDIR%\*' -DestinationPath '%ROOT%out\!ZIP_NAME!' -Force"
    echo   out\!ZIP_NAME!  pluginVersion=!ZIP_PVER! entryPoint=!ZIP_ENTRY!
    rmdir /s /q "%STAGEDIR%" 2>nul
    exit /b 0

:CopySatellites
    set "SAT_DLL=%~n3.resources.dll"
    for /d %%d in ("%~1\*") do (
        if exist "%%d\!SAT_DLL!" (
            mkdir "%~2\%%~nxd" 2>nul
            copy /y "%%d\!SAT_DLL!" "%~2\%%~nxd\"
        )
    )
    exit /b
