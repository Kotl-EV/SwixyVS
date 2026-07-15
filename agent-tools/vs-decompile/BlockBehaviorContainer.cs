using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

[DocumentAsJson]
public class BlockBehaviorContainer : BlockBehavior
{
	public BlockBehaviorContainer(Block block)
		: base(block)
	{
	}

	public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
	{
		handling = (EnumHandling)3;
		BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
		if (blockEntity is BlockEntityOpenableContainer)
		{
			return ((BlockEntityOpenableContainer)(object)blockEntity).OnPlayerRightClick(byPlayer, blockSel);
		}
		return false;
	}

	public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
	{
		handling = (EnumHandling)0;
		BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
		if (!(blockEntity is BlockEntityOpenableContainer))
		{
			return;
		}
		BlockEntityOpenableContainer blockEntityOpenableContainer = (BlockEntityOpenableContainer)(object)blockEntity;
		IPlayer[] allOnlinePlayers = world.AllOnlinePlayers;
		for (int i = 0; i < allOnlinePlayers.Length; i++)
		{
			if (allOnlinePlayers[i].InventoryManager.HasInventory((IInventory)(object)blockEntityOpenableContainer.Inventory))
			{
				allOnlinePlayers[i].InventoryManager.CloseInventoryAndSync((IInventory)(object)blockEntityOpenableContainer.Inventory);
			}
		}
	}

	public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs, ref EnumHandling handled)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		((BlockBehavior)this).Activate(world, caller, blockSel, activationArgs, ref handled);
		BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
		int num = (int)(activationArgs.TryGetLong("close") ?? 2000);
		BlockEntityOpenableContainer container = blockEntity as BlockEntityOpenableContainer;
		if (container != null)
		{
			byte[] array = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(caller.Entity.EntityId, opened: true));
			((ICoreServerAPI)world.Api).Network.BroadcastBlockEntityPacket(blockSel.Position, 5001, array);
			ICoreAPI api = world.Api;
			((IWorldAccessor)((ICoreServerAPI)((api is ICoreServerAPI) ? api : null)).World).PlaySoundAt(container.OpenSound, blockSel.Position, 0.0, (IPlayer)null, 1f);
			world.Api.Event.RegisterCallback((Action<float>)delegate
			{
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				//IL_0062: Unknown result type (might be due to invalid IL or missing references)
				byte[] array2 = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(caller.Entity.EntityId, opened: false));
				((ICoreServerAPI)world.Api).Network.BroadcastBlockEntityPacket(blockSel.Position, 5001, array2);
				ICoreAPI api2 = world.Api;
				((IWorldAccessor)((ICoreServerAPI)((api2 is ICoreServerAPI) ? api2 : null)).World).PlaySoundAt(container.CloseSound, blockSel.Position, 0.0, (IPlayer)null, 1f);
			}, num);
		}
	}
}
