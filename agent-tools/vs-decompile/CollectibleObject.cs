using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Vintagestory.API.Common;

/// <summary>
/// Contains all properties shared by Blocks and Items
/// </summary>
public abstract class CollectibleObject : RegistryObject
{
	public static readonly Size3f DefaultSize = new Size3f(0.5f, 0.5f, 0.5f);

	/// <summary>
	/// Liquids are handled and rendered differently than solid blocks.
	/// </summary>
	public EnumMatterState MatterState = EnumMatterState.Solid;

	/// <summary>
	/// Max amount of collectible that one default inventory slot can hold
	/// </summary>
	public int MaxStackSize = 64;

	/// <summary>
	/// How many uses does this collectible has when being used. Item disappears at durability 0
	/// </summary>
	public int Durability = 1;

	/// <summary>
	/// Physical size of this collectible when held or (notionally) in a container. 0.5 x 0.5 x 0.5 meters by default.
	/// <br />Note, if all three dimensions are set to zero, the default will be used.
	/// </summary>
	public Size3f Dimensions = DefaultSize;

	/// <summary>
	/// When true, liquids become selectable to the player when being held in hands
	/// </summary>
	public bool LiquidSelectable;

	/// <summary>
	/// How much damage this collectible deals when used as a weapon
	/// </summary>
	public float AttackPower = 0.5f;

	/// <summary>
	/// If true, when the player holds the sneak key and right clicks with this item in hand, calls OnHeldInteractStart first. Without it, the order is reversed. Takes precedence over priority interact placed blocks.
	/// </summary>
	public bool HeldPriorityInteract;

	/// <summary>
	/// Until how for away can you attack entities using this collectibe
	/// </summary>
	public float AttackRange = GlobalConstants.DefaultAttackRange;

	/// <summary>
	/// From which damage sources does the item takes durability damage
	/// </summary>
	public EnumItemDamageSource[] DamagedBy;

	/// <summary>
	/// Modifies how fast the player can break a block when holding this item
	/// </summary>
	public Dictionary<EnumBlockMaterial, float> MiningSpeed;

	/// <summary>
	/// What tier this block can mine when held in hands
	/// </summary>
	public int ToolTier;

	public HeldSounds HeldSounds;

	/// <summary>
	/// List of creative tabs in which this collectible should appear in
	/// </summary>
	public string[] CreativeInventoryTabs;

	/// <summary>
	/// If set, the breaking particles will be taken from this texture, otherwise it'll just pick the first texture
	/// </summary>
	public string ParticlesTextureCode;

	/// <summary>
	/// If you want to add itemstacks with custom attributes to the creative inventory, add them to this list
	/// </summary>
	public CreativeTabAndStackList[] CreativeInventoryStacks;

	/// <summary>
	/// Alpha test value for rendering in gui, fp hand, tp hand or on the ground
	/// </summary>
	public float RenderAlphaTest = 0.05f;

	/// <summary>
	/// Used for scaling, rotation or offseting the block when rendered in guis
	/// </summary>
	public ModelTransform GuiTransform;

	/// <summary>
	/// Used for scaling, rotation or offseting the block when rendered in the first person mode hand
	/// </summary>
	public ModelTransform FpHandTransform;

	/// <summary>
	/// Used for scaling, rotation or offseting the block when rendered in the third person mode hand
	/// </summary>
	public ModelTransform TpHandTransform;

	/// <summary>
	/// Used for scaling, rotation or offseting the block when rendered in the third person mode offhand
	/// </summary>
	public ModelTransform TpOffHandTransform;

	/// <summary>
	/// Used for scaling, rotation or offseting the rendered as a dropped item on the ground
	/// </summary>
	public ModelTransform GroundTransform;

	/// <summary>
	/// Custom Attributes that's always assiociated with this item
	/// </summary>
	public JsonObject Attributes;

	/// <summary>
	/// Information about the burnable states
	/// </summary>
	public CombustibleProperties CombustibleProps;

	/// <summary>
	/// Information about the nutrition states
	/// </summary>
	public FoodNutritionProperties NutritionProps;

	/// <summary>
	/// Information about the transitionable states
	/// </summary>
	public TransitionableProperties[] TransitionableProps;

	/// <summary>
	/// If set, the collectible can be ground into something else
	/// </summary>
	public GrindingProperties GrindingProps;

	/// <summary>
	/// If set, the collectible can be crushed into something else
	/// </summary>
	public CrushingProperties CrushingProps;

	/// <summary>
	/// Particles that should spawn in regular intervals from this block or item when held in hands
	/// </summary>
	public AdvancedParticleProperties[] ParticleProperties;

	/// <summary>
	/// The origin point from which particles are being spawned
	/// </summary>
	public FastVec3f TopMiddlePos = new FastVec3f(0.5f, 1f, 0.5f);

	/// <summary>
	/// If set, this item will be classified as given tool
	/// </summary>
	public EnumTool? Tool;

	/// <summary>
	/// Determines in which kind of bags the item can be stored in
	/// </summary>
	public EnumItemStorageFlags StorageFlags = EnumItemStorageFlags.General;

	/// <summary>
	/// Determines on whether an object floats on liquids or not. Water has a density of 1000
	/// </summary>
	public int MaterialDensity = 2000;

	/// <summary>
	/// The animation to play in 3rd person mod when hitting with this collectible
	/// </summary>
	public string HeldTpHitAnimation = "breakhand";

	/// <summary>
	/// The animation to play in 3rd person mod when holding this collectible in the right hand
	/// </summary>
	public string HeldRightTpIdleAnimation;

	/// <summary>
	/// The animation to play in 3rd person mod when holding this collectible in the left hand
	/// </summary>
	public string HeldLeftTpIdleAnimation;

	/// <summary>
	///
	/// </summary>
	public string HeldLeftReadyAnimation;

	/// <summary>
	///
	/// </summary>
	public string HeldRightReadyAnimation;

	/// <summary>
	/// The animation to play in 3rd person mod when using this collectible
	/// </summary>
	public string HeldTpUseAnimation = "interactstatic";

	/// <summary>
	/// The api object, assigned during OnLoaded
	/// </summary>
	protected ICoreAPI api;

	/// <summary>
	/// Modifiers that can alter the behavior of the item or block, mostly for held interaction
	/// </summary>
	public CollectibleBehavior[] CollectibleBehaviors = Array.Empty<CollectibleBehavior>();

	/// <summary>
	/// For light emitting collectibles: hue, saturation and brightness value
	/// </summary>
	public ThreeBytes LightHsv;

	public TagSet Tags = TagSet.Empty;

	/// <summary>
	/// This value is set by the BlockId- or ItemId-Remapper if it encounters a block/item in the savegame,
	/// but no longer exists as a loaded block/item
	/// </summary>
	public bool IsMissing { get; set; }

	/// <summary>
	/// The block or item id
	/// </summary>
	public abstract int Id { get; }

	/// <summary>
	/// Block or Item?
	/// </summary>
	public abstract EnumItemClass ItemClass { get; }

	[Obsolete("Use GetToolTier()")]
	public int MiningTier
	{
		get
		{
			return ToolTier;
		}
		set
		{
			ToolTier = value;
		}
	}

	/// <summary>
	/// For blocks and items, the hashcode is the id - useful when building HashSets
	/// </summary>
	public override int GetHashCode()
	{
		return Id;
	}

	public virtual TagSet GetTags(ItemStack stack)
	{
		return Tags;
	}

	public void OnLoadedNative(ICoreAPI api)
	{
		this.api = api;
		OnLoaded(api);
	}

	/// <summary>
	/// Server Side: Called one the collectible has been registered
	/// Client Side: Called once the collectible has been loaded from server packet
	/// </summary>
	public virtual void OnLoaded(ICoreAPI api)
	{
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		for (int i = 0; i < collectibleBehaviors.Length; i++)
		{
			collectibleBehaviors[i].OnLoaded(api);
		}
	}

	/// <summary>
	/// Called when the client/server is shutting down
	/// </summary>
	/// <param name="api"></param>
	public virtual void OnUnloaded(ICoreAPI api)
	{
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		for (int i = 0; i < collectibleBehaviors.Length; i++)
		{
			collectibleBehaviors[i].OnUnloaded(api);
		}
	}

	/// <summary>
	/// Should return the light HSV values.
	/// Warning: This method is likely to get called in a background thread. Please make sure your code in here is thread safe.
	/// </summary>
	/// <param name="blockAccessor"></param>
	/// <param name="pos">May be null</param>
	/// <param name="stack">Set if its an itemstack for which the engine wants to check the light level</param>
	/// <returns></returns>
	public virtual byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
	{
		return LightHsv;
	}

	/// <summary>
	/// Should return the burnable properties of the item/block
	/// </summary>
	/// <param name="world">May be null</param>
	/// <param name="itemstack">Set if its an itemstack for which to get properties</param>
	/// <param name="pos">May be null</param>
	/// <returns></returns>
	public virtual CombustibleProperties GetCombustibleProperties(IWorldAccessor world, ItemStack itemstack, BlockPos pos)
	{
		return CombustibleProps;
	}

	/// <summary>
	/// Should return the nutrition properties of the item/block
	/// </summary>
	/// <param name="world"></param>
	/// <param name="itemstack"></param>
	/// <param name="forEntity"></param>
	/// <returns></returns>
	public virtual FoodNutritionProperties GetNutritionProperties(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
	{
		return NutritionProps;
	}

	/// <summary>
	/// Should return the transition properties of the item/block when in itemstack form
	/// </summary>
	/// <param name="world"></param>
	/// <param name="itemstack"></param>
	/// <param name="forEntity"></param>
	/// <returns></returns>
	public virtual TransitionableProperties[] GetTransitionableProperties(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
	{
		return TransitionableProps;
	}

	/// <summary>
	/// Should return properties used for grounding collectible into something else
	/// </summary>
	/// <param name="world"></param>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual GrindingProperties GetGrindingProperties(IWorldAccessor world, ItemStack itemstack)
	{
		return GrindingProps;
	}

	/// <summary>
	/// Should return properties used for crushing collectible into something else
	/// </summary>
	/// <param name="world"></param>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual CrushingProperties GetCrushingProperties(IWorldAccessor world, ItemStack itemstack)
	{
		return CrushingProps;
	}

	/// <summary>
	/// Should returns true if this collectible requires UpdateAndGetTransitionStates() to be called when ticking.
	/// <br />Typical usage: true if this collectible itself has transitionable properties, or true for collectibles which hold other itemstacks with transitionable properties (for example, a cooked food container)
	/// </summary>
	/// <param name="world"></param>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual bool RequiresTransitionableTicking(IWorldAccessor world, ItemStack itemstack)
	{
		TransitionableProperties[] transitionableProperties = itemstack.Collectible.GetTransitionableProperties(world, itemstack, null);
		bool result = transitionableProperties != null && transitionableProperties.Length != 0;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			bool flag = obj.RequiresTransitionableTicking(world, itemstack, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				result = flag;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return result;
			}
		}
		return result;
	}

	/// <summary>
	/// Should return in which storage containers this item can be placed in
	/// </summary>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual EnumItemStorageFlags GetStorageFlags(ItemStack itemstack)
	{
		bool flag = false;
		EnumItemStorageFlags enumItemStorageFlags = StorageFlags;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			EnumItemStorageFlags storageFlags = obj.GetStorageFlags(itemstack, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				flag = true;
				enumItemStorageFlags = storageFlags;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return enumItemStorageFlags;
			}
		}
		if (flag)
		{
			return enumItemStorageFlags;
		}
		IHeldBag collectibleInterface = GetCollectibleInterface<IHeldBag>();
		if (collectibleInterface != null && (enumItemStorageFlags & EnumItemStorageFlags.Backpack) > (EnumItemStorageFlags)0 && collectibleInterface.IsEmpty(itemstack))
		{
			return EnumItemStorageFlags.General | EnumItemStorageFlags.Backpack;
		}
		return enumItemStorageFlags;
	}

	/// <summary>
	/// Returns a hardcoded rgb color (green-&gt;yellow-&gt;red) that is representative for its remaining durability vs total durability
	/// </summary>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual int GetItemDamageColor(ItemStack itemstack)
	{
		int maxDurability = GetMaxDurability(itemstack);
		if (maxDurability == 0)
		{
			return 0;
		}
		int num = GameMath.Clamp(100 * itemstack.Collectible.GetRemainingDurability(itemstack) / maxDurability, 0, 99);
		return GuiStyle.DamageColorGradient[num];
	}

	/// <summary>
	/// Return true if remaining durability != total durability
	/// </summary>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual bool ShouldDisplayItemDamage(ItemStack itemstack)
	{
		return GetMaxDurability(itemstack) != GetRemainingDurability(itemstack);
	}

	/// <summary>
	/// This method is called before rendering the item stack into GUI, first person hand, third person hand and/or on the ground
	/// The renderinfo object is pre-filled with default values.
	/// </summary>
	/// <param name="capi"></param>
	/// <param name="itemstack"></param>
	/// <param name="target"></param>
	/// <param name="renderinfo"></param>
	public virtual void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
	{
		for (int i = 0; i < CollectibleBehaviors.Length; i++)
		{
			CollectibleBehaviors[i].OnBeforeRender(capi, itemstack, target, ref renderinfo);
		}
	}

	[Obsolete("Use GetMaxDurability instead")]
	public virtual int GetDurability(IItemStack itemstack)
	{
		return GetMaxDurability(itemstack as ItemStack);
	}

	/// <summary>
	/// Returns the items total durability
	/// </summary>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual int GetMaxDurability(ItemStack itemstack)
	{
		int durability = Durability;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			int maxDurability = bh.GetMaxDurability(itemstack, durability, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				durability = maxDurability;
			}
		}, delegate
		{
		});
		return durability;
	}

	public virtual int GetRemainingDurability(ItemStack itemstack)
	{
		int durability = (int)itemstack.Attributes.GetDecimal("durability", GetMaxDurability(itemstack));
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			int remainingDurability = bh.GetRemainingDurability(itemstack, durability, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				durability = remainingDurability;
			}
		}, delegate
		{
		});
		return durability;
	}

	/// <summary>
	/// The amount of damage dealt when used as a weapon
	/// </summary>
	/// <param name="itemStack"></param>
	/// <returns></returns>
	public virtual float GetAttackPower(ItemStack itemStack)
	{
		float atp = AttackPower;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			float attackPower = bh.GetAttackPower(itemStack, atp, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				atp = attackPower;
			}
		}, delegate
		{
		});
		return atp;
	}

	/// <summary>
	/// The amount of damage dealt when used as a weapon on given entity
	/// </summary>
	/// <param name="baseDamage"></param>
	/// <param name="entity"></param>
	/// <param name="itemStack"></param>
	/// <param name="isCriticalHit"></param>
	/// <returns></returns>
	public virtual float GetDamageToEntity(float baseDamage, Entity entity, ItemStack itemStack, ref bool isCriticalHit)
	{
		float outDamage = 0f;
		bool outCrit = false;
		bool bhcrit = isCriticalHit;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			outDamage = bh.GetDamageToEntity(baseDamage, entity, itemStack, ref bhcrit, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				baseDamage = outDamage;
				outCrit = bhcrit;
			}
		}, delegate
		{
			outDamage = baseDamage;
			outCrit = bhcrit;
		});
		isCriticalHit = outCrit;
		return outDamage;
	}

	/// <summary>
	/// The the attack range when used as a weapon
	/// </summary>
	/// <param name="withItemStack"></param>
	/// <returns></returns>
	public virtual float GetAttackRange(IItemStack withItemStack)
	{
		float atr = AttackRange;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			float attackRange = bh.GetAttackRange((ItemStack)withItemStack, atr, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				atr = attackRange;
			}
		}, delegate
		{
		});
		return atr;
	}

	/// <summary>
	/// Player is holding this collectible and breaks the targeted block
	/// </summary>
	/// <param name="player"></param>
	/// <param name="blockSel"></param>
	/// <param name="itemslot"></param>
	/// <param name="remainingResistance"></param>
	/// <param name="dt"></param>
	/// <param name="counter"></param>
	/// <returns></returns>
	public virtual float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
	{
		bool flag = false;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handled = EnumHandling.PassThrough;
			float num = obj.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter, ref handled);
			if (handled != EnumHandling.PassThrough)
			{
				remainingResistance = num;
				flag = true;
			}
			if (handled == EnumHandling.PreventSubsequent)
			{
				return remainingResistance;
			}
		}
		if (flag)
		{
			return remainingResistance;
		}
		Block block = player.Entity.World.BlockAccessor.GetBlock(blockSel.Position);
		EnumBlockMaterial blockMaterial = block.GetBlockMaterial(api.World.BlockAccessor, blockSel.Position);
		Vec3f normalf = blockSel.Face.Normalf;
		Random rand = player.Entity.World.Rand;
		Dictionary<EnumBlockMaterial, float> miningSpeeds = GetMiningSpeeds(itemslot);
		int requiredMiningTier = block.GetRequiredMiningTier(api.World, blockSel.Position);
		bool flag2 = requiredMiningTier > 0 && itemslot.Itemstack?.Collectible != null && (itemslot.Itemstack.Collectible.GetToolTier(itemslot) < requiredMiningTier || miningSpeeds == null || !miningSpeeds.ContainsKey(blockMaterial));
		double num2 = ((blockMaterial == EnumBlockMaterial.Ore) ? 0.72 : 0.12);
		EnumTool? enumTool = itemslot.Itemstack?.Collectible.GetTool(itemslot);
		if (counter % 5 == 0 && (rand.NextDouble() < num2 || flag2) && (blockMaterial == EnumBlockMaterial.Stone || blockMaterial == EnumBlockMaterial.Ore) && (enumTool == EnumTool.Pickaxe || enumTool == EnumTool.Hammer))
		{
			double num3 = (double)blockSel.Position.X + blockSel.HitPosition.X;
			double num4 = (double)blockSel.Position.Y + blockSel.HitPosition.Y;
			double num5 = (double)blockSel.Position.Z + blockSel.HitPosition.Z;
			player.Entity.World.SpawnParticles(new SimpleParticleProperties
			{
				MinQuantity = 0f,
				AddQuantity = 8f,
				Color = ColorUtil.ToRgba(255, 255, 255, 128),
				MinPos = new Vec3d(num3 + (double)(normalf.X * 0.01f), num4 + (double)(normalf.Y * 0.01f), num5 + (double)(normalf.Z * 0.01f)),
				AddPos = new Vec3d(0.0, 0.0, 0.0),
				MinVelocity = new Vec3f(4f * normalf.X, 4f * normalf.Y, 4f * normalf.Z),
				AddVelocity = new Vec3f(8f * ((float)rand.NextDouble() - 0.5f), 8f * ((float)rand.NextDouble() - 0.5f), 8f * ((float)rand.NextDouble() - 0.5f)),
				LifeLength = 0.025f,
				GravityEffect = 0f,
				MinSize = 0.03f,
				MaxSize = 0.4f,
				ParticleModel = EnumParticleModel.Cube,
				VertexFlags = 200,
				SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.15f)
			}, player);
		}
		if (flag2)
		{
			return remainingResistance;
		}
		return remainingResistance - GetMiningSpeed(itemslot.Itemstack, blockSel, block, player) * dt;
	}

	/// <summary>
	/// Whenever the collectible was modified while inside a slot, usually when it was moved, split or merged.
	/// </summary>
	/// <param name="world"></param>
	/// <param name="slot">The slot the item is or was in</param>
	/// <param name="extractedStack">Non null if the itemstack was removed from this slot</param>
	public virtual void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack extractedStack = null)
	{
	}

	/// <summary>
	/// Player has broken a block while holding this collectible. Return false if you want to cancel the block break event.
	/// </summary>
	/// <param name="world"></param>
	/// <param name="byEntity"></param>
	/// <param name="itemslot"></param>
	/// <param name="blockSel"></param>
	/// <param name="dropQuantityMultiplier"></param>
	/// <returns></returns>
	public virtual bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1f)
	{
		bool flag = true;
		bool flag2 = false;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling bhHandling = EnumHandling.PassThrough;
			bool flag3 = obj.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier, ref bhHandling);
			if (bhHandling != EnumHandling.PassThrough)
			{
				flag = flag && flag3;
				flag2 = true;
			}
			if (bhHandling == EnumHandling.PreventSubsequent)
			{
				return flag;
			}
		}
		if (flag2)
		{
			return flag;
		}
		IPlayer byPlayer = null;
		if (byEntity is EntityPlayer)
		{
			byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
		}
		(blockSel.Block ?? world.BlockAccessor.GetBlock(blockSel.Position)).OnBlockBroken(world, blockSel.Position, byPlayer, dropQuantityMultiplier);
		EnumItemDamageSource[] damagedBy = GetDamagedBy(itemslot);
		if (damagedBy != null && damagedBy.Contains(EnumItemDamageSource.BlockBreaking))
		{
			DamageItem(world, byEntity, itemslot);
		}
		return true;
	}

	/// <summary>
	/// Called every game tick when the player breaks a block with this item in his hands. Returns the mining speed for given block.
	/// </summary>
	/// <param name="itemstack"></param>
	/// <param name="blockSel"></param>
	/// <param name="block"></param>
	/// <param name="forPlayer"></param>
	/// <returns></returns>
	public virtual float GetMiningSpeed(IItemStack itemstack, BlockSelection blockSel, Block block, IPlayer forPlayer)
	{
		float traitRate = 1f;
		EnumBlockMaterial material = block.GetBlockMaterial(api.World.BlockAccessor, blockSel.Position);
		if (material == EnumBlockMaterial.Ore || material == EnumBlockMaterial.Stone)
		{
			traitRate = forPlayer.Entity.Stats.GetBlended("miningSpeedMul");
		}
		float toolMiningSpeed = 1f;
		Dictionary<EnumBlockMaterial, float> miningSpeeds = GetMiningSpeeds(new DummySlot((ItemStack)itemstack));
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			float miningSpeed = bh.GetMiningSpeed(itemstack as ItemStack, blockSel, block, forPlayer, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				toolMiningSpeed *= miningSpeed;
			}
		}, delegate
		{
			if (miningSpeeds == null || !miningSpeeds.ContainsKey(material))
			{
				toolMiningSpeed *= traitRate;
			}
			else
			{
				toolMiningSpeed *= miningSpeeds[material] * traitRate * GlobalConstants.ToolMiningSpeedModifier;
			}
		});
		return toolMiningSpeed;
	}

	/// <summary>
	/// Only get the mining speed modifier from CollectibleBehavior such as the buffable (quenching).
	/// Also applies the GlobalConstants.ToolMiningSpeedModifier if prevent default is not set by any CollectibleBehavior
	/// </summary>
	/// <param name="itemStack"></param>
	/// <returns></returns>
	public virtual float GetMiningSpeedModifier(IItemStack itemStack)
	{
		float toolMiningSpeed = 1f;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			float miningSpeedModifier = bh.GetMiningSpeedModifier(itemStack as ItemStack, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				toolMiningSpeed *= miningSpeedModifier;
			}
		}, delegate
		{
			toolMiningSpeed *= GlobalConstants.ToolMiningSpeedModifier;
		});
		return toolMiningSpeed;
	}

	/// <summary>
	/// Returns the mining speeds for all materials of the item.
	/// </summary>
	/// <param name="slot"></param>
	/// <returns></returns>
	public virtual Dictionary<EnumBlockMaterial, float> GetMiningSpeeds(ItemSlot slot)
	{
		Dictionary<EnumBlockMaterial, float> result = MiningSpeed;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling bhHandling = EnumHandling.PassThrough;
			Dictionary<EnumBlockMaterial, float> miningSpeeds = obj.GetMiningSpeeds(slot, ref bhHandling);
			if (bhHandling != EnumHandling.PassThrough)
			{
				result = miningSpeeds;
			}
			if (bhHandling == EnumHandling.PreventSubsequent)
			{
				return result;
			}
		}
		return result;
	}

	/// <summary>
	/// Returns tool type of the item
	/// </summary>
	/// <param name="slot"></param>
	/// <returns>Tool type</returns>
	public virtual EnumTool? GetTool(ItemSlot slot)
	{
		return Tool;
	}

	/// <summary>
	/// What tier this tool can mine when held in hands
	/// </summary>
	/// <param name="slot"></param>
	/// <returns></returns>
	public virtual int GetToolTier(ItemSlot slot)
	{
		return ToolTier;
	}

	/// <summary>
	/// From which damage sources does the item take durability damage
	/// </summary>
	/// <param name="slot"></param>
	/// <returns></returns>
	public virtual EnumItemDamageSource[] GetDamagedBy(ItemSlot slot)
	{
		return DamagedBy;
	}

	/// <summary>
	/// Not implemented yet
	/// </summary>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <returns></returns>
	[Obsolete]
	public virtual ModelTransformKeyFrame[] GeldHeldFpHitAnimation(ItemSlot slot, Entity byEntity)
	{
		return null;
	}

	/// <summary>
	/// Called when an entity uses this item to hit something in 3rd person mode
	/// </summary>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <returns></returns>
	public virtual string GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity)
	{
		string anim = HeldTpHitAnimation;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			string heldTpHitAnimation = bh.GetHeldTpHitAnimation(slot, byEntity, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				anim = heldTpHitAnimation;
			}
		}, delegate
		{
		});
		return anim;
	}

	/// <summary>
	/// Called when an entity holds this item in hands in 3rd person mode
	/// </summary>
	/// <param name="activeHotbarSlot"></param>
	/// <param name="forEntity"></param>
	/// <param name="hand"></param>
	/// <returns></returns>
	public virtual string GetHeldReadyAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
	{
		string anim = (anim = ((hand == EnumHand.Left) ? HeldLeftReadyAnimation : HeldRightReadyAnimation));
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			string heldReadyAnimation = bh.GetHeldReadyAnimation(activeHotbarSlot, forEntity, hand, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				anim = heldReadyAnimation;
			}
		}, delegate
		{
		});
		if (api is ICoreClientAPI coreClientAPI)
		{
			ItemStack itemstack = coreClientAPI.World.Player.Entity.LeftHandItemSlot.Itemstack;
			if (activeHotbarSlot.Itemstack.Collectible.GetTemperature(forEntity.World, activeHotbarSlot.Itemstack) > (float)GlobalConstants.TooHotToTouchTemperature && itemstack != null && itemstack.ItemAttributes?.IsTrue("heatResistant") == true)
			{
				return null;
			}
		}
		return anim;
	}

	/// <summary>
	/// Called when an entity holds this item in hands in 3rd person mode
	/// </summary>
	/// <param name="activeHotbarSlot"></param>
	/// <param name="forEntity"></param>
	/// <param name="hand"></param>
	/// <returns></returns>
	public virtual string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
	{
		string anim = (anim = ((hand == EnumHand.Left) ? HeldLeftTpIdleAnimation : HeldRightTpIdleAnimation));
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			string heldTpIdleAnimation = bh.GetHeldTpIdleAnimation(activeHotbarSlot, forEntity, hand, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				anim = heldTpIdleAnimation;
			}
		}, delegate
		{
		});
		return anim;
	}

	/// <summary>
	/// Called when an entity holds this item in hands in 3rd person mode
	/// </summary>
	/// <param name="activeHotbarSlot"></param>
	/// <param name="forEntity"></param>
	/// <returns></returns>
	public virtual string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
	{
		string anim = null;
		if (GetNutritionProperties(forEntity.World, activeHotbarSlot.Itemstack, forEntity) == null)
		{
			anim = HeldTpUseAnimation;
		}
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			string heldTpUseAnimation = bh.GetHeldTpUseAnimation(activeHotbarSlot, forEntity, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				anim = heldTpUseAnimation;
			}
		}, delegate
		{
		});
		return anim;
	}

	/// <summary>
	/// An entity used this collectibe to attack something
	/// </summary>
	/// <param name="world"></param>
	/// <param name="byEntity"></param>
	/// <param name="attackedEntity"></param>
	/// <param name="itemslot"></param>
	public virtual void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot itemslot)
	{
		EnumItemDamageSource[] damagedBy = GetDamagedBy(itemslot);
		if (damagedBy != null && damagedBy.Contains(EnumItemDamageSource.Attacking) && attackedEntity != null && attackedEntity.Alive)
		{
			DamageItem(world, byEntity, itemslot);
		}
	}

	/// <summary>
	/// Called when this collectible is attempted to being used as part of a crafting recipe and should get consumed now. Return false if it doesn't match the ingredient
	/// </summary>
	/// <param name="inputStack"></param>
	/// <param name="recipe"></param>
	/// <param name="ingredient"></param>
	/// <returns></returns>
	public virtual bool MatchesForCrafting(ItemStack inputStack, IRecipeBase recipe, IRecipeIngredient ingredient)
	{
		if (!ingredient.ConsumeProperties.Consume && ingredient.ConsumeProperties.DurabilityCost > inputStack.Collectible.GetRemainingDurability(inputStack))
		{
			return false;
		}
		return true;
	}

	public virtual void OnConsumedByCrafting(ItemSlot[] allInputSlots, ItemSlot stackInSlot, IRecipeBase recipe, IRecipeIngredient fromIngredient, IPlayer byPlayer, int quantity)
	{
		JsonObject attributes = Attributes;
		if (attributes != null && attributes["noConsumeOnCrafting"].AsBool())
		{
			string text = stackInSlot?.Itemstack?.Collectible?.Code.ToString() ?? "";
			if ((!(text == "game:schematic-glider") && !(text == "game:schematic-customtranslocator")) || fromIngredient.ConsumeProperties.Consume)
			{
				byPlayer.Entity?.Api.Logger.Warning($"Collectible '{Code}' has 'noConsumeOnCrafting' set to true. This attribute is obsolete, use 'consume = false' in recipes instead.");
			}
			return;
		}
		if (!fromIngredient.ConsumeProperties.Consume)
		{
			if (fromIngredient.ConsumeProperties.DurabilityChange < 0)
			{
				stackInSlot.Itemstack.Collectible.DamageItem(byPlayer.Entity.World, byPlayer.Entity, stackInSlot, fromIngredient.ConsumeProperties.DurabilityCost, fromIngredient.ConsumeProperties.BreakOnZeroDurability);
			}
			return;
		}
		stackInSlot.Itemstack.StackSize -= quantity;
		if (stackInSlot.Itemstack.StackSize <= 0)
		{
			stackInSlot.Itemstack = null;
			stackInSlot.MarkDirty();
		}
		if (fromIngredient.ReturnedStack != null)
		{
			ItemStack itemstack = fromIngredient.ReturnedStack.ResolvedItemstack.Clone();
			if (!byPlayer.InventoryManager.TryGiveItemstack(itemstack, slotNotifyEffect: true))
			{
				api.World.SpawnItemEntity(itemstack, byPlayer.Entity.Pos.XYZ);
			}
		}
	}

	/// <summary>
	/// Called when a matching recipe has been found and an item is placed into the crafting output slot (which is still before the player clicks on the output slot to actually craft the item and consume the ingredients)
	/// </summary>
	/// <param name="allInputslots"></param>
	/// <param name="outputSlot"></param>
	/// <param name="byRecipe"></param>
	public virtual void OnCreatedByCrafting(ItemSlot[] allInputSlots, ItemSlot outputSlot, IRecipeBase byRecipe)
	{
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling hd)
		{
			bh.OnCreatedByCrafting(allInputSlots, outputSlot, byRecipe, ref hd);
		}, delegate
		{
			float num = 0f;
			float num2 = 0f;
			if (byRecipe.AverageDurability)
			{
				IEnumerable<IRecipeIngredient> recipeIngredients = byRecipe.RecipeIngredients;
				ItemSlot[] array = allInputSlots;
				foreach (ItemSlot itemSlot in array)
				{
					if (!itemSlot.Empty)
					{
						ItemStack itemstack = itemSlot.Itemstack;
						int maxDurability = itemstack.Collectible.GetMaxDurability(itemstack);
						if (maxDurability == 0)
						{
							num += 0.125f;
							num2 += 0.125f;
						}
						else
						{
							bool flag = false;
							foreach (IRecipeIngredient item in recipeIngredients)
							{
								if (item != null && !item.ConsumeProperties.Consume && item.SatisfiesAsIngredient(itemstack))
								{
									flag = true;
									break;
								}
							}
							if (!flag)
							{
								num2 += 1f;
								int remainingDurability = itemstack.Collectible.GetRemainingDurability(itemstack);
								num += (float)remainingDurability / (float)maxDurability;
							}
						}
					}
				}
				float num3 = num / num2;
				if (num3 < 1f)
				{
					outputSlot.Itemstack.Collectible.SetDurability(outputSlot.Itemstack, (int)Math.Max(1f, num3 * (float)outputSlot.Itemstack.Collectible.GetMaxDurability(outputSlot.Itemstack)));
				}
			}
			TransitionableProperties transitionableProperties = outputSlot.Itemstack.Collectible.GetTransitionableProperties(api.World, outputSlot.Itemstack, null)?.FirstOrDefault((TransitionableProperties p) => p.Type == EnumTransitionType.Perish);
			if (transitionableProperties != null)
			{
				transitionableProperties.TransitionedStack.Resolve(api.World, "oncrafted perished stack", Code);
				CarryOverFreshness(api, allInputSlots, new ItemStack[1] { outputSlot.Itemstack }, transitionableProperties);
			}
		});
	}

	/// <summary>
	/// Called after the player has taken out the item from the output slot
	/// </summary>
	/// <param name="slots"></param>
	/// <param name="outputSlot"></param>
	/// <param name="matchingRecipe"></param>
	/// <returns>true to prevent default ingredient consumption</returns>
	public virtual bool ConsumeCraftingIngredients(ItemSlot[] slots, ItemSlot outputSlot, IRecipeBase matchingRecipe)
	{
		bool result = false;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			bool flag = obj.ConsumeCraftingIngredients(slots, outputSlot, matchingRecipe, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				result = flag;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return result;
			}
		}
		return result;
	}

	/// <summary>
	/// Sets the items durability
	/// </summary>
	/// <param name="itemstack"></param>
	/// <param name="amount"></param>
	public virtual void SetDurability(ItemStack itemstack, int amount)
	{
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling hd)
		{
			bh.OnSetDurability(itemstack, ref amount, ref hd);
		}, delegate
		{
			itemstack.Attributes.SetInt("durability", amount);
		});
	}

	/// <summary>
	/// Causes the item to be damaged. If 'destroyOnZeroDurability == true' will play a breaking sound and remove the itemstack if no more durability is left
	/// </summary>
	/// <param name="world"></param>
	/// <param name="byEntity"></param>
	/// <param name="itemSlot"></param>
	/// <param name="amount">Amount of damage</param>
	public virtual void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemSlot, int amount = 1, bool destroyOnZeroDurability = true)
	{
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling hd)
		{
			bh.OnDamageItem(world, byEntity, itemSlot, ref amount, ref hd);
		}, delegate
		{
			ItemStack itemstack = itemSlot.Itemstack;
			int remainingDurability = itemstack.Collectible.GetRemainingDurability(itemstack);
			remainingDurability -= amount;
			itemstack.Collectible.SetDurability(itemstack, remainingDurability);
			if (remainingDurability <= 0)
			{
				if (!destroyOnZeroDurability)
				{
					itemstack.Collectible.SetDurability(itemstack, 0);
				}
				else
				{
					DestroyItem(world, byEntity, itemSlot);
				}
			}
		});
		itemSlot.MarkDirty();
	}

	/// <summary>
	/// Will play a breaking sound and remove the itemstack
	/// </summary>
	/// <param name="world"></param>
	/// <param name="byEntity"></param>
	/// <param name="itemSlot"></param>
	public virtual void DestroyItem(IWorldAccessor world, Entity byEntity, ItemSlot itemSlot)
	{
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling hd)
		{
			bh.OnDestroyItem(world, byEntity, itemSlot, ref hd);
		}, delegate
		{
			EnumTool? tool = itemSlot?.Itemstack?.Collectible?.GetTool(itemSlot);
			ItemStack itemstack = itemSlot.Itemstack;
			itemSlot.Itemstack = null;
			IPlayer player = (byEntity as EntityPlayer)?.Player;
			if (player != null)
			{
				if (tool.HasValue)
				{
					string ident = Attributes?["slotRefillIdentifier"].ToString();
					RefillSlotIfEmpty(itemSlot, byEntity as EntityAgent, (ItemStack stack) => (ident == null) ? (stack.Collectible.GetTool(new DummySlot(stack)) == tool) : (stack.ItemAttributes?["slotRefillIdentifier"]?.ToString() == ident));
					if (!itemSlot.Empty)
					{
						JsonObject attributes = Attributes;
						if (attributes != null && attributes.IsTrue("rememberToolModeWhenBroken"))
						{
							itemSlot.Itemstack.Collectible.SetToolMode(itemSlot, player, null, GetToolMode(new DummySlot(itemstack), player, null));
						}
					}
				}
				if (itemSlot.Itemstack != null && !itemSlot.Itemstack.Attributes.HasAttribute("durability"))
				{
					itemstack = itemSlot.Itemstack;
					itemstack.Collectible.SetDurability(itemstack, itemstack.Collectible.GetMaxDurability(itemstack));
				}
			}
			world.PlaySoundAt(HeldSounds.ToolBreak, byEntity);
			world.SpawnCubeParticles(byEntity.Pos.XYZ.Add(byEntity.SelectionBox.Y2 / 2f), itemstack, 0.25f, 30, 1f, player);
		});
		itemSlot.MarkDirty();
	}

	public virtual void RefillSlotIfEmpty(ItemSlot slot, EntityAgent byEntity, ActionConsumable<ItemStack> matcher)
	{
		if (!slot.Empty)
		{
			return;
		}
		byEntity.WalkInventory(delegate(ItemSlot invslot)
		{
			if (invslot is ItemSlotCreative)
			{
				return true;
			}
			InventoryBase inventory = invslot.Inventory;
			if (!(inventory is InventoryBasePlayer) && !inventory.HasOpened((byEntity as EntityPlayer).Player))
			{
				return true;
			}
			if (invslot.Itemstack != null && matcher(invslot.Itemstack))
			{
				invslot.TryPutInto(byEntity.World, slot);
				invslot.Inventory?.PerformNotifySlot(invslot.Inventory.GetSlotId(invslot));
				slot.Inventory?.PerformNotifySlot(slot.Inventory.GetSlotId(slot));
				slot.MarkDirty();
				invslot.MarkDirty();
				return false;
			}
			return true;
		});
	}

	public virtual SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
	{
		for (int i = 0; i < CollectibleBehaviors.Length; i++)
		{
			SkillItem[] toolModes = CollectibleBehaviors[i].GetToolModes(slot, forPlayer, blockSel);
			if (toolModes != null)
			{
				return toolModes;
			}
		}
		return null;
	}

	/// <summary>
	/// Should return the current items tool mode.
	/// </summary>
	/// <param name="slot"></param>
	/// <param name="byPlayer"></param>
	/// <param name="blockSelection"></param>
	/// <returns>The tool mode to display or -1 to not display the current mode</returns>
	public virtual int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
	{
		for (int i = 0; i < CollectibleBehaviors.Length; i++)
		{
			int toolMode = CollectibleBehaviors[i].GetToolMode(slot, byPlayer, blockSelection);
			if (toolMode != 0)
			{
				return toolMode;
			}
		}
		return 0;
	}

	/// <summary>
	/// Should set given toolmode
	/// </summary>
	/// <param name="slot"></param>
	/// <param name="byPlayer"></param>
	/// <param name="blockSelection"></param>
	/// <param name="toolMode"></param>
	public virtual void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
	{
		for (int i = 0; i < CollectibleBehaviors.Length; i++)
		{
			CollectibleBehaviors[i].SetToolMode(slot, byPlayer, blockSelection, toolMode);
		}
	}

	/// <summary>
	/// This method is called during the opaque render pass when this item or block is being held in hands
	/// </summary>
	/// <param name="inSlot"></param>
	/// <param name="byPlayer"></param>
	public virtual void OnHeldRenderOpaque(ItemSlot inSlot, IClientPlayer byPlayer)
	{
	}

	/// <summary>
	/// This method is called during the order independent transparency render pass when this item or block is being held in hands
	/// </summary>
	/// <param name="inSlot"></param>
	/// <param name="byPlayer"></param>
	public virtual void OnHeldRenderOit(ItemSlot inSlot, IClientPlayer byPlayer)
	{
	}

	/// <summary>
	/// This method is called during the ortho (for 2D GUIs) render pass when this item or block is being held in hands
	/// </summary>
	/// <param name="inSlot"></param>
	/// <param name="byPlayer"></param>
	public virtual void OnHeldRenderOrtho(ItemSlot inSlot, IClientPlayer byPlayer)
	{
	}

	/// <summary>
	/// Called every frame when the player is holding this collectible in his hands. Is not called during OnUsing() or OnAttacking()
	/// </summary>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	public virtual void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
	{
	}

	public virtual void OnHeldActionAnimStart(ItemSlot slot, EntityAgent byEntity, EnumHandInteract type)
	{
	}

	/// <summary>
	/// Called every game tick when this collectible is in dropped form in the world (i.e. as EntityItem)
	/// </summary>
	/// <param name="entityItem"></param>
	public virtual void OnGroundIdle(EntityItem entityItem)
	{
		if (!entityItem.Swimming || api.Side != EnumAppSide.Server)
		{
			return;
		}
		JsonObject attributes = Attributes;
		if (attributes != null && attributes.IsTrue("dissolveInWater"))
		{
			if (api.World.Rand.NextDouble() < 0.01)
			{
				api.World.SpawnCubeParticles(entityItem.Pos.XYZ, entityItem.Itemstack.Clone(), 0.1f, 80, 0.3f);
				entityItem.Die();
			}
			else if (api.World.Rand.NextDouble() < 0.2)
			{
				api.World.SpawnCubeParticles(entityItem.Pos.XYZ, entityItem.Itemstack.Clone(), 0.1f, 2, 0.2f + (float)api.World.Rand.NextDouble() / 5f);
			}
		}
	}

	/// <summary>
	/// Called every frame when this item is being displayed in the gui
	/// </summary>
	/// <param name="world"></param>
	/// <param name="stack"></param>
	public virtual void InGuiIdle(IWorldAccessor world, ItemStack stack)
	{
	}

	/// <summary>
	/// Called when this item was collected by an entity
	/// </summary>
	/// <param name="stack"></param>
	/// <param name="entity"></param>
	public virtual void OnCollected(ItemStack stack, Entity entity)
	{
	}

	/// <summary>
	/// General begin use access. Override OnHeldAttackStart or OnHeldInteractStart to alter the behavior.
	/// </summary>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSel"></param>
	/// <param name="entitySel"></param>
	/// <param name="useType"></param>
	/// <param name="firstEvent">True on first mouse down</param>
	/// <param name="handling">Whether or not to do any subsequent actions. If not set or set to NotHandled, the action will not called on the server.</param>
	/// <returns></returns>
	public virtual void OnHeldUseStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumHandInteract useType, bool firstEvent, ref EnumHandHandling handling)
	{
		switch (useType)
		{
		case EnumHandInteract.HeldItemAttack:
			OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
			break;
		case EnumHandInteract.HeldItemInteract:
			OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
			break;
		}
	}

	/// <summary>
	/// General cancel use access. Override OnHeldAttackCancel or OnHeldInteractCancel to alter the behavior.
	/// </summary>
	/// <param name="secondsPassed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSel"></param>
	/// <param name="entitySel"></param>
	/// <param name="cancelReason"></param>
	/// <returns></returns>
	public EnumHandInteract OnHeldUseCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
	{
		EnumHandInteract handUse = byEntity.Controls.HandUse;
		if (!((handUse == EnumHandInteract.HeldItemAttack) ? OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSel, entitySel, cancelReason) : OnHeldInteractCancel(secondsPassed, slot, byEntity, blockSel, entitySel, cancelReason)))
		{
			return handUse;
		}
		return EnumHandInteract.None;
	}

	/// <summary>
	/// General using access. Override OnHeldAttackStep or OnHeldInteractStep to alter the behavior. Called every 20ms
	/// </summary>
	/// <param name="secondsPassed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSel"></param>
	/// <param name="entitySel"></param>
	/// <returns></returns>
	public EnumHandInteract OnHeldUseStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
	{
		EnumHandInteract handUse = byEntity.Controls.HandUse;
		if (!((handUse == EnumHandInteract.HeldItemAttack) ? OnHeldAttackStep(secondsPassed, slot, byEntity, blockSel, entitySel) : OnHeldInteractStep(secondsPassed, slot, byEntity, blockSel, entitySel)))
		{
			return EnumHandInteract.None;
		}
		return handUse;
	}

	/// <summary>
	/// General use over access. Override OnHeldAttackStop or OnHeldInteractStop to alter the behavior.
	/// </summary>
	/// <param name="secondsPassed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSel"></param>
	/// <param name="entitySel"></param>
	/// <param name="useType"></param>
	public void OnHeldUseStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumHandInteract useType)
	{
		if (useType == EnumHandInteract.HeldItemAttack)
		{
			OnHeldAttackStop(secondsPassed, slot, byEntity, blockSel, entitySel);
		}
		else
		{
			OnHeldInteractStop(secondsPassed, slot, byEntity, blockSel, entitySel);
		}
	}

	/// <summary>
	/// When the player has begun using this item for attacking (left mouse click). Return true to play a custom action.
	/// </summary>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSel"></param>
	/// <param name="entitySel"></param>
	/// <param name="handling">Whether or not to do any subsequent actions. If not set or set to NotHandled, the action will not called on the server.</param>
	/// <returns></returns>
	public virtual void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
	{
		EnumHandHandling bhHandHandling = EnumHandHandling.NotHandled;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling hd)
		{
			bh.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref bhHandHandling, ref hd);
		}, delegate
		{
			if (HeldSounds?.Attack.Location != null)
			{
				api.World.PlaySoundAt(HeldSounds.Attack, byEntity, (byEntity as EntityPlayer)?.Player);
			}
		});
		handling = bhHandHandling;
	}

	/// <summary>
	/// When the player has canceled a custom attack action. Return false to deny action cancellation.
	/// </summary>
	/// <param name="secondsPassed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSelection"></param>
	/// <param name="entitySel"></param>
	/// <param name="cancelReason"></param>
	/// <returns></returns>
	public virtual bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
	{
		bool retval = false;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling hd)
		{
			bool flag = bh.OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSelection, entitySel, cancelReason, ref hd);
			if (hd != EnumHandling.PassThrough)
			{
				retval = flag;
			}
		}, delegate
		{
		});
		return retval;
	}

	/// <summary>
	/// Called continously when a custom attack action is playing. Return false to stop the action. Called every 20ms
	/// </summary>
	/// <param name="secondsPassed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSelection"></param>
	/// <param name="entitySel"></param>
	/// <returns></returns>
	public virtual bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
	{
		bool retval = false;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling hd)
		{
			bool flag = bh.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSelection, entitySel, ref hd);
			if (hd != EnumHandling.PassThrough)
			{
				retval = flag;
			}
		}, delegate
		{
		});
		return retval;
	}

	/// <summary>
	/// Called when a custom attack action is finished
	/// </summary>
	/// <param name="secondsPassed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSelection"></param>
	/// <param name="entitySel"></param>
	public virtual void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
	{
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling hd)
		{
			bh.OnHeldAttackStop(secondsPassed, slot, byEntity, blockSelection, entitySel, ref hd);
		}, delegate
		{
		});
	}

	/// <summary>
	/// Called when the player right clicks while holding this block/item in his hands
	/// </summary>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSel"></param>
	/// <param name="entitySel"></param>
	/// <param name="firstEvent">True when the player pressed the right mouse button on this block. Every subsequent call, while the player holds right mouse down will be false, it gets called every second while right mouse is down</param>
	/// <param name="handling">Whether or not to do any subsequent actions. If not set or set to NotHandled, the action will not called on the server.</param>
	public virtual void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
	{
		EnumHandHandling handHandling = EnumHandHandling.NotHandled;
		bool flag = false;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling2 = EnumHandling.PassThrough;
			obj.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling2);
			if (handling2 != EnumHandling.PassThrough)
			{
				handling = handHandling;
				flag = true;
			}
			if (handling2 == EnumHandling.PreventSubsequent)
			{
				return;
			}
		}
		if (!flag)
		{
			if (blockSel != null && getCoolingMedium(blockSel) != null && GetTemperature(api.World, slot.Itemstack) > (float)GlobalConstants.TooHotToTouchTemperature)
			{
				handling = EnumHandHandling.Handled;
				return;
			}
			tryEatBegin(slot, byEntity, ref handHandling);
			handling = handHandling;
		}
	}

	protected ICoolingMedium getCoolingMedium(BlockSelection blockSel)
	{
		ICoolingMedium coolingMedium = api.World.BlockAccessor.GetBlock(blockSel.Position, 2).GetInterface<ICoolingMedium>(api.World, blockSel.Position);
		if (coolingMedium != null)
		{
			return coolingMedium;
		}
		BlockPos pos = blockSel.Position.AddCopy(blockSel.Face.Normali);
		return api.World.BlockAccessor.GetBlock(pos, 2).GetInterface<ICoolingMedium>(api.World, pos);
	}

	/// <summary>
	/// Called every 20ms while the player is using this collectible. Return false to stop the interaction.
	/// </summary>
	/// <param name="secondsUsed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSel"></param>
	/// <param name="entitySel"></param>
	/// <returns>False if the interaction should be stopped. True if the interaction should continue</returns>
	public virtual bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
	{
		bool flag = true;
		bool flag2 = false;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			bool flag3 = obj.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				flag = flag && flag3;
				flag2 = true;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return flag;
			}
		}
		if (flag2)
		{
			return flag;
		}
		if (!flag2 && blockSel != null)
		{
			ICoolingMedium coolingMedium = getCoolingMedium(blockSel);
			if (coolingMedium != null && GetTemperature(byEntity.World, slot.Itemstack) > (float)GlobalConstants.CollectibleDefaultTemperature && coolingMedium.CanCool(slot, blockSel.FullPosition))
			{
				coolingMedium.CoolNow(slot, blockSel.FullPosition, 0.02f);
				slot.MarkDirty();
				return true;
			}
		}
		return tryEatStep(secondsUsed, slot, byEntity);
	}

	/// <summary>
	/// Called when the player successfully completed the using action, always called once an interaction is over
	/// </summary>
	/// <param name="secondsUsed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSel"></param>
	/// <param name="entitySel"></param>
	public virtual void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
	{
		bool flag = false;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			obj.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				flag = true;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return;
			}
		}
		if (!flag)
		{
			tryEatStop(secondsUsed, slot, byEntity);
		}
	}

	/// <summary>
	/// When the player released the right mouse button. Return false to deny the cancellation (= will keep using the item until OnHeldInteractStep returns false).
	/// </summary>
	/// <param name="secondsUsed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="blockSel"></param>
	/// <param name="entitySel"></param>
	/// <param name="cancelReason"></param>
	/// <returns></returns>
	public virtual bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
	{
		bool flag = true;
		bool flag2 = false;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handled = EnumHandling.PassThrough;
			bool flag3 = obj.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
			if (handled != EnumHandling.PassThrough)
			{
				flag = flag && flag3;
				flag2 = true;
			}
			if (handled == EnumHandling.PreventSubsequent)
			{
				return flag;
			}
		}
		if (flag2)
		{
			return flag;
		}
		return true;
	}

	/// <summary>
	/// Tries to eat the contents in the slot, first call
	/// </summary>
	protected virtual void tryEatBegin(ItemSlot slot, EntityAgent byEntity, ref EnumHandHandling handling, string eatSound = "eat", int eatSoundRepeats = 1)
	{
		if (!slot.Empty && GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity) != null)
		{
			byEntity.World.RegisterCallback(delegate
			{
				playEatSound(byEntity, eatSound, eatSoundRepeats);
			}, 500);
			byEntity.AnimManager?.StartAnimation("eat");
			handling = EnumHandHandling.PreventDefault;
		}
	}

	protected void playEatSound(EntityAgent byEntity, string eatSound = "eat", int eatSoundRepeats = 1)
	{
		if (byEntity.Controls.HandUse != EnumHandInteract.HeldItemInteract)
		{
			return;
		}
		IPlayer dualCallByPlayer = null;
		if (byEntity is EntityPlayer)
		{
			dualCallByPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
		}
		byEntity.PlayEntitySound(eatSound, dualCallByPlayer);
		eatSoundRepeats--;
		if (eatSoundRepeats > 0)
		{
			byEntity.World.RegisterCallback(delegate
			{
				playEatSound(byEntity, eatSound, eatSoundRepeats);
			}, 300);
		}
	}

	/// <summary>
	/// Tries to eat the contents in the slot, eat step call
	/// </summary>
	/// <param name="secondsUsed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	/// <param name="spawnParticleStack"></param>
	protected virtual bool tryEatStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, ItemStack spawnParticleStack = null)
	{
		if (GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity) == null)
		{
			return false;
		}
		Vec3d xYZ = byEntity.Pos.AheadCopy(0.4000000059604645).XYZ;
		xYZ.X += byEntity.LocalEyePos.X;
		xYZ.Y += byEntity.LocalEyePos.Y - 0.4000000059604645;
		xYZ.Z += byEntity.LocalEyePos.Z;
		if (secondsUsed > 0.5f && (int)(30f * secondsUsed) % 7 == 1)
		{
			byEntity.World.SpawnCubeParticles(xYZ, spawnParticleStack ?? slot.Itemstack, 0.3f, 4, 0.5f, (byEntity as EntityPlayer)?.Player);
		}
		if (byEntity.World is IClientWorldAccessor)
		{
			return secondsUsed <= 1f;
		}
		return true;
	}

	/// <summary>
	/// Finished eating the contents in the slot, final call
	/// </summary>
	/// <param name="secondsUsed"></param>
	/// <param name="slot"></param>
	/// <param name="byEntity"></param>
	protected virtual void tryEatStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
	{
		FoodNutritionProperties nutritionProperties = GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity);
		if (!(byEntity.World is IServerWorldAccessor) || nutritionProperties == null || !(secondsUsed >= 0.95f))
		{
			return;
		}
		float spoilState = UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish)?.TransitionLevel ?? 0f;
		float num = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, byEntity);
		float num2 = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, byEntity);
		byEntity.ReceiveSaturation(nutritionProperties.Satiety * num, nutritionProperties.FoodCategory, nutritionProperties.SaturationLossDelay);
		IPlayer player = null;
		if (byEntity is EntityPlayer)
		{
			player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
		}
		slot.TakeOut(1);
		if (nutritionProperties.EatenStack != null)
		{
			if (slot.Empty)
			{
				slot.Itemstack = nutritionProperties.EatenStack.ResolvedItemstack.Clone();
			}
			else if (player == null || !player.InventoryManager.TryGiveItemstack(nutritionProperties.EatenStack.ResolvedItemstack.Clone(), slotNotifyEffect: true))
			{
				byEntity.World.SpawnItemEntity(nutritionProperties.EatenStack.ResolvedItemstack.Clone(), byEntity.Pos.XYZ);
			}
		}
		float num3 = nutritionProperties.Health * num2;
		float num4 = byEntity.WatchedAttributes.GetFloat("intoxication");
		byEntity.WatchedAttributes.SetFloat("intoxication", Math.Min(1.1f, num4 + nutritionProperties.Intoxication));
		float num5 = byEntity.WatchedAttributes.GetFloat("psychedelic");
		byEntity.WatchedAttributes.SetFloat("psychedelic", Math.Min(2f, num5 + nutritionProperties.Psychedelic));
		if (num3 != 0f)
		{
			float valueOrDefault = (slot.Itemstack?.Collectible?.Attributes?["eatHealthEffectDurationSec"].AsFloat()).GetValueOrDefault();
			int ticksPerDuration = slot.Itemstack?.Collectible?.Attributes?["eatHealthEffectTicks"].AsInt(1) ?? 1;
			byEntity.ReceiveDamage(new DamageSource
			{
				Source = EnumDamageSource.Internal,
				Type = ((num3 > 0f) ? EnumDamageType.Heal : EnumDamageType.Poison),
				Duration = TimeSpan.FromSeconds(valueOrDefault),
				TicksPerDuration = ticksPerDuration,
				DamageOverTimeTypeEnum = ((!(num3 > 0f)) ? EnumDamageOverTimeEffectType.Poison : EnumDamageOverTimeEffectType.Unknown)
			}, Math.Abs(num3));
		}
		slot.MarkDirty();
		player.InventoryManager.BroadcastHotbarSlot();
	}

	/// <summary>
	/// Callback when the player dropped this item from his inventory. You can set handling to PreventDefault to prevent dropping this item.
	/// You can also check if the entityplayer of this player is dead to check if dropping of this item was due the players death
	/// </summary>
	/// <param name="world"></param>
	/// <param name="byPlayer"></param>
	/// <param name="slot"></param>
	/// <param name="quantity">Amount of items the player wants to drop</param>
	/// <param name="handling"></param>
	public virtual void OnHeldDropped(IWorldAccessor world, IPlayer byPlayer, ItemSlot slot, int quantity, ref EnumHandling handling)
	{
	}

	/// <summary>
	/// Called by the inventory system when you hover over an item stack. This is the item stack name that is getting displayed.
	/// </summary>
	/// <param name="itemStack"></param>
	/// <returns></returns>
	public virtual string GetHeldItemName(ItemStack itemStack)
	{
		if (Code == null)
		{
			return "Invalid block, id " + Id;
		}
		string text = ItemClass.Name();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(Lang.GetMatching(Code?.Domain + ":" + text + "-" + Code?.Path));
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		for (int i = 0; i < collectibleBehaviors.Length; i++)
		{
			collectibleBehaviors[i].GetHeldItemName(stringBuilder, itemStack);
		}
		return stringBuilder.ToString();
	}

	/// <summary>
	/// Called by the inventory system when you hover over an item stack. This is the text that is getting displayed.
	/// </summary>
	/// <param name="inSlot"></param>
	/// <param name="dsc"></param>
	/// <param name="world"></param>
	/// <param name="withDebugInfo"></param>
	public virtual void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
	{
		ItemStack itemstack = inSlot.Itemstack;
		string itemDescText = GetItemDescText();
		if (withDebugInfo)
		{
			dsc.AppendLine("<font color=\"#bbbbbb\">Id:" + Id + "</font>");
			dsc.AppendLine(string.Concat("<font color=\"#bbbbbb\">Code: ", Code, "</font>"));
			ICoreAPI coreAPI = api;
			if (coreAPI != null && coreAPI.Side == EnumAppSide.Client && (api as ICoreClientAPI).Input.KeyboardKeyStateRaw[1])
			{
				dsc.AppendLine("<font color=\"#bbbbbb\">Attributes: " + inSlot.Itemstack.Attributes.ToJsonToken() + "</font>\n");
			}
			if (world.Api is ICoreClientAPI coreClientAPI)
			{
				StringBuilder stringBuilder = new StringBuilder(1024);
				stringBuilder.AppendJoin(", ", coreClientAPI.CollectibleTagRegistry.SlowEnumerateTagNames(GetTags(inSlot.Itemstack)));
				if (stringBuilder.Length > 0)
				{
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(35, 1, dsc);
					handler.AppendLiteral("<font color=\"#bbbbbb\">Tags: ");
					handler.AppendFormatted(stringBuilder);
					handler.AppendLiteral("</font>");
					dsc.AppendLine(ref handler);
				}
			}
		}
		int maxDurability = GetMaxDurability(itemstack);
		if (maxDurability > 1)
		{
			dsc.AppendLine(Lang.Get("Durability: {0} / {1}", itemstack.Collectible.GetRemainingDurability(itemstack), maxDurability));
		}
		Dictionary<EnumBlockMaterial, float> miningSpeeds = GetMiningSpeeds(inSlot);
		float miningSpeedModifier = GetMiningSpeedModifier(inSlot.Itemstack);
		if (miningSpeeds != null && miningSpeeds.Count > 0)
		{
			dsc.AppendLine(Lang.Get("Tool Tier: {0}", GetToolTier(inSlot)));
			dsc.Append(Lang.Get("item-tooltip-miningspeed"));
			int num = 0;
			foreach (KeyValuePair<EnumBlockMaterial, float> item in miningSpeeds)
			{
				if (!((double)item.Value < 1.1))
				{
					if (num > 0)
					{
						dsc.Append(", ");
					}
					dsc.Append(Lang.Get(item.Key.ToString()) + " " + (item.Value * miningSpeedModifier).ToString("#.#") + "x");
					num++;
				}
			}
			dsc.Append("\n");
		}
		IHeldBag collectibleInterface = GetCollectibleInterface<IHeldBag>();
		if (collectibleInterface != null)
		{
			dsc.AppendLine(Lang.Get("Storage Slots: {0}", collectibleInterface.GetQuantitySlots(itemstack)));
			bool flag = false;
			ItemStack[] contents = collectibleInterface.GetContents(itemstack, world);
			if (contents != null)
			{
				ItemStack[] array = contents;
				foreach (ItemStack itemStack in array)
				{
					if (itemStack != null && itemStack.StackSize != 0)
					{
						if (!flag)
						{
							dsc.AppendLine(Lang.Get("Contents: "));
							flag = true;
						}
						itemStack.ResolveBlockOrItem(world);
						dsc.AppendLine("- " + itemStack.StackSize + "x " + itemStack.GetName());
					}
				}
				if (!flag)
				{
					dsc.AppendLine(Lang.Get("Empty"));
				}
			}
		}
		EntityPlayer entityPlayer = ((world.Side == EnumAppSide.Client) ? (world as IClientWorldAccessor).Player.Entity : null);
		float spoilState = AppendPerishableInfoText(inSlot, dsc, world);
		FoodNutritionProperties nutritionProperties = GetNutritionProperties(world, itemstack, entityPlayer);
		if (nutritionProperties != null)
		{
			float num2 = GlobalConstants.FoodSpoilageSatLossMul(spoilState, itemstack, entityPlayer);
			float num3 = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, itemstack, entityPlayer);
			if (Math.Abs(nutritionProperties.Health * num3) > 0.001f)
			{
				dsc.AppendLine(Lang.Get((MatterState == EnumMatterState.Liquid) ? "liquid-when-drunk-saturation-hp" : "When eaten: {0} sat, {1} hp", Math.Round(nutritionProperties.Satiety * num2), Math.Round(nutritionProperties.Health * num3, 2)));
			}
			else
			{
				dsc.AppendLine(Lang.Get((MatterState == EnumMatterState.Liquid) ? "liquid-when-drunk-saturation" : "When eaten: {0} sat", Math.Round(nutritionProperties.Satiety * num2)));
			}
			dsc.AppendLine(Lang.Get("Food Category: {0}", Lang.Get("foodcategory-" + nutritionProperties.FoodCategory.ToString().ToLowerInvariant())));
		}
		GrindingProperties grindingProperties = itemstack.Collectible.GetGrindingProperties(world, itemstack);
		if (grindingProperties?.GroundStack?.ResolvedItemstack != null)
		{
			dsc.AppendLine(Lang.Get("When ground: Turns into {0}x {1}", grindingProperties.GroundStack.ResolvedItemstack.StackSize, grindingProperties.GroundStack.ResolvedItemstack.GetName()));
		}
		CrushingProperties crushingProperties = itemstack.Collectible.GetCrushingProperties(world, itemstack);
		if (crushingProperties != null)
		{
			float num4 = crushingProperties.Quantity.avg * (float)crushingProperties.CrushedStack.ResolvedItemstack.StackSize;
			dsc.AppendLine(Lang.Get("When pulverized: Turns into {0:0.#}x {1}", num4, crushingProperties.CrushedStack.ResolvedItemstack.GetName()));
			dsc.AppendLine(Lang.Get("Requires Pulverizer tier: {0}", crushingProperties.HardnessTier));
		}
		if (GetAttackPower(itemstack) > 0.5f)
		{
			dsc.AppendLine(Lang.Get("Attack power: {0} damage", GetAttackPower(itemstack).ToString("0.#")));
			dsc.AppendLine(Lang.Get("Attack tier: {0}", GetToolTier(inSlot)));
		}
		if (GetAttackRange(itemstack) > GlobalConstants.DefaultAttackRange)
		{
			dsc.AppendLine(Lang.Get("Attack range: {0} m", GetAttackRange(itemstack).ToString("0.#")));
		}
		CombustibleProperties combustibleProperties = itemstack.Collectible.GetCombustibleProperties(world, itemstack, null);
		if (combustibleProperties != null)
		{
			string text = combustibleProperties.SmeltingType.ToString().ToLowerInvariant();
			if (text == "fire")
			{
				dsc.AppendLine(Lang.Get("itemdesc-fireinkiln"));
			}
			else
			{
				if (combustibleProperties.BurnTemperature > 0)
				{
					dsc.AppendLine(Lang.Get("Burn temperature: {0}°C", combustibleProperties.BurnTemperature));
					dsc.AppendLine(Lang.Get("Burn duration: {0}s", combustibleProperties.BurnDuration));
				}
				if (combustibleProperties.MeltingPoint > 0)
				{
					dsc.AppendLine(Lang.Get("game:smeltpoint-" + text, combustibleProperties.MeltingPoint));
				}
			}
			if (combustibleProperties.SmeltedStack?.ResolvedItemstack != null)
			{
				int smeltedRatio = combustibleProperties.SmeltedRatio;
				int stackSize = combustibleProperties.SmeltedStack.ResolvedItemstack.StackSize;
				string value = ((smeltedRatio == 1) ? Lang.Get("game:smeltdesc-" + text + "-singular", stackSize, combustibleProperties.SmeltedStack.ResolvedItemstack.GetName()) : Lang.Get("game:smeltdesc-" + text + "-plural", smeltedRatio, stackSize, combustibleProperties.SmeltedStack.ResolvedItemstack.GetName()));
				dsc.AppendLine(value);
			}
		}
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		for (int i = 0; i < collectibleBehaviors.Length; i++)
		{
			collectibleBehaviors[i].GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
		}
		if (itemDescText.Length > 0 && dsc.Length > 0)
		{
			dsc.Append("\n");
		}
		dsc.Append(itemDescText);
		float temperature = GetTemperature(world, itemstack);
		if (temperature > 20f)
		{
			dsc.AppendLine(Lang.Get("Temperature: {0}°C", (int)temperature));
		}
		if (Code != null && Code.Domain != "game")
		{
			Mod mod = api.ModLoader.GetMod(Code.Domain);
			dsc.AppendLine(Lang.Get("Mod: {0}", mod?.Info.Name ?? Code.Domain));
		}
	}

	public virtual string GetItemDescText()
	{
		string text = Code?.Domain + ":" + ItemClass.ToString().ToLowerInvariant() + "desc-" + Code?.Path;
		string matching = Lang.GetMatching(text);
		if (matching == text)
		{
			return "";
		}
		return matching + "\n";
	}

	/// <summary>
	/// Interaction help thats displayed above the hotbar, when the player puts this item/block in his active hand slot
	/// </summary>
	/// <param name="inSlot"></param>
	/// <returns></returns>
	public virtual WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
	{
		WorldInteraction[] array = ((GetNutritionProperties(api.World, inSlot.Itemstack, null) == null) ? Array.Empty<WorldInteraction>() : new WorldInteraction[1]
		{
			new WorldInteraction
			{
				ActionLangCode = "heldhelp-eat",
				MouseButton = EnumMouseButton.Right
			}
		});
		EnumHandling handling = EnumHandling.PassThrough;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		for (int i = 0; i < collectibleBehaviors.Length; i++)
		{
			WorldInteraction[] heldInteractionHelp = collectibleBehaviors[i].GetHeldInteractionHelp(inSlot, ref handling);
			array = array.Append(heldInteractionHelp);
			if (handling == EnumHandling.PreventSubsequent)
			{
				break;
			}
		}
		return array;
	}

	public virtual float AppendPerishableInfoText(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
	{
		float num = 0f;
		TransitionState[] array = UpdateAndGetTransitionStates(api.World, inSlot);
		bool flag = false;
		if (array == null)
		{
			return 0f;
		}
		for (int i = 0; i < array.Length; i++)
		{
			num = Math.Max(num, AppendPerishableInfoText(inSlot, dsc, world, array[i], flag));
			flag = flag || num > 0f;
		}
		return num;
	}

	public virtual float AppendPerishableInfoText(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, TransitionState state, bool nowSpoiling)
	{
		TransitionableProperties props = state.Props;
		float num = GetTransitionRateMul(world, inSlot, props.Type);
		if (inSlot.Inventory is CreativeInventoryTab)
		{
			num = 1f;
		}
		float transitionLevel = state.TransitionLevel;
		float num2 = state.FreshHoursLeft / num;
		switch (props.Type)
		{
		case EnumTransitionType.Perish:
		{
			if (transitionLevel > 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-perishable-spoiling", (int)Math.Round(transitionLevel * 100f)));
				return transitionLevel;
			}
			if (num <= 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-perishable"));
				break;
			}
			float hoursPerDay = api.World.Calendar.HoursPerDay;
			float num7 = num2 / hoursPerDay / (float)api.World.Calendar.DaysPerYear;
			if (num7 >= 1f)
			{
				if (num7 <= 1.05f)
				{
					dsc.AppendLine(Lang.Get("itemstack-perishable-fresh-one-year"));
					break;
				}
				dsc.AppendLine(Lang.Get("itemstack-perishable-fresh-years", Math.Round(num7, 1)));
			}
			else if (num2 > hoursPerDay)
			{
				dsc.AppendLine(Lang.Get("itemstack-perishable-fresh-days", Math.Round(num2 / hoursPerDay, 1)));
			}
			else
			{
				dsc.AppendLine(Lang.Get("itemstack-perishable-fresh-hours", Math.Round(num2, 1)));
			}
			break;
		}
		case EnumTransitionType.Cure:
		{
			if (nowSpoiling)
			{
				break;
			}
			if (transitionLevel > 0f || (num2 <= 0f && num > 0f))
			{
				dsc.AppendLine(Lang.Get("itemstack-curable-curing", (int)Math.Round(transitionLevel * 100f)));
				break;
			}
			double num8 = api.World.Calendar.HoursPerDay;
			if (num <= 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-curable"));
			}
			else if ((double)num2 > num8)
			{
				dsc.AppendLine(Lang.Get("itemstack-curable-duration-days", Math.Round((double)num2 / num8, 1)));
			}
			else
			{
				dsc.AppendLine(Lang.Get("itemstack-curable-duration-hours", Math.Round(num2, 1)));
			}
			break;
		}
		case EnumTransitionType.Ripen:
		{
			if (nowSpoiling)
			{
				break;
			}
			if (transitionLevel > 0f || (num2 <= 0f && num > 0f))
			{
				dsc.AppendLine(Lang.Get("itemstack-ripenable-ripening", (int)Math.Round(transitionLevel * 100f)));
				break;
			}
			double num4 = api.World.Calendar.HoursPerDay;
			if (num <= 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-ripenable"));
			}
			else if ((double)num2 > num4)
			{
				dsc.AppendLine(Lang.Get("itemstack-ripenable-duration-days", Math.Round((double)num2 / num4, 1)));
			}
			else
			{
				dsc.AppendLine(Lang.Get("itemstack-ripenable-duration-hours", Math.Round(num2, 1)));
			}
			break;
		}
		case EnumTransitionType.Dry:
		{
			if (nowSpoiling)
			{
				break;
			}
			if (transitionLevel > 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-dryable-dried", (int)Math.Round(transitionLevel * 100f)));
				dsc.AppendLine(Lang.Get("Drying rate in this container: {0:0.##}x", num));
				break;
			}
			double num5 = api.World.Calendar.HoursPerDay;
			if (num <= 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-dryable"));
			}
			else if ((double)num2 > num5)
			{
				dsc.AppendLine(Lang.Get("itemstack-dryable-duration-days", Math.Round((double)num2 / num5, 1)));
			}
			else
			{
				dsc.AppendLine(Lang.Get("itemstack-dryable-duration-hours", Math.Round(num2, 1)));
			}
			break;
		}
		case EnumTransitionType.Melt:
		{
			if (nowSpoiling)
			{
				break;
			}
			if (transitionLevel > 0f || num2 <= 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-meltable-melted", (int)Math.Round(transitionLevel * 100f)));
				dsc.AppendLine(Lang.Get("Melting rate in this container: {0:0.##}x", num));
				break;
			}
			double num6 = api.World.Calendar.HoursPerDay;
			if (num <= 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-meltable"));
			}
			else if ((double)num2 > num6)
			{
				dsc.AppendLine(Lang.Get("itemstack-meltable-duration-days", Math.Round((double)num2 / num6, 1)));
			}
			else
			{
				dsc.AppendLine(Lang.Get("itemstack-meltable-duration-hours", Math.Round(num2, 1)));
			}
			break;
		}
		case EnumTransitionType.Harden:
		{
			if (nowSpoiling)
			{
				break;
			}
			if (transitionLevel > 0f || num2 <= 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-hardenable-hardened", (int)Math.Round(transitionLevel * 100f)));
				break;
			}
			double num3 = api.World.Calendar.HoursPerDay;
			if (num <= 0f)
			{
				dsc.AppendLine(Lang.Get("itemstack-hardenable"));
			}
			else if ((double)num2 > num3)
			{
				dsc.AppendLine(Lang.Get("itemstack-hardenable-duration-days", Math.Round((double)num2 / num3, 1)));
			}
			else
			{
				dsc.AppendLine(Lang.Get("itemstack-hardenable-duration-hours", Math.Round(num2, 1)));
			}
			break;
		}
		}
		return 0f;
	}

	public virtual void OnHandbookRecipeRender(ICoreClientAPI capi, IRecipeBase recipe, ItemSlot slot, double x, double y, double z, double size)
	{
		EnumHandling enumHandling = EnumHandling.PassThrough;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			obj.OnHandbookRecipeRender(capi, recipe, slot, x, y, z, size, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				enumHandling = handling;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				break;
			}
		}
		if (enumHandling == EnumHandling.PassThrough)
		{
			capi.Render.RenderItemstackToGui(slot, x, y, z, (float)size * 0.58f, -1);
		}
	}

	public virtual List<ItemStack> GetHandBookStacks(ICoreClientAPI capi)
	{
		if (Code == null)
		{
			return null;
		}
		JsonObject jsonObject = Attributes?["handbook"];
		if (jsonObject != null && jsonObject["exclude"].AsBool())
		{
			return null;
		}
		bool num = CreativeInventoryTabs != null && CreativeInventoryTabs.Length != 0;
		bool flag = CreativeInventoryStacks != null && CreativeInventoryStacks.Length != 0;
		if (!num && !flag && jsonObject?["include"].AsBool() != true)
		{
			return null;
		}
		List<ItemStack> list = new List<ItemStack>();
		if (flag && (jsonObject == null || !jsonObject["ignoreCreativeInvStacks"].AsBool()))
		{
			for (int i = 0; i < CreativeInventoryStacks.Length; i++)
			{
				JsonItemStack[] stacks = CreativeInventoryStacks[i].Stacks;
				for (int j = 0; j < stacks.Length; j++)
				{
					ItemStack stack = stacks[j].ResolvedItemstack;
					stack.ResolveBlockOrItem(capi.World);
					stack = stack.Clone();
					stack.StackSize = stack.Collectible.MaxStackSize;
					if (!list.Any((ItemStack itemStack) => itemStack.Equals(stack)))
					{
						list.Add(stack);
					}
				}
			}
		}
		else
		{
			ItemStack item = new ItemStack(this);
			list.Add(item);
		}
		return list;
	}

	/// <summary>
	/// Should return true if the stack can be placed into given slot
	/// </summary>
	/// <param name="stack"></param>
	/// <param name="slot"></param>
	/// <returns></returns>
	public virtual bool CanBePlacedInto(ItemStack stack, ItemSlot slot)
	{
		if (slot.StorageType != 0)
		{
			return (slot.StorageType & GetStorageFlags(stack)) > (EnumItemStorageFlags)0;
		}
		return true;
	}

	/// <summary>
	/// Should return the max. number of items that can be placed from sourceStack into the sinkStack
	/// </summary>
	/// <param name="sinkStack"></param>
	/// <param name="sourceStack"></param>
	/// <param name="priority"></param>
	/// <returns></returns>
	public virtual int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
	{
		int result = 0;
		bool flag = false;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			int mergableQuantity = obj.GetMergableQuantity(sinkStack, sourceStack, priority, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				result = mergableQuantity;
				flag = true;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return result;
			}
		}
		if (flag)
		{
			return result;
		}
		if (Equals(sinkStack, sourceStack, GlobalConstants.IgnoredStackAttributes) && sinkStack.StackSize < MaxStackSize)
		{
			return Math.Min(MaxStackSize - sinkStack.StackSize, sourceStack.StackSize);
		}
		return 0;
	}

	/// <summary>
	/// Is always called on the sink slots item
	/// </summary>
	/// <param name="op"></param>
	public virtual void TryMergeStacks(ItemStackMergeOperation op)
	{
		bool flag = false;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			obj.TryMergeStacks(op, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				flag = true;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return;
			}
		}
		if (flag)
		{
			return;
		}
		op.MovableQuantity = GetMergableQuantity(op.SinkSlot.Itemstack, op.SourceSlot.Itemstack, op.CurrentPriority);
		if (op.MovableQuantity == 0 || !op.SinkSlot.CanTakeFrom(op.SourceSlot, op.CurrentPriority))
		{
			return;
		}
		bool flag2 = false;
		bool flag3 = false;
		op.MovedQuantity = GameMath.Min(op.SinkSlot.GetRemainingSlotSpace(op.SourceSlot.Itemstack), op.MovableQuantity, op.RequestedQuantity);
		if (HasTemperature(op.SinkSlot.Itemstack) || HasTemperature(op.SourceSlot.Itemstack))
		{
			if (op.CurrentPriority < EnumMergePriority.DirectMerge && Math.Abs(GetTemperature(op.World, op.SinkSlot.Itemstack) - GetTemperature(op.World, op.SourceSlot.Itemstack)) > 30f)
			{
				op.MovedQuantity = 0;
				op.MovableQuantity = 0;
				op.RequiredPriority = EnumMergePriority.DirectMerge;
				return;
			}
			flag2 = true;
		}
		TransitionState[] array = UpdateAndGetTransitionStates(op.World, op.SourceSlot);
		TransitionState[] array2 = UpdateAndGetTransitionStates(op.World, op.SinkSlot);
		Dictionary<EnumTransitionType, TransitionState> dictionary = null;
		if (array != null)
		{
			bool flag4 = true;
			bool flag5 = true;
			if (array2 == null)
			{
				op.MovedQuantity = 0;
				op.MovableQuantity = 0;
				return;
			}
			dictionary = new Dictionary<EnumTransitionType, TransitionState>();
			TransitionState[] array3 = array2;
			foreach (TransitionState transitionState in array3)
			{
				dictionary[transitionState.Props.Type] = transitionState;
			}
			array3 = array;
			foreach (TransitionState transitionState2 in array3)
			{
				if (!dictionary.TryGetValue(transitionState2.Props.Type, out var value))
				{
					flag5 = false;
					flag4 = false;
					break;
				}
				if (Math.Abs(value.TransitionedHours - transitionState2.TransitionedHours) > 4f && Math.Abs(value.TransitionedHours - transitionState2.TransitionedHours) / transitionState2.FreshHours > 0.03f)
				{
					flag5 = false;
				}
			}
			if (!flag5 && op.CurrentPriority < EnumMergePriority.DirectMerge)
			{
				op.MovedQuantity = 0;
				op.MovableQuantity = 0;
				op.RequiredPriority = EnumMergePriority.DirectMerge;
				return;
			}
			if (!flag4)
			{
				op.MovedQuantity = 0;
				op.MovableQuantity = 0;
				return;
			}
			flag3 = true;
		}
		if (op.SourceSlot.Itemstack == null)
		{
			op.MovedQuantity = 0;
		}
		else
		{
			if (op.MovedQuantity <= 0)
			{
				return;
			}
			if (op.SinkSlot.Itemstack == null)
			{
				op.SinkSlot.Itemstack = new ItemStack(op.SourceSlot.Itemstack.Collectible, 0);
			}
			if (flag2)
			{
				SetTemperature(op.World, op.SinkSlot.Itemstack, ((float)op.SinkSlot.StackSize * GetTemperature(op.World, op.SinkSlot.Itemstack) + (float)op.MovedQuantity * GetTemperature(op.World, op.SourceSlot.Itemstack)) / (float)(op.SinkSlot.StackSize + op.MovedQuantity));
			}
			if (flag3)
			{
				float num = (float)op.MovedQuantity / (float)(op.MovedQuantity + op.SinkSlot.StackSize);
				TransitionState[] array3 = array;
				foreach (TransitionState transitionState3 in array3)
				{
					TransitionState transitionState4 = dictionary[transitionState3.Props.Type];
					SetTransitionState(op.SinkSlot.Itemstack, transitionState3.Props.Type, transitionState3.TransitionedHours * num + transitionState4.TransitionedHours * (1f - num));
				}
			}
			op.SinkSlot.Itemstack.StackSize += op.MovedQuantity;
			op.SourceSlot.Itemstack.StackSize -= op.MovedQuantity;
			if (op.SourceSlot.Itemstack.StackSize <= 0)
			{
				op.SourceSlot.Itemstack = null;
			}
		}
	}

	/// <summary>
	/// If the item is smeltable, this is the time it takes to smelt at smelting point
	/// </summary>
	/// <param name="world"></param>
	/// <param name="cookingSlotsProvider"></param>
	/// <param name="inputSlot"></param>
	/// <returns></returns>
	public virtual float GetMeltingDuration(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
	{
		return GetCombustibleProperties(world, inputSlot?.Itemstack, null)?.MeltingDuration ?? 0f;
	}

	/// <summary>
	/// If the item is smeltable, this is its melting point
	/// </summary>
	/// <param name="world"></param>
	/// <param name="cookingSlotsProvider"></param>
	/// <param name="inputSlot"></param>
	/// <returns></returns>
	public virtual float GetMeltingPoint(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
	{
		return GetCombustibleProperties(world, inputSlot.Itemstack, null)?.MeltingPoint ?? 0;
	}

	/// <summary>
	/// Should return true if this collectible is smeltable in an open fire
	/// </summary>
	/// <param name="world"></param>
	/// <param name="cookingSlotsProvider"></param>
	/// <param name="inputStack"></param>
	/// <param name="outputStack"></param>
	/// <returns></returns>
	public virtual bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
	{
		CombustibleProperties combustibleProperties = GetCombustibleProperties(world, inputStack, null);
		ItemStack itemStack = combustibleProperties?.SmeltedStack?.ResolvedItemstack;
		if (itemStack != null && inputStack.StackSize >= combustibleProperties.SmeltedRatio && combustibleProperties.MeltingPoint > 0 && (combustibleProperties.SmeltingType != EnumSmeltType.Fire || world.Config.GetString("allowOpenFireFiring").ToBool()))
		{
			if (outputStack != null)
			{
				return outputStack.Collectible.GetMergableQuantity(outputStack, itemStack, EnumMergePriority.AutoMerge) >= itemStack.StackSize;
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// Transform the item to it's smelted variant
	/// </summary>
	/// <param name="world"></param>
	/// <param name="cookingSlotsProvider"></param>
	/// <param name="inputSlot"></param>
	/// <param name="outputSlot"></param>
	public virtual void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
	{
		if (!CanSmelt(world, cookingSlotsProvider, inputSlot.Itemstack, outputSlot.Itemstack))
		{
			return;
		}
		CombustibleProperties combustibleProperties = inputSlot.Itemstack.Collectible.GetCombustibleProperties(world, inputSlot.Itemstack, null);
		ItemStack itemStack = combustibleProperties.SmeltedStack.ResolvedItemstack.Clone();
		TransitionState transitionState = UpdateAndGetTransitionState(world, new DummySlot(inputSlot.Itemstack), EnumTransitionType.Perish);
		if (transitionState != null)
		{
			TransitionState transitionState2 = itemStack.Collectible.UpdateAndGetTransitionState(world, new DummySlot(itemStack), EnumTransitionType.Perish);
			if (transitionState2 != null)
			{
				float val = transitionState.TransitionedHours / (transitionState.TransitionHours + transitionState.FreshHours) * 0.8f * (transitionState2.TransitionHours + transitionState2.FreshHours) - 1f;
				itemStack.Collectible.SetTransitionState(itemStack, EnumTransitionType.Perish, Math.Max(0f, val));
			}
		}
		int num = 1;
		if (outputSlot.Itemstack == null)
		{
			outputSlot.Itemstack = itemStack;
			outputSlot.Itemstack.StackSize = num * itemStack.StackSize;
		}
		else
		{
			itemStack.StackSize = num * itemStack.StackSize;
			ItemStackMergeOperation itemStackMergeOperation = new ItemStackMergeOperation(world, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.ConfirmedMerge, num * itemStack.StackSize);
			itemStackMergeOperation.SourceSlot = new DummySlot(itemStack);
			itemStackMergeOperation.SinkSlot = new DummySlot(outputSlot.Itemstack);
			outputSlot.Itemstack.Collectible.TryMergeStacks(itemStackMergeOperation);
			outputSlot.Itemstack = itemStackMergeOperation.SinkSlot.Itemstack;
		}
		inputSlot.Itemstack.StackSize -= num * combustibleProperties.SmeltedRatio;
		if (inputSlot.Itemstack.StackSize <= 0)
		{
			inputSlot.Itemstack = null;
		}
		outputSlot.MarkDirty();
	}

	/// <summary>
	/// Returns true if the stack can spoil
	/// </summary>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual bool CanSpoil(ItemStack itemstack)
	{
		if (itemstack == null || itemstack.Attributes == null)
		{
			return false;
		}
		if (itemstack.Collectible.GetNutritionProperties(api.World, itemstack, null) != null)
		{
			return itemstack.Attributes.HasAttribute("spoilstate");
		}
		return false;
	}

	/// <summary>
	/// Returns the transition state of given transition type
	/// </summary>
	/// <param name="world"></param>
	/// <param name="inslot"></param>
	/// <param name="type"></param>
	/// <returns></returns>
	public virtual TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot inslot, EnumTransitionType type)
	{
		bool flag = false;
		TransitionState result = null;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			TransitionState transitionState = obj.UpdateAndGetTransitionState(world, inslot, type, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				flag = true;
				result = transitionState;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return result;
			}
		}
		if (flag)
		{
			return result;
		}
		TransitionState[] array = UpdateAndGetTransitionStates(world, inslot);
		if (array == null)
		{
			return null;
		}
		for (int j = 0; j < array.Length; j++)
		{
			if (array[j].Props.Type == type)
			{
				return array[j];
			}
		}
		return null;
	}

	public virtual void SetTransitionState(ItemStack stack, EnumTransitionType type, float transitionedHours)
	{
		ITreeAttribute treeAttribute = (ITreeAttribute)stack.Attributes["transitionstate"];
		if (treeAttribute == null)
		{
			UpdateAndGetTransitionState(api.World, new DummySlot(stack), type);
			treeAttribute = (ITreeAttribute)stack.Attributes["transitionstate"];
		}
		TransitionableProperties[] transitionableProperties = GetTransitionableProperties(api.World, stack, null);
		for (int i = 0; i < transitionableProperties.Length; i++)
		{
			if (transitionableProperties[i].Type == type)
			{
				(treeAttribute["transitionedHours"] as FloatArrayAttribute).value[i] = transitionedHours;
				break;
			}
		}
	}

	public virtual float GetTransitionRateMul(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
	{
		float num = ((inSlot.Inventory == null) ? 1f : inSlot.Inventory.GetTransitionSpeedMul(transType, inSlot.Itemstack));
		if (transType == EnumTransitionType.Perish)
		{
			if (inSlot.Itemstack.Collectible.GetTemperature(world, inSlot.Itemstack) > 75f)
			{
				num = 0f;
			}
			num *= GlobalConstants.PerishSpeedModifier;
		}
		return num;
	}

	/// <summary>
	/// Returns a list of the current transition states of this item, redirects to UpdateAndGetTransitionStatesNative
	/// </summary>
	/// <param name="world"></param>
	/// <param name="inslot"></param>
	/// <returns></returns>
	public virtual TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
	{
		bool flag = false;
		TransitionState[] result = null;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			TransitionState[] array = obj.UpdateAndGetTransitionStates(world, inslot, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				flag = true;
				result = array;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return result;
			}
		}
		if (flag)
		{
			return result;
		}
		return UpdateAndGetTransitionStatesNative(world, inslot);
	}

	/// <summary>
	/// Returns a list of the current transition states of this item. Seperate from UpdateAndGetTransitionStates() so that you can call still call this methods several inheritances down, i.e. there is no base.base.Method() syntax in C#
	/// </summary>
	/// <param name="world"></param>
	/// <param name="inslot"></param>
	/// <returns></returns>
	protected virtual TransitionState[] UpdateAndGetTransitionStatesNative(IWorldAccessor world, ItemSlot inslot)
	{
		if (inslot is ItemSlotCreative)
		{
			return null;
		}
		ItemStack itemstack = inslot.Itemstack;
		TransitionableProperties[] transitionableProperties = GetTransitionableProperties(world, inslot.Itemstack, null);
		if (itemstack == null || transitionableProperties == null || transitionableProperties.Length == 0)
		{
			return null;
		}
		if (itemstack.Attributes == null)
		{
			itemstack.Attributes = new TreeAttribute();
		}
		if (itemstack.Attributes.GetBool("timeFrozen"))
		{
			return null;
		}
		if (!(itemstack.Attributes["transitionstate"] is ITreeAttribute))
		{
			itemstack.Attributes["transitionstate"] = new TreeAttribute();
		}
		ITreeAttribute treeAttribute = (ITreeAttribute)itemstack.Attributes["transitionstate"];
		TransitionState[] array = new TransitionState[transitionableProperties.Length];
		float[] array2;
		float[] array3;
		float[] array4;
		if (!treeAttribute.HasAttribute("createdTotalHours"))
		{
			treeAttribute.SetDouble("createdTotalHours", world.Calendar.TotalHours);
			treeAttribute.SetDouble("lastUpdatedTotalHours", world.Calendar.TotalHours);
			array2 = new float[transitionableProperties.Length];
			array3 = new float[transitionableProperties.Length];
			array4 = new float[transitionableProperties.Length];
			for (int i = 0; i < transitionableProperties.Length; i++)
			{
				array4[i] = 0f;
				array2[i] = transitionableProperties[i].FreshHours.nextFloat(1f, world.Rand);
				array3[i] = transitionableProperties[i].TransitionHours.nextFloat(1f, world.Rand);
			}
			treeAttribute["freshHours"] = new FloatArrayAttribute(array2);
			treeAttribute["transitionHours"] = new FloatArrayAttribute(array3);
			treeAttribute["transitionedHours"] = new FloatArrayAttribute(array4);
		}
		else
		{
			array2 = (treeAttribute["freshHours"] as FloatArrayAttribute).value;
			array3 = (treeAttribute["transitionHours"] as FloatArrayAttribute).value;
			array4 = (treeAttribute["transitionedHours"] as FloatArrayAttribute).value;
			if (transitionableProperties.Length - array2.Length > 0)
			{
				for (int j = array2.Length; j < transitionableProperties.Length; j++)
				{
					array2 = array2.Append(transitionableProperties[j].FreshHours.nextFloat(1f, world.Rand));
					array3 = array3.Append(transitionableProperties[j].TransitionHours.nextFloat(1f, world.Rand));
					array4 = array4.Append(0f);
				}
				(treeAttribute["freshHours"] as FloatArrayAttribute).value = array2;
				(treeAttribute["transitionHours"] as FloatArrayAttribute).value = array3;
				(treeAttribute["transitionedHours"] as FloatArrayAttribute).value = array4;
			}
		}
		double num = treeAttribute.GetDouble("lastUpdatedTotalHours");
		double totalHours = world.Calendar.TotalHours;
		bool flag = false;
		float num2 = (float)(totalHours - num);
		for (int k = 0; k < transitionableProperties.Length; k++)
		{
			TransitionableProperties transitionableProperties2 = transitionableProperties[k];
			if (transitionableProperties2 == null)
			{
				continue;
			}
			float transitionRateMul = GetTransitionRateMul(world, inslot, transitionableProperties2.Type);
			if (num2 > 0.05f)
			{
				float num3 = num2 * transitionRateMul;
				array4[k] += num3;
			}
			float freshHoursLeft = Math.Max(0f, array2[k] - array4[k]);
			float num4 = Math.Max(0f, array4[k] - array2[k]) / array3[k];
			if (num4 > 0f)
			{
				if (transitionableProperties2.Type == EnumTransitionType.Perish)
				{
					flag = true;
				}
				else if (flag)
				{
					continue;
				}
			}
			if (num4 >= 1f && world.Side == EnumAppSide.Server)
			{
				ItemStack itemStack = OnTransitionNow(inslot, transitionableProperties[k]);
				if (itemStack.StackSize <= 0)
				{
					inslot.Itemstack = null;
				}
				else
				{
					itemstack.SetFrom(itemStack);
				}
				inslot.MarkDirty();
				break;
			}
			array[k] = new TransitionState
			{
				FreshHoursLeft = freshHoursLeft,
				TransitionLevel = Math.Min(1f, num4),
				TransitionedHours = array4[k],
				TransitionHours = array3[k],
				FreshHours = array2[k],
				Props = transitionableProperties2
			};
		}
		if (num2 > 0.05f)
		{
			treeAttribute.SetDouble("lastUpdatedTotalHours", totalHours);
		}
		return (from s in array
			where s != null
			orderby (int)s.Props.Type
			select s).ToArray();
	}

	/// <summary>
	/// Called when any of its TransitionableProperties causes the stack to transition to another stack. Default behavior is to return props.TransitionedStack.ResolvedItemstack and set the stack size according to the transition rtio
	/// </summary>
	/// <param name="slot"></param>
	/// <param name="props"></param>
	/// <returns>The stack it should transition into</returns>
	public virtual ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
	{
		bool flag = false;
		ItemStack itemStack = props.TransitionedStack.ResolvedItemstack.Clone();
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior obj in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			ItemStack itemStack2 = obj.OnTransitionNow(slot, props, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				flag = true;
				itemStack = itemStack2;
			}
			if (handling == EnumHandling.PreventSubsequent)
			{
				return itemStack;
			}
		}
		if (flag)
		{
			return itemStack;
		}
		itemStack.StackSize = GameMath.RoundRandom(api.World.Rand, (float)slot.Itemstack.StackSize * props.TransitionRatio);
		return itemStack;
	}

	public static void CarryOverFreshness(ICoreAPI api, ItemSlot inputSlot, ItemStack outputStack, TransitionableProperties perishProps)
	{
		CarryOverFreshness(api, new ItemSlot[1] { inputSlot }, new ItemStack[1] { outputStack }, perishProps);
	}

	public static void CarryOverFreshness(ICoreAPI api, ItemSlot[] inputSlots, ItemStack[] outStacks, TransitionableProperties perishProps)
	{
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		int num4 = 0;
		foreach (ItemSlot itemSlot in inputSlots)
		{
			if (!itemSlot.Empty)
			{
				TransitionState transitionState = itemSlot.Itemstack?.Collectible?.UpdateAndGetTransitionState(api.World, itemSlot, EnumTransitionType.Perish);
				if (transitionState != null)
				{
					num4++;
					float num5 = transitionState.TransitionedHours / (transitionState.TransitionHours + transitionState.FreshHours);
					float num6 = Math.Max(0f, (transitionState.TransitionedHours - transitionState.FreshHours) / transitionState.TransitionHours);
					num2 = Math.Max(num6, num2);
					num += num5;
					num3 += num6;
				}
			}
		}
		num /= (float)Math.Max(1, num4);
		num3 /= (float)Math.Max(1, num4);
		for (int j = 0; j < outStacks.Length; j++)
		{
			if (outStacks[j] != null)
			{
				if (!(outStacks[j].Attributes["transitionstate"] is ITreeAttribute))
				{
					outStacks[j].Attributes["transitionstate"] = new TreeAttribute();
				}
				float num7 = perishProps.TransitionHours.nextFloat(1f, api.World.Rand);
				float num8 = perishProps.FreshHours.nextFloat(1f, api.World.Rand);
				ITreeAttribute treeAttribute = (ITreeAttribute)outStacks[j].Attributes["transitionstate"];
				treeAttribute.SetDouble("createdTotalHours", api.World.Calendar.TotalHours);
				treeAttribute.SetDouble("lastUpdatedTotalHours", api.World.Calendar.TotalHours);
				treeAttribute["freshHours"] = new FloatArrayAttribute(new float[1] { num8 });
				treeAttribute["transitionHours"] = new FloatArrayAttribute(new float[1] { num7 });
				if (num3 > 0f)
				{
					num3 *= 0.6f;
					treeAttribute["transitionedHours"] = new FloatArrayAttribute(new float[1] { num8 + Math.Max(0f, num7 * num3 - 2f) });
				}
				else
				{
					treeAttribute["transitionedHours"] = new FloatArrayAttribute(new float[1] { Math.Max(0f, num * (0.8f + (float)(2 + num4) * num2) * (num7 + num8)) });
				}
			}
		}
	}

	/// <summary>
	/// Test is failed for Perish-able items which have less than 50% of their fresh state remaining (or are already starting to spoil)
	/// </summary>
	/// <param name="world"></param>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual bool IsReasonablyFresh(IWorldAccessor world, ItemStack itemstack)
	{
		if (GetMaxDurability(itemstack) > 1 && (float)GetRemainingDurability(itemstack) / (float)GetMaxDurability(itemstack) < 0.95f)
		{
			return false;
		}
		if (itemstack == null)
		{
			return true;
		}
		TransitionableProperties[] transitionableProperties = GetTransitionableProperties(world, itemstack, null);
		if (transitionableProperties == null)
		{
			return true;
		}
		ITreeAttribute treeAttribute = (ITreeAttribute)itemstack.Attributes["transitionstate"];
		if (treeAttribute == null)
		{
			return true;
		}
		float[] value = (treeAttribute["freshHours"] as FloatArrayAttribute).value;
		float[] value2 = (treeAttribute["transitionedHours"] as FloatArrayAttribute).value;
		for (int i = 0; i < transitionableProperties.Length; i++)
		{
			TransitionableProperties obj = transitionableProperties[i];
			if (obj != null && obj.Type == EnumTransitionType.Perish && value2[i] > value[i] / 2f)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Returns true if the stack has a temperature attribute
	/// </summary>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual bool HasTemperature(IItemStack itemstack)
	{
		if (itemstack == null || itemstack.Attributes == null)
		{
			return false;
		}
		return itemstack.Attributes.HasAttribute("temperature");
	}

	/// <summary>
	/// Returns the stacks item temperature in degree celsius
	/// </summary>
	/// <param name="world"></param>
	/// <param name="itemstack"></param>
	/// <param name="didReceiveHeat">The amount of time it did receive heat since last update/call to this methode</param>
	/// <returns></returns>
	public virtual float GetTemperature(IWorldAccessor world, ItemStack itemstack, double didReceiveHeat)
	{
		if (!(itemstack?.Attributes?["temperature"] is ITreeAttribute))
		{
			return GlobalConstants.CollectibleDefaultTemperature;
		}
		ITreeAttribute treeAttribute = (ITreeAttribute)itemstack.Attributes["temperature"];
		double totalHours = world.Calendar.TotalHours;
		double num = treeAttribute.GetDouble("temperatureLastUpdate");
		double num2 = totalHours - (num + didReceiveHeat);
		float num3 = treeAttribute.GetFloat("temperature", GlobalConstants.CollectibleDefaultTemperature);
		if (num2 > 0.0117647061124444 && num3 > 0f)
		{
			num3 = Math.Max(0f, num3 - Math.Max(0f, (float)(totalHours - num) * treeAttribute.GetFloat("cooldownSpeed", 90f)));
			treeAttribute.SetFloat("temperature", num3);
		}
		treeAttribute.SetDouble("temperatureLastUpdate", totalHours);
		return num3;
	}

	/// <summary>
	/// Returns the stacks item temperature in degree celsius
	/// </summary>
	/// <param name="world"></param>
	/// <param name="itemstack"></param>
	/// <returns></returns>
	public virtual float GetTemperature(IWorldAccessor world, ItemStack itemstack)
	{
		float outTemp = GlobalConstants.CollectibleDefaultTemperature;
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			float temperature = bh.GetTemperature(world, itemstack, ref handling);
			if (handling != EnumHandling.PassThrough)
			{
				outTemp = temperature;
			}
		}, delegate
		{
			if (!(itemstack?.Attributes?["temperature"] is ITreeAttribute))
			{
				outTemp = GlobalConstants.CollectibleDefaultTemperature;
			}
			else
			{
				ITreeAttribute treeAttribute = (ITreeAttribute)itemstack.Attributes["temperature"];
				double totalHours = world.Calendar.TotalHours;
				double num = treeAttribute.GetDecimal("temperatureLastUpdate");
				double num2 = totalHours - num;
				outTemp = (float)treeAttribute.GetDecimal("temperature", GlobalConstants.CollectibleDefaultTemperature);
				if (!itemstack.Attributes.GetBool("timeFrozen") && num2 >= 0.006666666828095913 && outTemp > 0f)
				{
					float num3 = treeAttribute.GetFloat("cooldownSpeed", 120f) * Math.Max(1f, outTemp / 200f);
					outTemp = Math.Max(0f, outTemp - Math.Max(0f, (float)(totalHours - num) * num3));
					treeAttribute.SetFloat("temperature", outTemp);
					treeAttribute.SetDouble("temperatureLastUpdate", totalHours);
				}
			}
		});
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			bh.AfterGetTemperature(world, itemstack, outTemp, ref handling);
		}, delegate
		{
		});
		return outTemp;
	}

	/// <summary>
	/// Sets the stacks item temperature in degree celsius
	/// </summary>
	/// <param name="world"></param>
	/// <param name="itemstack"></param>
	/// <param name="temperature"></param>
	/// <param name="delayCooldown"></param>
	public virtual void SetTemperature(IWorldAccessor world, ItemStack itemstack, float temperature, bool delayCooldown = true)
	{
		if (itemstack == null)
		{
			return;
		}
		ITreeAttribute attr = (ITreeAttribute)itemstack.Attributes["temperature"];
		if (attr == null)
		{
			itemstack.Attributes["temperature"] = (attr = new TreeAttribute());
		}
		WalkBehaviors(delegate(CollectibleBehavior bh, ref EnumHandling handling)
		{
			bh.SetTemperature(world, itemstack, temperature, delayCooldown, ref handling);
		}, delegate
		{
			double num = world.Calendar.TotalHours;
			if (delayCooldown && attr.GetDecimal("temperature") < (double)temperature)
			{
				num += 0.5;
			}
			attr.SetDouble("temperatureLastUpdate", num);
			attr.SetFloat("temperature", temperature);
		});
	}

	/// <summary>
	/// Should return true if given stacks are equal, ignoring their stack size.
	/// </summary>
	/// <param name="thisStack"></param>
	/// <param name="otherStack"></param>
	/// <param name="ignoreAttributeSubTrees"></param>
	/// <returns></returns>
	public virtual bool Equals(ItemStack thisStack, ItemStack otherStack, params string[] ignoreAttributeSubTrees)
	{
		if (thisStack.Class == otherStack.Class && thisStack.Id == otherStack.Id)
		{
			return thisStack.Attributes.Equals(api.World, otherStack.Attributes, ignoreAttributeSubTrees);
		}
		return false;
	}

	/// <summary>
	/// Should return true if thisStack is a satisfactory replacement of otherStack. It's bascially an Equals() test, but it ignores any additional attributes that exist in otherStack
	/// </summary>
	/// <param name="thisStack"></param>
	/// <param name="otherStack"></param>
	/// <returns></returns>
	public virtual bool Satisfies(ItemStack thisStack, ItemStack otherStack)
	{
		if (thisStack.Class == otherStack.Class && thisStack.Id == otherStack.Id)
		{
			return thisStack.Attributes.IsSubSetOf(api.World, otherStack.Attributes);
		}
		return false;
	}

	/// <summary>
	/// This method is for example called by chests when they are being exported as part of a block schematic. Has to store all the currents block/item id mappings so it can be correctly imported again. By default it puts itself into the mapping and searches the itemstack attributes for attributes of type ItemStackAttribute and adds those to the mapping as well.
	/// </summary>
	/// <param name="world"></param>
	/// <param name="inSlot"></param>
	/// <param name="blockIdMapping"></param>
	/// <param name="itemIdMapping"></param>
	public virtual void OnStoreCollectibleMappings(IWorldAccessor world, ItemSlot inSlot, Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
	{
		if (this is Item)
		{
			itemIdMapping[Id] = Code;
		}
		else
		{
			blockIdMapping[Id] = Code;
		}
		OnStoreCollectibleMappings(world, inSlot.Itemstack.Attributes, blockIdMapping, itemIdMapping);
		ITreeAttribute obj = inSlot.Itemstack.Attributes["temperature"] as ITreeAttribute;
		if (obj != null && obj.HasAttribute("temperatureLastUpdate"))
		{
			GetTemperature(world, inSlot.Itemstack);
		}
	}

	/// <summary>
	/// This method is called after a block/item like this has been imported as part of a block schematic. Has to restore fix the block/item id mappings as they are probably different compared to the world from where they were exported. By default iterates over all the itemstacks attributes and searches for attribute sof type ItenStackAttribute and calls .FixMapping() on them.
	/// </summary>
	/// <param name="worldForResolve"></param>
	/// <param name="inSlot"></param>
	/// <param name="oldBlockIdMapping"></param>
	/// <param name="oldItemIdMapping"></param>
	[Obsolete("Use the variant with resolveImports parameter")]
	public virtual void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, ItemSlot inSlot, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
	{
		OnLoadCollectibleMappings(worldForResolve, inSlot, oldBlockIdMapping, oldItemIdMapping, resolveImports: true);
	}

	/// <summary>
	/// This method is called after a block/item like this has been imported as part of a block schematic. Has to restore fix the block/item id mappings as they are probably different compared to the world from where they were exported. By default iterates over all the itemstacks attributes and searches for attribute sof type ItenStackAttribute and calls .FixMapping() on them.
	/// </summary>
	/// <param name="worldForResolve"></param>
	/// <param name="inSlot"></param>
	/// <param name="oldBlockIdMapping"></param>
	/// <param name="oldItemIdMapping"></param>
	/// <param name="resolveImports">Turn it off to spawn structures as they are. For example, in this mode, instead of traders, their meta spawners will spawn</param>
	public virtual void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, ItemSlot inSlot, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, bool resolveImports)
	{
		OnLoadCollectibleMappings(worldForResolve, inSlot.Itemstack.Attributes, oldBlockIdMapping, oldItemIdMapping);
	}

	private void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, ITreeAttribute tree, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
	{
		foreach (KeyValuePair<string, IAttribute> item in tree)
		{
			if (item.Value is ITreeAttribute tree2)
			{
				OnLoadCollectibleMappings(worldForResolve, tree2, oldBlockIdMapping, oldItemIdMapping);
			}
			else if (item.Value is ItemstackAttribute { value: var value } itemstackAttribute)
			{
				if (value != null && !value.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
				{
					itemstackAttribute.value = null;
				}
				else
				{
					value?.Collectible.OnLoadCollectibleMappings(worldForResolve, value.Attributes, oldBlockIdMapping, oldItemIdMapping);
				}
			}
		}
		if (tree.HasAttribute("temperatureLastUpdate"))
		{
			tree.SetDouble("temperatureLastUpdate", worldForResolve.Calendar.TotalHours);
		}
		if (tree.HasAttribute("createdTotalHours"))
		{
			double num = tree.GetDouble("createdTotalHours");
			double num2 = tree.GetDouble("lastUpdatedTotalHours") - num;
			tree.SetDouble("lastUpdatedTotalHours", worldForResolve.Calendar.TotalHours);
			tree.SetDouble("createdTotalHours", worldForResolve.Calendar.TotalHours - num2);
		}
	}

	private void OnStoreCollectibleMappings(IWorldAccessor world, ITreeAttribute tree, Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
	{
		foreach (KeyValuePair<string, IAttribute> item in tree)
		{
			if (item.Value is ITreeAttribute tree2)
			{
				OnStoreCollectibleMappings(world, tree2, blockIdMapping, itemIdMapping);
			}
			else if (item.Value is ItemstackAttribute { value: { } value })
			{
				if (value.Collectible == null)
				{
					value.ResolveBlockOrItem(world);
				}
				if (value.Class == EnumItemClass.Item)
				{
					itemIdMapping[value.Id] = value.Collectible?.Code;
				}
				else
				{
					blockIdMapping[value.Id] = value.Collectible?.Code;
				}
			}
		}
	}

	/// <summary>
	/// Should return a random pixel within the items/blocks texture
	/// </summary>
	/// <param name="capi"></param>
	/// <param name="stack"></param>
	/// <returns></returns>
	public virtual int GetRandomColor(ICoreClientAPI capi, ItemStack stack)
	{
		return 0;
	}

	/// <summary>
	/// Returns true if this blocks matterstate is liquid.  (Liquid blocks should also implement IBlockFlowing)
	/// <br />
	/// IMPORTANT: Calling code should have looked up the block using IBlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid)
	/// </summary>
	/// <returns></returns>
	public virtual bool IsLiquid()
	{
		return MatterState == EnumMatterState.Liquid;
	}

	protected void WalkBehaviors(CollectibleBehaviorDelegate onBehavior, Action defaultAction)
	{
		bool flag = true;
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		foreach (CollectibleBehavior behavior in collectibleBehaviors)
		{
			EnumHandling handling = EnumHandling.PassThrough;
			onBehavior(behavior, ref handling);
			switch (handling)
			{
			case EnumHandling.PreventSubsequent:
				return;
			case EnumHandling.PreventDefault:
				flag = false;
				break;
			}
		}
		if (flag)
		{
			defaultAction();
		}
	}

	/// <summary>
	/// Returns the blocks behavior of given type, if it has such behavior
	/// </summary>
	/// <param name="type"></param>
	/// <param name="withInheritance"></param>
	/// <returns></returns>
	public CollectibleBehavior GetCollectibleBehavior(Type type, bool withInheritance)
	{
		return GetBehavior(CollectibleBehaviors, type, withInheritance);
	}

	public T GetCollectibleBehavior<T>(bool withInheritance) where T : CollectibleBehavior
	{
		return GetBehavior(CollectibleBehaviors, typeof(T), withInheritance) as T;
	}

	protected virtual CollectibleBehavior GetBehavior(CollectibleBehavior[] fromList, Type type, bool withInheritance)
	{
		if (withInheritance)
		{
			for (int i = 0; i < fromList.Length; i++)
			{
				Type type2 = fromList[i].GetType();
				if (type2 == type || type.IsAssignableFrom(type2))
				{
					return fromList[i];
				}
			}
			return null;
		}
		for (int j = 0; j < fromList.Length; j++)
		{
			if (fromList[j].GetType() == type)
			{
				return fromList[j];
			}
		}
		return null;
	}

	/// <summary>
	/// Returns instance of class that implements this interface in the following order<br />
	/// 1. Collectible (returns itself)<br />
	/// 2. CollectibleBlockBehavior (returns on of our own behavior)<br />
	/// </summary>
	/// <returns></returns>
	public virtual T GetCollectibleInterface<T>() where T : class
	{
		if (this is T result)
		{
			return result;
		}
		CollectibleBehavior collectibleBehavior = GetCollectibleBehavior(typeof(T), withInheritance: true);
		if (collectibleBehavior != null)
		{
			return collectibleBehavior as T;
		}
		return null;
	}

	/// <summary>
	/// Returns true if the block has given behavior
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="withInheritance"></param>
	/// <returns></returns>
	public virtual bool HasBehavior<T>(bool withInheritance = false) where T : CollectibleBehavior
	{
		return (T)GetCollectibleBehavior(typeof(T), withInheritance) != null;
	}

	/// <summary>
	/// Returns true if the block has given behavior
	/// </summary>
	/// <param name="type"></param>
	/// <param name="withInheritance"></param>
	/// <returns></returns>
	public virtual bool HasBehavior(Type type, bool withInheritance = false)
	{
		return GetCollectibleBehavior(type, withInheritance) != null;
	}

	/// <summary>
	/// Returns true if the block has given behavior
	/// </summary>
	/// <param name="type"></param>
	/// <param name="classRegistry"></param>
	/// <returns></returns>
	public virtual bool HasBehavior(string type, IClassRegistryAPI classRegistry)
	{
		return GetBehavior(classRegistry.GetBlockBehaviorClass(type)) != null;
	}

	/// <summary>
	/// Returns the blocks behavior of given type, if it has such behavior
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
	public CollectibleBehavior GetBehavior(Type type)
	{
		return GetCollectibleBehavior(type, withInheritance: false);
	}

	/// <summary>
	/// Returns the blocks behavior of given type, if it has such behavior
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public T GetBehavior<T>() where T : CollectibleBehavior
	{
		return (T)GetCollectibleBehavior(typeof(T), withInheritance: false);
	}

	/// <summary>
	/// Called immediately prior to a firepit or similar testing whether this Collectible can be smelted
	/// <br />Returns true if the caller should be marked dirty
	/// </summary>
	/// <param name="inventorySmelting"></param>
	public virtual bool OnSmeltAttempt(InventoryBase inventorySmelting)
	{
		return false;
	}

	[Obsolete]
	public static bool IsEmptyBackPack(IItemStack itemstack)
	{
		if (!IsBackPack(itemstack))
		{
			return false;
		}
		ITreeAttribute treeAttribute = itemstack.Attributes.GetTreeAttribute("backpack");
		if (treeAttribute == null)
		{
			return true;
		}
		foreach (KeyValuePair<string, IAttribute> item in treeAttribute.GetTreeAttribute("slots"))
		{
			IItemStack itemStack = (IItemStack)(item.Value?.GetValue());
			if (itemStack != null && itemStack.StackSize > 0)
			{
				return false;
			}
		}
		return true;
	}

	[Obsolete]
	public static bool IsBackPack(IItemStack itemstack)
	{
		if (itemstack == null || itemstack.Collectible.Attributes == null)
		{
			return false;
		}
		return itemstack.Collectible.Attributes["backpack"]["quantitySlots"].AsInt() > 0;
	}

	[Obsolete]
	public static int QuantityBackPackSlots(IItemStack itemstack)
	{
		if (itemstack == null || itemstack.Collectible.Attributes == null)
		{
			return 0;
		}
		return itemstack.Collectible.Attributes["backpack"]["quantitySlots"].AsInt();
	}
}
