# Run in 64-bit Windows PowerShell 5.1. No external package manager is used.
[CmdletBinding(DefaultParameterSetName='Directory')]
param(
    [Parameter(ParameterSetName='Directory')][string]$PayloadDirectory = '',
    [Parameter(Mandatory=$true,ParameterSetName='Installer')][string]$InstallerExe
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $PayloadDirectory) { $PayloadDirectory = Join-Path $PSScriptRoot 'payload' }
if ($PSVersionTable.PSEdition -ne 'Desktop' -or -not [Environment]::Is64BitProcess) {
    throw 'Use 64-bit Windows PowerShell 5.1 (powershell.exe), not PowerShell 7.'
}
function Read-Json([string]$Path) { Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
function Check-Payload([string]$Path, $Expected) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw ('Missing payload: ' + $Path) }
    if ((Get-Item -LiteralPath $Path).Length -ne $Expected.Length -or
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected.Sha256) {
        throw ('Payload SHA-256/length mismatch: ' + $Expected.Name)
    }
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4 C# compiler not found.' }
$manifestPath = Join-Path $PSScriptRoot 'package\manifest.json'
$manifest = Read-Json $manifestPath
$build = Join-Path $PSScriptRoot 'build'
$dist = Join-Path $PSScriptRoot 'dist'
[IO.Directory]::CreateDirectory($build) | Out-Null
[IO.Directory]::CreateDirectory($dist) | Out-Null
if ($PSCmdlet.ParameterSetName -eq 'Installer') {
    $inputExe = (Resolve-Path -LiteralPath $InstallerExe).Path
    $release = Read-Json (Join-Path $PSScriptRoot 'provenance\release.json')
    if ((Get-Item -LiteralPath $inputExe).Length -ne $release.bytes -or
        (Get-FileHash -LiteralPath $inputExe -Algorithm SHA256).Hash -ne $release.sha256) {
        throw 'This is not the documented AC0 PT-BR.exe release. Check provenance/release.json.'
    }
    # Read embedded data only. The original executable's entry point is never invoked.
    $assembly = [Reflection.Assembly]::LoadFile($inputExe)
    $PayloadDirectory = Join-Path $build 'payload'
    [IO.Directory]::CreateDirectory($PayloadDirectory) | Out-Null
    foreach ($file in $manifest.Files) {
        $inputStream = $assembly.GetManifestResourceStream($file.ResourceName)
        if ($null -eq $inputStream) { throw ('Missing embedded resource: ' + $file.ResourceName) }
        try {
            $outputStream = [IO.File]::Create((Join-Path $PayloadDirectory $file.Name))
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        } finally { $inputStream.Dispose() }
    }
}
$PayloadDirectory = (Resolve-Path -LiteralPath $PayloadDirectory).Path
foreach ($file in $manifest.Files) { Check-Payload (Join-Path $PayloadDirectory $file.Name) $file }
$temporaryExe = Join-Path $build ('installer_' + [Guid]::NewGuid().ToString('N') + '.exe')
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001','/warn:4',
    ('/out:' + $temporaryExe),('/win32icon:' + (Join-Path $PSScriptRoot 'assets\installer.ico')),
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),
    '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll','/reference:System.Web.Extensions.dll',
    ('/resource:' + $manifestPath + ',AC0_PTBR.manifest.json'),
    ('/resource:' + (Join-Path $PSScriptRoot 'assets\titulo.png') + ',AC0_PTBR.titulo.png'),
    ('/resource:' + (Join-Path $PSScriptRoot 'assets\banner.jpg') + ',AC0_PTBR.banner.jpg'),
    ('/resource:' + (Join-Path $PayloadDirectory 'a.arc') + ',AC0_PTBR.a.arc'),
    ('/resource:' + (Join-Path $PayloadDirectory 'd.arc') + ',AC0_PTBR.d.arc'),
    (Join-Path $PSScriptRoot 'src\AssemblyInfo.cs'))
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File |
    Where-Object { $_.Name -ne 'AssemblyInfo.cs' } | Sort-Object Name | ForEach-Object { $_.FullName })
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
$reportPath = Join-Path $build 'package-verification.json'
$process = Start-Process -FilePath $temporaryExe -ArgumentList ('--verificar-pacote "' + $reportPath + '"') -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw 'Embedded package verification failed.' }
$verification = Read-Json $reportPath
if (-not $verification.ok -or $verification.version -ne $manifest.Version) { throw 'Package verification failed.' }
$destination = Join-Path $dist 'AC0 PT-BR.exe'
if ([IO.File]::Exists($destination)) {
    [IO.File]::Replace($temporaryExe, $destination, [System.Management.Automation.Language.NullString]::Value)
} else { [IO.File]::Move($temporaryExe, $destination) }
$report = [ordered]@{ ok = $true; file = 'AC0 PT-BR.exe'; bytes = (Get-Item -LiteralPath $destination).Length;
    sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant(); payload = $verification }
[IO.File]::WriteAllText((Join-Path $dist 'build-report.json'), ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Host ('Built and verified: ' + $destination)
