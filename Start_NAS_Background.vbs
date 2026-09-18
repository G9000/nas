' Silent launcher for Personal NAS (Runs in background without CMD window)
Set WshShell = CreateObject("WScript.Shell")
strPath = WshShell.CurrentDirectory
WshShell.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -File """ & strPath & "\scripts\start-nas.ps1"" -NoBrowser", 0, False
Set WshShell = Nothing
