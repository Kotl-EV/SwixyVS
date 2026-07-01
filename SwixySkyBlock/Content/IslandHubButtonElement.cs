using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixySkyBlock.Content;

internal enum IslandHubButtonKind
{
    Tab,
    Action
}

internal enum IslandHubButtonVisual
{
    Normal,
    Hover,
    Pressed,
    Active,
    Disabled
}

internal sealed class IslandHubButtonElement : GuiElementControl
{
    private readonly string label;
    private readonly ActionConsumable onClick;
    private readonly bool active;
    private readonly IslandHubButtonKind kind;
    private readonly TextDrawUtil textDrawUtil = new();
    private LoadedTexture normalTexture;
    private LoadedTexture hoverTexture;
    private LoadedTexture pressedTexture;
    private LoadedTexture activeTexture;
    private LoadedTexture disabledTexture;
    private bool mouseDown;

    public IslandHubButtonElement(
        ICoreClientAPI api,
        string label,
        ElementBounds bounds,
        ActionConsumable onClick,
        bool active,
        IslandHubButtonKind kind,
        bool enabled = true)
        : base(api, bounds)
    {
        this.label = label ?? "";
        this.onClick = onClick;
        this.active = active;
        this.kind = kind;
        Enabled = enabled;
        if (kind == IslandHubButtonKind.Action)
        {
            Bounds.WithFixedPadding(4);
        }

        normalTexture = new LoadedTexture(api);
        hoverTexture = new LoadedTexture(api);
        pressedTexture = new LoadedTexture(api);
        activeTexture = new LoadedTexture(api);
        disabledTexture = new LoadedTexture(api);
    }

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        ComposeTextures();
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (normalTexture.TextureId == 0)
        {
            ComposeTextures();
        }

        LoadedTexture texture;
        if (!Enabled)
        {
            texture = disabledTexture;
        }
        else if (mouseDown)
        {
            texture = pressedTexture;
        }
        else if (kind == IslandHubButtonKind.Tab && active)
        {
            texture = IsMouseOver() ? hoverTexture : activeTexture;
        }
        else if (IsMouseOver())
        {
            texture = hoverTexture;
        }
        else
        {
            texture = normalTexture;
        }

        api.Render.Render2DTexturePremultipliedAlpha(
            texture.TextureId,
            Bounds.absX,
            Bounds.absY,
            Bounds.OuterWidth,
            Bounds.OuterHeight);
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (Enabled)
        {
            mouseDown = true;
        }
    }

    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        mouseDown = false;
        if (!Enabled || !IsMouseOver())
        {
            return;
        }

        api.Gui.PlaySound("toggleswitch");
        onClick.Invoke();
        args.Handled = true;
    }

    public override void Dispose()
    {
        base.Dispose();
        normalTexture?.Dispose();
        hoverTexture?.Dispose();
        pressedTexture?.Dispose();
        activeTexture?.Dispose();
        disabledTexture?.Dispose();
    }

    private bool IsMouseOver() => IsPositionInside(api.Input.MouseX, api.Input.MouseY);

    private void ComposeTextures()
    {
        Bounds.CalcWorldBounds();
        if (Bounds.OuterWidthInt < 1 || Bounds.OuterHeightInt < 1)
        {
            return;
        }

        normalTexture?.Dispose();
        hoverTexture?.Dispose();
        pressedTexture?.Dispose();
        activeTexture?.Dispose();
        disabledTexture?.Dispose();

        normalTexture = new LoadedTexture(api);
        hoverTexture = new LoadedTexture(api);
        pressedTexture = new LoadedTexture(api);
        activeTexture = new LoadedTexture(api);
        disabledTexture = new LoadedTexture(api);

        BakeTexture(normalTexture, IslandHubButtonVisual.Normal);
        BakeTexture(hoverTexture, IslandHubButtonVisual.Hover);
        BakeTexture(pressedTexture, IslandHubButtonVisual.Pressed);
        BakeTexture(activeTexture, IslandHubButtonVisual.Active);
        BakeTexture(disabledTexture, IslandHubButtonVisual.Disabled);
    }

    private void BakeTexture(LoadedTexture target, IslandHubButtonVisual visual)
    {
        using var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
        using var ctx = genContext(surface);
        IslandHubTheme.DrawRoundedRect(ctx, 0, 0, Bounds.OuterWidth, Bounds.OuterHeight, 4);
        ctx.Clip();

        IslandHubTheme.DrawButton(ctx, Bounds.OuterWidth, Bounds.OuterHeight, kind, visual);

        var font = IslandHubTheme.CreateButtonFont(kind, visual);
        using var labelTexture = api.Gui.TextTexture.GenTextTexture(label, font);
        var textWidth = Math.Max(1, labelTexture.Width);
        var textX = Math.Max(0, (Bounds.OuterWidth - textWidth) / 2.0);
        var textHeight = textDrawUtil.GetMultilineTextHeight(font, label, textWidth);
        var textY = Math.Max(0, (Bounds.OuterHeight - textHeight) / 2.0);
        textDrawUtil.AutobreakAndDrawMultilineTextAt(ctx, font, label, textX, textY, textWidth);
        generateTexture(surface, ref target);
    }
}