[CmdletBinding()]
param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{455EC966-AC96-4FC3-8B47-A30D9D0EBB1B}_is1'
if(-not(Test-Path -LiteralPath $key)) { throw 'Bing Wallpaper Lite installer registration was not found. Use Windows Settings > Apps to check the installation.' }
$uninstaller = (Get-ItemProperty -LiteralPath $key).UninstallString.Trim('"')
$options = if($Quiet) { @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') } else { @() }
$process=Start-Process -FilePath $uninstaller -ArgumentList $options -Wait -PassThru
if($process.ExitCode -ne 0) { throw "Uninstaller exit code: $($process.ExitCode)" }
