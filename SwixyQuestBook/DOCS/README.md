# Questbook — Общая документация

## О проекте

**Questbook** — мод (плагин) для игры **Vintage Story** (воксельный sandbox), добавляющий систему квестов с визуальным деревом, категориями, прогрессом игроков, наградами и админ-панелью для создания квестов.

- **Автор:** Mengrel
- **Язык:** C# (.NET 10.0)
- **Target API:** Vintage Story 1.22.3
- **Текущая версия:** 1.1
- **Открывается клавишей:** K
- **Локализация:** ru, en, de, es, fr, zh

---

## Структура проекта

```
SwixyViStory/
├── QuestbookMod.cs          # Точка входа мода (ModSystem)
├── modinfo.json             # Метаданные мода
├── Data/
│   ├── quests.json          # Источник правды для данных квестов
│   └── QuestbookSampleData.cs  # Заглушка (возвращает [])
├── Server/
│   ├── QuestbookServerSystem.cs   # Серверная логика, сеть, сохранение
│   ├── QuestbookQuestData.cs      # Загрузка quests.json
│   └── QuestbookPlayerProgress.cs # Прогресс игроков (файлы по UID)
├── Client/
│   ├── QuestbookClientSystem.cs   # Клиентская инициализация, hotkey K
│   └── QuestbookClientDataManager.cs # Хранение данных на клиенте
├── Gui/
│   ├── QuestbookDialog.cs              # Основной диалог (книга квестов)
│   ├── QuestbookDialog.Admin.cs        # Админ-панель (часть диалога)
│   ├── QuestbookGuiLayout.cs           # Константы layout/координат
│   ├── QuestbookAdminData.cs           # Данные админки + слоты
│   ├── QuestbookCategoryDefinition.cs  # Категория квестов
│   ├── QuestbookQuestNodeDefinition.cs # Узел квеста
│   ├── QuestbookQuestConnectionDefinition.cs # Связь между узлами
│   ├── QuestbookQuestNodeType.cs       # enum: Start, Quest, Checkpoint
│   └── QuestbookQuestNodeState.cs      # enum: Available, Completed
├── Network/                 # Пакеты клиент-сервер
├── Helpers/                 # Инвентарь, звуки, локализация
└── assets/                  # Текстуры, иконки, lang-файлы
```

---

## Архитектура

### Типы узлов (Quest Nodes)

| Тип | Описание | Ограничения |
|-----|----------|-------------|
| **Start** | Начальный узел категории | Всегда в (0,0). Только InfoText. Без Direction. |
| **Quest** | Обычный квест | До 4 предметов-целей, до 2 наград. Requires Items. |
| **Checkpoint** | Контрольная точка | InfoText + Direction + ParentNode. Без предметов. |

### Прогресс и разблокировка

- Узел разблокирован, когда **все** его родительские узлы (по связям)Completed.
- Визуальные состояния: **Inactive** (locked) → **Active** (unlocked) → **Completed**.
- `IsNodeUnlocked()` проверяет ВСЕ входящие связи узла.

### Клиент-серверная модель

- **Античит:** Вся валидация предметов (проверка, потребление, выдача) на сервере.
- Клиент только отображает данные; изменение JSON вручную не даёт наград.
- Сервер рассылает `BroadcastQuestsToAllPlayers()` после каждого изменения.

### Данные квестов

- **Источник правды (в моде):** `Data/quests/` (`manifest.json` + `branches/*.json`).
- **Runtime на сервере (ModConfig):**
  - `%APPDATA%\VintagestoryData\ModConfig\swixyquestbook\quests\manifest.json`
  - `%APPDATA%\VintagestoryData\ModConfig\swixyquestbook\quests\branches\*.json`
- При первом запуске сервер копирует packaged defaults из `swixyquestbook/quests/` рядом с DLL.
- **QuestbookSampleData.cs** — заглушка (возвращает `[]`), не используется.

### Игроки

- Прогресс: `%APPDATA%\VintagestoryData\ModConfig\swixyquestbook\players\{playeruid}.json`

---

## Сборка и запуск

1. Откройте `Questbook.csproj` в IDE (Visual Studio / Rider).
2. Соберите проект (Build).
3. Скопируйте скомпилированный DLL в папку модов Vintage Story.
4. Или используйте скрипты в корне проекта:
   - `Запустить игру.bat` / `Запустить игру.ps1`
   - `Перезагрузить мод.ps1`

---

## Создание квестов

### Через quests.json (ручной способ)

Редактируйте `Data\quests.json` и пересоберите мод. Структура:

```json
{
  "categories": [
    {
      "iconFileName": "icon.png",
      "title": "category.title",
      "headerTitle": "CategoryHeader",
      "nodes": [...],
      "connections": [...]
    }
  ]
}
```

### Через админку в игре

Откройте книгу квестов (K), затем нажмите кнопку настроек (шестерёнка) внизу справа. Подробнее — см. [ADMIN_GUIDE.md](./ADMIN_GUIDE.md).

---

## Локализация

Файлы локализации в `assets/questbook/lang/`:
- `ru.json`, `en.json`, `de.json`, `es.json`, `fr.json`, `zh.json`

Ключи вида `questbook:key.name`. Если ключ не найден — VS покажет сам ключ или текст из `en.json`.

---

## Часто задаваемые вопросы

**Q: Почему предметы не сохраняются после добавления через админку?**
A: Проверьте, что версия мода >= 1.1. Исправлен баг `SaveCurrentSlot()`.

**Q: Как добавить новую категорию?**
A: Добавьте запись в `quests.json` на сервере. Перезапуск мода не нужен.

**Q: Предметы-шаблоны (wildcards)?**
A: Код предмета может содержать `*`: `clay-*`, `*-stick`, `*clay*`. Проверка без учёта регистра.
