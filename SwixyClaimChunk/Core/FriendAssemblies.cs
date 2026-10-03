using System.Runtime.CompilerServices;

// Находится в Shared.dll, чтобы Server/Client сборки видели внутренние типы.
[assembly: InternalsVisibleTo("SwixyClaimChunk.Server")]
[assembly: InternalsVisibleTo("SwixyClaimChunk.Client")]