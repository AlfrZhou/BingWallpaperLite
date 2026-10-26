param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'build'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\Bing.ico') -Destination (Join-Path $OutputDirectory 'BingWallpaperLite.ico') -Force