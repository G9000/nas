<#
.SYNOPSIS
    Stops the Personal NAS File Server and Cloudflare Tunnel.
#>

Write-Host "Stopping NAS services..." -ForegroundColor Yellow

$stoppedAny = $false

# Stop FileBrowser
$fbList = Get-Process -Name "filebrowser" -ErrorAction SilentlyContinue
if ($fbList) {
    foreach ($proc in $fbList) {
        Write-Host "Stopping FileBrowser (PID: $($proc.Id))..." -ForegroundColor Cyan
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    $stoppedAny = $true
}

# Stop Cloudflared
$cfList = Get-Process -Name "cloudflared" -ErrorAction SilentlyContinue
if ($cfList) {
    foreach ($proc in $cfList) {
        Write-Host "Stopping Cloudflare Online Tunnel (PID: $($proc.Id))..." -ForegroundColor Cyan
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    $stoppedAny = $true
}

if ($stoppedAny) {
    Write-Host "NAS services stopped successfully." -ForegroundColor Green
} else {
    Write-Host "No NAS services were running." -ForegroundColor Gray
}

Start-Sleep -Seconds 1
