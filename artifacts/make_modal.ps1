Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$srcRef = Join-Path $PSScriptRoot "group1213\Group1213.png"
$out = Join-Path $root "SwixyQuestBook\assets\swixyquestbook\textures\modal.png"

$ref = [System.Drawing.Bitmap]::FromFile($srcRef)
$w = $ref.Width
$h = $ref.Height
Write-Output "ref ${w}x${h}"

$face = $ref.GetPixel(390, 300)
Write-Output "face $($face.R),$($face.G),$($face.B)"

$bmp = New-Object System.Drawing.Bitmap $ref
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$brush = New-Object System.Drawing.SolidBrush $face

function Cover([int]$x, [int]$y, [int]$cw, [int]$ch) {
  if ($x -lt 0) { $cw += $x; $x = 0 }
  if ($y -lt 0) { $ch += $y; $y = 0 }
  if ($x + $cw -gt $w) { $cw = $w - $x }
  if ($y + $ch -gt $h) { $ch = $h - $y }
  if ($cw -gt 0 -and $ch -gt 0) { $g.FillRectangle($brush, $x, $y, $cw, $ch) }
}

# Wipe sample UI; keep frame + wavy section borders.
Cover 160 50 460 110
Cover 640 70 80 80
Cover 95 170 285 135
Cover 400 170 290 135
Cover 95 328 590 100
Cover 75 450 635 85

$g.Dispose()
$brush.Dispose()
$ref.Dispose()

$dir = Split-Path $out -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
if (Test-Path $out) { Remove-Item $out -Force }
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "wrote $out"
