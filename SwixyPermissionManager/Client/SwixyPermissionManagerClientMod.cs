using SwixyPermissionManager.Content;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixyPermissionManager;

public sealed partial class SwixyPermissionManagerClientMod : ModSystem
{
    private ICoreClientAPI? clientApi;
    private IClientNetworkChannel? clientChannel;
    private PermissionManagerDialog? dialog;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void Dispose()
    {
        dialog?.Dispose();
        dialog = null;
        clientApi = null;
        clientChannel = null;
        base.Dispose();
    }
}
