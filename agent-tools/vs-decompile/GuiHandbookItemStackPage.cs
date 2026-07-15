using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class GuiHandbookItemStackPage : GuiHandbookPage
{
	public ItemStack Stack;

	public LoadedTexture Texture;

	public string TextCacheTitle;

	public string TextCacheAll;

	public float searchWeightOffset;

	public InventoryBase unspoilableInventory;

	public DummySlot dummySlot;

	private ElementBounds scissorBounds;

	private bool isDuplicate;

	public override float SearchWeightOffset => searchWeightOffset;

	public override string PageCode => PageCodeForStack(Stack);

	public override string CategoryCode => "stack";

	public override bool IsDuplicate => isDuplicate;

	public GuiHandbookItemStackPage(ICoreClientAPI capi, ItemStack stack)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		Stack = stack;
		unspoilableInventory = (InventoryBase)new CreativeInventoryTab(1, "not-used", (ICoreAPI)null);
		dummySlot = new DummySlot(stack, unspoilableInventory);
		TextCacheTitle = StringUtil.ToSearchFriendly(stack.GetName());
		TextCacheAll = StringUtil.ToSearchFriendly(stack.GetName() + " " + stack.GetDescription((IWorldAccessor)(object)capi.World, (ItemSlot)(object)dummySlot, false));
		JsonObject attributes = stack.Collectible.Attributes;
		int num;
		if (attributes == null)
		{
			num = 0;
		}
		else
		{
			JsonObject obj = attributes["handbook"];
			num = ((((obj != null) ? new bool?(obj["isDuplicate"].AsBool(false)) : ((bool?)null)) == true) ? 1 : 0);
		}
		isDuplicate = (byte)num != 0;
		JsonObject attributes2 = stack.Collectible.Attributes;
		float? obj2;
		if (attributes2 == null)
		{
			obj2 = null;
		}
		else
		{
			JsonObject obj3 = attributes2["handbook"];
			obj2 = ((obj3 != null) ? new float?(obj3["searchWeightOffset"].AsFloat(0f)) : ((float?)null));
		}
		float? num2 = obj2;
		searchWeightOffset = num2.GetValueOrDefault();
	}

	public static string PageCodeForStack(ItemStack stack)
	{
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		if (stack.Attributes != null && stack.Attributes.Count > 0)
		{
			ITreeAttribute val = stack.Attributes.Clone();
			string[] ignoredStackAttributes = GlobalConstants.IgnoredStackAttributes;
			foreach (string text in ignoredStackAttributes)
			{
				val.RemoveAttribute(text);
			}
			val.RemoveAttribute("durability");
			OrderedDictionary<string, IAttribute> val2 = val.SortedCopy(true);
			if (val.Count != 0)
			{
				string text2 = TreeAttribute.ToJsonToken((IEnumerable<KeyValuePair<string, IAttribute>>)val2);
				return ItemClassMethods.Name(stack.Class) + "-" + ((RegistryObject)stack.Collectible).Code.ToShortString() + "-" + text2;
			}
		}
		return ItemClassMethods.Name(stack.Class) + "-" + ((RegistryObject)stack.Collectible).Code.ToShortString();
	}

	public void Recompose(ICoreClientAPI capi)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		LoadedTexture texture = Texture;
		if (texture != null)
		{
			texture.Dispose();
		}
		Texture = new TextTextureUtil(capi).GenTextTexture(Stack.GetName(), CairoFont.WhiteSmallText(), (TextBackground)null);
		scissorBounds = ElementBounds.FixedSize(50.0, 50.0);
		scissorBounds.ParentBounds = capi.Gui.WindowBounds;
	}

	public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
	{
		float num = (float)GuiElement.scaled(25.0);
		float num2 = (float)GuiElement.scaled(10.0);
		if (Texture == null)
		{
			Recompose(capi);
		}
		scissorBounds.fixedX = ((double)num2 + x - (double)(num / 2f)) / (double)RuntimeEnv.GUIScale;
		scissorBounds.fixedY = (y - (double)(num / 2f)) / (double)RuntimeEnv.GUIScale;
		scissorBounds.CalcWorldBounds();
		if (!(scissorBounds.InnerWidth <= 0.0) && !(scissorBounds.InnerHeight <= 0.0))
		{
			capi.Render.PushScissor(scissorBounds, true);
			capi.Render.RenderItemstackToGui((ItemSlot)(object)dummySlot, x + (double)num2 + (double)(num / 2f), y + (double)(num / 2f), 100.0, num, -1, true, false, false);
			capi.Render.PopScissor();
			capi.Render.Render2DTexturePremultipliedAlpha(Texture.TextureId, x + (double)num + GuiElement.scaled(25.0), y + (double)(num / 4f) - GuiElement.scaled(3.0), (double)Texture.Width, (double)Texture.Height, 50f, (Vec4f)null);
		}
	}

	public override void Dispose()
	{
		LoadedTexture texture = Texture;
		if (texture != null)
		{
			texture.Dispose();
		}
		Texture = null;
	}

	public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
	{
		RichTextComponentBase[] pageText = GetPageText(detailViewGui.Api, allstacks, openDetailPageFor);
		GuiComposerHelpers.AddRichtext(detailViewGui, pageText, textBounds, "richtext");
	}

	protected virtual RichTextComponentBase[] GetPageText(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
	{
		return Stack.Collectible.GetBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>()?.GetHandbookInfo((ItemSlot)(object)dummySlot, capi, allStacks, openDetailPageFor) ?? Array.Empty<RichTextComponentBase>();
	}

	public override PageText GetPageText()
	{
		return new PageText
		{
			Title = TextCacheTitle,
			Text = TextCacheAll
		};
	}
}
