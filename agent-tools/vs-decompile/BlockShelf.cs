using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BlockShelf : Block
{
	private WorldInteraction[]? interactions;

	public override void OnLoaded(ICoreAPI api)
	{
		((Block)this).OnLoaded(api);
		base.PlacedPriorityInteract = true;
		InteractionMatcherDelegate val5 = default(InteractionMatcherDelegate);
		interactions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "shelfInteractions", (CreateCachableObjectDelegate<WorldInteraction[]>)delegate
		{
			//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e6: Expected O, but got Unknown
			//IL_02e7: Expected O, but got Unknown
			//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0300: Unknown result type (might be due to invalid IL or missing references)
			//IL_0307: Unknown result type (might be due to invalid IL or missing references)
			//IL_030f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0319: Expected O, but got Unknown
			//IL_031a: Expected O, but got Unknown
			//IL_031c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0321: Unknown result type (might be due to invalid IL or missing references)
			//IL_032c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0337: Unknown result type (might be due to invalid IL or missing references)
			//IL_0339: Unknown result type (might be due to invalid IL or missing references)
			//IL_033e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0345: Unknown result type (might be due to invalid IL or missing references)
			//IL_034d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0357: Expected O, but got Unknown
			//IL_0358: Expected O, but got Unknown
			//IL_035a: Unknown result type (might be due to invalid IL or missing references)
			//IL_035f: Unknown result type (might be due to invalid IL or missing references)
			//IL_036a: Unknown result type (might be due to invalid IL or missing references)
			//IL_036c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0371: Unknown result type (might be due to invalid IL or missing references)
			//IL_0378: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a0: Expected O, but got Unknown
			//IL_038b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0390: Unknown result type (might be due to invalid IL or missing references)
			//IL_0393: Expected O, but got Unknown
			//IL_0398: Expected O, but got Unknown
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Expected O, but got Unknown
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Expected O, but got Unknown
			//IL_0281: Unknown result type (might be due to invalid IL or missing references)
			//IL_028b: Expected O, but got Unknown
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Expected O, but got Unknown
			//IL_0207: Unknown result type (might be due to invalid IL or missing references)
			//IL_020e: Expected O, but got Unknown
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_0230: Expected O, but got Unknown
			List<ItemStack> usableItemStacklist = new List<ItemStack>();
			List<ItemStack> list = new List<ItemStack>();
			foreach (CollectibleObject collectible in api.World.Collectibles)
			{
				int num;
				if (collectible == null)
				{
					num = 0;
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
					bool? flag = obj;
					num = ((flag == true) ? 1 : 0);
				}
				bool flag2 = (byte)num != 0;
				if (!flag2)
				{
					bool flag3 = ((collectible is IContainedInteractable || collectible is IBlockMealContainer) ? true : false);
					flag2 = flag3;
				}
				if (flag2)
				{
					goto IL_0101;
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
					bool? flag = obj3;
					if (flag == true)
					{
						goto IL_0101;
					}
				}
				goto IL_0114;
				IL_0101:
				usableItemStacklist.Add(new ItemStack(collectible, 1));
				goto IL_0114;
				IL_0114:
				if (BlockEntityShelf.GetShelvableLayout(new ItemStack(collectible, 1)).HasValue)
				{
					if (collectible is BlockPie blockPie)
					{
						ItemStack val = new ItemStack(collectible, 1);
						val.Attributes.SetInt("pieSize", 4);
						val.Attributes.SetString("topCrustType", "square");
						ITreeAttribute attributes3 = val.Attributes;
						attributes3.SetInt("bakeLevel", ((RegistryObject)blockPie).Variant["state"] switch
						{
							"raw" => 0, 
							"partbaked" => 1, 
							"perfect" => 2, 
							"charred" => 3, 
							_ => 0, 
						});
						ItemStack val2 = new ItemStack(api.World.GetItem(AssetLocation.op_Implicit("dough-spelt")), 2);
						ItemStack val3 = new ItemStack(api.World.GetItem(AssetLocation.op_Implicit("fruit-redapple")), 2);
						blockPie.SetContents(val, (ItemStack[])(object)new ItemStack[6] { val2, val3, val3, val3, val3, val2 });
						val.Attributes.SetFloat("quantityServings", 1f);
						list.Add(val);
					}
					else
					{
						list.Add(new ItemStack(collectible, 1));
					}
				}
			}
			ItemStack[] itemstacks = list.ToArray();
			WorldInteraction[] obj5 = new WorldInteraction[4]
			{
				new WorldInteraction
				{
					ActionLangCode = "blockhelp-shelf-use",
					MouseButton = (EnumMouseButton)2,
					Itemstacks = itemstacks,
					GetMatchingStacks = (InteractionStacksDelegate)delegate(WorldInteraction wi, BlockSelection bs, EntitySelection es)
					{
						BlockEntityShelf beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityShelf;
						return usableItemStacklist.Where((ItemStack stack) => beshelf?.CanUse(stack, bs) ?? false)?.ToArray();
					}
				},
				new WorldInteraction
				{
					ActionLangCode = "blockhelp-shelf-place",
					MouseButton = (EnumMouseButton)2,
					Itemstacks = itemstacks,
					GetMatchingStacks = (InteractionStacksDelegate)delegate(WorldInteraction wi, BlockSelection bs, EntitySelection es)
					{
						BlockEntityShelf beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityShelf;
						return usableItemStacklist.All(delegate(ItemStack stack)
						{
							BlockEntityShelf blockEntityShelf = beshelf;
							return blockEntityShelf != null && !blockEntityShelf.CanUse(stack, bs);
						}) ? usableItemStacklist.Where((ItemStack stack) => beshelf?.CanPlace(stack, bs, out var _) ?? false).ToArray() : null;
					}
				},
				new WorldInteraction
				{
					ActionLangCode = "blockhelp-shelf-place",
					HotKeyCode = "shift",
					MouseButton = (EnumMouseButton)2,
					Itemstacks = itemstacks,
					GetMatchingStacks = (InteractionStacksDelegate)delegate(WorldInteraction wi, BlockSelection bs, EntitySelection es)
					{
						BlockEntityShelf beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityShelf;
						return usableItemStacklist.Any((ItemStack stack) => beshelf?.CanUse(stack, bs) ?? false) ? usableItemStacklist.Where((ItemStack stack) => beshelf?.CanPlace(stack, bs, out var _) ?? false).ToArray() : null;
					}
				},
				default(WorldInteraction)
			};
			WorldInteraction val4 = new WorldInteraction
			{
				ActionLangCode = "blockhelp-shelf-take",
				MouseButton = (EnumMouseButton)2,
				RequireFreeHand = true
			};
			InteractionMatcherDelegate obj6 = val5;
			if (obj6 == null)
			{
				InteractionMatcherDelegate val6 = delegate(WorldInteraction wi, BlockSelection bs, EntitySelection es)
				{
					BlockEntityShelf obj7 = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityShelf;
					bool canTake = false;
					obj7?.CanPlace(null, bs, out canTake);
					return canTake;
				};
				InteractionMatcherDelegate val7 = val6;
				val5 = val6;
				obj6 = val7;
			}
			val4.ShouldApply = obj6;
			obj5[3] = val4;
			return (WorldInteraction[])(object)obj5;
		});
	}

	public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
	{
		return true;
	}

	public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
	{
		if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityShelf blockEntityShelf)
		{
			return blockEntityShelf.OnInteract(byPlayer, blockSel);
		}
		return ((Block)this).OnBlockInteractStart(world, byPlayer, blockSel);
	}

	public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		WorldInteraction[] placedBlockInteractionHelp = ((Block)this).GetPlacedBlockInteractionHelp(world, selection, forPlayer);
		if ((int)world.Claims.TestAccess(forPlayer, selection.Position, (EnumBlockAccessFlags)2) == 0)
		{
			WorldInteraction[] array = placedBlockInteractionHelp;
			WorldInteraction[] array2 = interactions;
			int num = 0;
			WorldInteraction[] array3 = (WorldInteraction[])(object)new WorldInteraction[array.Length + array2.Length];
			ReadOnlySpan<WorldInteraction> readOnlySpan = new ReadOnlySpan<WorldInteraction>(array);
			readOnlySpan.CopyTo(new Span<WorldInteraction>(array3).Slice(num, readOnlySpan.Length));
			num += readOnlySpan.Length;
			ReadOnlySpan<WorldInteraction> readOnlySpan2 = new ReadOnlySpan<WorldInteraction>(array2);
			readOnlySpan2.CopyTo(new Span<WorldInteraction>(array3).Slice(num, readOnlySpan2.Length));
			num += readOnlySpan2.Length;
			return array3;
		}
		return placedBlockInteractionHelp;
	}
}
