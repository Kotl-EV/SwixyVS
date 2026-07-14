# SwixyClaimChunk

SwixyClaimChunk is a Vintage Story code mod that adds a chunk-based land claim map interface.

Instead of using only chat commands, players can open a GUI, inspect nearby claimed chunks, select chunks on a grid, claim or unclaim them, and manage their own claims from one place.

## Features

- Chunk claim map with free, owned, other-player, and out-of-world states.
- Multi-chunk selection for batch claim and unclaim actions.
- Claim limits display for chunks and separate claim areas.
- `/land` command override that opens the claim map GUI.
- Default hotkey: `P`.
- Claims tab with claim list, renaming, member management, and access controls.
- Co-owner support stored server-side.
- Claim highlighting in the world.
- Admin unclaim support for players with the required server privilege.

## Requirements

- Vintage Story `1.22.0` or newer compatible `1.22.x` builds.
- The mod must be installed on both the server and the client for the full GUI experience.

This is not designed as a client-only mod. A client-only version could only automate existing chat commands and would be heavily limited. The full feature set requires server-side logic to read claim state, validate permissions, apply changes, synchronize the map, manage co-owners, and store additional claim metadata.

## Installation

1. Download the release zip for `swixyclaimchunk`.
2. Put the zip into the Vintage Story `Mods` folder.
3. Install it on the server as well as every client that should use the GUI.
4. Restart the game or server.

## Usage

- Press `P` to open or close the claim map.
- Run `/land` to open the same GUI.
- Left-click chunks on the map to select them.
- Right-click and drag to move around the map.
- Use the `Center` button to return the map to your current position.
- Open the `Claims` tab to rename claims, manage members, change access, delete claims, or highlight claim borders.

## Permissions

Players need the normal land-claiming permission to claim land. Players with server control privileges can unclaim other players' chunks through the admin flow.

The mod respects the world's land claiming setting and the player's configured land claim allowance and area limits.

## Building From Source

Set `VINTAGE_STORY` so the project can find the Vintage Story assemblies, then run:

```powershell
.\build.ps1
```

The build script publishes release zips into the `Releases` directory.

## Mod Info

- Mod ID: `swixyclaimchunk`
- Type: code mod
- Current version: `1.0.1`
- Authors: `Tehtelev`, `Kotl`
