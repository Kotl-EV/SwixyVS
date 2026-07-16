using System;
using System.Linq;
using SwixySkyBlock.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

public sealed partial class SwixySkyBlockServerMod
{
    private readonly StoryDungeonRegistry storyDungeonRegistry = new();
    private bool storySitesQueued;

    private void OnStoryDungeonStateRequest(IServerPlayer player, StoryDungeonStateRequestPacket _)
    {
        SendStoryDungeonState(player, "", 0);
    }

    private void OnStoryDungeonTeleportRequest(IServerPlayer player, StoryDungeonTeleportRequestPacket packet)
    {
        if (serverApi == null || !IsSkyBlockWorld)
        {
            return;
        }

        serverApi.Logger.Notification(
            "[SwixySkyBlock][Story] Teleport request from {0} for '{1}' (placed={2}, generating={3}).",
            player.PlayerName,
            packet.Code,
            storyDungeonRegistry.Get(packet.Code)?.Placed == true,
            StorySiteGenerationService.IsGenerating(packet.Code));

        var definition = StoryDungeonDefinitions.TryGet(packet.Code);
        var record = storyDungeonRegistry.Get(packet.Code);
        if (definition == null || record == null)
        {
            SendStoryDungeonState(
                player,
                Lang.GetL(player.LanguageCode, "swixyskyblock:story-error-not-found"),
                1);
            return;
        }

        if (!record.Placed)
        {
            QueueStoryDungeonPlacement(definition, record);
            SendStoryDungeonState(
                player,
                Lang.GetL(player.LanguageCode, "swixyskyblock:story-error-not-ready"),
                1);
            return;
        }

        var spawn = StoryStructurePlacer.ResolveSafeStorySpawn(serverApi, record.Spawn, record.Center);
        if (!spawn.Equals(record.Spawn))
        {
            record.Spawn = spawn;
            storyDungeonRegistry.Save(serverApi);
        }

        player.Entity.TeleportTo(spawn.ToVec3d().Add(0.5, 0, 0.5));
        SendStoryDungeonState(
            player,
            Lang.GetL(player.LanguageCode, "swixyskyblock:story-message-teleported", ResolveStorySiteName(player.LanguageCode, definition)),
            0);
    }

    private void SendStoryDungeonState(IServerPlayer player, string message, int messageType)
    {
        if (serverChannel == null)
        {
            return;
        }

        serverChannel.SendPacket(BuildStoryDungeonStatePacket(player, message, messageType), [player]);
    }

    private StoryDungeonStatePacket BuildStoryDungeonStatePacket(
        IServerPlayer player,
        string message,
        int messageType)
    {
        var langCode = player.LanguageCode;
        var sites = StoryDungeonDefinitions.All
            .OrderBy(static definition => definition.StoryOrder)
            .Select(definition =>
            {
                var record = storyDungeonRegistry.Get(definition.Code);
                return new StoryDungeonSiteStatePacket
                {
                    Code = definition.Code,
                    Name = ResolveStorySiteName(langCode, definition),
                    Ready = record?.Placed == true,
                    Generating = StorySiteGenerationService.IsPending(definition.Code),
                    Order = definition.StoryOrder,
                    Corner = (int)definition.Anchor
                };
            })
            .ToList();

        return new StoryDungeonStatePacket
        {
            Sites = sites,
            Message = message,
            MessageType = messageType
        };
    }

    private static string ResolveStorySiteName(string langCode, StoryDungeonDefinition definition) =>
        Lang.GetL(langCode, definition.LangKey);

    private void LoadStoryDungeons()
    {
        if (serverApi == null || !IsSkyBlockWorld)
        {
            return;
        }

        storyDungeonRegistry.Load(serverApi);
        storyDungeonRegistry.EnsurePlacementVersion(serverApi, StoryDungeonDefinitions.PlacementVersion);
        StorySiteGenerationService.ResetForWorldLoad();
        storySitesQueued = false;
    }

    private void OnRunGameStorySites()
    {
        if (storySitesQueued || !SkyBlockRuntime.Config.AutoGenerateStorySites)
        {
            return;
        }

        storySitesQueued = true;
        StartStorySiteGeneration();
    }

    private void StartStorySiteGeneration()
    {
        if (serverApi == null || !IsSkyBlockWorld)
        {
            return;
        }

        StorySiteGenerationService.ScheduleStart(
            serverApi,
            storyDungeonRegistry,
            onComplete: () =>
            {
                if (serverApi == null || !IsSkyBlockWorld)
                {
                    return;
                }

                storyDungeonRegistry.Save(serverApi);
                BroadcastStoryDungeonState();
            },
            onSiteComplete: _ =>
            {
                if (serverApi == null || !IsSkyBlockWorld)
                {
                    return;
                }

                storyDungeonRegistry.Save(serverApi);
                BroadcastStoryDungeonState();
            });
    }

    private void OnPlayerJoinStoryState(IServerPlayer player)
    {
        if (!IsSkyBlockWorld)
        {
            return;
        }

        SendStoryDungeonState(player, "", 0);
    }

    private void QueueStoryDungeonPlacement(StoryDungeonDefinition definition, StoryDungeonRecord record)
    {
        if (serverApi == null || record.Placed)
        {
            return;
        }

        if (!storySitesQueued)
        {
            storySitesQueued = true;
            StartStorySiteGeneration();
        }

        BroadcastStoryDungeonState();
    }

    private void BroadcastStoryDungeonState()
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        foreach (var player in serverApi.World.AllOnlinePlayers)
        {
            if (player is IServerPlayer serverPlayer)
            {
                SendStoryDungeonState(serverPlayer, "", 0);
            }
        }
    }

}
