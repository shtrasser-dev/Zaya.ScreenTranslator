@echo off
setlocal enabledelayedexpansion

set ROOT=%~dp0

if "%CI%"=="true" (
    set BUILD_CONFIG=Release
) else (
    set BUILD_CONFIG=Release
)

echo === Publishing Zaya.ScreenTranslator.Impl.Windows (%BUILD_CONFIG%) ===

dotnet publish "%ROOT%src\Zaya.ScreenTranslator.Impl.Windows\Zaya.ScreenTranslator.Impl.Windows.csproj" -c %BUILD_CONFIG% -o "%ROOT%out"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo === Detecting version ===

for /f "usebackq delims=" %%a in (`dotnet msbuild "%ROOT%src\Zaya.ScreenTranslator.Impl.Windows\Zaya.ScreenTranslator.Impl.Windows.csproj" -getProperty:Version -nologo -v:q`) do set VER=%%a
set VER=!VER: =!
if "!VER!"=="" set VER=0.4.0

for /f "tokens=1,2,3 delims=." %%a in ("!VER!") do (
    set VER_MAJOR=%%a
    set VER_MINOR=%%b
    set VER_PATCH=%%c
)
set CHANNEL=!VER_MAJOR!.!VER_MINOR!
echo   Version=!VER!  Channel=!CHANNEL!

echo !VER!>"%ROOT%out\version.txt"
echo !CHANNEL!>"%ROOT%out\channel.txt"

echo === Done: out\Zaya.ScreenTranslator.Impl.Windows.exe ===
