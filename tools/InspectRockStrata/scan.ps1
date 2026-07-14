$bytes = [IO.File]::ReadAllBytes('E:\Vintagestory\Mods\VSSurvivalMod.dll')
$text = [Text.Encoding]::UTF8.GetString($bytes)
[regex]::Matches($text, 'OnMapChunkGen|GenRockStrata|TopRockIdMap|OnChunkColumn') |
    ForEach-Object { $_.Value } |
    Sort-Object -Unique