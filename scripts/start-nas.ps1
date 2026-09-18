<#
.SYNOPSIS
    Starts the Personal NAS File Server (Auto-installs dependencies if missing).
#>
param (
    [switch]$NoBrowser
)

$RootPath = (Resolve-Path "$PSScriptRoot\..").Path
$BinDir = Join-Path $RootPath "bin"
$BinPath = Join-Path $BinDir "filebrowser.exe"
$DataDir = Join-Path $RootPath "data"
$DbPath = Join-Path $DataDir "filebrowser.db"
$StoragePath = Join-Path $RootPath "storage"
$LogPath = Join-Path $DataDir "nas.log"

# 1. Ensure Directories Exist
if (-not (Test-Path $BinDir)) { New-Item -ItemType Directory -Path $BinDir -Force | Out-Null }
if (-not (Test-Path $DataDir)) { New-Item -ItemType Directory -Path $DataDir -Force | Out-Null }
if (-not (Test-Path $StoragePath)) { New-Item -ItemType Directory -Path $StoragePath -Force | Out-Null }

# 2. Auto-download FileBrowser if missing (e.g. after fresh git clone)
if (-not (Test-Path $BinPath)) {
    Write-Host "filebrowser.exe not found. Auto-downloading latest release..." -ForegroundColor Cyan
    $tag = "v2.63.23"
    $url = "https://github.com/filebrowser/filebrowser/releases/download/$tag/windows-amd64-filebrowser.zip"
    $zipPath = Join-Path $RootPath "filebrowser.zip"
    curl.exe -L -o $zipPath $url
    if (Test-Path $zipPath) {
        Expand-Archive -Path $zipPath -DestinationPath $BinDir -Force
        Remove-Item $zipPath -Force
        Write-Host "FileBrowser downloaded successfully." -ForegroundColor Green
    } else {
        Write-Error "Failed to download FileBrowser binary."
        exit 1
    }
}

# 3. Auto-initialize Database if missing
if (-not (Test-Path $DbPath)) {
    Write-Host "Initializing new NAS database..." -ForegroundColor Cyan
    & $BinPath config init -d $DbPath -a 0.0.0.0 -p 8080 -r $StoragePath --branding.name "My Personal NAS" --minimumPasswordLength 4 | Out-Null
    & $BinPath users add admin NasAdmin2026! -d $DbPath --perm.admin | Out-Null
    Write-Host "Database initialized with default user 'admin'." -ForegroundColor Green
}

# 4. Check if already running
$existing = Get-Process -Name "filebrowser" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "FileBrowser is already running (PID: $($existing.Id))." -ForegroundColor Yellow
} else {
    Write-Host "Starting FileBrowser NAS server..." -ForegroundColor Cyan
    $proc = Start-Process -FilePath $BinPath -ArgumentList "-d `"$DbPath`" -r `"$StoragePath`" -a 0.0.0.0 -p 8080 --log `"$LogPath`"" -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 1
}

# 5. Get local IP addresses
$ips = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { 
    $_.InterfaceAlias -notlike "*Loopback*" -and 
    $_.IPAddress -notlike "169.254.*" -and 
    $_.IPAddress -notlike "172.20.*" -and
    $_.IPAddress -notlike "172.17.*"
}

$wifiIp = ($ips | Select-Object -ExpandProperty IPAddress -First 1)
if (-not $wifiIp) {
    $wifiIp = "127.0.0.1"
}

Clear-Host
Write-Host "===============================================================" -ForegroundColor Green
Write-Host "           PERSONAL NAS FILE SERVER IS ACTIVE!                 " -ForegroundColor Green
Write-Host "===============================================================" -ForegroundColor Green
Write-Host ""
Write-Host " [ACCESS LINKS]" -ForegroundColor Yellow
Write-Host "   - This PC (Localhost):    http://localhost:8080" -ForegroundColor Cyan
Write-Host "   - Wi-Fi Network Access:   http://$($wifiIp):8080" -ForegroundColor Green
Write-Host "     (Open this link from your Desktop PC, Phone, or Tablet)"
Write-Host ""
Write-Host " [LOGIN CREDENTIALS]" -ForegroundColor Yellow
Write-Host "   - Username: admin" -ForegroundColor White
Write-Host "   - Password: NasAdmin2026!" -ForegroundColor White
Write-Host "   (You can change the password at any time inside the Web UI Settings)" -ForegroundColor DarkGray
Write-Host ""
Write-Host " [STORAGE FOLDER]" -ForegroundColor Yellow
Write-Host "   - Location: $StoragePath" -ForegroundColor White
Write-Host "   (Files and folders placed here appear immediately in your NAS)"
Write-Host ""
Write-Host " [SHORTCUTS]" -ForegroundColor Yellow
Write-Host "   - To host online for internet access: Run Start_Online_Access.bat"
Write-Host "   - To access via Windows File Explorer: Run Enable_Windows_File_Sharing.bat"
Write-Host "   - To stop the server: Run Stop_NAS.bat"
Write-Host "===============================================================" -ForegroundColor Green
Write-Host ""

if (-not $NoBrowser) {
    Start-Process "http://localhost:8080"
}
