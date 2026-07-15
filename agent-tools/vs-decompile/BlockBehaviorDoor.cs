using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

[DocumentAsJson]
[AddDocumentationProperty("TriggerSound", "Sets both OpenSound & CloseSound.", "Vintagestory.API.Common.AssetLocation", "Optional", "sounds/block/door", true)]
public class BlockBehaviorDoor : StrongBlockBehavior, IMultiBlockColSelBoxes, IMultiBlockBlockProperties, IClaimTraverseable
{
	[DocumentAsJson("Optional", "sounds/block/door", true)]
	public AssetLocation OpenSound;

	[DocumentAsJson("Optional", "sounds/block/door", true)]
	public AssetLocation CloseSound;

	[DocumentAsJson("Optional", "1", true)]
	public int width;

	[DocumentAsJson("Optional", "1", true)]
	public int height;

	[DocumentAsJson("Optional", "True", true)]
	public bool handopenable;

	[DocumentAsJson("Optional", "True", true)]
	public bool airtight;

	[DocumentAsJson("Optional", "1.0 for each block", true)]
	public float[][] liquidBarrierHeight;

	private ICoreAPI api;

	public MeshData animatableOrigMesh;

	public Shape animatableShape;

	public string animatableDictKey;

	public BlockBehaviorDoor(Block block)
		: base(block)
	{
		airtight = ((CollectibleObject)block).Attributes["airtight"].AsBool(true);
		width = ((CollectibleObject)block).Attributes["width"].AsInt(1);
		height = ((CollectibleObject)block).Attributes["height"].AsInt(1);
		handopenable = ((CollectibleObject)block).Attributes["handopenable"].AsBool(true);
		liquidBarrierHeight = ((CollectibleObject)block).Attributes["liquidBarrierHeight"].AsObject<float[][]>((float[][])null);
	}

	public override void OnLoaded(ICoreAPI api)
	{
		this.api = api;
		OpenSound = (CloseSound = AssetLocation.Create(((CollectibleObject)((BlockBehavior)this).block).Attributes["triggerSound"].AsString("sounds/block/door"), "game"));
		JsonObject val = ((CollectibleObject)((BlockBehavior)this).block).Attributes["openSound"];
		if (val.Exists)
		{
			OpenSound = AssetLocation.Create(val.AsString("sounds/block/door"), "game");
		}
		val = ((CollectibleObject)((BlockBehavior)this).block).Attributes["closeSound"];
		if (val.Exists)
		{
			CloseSound = AssetLocation.Create(val.AsString("sounds/block/door"), "game");
		}
		((CollectibleBehavior)this).OnLoaded(api);
	}

	public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs, ref EnumHandling handled)
	{
		BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
		BEBehaviorDoor bEBehaviorDoor = ((blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorDoor>() : null);
		bool flag = !bEBehaviorDoor.Opened;
		if (activationArgs != null)
		{
			flag = activationArgs.GetBool("opened", flag);
		}
		if (bEBehaviorDoor.Opened != flag)
		{
			bEBehaviorDoor.ToggleDoorState(null, flag);
		}
	}

	public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos, ref EnumHandling handled)
	{
		handled = (EnumHandling)2;
		BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
		BEBehaviorDoor bEBehaviorDoor = ((blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorDoor>() : null);
		if (bEBehaviorDoor != null)
		{
			decalMesh.Rotate(0f, bEBehaviorDoor.RotateYRad, 0f);
		}
	}

	public static BEBehaviorDoor getDoorAt(IWorldAccessor world, BlockPos pos)
	{
		BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
		BEBehaviorDoor bEBehaviorDoor = ((blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorDoor>() : null);
		if (bEBehaviorDoor != null)
		{
			return bEBehaviorDoor;
		}
		if (world.BlockAccessor.GetBlock(pos) is BlockMultiblock blockMultiblock)
		{
			BlockEntity blockEntity2 = world.BlockAccessor.GetBlockEntity(pos.AddCopy(blockMultiblock.OffsetInv));
			return (blockEntity2 != null) ? blockEntity2.GetBehavior<BEBehaviorDoor>() : null;
		}
		return null;
	}

	public static bool HasCombinableLeftDoor(IWorldAccessor world, float RotateYRad, BlockPos pos, int width, out BEBehaviorDoor leftDoor, out int leftOffset)
	{
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Invalid comparison between Unknown and I4
		leftOffset = 0;
		leftDoor = null;
		BlockFacing cW = BlockFacing.HorizontalFromYaw(RotateYRad).GetCW();
		BlockPos val = pos.AddCopy(cW);
		leftDoor = getDoorAt(world, val);
		if (width > 1)
		{
			if (leftDoor == null)
			{
				for (int i = 2; i <= width; i++)
				{
					val = pos.AddCopy(cW, i);
					leftDoor = getDoorAt(world, val);
					if (leftDoor != null)
					{
						break;
					}
				}
			}
			if (leftDoor != null)
			{
				BlockPos val2 = ((BlockEntityBehavior)leftDoor).Pos.AddCopy(cW.Opposite, leftDoor.InvertHandles ? width : (width + leftDoor.doorBh.width - 1));
				leftOffset = (int)pos.DistanceTo(val2);
				if (((int)leftDoor.facingWhenClosed.Axis == 0 && val.X != ((BlockEntityBehavior)leftDoor).Pos.X) || ((int)leftDoor.facingWhenClosed.Axis == 2 && val.Z != ((BlockEntityBehavior)leftDoor).Pos.Z))
				{
					leftDoor = null;
					leftOffset = 0;
				}
			}
		}
		if (leftDoor != null && leftDoor.LeftDoor == null && leftDoor.RightDoor == null && leftDoor.facingWhenClosed == BlockFacing.HorizontalFromYaw(RotateYRad))
		{
			return true;
		}
		return false;
	}

	public static bool HasCombinableRightDoor(IWorldAccessor world, float RotateYRad, BlockPos pos, int width, out BEBehaviorDoor rightDoor, out int rightOffset)
	{
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Invalid comparison between Unknown and I4
		rightOffset = 0;
		rightDoor = null;
		BlockFacing cCW = BlockFacing.HorizontalFromYaw(RotateYRad).GetCCW();
		BlockPos val = pos.AddCopy(cCW);
		rightDoor = getDoorAt(world, val);
		if (width > 1)
		{
			if (rightDoor == null)
			{
				for (int i = 2; i <= width; i++)
				{
					val = pos.AddCopy(cCW, i);
					rightDoor = getDoorAt(world, val);
					if (rightDoor != null)
					{
						break;
					}
				}
			}
			if (rightDoor != null)
			{
				BlockPos val2 = ((BlockEntityBehavior)rightDoor).Pos.AddCopy(cCW.Opposite, (!rightDoor.InvertHandles) ? width : (width + rightDoor.doorBh.width - 1));
				rightOffset = (int)pos.DistanceTo(val2);
				if (((int)rightDoor.facingWhenClosed.Axis == 0 && val.X != ((BlockEntityBehavior)rightDoor).Pos.X) || ((int)rightDoor.facingWhenClosed.Axis == 2 && val.Z != ((BlockEntityBehavior)rightDoor).Pos.Z))
				{
					rightDoor = null;
					rightOffset = 0;
				}
			}
		}
		if (rightDoor != null && rightDoor.RightDoor == null && rightDoor.LeftDoor == null && rightDoor.facingWhenClosed == BlockFacing.HorizontalFromYaw(RotateYRad))
		{
			return true;
		}
		return false;
	}

	public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
	{
		BlockPos val = blockSel.Position.Copy();
		float rotateYRad = BEBehaviorDoor.getRotateYRad(byPlayer, blockSel);
		BlockFacing val2 = BlockFacing.HorizontalFromYaw(rotateYRad);
		bool blocked = false;
		BEBehaviorDoor leftDoor;
		int leftOffset;
		bool flag = HasCombinableLeftDoor(world, rotateYRad, blockSel.Position, width, out leftDoor, out leftOffset);
		if (flag && width > 1 && leftOffset != 0)
		{
			val.Add(val2.GetCCW(), leftOffset);
		}
		if (!flag && HasCombinableRightDoor(world, rotateYRad, blockSel.Position, width, out leftDoor, out leftOffset) && width > 1 && leftOffset != 0)
		{
			val.Add(val2.GetCW(), leftOffset);
		}
		IterateOverEach(val, rotateYRad, flag, delegate(BlockPos mpos)
		{
			if (!world.BlockAccessor.GetBlock(mpos, 1).IsReplacableBy(((BlockBehavior)this).block))
			{
				blocked = true;
				return false;
			}
			return true;
		});
		if (blocked)
		{
			handling = (EnumHandling)2;
			failureCode = "notenoughspace";
			return false;
		}
		return ((BlockBehavior)this).CanPlaceBlock(world, byPlayer, blockSel, ref handling, ref failureCode);
	}

	public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
	{
		handling = (EnumHandling)2;
		BlockPos val = blockSel.Position.Copy();
		IBlockAccessor blockAccessor = world.BlockAccessor;
		if (((BlockBehavior)this).block.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
		{
			float rotateYRad = BEBehaviorDoor.getRotateYRad(byPlayer, blockSel);
			BlockFacing val2 = BlockFacing.HorizontalFromYaw(rotateYRad);
			if (HasCombinableLeftDoor(world, rotateYRad, blockSel.Position, width, out var leftDoor, out var leftOffset))
			{
				if (width > 1 && leftOffset != 0)
				{
					val.Add(val2.GetCCW(), leftOffset);
				}
			}
			else if (HasCombinableRightDoor(world, rotateYRad, blockSel.Position, width, out leftDoor, out leftOffset) && width > 1 && leftOffset != 0)
			{
				val.Add(val2.GetCW(), leftOffset);
			}
			return placeDoor(world, byPlayer, itemstack, blockSel, val, blockAccessor);
		}
		return false;
	}

	public bool placeDoor(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, BlockPos pos, IBlockAccessor ba)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Invalid comparison between Unknown and I4
		ba.SetBlock(((BlockBehavior)this).block.BlockId, pos, itemstack);
		BlockEntity blockEntity = ba.GetBlockEntity(pos);
		((blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorDoor>() : null).OnBlockPlaced(itemstack, byPlayer, blockSel);
		if ((int)world.Side == 1)
		{
			placeMultiblockParts(world, pos);
		}
		return true;
	}

	public void placeMultiblockParts(IWorldAccessor world, BlockPos pos)
	{
		BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
		BEBehaviorDoor bEBehaviorDoor = ((blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorDoor>() : null);
		float yRotRad = bEBehaviorDoor?.RotateYRad ?? 0f;
		IterateOverEach(pos, yRotRad, bEBehaviorDoor?.InvertHandles ?? false, delegate(BlockPos mpos)
		{
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Expected O, but got Unknown
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_0146: Invalid comparison between Unknown and I4
			if (mpos == pos)
			{
				return true;
			}
			int num = mpos.X - pos.X;
			int num2 = mpos.Y - pos.Y;
			int num3 = mpos.Z - pos.Z;
			string text = ((num < 0) ? "n" : ((num > 0) ? "p" : "")) + Math.Abs(num);
			string text2 = ((num2 < 0) ? "n" : ((num2 > 0) ? "p" : "")) + Math.Abs(num2);
			string text3 = ((num3 < 0) ? "n" : ((num3 > 0) ? "p" : "")) + Math.Abs(num3);
			AssetLocation val = new AssetLocation("multiblock-monolithic-" + text + "-" + text2 + "-" + text3);
			Block block = world.GetBlock(val);
			world.BlockAccessor.SetBlock(((CollectibleObject)block).Id, mpos);
			if ((int)world.Side == 1)
			{
				world.BlockAccessor.TriggerNeighbourBlockUpdate(mpos);
			}
			return true;
		});
	}

	public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Invalid comparison between Unknown and I4
		if ((int)world.Side == 2)
		{
			return;
		}
		BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
		BEBehaviorDoor bEBehaviorDoor = ((blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorDoor>() : null);
		float yRotRad = bEBehaviorDoor?.RotateYRad ?? 0f;
		IterateOverEach(pos, yRotRad, bEBehaviorDoor?.InvertHandles ?? false, delegate(BlockPos mpos)
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Invalid comparison between Unknown and I4
			if (mpos == pos)
			{
				return true;
			}
			if (world.BlockAccessor.GetBlock(mpos) is BlockMultiblock)
			{
				world.BlockAccessor.SetBlock(0, mpos);
				if ((int)world.Side == 1)
				{
					world.BlockAccessor.TriggerNeighbourBlockUpdate(mpos);
				}
			}
			return true;
		});
		((BlockBehavior)this).OnBlockRemoved(world, pos, ref handling);
	}

	public void IterateOverEach(BlockPos pos, float yRotRad, bool invertHandle, ActionConsumable<BlockPos> onBlock)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		BlockPos val = new BlockPos(pos.dimension);
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				for (int k = 0; k < width; k++)
				{
					Vec3i adjacentOffset = BEBehaviorDoor.getAdjacentOffset(i, k, j, yRotRad, invertHandle);
					val.Set(pos.X + adjacentOffset.X, pos.Y + adjacentOffset.Y, pos.Z + adjacentOffset.Z);
					if (!onBlock.Invoke(val))
					{
						return;
					}
				}
			}
		}
	}

	public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
	{
		return getColSelBoxes(blockAccessor, pos, offset);
	}

	public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
	{
		return getColSelBoxes(blockAccessor, pos, offset);
	}

	private static Cuboidf[] getColSelBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
	{
		BlockEntity blockEntity = blockAccessor.GetBlockEntity(pos.AddCopy(offset.X, offset.Y, offset.Z));
		BEBehaviorDoor bEBehaviorDoor = ((blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorDoor>() : null);
		if (bEBehaviorDoor == null)
		{
			return null;
		}
		Vec3i adjacentOffset = bEBehaviorDoor.getAdjacentOffset(-1, -1);
		if (offset.X == adjacentOffset.X && offset.Z == adjacentOffset.Z)
		{
			return null;
		}
		if (bEBehaviorDoor.Opened)
		{
			Vec3i adjacentOffset2 = bEBehaviorDoor.getAdjacentOffset(-1);
			if (offset.X == adjacentOffset2.X && offset.Z == adjacentOffset2.Z)
			{
				return null;
			}
		}
		else
		{
			Vec3i adjacentOffset3 = bEBehaviorDoor.getAdjacentOffset(0, -1);
			if (offset.X == adjacentOffset3.X && offset.Z == adjacentOffset3.Z)
			{
				return null;
			}
		}
		return bEBehaviorDoor.ColSelBoxes;
	}

	public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
	{
		handled = (EnumHandling)3;
		BlockEntity blockEntity = blockAccessor.GetBlockEntity(pos);
		return ((blockEntity == null) ? null : blockEntity.GetBehavior<BEBehaviorDoor>()?.ColSelBoxes) ?? null;
	}

	public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
	{
		handled = (EnumHandling)3;
		BlockEntity blockEntity = blockAccessor.GetBlockEntity(pos);
		return ((blockEntity == null) ? null : blockEntity.GetBehavior<BEBehaviorDoor>()?.ColSelBoxes) ?? null;
	}

	public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
	{
		handled = (EnumHandling)3;
		BlockEntity blockEntity = blockAccessor.GetBlockEntity(pos);
		return ((blockEntity == null) ? null : blockEntity.GetBehavior<BEBehaviorDoor>()?.ColSelBoxes) ?? null;
	}

	public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing, ref EnumHandling handled)
	{
		return ((StrongBlockBehavior)this).GetParticleBreakBox(blockAccess, pos, facing, ref handled);
	}

	public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData, ref EnumHandling handled)
	{
		BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
		BEBehaviorDoor bEBehaviorDoor = ((blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorDoor>() : null);
		if (bEBehaviorDoor.Opened)
		{
			float num = (bEBehaviorDoor.InvertHandles ? 90 : (-90));
			decalModelData = decalModelData.Rotate(0f, num * ((float)Math.PI / 180f), 0f);
			if (!bEBehaviorDoor.InvertHandles)
			{
				decalModelData = decalModelData.Scale(1f, 1f, -1f);
			}
		}
		((StrongBlockBehavior)this).GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData, ref handled);
	}

	public override void GetHeldItemName(StringBuilder sb, ItemStack itemStack)
	{
		doorNameWithMaterial(sb);
	}

	public override void GetPlacedBlockName(StringBuilder sb, IWorldAccessor world, BlockPos pos)
	{
	}

	private void doorNameWithMaterial(StringBuilder sb)
	{
		if (((RegistryObject)((BlockBehavior)this).block).Variant.ContainsKey("wood"))
		{
			string text = sb.ToString();
			sb.Clear();
			sb.Append(Lang.Get("doorname-with-material", new object[2]
			{
				text,
				Lang.Get("material-" + ((RegistryObject)((BlockBehavior)this).block).Variant["wood"], Array.Empty<object>())
			}));
		}
	}

	public override float GetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos, ref EnumHandling handled)
	{
		handled = (EnumHandling)2;
		BEBehaviorDoor bEBehavior = ((BlockBehavior)this).block.GetBEBehavior<BEBehaviorDoor>(pos);
		if (bEBehavior == null)
		{
			return 0f;
		}
		if (!bEBehavior.IsSideSolid(face))
		{
			return 0f;
		}
		if (liquidBarrierHeight != null)
		{
			return liquidBarrierHeight[height - 1][0];
		}
		if (!airtight)
		{
			return 0f;
		}
		return 1f;
	}

	public float MBGetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos, Vec3i offset)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Invalid comparison between Unknown and I4
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if (offset.X != 0 && offset.Z != 0)
		{
			return 0f;
		}
		BEBehaviorDoor bEBehavior = ((BlockBehavior)this).block.GetBEBehavior<BEBehaviorDoor>(pos.AddCopy(offset.X, offset.Y, offset.Z));
		if (bEBehavior == null)
		{
			return 0f;
		}
		EnumAxis axis = (bEBehavior.Opened ? bEBehavior.facingWhenOpened : bEBehavior.facingWhenClosed).Axis;
		if (!bEBehavior.IsSideSolid(face) || ((int)axis == 0 && offset.X != 0) || ((int)axis == 2 && offset.Z != 0))
		{
			return 0f;
		}
		if (liquidBarrierHeight != null)
		{
			int num = (((int)axis == 0) ? Math.Abs(offset.Z) : Math.Abs(offset.X));
			return liquidBarrierHeight[height + offset.Y - 1][num];
		}
		if (!airtight)
		{
			return 0f;
		}
		return 1f;
	}

	public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type, ref EnumHandling handled)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		handled = (EnumHandling)2;
		BEBehaviorDoor bEBehavior = ((BlockBehavior)this).block.GetBEBehavior<BEBehaviorDoor>(pos);
		if (bEBehavior == null)
		{
			return 0;
		}
		if ((int)type == 1)
		{
			if (!bEBehavior.IsSideSolid(facing))
			{
				return 0;
			}
			return 3;
		}
		if (!airtight)
		{
			return 0;
		}
		if (api.World.Config.GetBool("openDoorsNotSolid", false))
		{
			if (!bEBehavior.IsSideSolid(facing))
			{
				return 0;
			}
			return getInsulation(pos);
		}
		if (!bEBehavior.IsSideSolid(facing) && !bEBehavior.IsSideSolid(facing.Opposite))
		{
			return 3;
		}
		return getInsulation(pos);
	}

	public int MBGetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type, Vec3i offset)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Invalid comparison between Unknown and I4
		BlockPos val = pos.AddCopy(offset.X, offset.Y, offset.Z);
		BEBehaviorDoor bEBehavior = ((BlockBehavior)this).block.GetBEBehavior<BEBehaviorDoor>(val);
		if (bEBehavior == null)
		{
			return 0;
		}
		if ((int)type == 1)
		{
			EnumAxis axis = (bEBehavior.Opened ? bEBehavior.facingWhenOpened : bEBehavior.facingWhenClosed).Axis;
			if (((int)axis == 0 && offset.X != 0) || ((int)axis == 2 && offset.Z != 0))
			{
				return 0;
			}
			if (!bEBehavior.IsSideSolid(facing))
			{
				return 0;
			}
			return 3;
		}
		if (!airtight)
		{
			return 0;
		}
		if (api.World.Config.GetBool("openDoorsNotSolid", false))
		{
			EnumAxis axis2 = (bEBehavior.Opened ? bEBehavior.facingWhenOpened : bEBehavior.facingWhenClosed).Axis;
			if (((int)axis2 == 0 && offset.X != 0) || ((int)axis2 == 2 && offset.Z != 0))
			{
				return 0;
			}
			if (!bEBehavior.IsSideSolid(facing))
			{
				return 0;
			}
			return getInsulation(val);
		}
		if (!bEBehavior.IsSideSolid(facing) && !bEBehavior.IsSideSolid(facing.Opposite))
		{
			return 3;
		}
		return getInsulation(val);
	}

	private int getInsulation(BlockPos pos)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Invalid comparison between Unknown and I4
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		EnumBlockMaterial blockMaterial = ((BlockBehavior)this).block.GetBlockMaterial(api.World.BlockAccessor, pos, (ItemStack)null);
		if ((int)blockMaterial == 7 || (int)blockMaterial == 6 || (int)blockMaterial == 1 || (int)blockMaterial == 15)
		{
			return -1;
		}
		return 1;
	}

	public bool MBCanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea, Vec3i offsetInv)
	{
		return false;
	}

	public JsonObject MBGetAttributes(IBlockAccessor blockAccessor, BlockPos pos)
	{
		return null;
	}

	public override bool SideIsSolid(BlockPos pos, int faceIndex, ref EnumHandling handled)
	{
		handled = (EnumHandling)3;
		return ((BlockBehavior)this).block.GetBEBehavior<BEBehaviorDoor>(pos)?.IsSideSolid(BlockFacing.ALLFACES[faceIndex]) ?? false;
	}

	public bool MBSideIsSolid(BlockPos pos, int faceIndex, Vec3i offset)
	{
		BlockPos val = pos.AddCopy(offset.X, offset.Y, offset.Z);
		return ((BlockBehavior)this).block.GetBEBehavior<BEBehaviorDoor>(val)?.IsSideSolid(BlockFacing.ALLFACES[faceIndex]) ?? false;
	}

	public override bool SideIsSolid(IBlockAccessor blockAccess, BlockPos pos, int faceIndex, ref EnumHandling handled)
	{
		handled = (EnumHandling)3;
		return ((BlockBehavior)this).block.GetBEBehavior<BEBehaviorDoor>(pos)?.IsSideSolid(BlockFacing.ALLFACES[faceIndex]) ?? false;
	}

	public bool MBSideIsSolid(IBlockAccessor blockAccess, BlockPos pos, int faceIndex, Vec3i offset)
	{
		BlockPos val = pos.AddCopy(offset.X, offset.Y, offset.Z);
		return ((BlockBehavior)this).block.GetBEBehavior<BEBehaviorDoor>(val)?.IsSideSolid(BlockFacing.ALLFACES[faceIndex]) ?? false;
	}
}
