// =============================================================================
// IslandPackets.cs
// -----------------------------------------------------------------------------
// Сетевые пакеты для системы островов с использованием Protocol Buffers.
// =============================================================================

using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock.Net;

// =============================================================================
// Пакеты для управления состоянием игрока (island hub)
// =============================================================================

public static class IslandAccessActionType
{
    public const int Refresh = 0;
    public const int AddPlayer = 1;
    public const int RemovePlayer = 2;
    public const int RenameClaim = 3;
    public const int DeleteClaim = 4;
    public const int UpdateMemberAccess = 5;
    public const int GrantCoOwnership = 6;
    public const int RecreateIsland = 7;
    public const int LeaveIsland = 8;
}

public static class IslandHubActionType
{
    public const int Refresh = 0;
    public const int Create = 1;
    public const int Delete = 2;
    public const int Recreate = 3;
    public const int GoHome = 4;
    public const int GoSpawn = 5;
}

[ProtoContract]
public class IslandHubRequestPacket { }

[ProtoContract]
public class IslandHubStatePacket
{
    [ProtoMember(1)] public bool HasIsland { get; set; }
    [ProtoMember(2)] public string TemplateName { get; set; } = "";
    [ProtoMember(3)] public List<string> AvailableTemplates { get; set; } = [];
    [ProtoMember(4)] public string Message { get; set; } = "";
    [ProtoMember(5)] public int MessageType { get; set; }
    [ProtoMember(6)] public bool IsIslandResident { get; set; }
}

[ProtoContract]
public class IslandActionPacket
{
    [ProtoMember(1)] public int Action { get; set; }
    [ProtoMember(2)] public string TemplateName { get; set; } = "";
}

[ProtoContract]
public class IslandGeneratorLabelsPacket
{
    [ProtoMember(1)] public List<IslandGeneratorLabelPacket> Labels { get; set; } = [];
}

[ProtoContract]
public class IslandGeneratorLabelsRequestPacket { }

[ProtoContract]
public class IslandGeneratorLabelPacket
{
    [ProtoMember(1)] public int X { get; set; }
    [ProtoMember(2)] public int Y { get; set; }
    [ProtoMember(3)] public int Z { get; set; }
    [ProtoMember(4)] public int Level { get; set; }
}

[ProtoContract]
public class IslandGeneratorStateRequestPacket { }

[ProtoContract]
public class IslandGeneratorUpgradeRequestPacket { }

[ProtoContract]
public class IslandGeneratorStatePacket
{
    [ProtoMember(1)] public int CurrentLevel { get; set; }
    [ProtoMember(2)] public int MaxLevel { get; set; }
    [ProtoMember(3)] public bool HasIsland { get; set; }
    [ProtoMember(4)] public bool CanUpgrade { get; set; }
    [ProtoMember(5)] public string CostItemCode { get; set; } = "";
    [ProtoMember(6)] public int CostQuantity { get; set; }
    [ProtoMember(7)] public int PlayerCostItemCount { get; set; }
    [ProtoMember(8)] public string Message { get; set; } = "";
    [ProtoMember(9)] public List<IslandGeneratorLevelStatePacket> Levels { get; set; } = [];
}

[ProtoContract]
public class IslandGeneratorLevelStatePacket
{
    [ProtoMember(1)] public int Level { get; set; }
    [ProtoMember(2)] public bool Unlocked { get; set; }
    [ProtoMember(3)] public List<IslandGeneratorEntryStatePacket> Entries { get; set; } = [];
}

[ProtoContract]
public class IslandGeneratorEntryStatePacket
{
    [ProtoMember(1)] public string BlockCode { get; set; } = "";
    [ProtoMember(2)] public double Chance { get; set; }
    [ProtoMember(3)] public double Percent { get; set; }
    [ProtoMember(4)] public int VariantCount { get; set; }
    [ProtoMember(5)] public string DisplayBlockCode { get; set; } = "";
}

// =============================================================================
// Пакеты рейтинга островов (island top)
// =============================================================================

[ProtoContract]
public class IslandTopRequestPacket { }

[ProtoContract]
public class IslandTopStatePacket
{
    [ProtoMember(1)] public List<IslandTopEntryPacket> Entries { get; set; } = [];
}

[ProtoContract]
public class IslandTopEntryPacket
{
    [ProtoMember(1)] public int Rank { get; set; }
    [ProtoMember(2)] public string PlayerName { get; set; } = "";
    [ProtoMember(3)] public int GeneratorLevel { get; set; }
    [ProtoMember(4)] public string TemplateName { get; set; } = "";
    [ProtoMember(5)] public bool IsViewer { get; set; }
}

// =============================================================================
// Пакеты для управления списками территорий (claim list)
// =============================================================================

[ProtoContract]
public class IslandClaimListRequestPacket { }

[ProtoContract]
public class IslandClaimAccessActionPacket
{
    [ProtoMember(1)] public int ClaimId { get; set; }
    [ProtoMember(2)] public int Action { get; set; }
    [ProtoMember(3)] public string PlayerName { get; set; } = "";
    [ProtoMember(4)] public int AccessFlags { get; set; }
    [ProtoMember(5)] public string ClaimName { get; set; } = "";
    [ProtoMember(6)] public string PlayerUid { get; set; } = "";
}

[ProtoContract]
public class IslandClaimListStatePacket
{
    [ProtoMember(1)] public List<IslandClaimInfoPacket> Claims { get; set; } = [];
    [ProtoMember(2)] public string Message { get; set; } = "";
    [ProtoMember(3)] public int MessageType { get; set; }
}

[ProtoContract]
public class IslandClaimInfoPacket
{
    /// <summary>ID территории (присваивается сервером).</summary>
    [ProtoMember(1)] public int ClaimId { get; set; }

    /// <summary>Имя территории.</summary>
    [ProtoMember(2)] public string Name { get; set; } = "";

    /// <summary>Количество регионов (areas).</summary>
    [ProtoMember(3)] public int AreaCount { get; set; }

    /// <summary>Объём территории в блоках.</summary>
    [ProtoMember(4)] public long Volume { get; set; }

    /// <summary>Список членов территории.</summary>
    [ProtoMember(5)] public List<IslandClaimMemberPacket> Members { get; set; } = [];

    /// <summary>Имя владельца (никнейм).</summary>
    [ProtoMember(6)] public string OwnerName { get; set; } = "";

    /// <summary>Количество чанков.</summary>
    [ProtoMember(7)] public long ChunkCount { get; set; }

    /// <summary>Я со-владелец этой территории.</summary>
    [ProtoMember(8)] public bool ViewerIsCoOwner { get; set; }

    /// <summary>Это островная территория (не личный кл).</summary>
    [ProtoMember(9)] public bool IsIslandClaim { get; set; } = true;

    /// <summary>Я могу покинуть эту территорию.</summary>
    [ProtoMember(10)] public bool ViewerCanLeave { get; set; }
}

[ProtoContract]
public class IslandClaimMemberPacket
{
    /// <summary>ID члена (player UID).</summary>
    [ProtoMember(1)] public string PlayerUid { get; set; } = "";

    /// <summary>Имя игрока (никнейм).</summary>
    [ProtoMember(2)] public string PlayerName { get; set; } = "";

    /// <summary>Права доступа (битовая маска).</summary>
    [ProtoMember(3)] public int AccessFlags { get; set; }

    /// <summary>Имя права доступа.</summary>
    [ProtoMember(4)] public string AccessName { get; set; } = "";

    /// <summary>Я владелец этой территории.</summary>
    [ProtoMember(5)] public bool IsOwner { get; set; }

    /// <summary>Я со-владелец этой территории.</summary>
    [ProtoMember(6)] public bool IsCoOwner { get; set; }
}

[ProtoContract]
public class IslandClaimShowRequestPacket
{
    [ProtoMember(1)] public int ClaimId { get; set; }
    [ProtoMember(2)] public bool Clear { get; set; }
}

[ProtoContract]
public class IslandClaimShowStatePacket
{
    [ProtoMember(1)] public int ClaimId { get; set; }
    [ProtoMember(2)] public List<IslandClaimAreaPacket> Areas { get; set; } = [];
    [ProtoMember(3)] public string Message { get; set; } = "";
    [ProtoMember(4)] public int MessageType { get; set; }
    [ProtoMember(5)] public bool Active { get; set; } = true;
}

[ProtoContract]
public class IslandClaimAreaPacket
{
    [ProtoMember(1)] public int X1 { get; set; }
    [ProtoMember(2)] public int Y1 { get; set; }
    [ProtoMember(3)] public int Z1 { get; set; }
    [ProtoMember(4)] public int X2 { get; set; }
    [ProtoMember(5)] public int Y2 { get; set; }
    [ProtoMember(6)] public int Z2 { get; set; }
}

// =============================================================================
// Пакеты для delta-оптимизированной передачи списка территорий
// =============================================================================
// Примечание: типы IslandClaimListDeltaPacket и IslandClaimListFilterPacket
// определены в отдельном файле IslandClaimListDeltaPackets.cs

// =============================================================================
// Пакеты для управления хабом островов (island hub)
// =============================================================================

[ProtoContract]
public class IslandHubDialogStatePacket
{
    [ProtoMember(1)] public bool CanCreateIsland { get; set; }
    [ProtoMember(2)] public bool HasIsland { get; set; }
    [ProtoMember(3)] public bool IsIslandResident { get; set; }
    [ProtoMember(4)] public List<string> AvailableTemplates { get; set; } = [];
    [ProtoMember(5)] public string Message { get; set; } = "";
}
