[CmdletBinding()]
param([string]$IsccPath)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build.ps1') | Out-Null
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Version.txt') -Raw).Trim()
if (-not $IsccPath) {
    $candidates = @((Join-Path $PSScriptRoot 'build\tools\InnoSetup\ISCC.exe'), 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe', 'C:\Program Files\Inno Setup 6\ISCC.exe')
    $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $IsccPath) { throw 'Inno Setup 6 compiler not found. Pass -IsccPath with the path to ISCC.exe.' }
$compileArguments = @('/DAppVersion=' + $version)
$compileArguments += (Join-Path $PSScriptRoot 'installer\BingWallpaperLite.iss')
& $IsccPath @compileArguments
if($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$packageName = 'BingWallpaperLite-Setup-' + $version + '.exe'
$package = Join-Path $PSScriptRoot ('dist\' + $packageName)
$hash = Get-FileHash -LiteralPath $package -Algorithm SHA256
($hash.Hash + '  ' + (Split-Path -Leaf $package)) | Set-Content -LiteralPath ($package + '.sha256') -Encoding ASCII
Write-Output $package
