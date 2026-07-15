using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BlockFirepit : Block, IIgnitable, ISmokeEmitter
{
	public bool IsExtinct;

	protected AdvancedParticleProperties[] ringParticles;

	protected Vec3f[] basePos;

	protected WorldInteraction[] interactions;

	private ICoreClientAPI capi;

	public int Stage => ((RegistryObject)this).LastCodePart(0) switch
	{
		"construct1" => 1, 
		"construct2" => 2, 
		"construct3" => 3, 
		"construct4" => 4, 
		_ => 5, 
	};

	public string NextStageCodePart => ((RegistryObject)this).LastCodePart(0) switch
	{
		"construct1" => "construct2", 
		"construct2" => "construct3", 
		"construct3" => "construct4", 
		"construct4" => "cold", 
		_ => "cold", 
	};

	public override void OnLoaded(ICoreAPI api)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Invalid comparison between Unknown and I4
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Expected O, but got Unknown
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Expected O, but got Unknown
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Expected O, but got Unknown
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Expected O, but got Unknown
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Expected O, but got Unknown
		((Block)this).OnLoaded(api);
		ICoreAPI obj = api;
		capi = (ICoreClientAPI)(object)((obj is ICoreClientAPI) ? obj : null);
		ICoreClientAPI obj2 = capi;
		if (obj2 != null)
		{
			((IEventAPI)obj2.Event).RegisterEventBusListener(new EventBusListenerDelegate(OnGetTransform), 0.5, "ongettransform");
		}
		IsExtinct = ((RegistryObject)this).LastCodePart(0) != "lit";
		if (!IsExtinct && (int)api.Side == 2)
		{
			ringParticles = (AdvancedParticleProperties[])(object)new AdvancedParticleProperties[((CollectibleObject)this).ParticleProperties.Length * 4];
			basePos = (Vec3f[])(object)new Vec3f[ringParticles.Length];
			Cuboidf[] array = (Cuboidf[])(object)new Cuboidf[4]
			{
				new Cuboidf(0.125f, 0f, 0.125f, 0.3125f, 0.5f, 0.875f),
				new Cuboidf(0.7125f, 0f, 0.125f, 0.875f, 0.5f, 0.875f),
				new Cuboidf(0.125f, 0f, 0.125f, 0.875f, 0.5f, 0.3125f),
				new Cuboidf(0.125f, 0f, 0.7125f, 0.875f, 0.5f, 0.875f)
			};
			for (int i = 0; i < ((CollectibleObject)this).ParticleProperties.Length; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					AdvancedParticleProperties val = ((CollectibleObject)this).ParticleProperties[i].Clone();
					Cuboidf val2 = array[j];
					basePos[i * 4 + j] = new Vec3f(0f, 0f, 0f);
					val.PosOffset[0].avg = val2.MidX;
					val.PosOffset[0].var = val2.Width / 2f;
					val.PosOffset[1].avg = 0.1f;
					val.PosOffset[1].var = 0.05f;
					val.PosOffset[2].avg = val2.MidZ;
					val.PosOffset[2].var = val2.Length / 2f;
					NatFloat quantity = val.Quantity;
					quantity.avg /= 4f;
					NatFloat quantity2 = val.Quantity;
					quantity2.var /= 4f;
					ringParticles[i * 4 + j] = val;
				}
			}
		}
		InteractionMatcherDelegate val4 = default(InteractionMatcherDelegate);
		InteractionStacksDelegate val8 = default(InteractionStacksDelegate);
		interactions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "firepitInteractions-" + Stage, (CreateCachableObjectDelegate<WorldInteraction[]>)delegate
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Expected O, but got Unknown
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Expected O, but got Unknown
			//IL_004b: Expected O, but got Unknown
			//IL_009d: Expected O, but got Unknown
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Expected O, but got Unknown
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Expected O, but got Unknown
			//IL_0096: Expected O, but got Unknown
			List<ItemStack> list = BlockBehaviorCanIgnite.CanIgniteStacks(api, withFirestarter: true);
			WorldInteraction[] array2 = new WorldInteraction[3];
			WorldInteraction val3 = new WorldInteraction
			{
				ActionLangCode = "blockhelp-firepit-open",
				MouseButton = (EnumMouseButton)2
			};
			InteractionMatcherDelegate obj3 = val4;
			if (obj3 == null)
			{
				InteractionMatcherDelegate val5 = (WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection) => Stage == 5;
				InteractionMatcherDelegate val6 = val5;
				val4 = val5;
				obj3 = val6;
			}
			val3.ShouldApply = obj3;
			array2[0] = val3;
			WorldInteraction val7 = new WorldInteraction
			{
				ActionLangCode = "blockhelp-firepit-ignite",
				MouseButton = (EnumMouseButton)2,
				Itemstacks = list.ToArray()
			};
			InteractionStacksDelegate obj4 = val8;
			if (obj4 == null)
			{
				InteractionStacksDelegate val9 = delegate(WorldInteraction wi, BlockSelection bs, EntitySelection es)
				{
					BlockEntityFirepit blockEntityFirepit = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityFirepit;
					return (blockEntityFirepit?.fuelSlot != null && !blockEntityFirepit.fuelSlot.Empty && !blockEntityFirepit.IsBurning) ? wi.Itemstacks : null;
				};
				InteractionStacksDelegate val10 = val9;
				val8 = val9;
				obj4 = val10;
			}
			val7.GetMatchingStacks = obj4;
			array2[1] = val7;
			array2[2] = new WorldInteraction
			{
				ActionLangCode = "blockhelp-firepit-refuel",
				MouseButton = (EnumMouseButton)2,
				HotKeyCode = "shift"
			};
			return (WorldInteraction[])(object)array2;
		});
	}

	private void OnGetTransform(string eventName, ref EnumHandling handling, IAttribute data)
	{
		TreeAttribute val = (TreeAttribute)(object)((data is TreeAttribute) ? data : null);
		if (!(val.GetString("target", (string)null) != "infirepitTransform"))
		{
			InFirePitProps renderProps = BlockEntityFirepit.GetRenderProps(((IPlayer)capi.World.Player).InventoryManager.ActiveHotbarSlot.Itemstack);
			if (renderProps?.Transform != null)
			{
				handling = (EnumHandling)2;
				val.SetBool("preventDefault", true);
				renderProps.Transform.ToTreeAttribute(val);
			}
		}
	}

	public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		if (world.Rand.NextDouble() < 0.05)
		{
			BlockEntityFirepit blockEntity = ((Block)this).GetBlockEntity<BlockEntityFirepit>(pos);
			if (blockEntity != null && blockEntity.IsBurning)
			{
				entity.ReceiveDamage(new DamageSource
				{
					Source = (EnumDamageSource)0,
					SourceBlock = (Block)(object)this,
					Type = (EnumDamageType)1,
					SourcePos = pos.ToVec3d()
				}, 0.5f);
			}
		}
		((Block)this).OnEntityInside(world, entity, pos);
	}

	EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
	{
		if ((((CollectibleObject)this).api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFirepit).IsBurning)
		{
			if (!(secondsIgniting > 2f))
			{
				return EnumIgniteState.Ignitable;
			}
			return EnumIgniteState.IgniteNow;
		}
		return EnumIgniteState.NotIgnitable;
	}

	public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
	{
		if (!(((CollectibleObject)this).api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityFirepit blockEntityFirepit))
		{
			return EnumIgniteState.NotIgnitable;
		}
		return blockEntityFirepit.GetIgnitableState(secondsIgniting);
	}

	public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
	{
		if (((CollectibleObject)this).api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityFirepit { canIgniteFuel: false } blockEntityFirepit)
		{
			blockEntityFirepit.canIgniteFuel = true;
			blockEntityFirepit.extinguishedTotalHours = ((CollectibleObject)this).api.World.Calendar.TotalHours;
		}
		handling = (EnumHandling)2;
	}

	public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
	{
		bool flag = default(bool);
		bool result = ((Block)this).ShouldReceiveClientParticleTicks(world, player, pos, ref flag);
		isWindAffected = true;
		return result;
	}

	public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
	{
		if (IsExtinct)
		{
			((Block)this).OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
		}
		else if (manager.BlockAccess.GetBlockEntity(pos) is BlockEntityFirepit { CurrentModel: EnumFirepitModel.Wide })
		{
			for (int i = 0; i < ringParticles.Length; i++)
			{
				AdvancedParticleProperties val = ringParticles[i];
				val.WindAffectednesAtPos = windAffectednessAtPos;
				val.basePos.X = (float)pos.X + basePos[i].X;
				val.basePos.Y = (float)pos.InternalY + basePos[i].Y;
				val.basePos.Z = (float)pos.Z + basePos[i].Z;
				manager.Spawn((IParticlePropertiesProvider)(object)val);
			}
		}
		else
		{
			((Block)this).OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
		}
	}

	public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
	{
		//IL_03e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ea: Invalid comparison between Unknown and I4
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Expected O, but got Unknown
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Expected O, but got Unknown
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Expected O, but got Unknown
		if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, (EnumBlockAccessFlags)2))
		{
			return false;
		}
		int stage = Stage;
		ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
		ItemStack val = ((activeHotbarSlot != null) ? activeHotbarSlot.Itemstack : null);
		if (stage == 5)
		{
			BlockEntityFirepit blockEntityFirepit = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFirepit;
			if (blockEntityFirepit != null && ((val != null) ? val.Block : null) != null && ((CollectibleObject)val.Block).HasBehavior<BlockBehaviorCanIgnite>(false) && blockEntityFirepit.GetIgnitableState(0f) == EnumIgniteState.Ignitable)
			{
				return false;
			}
			if (blockEntityFirepit != null && val != null)
			{
				bool flag = false;
				if (((EntityAgent)byPlayer.Entity).Controls.ShiftKey)
				{
					CombustibleProperties combustibleProperties = val.Collectible.GetCombustibleProperties(world, val, (BlockPos)null);
					if (combustibleProperties != null && combustibleProperties.MeltingPoint > 0)
					{
						ItemStackMoveOperation val2 = new ItemStackMoveOperation(world, (EnumMouseButton)0, (EnumModifierKey)0, (EnumMergePriority)1, 1);
						byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(blockEntityFirepit.inputSlot, ref val2);
						if (val2.MovedQuantity > 0)
						{
							flag = true;
						}
					}
					if (combustibleProperties != null && combustibleProperties.BurnTemperature > 0)
					{
						ItemStackMoveOperation val3 = new ItemStackMoveOperation(world, (EnumMouseButton)0, (EnumModifierKey)0, (EnumMergePriority)1, 1);
						byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(blockEntityFirepit.fuelSlot, ref val3);
						if (val3.MovedQuantity > 0)
						{
							flag = true;
						}
					}
				}
				JsonObject attributes = val.Collectible.Attributes;
				if (attributes != null && attributes.IsTrue("mealContainer") && !flag)
				{
					ItemSlot val4 = null;
					ItemStack inputStack = blockEntityFirepit.inputStack;
					if (((inputStack != null) ? inputStack.Collectible : null) is BlockCookedContainer)
					{
						val4 = blockEntityFirepit.inputSlot;
					}
					ItemStack outputStack = blockEntityFirepit.outputStack;
					if (((outputStack != null) ? outputStack.Collectible : null) is BlockCookedContainer)
					{
						val4 = blockEntityFirepit.outputSlot;
					}
					if (val4 != null)
					{
						BlockCookedContainer blockCookedContainer = val4.Itemstack.Collectible as BlockCookedContainer;
						ItemSlot activeHotbarSlot2 = byPlayer.InventoryManager.ActiveHotbarSlot;
						if (byPlayer.InventoryManager.ActiveHotbarSlot.StackSize > 1)
						{
							activeHotbarSlot2 = (ItemSlot)new DummySlot(activeHotbarSlot2.TakeOut(1));
							byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
							blockCookedContainer.ServeIntoStack(activeHotbarSlot2, val4, world);
							if (!byPlayer.InventoryManager.TryGiveItemstack(activeHotbarSlot2.Itemstack, true))
							{
								world.SpawnItemEntity(activeHotbarSlot2.Itemstack, ((Entity)byPlayer.Entity).Pos.XYZ, (Vec3d)null);
							}
						}
						else
						{
							blockCookedContainer.ServeIntoStack(activeHotbarSlot2, val4, world);
						}
					}
					else if (!blockEntityFirepit.inputSlot.Empty || byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(((CollectibleObject)this).api.World, blockEntityFirepit.inputSlot, 1) == 0)
					{
						blockEntityFirepit.OnPlayerRightClick(byPlayer, blockSel);
					}
					flag = true;
				}
				CollectibleObject val5 = ((val != null) ? val.Collectible : null);
				bool flag2 = ((val5 is BlockSmeltingContainer || val5 is BlockSmeltedContainer) ? true : false);
				if (flag2 && !flag && byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(((CollectibleObject)this).api.World, blockEntityFirepit.inputSlot, 1) > 0)
				{
					flag = true;
				}
				if (flag)
				{
					IPlayer obj = ((byPlayer is IClientPlayer) ? byPlayer : null);
					if (obj != null)
					{
						((IClientPlayer)obj).TriggerFpAnimation((EnumHandInteract)2);
					}
					JsonObject itemAttributes = val.ItemAttributes;
					AssetLocation val6 = ((itemAttributes != null && itemAttributes["placeSound"].Exists) ? AssetLocation.Create(val.ItemAttributes["placeSound"].AsString((string)null), ((RegistryObject)val.Collectible).Code.Domain) : null);
					if (val6 != (AssetLocation)null)
					{
						((CollectibleObject)this).api.World.PlaySoundAt(val6.WithPathPrefixOnce("sounds/"), (double)blockSel.Position.X, (double)blockSel.Position.InternalY, (double)blockSel.Position.Z, byPlayer, 0.88f + (float)((CollectibleObject)this).api.World.Rand.NextDouble() * 0.24f, 16f, 1f);
					}
					return true;
				}
			}
			return ((Block)this).OnBlockInteractStart(world, byPlayer, blockSel);
		}
		if (val != null && TryConstruct(world, blockSel.Position, val.Collectible, byPlayer))
		{
			if (byPlayer != null && (int)byPlayer.WorldData.CurrentGameMode != 2)
			{
				byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
			}
			return true;
		}
		return false;
	}

	public bool TryConstruct(IWorldAccessor world, BlockPos pos, CollectibleObject obj, IPlayer player)
	{
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Expected O, but got Unknown
		int stage = Stage;
		JsonObject attributes = obj.Attributes;
		if (attributes == null || !attributes.IsTrue("firepitConstructable"))
		{
			return false;
		}
		switch (stage)
		{
		case 5:
			return false;
		case 4:
		{
			if (!IsFirewoodPile(world, pos.DownCopy(1)))
			{
				break;
			}
			Block block = world.GetBlock(new AssetLocation("charcoalpit"));
			if (block != null)
			{
				world.BlockAccessor.SetBlock(block.BlockId, pos);
				(world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCharcoalPit)?.Init(player);
				IPlayer obj2 = ((player is IClientPlayer) ? player : null);
				if (obj2 != null)
				{
					((IClientPlayer)obj2).TriggerFpAnimation((EnumHandInteract)2);
				}
				return true;
			}
			break;
		}
		}
		Block block2 = world.GetBlock(((RegistryObject)this).CodeWithParts(NextStageCodePart));
		world.BlockAccessor.ExchangeBlock(block2.BlockId, pos);
		world.BlockAccessor.MarkBlockDirty(pos, (IPlayer)null);
		if (block2.Sounds != null)
		{
			world.PlaySoundAt(block2.Sounds.Place, pos, -0.5, player, 1f);
		}
		if (stage == 4)
		{
			BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
			if (blockEntity is BlockEntityFirepit)
			{
				((InventoryBase)((BlockEntityFirepit)(object)blockEntity).inventory)[0].Itemstack = new ItemStack(obj, 4);
			}
		}
		IPlayer obj3 = ((player is IClientPlayer) ? player : null);
		if (obj3 != null)
		{
			((IClientPlayer)obj3).TriggerFpAnimation((EnumHandInteract)2);
		}
		return true;
	}

	public static bool IsFirewoodPile(IWorldAccessor world, BlockPos pos)
	{
		BlockEntityGroundStorage blockEntity = world.BlockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos);
		if (blockEntity != null)
		{
			ItemSlot obj = blockEntity.Inventory[0];
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				ItemStack itemstack = obj.Itemstack;
				obj2 = ((itemstack != null) ? itemstack.Collectible : null);
			}
			return obj2 is ItemFirewood;
		}
		return false;
	}

	public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
	{
		return ArrayExtensions.Append<WorldInteraction>(interactions, ((Block)this).GetPlacedBlockInteractionHelp(world, selection, forPlayer));
	}

	public override float GetTraversalCost(BlockPos pos, EnumAICreatureType creatureType)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if ((int)creatureType == 1 || (int)creatureType == 2)
		{
			BlockEntityFirepit blockEntity = ((Block)this).GetBlockEntity<BlockEntityFirepit>(pos);
			if (blockEntity == null || !blockEntity.IsBurning)
			{
				return 1f;
			}
			return 10000f;
		}
		return ((Block)this).GetTraversalCost(pos, creatureType);
	}

	public bool EmitsSmoke(BlockPos pos)
	{
		return (((CollectibleObject)this).api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFirepit)?.IsBurning ?? false;
	}
}
