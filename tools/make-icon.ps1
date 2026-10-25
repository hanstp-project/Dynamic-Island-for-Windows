<#
.SYNOPSIS
  Generates the application icon (multi-resolution .ico) used by the app, tray and installer.
.DESCRIPTION
  Draws the icon procedurally so it stays crisp at every size (16..256 px).
  Run with Windows PowerShell 5.1 or PowerShell 7 on Windows:
      powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
#>
param(
    [string]$OutFile = (Join-Path $PSScriptRoot '..\src\WindowsDynamicIsland\Resources\Icons\app.ico')
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconFrame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [float]$size
    $pad = [Math]::Max(0.5, $s * 0.04)
    $tile = New-RoundedRectPath $pad $pad ($s - 2 * $pad) ($s - 2 * $pad) ($s * 0.22)

    # Tile: deep indigo -> violet gradient
    $rect = New-Object System.Drawing.RectangleF(0, 0, $s, $s)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect,
        [System.Drawing.Color]::FromArgb(255, 74, 92, 255),
        [System.Drawing.Color]::FromArgb(255, 168, 85, 247), 55.0)
    $g.FillPath($grad, $tile)

    # Soft top highlight
    if ($size -ge 32) {
        $hl = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect,
            [System.Drawing.Color]::FromArgb(70, 255, 255, 255),
            [System.Drawing.Color]::FromArgb(0, 255, 255, 255), 90.0)
        $g.FillPath($hl, $tile)
        $hl.Dispose()
    }

    # The island pill
    $pw = $s * 0.62; $ph = [Math]::Max(3.0, $s * 0.22)
    $px = ($s - $pw) / 2; $py = $s * 0.24
    $pill = New-RoundedRectPath $px $py $pw $ph ($ph / 2)
    $g.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 8, 8, 10))), $pill)

    # Camera-style dot + activity dot inside the pill
    if ($size -ge 20) {
        $dot = $ph * 0.42
        $g.FillEllipse((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 52, 211, 153))),
            [float]($px + $pw - $ph * 0.5 - $dot / 2), [float]($py + ($ph - $dot) / 2), [float]$dot, [float]$dot)
    }

    # Lower "content" bars suggesting expansion
    if ($size -ge 32) {
        $bw = $s * 0.46; $bh = [Math]::Max(2.0, $s * 0.07)
        $b1 = New-RoundedRectPath (($s - $bw) / 2) ($s * 0.58) $bw $bh ($bh / 2)
        $g.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 255, 255, 255))), $b1)
        $bw2 = $s * 0.30
        $b2 = New-RoundedRectPath (($s - $bw2) / 2) ($s * 0.71) $bw2 $bh ($bh / 2)
        $g.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(130, 255, 255, 255))), $b2)
    }

    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = @()
foreach ($sz in $sizes) {
    $bmp = New-IconFrame $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += , @{ Size = $sz; Bytes = $ms.ToArray() }
    $bmp.Dispose()
}

$dir = Split-Path -Parent $OutFile
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

$fs = [System.IO.File]::Create($OutFile)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$f.Bytes.Length); $bw.Write([UInt32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $bw.Write($f.Bytes) }
$bw.Close()
Write-Host "Icon written to $OutFile"
