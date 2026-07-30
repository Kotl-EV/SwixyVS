Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$src = Join-Path $PSScriptRoot "group643.png"
$dstDir = Join-Path $root "SwixyQuestBook\assets\swixyquestbook\textures"
$dstIdle = Join-Path $dstDir "close.png"
$dstHover = Join-Path $dstDir "close_active.png"

# Prefer original Downloads if present
$dl = "C:\Users\Егор\Downloads\Group 643.png"
if (Test-Path -LiteralPath $dl) {
  Copy-Item -LiteralPath $dl -Destination $src -Force
}

$img = [System.Drawing.Bitmap]::FromFile($src)
Write-Output "source $($img.Width)x$($img.Height)"

# Idle = exact asset
$img.Save($dstIdle, [System.Drawing.Imaging.ImageFormat]::Png)

# Hover = lighter gray X (same shape)
$hover = New-Object System.Drawing.Bitmap $img
for ($y = 0; $y -lt $hover.Height; $y++) {
  for ($x = 0; $x -lt $hover.Width; $x++) {
    $c = $hover.GetPixel($x, $y)
    if ($c.A -lt 8) { continue }
    $r = [Math]::Min(255, [int]($c.R * 1.55 + 48))
    $g = [Math]::Min(255, [int]($c.G * 1.55 + 48))
    $b = [Math]::Min(255, [int]($c.B * 1.55 + 48))
    $hover.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c.A, $r, $g, $b))
  }
}
$hover.Save($dstHover, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "wrote close.png + close_active.png ($($img.Width)x$($img.Height))"
$hover.Dispose()
$img.Dispose()
