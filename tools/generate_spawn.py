#!/usr/bin/env python3
"""Generate a compact circular Spawn.json with trader kiosks."""
import json
import math

SIZE_X = 39
SIZE_Y = 16
SIZE_Z = 39
CX = SIZE_X // 2
CZ = SIZE_Z // 2
ISLAND_RADIUS = 16
GROUND_Y = 5
SURFACE_Y = GROUND_Y + 1

BLOCKS = {
    "rock": 1,
    "cobble": 2,
    "soil": 3,
    "plaster": 4,
    "log_tree": 5,
    "leaves": 6,
    "log_post": 7,
    "planks_ud": 8,
    "planks_ns": 9,
    "planks_we": 10,
    "debarked_ns": 11,
    "debarked_we": 12,
    "beam": 13,
    "slab_down": 14,
    "slab_up": 15,
    "lantern": 16,
    "fence_n": 17,
    "fence_s": 18,
    "fence_e": 19,
    "fence_w": 20,
    "fence_ns": 21,
    "fence_ew": 22,
    "gravel": 23,
}

BLOCK_CODES = {
    1: "game:rock-granite",
    2: "game:cobblestone-granite",
    3: "game:soil-medium-normal",
    4: "game:plaster-plain",
    5: "game:log-grown-oak-ud",
    6: "game:leaves-grown-oak",
    7: "game:log-placed-oak-ud",
    8: "game:planks-oak-ud",
    9: "game:planks-oak-ns",
    10: "game:planks-oak-we",
    11: "game:debarkedlog-oak-ns",
    12: "game:debarkedlog-oak-we",
    13: "game:supportbeam-oak",
    14: "game:plankslab-oak-down-free",
    15: "game:plankslab-oak-up-free",
    16: "game:lantern-small-up",
    17: "game:woodenfence-oak-n-free",
    18: "game:woodenfence-oak-s-free",
    19: "game:woodenfence-oak-e-free",
    20: "game:woodenfence-oak-w-free",
    21: "game:woodenfence-oak-ns-free",
    22: "game:woodenfence-oak-ew-free",
    23: "game:gravel-granite",
}

grid = {}

def pack(x, y, z):
    return x | (z << 10) | (y << 20)

def in_bounds(x, y, z):
    return 0 <= x < SIZE_X and 0 <= y < SIZE_Y and 0 <= z < SIZE_Z

def in_island(x, z):
    dx, dz = x - CX, z - CZ
    return dx * dx + dz * dz <= ISLAND_RADIUS * ISLAND_RADIUS

def set_block(x, y, z, bid, require_island=False):
    if in_bounds(x, y, z) and (not require_island or in_island(x, z)):
        grid[(x, y, z)] = bid

def dist_sq(x1, z1, x2, z2):
    dx, dz = x1 - x2, z1 - z2
    return dx * dx + dz * dz

def stamp_path(x, z):
    for dx in range(-1, 2):
        for dz in range(-1, 2):
            px, pz = x + dx, z + dz
            if not in_island(px, pz):
                continue
            if abs(dx) + abs(dz) <= 1:
                set_block(px, SURFACE_Y, pz, BLOCKS["cobble"])
            elif dist_sq(px, pz, CX, CZ) > 24:
                set_block(px, SURFACE_Y, pz, BLOCKS["gravel"])

def add_lantern_post(x, z, height=2):
    if not in_island(x, z):
        return
    set_block(x, SURFACE_Y, z, BLOCKS["cobble"])
    for h in range(1, height + 1):
        set_block(x, SURFACE_Y + h, z, BLOCKS["log_post"])
    set_block(x, SURFACE_Y + height + 1, z, BLOCKS["lantern"])

def add_shrub(x, z):
    if in_island(x, z):
        set_block(x, SURFACE_Y + 1, z, BLOCKS["leaves"])

# Island body, exactly round on the surface.
for x in range(SIZE_X):
    for z in range(SIZE_Z):
        if not in_island(x, z):
            continue
        d = math.sqrt(dist_sq(x, z, CX, CZ))
        depth = 5 if d <= ISLAND_RADIUS - 4 else (4 if d <= ISLAND_RADIUS - 1.5 else 3)
        for layer in range(depth):
            y = GROUND_Y - layer
            if layer == depth - 1:
                bid = BLOCKS["rock"]
            elif layer == 0:
                bid = BLOCKS["soil"]
            else:
                bid = BLOCKS["gravel"] if layer == 1 and d > ISLAND_RADIUS - 4 else BLOCKS["cobble"]
            set_block(x, y, z, bid)

# Central plaza.
for dx in range(-4, 5):
    for dz in range(-4, 5):
        d2 = dx * dx + dz * dz
        if d2 <= 16:
            set_block(CX + dx, SURFACE_Y, CZ + dz, BLOCKS["cobble"])
        if d2 <= 4:
            set_block(CX + dx, SURFACE_Y, CZ + dz, BLOCKS["plaster"])

TRADERS = [
    (-11, 0, "e"),
    (11, 0, "w"),
    (0, -11, "s"),
    (0, 11, "n"),
    (11, 11, "nw"),
    (-11, 11, "ne"),
]

def draw_path(tx, tz):
    steps = max(abs(tx), abs(tz))
    for i in range(1, steps + 1):
        px = CX + round(tx * i / steps)
        pz = CZ + round(tz * i / steps)
        stamp_path(px, pz)

for ox, oz, _ in TRADERS:
    draw_path(ox, oz)

for dx in range(-2, 3):
    for dz in range(-2, 3):
        if dx * dx + dz * dz <= 4:
            set_block(CX + dx, SURFACE_Y, CZ + dz, BLOCKS["plaster"])

for lx, lz in [
    (CX - 6, CZ - 6), (CX + 6, CZ - 6), (CX - 6, CZ + 6), (CX + 6, CZ + 6),
    (CX - 8, CZ), (CX + 8, CZ), (CX, CZ - 8), (CX, CZ + 8),
]:
    add_lantern_post(lx, lz)

for sx, sz in [(-6, -4), (-4, -6), (6, -4), (4, -6), (-6, 4), (-4, 6), (6, 4), (4, 6)]:
    add_shrub(CX + sx, CZ + sz)

def add_tree(tx, tz, height):
    x, z = CX + tx, CZ + tz
    if not in_island(x, z):
        return
    set_block(x, SURFACE_Y, z, BLOCKS["soil"])
    for h in range(height):
        set_block(x, SURFACE_Y + 1 + h, z, BLOCKS["log_tree"])
    for ly in range(SURFACE_Y + height - 1, SURFACE_Y + height + 2):
        for dx in range(-2, 3):
            for dz in range(-2, 3):
                if abs(dx) + abs(dz) <= 3:
                    set_block(x + dx, ly, z + dz, BLOCKS["leaves"], require_island=True)

for tree in [(-10, -8, 4), (-12, 6, 4), (7, -11, 4), (-5, 12, 3), (12, 4, 3)]:
    add_tree(*tree)

def build_side_kiosk(cx, cz, facing):
    floor_y = SURFACE_Y
    for dx in range(-2, 3):
        for dz in range(-2, 3):
            set_block(cx + dx, floor_y, cz + dz, BLOCKS["planks_ud"], require_island=True)

    for dx, dz in [(-2, -2), (-2, 2), (2, -2), (2, 2)]:
        for h in range(1, 4):
            set_block(cx + dx, floor_y + h, cz + dz, BLOCKS["debarked_ns"], require_island=True)

    back = {"e": "w", "w": "e", "s": "n", "n": "s"}[facing]
    for i in range(-2, 3):
        if back in ("w", "e"):
            x = cx + (-2 if back == "w" else 2)
            set_block(x, floor_y + 1, cz + i, BLOCKS["planks_ns"], require_island=True)
            set_block(x, floor_y + 2, cz + i, BLOCKS["planks_ns"], require_island=True)
        else:
            z = cz + (-2 if back == "n" else 2)
            set_block(cx + i, floor_y + 1, z, BLOCKS["planks_we"], require_island=True)
            set_block(cx + i, floor_y + 2, z, BLOCKS["planks_we"], require_island=True)

    if facing == "e":
        for dz in range(-1, 2):
            set_block(cx + 1, floor_y + 1, cz + dz, BLOCKS["slab_up"])
    elif facing == "w":
        for dz in range(-1, 2):
            set_block(cx - 1, floor_y + 1, cz + dz, BLOCKS["slab_up"])
    elif facing == "s":
        for dx in range(-1, 2):
            set_block(cx + dx, floor_y + 1, cz + 1, BLOCKS["slab_up"])
    elif facing == "n":
        for dx in range(-1, 2):
            set_block(cx + dx, floor_y + 1, cz - 1, BLOCKS["slab_up"])

    roof_y = floor_y + 4
    for dx in range(-2, 3):
        for dz in range(-2, 3):
            set_block(cx + dx, roof_y, cz + dz, BLOCKS["beam"], require_island=True)
            if abs(dx) < 2 and abs(dz) < 2:
                set_block(cx + dx, roof_y + 1, cz + dz, BLOCKS["planks_ud"], require_island=True)

    gate = {"e": (cx + 1, cz), "w": (cx - 1, cz), "s": (cx, cz + 1), "n": (cx, cz - 1)}[facing]
    set_block(gate[0], roof_y + 2, gate[1], BLOCKS["lantern"])
    set_block(cx, SURFACE_Y, cz, BLOCKS["plaster"])

def build_corner_kiosk(cx, cz):
    floor_y = SURFACE_Y
    for x in range(cx - 3, cx + 1):
        for z in range(cz - 3, cz + 1):
            set_block(x, floor_y, z, BLOCKS["planks_ud"], require_island=True)

    for x, z in [(cx - 3, cz - 3), (cx - 3, cz), (cx, cz - 3)]:
        for h in range(1, 4):
            set_block(x, floor_y + h, z, BLOCKS["debarked_ns"], require_island=True)

    for i in range(-3, 1):
        set_block(cx - 3, floor_y + 1, cz + i, BLOCKS["planks_ns"], require_island=True)
        set_block(cx - 3, floor_y + 2, cz + i, BLOCKS["planks_ns"], require_island=True)
        set_block(cx + i, floor_y + 1, cz - 3, BLOCKS["planks_we"], require_island=True)
        set_block(cx + i, floor_y + 2, cz - 3, BLOCKS["planks_we"], require_island=True)

    set_block(cx - 1, floor_y + 1, cz, BLOCKS["slab_up"])
    set_block(cx, floor_y + 1, cz - 1, BLOCKS["slab_up"])

    roof_y = floor_y + 4
    for x in range(cx - 3, cx + 1):
        for z in range(cz - 3, cz + 1):
            set_block(x, roof_y, z, BLOCKS["beam"], require_island=True)
            if x < cx and z < cz:
                set_block(x, roof_y + 1, z, BLOCKS["planks_ud"], require_island=True)

    set_block(cx - 1, roof_y + 2, cz - 1, BLOCKS["lantern"])
    set_block(cx, SURFACE_Y, cz, BLOCKS["plaster"])

def build_corner_kiosk_ne(cx, cz):
    floor_y = SURFACE_Y
    for x in range(cx, cx + 4):
        for z in range(cz - 3, cz + 1):
            set_block(x, floor_y, z, BLOCKS["planks_ud"], require_island=True)

    for x, z in [(cx + 3, cz - 3), (cx + 3, cz), (cx, cz - 3)]:
        for h in range(1, 4):
            set_block(x, floor_y + h, z, BLOCKS["debarked_ns"], require_island=True)

    for i in range(-3, 1):
        set_block(cx + 3, floor_y + 1, cz + i, BLOCKS["planks_ns"], require_island=True)
        set_block(cx + 3, floor_y + 2, cz + i, BLOCKS["planks_ns"], require_island=True)
        set_block(cx - i, floor_y + 1, cz - 3, BLOCKS["planks_we"], require_island=True)
        set_block(cx - i, floor_y + 2, cz - 3, BLOCKS["planks_we"], require_island=True)

    set_block(cx + 1, floor_y + 1, cz, BLOCKS["slab_up"])
    set_block(cx, floor_y + 1, cz - 1, BLOCKS["slab_up"])

    roof_y = floor_y + 4
    for x in range(cx, cx + 4):
        for z in range(cz - 3, cz + 1):
            set_block(x, roof_y, z, BLOCKS["beam"], require_island=True)
            if x > cx and z < cz:
                set_block(x, roof_y + 1, z, BLOCKS["planks_ud"], require_island=True)

    set_block(cx + 1, roof_y + 2, cz - 1, BLOCKS["lantern"])
    set_block(cx, SURFACE_Y, cz, BLOCKS["plaster"])

for ox, oz, facing in TRADERS:
    x, z = CX + ox, CZ + oz
    if facing == "nw":
        build_corner_kiosk(x, z)
    elif facing == "ne":
        build_corner_kiosk_ne(x, z)
    else:
        build_side_kiosk(x, z, facing)

indices = []
block_ids = []
for (x, y, z), bid in sorted(grid.items()):
    indices.append(pack(x, y, z))
    block_ids.append(bid)

schematic = {
    "GameVersion": "1.22.3",
    "SizeX": SIZE_X,
    "SizeY": SIZE_Y,
    "SizeZ": SIZE_Z,
    "BlockCodes": {str(k): v for k, v in BLOCK_CODES.items()},
    "ItemCodes": {},
    "Indices": indices,
    "BlockIds": block_ids,
    "ReplaceMode": 2,
}

out_path = r"D:\GitHub\SwixyVS\SwixySkyBlock\assets\swixyskyblock\schematics\Spawn.json"
with open(out_path, "w", encoding="utf-8") as f:
    json.dump(schematic, f, separators=(",", ":"))

print(f"Written {out_path}: {len(indices)} blocks, {SIZE_X}x{SIZE_Y}x{SIZE_Z}, radius {ISLAND_RADIUS}")
