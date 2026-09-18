@echo off
title Personal NAS Server
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start-nas.ps1"
pause
