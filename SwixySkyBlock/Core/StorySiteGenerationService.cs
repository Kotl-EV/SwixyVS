using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>
/// Генерация сюжетных сайтов строго по очереди: один сайт полностью, затем следующий.
/// </summary>
internal static class StorySiteGenerationService
{
    private const int SchedulerDelayMs = 200;
    private const int StartDelayMs = 6000;

    private static readonly object Sync = new();
    private static readonly Queue<(StoryDungeonDefinition Definition, StoryDungeonRecord Record)> SiteQueue = new();
    private static SiteJob? activeJob;
    private static ICoreServerAPI? schedulerApi;
    private static bool schedulerQueued;
    private static bool pipelineStarted;
    private static bool startScheduled;
    private static int totalSites;
    private static int completedSites;
    private static Action? onAllComplete;
    private static Action<StorySiteContext>? onSiteComplete;

    private sealed class SiteJob
    {
        public required StorySiteContext Context { get; init; }
        public required List<(int Cx, int Cz)> Columns { get; init; }
        public int ColumnIndex;
        public int PlacedStructureBlocks;
        public bool ChunkLoadPending;
        public bool Finalized;
        public int ZeroBlockRetries;
    }

    public static void ResetForWorldLoad()
    {
        lock (Sync)
        {
            SiteQueue.Clear();
            activeJob = null;
            pipelineStarted = false;
            startScheduled = false;
            schedulerQueued = false;
            totalSites = 0;
            completedSites = 0;
        }
    }

    /// <summary>Сайт генерируется прямо сейчас (не просто стоит в очереди).</summary>
    public static bool IsGenerating(string code)
    {
        lock (Sync)
        {
            return activeJob?.Context.Definition.Code.Equals(code, StringComparison.OrdinalIgnoreCase) == true
                && !activeJob.Finalized;
        }
    }

    /// <summary>Сайт ещё не готов: в очереди или генерируется сейчас.</summary>
    public static StorySiteContext? GetActiveContextForChunk(int chunkX, int chunkZ)
    {
        lock (Sync)
        {
            if (activeJob == null || activeJob.Finalized)
            {
                return null;
            }

            var context = activeJob.Context;
            return context.IslandColumns.Any(c => c.Cx == chunkX && c.Cz == chunkZ)
                || context.IntersectsStructureColumn(chunkX, chunkZ)
                ? context
                : null;
        }
    }

    public static bool IsPending(string code)
    {
        lock (Sync)
        {
            if (!pipelineStarted)
            {
                return false;
            }

            if (activeJob?.Context.Definition.Code.Equals(code, StringComparison.OrdinalIgnoreCase) == true
                && !activeJob.Finalized)
            {
                return true;
            }

            return SiteQueue.Any(pair =>
                pair.Definition.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static void ScheduleStart(
        ICoreServerAPI api,
        StoryDungeonRegistry registry,
        Action? onComplete = null,
        Action<StorySiteContext>? onSiteComplete = null)
    {
        lock (Sync)
        {
            if (pipelineStarted)
            {
                return;
            }

            pipelineStarted = true;
            onAllComplete = onComplete;
            StorySiteGenerationService.onSiteComplete = onSiteComplete;
        }

        StorySiteRegistry.Initialize(api, registry);

        lock (Sync)
        {
            SiteQueue.Clear();
            foreach (var pair in StorySiteRegistry.PendingSites)
            {
                SiteQueue.Enqueue(pair);
            }

            activeJob = null;
            totalSites = SiteQueue.Count + completedSites;
            startScheduled = false;
        }

        if (SiteQueue.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        api.Logger.Notification(
            "[SwixySkyBlock] Story generation queue: {0} site(s), sequential (one at a time).",
            SiteQueue.Count);

        lock (Sync)
        {
            if (startScheduled)
            {
                return;
            }

            startScheduled = true;
        }

        api.Event.RegisterCallback(_ => BeginGeneration(api), StartDelayMs);
    }

    private static void BeginGeneration(ICoreServerAPI api)
    {
        lock (Sync)
        {
            if (SiteQueue.Count == 0 && activeJob == null)
            {
                onAllComplete?.Invoke();
                onAllComplete = null;
                return;
            }
        }

        EnsureScheduler(api);
    }

    private static void EnsureScheduler(ICoreServerAPI api)
    {
        lock (Sync)
        {
            schedulerApi = api;
            if (schedulerQueued)
            {
                return;
            }

            if (activeJob == null && SiteQueue.Count == 0)
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

        lock (Sync)
        {
            schedulerQueued = false;
            api = schedulerApi;
        }

        if (api == null)
        {
            return;
        }

        if (!TryEnsureActiveJob(api))
        {
            onAllComplete?.Invoke();
            onAllComplete = null;
            return;
        }

        var job = activeJob!;
        if (!job.Finalized && !job.ChunkLoadPending && job.ColumnIndex < job.Columns.Count)
        {
            AdvanceJobOneColumn(api, job);
        }

        if (!job.Finalized && job.ColumnIndex >= job.Columns.Count && !job.ChunkLoadPending)
        {
            TryFinalizeSite(api, job);
        }

        lock (Sync)
        {
            if (activeJob?.Finalized == true)
            {
                activeJob = null;
            }

            if (activeJob != null || SiteQueue.Count > 0)
            {
                EnsureScheduler(api);
            }
        }
    }

    private static bool TryEnsureActiveJob(ICoreServerAPI api)
    {
        lock (Sync)
        {
            if (activeJob != null && !activeJob.Finalized)
            {
                return true;
            }

            activeJob = null;

            if (SiteQueue.Count == 0)
            {
                return false;
            }

            var (definition, record) = SiteQueue.Dequeue();
            if (record.Placed)
            {
                return TryEnsureActiveJob(api);
            }

            var context = StorySiteRegistry.TryGetOrCreate(api, definition, record);
            if (context == null)
            {
                api.Logger.Warning(
                    "[SwixySkyBlock] Skipping story site '{0}' (context creation failed).",
                    definition.Code);
                return TryEnsureActiveJob(api);
            }

            var columns = new HashSet<(int, int)>();
            foreach (var column in context.IslandColumns)
            {
                columns.Add((column.Cx, column.Cz));
            }

            foreach (var column in context.StructureColumns)
            {
                columns.Add((column.Cx, column.Cz));
            }

            SkyBlockClimateMaps.EnsureForBlockPos(api, context.Center.X, context.Center.Z);
            activeJob = new SiteJob
            {
                Context = context,
                Columns = columns.OrderBy(c => c.Item1).ThenBy(c => c.Item2).ToList()
            };

            var index = completedSites + 1;
            var remaining = SiteQueue.Count + 1;
            api.Logger.Notification(
                "[SwixySkyBlock] Story site {0}/{1}: generating '{2}' ({3} column(s))...",
                index,
                index + SiteQueue.Count,
                definition.Code,
                activeJob.Columns.Count);
            return true;
        }
    }

    private static void AdvanceJobOneColumn(ICoreServerAPI api, SiteJob job)
    {
        var context = job.Context;
        var (cx, cz) = job.Columns[job.ColumnIndex];

        var chunks = StoryDungeonChunkLoader.LoadColumnChunks(api, cx, cz)?.OfType<IServerChunk>().ToArray();
        if (chunks != null && chunks.Length > 0)
        {
            job.PlacedStructureBlocks += StorySiteColumnGenerator.Generate(api, context, cx, cz, chunks);
            job.ColumnIndex++;
            return;
        }

        job.ChunkLoadPending = true;
        api.WorldManager.LoadChunkColumnFast(cx, cz, new ChunkLoadOptions
        {
            KeepLoaded = false,
            OnLoaded = () => api.Event.RegisterCallback(_ =>
            {
                job.ChunkLoadPending = false;
                if (StoryDungeonChunkLoader.LoadColumnChunks(api, cx, cz)?.OfType<IServerChunk>().ToArray()
                        is { Length: > 0 } loaded)
                {
                    job.PlacedStructureBlocks += StorySiteColumnGenerator.Generate(api, context, cx, cz, loaded);
                }

                job.ColumnIndex++;
            }, 0)
        });
    }

    private static void TryFinalizeSite(ICoreServerAPI api, SiteJob job)
    {
        var context = job.Context;
        if (job.PlacedStructureBlocks <= 0)
        {
            job.ZeroBlockRetries++;
            if (job.ZeroBlockRetries > 2)
            {
                api.Logger.Warning(
                    "[SwixySkyBlock] Story site '{0}' failed after retries (0 structure blocks).",
                    context.Definition.Code);
                job.Finalized = true;
                completedSites++;
                onSiteComplete?.Invoke(context);
                return;
            }

            api.Logger.Warning(
                "[SwixySkyBlock] Story site '{0}' generated 0 structure blocks; retrying ({1}/2).",
                context.Definition.Code,
                job.ZeroBlockRetries);
            job.ColumnIndex = 0;
            job.PlacedStructureBlocks = 0;
            return;
        }

        job.Finalized = true;
        var blockCount = StoryStructurePlacer.CountStructureBlocks(api, context.Location);
        StoryStructurePlacer.PlaceEntities(api, context.Schematic, context.StartPos, context.RockBlock);
        StoryStructurePlacer.ApplyLandClaims(api, context.Structure, context.Location);
        context.Record.Spawn = StoryStructurePlacer.ComputeSpawn(api, context.StartPos, context.Schematic, context.Location);
        context.Record.Placed = true;
        completedSites++;

        api.Logger.Notification(
            "[SwixySkyBlock] Story site '{0}' done ({1} blocks). Spawn {2}. Next in queue: {3}.",
            context.Definition.Code,
            blockCount,
            context.Record.Spawn,
            SiteQueue.Count > 0 ? SiteQueue.Peek().Definition.Code : "none");

        onSiteComplete?.Invoke(context);
        TriggerWorldgenHook(api, context);
        api.Event.RegisterCallback(
            _ => StoryStructurePlacer.RelightBounds(api, context.Location),
            StoryStructurePlacer.RelightDelayMs);
    }

    private static void TriggerWorldgenHook(ICoreServerAPI api, StorySiteContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Definition.WorldgenHook))
        {
            return;
        }

        api.Event.RegisterCallback(
            _ =>
            {
                try
                {
                    api.Event.TriggerWorldgenHook(
                        context.Definition.WorldgenHook,
                        api.World.BlockAccessor,
                        context.StartPos,
                        context.Definition.Code);
                }
                catch (Exception ex)
                {
                    api.Logger.Warning(
                        "[SwixySkyBlock] Worldgen hook '{0}' failed for '{1}': {2}",
                        context.Definition.WorldgenHook,
                        context.Definition.Code,
                        ex.Message);
                }
            },
            500);
    }
}
