@echo off
rem build-release.bat -- assemble the Infinite Conquest alpha playable release (Windows).
rem Source: Mrice90/TubaExperiment @ 992bc95 (branch strip/zeus-poseidon-desktop),
rem read-only. Run fetch-source.bat first. Requires JDK 17+ and git on PATH.
rem Env: ALPHA (default build\alpha-src under this script)
rem      STAGE (default build\stage under this script)
rem      ALPHA_ALLOW_UNPINNED=1 to build a different checkout (not the release recipe)
rem AI-056 exit codes (distinct per check; asserted by regress.bat):
rem    1  diagnostics / certutil hash-tool failure
rem   10-15 missing tools / java too old
rem   20 no git repo at ALPHA / 21 source pin mismatch
rem   22 no curl / 23 download failed / 24 hash failed / 25 .sha1 mismatch / 26 .sha1 fetch failed / 27 SHA-256 pin mismatch (AI-055)
rem   28 compilation/jarring failed / 29 smoke test failed
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
rem AI-055: supply-chain pin -- full SHA-256 of each Jackson 2.18.2 jar,
rem verified 2026-09-28 against Maven Central (jars also match published .sha1).
rem The fetched .sha1 stays as a secondary transmission check; the PIN is the
rem trust anchor (a malicious mirror can serve a self-consistent jar+.sha1;
rem only the pin catches that -- see regress.bat --break=depswap). When Jackson
rem is bumped, update these pins with review -- never delete the check.
set PIN_jackson_databind=4b364e6850dc89172fcf1d4dd26b8ff5488eda44ff4657e22dd265203dd5ab3c
set PIN_jackson_core=d8054ae7c0d1c2d2f55d28e46026ebe5892881f3fab5f439233184381c3b4a1f
set PIN_jackson_annotations=581bd61000ef7648943f781ca05689e56d03f6052748365a8e2b3a9b5d3fa32f
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
javac -encoding UTF-8 -nowarn -cp "%CP_JARS%" -d "%STAGE_DIR%\classes" @"%SRCLIST%" || exit /b 28

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
jar --create --file "%STAGE_DIR%\release\%JARNAME%" --manifest "%STAGE_DIR%\manifest.txt" -C "%STAGE_DIR%\classes" . || exit /b 28

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
call "%HERE%smoke.bat" "%HERE%%JARNAME%" || exit /b 29

echo == done ==
dir "%HERE%%JARNAME%" "%HERE%CHECKSUMS.sha256"

exit /b 0

:fetchdep
rem AI-055: %1 = artifact. The published .sha1 is fetched from Maven Central
rem (cached in %DEPS%) as a secondary transmission check; the primary check
rem is the hardcoded SHA-256 pin (PIN_jackson_*, verified 2026-09-28).
rem Update the pins with review when Jackson is bumped.
set ART=%~1
set JARF=%ART%-2.18.2.jar
set URL=https://repo1.maven.org/maven2/com/fasterxml/jackson/core/%ART%/2.18.2/%JARF%
if not exist "%DEPS%\%JARF%" (
  echo fetching %JARF% ...
  where curl.exe >nul 2>nul || (echo ERROR: curl.exe not found -- install curl or place %JARF% in %DEPS% manually. & exit /b 22)
  curl.exe -sSL --max-time 180 -o "%DEPS%\%JARF%" "%URL%" || (echo ERROR: download failed for %JARF% & exit /b 23)
)
if not exist "%DEPS%\%JARF%.sha1" (
  echo fetching %JARF%.sha1 ...
  curl.exe -sSL --max-time 60 -o "%DEPS%\%JARF%.sha1" "%URL%.sha1" || (echo ERROR: could not fetch %JARF%.sha1 & exit /b 26)
)
set EXP=
for /f "tokens=1" %%E in ('type "%DEPS%\%JARF%.sha1"') do (if not defined EXP set EXP=%%E)
if not defined EXP (
  echo ERROR: could not parse %JARF%.sha1.
  exit /b 26
)
set DH=
for /f %%H in ('certutil -hashfile "%DEPS%\%JARF%" SHA1 ^| findstr /v ":"') do (set DH=%%H & goto :depchecked)
:depchecked
if not defined DH (
  echo ERROR: could not hash %JARF% with certutil.
  exit /b 24
)
rem AI-052-WIN: trim trailing spaces from certutil output (for /f can leave them)
:trimdh
if "!DH:~-1!"==" " (
  set "DH=!DH:~0,-1!"
  goto :trimdh
)
:trimdone
if /i not "!DH!"=="%EXP%" (echo ERROR: checksum mismatch for %JARF% -- expected %EXP%, got !DH! & echo ::error::AI-052-WIN hash mismatch %JARF% exp=%EXP% got=!DH! & exit /b 25)
rem AI-055: primary trust anchor -- pinned SHA-256, fail closed.
set PEXP=
if "%ART%"=="jackson-databind" set PEXP=%PIN_jackson_databind%
if "%ART%"=="jackson-core" set PEXP=%PIN_jackson_core%
if "%ART%"=="jackson-annotations" set PEXP=%PIN_jackson_annotations%
set PH=
for /f %%H in ('certutil -hashfile "%DEPS%\%JARF%" SHA256 ^| findstr /v ":"') do (set PH=%%H & goto :pin256checked)
:pin256checked
if not defined PH (
  echo ERROR: could not hash %JARF% with certutil ^(SHA256^).
  exit /b 24
)
:trimp256
if "!PH:~-1!"==" " (
  set "PH=!PH:~0,-1!"
  goto :trimp256
)
:trimp256done
if /i not "!PH!"=="!PEXP!" (echo ERROR: SHA-256 pin mismatch for %JARF% -- supply-chain check failed & exit /b 27)
echo verified %JARF% (sha1 + pinned sha256)
exit /b 0
