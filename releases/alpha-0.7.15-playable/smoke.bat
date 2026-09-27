@echo off
rem smoke.bat -- runnable smoke checks for the built alpha jar (Windows).
rem Usage: smoke.bat [jar-path]   (default: infinite-conquest-alpha-0.7.15.jar next to this script)
rem Exits 1 if any check fails. The headless engine check runs 36 seeded
rem bot-vs-bot matches (~10s) through the real game code.
setlocal EnableDelayedExpansion
set HERE=%~dp0
if "%~1"=="" (set JAR=%HERE%infinite-conquest-alpha-0.7.15.jar) else (set JAR=%~1)
set PASS=0
set FAIL=0

where java >nul 2>nul || (echo FAIL: java not found on PATH & exit /b 1)
echo == smoke: %JAR% ==

if exist "%JAR%" (echo PASS: jar exists & set /a PASS+=1) else (echo FAIL: jar exists & set /a FAIL+=1 & goto :result)

mkdir "%TEMP%\smoke-meta" 2>nul
pushd "%TEMP%\smoke-meta" >nul
jar xf "%JAR%" META-INF/MANIFEST.MF >nul 2>&1
findstr /c:"Main-Class: com.infiniteconquest.gui.GameShell" META-INF\MANIFEST.MF >nul 2>&1
if !ERRORLEVEL!==0 (echo PASS: manifest names the GUI entry point & set /a PASS+=1) else (echo FAIL: manifest names the GUI entry point & set /a FAIL+=1)
popd >nul
rmdir /s /q "%TEMP%\smoke-meta" >nul 2>&1

jar tf "%JAR%" 2>nul | findstr /c:"com/infiniteconquest/gui/GameShell.class" >nul
if !ERRORLEVEL!==0 (echo PASS: entry-point class is inside the jar & set /a PASS+=1) else (echo FAIL: entry-point class is inside the jar & set /a FAIL+=1)

jar tf "%JAR%" 2>nul | findstr /r "cards.*json" >nul
if !ERRORLEVEL!==0 (echo PASS: card data resources are inside the jar & set /a PASS+=1) else (echo FAIL: card data resources are inside the jar & set /a FAIL+=1)

java -cp "%JAR%" com.infiniteconquest.cli.InfiniteConquestCli simulate 1 42 "%TEMP%\smoke-report.json" 2>nul | findstr /c:"Simulated 36 matches" >nul
if !ERRORLEVEL!==0 (echo PASS: headless engine - 36 seeded bot matches complete & set /a PASS+=1) else (echo FAIL: headless engine - 36 seeded bot matches complete & set /a FAIL+=1)

:result
echo == result: %PASS% passed, %FAIL% failed ==
if %FAIL% NEQ 0 exit /b 1
exit /b 0
