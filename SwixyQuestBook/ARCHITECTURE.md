# SwixyQuestBook — structure

## One project

```
SwixyQuestBook/          ← единственный .csproj (IDE / Debug)
  Domain/                → Shared (модели, goals)
  Network/               → Shared (пакеты)
  Util/Inventory/        → Shared
  Util/Items/ItemCode*   → Shared
  Server/                → Server-only
  Client/                → Client-only
  Gui/                   → Client-only
  Util/Audio|Textures|…  → Client-only
  QuestbookMod.cs        → Client-only
```

## CakeBuild auto-split

`CakeBuild` **сам** сканирует `.cs` файлы (`SourceSideAnalyzer`),
генерирует временные csproj в `obj/cake-split/`, собирает 3 DLL и пакует 2 мода:

| Package | DLLs | Extra |
|---------|------|--------|
| `*_server_*.zip` | Server + Shared | quests |
| `*_client_*.zip` | Client + Shared | assets |

```powershell
.\build.ps1
# or
dotnet run --project CakeBuild/CakeBuild.csproj -- --configuration=Release
```

Отчёт классификации: `SwixyQuestBook/obj/cake-split/classification.txt`

## Rules (folder conventions)

| Path | Side |
|------|------|
| `Server/**` | Server |
| `Client/**`, `Gui/**`, `QuestbookMod.cs` | Client |
| `Domain/**`, `Network/**`, `Util/Inventory/**`, `Util/Items/QuestbookItemCodeHelper.cs` | Shared |
| `Util/Audio|Localization|Textures`, ItemDisplay/Icon | Client |
