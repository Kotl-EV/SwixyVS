using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkMod"/> — вспомогательные типы серверной логики.</summary>
public sealed partial class SwixyClaimChunkMod
{
    /// <summary>Результат серверной операции: ключ локализации и тип (0 — успех, 1 — ошибка) для UI.</summary>
    private readonly struct ClaimActionResult
    {
        public readonly string? LangKey;
        public readonly object[] Args;
        public readonly string? CompositeMessage;
        public readonly int MessageType;

        private ClaimActionResult(string? langKey, object[] args, string? compositeMessage, int messageType)
        {
            LangKey = langKey;
            Args = args;
            CompositeMessage = compositeMessage;
            MessageType = messageType;
        }

        public bool HasMessage => !string.IsNullOrEmpty(LangKey) || !string.IsNullOrEmpty(CompositeMessage);

        public string Resolve(IServerPlayer player)
        {
            if (!string.IsNullOrEmpty(CompositeMessage))
            {
                return CompositeMessage;
            }

            return string.IsNullOrEmpty(LangKey)
                ? ""
                : Lang.GetL(player.LanguageCode, LangKey, Args);
        }

        public static ClaimActionResult Success() => new(null, [], null, 0);

        public static ClaimActionResult Success(string langKey, params object[] args) => new(langKey, args, null, 0);

        public static ClaimActionResult SuccessComposite(string localizedMessage) => new(null, [], localizedMessage, 0);

        public static ClaimActionResult Error(string langKey, params object[] args) => new(langKey, args, null, 1);
    }

    /// <summary>Сериализуемые данные со-владельцев для SaveGame.</summary>
    [ProtoContract]
    private sealed class CoOwnerSaveData
    {
        [ProtoMember(1)]
        public Dictionary<string, List<string>> Entries { get; set; } = [];
    }
}
