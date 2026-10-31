# Turns your own paintings in art\raw (day.png, night.png, mascot.png, icon.png) into the app's assets:
#   src\Assets\bg-day.jpg, bg-night.jpg   (2240 px wide, JPEG q88)
#   src\Assets\mascot.png                 (512 px, transparent)
#   src\Assets\nakawari.ico               (16-256 px, PNG-compressed entries, cropped to the badge)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$raw = Join-Path $root 'art\raw'
$out = Join-Path $root 'src\Assets'
New-Item -ItemType Directory -Force $out | Out-Null

function Resize([Drawing.Image]$image, [int]$width, [int]$height) {
    $bmp = New-Object Drawing.Bitmap $width, $height, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($image, 0, 0, $width, $height)
    $g.Dispose()
    return $bmp
}

function SaveJpeg([Drawing.Bitmap]$bmp, [string]$path, [long]$quality) {
    $codec = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
    $params = New-Object Drawing.Imaging.EncoderParameters 1
    $params.Param[0] = New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality), $quality
    $bmp.Save($path, $codec, $params)
}

# Smallest square around every pixel that is more than faintly visible.
function OpaqueSquare([Drawing.Bitmap]$bmp) {
    $minX = $bmp.Width; $minY = $bmp.Height; $maxX = 0; $maxY = 0
    for ($y = 0; $y -lt $bmp.Height; $y += 2) {
        for ($x = 0; $x -lt $bmp.Width; $x += 2) {
            if ($bmp.GetPixel($x, $y).A -gt 40) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    $size = [Math]::Max($maxX - $minX, $maxY - $minY) + 8
    $cx = ($minX + $maxX) / 2; $cy = ($minY + $maxY) / 2
    return New-Object Drawing.Rectangle ([int]($cx - $size / 2)), ([int]($cy - $size / 2)), $size, $size
}

foreach ($name in 'day', 'night') {
    $img = [Drawing.Image]::FromFile((Join-Path $raw "$name.png"))
    $bmp = Resize $img 2240 ([int](2240 * $img.Height / $img.Width))
    SaveJpeg $bmp (Join-Path $out "bg-$name.jpg") 88
    $bmp.Dispose(); $img.Dispose()
}

$mascot = [Drawing.Image]::FromFile((Join-Path $raw 'mascot.png'))
$small = Resize $mascot 512 512
$small.Save((Join-Path $out 'mascot.png'), [Drawing.Imaging.ImageFormat]::Png)
$small.Dispose(); $mascot.Dispose()

# Icon: crop to the badge, then write a multi-size .ico with PNG-encoded frames.
$icon = [Drawing.Bitmap]::FromFile((Join-Path $raw 'icon.png'))
$crop = $icon.Clone((OpaqueSquare $icon), $icon.PixelFormat)
$sizes = 16, 24, 32, 48, 64, 128, 256
# 256 px is stored as PNG; smaller sizes as 32-bit DIBs, which every Windows API can decode.
function DibFrame([Drawing.Bitmap]$f) {
    $s = $f.Width
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms
    $w.Write([UInt32]40); $w.Write([Int32]$s); $w.Write([Int32]($s * 2))   # height doubled: XOR + AND mask
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)
    $w.Write([UInt32]0); $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $f.GetPixel($x, $y)
            $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A)
        }
    }
    $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
    $w.Write((New-Object byte[] ($maskRow * $s)))   # all-zero AND mask: alpha channel decides
    return , $ms.ToArray()
}

$frames = foreach ($s in $sizes) {
    $f = Resize $crop $s $s
    if ($s -ge 256) {
        $ms = New-Object IO.MemoryStream
        $f.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
        , $ms.ToArray()
    }
    else { DibFrame $f }
    $f.Dispose()
}
[IO.File]::WriteAllBytes((Join-Path $out 'icon-256.png'), $frames[-1])
$crop.Dispose(); $icon.Dispose()

$ico = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $ico
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$frames[$i].Length); $w.Write([UInt32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $w.Write($frame) }
[IO.File]::WriteAllBytes((Join-Path $out 'nakawari.ico'), $ico.ToArray())

Get-ChildItem $out | Select-Object Name, @{ n = 'KB'; e = { [int]($_.Length / 1KB) } }
