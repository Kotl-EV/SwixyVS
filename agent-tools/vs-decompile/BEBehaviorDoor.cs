using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

[DocumentAsJson]
[AddDocumentationProperty("breakOnTriggerChance", "Affects the chance of door breaking and dropping itself upon using it", "System.Single", "Optional", "0", true)]
[AddDocumentationProperty("easingSpeed", "Affects animation speed of door opening or closing", "System.Single", "Optional", "10", true)]
public class BEBehaviorDoor : BEBehaviorAnimatable, IInteractable, IRotatable
{
	public float RotateYRad;

	protected bool opened;

	protected bool invertHandles;

	protected MeshData mesh;

	protected Cuboidf[] boxesClosed;

	protected Cuboidf[] boxesOpened;

	protected Vec3i leftDoorOffset;

	protected Vec3i rightDoorOffset;

	public BlockBehaviorDoor doorBh;

	public string StoryLockedCode;

	public BlockFacing facingWhenClosed => BlockFacing.HorizontalFromYaw(RotateYRad);

	public BlockFacing facingWhenOpened
	{
		get
		{
			if (!invertHandles)
			{
				return facingWhenClosed.GetCW();
			}
			return facingWhenClosed.GetCCW();
		}
	}

	public BEBehaviorDoor LeftDoor
	{
		get
		{
			if (leftDoorOffset != (Vec3i)null)
			{
				BEBehaviorDoor doorAt = BlockBehaviorDoor.getDoorAt(((BlockEntityBehavior)this).Api.World, ((BlockEntityBehavior)this).Pos.AddCopy(leftDoorOffset));
				if (doorAt == null)
				{
					leftDoorOffset = null;
				}
				return doorAt;
			}
			return null;
		}
		protected set
		{
			leftDoorOffset = ((value == null) ? null : ((BlockEntityBehavior)value).Pos.SubCopy(((BlockEntityBehavior)this).Pos).ToVec3i());
		}
	}

	public BEBehaviorDoor RightDoor
	{
		get
		{
			if (rightDoorOffset != (Vec3i)null)
			{
				BEBehaviorDoor doorAt = BlockBehaviorDoor.getDoorAt(((BlockEntityBehavior)this).Api.World, ((BlockEntityBehavior)this).Pos.AddCopy(rightDoorOffset));
				if (doorAt == null)
				{
					rightDoorOffset = null;
				}
				return doorAt;
			}
			return null;
		}
		protected set
		{
			rightDoorOffset = ((value == null) ? null : ((BlockEntityBehavior)value).Pos.SubCopy(((BlockEntityBehavior)this).Pos).ToVec3i());
		}
	}

	public Cuboidf[] ColSelBoxes
	{
		get
		{
			if (!opened)
			{
				return boxesClosed;
			}
			return boxesOpened;
		}
	}

	public bool Opened => opened;

	public bool InvertHandles => invertHandles;

	public BEBehaviorDoor(BlockEntity blockentity)
		: base(blockentity)
	{
		boxesClosed = ((BlockEntityBehavior)this).Block.CollisionBoxes;
		doorBh = ((CollectibleObject)((BlockEntityBehavior)this).Block).GetBehavior<BlockBehaviorDoor>();
	}

	public override void Initialize(ICoreAPI api, JsonObject properties)
	{
		base.Initialize(api, properties);
		SetupRotationsAndColSelBoxes(initalSetup: false);
		if (opened && animUtil != null && !((AnimationUtil)animUtil).activeAnimationsByAnimCode.ContainsKey("opened"))
		{
			ToggleDoorWing(opened: true);
		}
	}

	public Vec3i getAdjacentOffset(int right, int back = 0, int up = 0)
	{
		return getAdjacentOffset(right, back, up, RotateYRad, invertHandles);
	}

	public static Vec3i getAdjacentOffset(int right, int back, int up, float rotateYRad, bool invertHandles)
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		if (invertHandles)
		{
			right = -right;
		}
		return new Vec3i(right * (int)Math.Round(Math.Sin(rotateYRad + (float)Math.PI / 2f)) - back * (int)Math.Round(Math.Sin(rotateYRad)), up, right * (int)Math.Round(Math.Cos(rotateYRad + (float)Math.PI / 2f)) - back * (int)Math.Round(Math.Cos(rotateYRad)));
	}

	internal void SetupRotationsAndColSelBoxes(bool initalSetup)
	{
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0335: Invalid comparison between Unknown and I4
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Invalid comparison between Unknown and I4
		if (initalSetup)
		{
			if (BlockBehaviorDoor.HasCombinableLeftDoor(((BlockEntityBehavior)this).Api.World, RotateYRad, ((BlockEntityBehavior)this).Pos, doorBh.width, out var leftDoor, out var leftOffset) && leftDoor.LeftDoor == null && leftDoor.RightDoor == null && leftDoor.facingWhenClosed == facingWhenClosed)
			{
				if (leftDoor.invertHandles)
				{
					if (leftDoor.doorBh.width > 1)
					{
						((BlockEntityBehavior)this).Api.World.BlockAccessor.SetBlock(0, ((BlockEntityBehavior)leftDoor).Pos);
						BlockPos val = ((BlockEntityBehavior)this).Pos.AddCopy(facingWhenClosed.GetCW(), leftDoor.doorBh.width + doorBh.width - 1);
						((BlockEntityBehavior)this).Api.World.BlockAccessor.SetBlock(((CollectibleObject)((BlockEntityBehavior)leftDoor).Block).Id, val);
						leftDoor = ((BlockEntityBehavior)this).Block.GetBEBehavior<BEBehaviorDoor>(val);
						leftDoor.RotateYRad = RotateYRad;
						leftDoor.doorBh.placeMultiblockParts(((BlockEntityBehavior)this).Api.World, val);
						LeftDoor = leftDoor;
						LeftDoor.RightDoor = this;
						LeftDoor.SetupRotationsAndColSelBoxes(initalSetup: true);
					}
					else
					{
						leftDoor.invertHandles = false;
						LeftDoor = leftDoor;
						LeftDoor.RightDoor = this;
						((BlockEntityBehavior)LeftDoor).Blockentity.MarkDirty(true, (IPlayer)null);
						LeftDoor.SetupRotationsAndColSelBoxes(initalSetup: false);
					}
				}
				else
				{
					LeftDoor = leftDoor;
					LeftDoor.RightDoor = this;
				}
				invertHandles = true;
				((BlockEntityBehavior)this).Blockentity.MarkDirty(true, (IPlayer)null);
			}
			if (BlockBehaviorDoor.HasCombinableRightDoor(((BlockEntityBehavior)this).Api.World, RotateYRad, ((BlockEntityBehavior)this).Pos, doorBh.width, out leftDoor, out leftOffset) && leftDoor.LeftDoor == null && leftDoor.RightDoor == null && leftDoor.facingWhenClosed == facingWhenClosed && (int)((BlockEntityBehavior)this).Api.Side == 1)
			{
				if (!leftDoor.invertHandles)
				{
					if (leftDoor.doorBh.width > 1)
					{
						((BlockEntityBehavior)this).Api.World.BlockAccessor.SetBlock(0, ((BlockEntityBehavior)leftDoor).Pos);
						BlockPos val2 = ((BlockEntityBehavior)this).Pos.AddCopy(facingWhenClosed.GetCCW(), leftDoor.doorBh.width + doorBh.width - 1);
						((BlockEntityBehavior)this).Api.World.BlockAccessor.SetBlock(((CollectibleObject)((BlockEntityBehavior)leftDoor).Block).Id, val2);
						leftDoor = ((BlockEntityBehavior)this).Block.GetBEBehavior<BEBehaviorDoor>(val2);
						leftDoor.RotateYRad = RotateYRad;
						leftDoor.invertHandles = true;
						leftDoor.doorBh.placeMultiblockParts(((BlockEntityBehavior)this).Api.World, val2);
						RightDoor = leftDoor;
						RightDoor.LeftDoor = this;
						leftDoor.SetupRotationsAndColSelBoxes(initalSetup: true);
					}
					else
					{
						leftDoor.invertHandles = true;
						RightDoor = leftDoor;
						RightDoor.LeftDoor = this;
						((BlockEntityBehavior)RightDoor).Blockentity.MarkDirty(true, (IPlayer)null);
						RightDoor.SetupRotationsAndColSelBoxes(initalSetup: false);
					}
				}
				else
				{
					RightDoor = leftDoor;
					RightDoor.LeftDoor = this;
				}
			}
		}
		if ((int)((BlockEntityBehavior)this).Api.Side == 2)
		{
			if (doorBh.animatableOrigMesh == null)
			{
				string text = ((object)((BlockEntityBehavior)this).Block.Shape).ToString();
				doorBh.animatableOrigMesh = animUtil.CreateMesh(text, null, out var resultingShape, null);
				doorBh.animatableShape = resultingShape;
				doorBh.animatableDictKey = text;
			}
			if (doorBh.animatableOrigMesh != null)
			{
				((AnimationUtil)animUtil).InitializeAnimator(doorBh.animatableDictKey, doorBh.animatableOrigMesh, doorBh.animatableShape, (Vec3f)null, (EnumRenderStage)1);
			}
		}
		UpdateHitBoxes();
	}

	protected virtual void UpdateMeshAndAnimations()
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Expected O, but got Unknown
		mesh = doorBh.animatableOrigMesh.Clone();
		if (RotateYRad != 0f)
		{
			float num = (invertHandles ? (0f - RotateYRad) : RotateYRad);
			mesh = mesh.Rotate(0f, num, 0f);
			((AnimationUtil)animUtil).renderer.rotationDeg.Y = num * (180f / (float)Math.PI);
		}
		if (invertHandles)
		{
			Matrixf val = new Matrixf();
			val.Translate(0.5f, 0.5f, 0.5f).Scale(-1f, 1f, 1f).Translate(-0.5f, -0.5f, -0.5f);
			mesh.MatrixTransform(val.Values);
			((AnimationUtil)animUtil).renderer.backfaceCulling = false;
			((AnimationUtil)animUtil).renderer.ScaleX = -1f;
		}
	}

	protected virtual void UpdateHitBoxes()
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Expected O, but got Unknown
		if (RotateYRad != 0f)
		{
			boxesClosed = ((BlockEntityBehavior)this).Block.CollisionBoxes;
			Cuboidf[] array = (Cuboidf[])(object)new Cuboidf[boxesClosed.Length];
			for (int i = 0; i < boxesClosed.Length; i++)
			{
				array[i] = boxesClosed[i].RotatedCopy(0f, RotateYRad * (180f / (float)Math.PI), 0f, new Vec3d(0.5, 0.5, 0.5));
			}
			boxesClosed = array;
		}
		Cuboidf[] array2 = (Cuboidf[])(object)new Cuboidf[boxesClosed.Length];
		for (int j = 0; j < boxesClosed.Length; j++)
		{
			array2[j] = boxesClosed[j].RotatedCopy(0f, (float)(invertHandles ? 90 : (-90)), 0f, new Vec3d(0.5, 0.5, 0.5));
		}
		boxesOpened = array2;
	}

	public virtual void OnBlockPlaced(ItemStack byItemStack, IPlayer byPlayer, BlockSelection blockSel)
	{
		if (byItemStack != null)
		{
			RotateYRad = getRotateYRad(byPlayer, blockSel);
			SetupRotationsAndColSelBoxes(initalSetup: true);
		}
	}

	public static float getRotateYRad(IPlayer byPlayer, BlockSelection blockSel)
	{
		BlockPos val = (blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position);
		double y = ((Entity)byPlayer.Entity).Pos.X - ((double)val.X + blockSel.HitPosition.X);
		double x = (double)(float)((Entity)byPlayer.Entity).Pos.Z - ((double)val.Z + blockSel.HitPosition.Z);
		float num = (float)Math.Atan2(y, x);
		float num2 = (float)Math.PI / 2f;
		return (float)(int)Math.Round(num / num2) * num2;
	}

	public bool IsSideSolid(BlockFacing facing)
	{
		if (opened || facing != facingWhenClosed)
		{
			if (opened)
			{
				return facing == facingWhenOpened;
			}
			return false;
		}
		return true;
	}

	public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Invalid comparison between Unknown and I4
		if (!doorBh.handopenable && (int)byPlayer.WorldData.CurrentGameMode != 2)
		{
			ICoreAPI api = ((BlockEntityBehavior)this).Api;
			((ICoreClientAPI)((api is ICoreClientAPI) ? api : null)).TriggerIngameError((object)this, "nothandopenable", Lang.Get("This door cannot be opened by hand.", Array.Empty<object>()));
			return true;
		}
		ToggleDoorState(byPlayer, !opened);
		handling = (EnumHandling)2;
		return true;
	}

	public void ToggleDoorState(IPlayer byPlayer, bool opened)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Invalid comparison between Unknown and I4
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Invalid comparison between Unknown and I4
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Expected O, but got Unknown
		float num = ((CollectibleObject)((BlockEntityBehavior)this).Block).Attributes["breakOnTriggerChance"].AsFloat(0f);
		if ((int)((BlockEntityBehavior)this).Api.Side == 1 && ((BlockEntityBehavior)this).Api.World.Rand.NextDouble() < (double)num && (int)byPlayer.WorldData.CurrentGameMode != 2)
		{
			((BlockEntityBehavior)this).Api.World.BlockAccessor.BreakBlock(((BlockEntityBehavior)this).Pos, byPlayer, 1f);
			((BlockEntityBehavior)this).Api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), ((BlockEntityBehavior)this).Pos, 0.0, (IPlayer)null, true, 32f, 1f);
			return;
		}
		this.opened = opened;
		ToggleDoorWing(opened);
		float num2 = (opened ? 1.1f : 0.9f);
		AssetLocation val = ((!opened) ? doorBh?.CloseSound : doorBh?.OpenSound);
		((BlockEntityBehavior)this).Api.World.PlaySoundAt(val, (double)((float)((BlockEntityBehavior)this).Pos.X + 0.5f), (double)((float)((BlockEntityBehavior)this).Pos.InternalY + 0.5f), (double)((float)((BlockEntityBehavior)this).Pos.Z + 0.5f), byPlayer, (EnumSoundType)0, num2, 32f, 1f);
		if (LeftDoor != null && invertHandles)
		{
			LeftDoor.ToggleDoorWing(opened);
			LeftDoor.UpdateNeighbors();
		}
		else if (RightDoor != null)
		{
			RightDoor.ToggleDoorWing(opened);
			RightDoor.UpdateNeighbors();
		}
		((BlockEntityBehavior)this).Blockentity.MarkDirty(true, (IPlayer)null);
		UpdateNeighbors();
	}

	private void UpdateNeighbors()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Invalid comparison between Unknown and I4
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		if ((int)((BlockEntityBehavior)this).Api.Side != 1)
		{
			return;
		}
		BlockPos val = new BlockPos(((BlockEntityBehavior)this).Pos.dimension);
		for (int i = 0; i < doorBh.height; i++)
		{
			val.Set(((BlockEntityBehavior)this).Pos).Add(0, i, 0);
			BlockFacing val2 = BlockFacing.ALLFACES[Opened ? facingWhenClosed.HorizontalAngleIndex : facingWhenOpened.HorizontalAngleIndex];
			for (int j = 0; j < doorBh.width; j++)
			{
				((BlockEntityBehavior)this).Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(val);
				val.Add(val2, 1);
			}
		}
	}

	private void ToggleDoorWing(bool opened)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Expected O, but got Unknown
		this.opened = opened;
		if (!opened)
		{
			((AnimationUtil)animUtil).StopAnimation("opened");
		}
		else
		{
			JsonObject attributes = ((CollectibleObject)((BlockEntityBehavior)this).Block).Attributes;
			float num = ((attributes != null) ? attributes["easingSpeed"].AsFloat(10f) : 10f);
			((AnimationUtil)animUtil).StartAnimation(new AnimationMetaData
			{
				Animation = "opened",
				Code = "opened",
				EaseInSpeed = num,
				EaseOutSpeed = num
			});
		}
		((BlockEntityBehavior)this).Blockentity.MarkDirty(false, (IPlayer)null);
	}

	public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
	{
		if (mesh == null)
		{
			UpdateMeshAndAnimations();
		}
		if (!base.OnTesselation(mesher, tessThreadTesselator))
		{
			mesher.AddMeshData(mesh, 1);
		}
		return true;
	}

	public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Invalid comparison between Unknown and I4
		((BlockEntityBehavior)this).FromTreeAttributes(tree, worldAccessForResolve);
		bool flag = opened;
		RotateYRad = tree.GetFloat("rotateYRad", 0f);
		opened = tree.GetBool("opened", false);
		invertHandles = tree.GetBool("invertHandles", false);
		leftDoorOffset = TreeAttributeUtil.GetVec3i(tree, "leftDoorPos", (Vec3i)null);
		rightDoorOffset = TreeAttributeUtil.GetVec3i(tree, "rightDoorPos", (Vec3i)null);
		StoryLockedCode = tree.GetString("storyLockedCode", (string)null);
		if (opened != flag && animUtil != null)
		{
			ToggleDoorWing(opened);
		}
		if (((BlockEntityBehavior)this).Api != null && (int)((BlockEntityBehavior)this).Api.Side == 2)
		{
			UpdateMeshAndAnimations();
			if (opened && !flag && animUtil != null && !((AnimationUtil)animUtil).activeAnimationsByAnimCode.ContainsKey("opened"))
			{
				ToggleDoorWing(opened: true);
			}
			UpdateHitBoxes();
			((BlockEntityBehavior)this).Api.World.BlockAccessor.MarkBlockDirty(((BlockEntityBehavior)this).Pos, (IPlayer)null);
		}
	}

	public override void ToTreeAttributes(ITreeAttribute tree)
	{
		((BlockEntityBehavior)this).ToTreeAttributes(tree);
		tree.SetFloat("rotateYRad", RotateYRad);
		tree.SetBool("opened", opened);
		tree.SetBool("invertHandles", invertHandles);
		if (StoryLockedCode != null)
		{
			tree.SetString("storyLockedCode", StoryLockedCode);
		}
		if (leftDoorOffset != (Vec3i)null)
		{
			TreeAttributeUtil.SetVec3i(tree, "leftDoorPos", leftDoorOffset);
		}
		if (rightDoorOffset != (Vec3i)null)
		{
			TreeAttributeUtil.SetVec3i(tree, "rightDoorPos", rightDoorOffset);
		}
	}

	public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
	{
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		ICoreAPI api = ((BlockEntityBehavior)this).Api;
		ICoreClientAPI val = (ICoreClientAPI)(object)((api is ICoreClientAPI) ? api : null);
		if (val != null && val.Settings.Bool["extendedDebugInfo"])
		{
			dsc.AppendLine(((object)facingWhenClosed)?.ToString() + (invertHandles ? "-inv " : " ") + (opened ? "open" : "closed"));
			dsc.AppendLine(doorBh.height + "x" + doorBh.width + ((leftDoorOffset != (Vec3i)null) ? (" leftdoor at:" + (object)leftDoorOffset) : " ") + ((rightDoorOffset != (Vec3i)null) ? (" rightdoor at:" + (object)rightDoorOffset) : " "));
			EnumHandling val2 = (EnumHandling)0;
			if (((BlockBehavior)doorBh).GetLiquidBarrierHeightOnSide(BlockFacing.NORTH, ((BlockEntityBehavior)this).Pos, ref val2) > 0f)
			{
				dsc.AppendLine("Barrier to liquid on side: North");
			}
			if (((BlockBehavior)doorBh).GetLiquidBarrierHeightOnSide(BlockFacing.EAST, ((BlockEntityBehavior)this).Pos, ref val2) > 0f)
			{
				dsc.AppendLine("Barrier to liquid on side: East");
			}
			if (((BlockBehavior)doorBh).GetLiquidBarrierHeightOnSide(BlockFacing.SOUTH, ((BlockEntityBehavior)this).Pos, ref val2) > 0f)
			{
				dsc.AppendLine("Barrier to liquid on side: South");
			}
			if (((BlockBehavior)doorBh).GetLiquidBarrierHeightOnSide(BlockFacing.WEST, ((BlockEntityBehavior)this).Pos, ref val2) > 0f)
			{
				dsc.AppendLine("Barrier to liquid on side: West");
			}
		}
	}

	public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
	{
		RotateYRad = tree.GetFloat("rotateYRad", 0f);
		RotateYRad = (RotateYRad - (float)degreeRotation * ((float)Math.PI / 180f)) % ((float)Math.PI * 2f);
		tree.SetFloat("rotateYRad", RotateYRad);
	}
}
