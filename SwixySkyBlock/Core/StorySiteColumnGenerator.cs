using System.Linq;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Единая точка генерации колонки: остров + ванильная структура.</summary>
internal static class StorySiteColumnGenerator
{
    public static int Generate(
        ICoreServerAPI api,
        StorySiteContext site,
        int chunkX,
        int chunkZ,
        IServerChunk[] chunks)
    {
        if (chunks.Length == 0)
        {
            return 0;
        }

        if (SkyBlockRuntime.Config.UseVanillaStoryTerrain)
        {
            SkyBlockVanillaStoryPocketGen.GenerateColumn(api, chunkX, chunkZ, chunks);
        }
        else
        {
            SkyBlockRockStrataGen.SeedTopRockForColumn(api, chunks);

            var chunkTouchesIsland = site.IslandColumns.Any(c => c.Cx == chunkX && c.Cz == chunkZ);
            if (chunkTouchesIsland)
            {
                StorySiteTerrainGen.GenerateColumn(api, site, chunkX, chunkZ, chunks);
            }
        }

        if (!site.IntersectsStructureColumn(chunkX, chunkZ))
        {
            return 0;
        }

        StoryStructurePlacer.SeedTerrainHeightMap(
            chunkX,
            chunkZ,
            chunks,
            site.Location,
            site.Structure.Placement);
        return StoryStructurePlacer.PlaceStructureColumn(
            api,
            site.Structure,
            site.Schematic,
            site.StartPos,
            site.RockBlock,
            site.WgenAccessor,
            chunkX,
            chunkZ,
            chunks);
    }
}
