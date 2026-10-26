[CmdletBinding()]
param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Version.txt') -Raw).Trim()
$package = Join-Path $PSScriptRoot ('dist\BingWallpaperLite-Setup-' + $version + '.exe')
if(-not (Test-Path -LiteralPath $package)) { & (Join-Path $PSScriptRoot 'Package.ps1') }
$options = if($Quiet) { @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') } else { @() }
$installer = Start-Process -FilePath $package -ArgumentList $options -PassThru -Wait
if($installer.ExitCode -ne 0) { throw "Installer exit code: $($installer.ExitCode)" }
