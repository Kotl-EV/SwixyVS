namespace SwixyClaimChunk.Core;

/// <summary>Общие константы мода (канал, hotkey, радиусы, ключи SaveGame, net limits).</summary>
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

    // ── Network hardening (DoS / abuse) ──

    /// <summary>Макс. чанков в одном batch (прямоугольник/лассо).</summary>
    public const int MaxBatchChunks = 400;

    /// <summary>Макс. кодов в Use whitelist.</summary>
    public const int MaxUseFilterCodes = 64;

    /// <summary>Макс. длина raw-строки UseFilterCodesRaw.</summary>
    public const int MaxUseFilterCodesRawLength = 4096;

    /// <summary>Макс. длина имени привата.</summary>
    public const int MaxClaimNameLength = 48;

    /// <summary>Макс. длина ника / UID в пакетах.</summary>
    public const int MaxPlayerNameLength = 64;

    public const int MaxPlayerUidLength = 64;

    /// <summary>Макс. длина Message в ответах UI.</summary>
    public const int MaxPacketMessageLength = 256;

    /// <summary>Rate limits (мс между одинаковыми действиями одного игрока).</summary>
    public const int RateMapRequestMs = 150;
    public const int RateChunkActionMs = 80;
    public const int RateBatchMs = 250;
    public const int RateClaimListMs = 400;
    public const int RateAccessActionMs = 120;
    public const int RateShowMs = 200;
    public const int RateUseFiltersRequestMs = 1000;
    public const int RateUseFilterScanMs = 2500;
}
