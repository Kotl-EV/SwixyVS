# Questbook — General Documentation

## Overview

Questbook is a mod for **Vintage Story** (version 1.22.3) that adds a quest system with an in-game journal, quest graph visualization, and an admin panel for creating/editing quests at runtime.

- **Mod ID**: `questbook`
- **Author**: Mengel
- **Open key**: `K`

---

## Architecture

```
QuestbookMod.cs              — Main entry point, manages network channel
QuestbookClientSystem.cs     — Client-side system: hotkeys, dialogs, network handlers
QuestbookClientDataManager.cs — Client data cache: categories, progress, sync
QuestbookServerSystem.cs     — Server-side system: data storage, quest validation, admin API
QuestbookDialog.cs           — Main GUI: sidebar, graph view, modal dialogs
QuestbookDialog.Admin.cs     — Admin panel: node creation, deletion, saving
QuestbookGuiLayout.cs        — All layout constants (positions, sizes, colors)
```

### Data Flow

```
Server (quests.json)  →  SyncQuestsPacket  →  Client (QuestbookClientDataManager)
Server (players/*.json) → SyncProgressPacket → Client (node states)
Client (submit quest) →  SubmitQuestRequest → Server (validate + consume items + give rewards)
Client (admin)        →  AdminCreateNode/Delete/Save → Server → Broadcast to all players
```

---

## Node Types

| Type       | Description                                            |
|------------|--------------------------------------------------------|
| `Start`    | Entry point of a category. Only one per category.      |
| `Quest`    | Requires items, gives rewards. Can have up to 4 required + 2 reward items. |
| `Checkpoint` | Informational node. Unlocks branches after completion. |

---

## Quest Graph Structure

Each category is a directed graph:

- **Nodes** have `(id, x, y)` coordinates and a type
- **Connections** are `(startNodeId → endNodeId)` edges
- A node is **unlocked** only when all parent nodes are completed
- `Start` nodes are always available
- `Checkpoint` nodes require all parents to be completed before they can be opened

---

## File Structure

```
SwixyViStory/
├── QuestbookMod.cs                 — ModSystem entry point
├── modinfo.json                    — Mod metadata
├── Data/
│   ├── quests.json                 — Quest definitions (categories, nodes, connections)
│   └── QuestbookSampleData.cs      — Sample data helper
├── Client/
│   ├── QuestbookClientSystem.cs    — Client lifecycle, hotkey, network
│   └── QuestbookClientDataManager.cs — Client-side data manager
├── Server/
│   ├── QuestbookServerSystem.cs    — Server lifecycle, data persistence
│   ├── QuestbookQuestData.cs       — JSON models + network packets
│   └── QuestbookPlayerProgress.cs  — Player progress tracking
├── Gui/
│   ├── QuestbookDialog.cs          — Main dialog (sidebar + graph + modal)
│   ├── QuestbookDialog.Admin.cs    — Admin panel (add/delete/save nodes)
│   ├── QuestbookGuiLayout.cs       — Layout constants
│   ├── QuestbookAdminData.cs       — Admin panel state + field logic
│   ├── QuestbookCategoryDefinition.cs  — Category model
│   ├── QuestbookQuestNodeDefinition.cs — Node model + item requirements
│   ├── QuestbookQuestNodeState.cs      — Available / Completed
│   ├── QuestbookQuestNodeType.cs       — Start / Quest / Checkpoint
│   └── QuestbookQuestConnectionDefinition.cs — Edge model
├── Network/
│   └── QuestbookQuestSubmitPackets.cs  — All network packets (submit, sync, admin)
├── Helpers/
│   ├── QuestbookInventoryHelper.cs — Inventory operations (count, consume, give)
│   ├── QuestbookLang.cs            — Localization helper
│   └── QuestbookSoundHelper.cs     — Sound effects
└── assets/questbook/
    ├── icon/           — Sidebar icons per category
    ├── textures/       — GUI textures (backgrounds, buttons, node states)
    ├── lang/           — Localization files
    ├── fonts/          — Montserrat font
    ├── sounds/         — Sound effects
    ├── blocktypes/     — Block type definitions
    └── itemtypes/      — Item type definitions
```

---

## quests.json Format

```json
{
  "version": "1.0",
  "categories": [
    {
      "iconFileName": "Introduction.png",
      "title": "category.introduction.title",
      "headerTitle": "category.introduction.header",
      "nodes": [
        {
          "id": 0,
          "x": 0, "y": 0,
          "nodeType": "Start",
          "description": "quest.intro.0.description",
          "requiredItems": [],
          "rewardItems": []
        },
        {
          "id": 1,
          "x": 178, "y": 0,
          "nodeType": "Quest",
          "description": "quest.intro.1.description",
          "requiredItems": [{ "collectibleCode": "game:stick", "count": 16 }],
          "rewardItems": [{ "collectibleCode": "game:flint", "count": 4 }]
        }
      ],
      "connections": [
        { "startNodeId": 0, "endNodeId": 1 }
      ]
    }
  ]
}
```

### Node fields

| Field          | Type     | Description                              |
|----------------|----------|------------------------------------------|
| `id`           | int      | Unique node ID within category           |
| `x`, `y`       | double   | Graph coordinates                        |
| `nodeType`     | string   | `"Start"`, `"Quest"`, or `"Checkpoint"` |
| `description`  | string   | Localization key or plain text           |
| `requiredItems`| array    | Items the player must have to submit     |
| `rewardItems`  | array    | Items given on completion                |

### Connection fields

| Field         | Type | Description           |
|---------------|------|-----------------------|
| `startNodeId` | int  | Parent node ID        |
| `endNodeId`   | int  | Child node ID         |

---

## Network Packets

| Packet                        | Direction | Purpose                          |
|-------------------------------|-----------|----------------------------------|
| `QuestbookSyncQuestsPacket`   | S → C     | Full quest database sync         |
| `QuestbookSyncProgressPacket` | S → C     | Player progress sync             |
| `QuestbookSubmitQuestRequest` | C → S     | Player submits a quest           |
| `QuestbookSubmitQuestResponse`| S → C     | Submit result (success/fail)     |
| `QuestbookAdminCreateNodeRequest` | C → S | Admin creates a node            |
| `QuestbookAdminDeleteLastNodeRequest` | C → S | Admin deletes last node |
| `QuestbookAdminSaveCategoryRequest` | C → S | Admin saves entire category  |
| `QuestbookAdminResponse`      | S → C     | Admin operation result           |

---

## Data Persistence

- **Quests**: `ServerData/Questbook/questbook/quests.json`
- **Player progress**: `ServerData/Questbook/questbook/players/<uid>.json`
- On first load, the mod copies `Data/quests.json` from the mod folder to the server data folder
- Quests are re-saved after any admin edit
- Player progress is saved on disconnect and after each quest completion

---

## Building

```bash
# From project root:
dotnet build
```

Output goes to `bin/Mods/Questbook/` and is also copied to the game's `Mods/Questbook/` directory.

**Note**: Close Vintage Story before building if you want the copied files to take effect.
