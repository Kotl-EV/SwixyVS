using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Очередь сюжетных сайтов; полный контекст создаётся лениво по одному сайту.</summary>
internal static class StorySiteRegistry
{
    private static readonly Dictionary<string, StorySiteContext> Contexts =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly List<(StoryDungeonDefinition Definition, StoryDungeonRecord Record)> Pending = [];

    public static IReadOnlyList<(StoryDungeonDefinition Definition, StoryDungeonRecord Record)> PendingSites => Pending;

    public static void Initialize(ICoreServerAPI api, StoryDungeonRegistry registry)
    {
        Contexts.Clear();
        Pending.Clear();

        foreach (var record in registry.All.OrderBy(static r =>
            StoryDungeonDefinitions.TryGet(r.Code)?.StoryOrder ?? int.MaxValue))
        {
            if (record.Placed)
            {
                continue;
            }

            var definition = StoryDungeonDefinitions.TryGet(record.Code);
            if (definition == null)
            {
                continue;
            }

            Pending.Add((definition, record));
        }

        api.Logger.Notification(
            "[SwixySkyBlock] Story site queue ready ({0} pending site(s)).",
            Pending.Count);
    }

    public static StorySiteContext? TryGet(string code) =>
        Contexts.TryGetValue(code, out var context) ? context : null;

    public static StorySiteContext? TryGetOrCreate(
        ICoreServerAPI api,
        StoryDungeonDefinition definition,
        StoryDungeonRecord record)
    {
        if (Contexts.TryGetValue(definition.Code, out var existing))
        {
            return existing;
        }

        api.Logger.Notification(
            "[SwixySkyBlock] Preparing story site context '{0}'...",
            definition.Code);

        var context = StorySiteContext.TryCreate(api, definition, record);
        if (context != null)
        {
            Contexts[definition.Code] = context;
        }

        return context;
    }
}