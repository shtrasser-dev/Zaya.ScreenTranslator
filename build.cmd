@echo off

set ROOT=%~dp0

echo === Publishing Zaya.ScreenTranslator.Impl.Windows ===

dotnet publish "%ROOT%src\Zaya.ScreenTranslator.Impl.Windows\Zaya.ScreenTranslator.Impl.Windows.csproj" -c Release -o "%ROOT%out"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo === Done: out\Zaya.ScreenTranslator.Impl.Windows.exe ===
