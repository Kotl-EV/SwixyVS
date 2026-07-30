Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$srcRef = Join-Path $PSScriptRoot "group1213\Group1213.png"
$outQuest = Join-Path $root "SwixyQuestBook\assets\swixyquestbook\textures\modal.png"
$outQuestCopy = Join-Path $root "SwixyQuestBook\assets\swixyquestbook\textures\modal_quest.png"

$ref = [System.Drawing.Bitmap]::FromFile($srcRef)
$w = $ref.Width
$h = $ref.Height
Write-Output "ref ${w}x${h}"

# Sample face just inside the ornate frame (not from UI sample content).
$face = $ref.GetPixel(55, 55)
Write-Output "face $($face.R),$($face.G),$($face.B)"

$bmp = New-Object System.Drawing.Bitmap $ref
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$brush = New-Object System.Drawing.SolidBrush $face

# One uniform interior wipe — keeps the real Group 1213 frame (corners/edges),
# removes sample text/icons/button AND the earlier gray patch artifacts.
# SVG face rect is ~35,34 711x528; leave ~36px for the metal/wood border.
$inset = 36
$g.FillRectangle($brush, $inset, $inset, $w - 2 * $inset, $h - 2 * $inset)

$g.Dispose()
$brush.Dispose()
$ref.Dispose()

foreach ($out in @($outQuest, $outQuestCopy)) {
  $dir = Split-Path $out -Parent
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
  if (Test-Path $out) { Remove-Item $out -Force }
  $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output "wrote $out"
}
$bmp.Dispose()
