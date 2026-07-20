using System.Runtime.CompilerServices;
using Vintagestory.API.Common;

[assembly: ModDependency("game", "1.22.0")]
[assembly: ModInfo(
    "SwixyClaimChunk",
    "swixyclaimchunk",
    Website = "https://github.com/tehtelev/Swixy",
    Description = "Chunk claim map interface.",
    Version = "1.0.5",
    Authors =
    [
        "Tehtelev",
        "Kotl"
    ]
)]

// CakeBuild splits Core into SwixyClaimChunk.Shared.dll; side assemblies need internal types.
[assembly: InternalsVisibleTo("SwixyClaimChunk.Server")]
[assembly: InternalsVisibleTo("SwixyClaimChunk.Client")]
