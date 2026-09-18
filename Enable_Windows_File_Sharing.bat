@echo off
title Enable Windows SMB File Sharing
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\enable-smb-share.ps1"
