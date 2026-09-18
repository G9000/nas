<#
.SYNOPSIS
    Fixes Windows Firewall and Network Profile to allow local Wi-Fi devices to connect to NAS.
#>

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "Requesting Administrator access to unblock Windows Firewall..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

Clear-Host
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "           UNBLOCKING WINDOWS FIREWALL FOR NAS                 " -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Update existing filebrowser.exe rules from Block to Allow
$fbRules = Get-NetFirewallRule | Where-Object { $_.DisplayName -like "*filebrowser*" }
if ($fbRules) {
    foreach ($r in $fbRules) {
        Set-NetFirewallRule -Name $r.Name -Action Allow -Profile Any -Enabled True
        Write-Host "[OK] Changed '$($r.DisplayName)' rule from Block to ALLOW." -ForegroundColor Green
    }
}

# 2. Add explicit TCP Port 8080 inbound rule
$portRule = Get-NetFirewallRule -DisplayName "Personal NAS (Port 8080)" -ErrorAction SilentlyContinue
if (-not $portRule) {
    New-NetFirewallRule -DisplayName "Personal NAS (Port 8080)" -Direction Inbound -LocalPort 8080 -Protocol TCP -Action Allow -Profile Any | Out-Null
    Write-Host "[OK] Created Inbound Firewall Rule for TCP Port 8080." -ForegroundColor Green
} else {
    Set-NetFirewallRule -DisplayName "Personal NAS (Port 8080)" -Action Allow -Profile Any -Enabled True
    Write-Host "[OK] Updated Inbound Firewall Rule for TCP Port 8080 to ALLOW." -ForegroundColor Green
}

# 3. Switch Wi-Fi network profile to Private (Home)
try {
    $wifi = Get-NetConnectionProfile | Where-Object { $_.InterfaceAlias -eq "Wi-Fi" }
    if ($wifi -and $wifi.NetworkCategory -ne "Private") {
        Set-NetConnectionProfile -Name $wifi.Name -NetworkCategory Private
        Write-Host "[OK] Set Wi-Fi network '$($wifi.Name)' to PRIVATE (Home Network)." -ForegroundColor Green
    } else {
        Write-Host "[OK] Wi-Fi network category is already Private." -ForegroundColor Green
    }
} catch {
    Write-Host "[!] Could not switch Wi-Fi profile: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "===============================================================" -ForegroundColor Green
Write-Host "     SUCCESS! Port 8080 is now fully open on your Wi-Fi.       " -ForegroundColor Green
Write-Host "===============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Now try refreshing http://192.168.100.130:8080 on your other computer!" -ForegroundColor Yellow
Write-Host ""
Write-Host "Press [ENTER] to close this window."
Read-Host
