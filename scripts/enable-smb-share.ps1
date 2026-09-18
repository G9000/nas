<#
.SYNOPSIS
    Enables Windows Native SMB File Sharing for the NAS Storage folder.
#>

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "Elevating to Administrator to configure Windows Network Sharing..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

$RootPath = (Resolve-Path "$PSScriptRoot\..").Path
$StoragePath = Join-Path $RootPath "storage"
$ShareName = "NAS"

Write-Host "Configuring Windows Network Share for: $StoragePath" -ForegroundColor Cyan

# Enable File and Printer Sharing in Windows Firewall for Private Networks
try {
    Enable-NetFirewallRule -DisplayGroup "File and Printer Sharing" -ErrorAction SilentlyContinue
    Write-Host "[OK] Enabled File and Printer Sharing firewall rule." -ForegroundColor Green
} catch {
    Write-Host "[!] Could not automatically update firewall rule: $_" -ForegroundColor Yellow
}

# Check if share already exists
$existingShare = Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue

if ($existingShare) {
    Write-Host "[INFO] SMB Share '$ShareName' already exists pointing to $($existingShare.Path)." -ForegroundColor Yellow
    if ($existingShare.Path -ne $StoragePath) {
        Write-Host "Updating share path to $StoragePath..." -ForegroundColor Cyan
        Remove-SmbShare -Name $ShareName -Force
        New-SmbShare -Name $ShareName -Path $StoragePath -FullAccess "Everyone" | Out-Null
    }
} else {
    try {
        New-SmbShare -Name $ShareName -Path $StoragePath -FullAccess "Everyone" | Out-Null
        Write-Host "[OK] Created SMB Network Share: $ShareName" -ForegroundColor Green
    } catch {
        Write-Host "[!] Failed to create SMB share: $_" -ForegroundColor Red
        Read-Host "Press Enter to exit..."
        exit 1
    }
}

# Grant NTFS file permissions to Everyone for local sharing
try {
    $acl = Get-Acl $StoragePath
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule("Everyone", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.AddAccessRule($rule)
    Set-Acl $StoragePath $acl
    Write-Host "[OK] NTFS Permissions configured for network access." -ForegroundColor Green
} catch {
    Write-Host "[!] Could not set NTFS permissions: $_" -ForegroundColor Yellow
}

# Get computer name and IP
$compName = $env:COMPUTERNAME
$ips = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { 
    $_.InterfaceAlias -notlike "*Loopback*" -and 
    $_.IPAddress -notlike "169.254.*" -and 
    $_.IPAddress -notlike "172.20.*" -and
    $_.IPAddress -notlike "172.17.*"
}
$wifiIp = ($ips | Select-Object -ExpandProperty IPAddress -First 1)

Clear-Host
Write-Host "===============================================================" -ForegroundColor Green
Write-Host "      WINDOWS FILE EXPLORER (SMB) SHARING IS ACTIVE!           " -ForegroundColor Green
Write-Host "===============================================================" -ForegroundColor Green
Write-Host ""
Write-Host " [HOW TO CONNECT FROM YOUR DESKTOP PC (SAME WI-FI)]" -ForegroundColor Yellow
Write-Host "   Method 1: Open File Explorer on your Desktop and type:" -ForegroundColor White
Write-Host "             \\$compName\$ShareName" -ForegroundColor Cyan
Write-Host "             or" -ForegroundColor White
Write-Host "             \\$wifiIp\$ShareName" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Method 2: Map as a Permanent Network Drive (e.g. Z: Drive):" -ForegroundColor White
Write-Host "             1. On your Desktop, open 'This PC' in File Explorer." -ForegroundColor DarkGray
Write-Host "             2. Click the '...' or 'Map network drive' button." -ForegroundColor DarkGray
Write-Host "             3. Choose Drive letter 'Z:' and enter: \\$wifiIp\$ShareName" -ForegroundColor DarkGray
Write-Host "             4. Check 'Reconnect at sign-in' and click Finish!" -ForegroundColor DarkGray
Write-Host ""
Write-Host "   Any file you drop into this folder on your laptop or desktop" -ForegroundColor White
Write-Host "   will instantly appear on both machines!" -ForegroundColor White
Write-Host "===============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Press [ENTER] to close this window."
Read-Host
