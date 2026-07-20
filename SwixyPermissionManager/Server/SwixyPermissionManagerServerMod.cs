// =============================================================================
// Серверный ModSystem — оболочка над Config.Roles / Privilege / player roles.
// =============================================================================

using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SwixyPermissionManager;

public sealed partial class SwixyPermissionManagerServerMod : ModSystem
{
    private ICoreServerAPI? serverApi;
    private IServerNetworkChannel? serverChannel;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void Dispose()
    {
        serverApi = null;
        serverChannel = null;
        base.Dispose();
    }

    private bool CanManage(IServerPlayer player) =>
        player != null
        && (player.HasPrivilege(Privilege.controlserver)
            || player.HasPrivilege(Privilege.grantrevoke)
            || player.HasPrivilege(Privilege.root));
}
