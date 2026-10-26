[CmdletBinding()]
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
if(-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') | Out-Null }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$testDirectory = Join-Path $PSScriptRoot 'build\tests'
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$test = Join-Path $testDirectory 'PaintRegressionTests.exe'
& $compiler /nologo /target:exe /main:BingWallpaperLite.PaintRegressionTests /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    ('/out:' + $test) (Join-Path $PSScriptRoot 'ui\Controls.cs') (Join-Path $PSScriptRoot 'tests\PaintRegressionTests.cs')
if($LASTEXITCODE -ne 0) { throw 'Paint regression test compilation failed.' }
& $test
if($LASTEXITCODE -ne 0) { throw 'Paint regression tests failed.' }
