using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>
/// Каждая сюжетная локация готовится в своём worker-потоке;
/// запись в мир — по одному шагу на локацию за тик главного потока.
/// </summary>
internal static class StoryDungeonPlacementService
{
    private const int SchedulerDelayMs = 100;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, SitePlacement> ActiveSites = new(StringComparer.OrdinalIgnoreCase);
    private static ICoreServerAPI? schedulerApi;
    private static bool schedulerQueued;

    private enum SiteStage
    {
        Preparing,
        PlacingIsland,
        PlacingStructure,
        Done
    }

    private sealed class SitePlacement
    {
        public required StoryDungeonDefinition Definition { get; init; }
        public required StoryDungeonRecord Record { get; init; }
        public required Action<bool> OnComplete { get; init; }
        public SiteStage Stage = SiteStage.Preparing;
        public PreparedStoryIsland? Prepared;
        public int IslandColumnIndex;
        public StoryStructurePlacementSession? StructureSession;
        public bool IslandChunkLoadPending;
    }

    public static bool IsGenerating(string code)
    {
        lock (Sync)
        {
            return ActiveSites.ContainsKey(code);
        }
    }

    public static void EnqueuePlacement(
        ICoreServerAPI api,
        StoryDungeonDefinition definition,
        StoryDungeonRecord record,
        Action<bool> onComplete)
    {
        if (record.Placed)
        {
            onComplete(false);
            return;
        }

        lock (Sync)
        {
            if (ActiveSites.ContainsKey(definition.Code))
            {
                onComplete(false);
                return;
            }

            ActiveSites[definition.Code] = new SitePlacement
            {
                Definition = definition,
                Record = record,
                OnComplete = onComplete
            };
        }

        StartWorkerPreparation(api, definition, record);
        EnsureScheduler(api);
    }

    private static void StartWorkerPreparation(
        ICoreServerAPI api,
        StoryDungeonDefinition definition,
        StoryDungeonRecord record)
    {
        var worldSeed = api.World.Seed;
        var center = record.Center.Copy();

        api.Logger.Notification(
            "[SwixySkyBlock] Preparing story site '{0}' on worker thread...",
            definition.Code);

        Task.Run(() => PreparedStoryIsland.Build(definition, worldSeed, center))
            .ContinueWith(task =>
            {
                api.Event.RegisterCallback(_ =>
                {
                    lock (Sync)
                    {
                        if (!ActiveSites.ContainsKey(definition.Code))
                        {
                            return;
                        }
                    }

                    if (task.IsCanceled || task.IsFaulted)
                    {
                        var message = task.Exception?.GetBaseException().Message ?? "cancelled";
                        api.Logger.Warning(
                            "[SwixySkyBlock] Worker preparation failed for '{0}': {1}",
                            definition.Code,
                            message);
                        CompleteSite(api, definition.Code, false);
                        return;
                    }

                    lock (Sync)
                    {
                        if (!ActiveSites.TryGetValue(definition.Code, out var site))
                        {
                            return;
                        }

                        site.Prepared = task.Result;
                        site.Stage = SiteStage.PlacingIsland;
                    }

                    api.Logger.Notification(
                        "[SwixySkyBlock] Story site '{0}' ready for placement ({1} island column(s)).",
                        definition.Code,
                        task.Result.ColumnOrder.Count);

                    EnsureScheduler(api);
                }, 0);
            });
    }

    private static void EnsureScheduler(ICoreServerAPI api)
    {
        lock (Sync)
        {
            schedulerApi = api;
            if (schedulerQueued || ActiveSites.Count == 0)
            {
                return;
            }

            schedulerQueued = true;
        }

        api.Event.RegisterCallback(_ => RunSchedulerTick(), SchedulerDelayMs);
    }

    private static void RunSchedulerTick()
    {
        ICoreServerAPI? api;
        List<SitePlacement> sites;

        lock (Sync)
        {
            schedulerQueued = false;
            api = schedulerApi;
            sites = [.. ActiveSites.Values];
        }

        if (api == null || sites.Count == 0)
        {
            return;
        }

        var anyActive = false;
        foreach (var site in sites)
        {
            if (site.Stage == SiteStage.Done)
            {
                continue;
            }

            anyActive = true;
            AdvanceSite(api, site);
        }

        if (anyActive)
        {
            EnsureScheduler(api);
        }
    }

    private static void AdvanceSite(ICoreServerAPI api, SitePlacement site)
    {
        switch (site.Stage)
        {
            case SiteStage.Preparing:
                return;

            case SiteStage.PlacingIsland:
                if (!AdvanceIslandColumn(api, site))
                {
                    return;
                }

                site.Stage = SiteStage.PlacingStructure;
                SkyBlockClimateMaps.EnsureForBlockPos(
                    api,
                    site.Record.Center.X,
                    site.Record.Center.Z);
                site.StructureSession = StoryStructurePlacementSession.TryCreate(
                    api,
                    site.Definition,
                    site.Record.Center);
                if (site.StructureSession == null)
                {
                    api.Logger.Warning(
                        "[SwixySkyBlock] Vanilla story structure placement failed for '{0}' at {1}.",
                        site.Definition.Code,
                        site.Record.Center);
                    CompleteSite(api, site.Definition.Code, false);
                }

                return;

            case SiteStage.PlacingStructure:
                if (site.StructureSession == null)
                {
                    CompleteSite(api, site.Definition.Code, false);
                    return;
                }

                var result = site.StructureSession.TryAdvanceColumn();
                switch (result)
                {
                    case StoryStructureAdvanceResult.InProgress:
                    case StoryStructureAdvanceResult.WaitingForChunk:
                        return;

                    case StoryStructureAdvanceResult.Succeeded:
                        site.Record.Spawn = site.StructureSession.Spawn!;
                        TryTriggerStoryWorldgenHook(api, site.Definition, site.Record.Spawn);
                        site.Record.Placed = true;
                        api.Logger.Notification(
                            "[SwixySkyBlock] Story site '{0}' placed at center {1}, spawn {2}.",
                            site.Definition.Code,
                            site.Record.Center,
                            site.Record.Spawn);
                        CompleteSite(api, site.Definition.Code, true);
                        return;

                    case StoryStructureAdvanceResult.Failed:
                        api.Logger.Warning(
                            "[SwixySkyBlock] Vanilla story structure placement failed for '{0}' at {1}.",
                            site.Definition.Code,
                            site.Record.Center);
                        CompleteSite(api, site.Definition.Code, false);
                        return;
                }

                return;
        }
    }

    private static bool AdvanceIslandColumn(ICoreServerAPI api, SitePlacement site)
    {
        var prepared = site.Prepared;
        if (prepared == null)
        {
            return true;
        }

        if (site.IslandColumnIndex >= prepared.ColumnOrder.Count)
        {
            return true;
        }

        if (site.IslandChunkLoadPending)
        {
            return false;
        }

        var (cx, cz) = prepared.ColumnOrder[site.IslandColumnIndex];
        var chunks = StoryDungeonChunkLoader.LoadColumnChunks(api, cx, cz);
        if (chunks == null)
        {
            site.IslandChunkLoadPending = true;
            api.WorldManager.LoadChunkColumnFast(cx, cz, new ChunkLoadOptions
            {
                KeepLoaded = false,
                OnLoaded = () => api.Event.RegisterCallback(_ => site.IslandChunkLoadPending = false, 0)
            });
            return false;
        }

        if (prepared.ColumnBlocks.TryGetValue((cx, cz), out var blocks))
        {
            IslandPlacer.PlacePrecomputedColumn(
                api,
                new Vec2i(cx, cz),
                chunks,
                prepared.Origin,
                prepared.Island.Schematic,
                blocks);
        }

        site.IslandColumnIndex++;
        return site.IslandColumnIndex >= prepared.ColumnOrder.Count;
    }

    private static void CompleteSite(ICoreServerAPI api, string code, bool placed)
    {
        SitePlacement? site;
        lock (Sync)
        {
            if (!ActiveSites.TryGetValue(code, out site))
            {
                return;
            }

            site.Stage = SiteStage.Done;
            ActiveSites.Remove(code);
        }

        site.OnComplete(placed);
        EnsureScheduler(api);
    }

    private static void TryTriggerStoryWorldgenHook(
        ICoreServerAPI api,
        StoryDungeonDefinition definition,
        BlockPos schematicOrigin)
    {
        if (string.IsNullOrWhiteSpace(definition.WorldgenHook))
        {
            return;
        }

        api.Event.RegisterCallback(
            _ =>
            {
                try
                {
                    api.Event.TriggerWorldgenHook(
                        definition.WorldgenHook,
                        api.World.BlockAccessor,
                        schematicOrigin,
                        definition.Code);
                }
                catch (Exception ex)
                {
                    api.Logger.Warning(
                        "[SwixySkyBlock] Worldgen hook '{0}' failed for '{1}': {2}",
                        definition.WorldgenHook,
                        definition.Code,
                        ex.Message);
                }
            },
            500);
    }
}