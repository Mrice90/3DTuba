@echo off
rem AI-079: build the rules bridge against the pinned alpha JAR and run the
rem protocol tests (determinism, valid act, stale/fake rejection, no mutation
rem on rejection, bot auto-play, scripted GAME_OVER, AI-062 validation of
rem every event, hidden-state redaction). Writes the golden transcript
rem fixture (fixtures\golden-seed-42.jsonl).
rem
rem Usage: run.bat
rem Prereq: infinite-conquest-alpha-*.jar built (.\build-release.bat).
setlocal EnableExtensions
set "SCRIPT_DIR=%~dp0"
python "%SCRIPT_DIR%test_bridge.py"
exit /b %ERRORLEVEL%
