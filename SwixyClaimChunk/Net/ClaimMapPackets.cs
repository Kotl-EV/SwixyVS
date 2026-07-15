// =============================================================================
// ClaimMapPackets.cs
// -----------------------------------------------------------------------------
// Контракты сетевых пакетов (ProtoBuf) для мода SwixyClaimChunk.
// Описывает запросы клиента, ответы сервера и вспомогательные DTO для карты чанков,
// списка приватов, подсветки областей и управления доступом участников.
// Все типы сериализуются через ProtoContract/ProtoMember с фиксированными номерами полей.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;

namespace SwixyClaimChunk.Net;

/// <summary>
/// Состояние ячейки чанка на карте приватов (значения поля <see cref="ClaimChunkCellPacket.State"/>).
/// </summary>
public static class ClaimChunkCellState
{
    /// <summary>Чанк свободен — можно занять приватом.</summary>
    public const int Free = 0;

    /// <summary>Чанк принадлежит текущему игроку (свой приват).</summary>
    public const int Own = 1;

    /// <summary>Чанк занят другим игроком.</summary>
    public const int Other = 2;

    /// <summary>Чанк вне границ мира / недоступен для привата.</summary>
    public const int OutOfWorld = 3;
}

/// <summary>
/// Запрос клиента: получить снимок карты чанков вокруг центра с заданным радиусом.
/// </summary>
[ProtoContract]
public class ClaimMapRequestPacket
{
    /// <summary>Координата X центрального чанка (ось мира).</summary>
    [ProtoMember(1)]
    public int CenterChunkX { get; set; }

    /// <summary>Координата Z центрального чанка (ось мира).</summary>
    [ProtoMember(2)]
    public int CenterChunkZ { get; set; }

    /// <summary>Радиус окна карты в чанках от центра (полуразмер квадрата/области запроса).</summary>
    [ProtoMember(3)]
    public int Radius { get; set; }
}

/// <summary>
/// Запрос клиента: действие над одним чанком (занять / освободить) с контекстом карты.
/// </summary>
[ProtoContract]
public class ClaimChunkActionPacket
{
    /// <summary>Координата X целевого чанка.</summary>
    [ProtoMember(1)]
    public int ChunkX { get; set; }

    /// <summary>Координата Z целевого чанка.</summary>
    [ProtoMember(2)]
    public int ChunkZ { get; set; }

    /// <summary>Координата X центра карты на момент действия (для согласованного ответа).</summary>
    [ProtoMember(3)]
    public int CenterChunkX { get; set; }

    /// <summary>Координата Z центра карты на момент действия.</summary>
    [ProtoMember(4)]
    public int CenterChunkZ { get; set; }

    /// <summary>Радиус окна карты, с которым клиент ожидает обновлённое состояние.</summary>
    [ProtoMember(5)]
    public int Radius { get; set; }
}

/// <summary>
/// Запрос клиента: пакетное действие над несколькими чанками (выделение области на карте).
/// </summary>
[ProtoContract]
public class ClaimChunksBatchActionPacket
{
    /// <summary>Список координат чанков, над которыми выполняется одно действие.</summary>
    [ProtoMember(1)]
    public List<ClaimChunkCoordPacket> Chunks { get; set; } = [];

    /// <summary>Координата X центра карты для последующего ответа.</summary>
    [ProtoMember(2)]
    public int CenterChunkX { get; set; }

    /// <summary>Координата Z центра карты для последующего ответа.</summary>
    [ProtoMember(3)]
    public int CenterChunkZ { get; set; }

    /// <summary>Радиус окна карты для последующего ответа.</summary>
    [ProtoMember(4)]
    public int Radius { get; set; }
}

/// <summary>
/// Пара координат одного чанка (элемент списков в пакетных операциях).
/// </summary>
[ProtoContract]
public class ClaimChunkCoordPacket
{
    /// <summary>Координата X чанка.</summary>
    [ProtoMember(1)]
    public int ChunkX { get; set; }

    /// <summary>Координата Z чанка.</summary>
    [ProtoMember(2)]
    public int ChunkZ { get; set; }
}

/// <summary>
/// Ответ сервера: полное состояние карты чанков, лимиты привата и список ячеек.
/// </summary>
[ProtoContract]
public class ClaimMapStatePacket
{
    // --- Группа: геометрия окна карты ---

    /// <summary>Координата X центра отображаемого окна карты.</summary>
    [ProtoMember(1)]
    public int CenterChunkX { get; set; }

    /// <summary>Координата Z центра отображаемого окна карты.</summary>
    [ProtoMember(2)]
    public int CenterChunkZ { get; set; }

    /// <summary>Координата X чанка, в котором находится игрок (маркер на карте).</summary>
    [ProtoMember(3)]
    public int PlayerChunkX { get; set; }

    /// <summary>Координата Z чанка, в котором находится игрок.</summary>
    [ProtoMember(4)]
    public int PlayerChunkZ { get; set; }

    /// <summary>Радиус окна карты в чанках (согласован с запросом клиента).</summary>
    [ProtoMember(5)]
    public int Radius { get; set; }

    // --- Группа: параметры мира и сетки чанков ---

    /// <summary>Размер чанка в блоках (горизонталь; обычно совпадает с chunkSize мира).</summary>
    [ProtoMember(6)]
    public int ChunkSize { get; set; }

    /// <summary>Ширина мира в блоках по оси X (для границ карты).</summary>
    [ProtoMember(7)]
    public int MapSizeX { get; set; }

    /// <summary>Глубина мира в блоках по оси Z (для границ карты).</summary>
    [ProtoMember(8)]
    public int MapSizeZ { get; set; }

    /// <summary>Высота мира в блоках по оси Y (для расчёта объёма чанка).</summary>
    [ProtoMember(16)]
    public int MapSizeY { get; set; }

    /// <summary>Клиент может выделять и снимать приват с чужих чанков (controlserver).</summary>
    [ProtoMember(17)]
    public bool CanAdminUnclaimOthers { get; set; }

    // --- Группа: квоты и использование привата игрока ---

    /// <summary>Текущий занятый объём привата (в блоках или согласованных единицах сервера).</summary>
    [ProtoMember(9)]
    public long UsedVolume { get; set; }

    /// <summary>Максимально допустимый объём привата для игрока.</summary>
    [ProtoMember(10)]
    public long MaxVolume { get; set; }

    /// <summary>Число уже созданных областей (зон) привата.</summary>
    [ProtoMember(11)]
    public int UsedAreas { get; set; }

    /// <summary>Максимально допустимое число областей привата.</summary>
    [ProtoMember(12)]
    public int MaxAreas { get; set; }

    // --- Группа: сообщение для UI (ошибка, подсказка, статус операции) ---

    /// <summary>Текст сообщения для отображения в интерфейсе карты.</summary>
    [ProtoMember(13)]
    public string Message { get; set; } = "";

    /// <summary>Тип сообщения (код стиля/серьёзности на стороне клиента).</summary>
    [ProtoMember(14)]
    public int MessageType { get; set; }

    // --- Группа: данные ячеек карты ---

    /// <summary>Список ячеек чанков в окне карты с состоянием и метаданными владельца.</summary>
    [ProtoMember(15)]
    public List<ClaimChunkCellPacket> Chunks { get; set; } = [];
}

/// <summary>
/// Тип действия в пакете управления доступом к привату (<see cref="ClaimAccessActionPacket.Action"/>).
/// </summary>
public static class ClaimAccessActionType
{
    /// <summary>Обновить данные привата / список участников с сервера.</summary>
    public const int Refresh = 0;

    /// <summary>Добавить игрока в приват с указанными флагами доступа.</summary>
    public const int AddPlayer = 1;

    /// <summary>Удалить игрока из привата.</summary>
    public const int RemovePlayer = 2;

    /// <summary>Переименовать приват.</summary>
    public const int RenameClaim = 3;

    /// <summary>Удалить приват целиком.</summary>
    public const int DeleteClaim = 4;

    /// <summary>Изменить флаги доступа существующего участника.</summary>
    public const int UpdateMemberAccess = 5;

    /// <summary>Выдать участнику полные права со-владельца, не снимая текущего владельца.</summary>
    public const int GrantCoOwnership = 6;

    /// <summary>Установить фильтр блоков для права Use.</summary>
    public const int SetUseFilter = 7;
}

/// <summary>
/// Режим фильтра блоков для Use в привате.
/// </summary>
public static class ClaimUseFilterMode
{
    /// <summary>Без ограничений: Use разрешает все блоки (как ваниль).</summary>
    public const int AllowAll = 0;

    /// <summary>Use только для выбранных кодов блоков.</summary>
    public const int Whitelist = 1;
}

/// <summary>
/// Запрос клиента: получить список всех приватов текущего игрока.
/// </summary>
[ProtoContract]
public class ClaimListRequestPacket
{
}

/// <summary>
/// Уведомление сервера: открыть GUI карты приватов на клиенте.
/// </summary>
[ProtoContract]
public class ClaimOpenGuiPacket
{
}

/// <summary>
/// Запрос клиента: включить или снять подсветку областей выбранного привата в мире.
/// </summary>
[ProtoContract]
public class ClaimShowRequestPacket
{
    /// <summary>Идентификатор привата для подсветки.</summary>
    [ProtoMember(1)]
    public int ClaimId { get; set; }

    /// <summary>При true — снять подсветку вместо показа (очистить оверлей).</summary>
    [ProtoMember(2)]
    public bool Clear { get; set; }
}

/// <summary>
/// Ответ сервера: набор AABB-областей привата для визуальной подсветки в клиенте.
/// </summary>
[ProtoContract]
public class ClaimShowStatePacket
{
    /// <summary>Идентификатор привата, к которому относятся области.</summary>
    [ProtoMember(1)]
    public int ClaimId { get; set; }

    /// <summary>Список прямоугольных областей привата в мировых координатах блоков.</summary>
    [ProtoMember(2)]
    public List<ClaimAreaPacket> Areas { get; set; } = [];

    /// <summary>Текст сообщения для UI (ошибка доступа, «приват не найден» и т.п.).</summary>
    [ProtoMember(3)]
    public string Message { get; set; } = "";

    /// <summary>Тип сообщения для стилизации на клиенте.</summary>
    [ProtoMember(4)]
    public int MessageType { get; set; }

    /// <summary>Подсветка активна; при false клиент должен скрыть оверлей.</summary>
    [ProtoMember(5)]
    public bool Active { get; set; } = true;
}

/// <summary>
/// Ось-выровненная область привата (два угловых блока inclusive/exclusive по соглашению сервера).
/// </summary>
[ProtoContract]
public class ClaimAreaPacket
{
    /// <summary>Минимальная координата X области.</summary>
    [ProtoMember(1)]
    public int X1 { get; set; }

    /// <summary>Минимальная координата Y области.</summary>
    [ProtoMember(2)]
    public int Y1 { get; set; }

    /// <summary>Минимальная координата Z области.</summary>
    [ProtoMember(3)]
    public int Z1 { get; set; }

    /// <summary>Максимальная координата X области.</summary>
    [ProtoMember(4)]
    public int X2 { get; set; }

    /// <summary>Максимальная координата Y области.</summary>
    [ProtoMember(5)]
    public int Y2 { get; set; }

    /// <summary>Максимальная координата Z области.</summary>
    [ProtoMember(6)]
    public int Z2 { get; set; }
}

/// <summary>
/// Запрос клиента: административное действие над приватом (участники, имя, удаление).
/// </summary>
[ProtoContract]
public class ClaimAccessActionPacket
{
    /// <summary>Идентификатор целевого привата.</summary>
    [ProtoMember(1)]
    public int ClaimId { get; set; }

    /// <summary>Код действия; см. константы <see cref="ClaimAccessActionType"/>.</summary>
    [ProtoMember(2)]
    public int Action { get; set; }

    /// <summary>Имя игрока-цели (для добавления/удаления/смены доступа).</summary>
    [ProtoMember(3)]
    public string PlayerName { get; set; } = "";

    /// <summary>Битовая маска прав доступа для добавления или обновления участника.</summary>
    [ProtoMember(4)]
    public int AccessFlags { get; set; }

    /// <summary>Новое имя привата (для действия переименования).</summary>
    [ProtoMember(5)]
    public string ClaimName { get; set; } = "";

    /// <summary>UID целевого игрока (надёжнее ника для офлайн-участников).</summary>
    [ProtoMember(6)]
    public string PlayerUid { get; set; } = "";

    /// <summary>Режим фильтра Use; см. <see cref="ClaimUseFilterMode"/>.</summary>
    [ProtoMember(7)]
    public int UseFilterMode { get; set; }

    /// <summary>
    /// Коды блоков whitelist, через перевод строки (надёжнее List для protobuf-net).
    /// Пример: "game:door-oak\ngame:chest-east"
    /// </summary>
    [ProtoMember(8)]
    public string UseFilterCodesRaw { get; set; } = "";
}

/// <summary>
/// Ответ сервера: полный список приватов игрока с метаданными и участниками.
/// </summary>
[ProtoContract]
public class ClaimListStatePacket
{
    /// <summary>Коллекция кратких описаний приватов для списка в GUI.</summary>
    [ProtoMember(1)]
    public List<ClaimInfoPacket> Claims { get; set; } = [];

    /// <summary>Сообщение для отображения в списке (статус, ошибка).</summary>
    [ProtoMember(2)]
    public string Message { get; set; } = "";

    /// <summary>Тип сообщения для клиентского оформления.</summary>
    [ProtoMember(3)]
    public int MessageType { get; set; }
}

/// <summary>
/// Краткая информация об одном привате в списке (строка GUI / элемент выбора).
/// </summary>
[ProtoContract]
public class ClaimInfoPacket
{
    /// <summary>Уникальный идентификатор привата.</summary>
    [ProtoMember(1)]
    public int ClaimId { get; set; }

    /// <summary>Отображаемое имя привата.</summary>
    [ProtoMember(2)]
    public string Name { get; set; } = "";

    /// <summary>Число отдельных областей (AABB) внутри привата.</summary>
    [ProtoMember(3)]
    public int AreaCount { get; set; }

    /// <summary>Суммарный объём привата в блоках (или единицах сервера).</summary>
    [ProtoMember(4)]
    public long Volume { get; set; }

    /// <summary>Список участников с правами доступа.</summary>
    [ProtoMember(5)]
    public List<ClaimMemberPacket> Members { get; set; } = [];

    /// <summary>Имя владельца привата (для подписи в UI).</summary>
    [ProtoMember(6)]
    public string OwnerName { get; set; } = "";

    /// <summary>Количество занятых чанков (агрегат для отображения в списке).</summary>
    [ProtoMember(7)]
    public long ChunkCount { get; set; }

    /// <summary>Текущий игрок видит приват как со-владелец, а не официальный владелец.</summary>
    [ProtoMember(8)]
    public bool ViewerIsCoOwner { get; set; }

    /// <summary>Режим фильтра Use; см. <see cref="ClaimUseFilterMode"/>.</summary>
    [ProtoMember(9)]
    public int UseFilterMode { get; set; }

    /// <summary>
    /// Коды блоков whitelist через '\n' (см. <see cref="ClaimAccessActionPacket.UseFilterCodesRaw"/>).
    /// </summary>
    [ProtoMember(10)]
    public string UseFilterCodesRaw { get; set; } = "";
}

/// <summary>Хелперы сериализации списка кодов блоков в строку пакета.</summary>
public static class ClaimUseFilterCodesCodec
{
    public const char Separator = '\n';

    public static string Join(IEnumerable<string>? codes)
    {
        if (codes == null)
        {
            return "";
        }

        return string.Join(Separator, codes.Where(static code => !string.IsNullOrWhiteSpace(code)));
    }

    public static List<string> Split(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(['\n', '\r', '|', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .ToList();
    }
}

/// <summary>
/// Участник привата: идентификатор, имя, права и признак владельца.
/// </summary>
[ProtoContract]
public class ClaimMemberPacket
{
    /// <summary>Уникальный идентификатор игрока (UID), стабильный между сессиями.</summary>
    [ProtoMember(1)]
    public string PlayerUid { get; set; } = "";

    /// <summary>Отображаемое имя игрока.</summary>
    [ProtoMember(2)]
    public string PlayerName { get; set; } = "";

    /// <summary>Битовая маска прав доступа в этом привате.</summary>
    [ProtoMember(3)]
    public int AccessFlags { get; set; }

    /// <summary>Человекочитаемое название уровня доступа (для UI).</summary>
    [ProtoMember(4)]
    public string AccessName { get; set; } = "";

    /// <summary>True, если участник является владельцем привата.</summary>
    [ProtoMember(5)]
    public bool IsOwner { get; set; }

    /// <summary>True, если участник назначен со-владельцем (корона), независимо от Use/Build.</summary>
    [ProtoMember(6)]
    public bool IsCoOwner { get; set; }
}

/// <summary>
/// Одна ячейка на карте чанков: координаты, состояние занятости и владелец.
/// </summary>
[ProtoContract]
public class ClaimChunkCellPacket
{
    /// <summary>Координата X чанка на карте.</summary>
    [ProtoMember(1)]
    public int ChunkX { get; set; }

    /// <summary>Координата Z чанка на карте.</summary>
    [ProtoMember(2)]
    public int ChunkZ { get; set; }

    /// <summary>Состояние ячейки; см. <see cref="ClaimChunkCellState"/>.</summary>
    [ProtoMember(3)]
    public int State { get; set; }

    /// <summary>Имя владельца чанка (для чужих приватов; может быть пустым для свободных).</summary>
    [ProtoMember(4)]
    public string OwnerName { get; set; } = "";

    /// <summary>Идентификатор привата, которому принадлежит чанк (0 если не занят).</summary>
    [ProtoMember(5)]
    public int ClaimId { get; set; }

    /// <summary>Имя привата для подсказки при наведении на ячейку.</summary>
    [ProtoMember(6)]
    public string ClaimName { get; set; } = "";
}
