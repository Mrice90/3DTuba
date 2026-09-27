@echo off
rem play.bat -- launch the Infinite Conquest alpha (Windows).
rem Expects infinite-conquest-alpha-0.7.15.jar next to this script.
setlocal
set DIR=%~dp0
set JAR=%DIR%infinite-conquest-alpha-0.7.15.jar

where java >nul 2>nul || (
  echo ERROR: java not found on PATH -- install JDK 17+ (Temurin/Adoptium), then re-run.
  exit /b 1
)
call :checkver || exit /b 1

if not exist "%JAR%" (
  echo ERROR: %JAR% not found.
  echo The jar is NOT stored in this repository (a repo rule rejects 90MB+ blobs).
  echo Get it from the release handoff, place it next to play.bat, then verify:
  echo   certutil -hashfile "%JAR%" SHA256
  echo Expected: 728c3fc101ad686e8c73c7a9af979125d7052f943f7b89645edbdc5149029523
  echo Or rebuild it from the pinned source: fetch-source.bat ^&^& build-release.bat
  exit /b 1
)
java -jar "%JAR%" %*
exit /b %ERRORLEVEL%

:checkver
for /f "tokens=3" %%v in ('java -XshowSettings:properties -version 2^>^&1 ^| findstr /c:"java.version"') do set JV=%%v
for /f "delims=. tokens=1" %%m in ("%JV%") do set JMAJOR=%%m
if %JMAJOR% LSS 17 (
  echo ERROR: java %JV% is too old -- JDK 17+ required.
  exit /b 1
)
exit /b 0
