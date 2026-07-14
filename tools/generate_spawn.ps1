$ErrorActionPreference = "Stop"

$SIZE_X = 39
$SIZE_Y = 16
$SIZE_Z = 39
$CX = [int][math]::Floor($SIZE_X / 2)
$CZ = [int][math]::Floor($SIZE_Z / 2)
$ISLAND_RADIUS = 16
$GROUND_Y = 5
$SURFACE_Y = $GROUND_Y + 1

$BLOCKS = @{
    rock        = 1
    cobble      = 2
    soil        = 3
    plaster     = 4
    log_tree    = 5
    leaves      = 6
    log_post    = 7
    planks_ud   = 8
    planks_ns   = 9
    planks_we   = 10
    debarked_ns = 11
    debarked_we = 12
    beam        = 13
    slab_down   = 14
    slab_up     = 15
    lantern     = 16
    fence_n     = 17
    fence_s     = 18
    fence_e     = 19
    fence_w     = 20
    fence_ns    = 21
    fence_ew    = 22
    gravel      = 23
}

$BLOCK_CODES = [ordered]@{
    "1"  = "game:rock-granite"
    "2"  = "game:cobblestone-granite"
    "3"  = "game:soil-medium-normal"
    "4"  = "game:plaster-plain"
    "5"  = "game:log-grown-oak-ud"
    "6"  = "game:leaves-grown-oak"
    "7"  = "game:log-placed-oak-ud"
    "8"  = "game:planks-oak-ud"
    "9"  = "game:planks-oak-ns"
    "10" = "game:planks-oak-we"
    "11" = "game:debarkedlog-oak-ns"
    "12" = "game:debarkedlog-oak-we"
    "13" = "game:supportbeam-oak"
    "14" = "game:plankslab-oak-down-free"
    "15" = "game:plankslab-oak-up-free"
    "16" = "game:lantern-small-up"
    "17" = "game:woodenfence-oak-n-free"
    "18" = "game:woodenfence-oak-s-free"
    "19" = "game:woodenfence-oak-e-free"
    "20" = "game:woodenfence-oak-w-free"
    "21" = "game:woodenfence-oak-ns-free"
    "22" = "game:woodenfence-oak-ew-free"
    "23" = "game:gravel-granite"
}

$grid = @{}

function Pack-Index($x, $y, $z) {
    return ($x -bor ($z -shl 10) -bor ($y -shl 20))
}

function In-Island($x, $z) {
    $dx = $x - $CX
    $dz = $z - $CZ
    return ($dx * $dx + $dz * $dz) -le ($ISLAND_RADIUS * $ISLAND_RADIUS)
}

function DistSq($x1, $z1, $x2, $z2) {
    $dx = $x1 - $x2
    $dz = $z1 - $z2
    return $dx * $dx + $dz * $dz
}

function Set-Block($x, $y, $z, $bid, [switch]$RequireIsland) {
    if ($x -lt 0 -or $x -ge $SIZE_X -or $y -lt 0 -or $y -ge $SIZE_Y -or $z -lt 0 -or $z -ge $SIZE_Z) {
        return
    }
    if ($RequireIsland -and -not (In-Island $x $z)) {
        return
    }
    $grid["$x,$y,$z"] = $bid
}

function Stamp-Path($x, $z) {
    for ($dx = -1; $dx -le 1; $dx++) {
        for ($dz = -1; $dz -le 1; $dz++) {
            $px = $x + $dx
            $pz = $z + $dz
            if (-not (In-Island $px $pz)) { continue }
            if ([math]::Abs($dx) + [math]::Abs($dz) -le 1) {
                Set-Block $px $SURFACE_Y $pz $BLOCKS.cobble
            }
            elseif ((DistSq $px $pz $CX $CZ) -gt 24) {
                Set-Block $px $SURFACE_Y $pz $BLOCKS.gravel
            }
        }
    }
}

function Add-LanternPost($x, $z, $height = 2) {
    if (-not (In-Island $x $z)) { return }
    Set-Block $x $SURFACE_Y $z $BLOCKS.cobble
    for ($h = 1; $h -le $height; $h++) {
        Set-Block $x ($SURFACE_Y + $h) $z $BLOCKS.log_post
    }
    Set-Block $x ($SURFACE_Y + $height + 1) $z $BLOCKS.lantern
}

function Add-Shrub($x, $z) {
    if (In-Island $x $z) {
        Set-Block $x ($SURFACE_Y + 1) $z $BLOCKS.leaves
    }
}

for ($x = 0; $x -lt $SIZE_X; $x++) {
    for ($z = 0; $z -lt $SIZE_Z; $z++) {
        if (-not (In-Island $x $z)) { continue }
        $d = [math]::Sqrt((DistSq $x $z $CX $CZ))
        if ($d -le $ISLAND_RADIUS - 4) { $depth = 5 }
        elseif ($d -le $ISLAND_RADIUS - 1.5) { $depth = 4 }
        else { $depth = 3 }

        for ($layer = 0; $layer -lt $depth; $layer++) {
            $y = $GROUND_Y - $layer
            if ($layer -eq ($depth - 1)) { $bid = $BLOCKS.rock }
            elseif ($layer -eq 0) { $bid = $BLOCKS.soil }
            else { $bid = if ($layer -eq 1 -and $d -gt $ISLAND_RADIUS - 4) { $BLOCKS.gravel } else { $BLOCKS.cobble } }
            Set-Block $x $y $z $bid
        }
    }
}

for ($dx = -4; $dx -le 4; $dx++) {
    for ($dz = -4; $dz -le 4; $dz++) {
        $d2 = $dx * $dx + $dz * $dz
        if ($d2 -le 16) { Set-Block ($CX + $dx) $SURFACE_Y ($CZ + $dz) $BLOCKS.cobble }
        if ($d2 -le 4) { Set-Block ($CX + $dx) $SURFACE_Y ($CZ + $dz) $BLOCKS.plaster }
    }
}

$TRADERS = @(
    @(-11, 0, "e"),
    @(11, 0, "w"),
    @(0, -11, "s"),
    @(0, 11, "n"),
    @(11, 11, "nw"),
    @(-11, 11, "ne")
)

function Draw-Path($tx, $tz) {
    $steps = [math]::Max([math]::Abs($tx), [math]::Abs($tz))
    for ($i = 1; $i -le $steps; $i++) {
        $px = $CX + [int][math]::Round($tx * $i / $steps)
        $pz = $CZ + [int][math]::Round($tz * $i / $steps)
        Stamp-Path $px $pz
    }
}

foreach ($trader in $TRADERS) {
    Draw-Path $trader[0] $trader[1]
}

for ($dx = -2; $dx -le 2; $dx++) {
    for ($dz = -2; $dz -le 2; $dz++) {
        if ($dx * $dx + $dz * $dz -le 4) {
            Set-Block ($CX + $dx) $SURFACE_Y ($CZ + $dz) $BLOCKS.plaster
        }
    }
}

$LANTERNS = @(
    @(($CX - 6), ($CZ - 6)), @(($CX + 6), ($CZ - 6)), @(($CX - 6), ($CZ + 6)), @(($CX + 6), ($CZ + 6)),
    @(($CX - 8), $CZ), @(($CX + 8), $CZ), @($CX, ($CZ - 8)), @($CX, ($CZ + 8))
)
foreach ($lantern in $LANTERNS) {
    Add-LanternPost $lantern[0] $lantern[1]
}

foreach ($shrub in @(@(-6, -4), @(-4, -6), @(6, -4), @(4, -6), @(-6, 4), @(-4, 6), @(6, 4), @(4, 6))) {
    Add-Shrub ($CX + $shrub[0]) ($CZ + $shrub[1])
}

function Add-Tree($tx, $tz, $height) {
    $x = $CX + $tx
    $z = $CZ + $tz
    if (-not (In-Island $x $z)) { return }
    Set-Block $x $SURFACE_Y $z $BLOCKS.soil
    for ($h = 0; $h -lt $height; $h++) {
        Set-Block $x ($SURFACE_Y + 1 + $h) $z $BLOCKS.log_tree
    }
    for ($ly = $SURFACE_Y + $height - 1; $ly -lt $SURFACE_Y + $height + 2; $ly++) {
        for ($dx = -2; $dx -le 2; $dx++) {
            for ($dz = -2; $dz -le 2; $dz++) {
                if ([math]::Abs($dx) + [math]::Abs($dz) -le 3) {
                    Set-Block ($x + $dx) $ly ($z + $dz) $BLOCKS.leaves -RequireIsland
                }
            }
        }
    }
}

foreach ($tree in @(@(-10, -8, 4), @(-12, 6, 4), @(7, -11, 4), @(-5, 12, 3), @(12, 4, 3))) {
    Add-Tree $tree[0] $tree[1] $tree[2]
}

function Build-SideKiosk($cx, $cz, $facing) {
    $floorY = $SURFACE_Y
    for ($dx = -2; $dx -le 2; $dx++) {
        for ($dz = -2; $dz -le 2; $dz++) {
            Set-Block ($cx + $dx) $floorY ($cz + $dz) $BLOCKS.planks_ud -RequireIsland
        }
    }

    foreach ($corner in @(@(-2, -2), @(-2, 2), @(2, -2), @(2, 2))) {
        for ($h = 1; $h -le 3; $h++) {
            Set-Block ($cx + $corner[0]) ($floorY + $h) ($cz + $corner[1]) $BLOCKS.debarked_ns -RequireIsland
        }
    }

    $back = @{ e = "w"; w = "e"; s = "n"; n = "s" }[$facing]
    for ($i = -2; $i -le 2; $i++) {
        if ($back -eq "w" -or $back -eq "e") {
            $x = if ($back -eq "w") { $cx - 2 } else { $cx + 2 }
            Set-Block $x ($floorY + 1) ($cz + $i) $BLOCKS.planks_ns -RequireIsland
            Set-Block $x ($floorY + 2) ($cz + $i) $BLOCKS.planks_ns -RequireIsland
        }
        else {
            $z = if ($back -eq "n") { $cz - 2 } else { $cz + 2 }
            Set-Block ($cx + $i) ($floorY + 1) $z $BLOCKS.planks_we -RequireIsland
            Set-Block ($cx + $i) ($floorY + 2) $z $BLOCKS.planks_we -RequireIsland
        }
    }

    switch ($facing) {
        "e" { for ($dz = -1; $dz -le 1; $dz++) { Set-Block ($cx + 1) ($floorY + 1) ($cz + $dz) $BLOCKS.slab_up } }
        "w" { for ($dz = -1; $dz -le 1; $dz++) { Set-Block ($cx - 1) ($floorY + 1) ($cz + $dz) $BLOCKS.slab_up } }
        "s" { for ($dx = -1; $dx -le 1; $dx++) { Set-Block ($cx + $dx) ($floorY + 1) ($cz + 1) $BLOCKS.slab_up } }
        "n" { for ($dx = -1; $dx -le 1; $dx++) { Set-Block ($cx + $dx) ($floorY + 1) ($cz - 1) $BLOCKS.slab_up } }
    }

    $roofY = $floorY + 4
    for ($dx = -2; $dx -le 2; $dx++) {
        for ($dz = -2; $dz -le 2; $dz++) {
            Set-Block ($cx + $dx) $roofY ($cz + $dz) $BLOCKS.beam -RequireIsland
            if ([math]::Abs($dx) -lt 2 -and [math]::Abs($dz) -lt 2) {
                Set-Block ($cx + $dx) ($roofY + 1) ($cz + $dz) $BLOCKS.planks_ud -RequireIsland
            }
        }
    }

    $gate = @{ e = @(($cx + 1), $cz); w = @(($cx - 1), $cz); s = @($cx, ($cz + 1)); n = @($cx, ($cz - 1)) }[$facing]
    Set-Block $gate[0] ($roofY + 2) $gate[1] $BLOCKS.lantern
    Set-Block $cx $SURFACE_Y $cz $BLOCKS.plaster
}

function Build-CornerKiosk($cx, $cz) {
    $floorY = $SURFACE_Y
    for ($x = $cx - 3; $x -le $cx; $x++) {
        for ($z = $cz - 3; $z -le $cz; $z++) {
            Set-Block $x $floorY $z $BLOCKS.planks_ud -RequireIsland
        }
    }

    foreach ($post in @(@(($cx - 3), ($cz - 3)), @(($cx - 3), $cz), @($cx, ($cz - 3)))) {
        for ($h = 1; $h -le 3; $h++) {
            Set-Block $post[0] ($floorY + $h) $post[1] $BLOCKS.debarked_ns -RequireIsland
        }
    }

    for ($i = -3; $i -le 0; $i++) {
        Set-Block ($cx - 3) ($floorY + 1) ($cz + $i) $BLOCKS.planks_ns -RequireIsland
        Set-Block ($cx - 3) ($floorY + 2) ($cz + $i) $BLOCKS.planks_ns -RequireIsland
        Set-Block ($cx + $i) ($floorY + 1) ($cz - 3) $BLOCKS.planks_we -RequireIsland
        Set-Block ($cx + $i) ($floorY + 2) ($cz - 3) $BLOCKS.planks_we -RequireIsland
    }

    Set-Block ($cx - 1) ($floorY + 1) $cz $BLOCKS.slab_up
    Set-Block $cx ($floorY + 1) ($cz - 1) $BLOCKS.slab_up

    $roofY = $floorY + 4
    for ($x = $cx - 3; $x -le $cx; $x++) {
        for ($z = $cz - 3; $z -le $cz; $z++) {
            Set-Block $x $roofY $z $BLOCKS.beam -RequireIsland
            if ($x -lt $cx -and $z -lt $cz) {
                Set-Block $x ($roofY + 1) $z $BLOCKS.planks_ud -RequireIsland
            }
        }
    }

    Set-Block ($cx - 1) ($roofY + 2) ($cz - 1) $BLOCKS.lantern
    Set-Block $cx $SURFACE_Y $cz $BLOCKS.plaster
}

function Build-CornerKioskNE($cx, $cz) {
    $floorY = $SURFACE_Y
    for ($x = $cx; $x -le $cx + 3; $x++) {
        for ($z = $cz - 3; $z -le $cz; $z++) {
            Set-Block $x $floorY $z $BLOCKS.planks_ud -RequireIsland
        }
    }

    foreach ($post in @(@(($cx + 3), ($cz - 3)), @(($cx + 3), $cz), @($cx, ($cz - 3)))) {
        for ($h = 1; $h -le 3; $h++) {
            Set-Block $post[0] ($floorY + $h) $post[1] $BLOCKS.debarked_ns -RequireIsland
        }
    }

    for ($i = -3; $i -le 0; $i++) {
        Set-Block ($cx + 3) ($floorY + 1) ($cz + $i) $BLOCKS.planks_ns -RequireIsland
        Set-Block ($cx + 3) ($floorY + 2) ($cz + $i) $BLOCKS.planks_ns -RequireIsland
        Set-Block ($cx - $i) ($floorY + 1) ($cz - 3) $BLOCKS.planks_we -RequireIsland
        Set-Block ($cx - $i) ($floorY + 2) ($cz - 3) $BLOCKS.planks_we -RequireIsland
    }

    Set-Block ($cx + 1) ($floorY + 1) $cz $BLOCKS.slab_up
    Set-Block $cx ($floorY + 1) ($cz - 1) $BLOCKS.slab_up

    $roofY = $floorY + 4
    for ($x = $cx; $x -le $cx + 3; $x++) {
        for ($z = $cz - 3; $z -le $cz; $z++) {
            Set-Block $x $roofY $z $BLOCKS.beam -RequireIsland
            if ($x -gt $cx -and $z -lt $cz) {
                Set-Block $x ($roofY + 1) $z $BLOCKS.planks_ud -RequireIsland
            }
        }
    }

    Set-Block ($cx + 1) ($roofY + 2) ($cz - 1) $BLOCKS.lantern
    Set-Block $cx $SURFACE_Y $cz $BLOCKS.plaster
}

foreach ($trader in $TRADERS) {
    $x = $CX + $trader[0]
    $z = $CZ + $trader[1]
    if ($trader[2] -eq "nw") {
        Build-CornerKiosk $x $z
    }
    elseif ($trader[2] -eq "ne") {
        Build-CornerKioskNE $x $z
    }
    else {
        Build-SideKiosk $x $z $trader[2]
    }
}

$entries = foreach ($key in $grid.Keys) {
    $parts = $key.Split(",")
    [pscustomobject]@{
        X   = [int]$parts[0]
        Y   = [int]$parts[1]
        Z   = [int]$parts[2]
        Bid = [int]$grid[$key]
    }
}

$indices = New-Object System.Collections.Generic.List[int]
$blockIds = New-Object System.Collections.Generic.List[int]
foreach ($entry in ($entries | Sort-Object X, Y, Z)) {
    $indices.Add((Pack-Index $entry.X $entry.Y $entry.Z)) | Out-Null
    $blockIds.Add($entry.Bid) | Out-Null
}

$schematic = [ordered]@{
    GameVersion = "1.22.3"
    SizeX       = $SIZE_X
    SizeY       = $SIZE_Y
    SizeZ       = $SIZE_Z
    BlockCodes  = $BLOCK_CODES
    ItemCodes   = @{}
    Indices     = $indices.ToArray()
    BlockIds    = $blockIds.ToArray()
    ReplaceMode = 2
}

$out = "D:\GitHub\SwixyVS\SwixySkyBlock\assets\swixyskyblock\schematics\Spawn.json"
$schematic | ConvertTo-Json -Compress -Depth 5 | Set-Content -Path $out -Encoding UTF8 -NoNewline
Write-Host "Written $out : $($indices.Count) blocks, ${SIZE_X}x${SIZE_Y}x${SIZE_Z}, radius $ISLAND_RADIUS"
