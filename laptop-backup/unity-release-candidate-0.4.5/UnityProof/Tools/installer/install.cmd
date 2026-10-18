@echo off
rem IExpress entry point: runs the PowerShell installer from the extraction folder.
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0install.ps1"
