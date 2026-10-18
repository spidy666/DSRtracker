@echo off
rem ---------------------------------------------------------------------------
rem  build.bat - baut dist\DSRTracker.exe aus dem Quellcode
rem  Ergebnis: eine einzelne exe, die kein installiertes .NET braucht
rem  (self-contained, single file, komprimiert). Braucht einmalig das .NET SDK.
rem ---------------------------------------------------------------------------
rem In den Ordner dieser Datei wechseln (egal, von wo aus gestartet)
cd /d "%~dp0"
rem Ist das .NET SDK installiert?
where dotnet >nul 2>&1
if errorlevel 1 (
  echo Das .NET SDK fehlt. Download: https://dotnet.microsoft.com/download/dotnet/8.0  ^(SDK x64^)
  pause
  exit /b 1
)
echo Baue DSRTracker.exe ...
rem -r win-x64: fuer 64-Bit-Windows; --self-contained: .NET-Laufzeit steckt in der exe
dotnet publish DSRTracker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -o dist
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
