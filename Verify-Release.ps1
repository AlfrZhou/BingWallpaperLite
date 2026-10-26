[CmdletBinding()]
param([string]$Version)
$ErrorActionPreference = 'Stop'
if(-not $Version) { $Version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Version.txt') -Raw).Trim() }
$release = Join-Path $PSScriptRoot 'build\release'
$package = Join-Path $PSScriptRoot ('dist\BingWallpaperLite-Setup-' + $Version + '.exe')
$installerSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'installer\BingWallpaperLite.iss'))
if($installerSource -match 'ExecutionPolicy|powershell\.exe|pauseofficial|Stop-Process|Disable-ScheduledTask') { throw 'Retired installer behavior is still present.' }
$payload = [regex]::Match($installerSource,'(?s)\[Files\](.*?)\[Icons\]').Groups[1].Value
if($payload -match '(?i)\.ps1|\.cmd|\.bat') { throw 'Executable scripts found in installer payload.' }
$native = Join-Path $release 'BingWallpaperLite.exe'
$bytes = [IO.File]::ReadAllBytes($native)
$pe = [BitConverter]::ToInt32($bytes,0x3c)
$flags = [BitConverter]::ToUInt16($bytes,$pe + 24 + 70)
$timestamp = [BitConverter]::ToUInt32($bytes,$pe + 8)
if(($flags -band 0x160) -ne 0x160) { throw 'Native ASLR, DEP or high-entropy ASLR flag is missing.' }
if($timestamp -ne 0) { throw 'Native executable is not using the reproducible link timestamp.' }
$imports = & (Join-Path $PSScriptRoot 'build\tools\w64devkit\bin\objdump.exe') -p $native
if($LASTEXITCODE -ne 0) { throw 'Could not inspect native imports.' }
$dlls = @($imports | Select-String 'DLL Name:\s*(\S+)' | ForEach-Object {$_.Matches[0].Groups[1].Value})
foreach($dll in $dlls) { if($dll.ToLowerInvariant() -notin @('kernel32.dll','msvcrt.dll','shell32.dll','user32.dll','imm32.dll')) { throw "Unexpected runtime dependency: $dll" } }
$artifacts = @()
foreach($file in @((Join-Path $release 'BingWallpaperLite.exe'),(Join-Path $release 'BingWallpaperHelper.exe'),$package)) {
    $item = Get-Item -LiteralPath $file
    $actualVersion = $item.VersionInfo.FileVersion.Trim()
    if($actualVersion -ne ($Version + '.0') -and $actualVersion -ne $Version) { throw "Wrong file version: $($item.Name)" }
    if(-not $item.VersionInfo.FileDescription) { throw "Missing file description: $($item.Name)" }
    $artifacts += [ordered]@{Name=$item.Name;Bytes=$item.Length;SHA256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash;FileVersion=$item.VersionInfo.FileVersion;SignatureStatus=[string](Get-AuthenticodeSignature -LiteralPath $file).Status}
}
$expected = ((Get-Content -LiteralPath ($package + '.sha256') -Raw) -split ' ')[0]
if($artifacts[-1].SHA256 -ne $expected) { throw 'Installer checksum does not match its sidecar.' }
$result = [ordered]@{
    Version=$Version
    InstallerRunsPowerShell=$false
    InstallerChangesOtherApplications=$false
    NativeASLR=$true
    NativeDEP=$true
    NativeHighEntropyASLR=$true
    NativeLinkTimestamp=$timestamp
    NativeDependencies=$dlls
    Artifacts=$artifacts
    AntivirusVerdict='Not verified; static build checks are not an antivirus scan.'
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot ('dist\BingWallpaperLite-' + $Version + '-review.json')) -Encoding UTF8
$result | ConvertTo-Json -Depth 5
