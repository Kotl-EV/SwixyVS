using System;
using System.Collections.Generic;
using System.Linq;
using SwixySkyBlock.Net;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock;

internal sealed class IslandGeneratorLabelRenderer : IRenderer
{
    private const int RendererRange = 80;
    private const int MaxRenderDistance = 20;
    private const double MaxRenderDistanceSq = MaxRenderDistance * MaxRenderDistance;
    private readonly ICoreClientAPI api;
    private readonly Dictionary<int, LoadedTexture> texturesByLevel = new();
    private readonly List<IslandGeneratorLabelPacket> labels = [];
    private readonly CairoFont font;

    public double RenderOrder => 0.97;
    public int RenderRange => RendererRange;

    public IslandGeneratorLabelRenderer(ICoreClientAPI api)
    {
        this.api = api;
        font = CairoFont.WhiteSmallText().WithFontSize(15);
        font.StrokeWidth = 3;
        font.StrokeColor = [0, 0, 0, 0.85];
    }

    public void Apply(IslandGeneratorLabelsPacket packet)
    {
        labels.Clear();
        if (packet.Labels != null)
        {
            labels.AddRange(packet.Labels);
        }
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho || labels.Count == 0 || api.World?.Player?.Entity == null)
        {
            return;
        }

        var playerPos = api.World.Player.Entity.Pos.XYZ;
        foreach (var label in labels)
        {
            var generatorPos = new Vec3d(label.X + 0.5, label.Y + 0.5, label.Z + 0.5);
            if (playerPos.SquareDistanceTo(generatorPos) > MaxRenderDistanceSq)
            {
                continue;
            }

            var worldPos = new Vec3d(label.X + 0.5, label.Y + 1.55, label.Z + 0.5);
            var screen = MatrixToolsd.Project(
                worldPos,
                api.Render.PerspectiveProjectionMat,
                api.Render.PerspectiveViewMat,
                api.Render.FrameWidth,
                api.Render.FrameHeight);

            if (screen.Z < 0)
            {
                continue;
            }

            var texture = GetTexture(label.Level);
            var x = (float)(screen.X - texture.Width / 2.0);
            var y = (float)(api.Render.FrameHeight - screen.Y - texture.Height / 2.0);
            api.Render.Render2DLoadedTexture(texture, x, y, 45);
        }
    }

    public void Dispose()
    {
        foreach (var texture in texturesByLevel.Values.ToList())
        {
            texture.Dispose();
        }

        texturesByLevel.Clear();
        labels.Clear();
    }

    private LoadedTexture GetTexture(int level)
    {
        level = Math.Max(1, level);
        if (texturesByLevel.TryGetValue(level, out var texture))
        {
            return texture;
        }

        texture = api.Gui.TextTexture.GenTextTexture($"Генератор ресурсов\nУровень: {level}", font);
        texturesByLevel[level] = texture;
        return texture;
    }
}
