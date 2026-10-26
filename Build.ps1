[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'build\release'
$objects = Join-Path $PSScriptRoot 'build\obj'
New-Item -ItemType Directory -Path $output -Force | Out-Null
New-Item -ItemType Directory -Path $objects -Force | Out-Null
& (Join-Path $PSScriptRoot 'Build-Assets.ps1') -OutputDirectory $output
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler was not found.' }
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Version.txt') -Raw).Trim()
if($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid Version.txt.' }
$versionInfo = Join-Path $objects 'VersionInfo.cs'
[IO.File]::WriteAllText($versionInfo, ('using System.Reflection;' + [Environment]::NewLine + '[assembly: AssemblyVersion("' + $version + '.0")]' + [Environment]::NewLine + '[assembly: AssemblyFileVersion("' + $version + '.0")]'))
$header = '#define APP_VERSION_TUPLE ' + $version.Replace('.',',') + ',0' + [Environment]::NewLine + '#define APP_VERSION_STRING "' + $version + '.0\0"'
[IO.File]::WriteAllText((Join-Path $objects 'version.h'),$header)
$arguments = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/main:BingWallpaperLite.HelperEntry',
    '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll', '/reference:System.Windows.Forms.dll',
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'src\App.manifest')),
    ('/win32icon:' + (Join-Path $output 'BingWallpaperLite.ico')),
    ('/resource:' + (Join-Path $output 'BingWallpaperLite.ico') + ',BingWallpaperLite.ico'),
    ('/out:' + (Join-Path $output 'BingWallpaperHelper.exe')), $versionInfo)
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'ui') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Helper compilation failed.' }
$nativeTools = Join-Path $PSScriptRoot 'build\tools\w64devkit\bin'
$gcc = Join-Path $nativeTools 'gcc.exe'
$windres = Join-Path $nativeTools 'windres.exe'
if (-not (Test-Path -LiteralPath $gcc)) { throw 'Native compiler missing: place w64devkit in build\tools\w64devkit.' }
Push-Location (Join-Path $PSScriptRoot 'native')
try {
    & $windres resources.rc -o (Join-Path $objects 'resources.o')
    if($LASTEXITCODE -ne 0) { throw 'Native resource compilation failed.' }
    & $gcc -std=c11 -Os -s -municode -mwindows -ffunction-sections '-Wl,--gc-sections,--dynamicbase,--nxcompat,--high-entropy-va,--no-insert-timestamp' `
        -o (Join-Path $output 'BingWallpaperLite.exe') app.c (Join-Path $objects 'resources.o') `
        -lshell32 -luser32 -limm32
    if($LASTEXITCODE -ne 0) { throw 'Native application compilation failed.' }
} finally { Pop-Location }
Write-Output (Join-Path $output 'BingWallpaperLite.exe')
