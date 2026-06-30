using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixySkyBlock;

internal sealed class PlayerIslandRegistry
{
    private readonly Dictionary<string, PlayerIslandRecord> records = new();
    private readonly SortedSet<int> freeSlots = new();
    private int nextSlot;

    public IReadOnlyCollection<PlayerIslandRecord> All => records.Values;

    public void Load(ICoreServerAPI api)
    {
        records.Clear();
        freeSlots.Clear();
        nextSlot = 0;

        var bytes = api.WorldManager.SaveGame.GetData(SkyBlockWorld.SaveKeyPlayerIslands);
        if (bytes == null || bytes.Length == 0)
        {
            return;
        }

        var data = SerializerUtil.Deserialize<PlayerIslandSaveData>(bytes);
        foreach (var record in data.Islands)
        {
            if (string.IsNullOrEmpty(record.PlayerUid))
            {
                continue;
            }

            records[record.PlayerUid] = record;
            nextSlot = System.Math.Max(nextSlot, record.SlotIndex + 1);
        }

        foreach (var slot in data.FreeSlots.Where(slot => slot >= 0 && slot < nextSlot))
        {
            freeSlots.Add(slot);
        }

        api.Logger.Notification("[SwixySkyBlock] Loaded {0} player island record(s).", records.Count);
    }

    public void Save(ICoreServerAPI api)
    {
        var data = new PlayerIslandSaveData
        {
            Islands = records.Values.OrderBy(static record => record.SlotIndex).ToList(),
            FreeSlots = freeSlots.ToList()
        };

        api.WorldManager.SaveGame.StoreData(
            SkyBlockWorld.SaveKeyPlayerIslands,
            SerializerUtil.Serialize(data));
    }

    public PlayerIslandRecord Create(ICoreServerAPI api, string playerUid, string templateName)
    {
        var slot = AllocateSlot();
        var record = new PlayerIslandRecord
        {
            PlayerUid = playerUid,
            TemplateName = string.IsNullOrWhiteSpace(templateName) ? "starter" : templateName.Trim(),
            SlotIndex = slot
        };
        record.SetOrigin(SkyBlockWorld.ComputePlayerIslandOrigin(api, slot));

        records[playerUid] = record;
        Save(api);
        return record;
    }

    public void Add(ICoreServerAPI api, PlayerIslandRecord record)
    {
        if (string.IsNullOrEmpty(record.PlayerUid))
        {
            return;
        }

        records[record.PlayerUid] = record;
        nextSlot = System.Math.Max(nextSlot, record.SlotIndex + 1);
        freeSlots.Remove(record.SlotIndex);
        Save(api);
    }

    public PlayerIslandRecord? Get(string playerUid) =>
        records.TryGetValue(playerUid, out var record) ? record : null;

    public PlayerIslandRecord? GetRecord(string playerUid) => Get(playerUid);

    public bool Has(string playerUid) => records.ContainsKey(playerUid);

    public void Remove(ICoreServerAPI api, string playerUid)
    {
        if (!records.Remove(playerUid, out var record))
        {
            return;
        }

        freeSlots.Add(record.SlotIndex);
        Save(api);
    }

    public void Remove(string playerUid)
    {
        if (records.Remove(playerUid, out var record))
        {
            freeSlots.Add(record.SlotIndex);
        }
    }

    public BlockPos GetSpawn(ICoreServerAPI api, PlayerIslandRecord record)
    {
        var templates = IslandBlueprint.LoadAll(api);
        var template = templates.FirstOrDefault(t => t.Name == record.TemplateName)
            ?? IslandBlueprint.PickForWorld(templates);
        return template.GetSpawnPosition(record.Origin);
    }

    private int AllocateSlot()
    {
        if (freeSlots.Count == 0)
        {
            return nextSlot++;
        }

        var slot = freeSlots.Min;
        freeSlots.Remove(slot);
        return slot;
    }
}
