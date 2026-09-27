@echo off
rem play.bat — launch Infinite Conquest (Windows). Requires Java 17+.
cd /d "%~dp0"
where java >nul 2>nul
if errorlevel 1 (
  echo Java 17+ is required but was not found. Install Temurin 17 from https://adoptium.net
  pause
  exit /b 1
)
start "" javaw -jar infinite-conquest-alpha-0.7.15.jar %*
