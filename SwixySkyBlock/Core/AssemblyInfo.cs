using System.Runtime.CompilerServices;
using Vintagestory.API.Common;

[assembly: ModDependency("game", "1.22.0")]
[assembly: ModDependency("survival", "1.22.0")]
[assembly: ModInfo(
    "SwixySkyBlock",
    "swixyskyblock",
    Website = "https://github.com/tehtelev/Swixy",
    Description = "SkyBlock gameplay for Vintage Story.",
    Version = "1.0.0",
    Authors =
    [
        "Tehtelev",
        "Kotl"
    ]
)]

// CakeBuild splits Core into SwixySkyBlock.Shared.dll; side assemblies need internal types.
[assembly: InternalsVisibleTo("SwixySkyBlock.Server")]
[assembly: InternalsVisibleTo("SwixySkyBlock.Client")]
