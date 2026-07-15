using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BlockEntityFirepit : BlockEntityOpenableContainer, IHeatSource, IFirePit, ITemperatureSensitive
{
	internal InventorySmelting inventory;

	public float prevFurnaceTemperature = 20f;

	public float furnaceTemperature = 20f;

	public int maxTemperature;

	public float inputStackCookingTime;

	public float fuelBurnTime;

	public float maxFuelBurnTime;

	public float smokeLevel;

	public bool canIgniteFuel;

	public float cachedFuel;

	public double extinguishedTotalHours;

	private GuiDialogBlockEntityFirepit clientDialog;

	private bool clientSidePrevBurning;

	private FirepitContentsRenderer renderer;

	private bool shouldRedraw;

	public float emptyFirepitBurnTimeMulBonus = 4f;

	private ICoreClientAPI capi;

	public bool IsHot => IsBurning;

	public virtual bool BurnsAllFuell => true;

	public virtual float HeatModifier => 1f;

	public virtual float BurnDurationModifier => 1f;

	public override string InventoryClassName => "stove";

	public virtual string DialogTitle => Lang.Get("Firepit", Array.Empty<object>());

	public override InventoryBase Inventory => (InventoryBase)(object)inventory;

	public bool IsSmoldering => canIgniteFuel;

	public bool IsBurning => fuelBurnTime > 0f;

	public float InputStackTemp
	{
		get
		{
			return GetTemp(inputStack);
		}
		set
		{
			SetTemp(inputStack, value);
		}
	}

	public float OutputStackTemp
	{
		get
		{
			return GetTemp(outputStack);
		}
		set
		{
			SetTemp(outputStack, value);
		}
	}

	public ItemSlot fuelSlot => ((InventoryBase)inventory)[0];

	public ItemSlot inputSlot => ((InventoryBase)inventory)[1];

	public ItemSlot outputSlot => ((InventoryBase)inventory)[2];

	public ItemSlot[] otherCookingSlots => inventory.CookingSlots;

	public ItemStack fuelStack
	{
		get
		{
			return ((InventoryBase)inventory)[0].Itemstack;
		}
		set
		{
			((InventoryBase)inventory)[0].Itemstack = value;
			((InventoryBase)inventory)[0].MarkDirty();
		}
	}

	public ItemStack inputStack
	{
		get
		{
			return ((InventoryBase)inventory)[1].Itemstack;
		}
		set
		{
			((InventoryBase)inventory)[1].Itemstack = value;
			((InventoryBase)inventory)[1].MarkDirty();
		}
	}

	public ItemStack outputStack
	{
		get
		{
			return ((InventoryBase)inventory)[2].Itemstack;
		}
		set
		{
			((InventoryBase)inventory)[2].Itemstack = value;
			((InventoryBase)inventory)[2].MarkDirty();
		}
	}

	public CombustibleProperties fuelCombustibleOpts => getCombustibleOpts(0);

	public EnumFirepitModel CurrentModel { get; private set; }

	public virtual int enviromentTemperature()
	{
		return 20;
	}

	public virtual float maxCookingTime()
	{
		if (inputSlot.Itemstack != null)
		{
			return inputSlot.Itemstack.Collectible.GetMeltingDuration(((BlockEntity)this).Api.World, (ISlotProvider)(object)inventory, inputSlot);
		}
		return 30f;
	}

	public BlockEntityFirepit()
	{
		inventory = new InventorySmelting(null, null);
		((InventoryBase)inventory).SlotModified += OnSlotModified;
	}

	public override void Initialize(ICoreAPI api)
	{
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Expected O, but got Unknown
		base.Initialize(api);
		inventory.pos = ((BlockEntity)this).Pos;
		((InventoryBase)inventory).LateInitialize("smelting-" + ((BlockEntity)this).Pos.X + "/" + ((BlockEntity)this).Pos.Y + "/" + ((BlockEntity)this).Pos.Z, api);
		((BlockEntity)this).RegisterGameTickListener((Action<float>)OnBurnTick, 100, 0);
		((BlockEntity)this).RegisterGameTickListener((Action<float>)On500msTick, 500, 0);
		ICoreClientAPI val = (ICoreClientAPI)(object)((api is ICoreClientAPI) ? api : null);
		if (val != null)
		{
			capi = val;
			renderer = new FirepitContentsRenderer(val, ((BlockEntity)this).Pos);
			val.Event.RegisterRenderer((IRenderer)(object)renderer, (EnumRenderStage)1, "firepit");
			((IEventAPI)val.Event).RegisterEventBusListener(new EventBusListenerDelegate(OnSetTransform), 0.5, "onsettransform");
			UpdateRenderer();
		}
	}

	protected void OnSetTransform(string eventName, ref EnumHandling handling, IAttribute data)
	{
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Expected O, but got Unknown
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Expected O, but got Unknown
		if (((TreeAttribute)((data is TreeAttribute) ? data : null)).GetString("target", (string)null) != "infirepitTransform" || ((BlockEntity)this).Pos.DistanceTo(((Entity)((IPlayer)capi.World.Player).Entity).Pos.XYZ) > 20f)
		{
			return;
		}
		CollectibleObject collectible = ((IPlayer)capi.World.Player).InventoryManager.ActiveHotbarSlot.Itemstack.Collectible;
		ModelTransform val = ModelTransform.CreateFromTreeAttribute((TreeAttribute)(object)((data is TreeAttribute) ? data : null));
		if (collectible != null)
		{
			JsonObject attributes = collectible.Attributes;
			if (((attributes != null) ? new bool?(attributes.KeyExists("inFirePitProps")) : ((bool?)null)) == true)
			{
				goto IL_00f5;
			}
		}
		if (collectible.Attributes == null)
		{
			collectible.Attributes = new JsonObject((JToken)new JObject());
		}
		collectible.Attributes.Token[(object)"inFirePitProps"][(object)"transform"] = JToken.FromObject((object)val);
		goto IL_00f5;
		IL_00f5:
		renderer.Transform = val;
	}

	private void OnSlotModified(int slotid)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Invalid comparison between Unknown and I4
		((BlockEntity)this).Block = ((BlockEntity)this).Api.World.BlockAccessor.GetBlock(((BlockEntity)this).Pos);
		UpdateRenderer();
		((BlockEntity)this).MarkDirty((int)((BlockEntity)this).Api.Side == 1, (IPlayer)null);
		shouldRedraw = true;
		if (((BlockEntity)this).Api is ICoreClientAPI && clientDialog != null)
		{
			SetDialogValues(((GuiDialogGeneric)clientDialog).Attributes);
		}
		IWorldChunk chunkAtBlockPos = ((BlockEntity)this).Api.World.BlockAccessor.GetChunkAtBlockPos(((BlockEntity)this).Pos);
		if (chunkAtBlockPos != null)
		{
			chunkAtBlockPos.MarkModified();
		}
	}

	private void On500msTick(float dt)
	{
		if (((BlockEntity)this).Api is ICoreServerAPI && (IsBurning || prevFurnaceTemperature != furnaceTemperature))
		{
			((BlockEntity)this).MarkDirty(false, (IPlayer)null);
		}
		prevFurnaceTemperature = furnaceTemperature;
	}

	private void OnBurnTick(float dt)
	{
		if (((RegistryObject)((BlockEntity)this).Block).Code.Path.Contains("construct"))
		{
			return;
		}
		if (((BlockEntity)this).Api is ICoreClientAPI)
		{
			renderer?.contentStackRenderer?.OnUpdate(InputStackTemp);
			return;
		}
		if (fuelBurnTime > 0f)
		{
			bool flag = Math.Abs(furnaceTemperature - (float)maxTemperature) < 50f && inputSlot.Empty;
			fuelBurnTime -= dt / (flag ? emptyFirepitBurnTimeMulBonus : 1f);
			if (fuelBurnTime <= 0f)
			{
				fuelBurnTime = 0f;
				maxFuelBurnTime = 0f;
				if (!canSmelt())
				{
					setBlockState("extinct");
					extinguishedTotalHours = ((BlockEntity)this).Api.World.Calendar.TotalHours;
				}
			}
		}
		if (!IsBurning && ((RegistryObject)((BlockEntity)this).Block).Variant["burnstate"] == "extinct" && ((BlockEntity)this).Api.World.Calendar.TotalHours - extinguishedTotalHours > 2.0)
		{
			canIgniteFuel = false;
			setBlockState("cold");
		}
		if (IsBurning)
		{
			furnaceTemperature = changeTemperature(furnaceTemperature, maxTemperature, dt);
		}
		if (canHeatInput())
		{
			heatInput(dt);
		}
		else
		{
			inputStackCookingTime = 0f;
		}
		if (canHeatOutput())
		{
			heatOutput(dt);
		}
		if (canSmeltInput() && inputStackCookingTime > maxCookingTime())
		{
			smeltItems();
		}
		if (!IsBurning && canIgniteFuel && canSmelt())
		{
			igniteFuel();
		}
		if (!IsBurning)
		{
			furnaceTemperature = changeTemperature(furnaceTemperature, enviromentTemperature(), dt);
		}
	}

	public EnumIgniteState GetIgnitableState(float secondsIgniting)
	{
		if (fuelSlot.Empty)
		{
			return EnumIgniteState.NotIgnitablePreventDefault;
		}
		if (IsBurning)
		{
			return EnumIgniteState.NotIgnitablePreventDefault;
		}
		if (!(secondsIgniting > 3f))
		{
			return EnumIgniteState.Ignitable;
		}
		return EnumIgniteState.IgniteNow;
	}

	public float changeTemperature(float fromTemp, float toTemp, float dt)
	{
		float num = Math.Abs(fromTemp - toTemp);
		dt += dt * (num / 28f);
		if (num < dt)
		{
			return toTemp;
		}
		if (fromTemp > toTemp)
		{
			dt = 0f - dt;
		}
		if (Math.Abs(fromTemp - toTemp) < 1f)
		{
			return toTemp;
		}
		return fromTemp + dt;
	}

	private bool canSmelt()
	{
		CombustibleProperties val = fuelCombustibleOpts;
		if (val == null)
		{
			return false;
		}
		bool flag = canHeatInput();
		if (BurnsAllFuell || flag)
		{
			return (float)val.BurnTemperature * HeatModifier > 0f;
		}
		return false;
	}

	public void heatInput(float dt)
	{
		float inputStackTemp = InputStackTemp;
		float num = inputStackTemp;
		float meltingPoint = inputSlot.Itemstack.Collectible.GetMeltingPoint(((BlockEntity)this).Api.World, (ISlotProvider)(object)inventory, inputSlot);
		float num2 = inputSlot.Itemstack.StackSize;
		if (inputStackTemp < furnaceTemperature)
		{
			float num3 = (1f + GameMath.Clamp((furnaceTemperature - inputStackTemp) / 30f, 0f, 1.6f)) * dt;
			if (num >= meltingPoint)
			{
				num3 /= 11f;
			}
			float num4 = changeTemperature(inputStackTemp, furnaceTemperature, num3);
			num4 = (num4 + (num2 - 1f) * inputStackTemp) / num2;
			int val = inputStack.Collectible.GetCombustibleProperties(((BlockEntity)this).Api.World, inputStack, (BlockPos)null)?.MaxTemperature ?? 0;
			JsonObject itemAttributes = inputStack.ItemAttributes;
			int num5 = Math.Max(val, (((itemAttributes != null) ? itemAttributes["maxTemperature"] : null) != null) ? inputStack.ItemAttributes["maxTemperature"].AsInt(0) : 0);
			if (num5 > 0)
			{
				num4 = Math.Min(num5, num4);
			}
			if (inputStackTemp != num4)
			{
				InputStackTemp = num4;
				num = num4;
			}
		}
		if (num >= meltingPoint)
		{
			float num6 = num / meltingPoint;
			inputStackCookingTime += (float)GameMath.Clamp((int)num6, 1, 30) * dt;
		}
		else if (inputStackCookingTime > 0f)
		{
			inputStackCookingTime -= 1f;
		}
	}

	public void heatOutput(float dt)
	{
		float outputStackTemp = OutputStackTemp;
		if (outputStackTemp < furnaceTemperature)
		{
			float num = changeTemperature(outputStackTemp, furnaceTemperature, 2f * dt);
			int val = outputStack.Collectible.GetCombustibleProperties(((BlockEntity)this).Api.World, outputStack, (BlockPos)null)?.MaxTemperature ?? 0;
			JsonObject itemAttributes = outputStack.ItemAttributes;
			int num2 = Math.Max(val, (((itemAttributes != null) ? itemAttributes["maxTemperature"] : null) != null) ? outputStack.ItemAttributes["maxTemperature"].AsInt(0) : 0);
			if (num2 > 0)
			{
				num = Math.Min(num2, num);
			}
			if (outputStackTemp != num)
			{
				OutputStackTemp = num;
			}
		}
	}

	public void CoolNow(float amountRel, OnStackToCool onStackToCoolCallback)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		((BlockEntity)this).Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), ((BlockEntity)this).Pos, -0.5, (IPlayer)null, false, 16f, 1f);
		fuelBurnTime -= amountRel / 10f;
		if (((BlockEntity)this).Api.World.Rand.NextDouble() < (double)(amountRel / 5f) || fuelBurnTime <= 0f)
		{
			setBlockState("cold");
			extinguishedTotalHours = -99.0;
			canIgniteFuel = false;
			fuelBurnTime = 0f;
			maxFuelBurnTime = 0f;
		}
		((BlockEntity)this).MarkDirty(true, (IPlayer)null);
	}

	private float GetTemp(ItemStack stack)
	{
		if (stack == null)
		{
			return enviromentTemperature();
		}
		if (inventory.CookingSlots.Length != 0)
		{
			bool flag = false;
			float num = 0f;
			for (int i = 0; i < inventory.CookingSlots.Length; i++)
			{
				ItemStack itemstack = inventory.CookingSlots[i].Itemstack;
				if (itemstack != null)
				{
					float temperature = itemstack.Collectible.GetTemperature(((BlockEntity)this).Api.World, itemstack);
					num = (flag ? Math.Min(num, temperature) : temperature);
					flag = true;
				}
			}
			return num;
		}
		return stack.Collectible.GetTemperature(((BlockEntity)this).Api.World, stack);
	}

	private void SetTemp(ItemStack stack, float value)
	{
		if (stack == null)
		{
			return;
		}
		if (inventory.CookingSlots.Length != 0)
		{
			for (int i = 0; i < inventory.CookingSlots.Length; i++)
			{
				ItemStack itemstack = inventory.CookingSlots[i].Itemstack;
				if (itemstack != null)
				{
					itemstack.Collectible.SetTemperature(((BlockEntity)this).Api.World, inventory.CookingSlots[i].Itemstack, value, true);
				}
			}
		}
		else
		{
			stack.Collectible.SetTemperature(((BlockEntity)this).Api.World, stack, value, true);
		}
	}

	public void igniteFuel()
	{
		igniteWithFuel((IItemStack)(object)fuelStack);
		ItemStack obj = fuelStack;
		obj.StackSize -= 1;
		if (fuelStack.StackSize <= 0)
		{
			fuelStack = null;
		}
	}

	public void igniteWithFuel(IItemStack stack)
	{
		CombustibleProperties combustibleProperties = stack.Collectible.GetCombustibleProperties(((BlockEntity)this).Api.World, (ItemStack)(object)((stack is ItemStack) ? stack : null), (BlockPos)null);
		maxFuelBurnTime = (fuelBurnTime = combustibleProperties.BurnDuration * BurnDurationModifier);
		maxTemperature = (int)((float)combustibleProperties.BurnTemperature * HeatModifier);
		smokeLevel = combustibleProperties.SmokeLevel;
		setBlockState("lit");
		((BlockEntity)this).MarkDirty(true, (IPlayer)null);
	}

	public void setBlockState(string state)
	{
		AssetLocation val = ((RegistryObject)((BlockEntity)this).Block).CodeWithVariant("burnstate", state);
		Block block = ((BlockEntity)this).Api.World.GetBlock(val);
		if (block != null)
		{
			((BlockEntity)this).Api.World.BlockAccessor.ExchangeBlock(((CollectibleObject)block).Id, ((BlockEntity)this).Pos);
			((BlockEntity)this).Block = block;
		}
	}

	public bool canHeatInput()
	{
		if (!canSmeltInput())
		{
			ItemStack obj = inputStack;
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				JsonObject itemAttributes = obj.ItemAttributes;
				obj2 = ((itemAttributes != null) ? itemAttributes["allowHeating"] : null);
			}
			if (obj2 != null)
			{
				return inputStack.ItemAttributes["allowHeating"].AsBool(false);
			}
			return false;
		}
		return true;
	}

	public bool canHeatOutput()
	{
		ItemStack obj = outputStack;
		object obj2;
		if (obj == null)
		{
			obj2 = null;
		}
		else
		{
			JsonObject itemAttributes = obj.ItemAttributes;
			obj2 = ((itemAttributes != null) ? itemAttributes["allowHeating"] : null);
		}
		if (obj2 != null)
		{
			return outputStack.ItemAttributes["allowHeating"].AsBool(false);
		}
		return false;
	}

	public bool canSmeltInput()
	{
		if (inputStack == null)
		{
			return false;
		}
		if (inputStack.Collectible.OnSmeltAttempt((InventoryBase)(object)inventory))
		{
			((BlockEntity)this).MarkDirty(true, (IPlayer)null);
		}
		CombustibleProperties combustibleProperties = inputStack.Collectible.GetCombustibleProperties(((BlockEntity)this).Api.World, inputStack, (BlockPos)null);
		if (inputStack.Collectible.CanSmelt(((BlockEntity)this).Api.World, (ISlotProvider)(object)inventory, inputSlot.Itemstack, outputSlot.Itemstack))
		{
			if (combustibleProperties != null)
			{
				return !combustibleProperties.RequiresContainer;
			}
			return true;
		}
		return false;
	}

	public void smeltItems()
	{
		inputStack.Collectible.DoSmelt(((BlockEntity)this).Api.World, (ISlotProvider)(object)inventory, inputSlot, outputSlot);
		inputStackCookingTime = 0f;
		((BlockEntity)this).MarkDirty(true, (IPlayer)null);
		inputSlot.MarkDirty();
	}

	public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Invalid comparison between Unknown and I4
		if ((int)((BlockEntity)this).Api.Side == 2)
		{
			toggleInventoryDialogClient(byPlayer, delegate
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Expected O, but got Unknown
				SyncedTreeAttribute val = new SyncedTreeAttribute();
				SetDialogValues((ITreeAttribute)(object)val);
				string dialogTitle = DialogTitle;
				InventoryBase obj = Inventory;
				BlockPos pos = ((BlockEntity)this).Pos;
				ICoreAPI api = ((BlockEntity)this).Api;
				clientDialog = new GuiDialogBlockEntityFirepit(dialogTitle, obj, pos, val, (ICoreClientAPI)(object)((api is ICoreClientAPI) ? api : null));
				return (GuiDialogBlockEntity)(object)clientDialog;
			});
		}
		return true;
	}

	public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
	{
		base.OnReceivedClientPacket(player, packetid, data);
	}

	public override void OnReceivedServerPacket(int packetid, byte[] data)
	{
		if (packetid == 1001)
		{
			IWorldAccessor world = ((BlockEntity)this).Api.World;
			((IPlayer)((IClientWorldAccessor)((world is IClientWorldAccessor) ? world : null)).Player).InventoryManager.CloseInventory((IInventory)(object)Inventory);
			GuiDialogBlockEntity obj = invDialog;
			if (obj != null)
			{
				((GuiDialog)obj).TryClose();
			}
			GuiDialogBlockEntity obj2 = invDialog;
			if (obj2 != null)
			{
				((GuiDialog)obj2).Dispose();
			}
			invDialog = null;
		}
	}

	public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
	{
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Invalid comparison between Unknown and I4
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Invalid comparison between Unknown and I4
		base.FromTreeAttributes(tree, worldForResolving);
		if (((BlockEntity)this).Api != null)
		{
			Inventory.AfterBlocksLoaded(((BlockEntity)this).Api.World);
		}
		furnaceTemperature = tree.GetFloat("furnaceTemperature", 0f);
		maxTemperature = tree.GetInt("maxTemperature", 0);
		inputStackCookingTime = tree.GetFloat("oreCookingTime", 0f);
		fuelBurnTime = tree.GetFloat("fuelBurnTime", 0f);
		maxFuelBurnTime = tree.GetFloat("maxFuelBurnTime", 0f);
		extinguishedTotalHours = tree.GetDouble("extinguishedTotalHours", 0.0);
		canIgniteFuel = tree.GetBool("canIgniteFuel", true);
		cachedFuel = tree.GetFloat("cachedFuel", 0f);
		ICoreAPI api = ((BlockEntity)this).Api;
		if (api != null && (int)api.Side == 2)
		{
			UpdateRenderer();
			if (clientDialog != null)
			{
				SetDialogValues(((GuiDialogGeneric)clientDialog).Attributes);
			}
		}
		ICoreAPI api2 = ((BlockEntity)this).Api;
		if (api2 != null && (int)api2.Side == 2 && (clientSidePrevBurning != IsBurning || shouldRedraw))
		{
			((BlockEntity)this).GetBehavior<BEBehaviorFirepitAmbient>()?.ToggleAmbientSounds(IsBurning);
			clientSidePrevBurning = IsBurning;
			((BlockEntity)this).MarkDirty(true, (IPlayer)null);
			shouldRedraw = false;
		}
	}

	private void UpdateRenderer()
	{
		if (renderer == null)
		{
			return;
		}
		ItemStack val = ((inputStack == null) ? outputStack : inputStack);
		if (renderer.ContentStack != null && renderer.contentStackRenderer != null && ((val != null) ? val.Collectible : null) is IInFirepitRendererSupplier && renderer.ContentStack.Equals(((BlockEntity)this).Api.World, val, GlobalConstants.IgnoredStackAttributes))
		{
			return;
		}
		renderer.contentStackRenderer?.Dispose();
		renderer.contentStackRenderer = null;
		if (((val != null) ? val.Collectible : null) is IInFirepitRendererSupplier)
		{
			IInFirepitRenderer rendererWhenInFirepit = (((val != null) ? val.Collectible : null) as IInFirepitRendererSupplier).GetRendererWhenInFirepit(val, this, val == outputStack);
			if (rendererWhenInFirepit != null)
			{
				renderer.SetChildRenderer(val, rendererWhenInFirepit);
				return;
			}
		}
		InFirePitProps renderProps = GetRenderProps(val);
		if (((val != null) ? val.Collectible : null) != null && !(((val != null) ? val.Collectible : null) is IInFirepitMeshSupplier) && renderProps != null)
		{
			renderer.SetContents(val, renderProps.Transform);
		}
		else
		{
			renderer.SetContents(null, null);
		}
	}

	private void SetDialogValues(ITreeAttribute dialogTree)
	{
		dialogTree.SetFloat("furnaceTemperature", furnaceTemperature);
		dialogTree.SetInt("maxTemperature", maxTemperature);
		dialogTree.SetFloat("oreCookingTime", inputStackCookingTime);
		dialogTree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
		dialogTree.SetFloat("fuelBurnTime", fuelBurnTime);
		if (inputSlot.Itemstack != null)
		{
			float meltingDuration = inputSlot.Itemstack.Collectible.GetMeltingDuration(((BlockEntity)this).Api.World, (ISlotProvider)(object)inventory, inputSlot);
			dialogTree.SetFloat("oreTemperature", InputStackTemp);
			dialogTree.SetFloat("maxOreCookingTime", meltingDuration);
		}
		else
		{
			dialogTree.RemoveAttribute("oreTemperature");
		}
		dialogTree.SetString("outputText", inventory.GetOutputText());
		dialogTree.SetInt("haveCookingContainer", inventory.HaveCookingContainer ? 1 : 0);
		dialogTree.SetInt("quantityCookingSlots", inventory.CookingSlots.Length);
	}

	public override void ToTreeAttributes(ITreeAttribute tree)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Expected O, but got Unknown
		base.ToTreeAttributes(tree);
		ITreeAttribute val = (ITreeAttribute)new TreeAttribute();
		Inventory.ToTreeAttributes(val);
		tree["inventory"] = (IAttribute)(object)val;
		tree.SetFloat("furnaceTemperature", furnaceTemperature);
		tree.SetInt("maxTemperature", maxTemperature);
		tree.SetFloat("oreCookingTime", inputStackCookingTime);
		tree.SetFloat("fuelBurnTime", fuelBurnTime);
		tree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
		tree.SetDouble("extinguishedTotalHours", extinguishedTotalHours);
		tree.SetBool("canIgniteFuel", canIgniteFuel);
		tree.SetFloat("cachedFuel", cachedFuel);
	}

	public override void OnBlockRemoved()
	{
		base.OnBlockRemoved();
		renderer?.Dispose();
		renderer = null;
		if (clientDialog != null)
		{
			((GuiDialog)clientDialog).TryClose();
			GuiDialogBlockEntityFirepit guiDialogBlockEntityFirepit = clientDialog;
			if (guiDialogBlockEntityFirepit != null)
			{
				((GuiDialog)guiDialogBlockEntityFirepit).Dispose();
			}
			clientDialog = null;
		}
	}

	public CombustibleProperties getCombustibleOpts(int slotid)
	{
		ItemSlot val = ((InventoryBase)inventory)[slotid];
		if (val.Itemstack == null)
		{
			return null;
		}
		return val.Itemstack.Collectible.GetCombustibleProperties(((BlockEntity)this).Api.World, val.Itemstack, (BlockPos)null);
	}

	public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Invalid comparison between Unknown and I4
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Invalid comparison between Unknown and I4
		foreach (ItemSlot item in Inventory)
		{
			if (item.Itemstack != null)
			{
				if ((int)item.Itemstack.Class == 1)
				{
					itemIdMapping[((CollectibleObject)item.Itemstack.Item).Id] = ((RegistryObject)item.Itemstack.Item).Code;
				}
				else
				{
					blockIdMapping[item.Itemstack.Block.BlockId] = ((RegistryObject)item.Itemstack.Block).Code;
				}
				item.Itemstack.Collectible.OnStoreCollectibleMappings(((BlockEntity)this).Api.World, item, blockIdMapping, itemIdMapping);
			}
		}
		ItemSlot[] cookingSlots = inventory.CookingSlots;
		foreach (ItemSlot val in cookingSlots)
		{
			if (val.Itemstack != null)
			{
				if ((int)val.Itemstack.Class == 1)
				{
					itemIdMapping[((CollectibleObject)val.Itemstack.Item).Id] = ((RegistryObject)val.Itemstack.Item).Code;
				}
				else
				{
					blockIdMapping[val.Itemstack.Block.BlockId] = ((RegistryObject)val.Itemstack.Block).Code;
				}
				val.Itemstack.Collectible.OnStoreCollectibleMappings(((BlockEntity)this).Api.World, val, blockIdMapping, itemIdMapping);
			}
		}
	}

	public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
	{
		base.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
	}

	public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
	{
		if (((BlockEntity)this).Block == null || ((RegistryObject)((BlockEntity)this).Block).Code.Path.Contains("construct"))
		{
			return false;
		}
		ItemStack contentStack = ((inputStack == null) ? outputStack : inputStack);
		MeshData contentMesh = getContentMesh(contentStack, tesselator);
		if (contentMesh != null)
		{
			mesher.AddMeshData(contentMesh, 1);
		}
		string text = ((RegistryObject)((BlockEntity)this).Block).Variant["burnstate"];
		string contentstate = CurrentModel.ToString().ToLowerInvariant();
		if (text == "cold" && fuelSlot.Empty)
		{
			text = "extinct";
		}
		if (text == null)
		{
			return true;
		}
		mesher.AddMeshData(getOrCreateMesh(text, contentstate), 1);
		return true;
	}

	private MeshData getContentMesh(ItemStack contentStack, ITesselatorAPI tesselator)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Invalid comparison between Unknown and I4
		CurrentModel = EnumFirepitModel.Normal;
		if (contentStack == null)
		{
			return null;
		}
		if (contentStack.Collectible is IInFirepitMeshSupplier)
		{
			EnumFirepitModel firepitModel = EnumFirepitModel.Normal;
			MeshData meshWhenInFirepit = (contentStack.Collectible as IInFirepitMeshSupplier).GetMeshWhenInFirepit(contentStack, ((BlockEntity)this).Api.World, ((BlockEntity)this).Pos, ref firepitModel);
			CurrentModel = firepitModel;
			if (meshWhenInFirepit != null)
			{
				return meshWhenInFirepit;
			}
		}
		if (contentStack.Collectible is IInFirepitRendererSupplier)
		{
			EnumFirepitModel desiredFirepitModel = (contentStack.Collectible as IInFirepitRendererSupplier).GetDesiredFirepitModel(contentStack, this, contentStack == outputStack);
			CurrentModel = desiredFirepitModel;
			return null;
		}
		InFirePitProps renderProps = GetRenderProps(contentStack);
		if (renderProps != null)
		{
			CurrentModel = renderProps.UseFirepitModel;
			if ((int)contentStack.Class != 1)
			{
				MeshData val = default(MeshData);
				tesselator.TesselateBlock(contentStack.Block, ref val);
				val.ModelTransform(renderProps.Transform);
				if (!IsBurning && renderProps.UseFirepitModel != EnumFirepitModel.Spit)
				{
					val.Translate(0f, -0.0625f, 0f);
				}
				return val;
			}
			return null;
		}
		if (renderer.RequireSpit)
		{
			CurrentModel = EnumFirepitModel.Spit;
		}
		return null;
	}

	public override void OnBlockUnloaded()
	{
		base.OnBlockUnloaded();
		renderer?.Dispose();
	}

	public static InFirePitProps GetRenderProps(ItemStack contentStack)
	{
		object obj;
		if (contentStack == null)
		{
			obj = null;
		}
		else
		{
			JsonObject itemAttributes = contentStack.ItemAttributes;
			obj = ((itemAttributes != null) ? itemAttributes["inFirePitProps"].AsObject<InFirePitProps>((InFirePitProps)null) : null);
		}
		InFirePitProps inFirePitProps = (InFirePitProps)obj;
		if (inFirePitProps != null)
		{
			inFirePitProps.Transform.EnsureDefaultValues();
		}
		return inFirePitProps;
	}

	public MeshData getOrCreateMesh(string burnstate, string contentstate)
	{
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<string, MeshData> orCreate = ObjectCacheUtil.GetOrCreate<Dictionary<string, MeshData>>(((BlockEntity)this).Api, "firepit-meshes", (CreateCachableObjectDelegate<Dictionary<string, MeshData>>)(() => new Dictionary<string, MeshData>()));
		string text = burnstate + "-" + contentstate;
		if (!orCreate.TryGetValue(text, out var value))
		{
			Block block = ((BlockEntity)this).Api.World.BlockAccessor.GetBlock(((BlockEntity)this).Pos);
			if (block.BlockId == 0)
			{
				return null;
			}
			_ = new MeshData[17];
			((ICoreClientAPI)((BlockEntity)this).Api).Tesselator.TesselateShape((CollectibleObject)(object)block, Shape.TryGet(((BlockEntity)this).Api, "shapes/block/wood/firepit/" + text + ".json"), ref value, (Vec3f)null, (int?)null, (string[])null);
		}
		return value;
	}

	public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
	{
		if (!IsBurning)
		{
			if (!IsSmoldering)
			{
				return 0f;
			}
			return 0.25f;
		}
		return 10f;
	}
}
