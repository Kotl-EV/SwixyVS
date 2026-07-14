# Questbook Admin Panel — Guide & Examples

## Opening the Admin Panel

1. Press **K** to open the Questbook
2. Select a category from the sidebar
3. Click the **settings icon** (bottom-right corner of the graph area)
4. The admin panel appears on the left side

> You must have `controlserver` privilege to see/use the admin panel.

---

## Panel Layout

```
┌─────────────────────────┐
│  Add an Item            │  ← Title
├─────────────────────────┤
│  [START ▾]              │  ← Preset selector (dropdown)
├─────────────────────────┤
│  [1] [2] [3] [4] [5]    │  ← Quest slots (only for QUEST preset)
├─────────────────────────┤
│  Goals_ID_1  [________] │
│  Goals_Num_1 [________] │
│  Goals_ID_2  [________] │  ← Up to 4 required items
│  Goals_Num_2 [________] │
│  Goals_ID_3  [________] │
│  Goals_Num_3 [________] │
│  Goals_ID_4  [________] │
│  Goals_Num_4 [________] │
├─────────────────────────┤
│  Awards_ID_1 [________] │
│  Awards_Num_1[________] │  ← Up to 2 reward items
│  Awards_ID_2 [________] │
│  Awards_Num_2[________] │
├─────────────────────────┤
│  Information Text       │  ← Description (multi-line)
│  [                   ]  │
│  [                   ]  │
│  [                   ]  │
├─────────────────────────┤
│  Direction: R           │  ← T / R / B / L (click to cycle)
│  Preset Quests: 1       │  ← 1 / 2 / 3 / 5 (click to cycle)
│  Parent Node: AUTO      │  ← Which node to attach to
├─────────────────────────┤
│  [Add]        [Delete]  │  ← Action buttons
├─────────────────────────┤
│  [Clear]                │  ← Clear all fields
│  [Save]                 │  ← Save to server
└─────────────────────────┘
```

---

## Presets

### START

Creates the entry point node. Only one `Start` node per category.

**Fields**: Information Text only.

**Example** — Create a Start node with intro text:

```
Preset:       START
Information:  Welcome to the questbook! Complete quests to progress.
```

Click **Add** → Start node appears at coordinates (0, 0).

---

### QUEST

Creates quest nodes that require items and give rewards.

**Fields**:
- **Goals_ID** — Item code (e.g., `stick`, `stone-*`, `game:flint`)
- **Goals_Num** — Required count (1–64)
- **Awards_ID** — Reward item code
- **Awards_Num** — Reward count (1–64)
- **Information Text** — Quest description
- **Direction** — Where to place the node relative to parent: `T` (top), `R` (right), `B` (bottom), `L` (left)
- **Preset Quests** — Number of quest nodes to create at once: 1, 2, 3, or 5
- **Parent Node** — Which existing node to attach to (AUTO = auto-detect)

**Item code format**:
- `stick` → auto-expanded to `game:stick`
- `stone-*` → wildcard, matches all stone variants
- `game:flint` → explicit namespace

---

### CHECKPOINT

Creates an informational node that unlocks downstream branches.

**Fields**: Information Text, Direction, Parent Node.

No items required. No rewards. Acts as a gate — players must click it after completing all parent quests.

---

## Quest Slots (Multi-Quest Preset)

When `Preset Quests` is set to 2, 3, or 5, you can configure multiple quests at once using slots `[1]` through `[5]`.

- Click a slot number to load/save that slot's data
- Each slot stores its own Goals, Awards, and Information Text
- Active slots depend on the `Preset Quests` count:
  - 1 → slot 1 only
  - 2 → slots 1–2
  - 3 → slots 1–3
  - 5 → slots 1–5

---

## Keyboard Shortcuts (Admin Panel)

| Key           | Action                                      |
|---------------|---------------------------------------------|
| **Tab**       | Move to next field                          |
| **Escape**    | Close field / close admin panel             |
| **Backspace** | Delete last character                       |
| **Ctrl+V**    | Paste from clipboard                        |
| Any character | Append to focused text field                |
| Any digit     | Append to focused number field (1–64)       |
| Any key       | Cycle Direction / Preset Quests / Parent Node |

---

## Creating Quest Branches — Examples

### Example 1: Simple Linear Quest Chain

Create a Start → 3 sequential quests:

**Step 1 — Start node:**
```
Preset:       START
Information:  Your adventure begins here!
```
Click **Add**.

**Step 2 — Quest 1 (sticks):**
```
Preset:       QUEST
Goals_ID_1:   stick
Goals_Num_1:  16
Awards_ID_1:  flint
Awards_Num_1: 4
Information:  Gather 16 sticks
Direction:    R
Preset Quests: 1
Parent Node:  AUTO
```
Click **Add**.

**Step 3 — Quest 2 (stone):**
```
Preset:       QUEST
Goals_ID_1:   stone-*
Goals_Num_1:  12
Awards_ID_1:  stick
Awards_Num_1: 8
Information:  Collect 12 stones of any type
Direction:    R
Preset Quests: 1
Parent Node:  AUTO
```
Click **Add**.

**Step 4 — Quest 3 (dry grass):**
```
Preset:       QUEST
Goals_ID_1:   drygrass
Goals_Num_1:  8
Awards_ID_1:  stick
Awards_Num_1: 4
Information:  Harvest dry grass
Direction:    R
Preset Quests: 1
Parent Node:  AUTO
```
Click **Add**.

Result: `Start → Quest1 → Quest2 → Quest3` (all in a row to the right)

---

### Example 2: Branching Path (2 Side Quests)

After the Start, create 2 quests branching upward and downward:

```
Preset:       QUEST
Goals_ID_1:   axe-flint
Goals_Num_1:  1
Awards_ID_1:  firewood
Awards_Num_1: 16
Information:  Craft a flint axe
Direction:    R
Preset Quests: 2      ← This creates 2 nodes
Parent Node:  AUTO
```

**Slot 1** (top branch):
```
Goals_ID_1:   axe-flint
Goals_Num_1:  1
Awards_ID_1:  firewood
Awards_Num_1: 16
Information:  Craft a flint axe
```

**Slot 2** (bottom branch):
```
Goals_ID_1:   shovel-flint
Goals_Num_1:  1
Awards_ID_1:  clay-blue
Awards_Num_1: 16
Information:  Craft a flint shovel
```

Click **Add**.

Result: Two quest nodes branch off at diagonal angles (NE and SE).

---

### Example 3: 3-Way Split (Forward + 2 Sides)

Create 3 quests from a parent: one forward, two to the sides:

```
Preset:       QUEST
Preset Quests: 3
Direction:    R
Parent Node:  AUTO
```

**Slot 1** (forward):
```
Goals_ID_1:   hoe-flint
Goals_Num_1:  1
Awards_ID_1:  seeds-flax
Awards_Num_1: 8
Information:  Craft a flint hoe
```

**Slot 2** (side — NE diagonal):
```
Goals_ID_1:   knifeblade-flint
Goals_Num_1:  1
Awards_ID_1:  stick
Awards_Num_1: 4
Information:  Craft a flint knife blade
```

**Slot 3** (side — SE diagonal):
```
Goals_ID_1:   pickaxe-copper
Goals_Num_1:  1
Awards_ID_1:  metalbit-tin
Awards_Num_1: 5
Information:  Craft a copper pickaxe
```

Click **Add**.

Result: Three nodes — one forward, two at 45° angles (upper-right and lower-right).

---

### Example 4: 5-Quest Fan-Out

Create 5 quests in a fan pattern:

```
Preset:       QUEST
Preset Quests: 5
Direction:    R
Parent Node:  AUTO
```

Configure all 5 slots with different items, then click **Add**.

Result: One node forward, and four nodes arranged in a fan pattern (NE, SE, and two perpendicular offsets).

---

### Example 5: Checkpoint Gate

After a chain of quests, add a checkpoint that unlocks the next section:

```
Preset:       CHECKPOINT
Information:  You've completed the introduction! Ready for the next chapter?
Direction:    R
Parent Node:  AUTO (or select a specific node)
```

Click **Add**.

The checkpoint appears at a greater distance (214px vs 150px for regular quests). After the player clicks it, all quests connected to it become available.

---

### Example 6: Specific Parent Node

To attach a quest to a specific existing node (not the last one in a direction):

1. Set **Parent Node** to a specific node ID (click to cycle through available nodes)
2. The display shows: `Parent Node: ID:5 [Q/R]` — meaning node 5, Quest type, direction Right

```
Preset:       QUEST
Goals_ID_1:   ingot-copper
Goals_Num_1:  1
Awards_ID_1:  metalbit-tin
Awards_Num_1: 5
Information:  Smelt a copper ingot
Direction:    R
Preset Quests: 1
Parent Node:  ID:23 [Q/R]    ← Attach to node 23 specifically
```

---

## Direction System

| Direction | Meaning                  | Visual               |
|-----------|--------------------------|----------------------|
| `T`       | Top (up)                 | Node placed above    |
| `R`       | Right                    | Node placed right    |
| `B`       | Bottom (down)            | Node placed below    |
| `L`       | Left                     | Node placed left     |

When using multi-quest presets (2, 3, 5), additional nodes are placed at diagonal or perpendicular offsets from the main direction.

---

## Coordinates & Spacing

| From → To               | Distance (px) |
|-------------------------|---------------|
| Start → Quest           | 178           |
| Quest → Quest           | 150           |
| Quest → Checkpoint      | 214           |
| Side quest offset       | 150 (diagonal)|
| Sub-quest offset        | 150           |

The system auto-calculates `(x, y)` based on the parent position and direction. You don't need to set coordinates manually.

---

## Deleting Nodes

Click **Delete** to remove the last non-Start node in the category. All connections to that node are also removed.

> Warning: This cannot be undone. Save first if you want to keep a backup.

---

## Clearing a Category

Click **Clear** to remove ALL nodes and connections from the current category. The category itself remains in the sidebar.

---

## Saving

Click **Save** to persist changes to the server. This:
1. Sends the full category data to the server
2. Server writes to `quests.json`
3. Server broadcasts updated data to all connected players

> Always save after making changes. Changes are lost if you close the dialog without saving.

---

## Canceling Changes

Press **Escape** while the admin panel is open to revert all unsaved changes (restores the snapshot taken when the panel was opened).

---

## Wildcard Item Codes

The mod supports wildcard patterns in item codes:

| Code         | Matches                              |
|--------------|--------------------------------------|
| `stone-*`    | `stone-granite`, `stone-basalt`, etc.|
| `fruit-*`    | All fruit variants                   |
| `clay-*`     | All clay variants                    |
| `seeds-*`    | All seed types                       |

---

## Common Item Codes Reference

| Code                      | Item                    |
|---------------------------|-------------------------|
| `game:stick`              | Stick                   |
| `game:flint`              | Flint                   |
| `game:stone-*`            | Any stone               |
| `game:drygrass`           | Dry grass               |
| `game:cattailtops`        | Cattail tops            |
| `game:axe-flint`          | Flint axe               |
| `game:shovel-flint`       | Flint shovel            |
| `game:knifeblade-flint`   | Flint knife blade       |
| `game:hoe-flint`          | Flint hoe               |
| `game:pickaxe-copper`     | Copper pickaxe          |
| `game:hammer-copper`      | Copper hammer           |
| `game:axe-felling-copper` | Copper felling axe      |
| `game:prospectingpick-copper` | Copper prospecting pick |
| `game:knife-generic-copper`   | Copper knife        |
| `game:shovel-copper`      | Copper shovel           |
| `game:ingot-copper`       | Copper ingot            |
| `game:nugget-nativecopper`| Native copper nugget    |
| `game:metalbit-tin`       | Tin metal bit           |
| `game:firewood`           | Firewood                |
| `game:clay-blue`          | Blue clay               |
| `game:clay-fire`          | Fire clay               |
| `game:seeds-flax`         | Flax seeds              |
| `game:soil-compost-none`  | Compost soil            |
| `game:soil-high-none`     | High-quality soil       |
| `game:redmeat-cooked`     | Cooked red meat         |
| `game:fruit-blueberry`    | Blueberry               |
| `game:fruit-*`            | Any fruit               |
| `game:poultice-reed-horsetail` | Horsetail poultice |
| `game:grain-rice`         | Rice grain              |
| `game:storagevessel-talik`| Talik storage vessel    |
| `game:storagevessel-blue-raw` | Raw blue storage vessel |
| `game:bowl-blue-raw`      | Raw blue bowl           |
| `game:claypot-blue-raw`   | Raw blue clay pot       |
| `game:crock-blue-raw`     | Raw blue crock          |
| `game:crucible-blue-raw`  | Raw blue crucible       |
| `game:toolmold-blue-raw-anvil` | Raw blue anvil mold |
| `game:ingotmold-blue-raw` | Raw blue ingot mold    |
