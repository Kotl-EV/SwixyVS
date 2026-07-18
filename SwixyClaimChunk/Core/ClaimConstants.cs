namespace SwixyClaimChunk.Core;

/// <summary>Общие константы мода (канал, hotkey, радиусы, ключи SaveGame).</summary>
public static class ClaimConstants
{
    public const string ChannelName = "SwixyClaimChunk";
    public const string OpenMapHotkeyCode = "swixyclaimchunkopenmap";
    public const int DefaultRadius = 10;
    public const int MaxRadius = 32;
    public const int ProtectionLevel = 1;
    public const string CoOwnersSaveKey = "swixyclaimchunk_coowners";
    public const string UseFiltersSaveKey = "swixyclaimchunk_use_filters";
    public const string ClaimFlagsSaveKey = "swixyclaimchunk_claim_flags";

    /// <summary>EntityBehavior name for PvP / animal protection.</summary>
    public const string ClaimProtectBehaviorCode = "swixyclaimchunkclaimprotect";
}
