@echo off
rem Same game, drawn on the planned 6 x 8 board. The rules engine still plays 4 x 6, so only the centre 24 hexes take moves.
start "Infinite Conquest Playtest board 6x8 preview" /D "%~dp0" "%~dp0InfiniteConquestPlaytest.exe" -board 6x8
