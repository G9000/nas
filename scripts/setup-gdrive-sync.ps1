<#
.SYNOPSIS
    Guide and script to synchronize your NAS storage folder with Google Drive using rclone.
#>

Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "        SYNC NAS STORAGE WITH YOUR GOOGLE DRIVE ACCOUNT        " -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "You can automatically sync your local NAS folder with Google Drive."
Write-Host ""
Write-Host "Steps to set up Google Drive Sync:" -ForegroundColor Yellow
Write-Host " 1. Install rclone (if not already installed):" -ForegroundColor White
Write-Host "    winget install Rclone.Rclone" -ForegroundColor DarkGray
Write-Host ""
Write-Host " 2. Configure your Google Drive connection:" -ForegroundColor White
Write-Host "    rclone config" -ForegroundColor DarkGray
Write-Host "    (Choose 'n' for new remote, name it 'gdrive', select 'drive' for Google Drive," -ForegroundColor DarkGray
Write-Host "     and follow the on-screen browser sign-in)" -ForegroundColor DarkGray
Write-Host ""
Write-Host " 3. Sync commands:" -ForegroundColor White
Write-Host "    - Push local NAS files to Google Drive:" -ForegroundColor DarkGray
Write-Host "      rclone sync `"$PSScriptRoot\..\storage`" gdrive:Personal_NAS -P" -ForegroundColor Green
Write-Host "    - Pull Google Drive files down to your local NAS:" -ForegroundColor DarkGray
Write-Host "      rclone sync gdrive:Personal_NAS `"$PSScriptRoot\..\storage`" -P" -ForegroundColor Green
Write-Host "===============================================================" -ForegroundColor Cyan
