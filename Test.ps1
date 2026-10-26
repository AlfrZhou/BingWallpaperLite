[CmdletBinding()]
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
if(-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') | Out-Null }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$testDirectory = Join-Path $PSScriptRoot 'build\tests'
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$testExe = Join-Path $testDirectory 'CoreTests.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$sources += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'ui') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:exe /main:Tests /reference:System.Drawing.dll /reference:System.Web.Extensions.dll `
    /reference:System.Windows.Forms.dll ('/out:' + $testExe) @sources (Join-Path $PSScriptRoot 'tests\Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $testExe (Join-Path $PSScriptRoot 'build')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
$gcc = Join-Path $PSScriptRoot 'build\tools\w64devkit\bin\gcc.exe'
$nativeTests = Join-Path $testDirectory 'NativeTests.exe'
& $gcc -std=c11 -Os -s -ffunction-sections '-Wl,--gc-sections' `
    -o $nativeTests (Join-Path $PSScriptRoot 'tests\NativeTests.c') -lshell32 -lcomctl32 -ladvapi32 -lgdi32 -luser32 -limm32
if($LASTEXITCODE -ne 0) { throw 'Native test compilation failed.' }
& $nativeTests
if($LASTEXITCODE -ne 0) { throw 'Native tests failed.' }
