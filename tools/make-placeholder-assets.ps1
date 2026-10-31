# Draws simple placeholder graphics so the app builds without the original paintings:
#   src\Assets\bg-day.jpg, bg-night.jpg   (2240 x 1260, soft gradient sky with hills)
#   src\Assets\mascot.png                 (512 px, transparent kite)
#   src\Assets\icon-256.png, nakawari.ico (kite badge, 16-256 px)
# The real Nakawari art is not part of the open-source release. To use your own paintings,
# put them in art\raw (day.png, night.png, mascot.png, icon.png) and run tools\make-assets.ps1 instead.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'src\Assets'
New-Item -ItemType Directory -Force $out | Out-Null

function NewCanvas([int]$width, [int]$height) {
    $bmp = New-Object Drawing.Bitmap $width, $height, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    return $bmp, $g
}

function Color([string]$hex) { return [Drawing.ColorTranslator]::FromHtml($hex) }

function SaveJpeg([Drawing.Bitmap]$bmp, [string]$path, [long]$quality) {
    $codec = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
    $params = New-Object Drawing.Imaging.EncoderParameters 1
    $params.Param[0] = New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality), $quality
    $bmp.Save($path, $codec, $params)
}

# Sky gradient plus three rounded hills; the card UI floats over the lower half.
function Background([string]$path, [string]$skyTop, [string]$skyBottom, [string[]]$hills, [string]$moon) {
    $w = 2240; $h = 1260
    $bmp, $g = NewCanvas $w $h
    $sky = New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.Point]::new(0, 0)), ([Drawing.Point]::new(0, $h)), (Color $skyTop), (Color $skyBottom)
    $g.FillRectangle($sky, 0, 0, $w, $h)
    if ($moon) {
        $g.FillEllipse((New-Object Drawing.SolidBrush (Color $moon)), [single]($w * 0.78), [single]($h * 0.12), [single]150, [single]150)
    }
    $i = 0
    foreach ($hill in $hills) {
        $brush = New-Object Drawing.SolidBrush (Color $hill)
        $top = $h * (0.62 + 0.11 * $i)
        $g.FillEllipse($brush, [single](-$w * 0.2 + $i * $w * 0.25), [single]$top, [single]($w * 1.1), [single]($h * 0.9))
        $i++
    }
    SaveJpeg $bmp $path 88
    $g.Dispose(); $bmp.Dispose()
}

# A kite: diamond, cross spars and a short tail. Drawn into a square of the given size.
function DrawKite([Drawing.Graphics]$g, [int]$size, [string]$fill, [string]$spar) {
    $cx = $size / 2; $cy = $size * 0.42
    $half = $size * 0.26
    $points = [Drawing.PointF[]]@(
        [Drawing.PointF]::new($cx, $cy - $half * 1.25),
        [Drawing.PointF]::new($cx + $half, $cy),
        [Drawing.PointF]::new($cx, $cy + $half * 1.25),
        [Drawing.PointF]::new($cx - $half, $cy)
    )
    $g.FillPolygon((New-Object Drawing.SolidBrush (Color $fill)), $points)
    $pen = New-Object Drawing.Pen (Color $spar), ([single][Math]::Max(2, $size / 48))
    $g.DrawLine($pen, $points[0], $points[2])
    $g.DrawLine($pen, $points[1], $points[3])
    $g.DrawPolygon($pen, $points)
    $tail = New-Object Drawing.Pen (Color $spar), ([single][Math]::Max(2, $size / 64))
    $g.DrawBezier($tail, $points[2], [Drawing.PointF]::new($cx + $size * 0.12, $cy + $half * 1.6),
        [Drawing.PointF]::new($cx - $size * 0.12, $cy + $half * 1.9), [Drawing.PointF]::new($cx + $size * 0.06, $size * 0.96))
}

function RoundedRect([int]$x, [int]$y, [int]$w, [int]$h, [int]$r) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $y, $r, $r, 180, 90)
    $path.AddArc($x + $w - $r, $y, $r, $r, 270, 90)
    $path.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0, 90)
    $path.AddArc($x, $y + $h - $r, $r, $r, 90, 90)
    $path.CloseFigure()
    return $path
}

function Badge([int]$size) {
    $bmp, $g = NewCanvas $size $size
    $inset = [int]($size * 0.04); $radius = [Math]::Max(2, [int]($size * 0.28))
    $g.FillPath((New-Object Drawing.SolidBrush (Color '#FBF8F1')), (RoundedRect $inset $inset ($size - 2 * $inset) ($size - 2 * $inset) $radius))
    DrawKite $g $size '#D94F3D' '#2B3642'
    $g.Dispose()
    return $bmp
}

# Backgrounds: colours match the Day and Night theme palettes.
Background (Join-Path $out 'bg-day.jpg') '#BFDDF2' '#F6F1E3' @('#9CC18C', '#7FAE7A', '#6A9A6B') $null
Background (Join-Path $out 'bg-night.jpg') '#121A33' '#2A3658' @('#223050', '#1B2745', '#141E38') '#E8E4D2'

# Mascot: a kite on a transparent square.
$mascot, $g = NewCanvas 512 512
DrawKite $g 512 '#D94F3D' '#2B3642'
$g.Dispose()
$mascot.Save((Join-Path $out 'mascot.png'), [Drawing.Imaging.ImageFormat]::Png)
$mascot.Dispose()

# Icon: 256 px PNG frame plus 32-bit DIB frames, the same layout make-assets.ps1 writes.
$sizes = 16, 24, 32, 48, 64, 128, 256
function DibFrame([Drawing.Bitmap]$f) {
    $s = $f.Width
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms
    $w.Write([UInt32]40); $w.Write([Int32]$s); $w.Write([Int32]($s * 2))
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)
    $w.Write([UInt32]0); $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $f.GetPixel($x, $y)
            $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A)
        }
    }
    $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
    $w.Write((New-Object byte[] ($maskRow * $s)))
    return , $ms.ToArray()
}

$frames = foreach ($s in $sizes) {
    $f = Badge $s
    if ($s -ge 256) {
        $ms = New-Object IO.MemoryStream
        $f.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
        , $ms.ToArray()
    }
    else { DibFrame $f }
    $f.Dispose()
}
[IO.File]::WriteAllBytes((Join-Path $out 'icon-256.png'), $frames[-1])

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

Get-ChildItem $out -File | Select-Object Name, @{ n = 'KB'; e = { [int]($_.Length / 1KB) } }
