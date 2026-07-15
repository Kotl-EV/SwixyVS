using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public abstract class BlockEntityOpenableContainer : BlockEntityContainer
{
	protected GuiDialogBlockEntity invDialog;

	public SoundAttributes OpenSound = new SoundAttributes(AssetLocation.Create("sounds/block/chestopen", "game"), true);

	public SoundAttributes CloseSound = new SoundAttributes(AssetLocation.Create("sounds/block/chestclose", "game"), true);

	public HashSet<long> LidOpenEntityId;

	public abstract bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel);

	public override void Initialize(ICoreAPI api)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		base.Initialize(api);
		LidOpenEntityId = new HashSet<long>();
		Inventory.LateInitialize(InventoryClassName + "-" + (object)((BlockEntity)this).Pos, api);
		Inventory.ResolveBlocksOrItems();
		Inventory.OnInventoryOpened += new OnInventoryOpenedDelegate(OnInventoryOpened);
		Inventory.OnInventoryClosed += new OnInventoryClosedDelegate(OnInventoryClosed);
		JsonObject attributes = ((CollectibleObject)((BlockEntity)this).Block).Attributes;
		OpenSound = (SoundAttributes)(((??)((attributes != null) ? attributes["openSound"].AsObject<SoundAttributes?>((SoundAttributes?)null, ((RegistryObject)((BlockEntity)this).Block).Code.Domain, true) : ((SoundAttributes?)null))) ?? OpenSound);
		JsonObject attributes2 = ((CollectibleObject)((BlockEntity)this).Block).Attributes;
		CloseSound = (SoundAttributes)(((??)((attributes2 != null) ? attributes2["closeSound"].AsObject<SoundAttributes?>((SoundAttributes?)null, ((RegistryObject)((BlockEntity)this).Block).Code.Domain, true) : ((SoundAttributes?)null))) ?? CloseSound);
	}

	protected void OnInventoryOpened(IPlayer player)
	{
		LidOpenEntityId.Add(((Entity)player.Entity).EntityId);
	}

	protected void OnInventoryClosed(IPlayer player)
	{
		LidOpenEntityId.Remove(((Entity)player.Entity).EntityId);
	}

	protected void toggleInventoryDialogClient(IPlayer byPlayer, CreateDialogDelegate onCreateDialog)
	{
		if (invDialog == null)
		{
			ICoreAPI api = ((BlockEntity)this).Api;
			ICoreClientAPI capi = (ICoreClientAPI)(object)((api is ICoreClientAPI) ? api : null);
			invDialog = onCreateDialog();
			((GuiDialog)invDialog).OnClosed += delegate
			{
				invDialog = null;
				capi.Network.SendBlockEntityPacket(((BlockEntity)this).Pos, 1001, (byte[])null);
			};
			((GuiDialog)invDialog).TryOpen();
			capi.Network.SendPacketClient(Inventory.Open(byPlayer));
			capi.Network.SendBlockEntityPacket(((BlockEntity)this).Pos, 1000, (byte[])null);
		}
		else
		{
			((GuiDialog)invDialog).TryClose();
		}
	}

	public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Expected O, but got Unknown
		if (packetid == 1001)
		{
			IPlayerInventoryManager inventoryManager = player.InventoryManager;
			if (inventoryManager != null)
			{
				inventoryManager.CloseInventory((IInventory)(object)Inventory);
			}
			data = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(((Entity)player.Entity).EntityId, opened: false));
			((ICoreServerAPI)((BlockEntity)this).Api).Network.BroadcastBlockEntityPacket(((BlockEntity)this).Pos, 5001, data, (IServerPlayer[])(object)new IServerPlayer[1] { (IServerPlayer)player });
		}
		if (!((BlockEntity)this).Api.World.Claims.TryAccess(player, ((BlockEntity)this).Pos, (EnumBlockAccessFlags)2))
		{
			((BlockEntity)this).Api.World.Logger.Audit("Player {0} sent an inventory packet to openable container at {1} but has no claim access. Rejected.", new object[2]
			{
				player.PlayerName,
				((BlockEntity)this).Pos
			});
		}
		else if (packetid < 1000)
		{
			Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);
			((BlockEntity)this).Api.World.BlockAccessor.GetChunkAtBlockPos(((BlockEntity)this).Pos).MarkModified();
		}
		else if (packetid == 1000)
		{
			IPlayerInventoryManager inventoryManager2 = player.InventoryManager;
			if (inventoryManager2 != null)
			{
				inventoryManager2.OpenInventory((IInventory)(object)Inventory);
			}
			data = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(((Entity)player.Entity).EntityId, opened: true));
			((ICoreServerAPI)((BlockEntity)this).Api).Network.BroadcastBlockEntityPacket(((BlockEntity)this).Pos, 5001, data, (IServerPlayer[])(object)new IServerPlayer[1] { (IServerPlayer)player });
		}
	}

	public override void OnReceivedServerPacket(int packetid, byte[] data)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		IClientWorldAccessor val = (IClientWorldAccessor)((BlockEntity)this).Api.World;
		if (packetid == 5000)
		{
			if (invDialog != null)
			{
				GuiDialogBlockEntity obj = invDialog;
				if (obj != null && ((GuiDialog)obj).IsOpened())
				{
					((GuiDialog)invDialog).TryClose();
				}
				GuiDialogBlockEntity obj2 = invDialog;
				if (obj2 != null)
				{
					((GuiDialog)obj2).Dispose();
				}
				invDialog = null;
				return;
			}
			BlockEntityContainerOpen blockEntityContainerOpen = BlockEntityContainerOpen.FromBytes(data);
			Inventory.FromTreeAttributes((ITreeAttribute)(object)blockEntityContainerOpen.Tree);
			Inventory.ResolveBlocksOrItems();
			string dialogTitle = blockEntityContainerOpen.DialogTitle;
			InventoryBase inventory = Inventory;
			BlockPos pos = ((BlockEntity)this).Pos;
			byte columns = blockEntityContainerOpen.Columns;
			ICoreAPI api = ((BlockEntity)this).Api;
			invDialog = (GuiDialogBlockEntity)new GuiDialogBlockEntityInventory(dialogTitle, inventory, pos, (int)columns, (ICoreClientAPI)(object)((api is ICoreClientAPI) ? api : null));
			Block block = ((BlockEntity)this).Api.World.BlockAccessor.GetBlock(((BlockEntity)this).Pos);
			GuiDialogBlockEntity obj3 = invDialog;
			JsonObject attributes = ((CollectibleObject)block).Attributes;
			SoundAttributes? obj4;
			if (attributes == null)
			{
				obj4 = null;
			}
			else
			{
				JsonObject obj5 = attributes["openSound"];
				obj4 = ((obj5 != null) ? obj5.AsObject<SoundAttributes?>((SoundAttributes?)null, ((RegistryObject)((BlockEntity)this).Block).Code.Domain, true) : ((SoundAttributes?)null));
			}
			obj3.OpenSound = (SoundAttributes)(((??)obj4) ?? OpenSound);
			GuiDialogBlockEntity obj6 = invDialog;
			JsonObject attributes2 = ((CollectibleObject)block).Attributes;
			SoundAttributes? obj7;
			if (attributes2 == null)
			{
				obj7 = null;
			}
			else
			{
				JsonObject obj8 = attributes2["closeSound"];
				obj7 = ((obj8 != null) ? obj8.AsObject<SoundAttributes?>((SoundAttributes?)null, ((RegistryObject)((BlockEntity)this).Block).Code.Domain, true) : ((SoundAttributes?)null));
			}
			obj6.CloseSound = (SoundAttributes)(((??)obj7) ?? CloseSound);
			((GuiDialog)invDialog).TryOpen();
		}
		if (packetid == 5001)
		{
			OpenContainerLidPacket openContainerLidPacket = SerializerUtil.Deserialize<OpenContainerLidPacket>(data);
			if (this is BlockEntityGenericTypedContainer blockEntityGenericTypedContainer)
			{
				if (openContainerLidPacket.Opened)
				{
					LidOpenEntityId.Add(openContainerLidPacket.EntityId);
					blockEntityGenericTypedContainer.OpenLid();
				}
				else
				{
					LidOpenEntityId.Remove(openContainerLidPacket.EntityId);
					if (LidOpenEntityId.Count == 0)
					{
						blockEntityGenericTypedContainer.CloseLid();
					}
				}
			}
		}
		if (packetid != 1001)
		{
			return;
		}
		((IPlayer)val.Player).InventoryManager.CloseInventory((IInventory)(object)Inventory);
		GuiDialogBlockEntity obj9 = invDialog;
		if (obj9 != null && ((GuiDialog)obj9).IsOpened())
		{
			GuiDialogBlockEntity obj10 = invDialog;
			if (obj10 != null)
			{
				((GuiDialog)obj10).TryClose();
			}
		}
		GuiDialogBlockEntity obj11 = invDialog;
		if (obj11 != null)
		{
			((GuiDialog)obj11).Dispose();
		}
		invDialog = null;
	}

	public override void OnBlockUnloaded()
	{
		((BlockEntity)this).OnBlockUnloaded();
		Dispose();
	}

	public override void OnBlockRemoved()
	{
		((BlockEntity)this).OnBlockRemoved();
		Dispose();
	}

	protected override void Dispose()
	{
		base.Dispose();
		GuiDialogBlockEntity obj = invDialog;
		if (obj != null && ((GuiDialog)obj).IsOpened())
		{
			GuiDialogBlockEntity obj2 = invDialog;
			if (obj2 != null)
			{
				((GuiDialog)obj2).TryClose();
			}
		}
		GuiDialogBlockEntity obj3 = invDialog;
		if (obj3 != null)
		{
			((GuiDialog)obj3).Dispose();
		}
		if (((BlockEntity)this).Api is ICoreServerAPI)
		{
			Inventory.openedByPlayerGUIds?.Clear();
		}
	}

	public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
	{
		base.GetBlockInfo(forPlayer, dsc);
	}

	public override void DropContents(Vec3d atPos)
	{
		Inventory.DropAll(atPos, 0);
	}
}
