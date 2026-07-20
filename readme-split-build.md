# Build packages (all Swixy mods)

Each mod stays **one** `.csproj` for IDE/Debug.  
`CakeBuild` produces **three** packages per mod.

```powershell
.\build.ps1
```

## Output (`Releases/`)

| Package | Zip | Contents | Use |
|---------|-----|----------|-----|
| **Full** (universal) | `{modid}_{version}.zip` | One DLL with client+server code + assets | Singleplayer / one Mods folder |
| **Server** | `{modid}_server_{version}.zip` | Server.dll + Shared.dll (+ lang) | Dedicated server only |
| **Client** | `{modid}_client_{version}.zip` | Client.dll + Shared.dll + assets | Player clients |

Install **only one** package type per Mods folder.  
Do **not** mix FULL + server/client, or server + client together (same modid).

## Layout conventions (Cake `SourceSideAnalyzer`)

Used for server/client split only. Full package builds the main `.csproj` as-is.

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
| PermissionManager | `SwixyPermissionManagerServerMod` | `SwixyPermissionManagerClientMod` |

Shared gets `InternalsVisibleTo(*.Server)` / `InternalsVisibleTo(*.Client)`.
