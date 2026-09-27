@echo off
rem fetch-source.bat -- fetch the pinned alpha source READ-ONLY into the build area.
rem Creates build\alpha-src, fetches exactly one commit from GitHub, checks it
rem out, and FAILS unless the checkout equals the pin. The upstream repo is
rem never modified. Usage: fetch-source.bat
setlocal
set PIN=992bc95c7164416ea0a25a4ce120f6ec0a0a167a
if defined ALPHA_PIN set PIN=%ALPHA_PIN%
set REPO=https://github.com/Mrice90/TubaExperiment.git
if defined ALPHA_REPO set REPO=%ALPHA_REPO%
set DEST=%~dp0build\alpha-src

where git >nul 2>nul || (echo ERROR: git not found on PATH. & exit /b 1)

if exist "%DEST%\.git" (
  echo == reusing existing checkout at %DEST% ==
) else (
  echo == fetching pinned source ==
  echo repo: %REPO%
  echo pin:  %PIN%
  if exist "%DEST%" rmdir /s /q "%DEST%"
  mkdir "%DEST%"
  git init -q "%DEST%" || exit /b 1
  git -C "%DEST%" remote add origin "%REPO%" || exit /b 1
  git -C "%DEST%" fetch -q --depth 1 origin "%PIN%" || exit /b 1
  git -C "%DEST%" checkout -q FETCH_HEAD || exit /b 1
)

for /f %%h in ('git -C "%DEST%" rev-parse HEAD') do set HEAD=%%h
echo checked out: %HEAD%
if not "%HEAD%"=="%PIN%" (
  echo ERROR: checkout (%HEAD%) does not match pin (%PIN%^); refusing to continue.
  exit /b 1
)
echo OK: source matches pin %PIN% (read-only; upstream untouched)
