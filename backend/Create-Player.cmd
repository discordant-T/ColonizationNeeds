@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Create-Player.ps1" %*
pause
