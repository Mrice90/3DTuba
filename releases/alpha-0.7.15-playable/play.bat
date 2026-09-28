@echo off
rem play.bat -- launch the Infinite Conquest alpha (Windows).
rem Expects infinite-conquest-alpha-0.7.15.jar next to this script.
setlocal
set DIR=%~dp0
set JAR=%DIR%infinite-conquest-alpha-0.7.15.jar

where java >nul 2>nul || (
  echo ERROR: java not found on PATH -- install JDK 17+ ^(Temurin/Adoptium^), then re-run.
  exit /b 1
)
call :checkver || exit /b 1

if not exist "%JAR%" (
  rem AI-052-WIN: escape every literal paren inside the block.
  echo ERROR: %JAR% not found.
  echo The jar is NOT stored in this repository ^(publishing it via the GitHub API was refused: HTTP 409 repository-rule validation^).
  echo Get it from the release handoff, place it next to play.bat, then verify:
  echo   certutil -hashfile "%JAR%" SHA256
  echo Expected: 728c3fc101ad686e8c73c7a9af979125d7052f943f7b89645edbdc5149029523
  echo Or rebuild it from the pinned source: fetch-source.bat ^&^& build-release.bat
  exit /b 1
)

rem AI-052-WIN: refuse to launch a tampered jar. If CHECKSUMS.sha256 ships next
rem to the launcher, the jar must match it (mirrors play.sh behavior).
if exist "%DIR%CHECKSUMS.sha256" (
  call :verify || exit /b 1
)

java -jar "%JAR%" %*
exit /b %ERRORLEVEL%

:checkver
rem AI-052-WIN: match java.specification.version exactly. The old
rem findstr /c:"java.version" also matched java.version.date, so the last
rem match won and the check compared a calendar date instead of the version.
set JV=
for /f "tokens=3" %%v in ('java -XshowSettings:properties -version 2^>^&1 ^| findstr /c:"java.specification.version"') do set JV=%%v
if not defined JV (
  echo ERROR: could not determine java.specification.version -- JDK 17+ required.
  exit /b 1
)
set JMAJOR=0
for /f "delims=. tokens=1" %%m in ("%JV%") do set JMAJOR=%%m
if %JMAJOR% LSS 17 (
  echo ERROR: java specification version %JV% is too old -- JDK 17+ required.
  exit /b 1
)
exit /b 0

:verify
set VH=
for /f %%H in ('certutil -hashfile "%JAR%" SHA256 ^| findstr /v ":"') do (
  set VH=%%H
  goto :got_vhash
)
:got_vhash
if not defined VH (
  echo ERROR: could not hash the jar -- refusing to launch.
  exit /b 1
)
set VMATCH=
for /f "usebackq tokens=1" %%E in ("%DIR%CHECKSUMS.sha256") do if /i "%%E"=="%VH%" set VMATCH=1
if not defined VMATCH (
  echo ERROR: jar checksum mismatch -- refusing to launch a tampered jar.
  echo Re-fetch the release handoff or rebuild: fetch-source.bat ^&^& build-release.bat
  exit /b 1
)
echo OK: jar matches CHECKSUMS.sha256
exit /b 0
