using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class BlockEntityShelf : BlockEntityDisplay
{
	protected static int slotCount = 8;

	protected InventoryGeneric inv;

	public override InventoryBase Inventory => (InventoryBase)(object)inv;

	public override string InventoryClassName => "shelf";

	public override string AttributeTransformCode => "onshelfTransform";

	protected string GetSlotType(int slotid)
	{
		return "shelf";
	}

	public BlockEntityShelf()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		inv = new InventoryGeneric(slotCount, "shelf-0", (ICoreAPI)null, (NewSlotDelegate)((int id, InventoryGeneric inv) => (ItemSlot)(object)new ItemSlotDisplay((InventoryBase)(object)inv, GetSlotType(id))));
	}

	public override void Initialize(ICoreAPI api)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		base.Initialize(api);
		((InventoryBase)inv).OnAcquireTransitionSpeed += new CustomGetTransitionSpeedMulDelegate(Inv_OnAcquireTransitionSpeed);
	}

	protected float Inv_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Invalid comparison between Unknown and I4
		if (((int)transType == 1 || (int)transType == 6) ? true : false)
		{
			Room room = container.Room;
			return ((room != null && room.ExitCount == 0) ? 2f : 0.5f) * 4f;
		}
		if (((BlockEntity)this).Api == null)
		{
			return 0f;
		}
		if ((int)transType != 5)
		{
			return 1f;
		}
		return GameMath.Clamp((1f - container.GetPerishRate() - 0.5f) * 3f, 0f, 1f);
	}

	public bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
	{
		ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
		if (TryUse(byPlayer, blockSel))
		{
			return true;
		}
		if (activeHotbarSlot.Empty)
		{
			return TryTake(byPlayer, blockSel);
		}
		if (GetShelvableLayout(activeHotbarSlot.Itemstack).HasValue)
		{
			return TryPut(byPlayer, blockSel);
		}
		return false;
	}

	public static EnumShelvableLayout? GetShelvableLayout(ItemStack? stack)
	{
		if (stack == null)
		{
			return null;
		}
		JsonObject val = stack.Collectible?.Attributes;
		CollectibleObject collectible = stack.Collectible;
		EnumShelvableLayout? enumShelvableLayout = ((collectible == null) ? ((EnumShelvableLayout?)null) : collectible.GetCollectibleInterface<IShelvable>()?.GetShelvableType(stack));
		EnumShelvableLayout? enumShelvableLayout2 = enumShelvableLayout;
		if (!enumShelvableLayout2.HasValue)
		{
			enumShelvableLayout = ((val != null) ? val["shelvable"].AsString((string)null) : null) switch
			{
				"Quadrants" => EnumShelvableLayout.Quadrants, 
				"Halves" => EnumShelvableLayout.Halves, 
				"SingleCenter" => EnumShelvableLayout.SingleCenter, 
				_ => null, 
			};
		}
		enumShelvableLayout2 = enumShelvableLayout;
		if (!enumShelvableLayout2.HasValue)
		{
			enumShelvableLayout = ((val != null && val["shelvable"].AsBool(false)) ? new EnumShelvableLayout?(EnumShelvableLayout.Quadrants) : ((EnumShelvableLayout?)null));
		}
		return enumShelvableLayout;
	}

	public bool CanUse(ItemStack? stack, BlockSelection blockSel)
	{
		if (stack == null)
		{
			return false;
		}
		CollectibleObject collectible = stack.Collectible;
		bool flag = blockSel.SelectionBoxIndex > 1;
		bool flag2 = blockSel.SelectionBoxIndex % 2 == 0;
		EnumShelvableLayout? shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 4 : 0].Itemstack);
		if ((!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter) && !flag2)
		{
			shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 6 : 2].Itemstack);
		}
		int num = (flag ? 4 : 0) + ((!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter) ? ((!flag2) ? 2 : 0) : 0);
		int num2 = num;
		bool flag3;
		if (shelvableLayout.HasValue)
		{
			EnumShelvableLayout valueOrDefault = shelvableLayout.GetValueOrDefault();
			if ((uint)(valueOrDefault - 1) <= 1u)
			{
				flag3 = true;
				goto IL_00be;
			}
		}
		flag3 = false;
		goto IL_00be;
		IL_00be:
		for (int num3 = num2 + (flag3 ? 1 : 2) - 1; num3 >= num; num3--)
		{
			if (!((InventoryBase)inv)[num3].Empty)
			{
				CollectibleObject collectible2 = ((InventoryBase)inv)[num3].Itemstack.Collectible;
				int num4;
				if (collectible == null)
				{
					num4 = 0;
				}
				else
				{
					JsonObject attributes = collectible.Attributes;
					bool? obj;
					if (attributes == null)
					{
						obj = null;
					}
					else
					{
						JsonObject obj2 = attributes["mealContainer"];
						obj = ((obj2 != null) ? new bool?(obj2.AsBool(false)) : ((bool?)null));
					}
					bool? flag4 = obj;
					num4 = ((flag4 == true) ? 1 : 0);
				}
				flag3 = (byte)num4 != 0;
				if (!flag3)
				{
					bool flag5 = ((collectible is IContainedInteractable || collectible is IBlockMealContainer) ? true : false);
					flag3 = flag5;
				}
				if (flag3)
				{
					return collectible2 is BlockCookedContainerBase;
				}
				if (collectible != null)
				{
					JsonObject attributes2 = collectible.Attributes;
					bool? obj3;
					if (attributes2 == null)
					{
						obj3 = null;
					}
					else
					{
						JsonObject obj4 = attributes2["canSealCrock"];
						obj3 = ((obj4 != null) ? new bool?(obj4.AsBool(false)) : ((bool?)null));
					}
					bool? flag4 = obj3;
					if (flag4 == true)
					{
						return collectible2 is BlockCrock;
					}
				}
			}
		}
		return false;
	}

	public bool CanPlace(ItemStack? stack, BlockSelection blockSel, out bool canTake)
	{
		bool flag = blockSel.SelectionBoxIndex > 1;
		bool flag2 = blockSel.SelectionBoxIndex % 2 == 0;
		EnumShelvableLayout? shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 4 : 0].Itemstack);
		if (shelvableLayout.HasValue)
		{
			EnumShelvableLayout valueOrDefault = shelvableLayout.GetValueOrDefault();
			if (valueOrDefault == EnumShelvableLayout.SingleCenter || (valueOrDefault == EnumShelvableLayout.Halves && flag2))
			{
				goto IL_0085;
			}
		}
		shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 6 : 2].Itemstack);
		if (shelvableLayout.HasValue && shelvableLayout == EnumShelvableLayout.Halves && !flag2)
		{
			goto IL_0085;
		}
		EnumShelvableLayout? shelvableLayout2 = GetShelvableLayout(stack);
		int num = (flag ? 4 : 0) + ((!shelvableLayout2.HasValue || shelvableLayout2 != EnumShelvableLayout.SingleCenter) ? ((!flag2) ? 2 : 0) : 0);
		int num2 = num;
		bool flag3;
		if (shelvableLayout2.HasValue)
		{
			EnumShelvableLayout valueOrDefault2 = shelvableLayout2.GetValueOrDefault();
			if ((uint)(valueOrDefault2 - 1) <= 1u)
			{
				flag3 = true;
				goto IL_00dd;
			}
		}
		flag3 = false;
		goto IL_00dd;
		IL_0085:
		canTake = true;
		return false;
		IL_00dd:
		int num3 = num2 + (flag3 ? 1 : 2);
		canTake = false;
		bool result = false;
		for (int num4 = num3 - 1; num4 >= num; num4--)
		{
			if (((InventoryBase)inv)[num4].Empty)
			{
				result = true;
			}
			else
			{
				canTake = true;
			}
		}
		return result;
	}

	private bool TryUse(IPlayer player, BlockSelection blockSel)
	{
		bool flag = blockSel.SelectionBoxIndex > 1;
		bool flag2 = blockSel.SelectionBoxIndex % 2 == 0;
		EnumShelvableLayout? shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 4 : 0].Itemstack);
		if ((!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter) && !flag2)
		{
			shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 6 : 2].Itemstack);
		}
		int num = (flag ? 4 : 0) + ((!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter) ? ((!flag2) ? 2 : 0) : 0);
		int num2 = num;
		bool flag3;
		if (shelvableLayout.HasValue)
		{
			EnumShelvableLayout valueOrDefault = shelvableLayout.GetValueOrDefault();
			if ((uint)(valueOrDefault - 1) <= 1u)
			{
				flag3 = true;
				goto IL_00b0;
			}
		}
		flag3 = false;
		goto IL_00b0;
		IL_00b0:
		int num3 = num2 + (flag3 ? 1 : 2);
		if (((EntityAgent)player.Entity).Controls.ShiftKey)
		{
			return false;
		}
		for (int num4 = num3 - 1; num4 >= num; num4--)
		{
			ItemStack itemstack = ((InventoryBase)inv)[num4].Itemstack;
			IContainedInteractable containedInteractable = ((itemstack != null) ? itemstack.Collectible.GetCollectibleInterface<IContainedInteractable>() : null);
			if (containedInteractable != null && containedInteractable.OnContainedInteractStart(this, ((InventoryBase)inv)[num4], player, blockSel))
			{
				((BlockEntity)this).MarkDirty(false, (IPlayer)null);
				return true;
			}
		}
		return false;
	}

	private bool TryPut(IPlayer byPlayer, BlockSelection blockSel)
	{
		//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
		ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
		bool flag = blockSel.SelectionBoxIndex > 1;
		bool flag2 = blockSel.SelectionBoxIndex % 2 == 0;
		int num = 0;
		EnumShelvableLayout? shelvableLayout = GetShelvableLayout(activeHotbarSlot.Itemstack);
		int num2 = (flag ? 4 : 0) + ((!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter) ? ((!flag2) ? 2 : 0) : 0);
		int num3 = num2 + ((shelvableLayout.HasValue && shelvableLayout == EnumShelvableLayout.SingleCenter) ? 4 : 2);
		bool flag3;
		if (shelvableLayout.HasValue)
		{
			EnumShelvableLayout valueOrDefault = shelvableLayout.GetValueOrDefault();
			if ((uint)(valueOrDefault - 1) <= 1u)
			{
				flag3 = true;
				goto IL_0095;
			}
		}
		flag3 = false;
		goto IL_0095;
		IL_0204:
		int num4;
		num3 = num4 + (flag3 ? 1 : 2);
		for (int i = num2; i < num3; i++)
		{
			if (!((InventoryBase)inv)[i].Empty)
			{
				continue;
			}
			int num5 = activeHotbarSlot.TryPutInto(((BlockEntity)this).Api.World, ((InventoryBase)inv)[i], 1);
			((BlockEntity)this).MarkDirty(false, (IPlayer)null);
			ICoreAPI api = ((BlockEntity)this).Api;
			ICoreAPI obj = ((api is ICoreClientAPI) ? api : null);
			if (obj != null)
			{
				((ICoreClientAPI)obj).World.Player.TriggerFpAnimation((EnumHandInteract)2);
			}
			if (num5 > 0)
			{
				IWorldAccessor world = ((BlockEntity)this).Api.World;
				ItemStack itemstack = ((InventoryBase)inv)[i].Itemstack;
				SoundAttributes? obj2;
				if (itemstack == null)
				{
					obj2 = null;
				}
				else
				{
					Block block = itemstack.Block;
					if (block == null)
					{
						obj2 = null;
					}
					else
					{
						BlockSounds sounds = block.Sounds;
						obj2 = ((sounds != null) ? new SoundAttributes?(sounds.Place) : ((SoundAttributes?)null));
					}
				}
				world.PlaySoundAt((SoundAttributes)(((??)obj2) ?? GlobalConstants.DefaultBuildSound), (Entity)(object)byPlayer.Entity, byPlayer, 1f);
				ILogger logger = ((BlockEntity)this).Api.World.Logger;
				object[] obj3 = new object[3] { byPlayer.PlayerName, null, null };
				ItemStack itemstack2 = ((InventoryBase)inv)[i].Itemstack;
				obj3[1] = ((itemstack2 != null) ? ((RegistryObject)itemstack2.Collectible).Code : null);
				obj3[2] = ((BlockEntity)this).Pos;
				logger.Audit("{0} Put 1x{1} into Shelf at {2}.", obj3);
				return true;
			}
			return false;
		}
		ICoreAPI api2 = ((BlockEntity)this).Api;
		ICoreAPI obj4 = ((api2 is ICoreClientAPI) ? api2 : null);
		if (obj4 != null)
		{
			((ICoreClientAPI)obj4).TriggerIngameError((object)this, "shelffull", Lang.Get("shelfhelp-shelffull-error", Array.Empty<object>()));
		}
		return false;
		IL_0095:
		if (flag3)
		{
			for (int j = num2; j < num3; j++)
			{
				if (!((InventoryBase)inv)[j].Empty)
				{
					EnumShelvableLayout? shelvableLayout2 = GetShelvableLayout(((InventoryBase)inv)[j].Itemstack);
					num += ((shelvableLayout2.HasValue && shelvableLayout2 == EnumShelvableLayout.SingleCenter) ? 4 : ((!shelvableLayout2.HasValue || shelvableLayout2 != EnumShelvableLayout.Halves) ? 1 : 2));
				}
			}
		}
		if (num > 0 && num < ((shelvableLayout.HasValue && shelvableLayout == EnumShelvableLayout.SingleCenter) ? 4 : 2))
		{
			ICoreAPI api3 = ((BlockEntity)this).Api;
			ICoreAPI obj5 = ((api3 is ICoreClientAPI) ? api3 : null);
			if (obj5 != null)
			{
				((ICoreClientAPI)obj5).TriggerIngameError((object)this, "needsmorespace", Lang.Get("shelfhelp-needsmorespace-error", Array.Empty<object>()));
			}
			return false;
		}
		if (!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter)
		{
			shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 4 : 0].Itemstack);
		}
		if ((!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter) && !flag2)
		{
			shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 6 : 2].Itemstack);
		}
		num2 = (flag ? 4 : 0) + ((!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter) ? ((!flag2) ? 2 : 0) : 0);
		num4 = num2;
		if (shelvableLayout.HasValue)
		{
			EnumShelvableLayout valueOrDefault = shelvableLayout.GetValueOrDefault();
			if ((uint)(valueOrDefault - 1) <= 1u)
			{
				flag3 = true;
				goto IL_0204;
			}
		}
		flag3 = false;
		goto IL_0204;
	}

	private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
	{
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		bool flag = blockSel.SelectionBoxIndex > 1;
		bool flag2 = blockSel.SelectionBoxIndex % 2 == 0;
		EnumShelvableLayout? shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 4 : 0].Itemstack);
		if ((!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter) && !flag2)
		{
			shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[flag ? 6 : 2].Itemstack);
		}
		int num = (flag ? 4 : 0) + ((!shelvableLayout.HasValue || shelvableLayout != EnumShelvableLayout.SingleCenter) ? ((!flag2) ? 2 : 0) : 0);
		for (int num2 = num + ((shelvableLayout.HasValue && shelvableLayout == EnumShelvableLayout.SingleCenter) ? 4 : 2) - 1; num2 >= num; num2--)
		{
			if (!((InventoryBase)inv)[num2].Empty)
			{
				ItemStack val = ((InventoryBase)inv)[num2].TakeOut(1);
				if (byPlayer.InventoryManager.TryGiveItemstack(val, false))
				{
					SoundAttributes? obj;
					if (val == null)
					{
						obj = null;
					}
					else
					{
						Block block = val.Block;
						if (block == null)
						{
							obj = null;
						}
						else
						{
							BlockSounds sounds = block.Sounds;
							obj = ((sounds != null) ? new SoundAttributes?(sounds.Place) : ((SoundAttributes?)null));
						}
					}
					((BlockEntity)this).Api.World.PlaySoundAt((SoundAttributes)(((??)obj) ?? GlobalConstants.DefaultBuildSound), (Entity)(object)byPlayer.Entity, byPlayer, 1f);
				}
				if (val != null && val.StackSize > 0)
				{
					((BlockEntity)this).Api.World.SpawnItemEntity(val, ((BlockEntity)this).Pos, (Vec3d)null);
				}
				((BlockEntity)this).Api.World.Logger.Audit("{0} Took 1x{1} from Shelf at {2}.", new object[3]
				{
					byPlayer.PlayerName,
					(val != null) ? ((RegistryObject)val.Collectible).Code : null,
					((BlockEntity)this).Pos
				});
				ICoreAPI api = ((BlockEntity)this).Api;
				ICoreAPI obj2 = ((api is ICoreClientAPI) ? api : null);
				if (obj2 != null)
				{
					((ICoreClientAPI)obj2).World.Player.TriggerFpAnimation((EnumHandInteract)2);
				}
				((BlockEntity)this).MarkDirty(false, (IPlayer)null);
				return true;
			}
		}
		return false;
	}

	protected override float[][] genTransformationMatrices()
	{
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		float[][] array = new float[slotCount][];
		for (int num = 0; num < slotCount; num++)
		{
			EnumShelvableLayout? shelvableLayout = GetShelvableLayout(((InventoryBase)inv)[num].Itemstack);
			float num2 = ((num % 4 >= 2) ? 0.75f : 0.25f);
			float num3 = ((num >= 4) ? 0.625f : 0.125f);
			float num4 = ((num % 2 == 0) ? 0.25f : 0.625f);
			bool flag = ((num == 0 || num == 4) ? true : false);
			if (flag && shelvableLayout.HasValue && shelvableLayout == EnumShelvableLayout.SingleCenter)
			{
				num2 = 0.5f;
			}
			switch (num)
			{
			case 0:
			case 2:
			case 4:
			case 6:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			bool flag2 = flag;
			bool flag3;
			if (flag2)
			{
				if (shelvableLayout.HasValue)
				{
					EnumShelvableLayout valueOrDefault = shelvableLayout.GetValueOrDefault();
					if ((uint)(valueOrDefault - 1) <= 1u)
					{
						flag3 = true;
						goto IL_00e2;
					}
				}
				flag3 = false;
				goto IL_00e2;
			}
			goto IL_00e6;
			IL_00e2:
			flag2 = flag3;
			goto IL_00e6;
			IL_00e6:
			if (flag2)
			{
				num4 = 0.4f;
			}
			array[num] = new Matrixf().Translate(0.5f, 0f, 0.5f).RotateYDeg(((BlockEntity)this).Block.Shape.rotateY).Translate(num2 - 0.5f, num3, num4 - 0.5f)
				.Translate(-0.5f, 0f, -0.5f)
				.Values;
		}
		return array;
	}

	public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
	{
		base.FromTreeAttributes(tree, worldForResolving);
		RedrawAfterReceivingTreeAttributes(worldForResolving);
	}

	public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
	{
		base.GetBlockInfo(forPlayer, sb);
		float num = GameMath.Clamp((1f - container.GetPerishRate() - 0.5f) * 3f, 0f, 1f);
		if (num > 0f)
		{
			sb.Append(Lang.Get("Suitable spot for food ripening.", Array.Empty<object>()));
		}
		sb.AppendLine();
		bool flag = forPlayer.CurrentBlockSelection != null && forPlayer.CurrentBlockSelection.SelectionBoxIndex > 1;
		for (int num2 = 3; num2 >= 0; num2--)
		{
			int num3 = num2 + (flag ? 4 : 0);
			num3 ^= 2;
			if (!((InventoryBase)inv)[num3].Empty)
			{
				ItemStack itemstack = ((InventoryBase)inv)[num3].Itemstack;
				object obj;
				if (itemstack == null)
				{
					obj = null;
				}
				else
				{
					CollectibleObject collectible = itemstack.Collectible;
					obj = ((collectible != null) ? collectible.GetTransitionableProperties(((BlockEntity)this).Api.World, itemstack, (Entity)(object)forPlayer.Entity) : null);
				}
				TransitionableProperties[] array = (TransitionableProperties[])obj;
				if (array != null && array.Length != 0)
				{
					sb.Append(PerishableInfoCompact(((BlockEntity)this).Api, ((InventoryBase)inv)[num3], num));
				}
				else
				{
					sb.AppendLine(((itemstack == null) ? null : itemstack.Collectible.GetCollectibleInterface<IContainedCustomName>()?.GetContainedInfo(((InventoryBase)inv)[num3])) ?? ((itemstack != null) ? itemstack.GetName() : null) ?? Lang.Get("unknown", Array.Empty<object>()));
				}
			}
		}
	}

	public static string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
	{
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Invalid comparison between Unknown and I4
		if (contentSlot.Empty)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		if (withStackName)
		{
			stringBuilder.Append(contentSlot.Itemstack.GetName());
		}
		TransitionState[] array = contentSlot.Itemstack.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);
		if (array == null)
		{
			return stringBuilder.ToString();
		}
		bool flag = false;
		bool flag2 = false;
		foreach (TransitionState val in array)
		{
			TransitionableProperties props = val.Props;
			float transitionRateMul = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, props.Type);
			if (transitionRateMul <= 0f)
			{
				continue;
			}
			float transitionLevel = val.TransitionLevel;
			float num = val.FreshHoursLeft / transitionRateMul;
			EnumTransitionType type = props.Type;
			if ((int)type != 0)
			{
				if ((int)type != 5 || flag)
				{
					continue;
				}
				flag2 = true;
				if (transitionLevel > 0f)
				{
					stringBuilder.Append(", " + Lang.Get("{1:0.#} days left to ripen ({0}%)", new object[2]
					{
						(int)Math.Round(transitionLevel * 100f),
						(val.TransitionHours - val.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate
					}));
					continue;
				}
				double num2 = Api.World.Calendar.HoursPerDay;
				if ((double)num / num2 >= (double)Api.World.Calendar.DaysPerYear)
				{
					stringBuilder.Append(", " + Lang.Get("will ripen in {0} years", new object[1] { Math.Round((double)num / num2 / (double)Api.World.Calendar.DaysPerYear, 1) }));
				}
				else if ((double)num > num2)
				{
					stringBuilder.Append(", " + Lang.Get("will ripen in {0} days", new object[1] { Math.Round((double)num / num2, 1) }));
				}
				else
				{
					stringBuilder.Append(", " + Lang.Get("will ripen in {0} hours", new object[1] { Math.Round(num, 1) }));
				}
				continue;
			}
			flag2 = true;
			if (transitionLevel > 0f)
			{
				flag = true;
				stringBuilder.Append(", " + Lang.Get("{0}% spoiled", new object[1] { (int)Math.Round(transitionLevel * 100f) }));
				continue;
			}
			double num3 = Api.World.Calendar.HoursPerDay;
			if ((double)num / num3 >= (double)Api.World.Calendar.DaysPerYear)
			{
				stringBuilder.Append(", " + Lang.Get("fresh for {0} years", new object[1] { Math.Round((double)num / num3 / (double)Api.World.Calendar.DaysPerYear, 1) }));
			}
			else if ((double)num > num3)
			{
				stringBuilder.Append(", " + Lang.Get("fresh for {0} days", new object[1] { Math.Round((double)num / num3, 1) }));
			}
			else
			{
				stringBuilder.Append(", " + Lang.Get("fresh for {0} hours", new object[1] { Math.Round(num, 1) }));
			}
		}
		if (flag2)
		{
			stringBuilder.AppendLine();
		}
		return stringBuilder.ToString();
	}
}
