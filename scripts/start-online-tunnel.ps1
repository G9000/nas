<#
.SYNOPSIS
    Starts a secure online tunnel to access your NAS anywhere over the internet.
#>

$RootPath = (Resolve-Path "$PSScriptRoot\..").Path
$BinDir = Join-Path $RootPath "bin"
$CfBin = Join-Path $BinDir "cloudflared.exe"
$DataDir = Join-Path $RootPath "data"
$LogPath = Join-Path $DataDir "cloudflared.log"

if (-not (Test-Path $BinDir)) { New-Item -ItemType Directory -Path $BinDir -Force | Out-Null }
if (-not (Test-Path $DataDir)) { New-Item -ItemType Directory -Path $DataDir -Force | Out-Null }

# Auto-download cloudflared if missing (e.g. after fresh git clone)
if (-not (Test-Path $CfBin)) {
    Write-Host "cloudflared.exe not found. Auto-downloading..." -ForegroundColor Cyan
    $cfUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"
    curl.exe -L -o $CfBin $cfUrl
    if (-not (Test-Path $CfBin)) {
        Write-Error "Failed to download cloudflared binary."
        exit 1
    }
}

# Ensure FileBrowser is running
$fb = Get-Process -Name "filebrowser" -ErrorAction SilentlyContinue
if (-not $fb) {
    Write-Host "FileBrowser is not running. Starting it now..." -ForegroundColor Yellow
    & "$PSScriptRoot\start-nas.ps1" -NoBrowser
}

# Check if tunnel is already running
$cfProcs = Get-Process -Name "cloudflared" -ErrorAction SilentlyContinue
if ($cfProcs) {
    Write-Host "Cleaning up existing cloudflared tunnels..." -ForegroundColor DarkGray
    foreach ($p in $cfProcs) {
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1
}

if (Test-Path $LogPath) {
    Remove-Item $LogPath -Force -ErrorAction SilentlyContinue
}

Write-Host "Creating secure online HTTPS tunnel for your NAS..." -ForegroundColor Cyan
Write-Host "Connecting to Cloudflare global network..." -ForegroundColor DarkGray

$proc = Start-Process -FilePath $CfBin -ArgumentList "tunnel", "--url", "http://localhost:8080" -PassThru -WindowStyle Hidden -RedirectStandardError $LogPath

$tunnelUrl = $null
$timeout = 25
$elapsed = 0

while ($elapsed -lt $timeout) {
    Start-Sleep -Seconds 1
    $elapsed++
    if (Test-Path $LogPath) {
        $log = Get-Content $LogPath -Raw -ErrorAction SilentlyContinue
        if ($log) {
            $match = [regex]::Match($log, "https://[a-zA-Z0-9-]+\.trycloudflare\.com")
            if ($match.Success) {
                $tunnelUrl = $match.Value
                break
            }
        }
    }
}

if ($tunnelUrl) {
    try {
        Set-Clipboard -Value $tunnelUrl
        $copied = "(Copied to clipboard!)"
    } catch {
        $copied = ""
    }

    Clear-Host
    Write-Host "===============================================================" -ForegroundColor Green
    Write-Host "             YOUR NAS IS NOW ACCESSIBLE ONLINE!                " -ForegroundColor Green
    Write-Host "===============================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host " [PUBLIC ONLINE URL]" -ForegroundColor Yellow
    Write-Host "   $tunnelUrl  $copied" -ForegroundColor Green
    Write-Host ""
    Write-Host " [HOW TO USE]" -ForegroundColor Yellow
    Write-Host "   - Open this link from ANY phone, laptop, or computer anywhere" -ForegroundColor White
    Write-Host "     in the world (even on 4G/5G or coffee shop Wi-Fi)!" -ForegroundColor White
    Write-Host "   - Sign in with the username and password configured in the NAS web interface." -ForegroundColor White
    Write-Host ""
    Write-Host " [SECURITY]" -ForegroundColor Yellow
    Write-Host "   - Browser traffic uses HTTPS to Cloudflare; Cloudflare terminates that connection." -ForegroundColor DarkGray
    Write-Host "   - This PC connects to Cloudflare through an encrypted tunnel." -ForegroundColor DarkGray
    Write-Host "   - No router port-forwarding required." -ForegroundColor DarkGray
    Write-Host ""
    Write-Host " Press [ENTER] to stop online access. After the tunnel stops, press any key to close this window." -ForegroundColor Yellow
    Write-Host "===============================================================" -ForegroundColor Green
    
    # Wait for user input to stop
    Read-Host
    
    Write-Host "Stopping online tunnel..." -ForegroundColor Yellow
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-Host "Online tunnel closed. Your local NAS is still running on your home Wi-Fi." -ForegroundColor Green
    Start-Sleep -Seconds 2
} else {
    Write-Error "Failed to establish Cloudflare tunnel within $timeout seconds. Check $LogPath for details."
}
