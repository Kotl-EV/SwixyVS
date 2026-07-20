using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixyClaimChunk;

/// <summary>Результат серверной операции: ключ локализации и тип (0 — успех, 1 — ошибка) для UI.</summary>
public readonly struct ClaimActionResult
{
    public readonly string? LangKey;
    public readonly object[] Args;
    public readonly string? CompositeMessage;
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
    [ProtoMember(1)]
    public Dictionary<string, List<string>> Entries { get; set; } = [];
}

/// <summary>Правило фильтра Use в памяти (не для protobuf SaveGame).</summary>
public sealed class UseFilterRuleData
{
    public int Mode { get; set; }

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
    /// Разрешить урон животным чужим игрокам.
    /// Если бит сброшен (default) — животные в привате защищены.
    /// (Старое имя ProtectAnimals: opt-in защита; v1 save инвертирует бит при загрузке.)
    /// </summary>
    public const int AllowAnimalDamage = 1 << 1;

    /// <summary>Устаревший алиас (v0 save: бит = «защищать»). Не использовать в новой логике.</summary>
    public const int ProtectAnimals = AllowAnimalDamage;

    public const int AllKnown = AllowPvp | AllowAnimalDamage;

    /// <summary>Животные защищены, если не разрешён урон.</summary>
    public static bool AreAnimalsProtected(int flags) => (flags & AllowAnimalDamage) == 0;
}

/// <summary>Флаги приватов в SaveGame: ключ → bitmask.</summary>
[ProtoContract]
public sealed class ClaimFlagsSaveData
{
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
    public required IServerPlayer Player { get; init; }
    public required int ClaimId { get; init; }
    public required LandClaim Claim { get; init; }
    public required List<Cuboidi> Areas { get; init; }
    /// <summary>Чанки (cx,cy,cz), пересекающие areas привата.</summary>
    public required List<(int Cx, int Cy, int Cz)> Chunks { get; init; }
    /// <summary>Ключ кэша (storage key + signature areas).</summary>
    public required string CacheKey { get; init; }
    public long AreasSignature { get; init; }

    /// <summary>0 = BlockEntities, 1 = block ids в чанках.</summary>
    public int Phase { get; set; }
    public int ChunkIndex { get; set; }
    /// <summary>Локальный индекс внутри текущего чанка (phase 1).</summary>
    public int LocalIndex { get; set; }

    public int Scanned { get; set; }
    public HashSet<int> SeenBlockIds { get; } = [];
    public Dictionary<string, string> InterestingPreferred { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Кэш результата скана use-filter по привату.</summary>
public sealed class UseFilterScanCacheEntry
{
    public long AreasSignature { get; init; }
    public string CodesRaw { get; init; } = "";
    public int CodeCount { get; init; }
    public int ScannedBlocks { get; init; }
}
