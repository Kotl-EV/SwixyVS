// Типы пакетов для оптимизированной передачи списка территорий (delta-update)
using ProtoBuf;
using Vintagestory.API.MathTools;
namespace SwixySkyBlock.Net;

/// <summary>
/// Дельта-пакет изменения территории. Вместо отправки всего списка — передаём только изменения.
/// </summary>
[ProtoContract]
public sealed class IslandClaimListDeltaPacket
{
    /// <summary>Ключ территории (формат: ownerUID:type).</summary>
    [ProtoMember(1)] public string ClaimKey { get; set; } = "";

    /// <summary>Тип сообщения: "add", "remove", "update".</summary>
    [ProtoMember(2)] public string MessageType { get; set; } = "";

    /// <summary>Имя владельца территории.</summary>
    [ProtoMember(3)] public string? OwnerName { get; set; }

    /// <summary>ID шаблона территории (для отображения).</summary>
    [ProtoMember(4)] public string? TemplateName { get; set; }

    /// <summary>Позиция создания острова.</summary>
    [ProtoMember(5)] public BlockPos Origin { get; set; } = default;

    /// <summary>Разрешение на доступ к территории.</summary>
    [ProtoMember(6)] public bool AccessGranted { get; set; }
}

/// <summary>
/// Пакет запроса списка территорий с фильтрацией по типу.
/// </summary>
[ProtoContract]
public sealed class IslandClaimListFilterPacket
{
    /// <summary>"all" — полный список, "my" — только мои территории.</summary>
    [ProtoMember(1)] public string FilterType { get; set; } = "";

    /// <summary>Ключ для фильтрации (если filterType != "all").</summary>
    [ProtoMember(2)] public string? ClaimKeyFilter { get; set; }
}