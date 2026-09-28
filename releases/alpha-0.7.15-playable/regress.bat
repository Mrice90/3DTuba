@echo off
rem regress.bat -- packaging regression harness (Windows mirror of regress.sh).
rem
rem Runs fetch-source.bat -^> build-release.bat (which auto-runs smoke.bat) inside
rem an isolated temporary copy of this directory, so the working tree is never
rem polluted. Then verifies the fresh JAR against its OWN freshly generated
rem CHECKSUMS.sha256 (rebuilds are not byte-identical, so an unrelated reference
rem artifact must not be used).
rem
rem Usage: regress.bat [--break=pin ^| --break=dep ^| --break=compile ^| --break=checksum ^| --break=smoke]
rem   (no flag)        clean run: every stage must succeed; prints REGRESSION: PASS.
rem   --break=pin      check out a non-pin commit in the temp source tree; the
rem                    build must fail closed at stage 'build' (pin verification).
rem   --break=dep      corrupt a dependency jar in the temp tree; the build must
rem                    fail its hash verification at stage 'build'.
rem   --break=compile  inject a syntax error into one temp source file; javac
rem                    must fail at stage 'build'.
rem   --break=checksum corrupt the built jar after smoke; the harness's own
rem                    checksum verification must fail at stage 'verify'.
rem   --break=smoke    remove card data resources from the temp tree; the
rem                    build's smoke step must fail at stage 'build' (the
rem                    underlying smoke.bat exits non-zero).
rem In --break mode the harness exits 0 only when the failure is detected at the
rem expected stage; any other outcome prints REGRESSION: FAIL and exits 1.
rem Set REGRESS_KEEP=1 to keep the temp dir for inspection.
rem Upstream repos are only fetched (read-only); nothing is pushed anywhere.
setlocal EnableDelayedExpansion

set "BREAK_MODE="
set "GAVE_BREAK="
:parse
if "%~1"=="" goto parsed
if "%~1"=="-h" goto help
if "%~1"=="--help" goto help
set "ARG=%~1"
if "!ARG:~0,8!"=="--break=" (
  set "GAVE_BREAK=1"
  set "BREAK_MODE=!ARG:~8!"
) else (
  echo ERROR: unknown argument: %~1>&2
  exit /b 2
)
shift
goto parse
:help
echo Usage: regress.bat [--break=pin ^| --break=dep ^| --break=compile ^| --break=checksum ^| --break=smoke]
exit /b 0
:parsed
if defined GAVE_BREAK (
  if not "%BREAK_MODE%"=="pin" if not "%BREAK_MODE%"=="dep" if not "%BREAK_MODE%"=="compile" if not "%BREAK_MODE%"=="checksum" if not "%BREAK_MODE%"=="smoke" (
    echo ERROR: --break must be pin, dep, compile, checksum, or smoke>&2
    exit /b 2
  )
)

if "%BREAK_MODE%"=="pin" set "EXPECT_FAIL_AT=build"
if "%BREAK_MODE%"=="dep" set "EXPECT_FAIL_AT=build"
if "%BREAK_MODE%"=="compile" set "EXPECT_FAIL_AT=build"
if "%BREAK_MODE%"=="checksum" set "EXPECT_FAIL_AT=verify"
if "%BREAK_MODE%"=="smoke" set "EXPECT_FAIL_AT=build"
if defined BREAK_MODE echo regress: intentional break mode '%BREAK_MODE%', expecting failure at stage '%EXPECT_FAIL_AT%'

rem A known non-pin commit on the same branch, used only for the --break=pin
rem fault injection (checked out inside the temp dir; upstream untouched).
set "OFF_PIN=a833daa039b9e02f5375c2dfe545623d763c132a"

set "HERE=%~dp0"
set "WORK=%TEMP%\alpha-regress-%RANDOM%%RANDOM%"
echo regress: work dir %WORK%
mkdir "%WORK%" || (echo ERROR: cannot create %WORK%>&2 & exit /b 1)
xcopy "%HERE%." "%WORK%\" /E /I /Q >nul || (echo ERROR: copy failed>&2 & exit /b 1)
rem Fresh copy of the packaging dir, with any previous build output removed so
rem the regression always starts clean.
if exist "%WORK%\build" rmdir /S /Q "%WORK%\build"
del /Q "%WORK%\infinite-conquest-alpha-*.jar" 2>nul
cd /D "%WORK%"

echo == stage: fetch ==
call "%WORK%\fetch-source.bat"
if errorlevel 1 call :fail fetch "fetch-source.bat exited non-zero"
echo stage fetch: OK

if "%BREAK_MODE%"=="pin" (
  echo regress: intentional break -- moving temp source off the release pin
  git -C "%WORK%\build\alpha-src" fetch -q --depth 1 origin %OFF_PIN%
  if errorlevel 1 call :fail build "could not fetch off-pin commit for fault injection"
  git -C "%WORK%\build\alpha-src" checkout -q FETCH_HEAD
  echo regress: temp source is now off the release pin
)

if "%BREAK_MODE%"=="dep" (
  if not exist "%WORK%\build\deps" mkdir "%WORK%\build\deps"
  echo corrupted-dependency> "%WORK%\build\deps\jackson-core-2.18.2.jar"
  echo regress: intentional break -- corrupted build\deps\jackson-core-2.18.2.jar
)

if "%BREAK_MODE%"=="compile" (
  set "COMPILE_TARGET="
  for /f "delims=" %%J in ('dir /s /b "%WORK%\build\alpha-src\game-cli\src\main\java\*.java" 2^>nul') do (
    if not defined COMPILE_TARGET set "COMPILE_TARGET=%%J"
  )
  if not defined COMPILE_TARGET call :fail build "no java source found for fault injection"
  echo @@@INVALID-JAVA-SYNTAX@@@>> "!COMPILE_TARGET!"
  echo regress: intentional break -- injected syntax error into !COMPILE_TARGET!
)

if "%BREAK_MODE%"=="smoke" (
  set "SMOKE_N=0"
  for /f "delims=" %%C in ('dir /s /b "%WORK%\build\alpha-src\*card*.json" 2^>nul') do (
    echo %%C | findstr /i "resources" >nul
    if not errorlevel 1 (
      del "%%C" >nul
      set /a SMOKE_N+=1
    )
  )
  if !SMOKE_N! EQU 0 call :fail build "no card JSON resources found for fault injection"
  echo regress: intentional break -- removed !SMOKE_N! card JSON resources from temp tree
)

echo == stage: build ==
call "%WORK%\build-release.bat"
if errorlevel 1 call :fail build "build-release.bat exited non-zero (underlying failure; for --break=smoke this is the expected smoke.bat failure)"
echo stage build: OK

echo == stage: verify ==
set "JAR="
for %%F in (infinite-conquest-alpha-*.jar) do set "JAR=%%F"
if not defined JAR call :fail verify "no built jar found"
if not exist CHECKSUMS.sha256 call :fail verify "CHECKSUMS.sha256 missing after build"

if "%BREAK_MODE%"=="checksum" (
  echo x>> "%JAR%"
  echo regress: intentional break -- corrupted %JAR%
)

rem AI-052-WIN: read the expected hash from the FILE (usebackq), and take the
rem actual hash from certutil's only colon-free line. The old code iterated the
rem literal string "CHECKSUMS.sha256" and regex-matched certutil output.
set "EXPECTED="
for /f "usebackq tokens=1" %%H in ("%WORK%\CHECKSUMS.sha256") do set "EXPECTED=%%H"
set "ACTUAL="
for /f %%H in ('certutil -hashfile "%JAR%" SHA256 ^| findstr /v ":"') do (
  set "ACTUAL=%%H"
  goto :got_actual
)
:got_actual
if not defined EXPECTED call :fail verify "could not read expected hash from CHECKSUMS.sha256"
if not defined ACTUAL call :fail verify "could not hash the built jar with certutil"
if /i not "%EXPECTED%"=="%ACTUAL%" call :fail verify "jar does not match its own generated checksum"
echo stage verify: OK (%JAR% matches its own generated checksum)

call :pass

rem -- subroutines never return: they jump to :finish, which cleans up and exits.

:fail
rem %1 = stage, %2 = detail
if defined BREAK_MODE (
  if "%~1"=="%EXPECT_FAIL_AT%" (
    set "EXITCODE=0"
    set "RESULT=REGRESSION: intentional break correctly detected at stage '%~1' (%~2)"
    goto :finish
  )
  set "EXITCODE=1"
  set "RESULT=REGRESSION: FAIL at stage '%~1' (%~2) -- break '%BREAK_MODE%' expected failure at '%EXPECT_FAIL_AT%'"
  goto :finish
)
set "EXITCODE=1"
set "RESULT=REGRESSION: FAIL at stage '%~1' (%~2)"
goto :finish

:pass
if defined BREAK_MODE (
  set "EXITCODE=1"
  set "RESULT=REGRESSION: BREAK NOT DETECTED -- pipeline passed despite --break=%BREAK_MODE%"
  goto :finish
)
set "EXITCODE=0"
set "RESULT=REGRESSION: PASS"
goto :finish

:finish
echo %RESULT%
if not "%REGRESS_KEEP%"=="1" (
  if defined WORK if exist "%WORK%" rmdir /S /Q "%WORK%"
)
exit /b %EXITCODE%
