// =============================================================================
// Клиентский ModSystem. Partial-файлы: Client/*
// =============================================================================

using SwixySkyBlock.Content;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixySkyBlock;

/// <summary>Клиентский hub, generator labels и входящие пакеты.</summary>
public sealed partial class SwixySkyBlockClientMod : ModSystem
{
    private ICoreClientAPI? clientApi;
    private IClientNetworkChannel? clientChannel;
    private IslandHubDialog? hubDialog;
    private IslandGeneratorLabelRenderer? generatorLabelRenderer;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        SkyBlockRuntime.Load(api);
        LegacySaveFixup.MigrateAllSaves(api.Logger);
    }

    public override void Dispose()
    {
        if (clientApi != null && generatorLabelRenderer != null)
        {
            clientApi.Event.UnregisterRenderer(generatorLabelRenderer, EnumRenderStage.Ortho);
        }

        generatorLabelRenderer?.Dispose();
        generatorLabelRenderer = null;
        clientApi = null;
        hubDialog = null;
        clientChannel = null;
        base.Dispose();
    }
}
