@echo off
title Stop NAS Server
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop-nas.ps1"
pause
