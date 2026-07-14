using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Реестр сюжетных локаций и их координат в сейве.</summary>
internal sealed class StoryDungeonRegistry
{
    private readonly Dictionary<string, StoryDungeonRecord> records = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<StoryDungeonRecord> All => records.Values;

    public StoryDungeonRecord? Get(string code) =>
        records.TryGetValue(code, out var record) ? record : null;

    public void EnsureDefinitions(ICoreServerAPI api)
    {
        foreach (var definition in StoryDungeonDefinitions.All)
        {
            if (records.ContainsKey(definition.Code))
            {
                continue;
            }

            var center = StoryDungeonDefinitions.ComputeSiteCenter(api, definition);
            records[definition.Code] = new StoryDungeonRecord
            {
                Code = definition.Code,
                Center = center,
                Spawn = center.UpCopy(2),
                Placed = false
            };
        }
    }

    public void Load(ICoreServerAPI api)
    {
        records.Clear();
        EnsureDefinitions(api);

        var bytes = api.WorldManager.SaveGame.GetData(SkyBlockWorld.SaveKeyStoryDungeons);
        if (bytes == null || bytes.Length == 0)
        {
            return;
        }

        try
        {
            var json = Encoding.UTF8.GetString(bytes);
            var saved = JsonSerializer.Deserialize<List<StoryDungeonSaveDto>>(json);
            if (saved == null)
            {
                return;
            }

            foreach (var dto in saved)
            {
                if (!records.TryGetValue(dto.Code, out var record))
                {
                    continue;
                }

                record.Center = new BlockPos(dto.CenterX, dto.CenterY, dto.CenterZ);
                record.Spawn = new BlockPos(dto.SpawnX, dto.SpawnY, dto.SpawnZ);
                record.Placed = dto.Placed;
            }
        }
        catch (Exception ex)
        {
            api.Logger.Warning("[SwixySkyBlock] Failed to load story dungeon save data: {0}", ex.Message);
        }
    }

    public void EnsurePlacementVersion(ICoreServerAPI api, int expectedVersion)
    {
        var bytes = api.WorldManager.SaveGame.GetData(SkyBlockWorld.SaveKeyStoryPlacementVersion);
        var currentVersion = 0;
        if (bytes != null && bytes.Length >= 4)
        {
            currentVersion = BitConverter.ToInt32(bytes, 0);
        }

        if (currentVersion >= expectedVersion)
        {
            return;
        }

        foreach (var record in records.Values)
        {
            record.Placed = false;
        }

        api.WorldManager.SaveGame.StoreData(
            SkyBlockWorld.SaveKeyStoryPlacementVersion,
            BitConverter.GetBytes(expectedVersion));
        api.Logger.Notification(
            "[SwixySkyBlock] Story placement version upgraded {0} -> {1}; locations will be regenerated.",
            currentVersion,
            expectedVersion);
    }

    public void Save(ICoreServerAPI api)
    {
        var payload = records.Values
            .OrderBy(static record => record.Code, StringComparer.OrdinalIgnoreCase)
            .Select(static record => new StoryDungeonSaveDto
            {
                Code = record.Code,
                CenterX = record.Center.X,
                CenterY = record.Center.Y,
                CenterZ = record.Center.Z,
                SpawnX = record.Spawn.X,
                SpawnY = record.Spawn.Y,
                SpawnZ = record.Spawn.Z,
                Placed = record.Placed
            })
            .ToList();

        var json = JsonSerializer.Serialize(payload);
        api.WorldManager.SaveGame.StoreData(SkyBlockWorld.SaveKeyStoryDungeons, Encoding.UTF8.GetBytes(json));
    }

    private sealed class StoryDungeonSaveDto
    {
        public string Code { get; set; } = "";
        public int CenterX { get; set; }
        public int CenterY { get; set; }
        public int CenterZ { get; set; }
        public int SpawnX { get; set; }
        public int SpawnY { get; set; }
        public int SpawnZ { get; set; }
        public bool Placed { get; set; }
    }
}