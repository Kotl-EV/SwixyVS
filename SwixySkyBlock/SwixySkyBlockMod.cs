// =============================================================================
// Точка входа ModSystem. Partial-файлы: Core/, Client/, Server/
// =============================================================================

using System.Collections.Generic;
using SwixySkyBlock.Content;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModDependency("game", "1.22.0")]
[assembly: ModDependency("survival", "1.22.0")]
[assembly: ModInfo(
    "SwixySkyBlock",
    "swixyskyblock",
    Website = "https://github.com/tehtelev/Swixy",
    Description = "SkyBlock gameplay for Vintage Story.",
    Version = "1.0.0",
    Authors =
    [
        "Tehtelev",
        "Kotl"
    ]
)]

namespace SwixySkyBlock;

/// <summary>ModSystem мода SkyBlock.</summary>
public sealed partial class SwixySkyBlockMod : ModSystem
{
    public const string OpenIslandHubHotkeyCode = "swixyskyblockopenhub";

    private ICoreClientAPI? clientApi;
    private ICoreServerAPI? serverApi;
    private IslandHubDialog? hubDialog;
    private IslandGeneratorLabelRenderer? generatorLabelRenderer;

    public override bool ShouldLoad(EnumAppSide forSide) => true;

    public override void Dispose()
    {
        if (clientApi != null && generatorLabelRenderer != null)
        {
            clientApi.Event.UnregisterRenderer(generatorLabelRenderer, EnumRenderStage.Ortho);
        }

        generatorLabelRenderer?.Dispose();
        generatorLabelRenderer = null;
        clientApi = null;
        serverApi = null;
        hubDialog = null;
        clientChannel = null;
        serverChannel = null;
        base.Dispose();
    }
}
