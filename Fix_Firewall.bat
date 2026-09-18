@echo off
title Unblock NAS Firewall
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\fix-firewall.ps1"
