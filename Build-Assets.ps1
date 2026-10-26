param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'build'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $OutputDirectory 'BingWallpaperLite.ico'
New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
$frames = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $green = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(15,118,110))
    $sage = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(144,212,187))
    $white = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(245,253,249))
    $sun = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,213,118))
    $graphics.FillEllipse($green, [single]0, [single]0, [single]$size, [single]$size)
    $graphics.FillEllipse($sun, [single]($size*.57), [single]($size*.22), [single]($size*.19), [single]($size*.19))
    $points = [Drawing.PointF[]]@([Drawing.PointF]::new($size*.10,$size*.74), [Drawing.PointF]::new($size*.39,$size*.33), [Drawing.PointF]::new($size*.72,$size*.76))
    $graphics.FillPolygon($white, $points)
    $points = [Drawing.PointF[]]@([Drawing.PointF]::new($size*.39,$size*.77), [Drawing.PointF]::new($size*.65,$size*.48), [Drawing.PointF]::new($size*.88,$size*.77))
    $graphics.FillPolygon($sage, $points)
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $frames += @{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    $green.Dispose(); $sage.Dispose(); $white.Dispose(); $sun.Dispose()
}
$output = [IO.File]::Create($destination)
$writer = [IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $output.Dispose() }
