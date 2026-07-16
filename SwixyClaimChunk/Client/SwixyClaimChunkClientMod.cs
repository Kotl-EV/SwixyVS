// =============================================================================
// Клиентский ModSystem. Partial-файлы: Client/*
// =============================================================================

using System;
using System.Collections.Generic;
using SwixyClaimChunk.Content;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixyClaimChunk;

/// <summary>Клиентский диалог карты приватов и prediction use-filter.</summary>
public sealed partial class SwixyClaimChunkClientMod : ModSystem
{
    private ICoreClientAPI? clientApi;
    private IClientNetworkChannel? clientChannel;
    private ClaimMapDialog? dialog;
    /// <summary>Клиентская копия фильтров (MP prediction). Не трогает server dict.</summary>
    private readonly Dictionary<string, UseFilterRuleData> clientUseFiltersByClaimKey = new(StringComparer.Ordinal);

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void Dispose()
    {
        dialog?.Dispose();
        if (clientApi != null)
        {
            clientApi.Event.OnTestBlockAccess -= OnClientTestBlockAccess;
            clientApi.Event.OnTestBlockAccessClaim -= OnClientTestBlockAccessClaim;
            clientApi.Event.LevelFinalize -= RequestUseFiltersFromServer;
        }

        dialog = null;
        clientApi = null;
        clientChannel = null;
        base.Dispose();
    }
}
