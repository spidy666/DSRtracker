@echo off
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
  echo Das .NET SDK fehlt. Download: https://dotnet.microsoft.com/download/dotnet/8.0  ^(SDK x64^)
  pause
  exit /b 1
)
echo Baue DSRTracker.exe ...
dotnet publish DSRTracker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
if errorlevel 1 (
  echo.
  echo Build fehlgeschlagen - siehe Meldungen oben.
  pause
  exit /b 1
)
echo.
echo Fertig: dist\DSRTracker.exe
echo Zum Weitergeben reicht der Inhalt des Ordners "dist".
pause
