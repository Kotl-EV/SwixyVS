$dll = (Resolve-Path "tools\LangProbe\bin\Debug\net10.0\VintagestoryAPI.dll").Path
$bytes = [IO.File]::ReadAllBytes($dll)
$text = [Text.Encoding]::ASCII.GetString($bytes)
$rx = [regex]'[A-Za-z_]{3,40}[Cc]ursor[A-Za-z_]{0,40}'
$rx.Matches($text) | ForEach-Object { $_.Value } | Sort-Object -Unique
Write-Output '---strings---'
$rx2 = [regex]'"([a-z\-]{3,20})"'
# too many; just known names
foreach ($n in @('linkselect','pointer','hand','notallowed','crosshair','text','move','wait','help','grab','default','arrow')) {
  if ($text.Contains($n)) { Write-Output "found: $n" }
}
