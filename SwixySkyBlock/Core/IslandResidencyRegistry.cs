using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixySkyBlock;

[ProtoContract]
internal sealed class IslandResidencyRecord
{
    [ProtoMember(1)] public string PlayerUid { get; set; } = "";
    [ProtoMember(2)] public string HostPlayerUid { get; set; } = "";
}

[ProtoContract]
internal sealed class IslandResidencySaveData
{
    [ProtoMember(1)] public List<IslandResidencyRecord> Entries { get; set; } = [];
}

internal sealed class IslandResidencyRegistry
{
    private readonly Dictionary<string, string> hostByPlayer = new();

    public void Load(ICoreServerAPI api)
    {
        hostByPlayer.Clear();
        var bytes = api.WorldManager.SaveGame.GetData(SkyBlockWorld.SaveKeyIslandResidency);
        if (bytes == null || bytes.Length == 0)
        {
            return;
        }

        var data = SerializerUtil.Deserialize<IslandResidencySaveData>(bytes);
        foreach (var entry in data.Entries)
        {
            if (string.IsNullOrEmpty(entry.PlayerUid) || string.IsNullOrEmpty(entry.HostPlayerUid))
            {
                continue;
            }

            hostByPlayer[entry.PlayerUid] = entry.HostPlayerUid;
        }

        api.Logger.Notification("[SwixySkyBlock] Loaded {0} island residency record(s).", hostByPlayer.Count);
    }

    public void Save(ICoreServerAPI api)
    {
        var data = new IslandResidencySaveData
        {
            Entries = hostByPlayer.Select(kv => new IslandResidencyRecord
            {
                PlayerUid = kv.Key,
                HostPlayerUid = kv.Value
            }).ToList()
        };

        api.WorldManager.SaveGame.StoreData(
            SkyBlockWorld.SaveKeyIslandResidency,
            SerializerUtil.Serialize(data));
    }

    public bool Has(string playerUid) => hostByPlayer.ContainsKey(playerUid);

    public string? GetHost(string playerUid) =>
        hostByPlayer.TryGetValue(playerUid, out var host) ? host : null;

    public void Set(ICoreServerAPI api, string playerUid, string hostPlayerUid)
    {
        hostByPlayer[playerUid] = hostPlayerUid;
        Save(api);
    }

    public void Remove(ICoreServerAPI api, string playerUid)
    {
        if (!hostByPlayer.Remove(playerUid))
        {
            return;
        }

        Save(api);
    }

    public void RemoveAllForHost(ICoreServerAPI api, string hostPlayerUid)
    {
        var residents = hostByPlayer.Where(kv => kv.Value == hostPlayerUid).Select(kv => kv.Key).ToList();
        if (residents.Count == 0)
        {
            return;
        }

        foreach (var uid in residents)
        {
            hostByPlayer.Remove(uid);
        }

        Save(api);
    }
}
