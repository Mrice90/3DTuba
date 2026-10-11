@echo off
rem Same game, drawn on a 37-hex hexagon board. The rules engine still plays 4 x 6, so only those 24 hexes inside it take moves.
start "Infinite Conquest Playtest hexagon board preview" /D "%~dp0" "%~dp0InfiniteConquestPlaytest.exe" -board hex
