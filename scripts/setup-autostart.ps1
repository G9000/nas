<#
.SYNOPSIS
    Configures FileBrowser NAS to start automatically in the background on Windows startup.
#>
param(
    [switch]$Remove
)

$startupFolder = [Environment]::GetFolderPath("Startup")
$vbsPath = Join-Path $startupFolder "Start_Personal_NAS.vbs"
$RootPath = (Resolve-Path "$PSScriptRoot\..").Path
$BatPath = Join-Path $RootPath "Start_NAS.bat"

if ($Remove) {
    if (Test-Path $vbsPath) {
        Remove-Item $vbsPath -Force
        Write-Host "Removed NAS from Windows startup." -ForegroundColor Green
    } else {
        Write-Host "NAS was not configured in Windows startup." -ForegroundColor Yellow
    }
    exit
}

# Create a silent VBS launcher so it runs in the background with no annoying black CMD window on startup
$vbsContent = @"
Set WshShell = CreateObject("WScript.Shell")
WshShell.Run chr(34) & "$BatPath" & chr(34), 0
Set WshShell = Nothing
"@

Set-Content -Path $vbsPath -Value $vbsContent -Encoding ASCII
Write-Host "===============================================================" -ForegroundColor Green
Write-Host "   SUCCESS: NAS will now start automatically on Windows login! " -ForegroundColor Green
Write-Host "===============================================================" -ForegroundColor Green
Write-Host "Startup script created at: $vbsPath" -ForegroundColor DarkGray
Write-Host "To remove autostart later, run: .\scripts\setup-autostart.ps1 -Remove" -ForegroundColor DarkGray
