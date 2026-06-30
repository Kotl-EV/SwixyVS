// Типы пакетов для оптимизированной передачи списка территорий (delta-update)
using Vintagestory.API.MathTools;
namespace SwixySkyBlock.Net;

/// <summary>
/// Дельта-пакет изменения территории. Вместо отправки всего списка — передаём только изменения.
/// </summary>
public sealed class IslandClaimListDeltaPacket
{
    /// <summary>Ключ территории (формат: ownerUID:type).</summary>
    public string ClaimKey { get; set; } = "";

    /// <summary>Тип сообщения: "add", "remove", "update".</summary>
    public string MessageType { get; set; } = "";

    /// <summary>Имя владельца территории.</summary>
    public string? OwnerName { get; set; }

    /// <summary>ID шаблона территории (для отображения).</summary>
    public string? TemplateName { get; set; }

    /// <summary>Позиция создания острова.</summary>
    public BlockPos Origin { get; set; } = default;

    /// <summary>Разрешение на доступ к территории.</summary>
    public bool AccessGranted { get; set; }
}

/// <summary>
/// Пакет запроса списка территорий с фильтрацией по типу.
/// </summary>
public sealed class IslandClaimListFilterPacket
{
    /// <summary>"all" — полный список, "my" — только мои территории.</summary>
    public string FilterType { get; set; } = "";

    /// <summary>Ключ для фильтрации (если filterType != "all").</summary>
    public string? ClaimKeyFilter { get; set; }
}