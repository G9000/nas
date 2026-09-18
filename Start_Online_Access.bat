@echo off
title Personal NAS - Online Access (Cloudflare Tunnel)
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start-online-tunnel.ps1"
pause
