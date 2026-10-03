using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixyClaimChunk.Core;

/// <summary>Результат серверной операции: ключ локализации и тип (0 — успех, 1 — ошибка) для UI.</summary>
public readonly struct ClaimActionResult
{
    /// <summary>Ключ локализация сообщения (если есть).</summary>
    public readonly string? LangKey;
    /// <summary>Аргументы подставки для LangKey.</summary>
    public readonly object[] Args;
    /// <summary>Готовое составное сообщение (приоритет над LangKey).</summary>
    public readonly string? CompositeMessage;
    /// <summary>Тип: 0 — успех, 1 — ошибка.</summary>
    public readonly int MessageType;

    private ClaimActionResult(string? langKey, object[] args, string? compositeMessage, int messageType)
    {
        LangKey = langKey;
        Args = args;
        CompositeMessage = compositeMessage;
        MessageType = messageType;
    }

    public bool HasMessage => !string.IsNullOrEmpty(LangKey) || !string.IsNullOrEmpty(CompositeMessage);

    public string Resolve(IServerPlayer player)
    {
        if (!string.IsNullOrEmpty(CompositeMessage))
        {
            return CompositeMessage;
        }

        return string.IsNullOrEmpty(LangKey)
            ? ""
            : Lang.GetL(player.LanguageCode, LangKey, Args);
    }

    public static ClaimActionResult Success() => new(null, [], null, 0);

    public static ClaimActionResult Success(string langKey, params object[] args) => new(langKey, args, null, 0);

    public static ClaimActionResult SuccessComposite(string localizedMessage) => new(null, [], localizedMessage, 0);

    public static ClaimActionResult Error(string langKey, params object[] args) => new(langKey, args, null, 1);
}

/// <summary>Сериализуемые данные со-владельцев для SaveGame.</summary>
[ProtoContract]
public sealed class CoOwnerSaveData
{
    /// <summary>Хранилище записей со-владельцев: ключ → список значений.</summary>
    [ProtoMember(1)]
    public Dictionary<string, List<string>> Entries { get; set; } = [];
}

/// <summary>
/// Фильтры Use в SaveGame.
/// Ключ — BuildClaimStorageKey; значение — [mode, code1, code2, ...]
/// (mode: "0" AllowAll, "1" Whitelist). Формат как у co-owners: Dictionary + List string.
/// </summary>
[ProtoContract]
public sealed class UseFilterSaveData
{
    /// <summary>Хранилище записей фильтров Use: ключ привата → режим + коды.</summary>
    [ProtoMember(1)]
    public Dictionary<string, List<string>> Entries { get; set; } = [];
}

/// <summary>Правило фильтра Use в памяти (не для protobuf SaveGame).</summary>
public sealed class UseFilterRuleData
{
    /// <summary>Режим: 0 — AllowAll, 1 — Whitelist.</summary>
    public int Mode { get; set; }

    /// <summary>Коды блоков в правиле.</summary>
    public List<string> Codes { get; set; } = [];
}

/// <summary>
/// Флаги привата (битовая маска).
/// Default 0 (безопасно): PvP выключен, животные защищены.
/// </summary>
public static class ClaimFlagBits
{
    /// <summary>Разрешить PvP в привате. Если бит сброшен — урон игрок→игрок блокируется.</summary>
    public const int AllowPvp = 1 << 0;

    /// <summary>
    /// Разрешить урон животным чурим игрокам.
    /// Если-bit сброшен (default) — животные в привате защищены.
    /// (Старое имя ProtectAnimals: opt-in защита; v1 save инвертирует бит при загрузке.)
    /// </summary>
    public const int AllowAnimalDamage = 1 << 1;

    /// <summary>Устаревший алиас (v0 save: бит = «защищать»). Не использовать в новой логике.</summary>
    public const int ProtectAnimals = AllowAnimalDamage;

    /// <summary>Все известные биты флагов.</summary>
    public const int AllKnown = AllowPvp | AllowAnimalDamage;

    /// <summary>Животные защищены, если не разрешён урон.</summary>
    public static bool AreAnimalsProtected(int flags) => (flags & AllowAnimalDamage) == 0;
}

/// <summary>Флаги приватов в SaveGame: ключ → bitmask.</summary>
[ProtoContract]
public sealed class ClaimFlagsSaveData
{
    /// <summary>Хранилище: ключ привата → bitmask.</summary>
    [ProtoMember(1)]
    public Dictionary<string, int> Entries { get; set; } = [];

    /// <summary>
    /// 0 = legacy: bit1 = ProtectAnimals (opt-in protect).
    /// 1+ = bit1 = AllowAnimalDamage (opt-in hurt; default protect).
    /// </summary>
    [ProtoMember(2)]
    public int Version { get; set; }
}

/// <summary>Фоновый скан блоков привата для UI фильтра Use (чанковый, с time-budget).</summary>
public sealed class UseFilterScanJob
{
    /// <summary>Игрок, для которого запущен скан.</summary>
    public required IServerPlayer Player { get; init; }
    /// <summary>ID привата для скана.</summary>
    public required int ClaimId { get; init; }
    /// <summary>Приват для скана.</summary>
    public required LandClaim Claim { get; init; }
    /// <summary>Участники (Cuboidi) привата.</summary>
    public required List<Cuboidi> Areas { get; init; }
    /// <summary>Чанки (cx,cy,cz), пересекающие areas привата.</summary>
    public required List<(int Cx, int Cy, int Cz)> Chunks { get; init; }
    /// <summary>Ключ кэша (storage key + signature areas).</summary>
    public required string CacheKey { get; init; }
    /// <summary>Сигнатура участников привата.</summary>
    public long AreasSignature { get; init; }

    /// <summary>0 = BlockEntities, 1 = block ids в чанках.</summary>
    public int Phase { get; set; }
    /// <summary>Индекс текущего чанка в скане.</summary>
    public int ChunkIndex { get; set; }
    /// <summary>Локальный индекс внутри текущего чанка (phase 1).</summary>
    public int LocalIndex { get; set; }

    /// <summary>Число просканированных элементов.</summary>
    public long Scanned { get; set; }
    /// <summary>Уже встреченные id блоков.</summary>
    public HashSet<int> SeenBlockIds { get; } = [];
    /// <summary>Предпочтительные интересные коды (по регистру).</summary>
    public Dictionary<string, string> InterestingPreferred { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Кэш результата скана use-filter по привату.</summary>
public sealed class UseFilterScanCacheEntry
{
    /// <summary>Сигнатура участников привата.</summary>
    public long AreasSignature { get; init; }
    /// <summary>Исходная строка кодов.</summary>
    public string CodesRaw { get; init; } = "";
    /// <summary>Количество кодов.</summary>
    public int CodeCount { get; init; }
    /// <summary>Число просканированных блоков.</summary>
    public int ScannedBlocks { get; init; }
}