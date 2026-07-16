# Split client/server build (all Swixy mods)

Each mod stays **one** `.csproj` for IDE/Debug.  
`CakeBuild` scans sources and emits two packages with separate DLLs.

```powershell
.\build.ps1
```

## Layout conventions (Cake `SourceSideAnalyzer`)

| Path | Package |
|------|---------|
| `Server/**` | Server DLL only |
| `Client/**`, `Content/**` (GUI), `Gui/**` | Client DLL only |
| `Core/**`, `Net/**`, `Domain/**`, `Network/**` | Shared DLL |

Mod entry points after refactor:

| Mod | Server | Client |
|-----|--------|--------|
| ClaimChunk | `SwixyClaimChunkServerMod` | `SwixyClaimChunkClientMod` |
| SkyBlock | `SwixySkyBlockServerMod` | `SwixySkyBlockClientMod` |
| QuestBook | `QuestbookServerSystem` | `QuestbookClientSystem` + `QuestbookMod` |

Shared gets `InternalsVisibleTo(*.Server)` / `InternalsVisibleTo(*.Client)`.

## Output (`Releases/`)

- `{modid}_server_{version}.zip` — Server.dll + Shared.dll (+ server extras)
- `{modid}_client_{version}.zip` — Client.dll + Shared.dll (+ assets)

Do **not** install both zips into one Mods folder.
