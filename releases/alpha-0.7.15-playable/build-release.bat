@echo off
rem build-release.bat -- assemble the Infinite Conquest alpha playable release (Windows).
rem Source: Mrice90/TubaExperiment @ 992bc95 (branch strip/zeus-poseidon-desktop),
rem read-only. Run fetch-source.bat first. Requires JDK 17+ and git on PATH.
rem Env: ALPHA (default build\alpha-src under this script)
rem      STAGE (default build\stage under this script)
rem      ALPHA_ALLOW_UNPINNED=1 to build a different checkout (not the release recipe)
setlocal EnableDelayedExpansion

set PIN=992bc95c7164416ea0a25a4ce120f6ec0a0a167a
set VERSION=0.7.15
set JARNAME=infinite-conquest-alpha-%VERSION%.jar
set HERE=%~dp0
if defined ALPHA (set ALPHA_DIR=%ALPHA%) else (set ALPHA_DIR=%HERE%build\alpha-src)
if defined STAGE (set STAGE_DIR=%STAGE%) else (set STAGE_DIR=%HERE%build\stage)

echo == diagnostics ==
where git >nul 2>nul || (echo ERROR: git not found on PATH. & exit /b 10)
where java >nul 2>nul || (echo ERROR: java not found on PATH -- install JDK 17+. & exit /b 11)
where javac >nul 2>nul || (echo ERROR: javac not found -- install a full JDK 17+, not just a JRE. & exit /b 12)
where jar >nul 2>nul || (echo ERROR: 'jar' tool not found -- install a full JDK 17+. & exit /b 13)

rem AI-052-WIN: match java.specification.version exactly. The old
rem findstr /c:"java.version" also matched java.version.date, so the last
rem match won and the check compared a calendar date instead of the version.
set JV=
rem AI-052-WIN: temp file avoids pipe-in-for quoting issues on cmd
java -XshowSettings:properties -version > "%TEMP%\jv-props.txt" 2>&1
for /f "tokens=3" %%v in ('find "java.specification.version" "%TEMP%\jv-props.txt"') do set JV=%%v
del "%TEMP%\jv-props.txt" 2>nul
if not defined JV (
  echo ERROR: could not determine java.specification.version -- JDK 17+ required.
  exit /b 14
)
set JMAJOR=0
for /f "delims=. tokens=1" %%m in ("!JV!") do set JMAJOR=%%m
if !JMAJOR! LSS 17 (echo ERROR: java specification version !JV! is too old -- JDK 17+ required. & exit /b 15)
echo java specification version: !JV!

if not exist "%ALPHA_DIR%\.git" (
  echo ERROR: alpha source not found at %ALPHA_DIR% -- run fetch-source.bat first.
  exit /b 20
)

for /f %%h in ('git -C "%ALPHA_DIR%" rev-parse HEAD') do set HEAD=%%h
echo alpha: !HEAD!
if not "!HEAD!"=="%PIN%" if not "%ALPHA_ALLOW_UNPINNED%"=="1" (
  rem AI-052-WIN: escape every literal paren inside the block.
  echo ERROR: checkout ^(!HEAD!^) does not match release pin ^(%PIN%^). Set ALPHA_ALLOW_UNPINNED=1 to build anyway.
  exit /b 21
)

echo == dependencies (pinned, hash-verified) ==
rem The Jackson jars are NOT tracked in the upstream repo; fetch the exact
rem artifacts from Maven Central and verify SHA-256 before use.
set DEPS=%HERE%build\deps
if not exist "%DEPS%" mkdir "%DEPS%"
call :fetchdep jackson-databind
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%
call :fetchdep jackson-core
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%
call :fetchdep jackson-annotations
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%
echo dependencies verified
set CP_JARS=%DEPS%\jackson-databind-2.18.2.jar;%DEPS%\jackson-core-2.18.2.jar;%DEPS%\jackson-annotations-2.18.2.jar

if exist "%STAGE_DIR%" rmdir /s /q "%STAGE_DIR%"
mkdir "%STAGE_DIR%\classes" "%STAGE_DIR%\release"

echo == compiling ==
set SRCLIST=%STAGE_DIR%\sources.txt
if exist "%SRCLIST%" del "%SRCLIST%"
for %%M in (game-core game-cli game-gui net-server) do (
  for /f "delims=" %%F in ('dir /s /b "%ALPHA_DIR%\%%M\src\main\java\*.java"') do (
    rem AI-052-WIN: javac @argfile treats backslash as an escape character, so
    rem raw Windows paths break compilation. Normalize to forward slashes.
    set "SRC=%%F"
    set "SRC=!SRC:\=/!"
    echo "!SRC!">>"%SRCLIST%"
  )
)
javac -encoding UTF-8 -nowarn -cp "%CP_JARS%" -d "%STAGE_DIR%\classes" @"%SRCLIST%" || exit /b 1

echo == resources ==
for %%M in (game-core game-cli game-gui net-server) do (
  if exist "%ALPHA_DIR%\%%M\src\main\resources" xcopy /E /I /Y "%ALPHA_DIR%\%%M\src\main\resources" "%STAGE_DIR%\classes\" >nul
)

echo == merging jackson ==
pushd "%STAGE_DIR%\classes"
for %%J in ("%DEPS%\jackson-*.jar") do jar xf "%%J"
del /s /q META-INF\*.SF META-INF\*.DSA META-INF\*.RSA >nul 2>nul
popd

echo == jarring ==
(
  echo Manifest-Version: 1.0
  echo Main-Class: com.infiniteconquest.gui.GameShell
  rem AI-052-WIN: the literal ) in "(alpha)" closed this output block early.
  echo Implementation-Title: Infinite Conquest ^(alpha^)
  echo Implementation-Version: %VERSION%
) > "%STAGE_DIR%\manifest.txt"
jar --create --file "%STAGE_DIR%\release\%JARNAME%" --manifest "%STAGE_DIR%\manifest.txt" -C "%STAGE_DIR%\classes" . || exit /b 1

echo == checksum ==
rem AI-052-WIN: certutil prints the hash on the only colon-free line. The old
rem "skip=1" skipped that very line, so HASH was always empty.
set HASH=
for /f %%H in ('certutil -hashfile "%STAGE_DIR%\release\%JARNAME%" SHA256 ^| findstr /v ":"') do (
  set HASH=%%H
  goto :got_hash
)
:got_hash
if not defined HASH (
  echo ERROR: could not hash %JARNAME% with certutil.
  exit /b 1
)
echo !HASH!  %JARNAME%> "%STAGE_DIR%\release\CHECKSUMS.sha256"
type "%STAGE_DIR%\release\CHECKSUMS.sha256"
copy /Y "%STAGE_DIR%\release\%JARNAME%" "%HERE%%JARNAME%" >nul
copy /Y "%STAGE_DIR%\release\CHECKSUMS.sha256" "%HERE%CHECKSUMS.sha256" >nul
echo copied %JARNAME% + CHECKSUMS.sha256 next to the launchers

echo == smoke ==
call "%HERE%smoke.bat" "%HERE%%JARNAME%" || exit /b 1

echo == done ==
dir "%HERE%%JARNAME%" "%HERE%CHECKSUMS.sha256"

exit /b 0

:fetchdep
rem AI-052-WIN: %1 = artifact. No hardcoded hash; the published .sha256 is
rem fetched from Maven Central (cached in %DEPS%) and the jar is verified
rem against it. This avoids stale hardcoded hashes.
set ART=%~1
set JARF=%ART%-2.18.2.jar
set URL=https://repo1.maven.org/maven2/com/fasterxml/jackson/core/%ART%/2.18.2/%JARF%
if not exist "%DEPS%\%JARF%" (
  echo fetching %JARF% ...
  where curl.exe >nul 2>nul || (echo ERROR: curl.exe not found -- install curl or place %JARF% in %DEPS% manually. & exit /b 22)
  curl.exe -sSL --max-time 180 -o "%DEPS%\%JARF%" "%URL%" || (echo ERROR: download failed for %JARF% & exit /b 23)
)
if not exist "%DEPS%\%JARF%.sha256" (
  echo fetching %JARF%.sha256 ...
  curl.exe -sSL --max-time 60 -o "%DEPS%\%JARF%.sha256" "%URL%.sha256" || (echo ERROR: could not fetch %JARF%.sha256 & exit /b 26)
)
set EXP=
for /f "tokens=1" %%E in ('type "%DEPS%\%JARF%.sha256"') do (if not defined EXP set EXP=%%E)
if not defined EXP (
  echo ERROR: could not parse %JARF%.sha256.
  exit /b 26
)
set DH=
for /f %%H in ('certutil -hashfile "%DEPS%\%JARF%" SHA256 ^| findstr /v ":"') do (set DH=%%H & goto :depchecked)
:depchecked
if not defined DH (
  echo ERROR: could not hash %JARF% with certutil.
  exit /b 24
)
if /i not "!DH!"=="%EXP%" (echo ERROR: checksum mismatch for %JARF% -- expected %EXP%, got !DH! & echo ::error::AI-052-WIN hash mismatch %JARF% exp=%EXP% got=!DH! & exit /b 25)
echo verified %JARF%
exit /b 0
