@echo off
cd /d "%~dp0"
rem Startet den Tracker und laedt die HTML-Dateien aus dem Ordner "web".
rem Aenderungen an overlay.html / control.html wirken dann ohne Neu-Bauen (nur Seite neu laden).
dist\DSRTracker.exe --web web
