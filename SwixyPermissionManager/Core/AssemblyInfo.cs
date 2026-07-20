using System.Runtime.CompilerServices;
using Vintagestory.API.Common;

[assembly: ModDependency("game", "1.22.0")]
[assembly: ModInfo(
    "SwixyPermissionManager",
    "swixypermissionmanager",
    Website = "https://github.com/tehtelev/Swixy",
    Description = "Permission groups and rights manager for Swixy mods.",
    Version = "1.0.0",
    Authors =
    [
        "Tehtelev",
        "Kotl"
    ]
)]

// CakeBuild splits Core into SwixyPermissionManager.Shared.dll; side assemblies need internal types.
[assembly: InternalsVisibleTo("SwixyPermissionManager.Server")]
[assembly: InternalsVisibleTo("SwixyPermissionManager.Client")]
