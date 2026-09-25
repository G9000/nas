$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$serverPath = Join-Path $projectRoot 'bin\filebrowser.exe'
$licenseSourcePath = Join-Path $projectRoot 'bin\LICENSE'
$outputPath = Join-Path $projectRoot 'PersonalNAS.exe'
$licenseOutputPath = Join-Path $projectRoot 'PersonalNAS-FileBrowser-LICENSE.txt'
# The archive hash is published in the official checksum file; the executable hash is from that verified archive.
$expectedArchiveSha256 = 'fdb1d86dfafff8b3861867c7797ce786570013088678e03de17cfd9476c72384'
$expectedServerSha256 = '1104fe179fdd6be245473fe212f93ff1169230830ff7664f25e01f034082fe05'
$sourcePaths = @(
    (Join-Path $projectRoot 'src\PersonalNAS\Program.cs'),
    (Join-Path $projectRoot 'src\PersonalNAS\TrayApplicationContext.cs'),
    (Join-Path $projectRoot 'src\PersonalNAS\NasServer.cs'),
    (Join-Path $projectRoot 'src\PersonalNAS\NetworkAddress.cs')
)

if (-not (Test-Path -LiteralPath $licenseSourcePath -PathType Leaf)) {
    throw "Required build input is missing: $licenseSourcePath. Restore the File Browser LICENSE file to bin\LICENSE, then rerun this script."
}

if (Test-Path -LiteralPath $serverPath -PathType Leaf) {
    $existingServerSha256 = (Get-FileHash -LiteralPath $serverPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($existingServerSha256 -ne $expectedServerSha256) {
        throw "The existing File Browser executable does not match the pinned v2.63.23 release (SHA-256 $expectedServerSha256). Restore the expected binary to $serverPath before building."
    }
}
else {
    $releaseTag = 'v2.63.23'
    $releaseUrl = "https://github.com/filebrowser/filebrowser/releases/download/$releaseTag/windows-amd64-filebrowser.zip"
    $tempBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
    $tempDirectoryName = "PersonalNAS-build-$([guid]::NewGuid().ToString('N'))"
    $tempRoot = [System.IO.Path]::GetFullPath((Join-Path $tempBase $tempDirectoryName)).TrimEnd('\')
    $tempBasePrefix = $tempBase.TrimEnd('\') + '\'
    if (-not $tempRoot.StartsWith($tempBasePrefix, [System.StringComparison]::OrdinalIgnoreCase) -or [System.IO.Path]::GetFileName($tempRoot) -ne $tempDirectoryName) {
        throw 'Refusing to use an unexpected temporary build directory.'
    }
    $archivePath = Join-Path $tempRoot 'filebrowser.zip'
    $extractPath = Join-Path $tempRoot 'extracted'
    $tempRootCreated = $false

    try {
        New-Item -ItemType Directory -Path $tempRoot -ErrorAction Stop | Out-Null
        $tempRootCreated = $true
        New-Item -ItemType Directory -Path $extractPath -ErrorAction Stop | Out-Null
        Write-Host "Downloading pinned File Browser $releaseTag..."
        Invoke-WebRequest -Uri $releaseUrl -OutFile $archivePath -UseBasicParsing
        if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf) -or (Get-Item -LiteralPath $archivePath).Length -eq 0) {
            throw "The pinned File Browser archive was empty or missing: $releaseUrl"
        }
        $archiveSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($archiveSha256 -ne $expectedArchiveSha256) {
            throw "The downloaded File Browser archive failed SHA-256 verification. Expected $expectedArchiveSha256, got $archiveSha256."
        }

        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath -ErrorAction Stop
        $extractedBinaries = @(Get-ChildItem -LiteralPath $extractPath -Filter 'filebrowser.exe' -File -Recurse)
        if ($extractedBinaries.Count -ne 1) {
            throw "Expected one filebrowser.exe in the pinned release archive, found $($extractedBinaries.Count)."
        }
        $extractedServerSha256 = (Get-FileHash -LiteralPath $extractedBinaries[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($extractedServerSha256 -ne $expectedServerSha256) {
            throw "The extracted File Browser executable failed SHA-256 verification. Expected $expectedServerSha256, got $extractedServerSha256."
        }

        if (-not (Test-Path -LiteralPath (Split-Path -Parent $serverPath) -PathType Container)) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $serverPath) -ErrorAction Stop | Out-Null
        }
        Copy-Item -LiteralPath $extractedBinaries[0].FullName -Destination $serverPath -ErrorAction Stop
        Write-Host "Restored the pinned File Browser binary to $serverPath"
    }
    finally {
        $cleanupTarget = if ($tempRootCreated) { Get-Item -LiteralPath $tempRoot -ErrorAction SilentlyContinue }
        if ($cleanupTarget -and $cleanupTarget.PSIsContainer) {
            $resolvedCleanupPath = [System.IO.Path]::GetFullPath($cleanupTarget.FullName).TrimEnd('\')
            if ($resolvedCleanupPath -ne $tempRoot -or -not $resolvedCleanupPath.StartsWith($tempBasePrefix, [System.StringComparison]::OrdinalIgnoreCase) -or ($cleanupTarget.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
                throw "Refusing to remove an unexpected temporary build directory: $resolvedCleanupPath"
            }
            Remove-Item -LiteralPath $resolvedCleanupPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
foreach ($sourcePath in $sourcePaths) {
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required launcher source file is missing: $sourcePath. Restore it before building."
    }
}

if ([string]::IsNullOrWhiteSpace($env:WINDIR)) {
    throw 'WINDIR is not set, so the .NET Framework C# compiler cannot be located.'
}

$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath -PathType Leaf)) {
    throw "The .NET Framework x64 C# compiler was not found at $compilerPath. Install the .NET Framework 4.x developer tools or restore csc.exe at that path."
}

$compilerArguments = @(
    '-nologo',
    '-target:winexe',
    '-platform:x64',
    "-out:$outputPath",
    '-reference:System.dll',
    '-reference:System.Core.dll',
    '-reference:System.Drawing.dll',
    '-reference:System.Windows.Forms.dll',
    "-resource:$serverPath,PersonalNAS.FileBrowser.exe"
) + $sourcePaths

Push-Location $projectRoot
try {
    & $compilerPath @compilerArguments
    if ($LASTEXITCODE -ne 0) {
        throw "The C# compiler failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf) -or (Get-Item -LiteralPath $outputPath).Length -eq 0) {
    throw "The compiler did not produce a non-empty launcher at $outputPath."
}

Copy-Item -LiteralPath $licenseSourcePath -Destination $licenseOutputPath -Force
if (-not (Test-Path -LiteralPath $licenseOutputPath -PathType Leaf)) {
    throw "The File Browser license was not copied to $licenseOutputPath."
}

Write-Host "Built $outputPath"
Write-Host "Copied File Browser license to $licenseOutputPath"
