using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BlockMultiblock : Block, IMultiblockOffset
{
	public delegate T BlockCallDelegateInterface<T, K>(K block);

	public delegate T BlockCallDelegateBlock<T>(Block block);

	public delegate void BlockCallDelegateInterface<K>(K block);

	public delegate void BlockCallDelegateBlock(Block block);

	public Vec3i Offset;

	public Vec3i OffsetInv;

	public override void OnLoaded(ICoreAPI api)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		((Block)this).OnLoaded(api);
		Offset = new Vec3i(StringUtil.ToInt(((RegistryObject)this).Variant["dx"].Replace("n", "-").Replace("p", ""), 0), StringUtil.ToInt(((RegistryObject)this).Variant["dy"].Replace("n", "-").Replace("p", ""), 0), StringUtil.ToInt(((RegistryObject)this).Variant["dz"].Replace("n", "-").Replace("p", ""), 0));
		OffsetInv = -Offset;
	}

	private T Handle<T, K>(IBlockAccessor ba, BlockPos pos, BlockCallDelegateInterface<T, K> onImplementsInterface, BlockCallDelegateBlock<T> onIsMultiblock, BlockCallDelegateBlock<T> onOtherwise) where K : class
	{
		Block block = ba.GetBlock(pos);
		K val = block as K;
		if (val == null)
		{
			val = block.GetInterface<K>(((CollectibleObject)this).api.World, pos);
		}
		if (val != null)
		{
			return onImplementsInterface(val);
		}
		if (block is BlockMultiblock)
		{
			return onIsMultiblock(block);
		}
		return onOtherwise(block);
	}

	private void Handle<K>(IBlockAccessor ba, BlockPos pos, BlockCallDelegateInterface<K> onImplementsInterface, BlockCallDelegateBlock onIsMultiblock, BlockCallDelegateBlock onOtherwise) where K : class
	{
		Block block = ba.GetBlock(pos);
		K val = block as K;
		if (val == null)
		{
			val = block.GetInterface<K>(((CollectibleObject)this).api.World, pos);
		}
		if (val != null)
		{
			onImplementsInterface(val);
		}
		else if (block is BlockMultiblock)
		{
			onIsMultiblock(block);
		}
		else
		{
			onOtherwise(block);
		}
	}

	public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
	{
		BlockSelection bsOffseted = blockSel.Clone();
		bsOffseted.Position.Add(OffsetInv);
		Handle(world.BlockAccessor, bsOffseted.Position, delegate(IMultiBlockActivate inf)
		{
			inf.MBActivate(world, caller, bsOffseted, activationArgs, OffsetInv);
		}, delegate
		{
			<>n__0(world, caller, bsOffseted, activationArgs);
		}, delegate(Block block)
		{
			block.Activate(world, caller, bsOffseted, activationArgs);
		});
	}

	public override BlockSounds GetSounds(IBlockAccessor ba, BlockSelection blockSel, ItemStack stack = null)
	{
		return Handle(ba, blockSel.Position.AddCopy(OffsetInv), (IMultiBlockInteract inf) => inf.MBGetSounds(ba, blockSel, stack, OffsetInv), (Block block) => <>n__1(ba, blockSel.AddPosCopy(OffsetInv), stack), (Block block) => block.GetSounds(ba, blockSel.AddPosCopy(OffsetInv), stack));
	}

	public override Cuboidf[] GetSelectionBoxes(IBlockAccessor ba, BlockPos pos)
	{
		BlockPos val = pos.AddCopy(OffsetInv);
		Block block = ((CollectibleObject)this).api.World.BlockAccessor.GetBlock(val);
		BlockBehaviorMultiblock behavior = ((CollectibleObject)block).GetBehavior<BlockBehaviorMultiblock>();
		if (behavior != null && behavior.offsetHitboxes)
		{
			Cuboidf[] selectionBoxes = block.GetSelectionBoxes(ba, val);
			return offsetedCopy(selectionBoxes);
		}
		return Handle(ba, val, (IMultiBlockColSelBoxes inf) => inf.MBGetSelectionBoxes(ba, pos, OffsetInv), (Block val2) => (Cuboidf[])(object)new Cuboidf[1] { Cuboidf.Default() }, (Block val2) => (Cuboidf[])((((CollectibleObject)val2).Id == 0) ? ((Array)new Cuboidf[1] { Cuboidf.Default() }) : ((Array)val2.GetSelectionBoxes(ba, pos.AddCopy(OffsetInv)))));
	}

	public override Cuboidf[] GetCollisionBoxes(IBlockAccessor ba, BlockPos pos)
	{
		BlockPos val = pos.AddCopy(OffsetInv);
		Block block = ((CollectibleObject)this).api.World.BlockAccessor.GetBlock(val);
		BlockBehaviorMultiblock behavior = ((CollectibleObject)block).GetBehavior<BlockBehaviorMultiblock>();
		if (behavior != null && behavior.offsetHitboxes)
		{
			Cuboidf[] collisionBoxes = block.GetCollisionBoxes(ba, val);
			return offsetedCopy(collisionBoxes);
		}
		return Handle(ba, val, (IMultiBlockColSelBoxes inf) => inf.MBGetCollisionBoxes(ba, pos, OffsetInv), (Block val2) => (Cuboidf[])(object)new Cuboidf[1] { Cuboidf.Default() }, (Block val2) => val2.GetCollisionBoxes(ba, pos.AddCopy(OffsetInv)));
	}

	public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor ba, BlockPos pos)
	{
		BlockPos val = pos.AddCopy(OffsetInv);
		Block block = ((CollectibleObject)this).api.World.BlockAccessor.GetBlock(val);
		BlockBehaviorMultiblock behavior = ((CollectibleObject)block).GetBehavior<BlockBehaviorMultiblock>();
		if (behavior != null && behavior.offsetHitboxes)
		{
			Cuboidf[] particleCollisionBoxes = block.GetParticleCollisionBoxes(ba, val);
			return offsetedCopy(particleCollisionBoxes);
		}
		return Handle(ba, pos.AddCopy(OffsetInv), (IMultiBlockColSelBoxes inf) => inf.MBGetCollisionBoxes(ba, pos, OffsetInv), (Block val2) => (Cuboidf[])(object)new Cuboidf[1] { Cuboidf.Default() }, (Block val2) => val2.GetParticleCollisionBoxes(ba, pos.AddCopy(OffsetInv)));
	}

	public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
	{
		BlockPos val = pos.AddCopy(OffsetInv);
		BlockBehaviorMultiblock behavior = ((CollectibleObject)((CollectibleObject)this).api.World.BlockAccessor.GetBlock(val)).GetBehavior<BlockBehaviorMultiblock>();
		if (behavior != null && behavior.offsetHitboxes)
		{
			return false;
		}
		return Handle(world.BlockAccessor, val, (IMultiBlockInteract inf) => inf.MBDoPartialSelection(world, pos, OffsetInv), (Block block) => <>n__2(world, pos.AddCopy(OffsetInv)), (Block block) => block.DoPartialSelection(world, pos.AddCopy(OffsetInv)));
	}

	protected Cuboidf[] offsetedCopy(Cuboidf[] boxes)
	{
		Cuboidf[] array = (Cuboidf[])(object)new Cuboidf[boxes.Length];
		for (int i = 0; i < boxes.Length; i++)
		{
			array[i] = boxes[i].Clone();
			array[i].Offset((float)OffsetInv.X, (float)OffsetInv.Y, (float)OffsetInv.Z);
		}
		return array;
	}

	public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
	{
		BlockSelection bsOffseted = blockSel.Clone();
		bsOffseted.Position.Add(OffsetInv);
		return Handle(((CollectibleObject)this).api.World.BlockAccessor, bsOffseted.Position, (IMultiBlockBlockBreaking inf) => inf.MBOnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter, OffsetInv), (Block block) => <>n__3(player, bsOffseted, itemslot, remainingResistance, dt, counter), delegate(Block block)
		{
			ICoreAPI api = ((CollectibleObject)this).api;
			ICoreClientAPI val = (ICoreClientAPI)(object)((api is ICoreClientAPI) ? api : null);
			if (val != null)
			{
				val.World.CloneBlockDamage(blockSel.Position, blockSel.Position.AddCopy(OffsetInv));
			}
			return block.OnGettingBroken(player, bsOffseted, itemslot, remainingResistance, dt, counter);
		});
	}

	public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
	{
		Block block = world.BlockAccessor.GetBlock(pos.AddCopy(OffsetInv));
		if (((CollectibleObject)block).Id == 0)
		{
			((Block)this).OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
			return;
		}
		IMultiBlockBlockBreaking multiBlockBlockBreaking = block as IMultiBlockBlockBreaking;
		if (multiBlockBlockBreaking == null)
		{
			multiBlockBlockBreaking = block.GetBehavior(typeof(IMultiBlockBlockBreaking), true) as IMultiBlockBlockBreaking;
		}
		if (multiBlockBlockBreaking != null)
		{
			multiBlockBlockBreaking.MBOnBlockBroken(world, pos, OffsetInv, byPlayer);
		}
		else if (!(block is BlockMultiblock))
		{
			block.OnBlockBroken(world, pos.AddCopy(OffsetInv), byPlayer, dropQuantityMultiplier);
		}
	}

	public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
	{
		return Handle(world.BlockAccessor, pos.AddCopy(OffsetInv), (IMultiBlockInteract inf) => inf.MBOnPickBlock(world, pos, OffsetInv), (Block block) => <>n__4(world, pos.AddCopy(OffsetInv)), (Block block) => block.OnPickBlock(world, pos.AddCopy(OffsetInv)));
	}

	public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
	{
		BlockSelection bsOffseted = blockSel.Clone();
		bsOffseted.Position.Add(OffsetInv);
		return Handle(world.BlockAccessor, bsOffseted.Position, (IMultiBlockInteract inf) => inf.MBOnBlockInteractStart(world, byPlayer, blockSel, OffsetInv), (Block block) => <>n__5(world, byPlayer, bsOffseted), (Block block) => block.OnBlockInteractStart(world, byPlayer, bsOffseted));
	}

	public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
	{
		BlockSelection bsOffseted = blockSel.Clone();
		bsOffseted.Position.Add(OffsetInv);
		return Handle(world.BlockAccessor, bsOffseted.Position, (IMultiBlockInteract inf) => inf.MBOnBlockInteractStep(secondsUsed, world, byPlayer, blockSel, OffsetInv), (Block block) => <>n__6(secondsUsed, world, byPlayer, bsOffseted), (Block block) => block.OnBlockInteractStep(secondsUsed, world, byPlayer, bsOffseted));
	}

	public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
	{
		BlockSelection bsOffseted = blockSel.Clone();
		bsOffseted.Position.Add(OffsetInv);
		Handle(world.BlockAccessor, bsOffseted.Position, delegate(IMultiBlockInteract inf)
		{
			inf.MBOnBlockInteractStop(secondsUsed, world, byPlayer, blockSel, OffsetInv);
		}, delegate
		{
			<>n__7(secondsUsed, world, byPlayer, bsOffseted);
		}, delegate(Block block)
		{
			block.OnBlockInteractStop(secondsUsed, world, byPlayer, bsOffseted);
		});
	}

	public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		BlockSelection bsOffseted = blockSel.Clone();
		bsOffseted.Position.Add(OffsetInv);
		return Handle(world.BlockAccessor, bsOffseted.Position, (IMultiBlockInteract inf) => inf.MBOnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason, OffsetInv), (Block block) => <>n__8(secondsUsed, world, byPlayer, bsOffseted, cancelReason), (Block block) => block.OnBlockInteractCancel(secondsUsed, world, byPlayer, bsOffseted, cancelReason));
	}

	public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer)
	{
		BlockSelection bsOffseted = blockSel.Clone();
		bsOffseted.Position.Add(OffsetInv);
		return Handle(world.BlockAccessor, bsOffseted.Position, (IMultiBlockInteract inf) => inf.MBGetPlacedBlockInteractionHelp(world, blockSel, forPlayer, OffsetInv), (Block block) => <>n__9(world, bsOffseted, forPlayer), (Block block) => block.GetPlacedBlockInteractionHelp(world, bsOffseted, forPlayer));
	}

	public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
	{
		BlockPos val = pos.AddCopy(OffsetInv);
		Block block = world.BlockAccessor.GetBlock(val);
		if (block is BlockMultiblock)
		{
			return "";
		}
		return block.GetPlacedBlockInfo(world, val, forPlayer);
	}

	public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
	{
		IClientWorldAccessor world = capi.World;
		return Handle(((IWorldAccessor)world).BlockAccessor, pos.AddCopy(OffsetInv), (IMultiBlockBlockBreaking inf) => inf.MBGetRandomColor(capi, pos, facing, rndIndex, OffsetInv), (Block block) => <>n__10(capi, pos, facing, rndIndex), (Block block) => block.GetRandomColor(capi, pos, facing, rndIndex));
	}

	public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
	{
		IClientWorldAccessor world = capi.World;
		return Handle(((IWorldAccessor)world).BlockAccessor, pos.AddCopy(OffsetInv), (IMultiBlockBlockBreaking inf) => inf.MBGetColorWithoutTint(capi, pos, OffsetInv), (Block block) => <>n__11(capi, pos), (Block block) => block.GetColorWithoutTint(capi, pos));
	}

	public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
	{
		return Handle(blockAccessor, pos.AddCopy(OffsetInv), (IMultiBlockBlockProperties inf) => inf.MBCanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea, OffsetInv), (Block nblock) => <>n__12(blockAccessor, block, pos, blockFace, attachmentArea), (Block nblock) => nblock.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea));
	}

	public override bool SideIsSolid(BlockPos pos, int faceIndex)
	{
		IBlockAccessor blockAccessor = ((CollectibleObject)this).api.World.BlockAccessor;
		return Handle(blockAccessor, pos.AddCopy(OffsetInv), (IMultiBlockBlockProperties inf) => inf.MBSideIsSolid(pos, faceIndex, OffsetInv), (Block nblock) => <>n__13(pos, faceIndex), (Block nblock) => nblock.SideIsSolid(pos, faceIndex));
	}

	public override bool SideIsSolid(IBlockAccessor blockAccessor, BlockPos pos, int faceIndex)
	{
		return Handle(blockAccessor, pos.AddCopy(OffsetInv), (IMultiBlockBlockProperties inf) => inf.MBSideIsSolid(blockAccessor, pos, faceIndex, OffsetInv), (Block nblock) => <>n__14(blockAccessor, pos, faceIndex), (Block nblock) => nblock.SideIsSolid(blockAccessor, pos, faceIndex));
	}

	public override JsonObject GetAttributes(IBlockAccessor blockAccessor, BlockPos pos)
	{
		return Handle(blockAccessor, pos.AddCopy(OffsetInv), (IMultiBlockBlockProperties inf) => inf.MBGetAttributes(blockAccessor, pos), (Block nblock) => <>n__15(blockAccessor, pos), (Block nblock) => nblock.GetAttributes(blockAccessor, pos));
	}

	public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		IBlockAccessor blockAccessor = ((CollectibleObject)this).api.World.BlockAccessor;
		return Handle(blockAccessor, pos.AddCopy(OffsetInv), (IMultiBlockBlockProperties inf) => inf.MBGetRetention(pos, facing, type, OffsetInv), (Block nblock) => <>n__16(pos, facing, (EnumRetentionType)0), (Block nblock) => nblock.GetRetention(pos, facing, (EnumRetentionType)0));
	}

	public override float GetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos)
	{
		IBlockAccessor blockAccessor = ((CollectibleObject)this).api.World.BlockAccessor;
		return Handle(blockAccessor, pos.AddCopy(OffsetInv), (IMultiBlockBlockProperties inf) => inf.MBGetLiquidBarrierHeightOnSide(face, pos, OffsetInv), (Block nblock) => <>n__17(face, pos), (Block nblock) => nblock.GetLiquidBarrierHeightOnSide(face, pos));
	}

	public override T GetBlockEntity<T>(BlockPos position)
	{
		Block block = ((CollectibleObject)this).api.World.BlockAccessor.GetBlock(position.AddCopy(OffsetInv));
		if (block is BlockMultiblock)
		{
			return ((Block)this).GetBlockEntity<T>(position);
		}
		return block.GetBlockEntity<T>(position.AddCopy(OffsetInv));
	}

	public override T GetBlockEntity<T>(BlockSelection blockSel)
	{
		Block block = ((CollectibleObject)this).api.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(OffsetInv));
		if (block is BlockMultiblock)
		{
			return ((Block)this).GetBlockEntity<T>(blockSel);
		}
		BlockSelection val = blockSel.Clone();
		val.Position.Add(OffsetInv);
		return block.GetBlockEntity<T>(val);
	}

	public override AssetLocation GetRotatedBlockCode(int angle)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Expected O, but got Unknown
		Vec3i val;
		switch ((angle / 90 % 4 + 4) % 4)
		{
		case 0:
			return ((RegistryObject)this).Code;
		case 1:
			val = new Vec3i(-Offset.Z, Offset.Y, Offset.X);
			break;
		case 2:
			val = new Vec3i(-Offset.X, Offset.Y, -Offset.Z);
			break;
		case 3:
			val = new Vec3i(Offset.Z, Offset.Y, -Offset.X);
			break;
		default:
			val = null;
			break;
		}
		return new AssetLocation(((RegistryObject)this).Code.Domain, "multiblock-monolithic" + OffsetToString(val.X) + OffsetToString(val.Y) + OffsetToString(val.Z));
	}

	private string OffsetToString(int x)
	{
		if (x == 0)
		{
			return "-0";
		}
		if (x < 0)
		{
			return "-n" + -x;
		}
		return "-p" + x;
	}

	public virtual BlockPos GetControlBlockPos(BlockPos pos)
	{
		return pos.AddCopy(OffsetInv);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private void <>n__0(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
	{
		((Block)this).Activate(world, caller, blockSel, activationArgs);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private BlockSounds <>n__1(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack stack = null)
	{
		return ((Block)this).GetSounds(blockAccessor, blockSel, stack);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private bool <>n__2(IWorldAccessor world, BlockPos pos)
	{
		return ((Block)this).DoPartialSelection(world, pos);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private float <>n__3(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
	{
		return ((Block)this).OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private ItemStack <>n__4(IWorldAccessor world, BlockPos pos)
	{
		return ((Block)this).OnPickBlock(world, pos);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private bool <>n__5(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
	{
		return ((Block)this).OnBlockInteractStart(world, byPlayer, blockSel);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private bool <>n__6(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
	{
		return ((Block)this).OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private void <>n__7(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
	{
		((Block)this).OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private bool <>n__8(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return ((Block)this).OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private WorldInteraction[] <>n__9(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
	{
		return ((Block)this).GetPlacedBlockInteractionHelp(world, selection, forPlayer);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private int <>n__10(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
	{
		return ((Block)this).GetRandomColor(capi, pos, facing, rndIndex);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private int <>n__11(ICoreClientAPI capi, BlockPos pos)
	{
		return ((Block)this).GetColorWithoutTint(capi, pos);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private bool <>n__12(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
	{
		return ((Block)this).CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private bool <>n__13(BlockPos pos, int faceIndex)
	{
		return ((Block)this).SideIsSolid(pos, faceIndex);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private bool <>n__14(IBlockAccessor blockAccess, BlockPos pos, int faceIndex)
	{
		return ((Block)this).SideIsSolid(blockAccess, pos, faceIndex);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private JsonObject <>n__15(IBlockAccessor blockAccessor, BlockPos pos)
	{
		return ((Block)this).GetAttributes(blockAccessor, pos);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private int <>n__16(BlockPos pos, BlockFacing facing, EnumRetentionType type)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return ((Block)this).GetRetention(pos, facing, type);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private float <>n__17(BlockFacing face, BlockPos pos)
	{
		return ((Block)this).GetLiquidBarrierHeightOnSide(face, pos);
	}
}
