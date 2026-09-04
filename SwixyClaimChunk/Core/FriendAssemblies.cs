using System.Runtime.CompilerServices;

// Lives in Shared.dll so Server/Client assemblies can see internal types.
[assembly: InternalsVisibleTo("SwixyClaimChunk.Server")]
[assembly: InternalsVisibleTo("SwixyClaimChunk.Client")]
