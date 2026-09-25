$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$serverPath = Join-Path $projectRoot 'bin\filebrowser.exe'
$licenseSourcePath = Join-Path $projectRoot 'bin\LICENSE'
$outputPath = Join-Path $projectRoot 'PersonalNAS.exe'
$licenseOutputPath = Join-Path $projectRoot 'PersonalNAS-FileBrowser-LICENSE.txt'
$sourcePaths = @(
    (Join-Path $projectRoot 'src\PersonalNAS\Program.cs'),
    (Join-Path $projectRoot 'src\PersonalNAS\TrayApplicationContext.cs'),
    (Join-Path $projectRoot 'src\PersonalNAS\NasServer.cs'),
    (Join-Path $projectRoot 'src\PersonalNAS\NetworkAddress.cs')
)

if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
    throw "Required build input is missing: $serverPath. Restore the pinned File Browser executable to bin\filebrowser.exe, then rerun this script."
}
if (-not (Test-Path -LiteralPath $licenseSourcePath -PathType Leaf)) {
    throw "Required build input is missing: $licenseSourcePath. Restore the File Browser LICENSE file to bin\LICENSE, then rerun this script."
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
