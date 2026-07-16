// =============================================================================
// Серверный ModSystem. Partial-файлы: Server/*
// =============================================================================

using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Серверная логика SkyBlock: острова, claims, worldgen, story sites.</summary>
public sealed partial class SwixySkyBlockServerMod : ModSystem
{
    private ICoreServerAPI? serverApi;
    private IServerNetworkChannel? serverChannel;
    private readonly PlayerIslandRegistry islandRegistry = new();
    private readonly IslandResidencyRegistry islandResidency = new();
    private readonly Dictionary<string, HashSet<string>> coOwnerUidsByClaimKey = new(StringComparer.Ordinal);

    private bool UsePerPlayerIslands =>
        serverApi?.Server?.IsDedicated == true;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        SkyBlockRuntime.Load(api);
    }

    public override void Dispose()
    {
        serverApi = null;
        serverChannel = null;
        base.Dispose();
    }
}
