using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixyQuestBook.Util.Audio
{
    public static class QuestbookSoundHelper
    {
        private const string ModId = "swixyquestbook";

        public static void PlayBookOpening(ICoreClientAPI api) => PlaySound(api, "bookopening");
        public static void PlayBookClosing(ICoreClientAPI api) => PlaySound(api, "bookclosing");
        public static void PlayCompleted(ICoreClientAPI api) => PlaySound(api, "completed");

        private static void PlaySound(ICoreClientAPI api, string soundName)
        {
            try
            {
                var player = api.World.Player?.Entity;
                if (player == null) return;

                api.World.PlaySoundAt(
                    new AssetLocation($"{ModId}:sounds/{soundName}.ogg"),
                    player.Pos.X, player.Pos.Y, player.Pos.Z
                );
            }
            catch { }
        }
    }
}
