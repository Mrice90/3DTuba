@echo off
rem regress.bat — bounded AI-004/AI-048 packaging regression (Windows mirror of regress.sh).
rem NOTE: not yet executed on Windows — first Windows run is a shakedown.
rem
rem Runs fetch-source.bat -> build-release.bat (auto-runs smoke.bat) inside a
rem temp copy of this directory, then verifies the fresh JAR against its OWN
rem freshly generated CHECKSUMS.sha256 (rebuilds are not byte-identical, so an
rem unrelated reference artifact must not be used).
rem
rem Usage: regress.bat [--break=pin | --break=checksum]
rem   --break=pin      check out a non-pin commit in the temp source tree; the
rem                    build must fail closed at pin verification.
rem   --break=checksum corrupt the built JAR after smoke; the self-checksum
rem                    verification must fail.
rem Set REGRESS_KEEP=1 to keep the temp dir.
setlocal EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "BREAK_MODE="
set "EXPECT_FAIL_AT="

:parse
if "%~1"=="" goto parsed
if "%~1"=="--break=pin" set "BREAK_MODE=pin" & set "EXPECT_FAIL_AT=build"
if "%~1"=="--break=checksum" set "BREAK_MODE=checksum" & set "EXPECT_FAIL_AT=verify"
if "%~1"=="-h" goto help
if "%~1"=="--help" goto help
shift
goto parse
:help
echo Usage: regress.bat [--break=pin ^| --break=checksum]
exit /b 0
:parsed
if defined BREAK_MODE (
  if not "%BREAK_MODE%"=="pin" if not "%BREAK_MODE%"=="checksum" (
    echo ERROR: --break must be pin or checksum>&2 & exit /b 2
  )
)

set "OFF_PIN=a833daa039b9e02f5375c2dfe545623d763c132a"
set "WORK=%TEMP%\alpha-regress-%RANDOM%%RANDOM%"
echo regress: work dir %WORK%
mkdir "%WORK%" || (echo ERROR: cannot create %WORK%>&2 & exit /b 1)
xcopy "%SCRIPT_DIR%." "%WORK%\" /E /I /Q >nul || (echo ERROR: copy failed>&2 & exit /b 1)
if exist "%WORK%\build" rmdir /S /Q "%WORK%\build%"
del /Q "%WORK%\infinite-conquest-alpha-*.jar" 2>nul
cd /D "%WORK%"

echo == stage: fetch ==
call fetch-source.bat
if errorlevel 1 call :fail fetch "fetch-source.bat exited non-zero" & exit /b 1
echo stage fetch: OK

if "%BREAK_MODE%"=="pin" (
  git -C build\alpha-src fetch -q --depth 1 origin %OFF_PIN%
  if errorlevel 1 call :fail build "could not fetch off-pin commit for fault injection" & exit /b 1
  git -C build\alpha-src checkout -q FETCH_HEAD
  echo regress: intentional break — source moved off the release pin
)

echo == stage: build ==
call build-release.bat
if errorlevel 1 call :fail build "build-release.bat exited non-zero" & exit /b 1
echo stage build: OK

echo == stage: verify ==
set "JAR="
for %%F in (infinite-conquest-alpha-*.jar) do set "JAR=%%F"
if not defined JAR call :fail verify "no built jar found" & exit /b 1
if not exist CHECKSUMS.sha256 call :fail verify "CHECKSUMS.sha256 missing after build" & exit /b 1

if "%BREAK_MODE%"=="checksum" (
  echo x>> "%JAR%"
  echo regress: intentional break — corrupted %JAR%
)

for /f "tokens=1" %%H in (CHECKSUMS.sha256) do set "EXPECTED=%%H"
for /f "tokens=*" %%H in ('certutil -hashfile "%JAR%" SHA256 ^| findstr /r "^[0-9a-f][0-9a-f]*$"') do set "ACTUAL=%%H"
set "ACTUAL=!ACTUAL: =!"
if /i not "%EXPECTED%"=="%ACTUAL%" call :fail verify "jar does not match its own generated checksum" & exit /b 1
echo stage verify: OK (%JAR% matches its own generated checksum)

call :pass
exit /b 0

:fail
rem %1 = stage, %2 = detail
if defined BREAK_MODE (
  if "%~1"=="%EXPECT_FAIL_AT%" (
    echo REGRESSION: intentional break correctly detected at stage '%~1' (%~2^)
    if not "%REGRESS_KEEP%"=="1" rmdir /S /Q "%WORK%"
    exit /b 0
  )
  echo REGRESSION: FAIL at stage '%~1' (%~2^) — break '%BREAK_MODE%' expected failure at '%EXPECT_FAIL_AT%'
  if not "%REGRESS_KEEP%"=="1" rmdir /S /Q "%WORK%"
  exit /b 1
)
echo REGRESSION: FAIL at stage '%~1' (%~2^)
if not "%REGRESS_KEEP%"=="1" rmdir /S /Q "%WORK%"
exit /b 1

:pass
if defined BREAK_MODE (
  echo REGRESSION: BREAK NOT DETECTED — pipeline passed despite --break=%BREAK_MODE%
  if not "%REGRESS_KEEP%"=="1" rmdir /S /Q "%WORK%"
  exit /b 1
)
echo REGRESSION: PASS
if not "%REGRESS_KEEP%"=="1" rmdir /S /Q "%WORK%"
exit /b 0
