# SwixyPermissionManager

Графическая оболочка над **ванильными ролями и правами** Vintage Story (`serverconfig.json` → Roles / Privilege).

Не своя система прав — те же `/role`, `/player … role`, `/list role`, что уже в игре.

## Возможности

| Действие | Как в игре |
|----------|------------|
| Список ролей | `Config.Roles` / `/list role` |
| Создать роль | запись в Roles + `MarkConfigDirty` |
| Удалить роль | кроме защищённых и default |
| Переименовать | `Name` роли |
| Выдать / забрать privilege | `IPlayerRole.GrantPrivilege` / `RevokePrivilege` |
| Пояснения прав | каталог по `Privilege.AllCodes()` + lang |
| Назначить игроку роль | `Permissions.SetRole` |

## Открыть

- **Ctrl+F7**
- `/perms`, `/permissions`, `/roles`
- Нужен `controlserver`, `grantrevoke` или `root`

## GUI

1. **Слева** — роли (группы привилегий)
2. **Центр** — список privilege; клик = grant/revoke у выбранной роли; фильтр
3. **Справа** — описание роли, **что делает выбранное право**, назначение игрока на роль

Изменения пишутся в **serverconfig** (как правки `/role … privilege grant|revoke`).
