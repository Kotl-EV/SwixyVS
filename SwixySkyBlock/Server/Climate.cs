using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Одинаковый умеренный климат на любых координатах, сезоны включены.</summary>
public sealed partial class SwixySkyBlockMod
{
    /// <summary>Регистрируем OnGetClimate до survival, чтобы подменить базовую температуру.</summary>
    public override double ExecuteOrder() => 0.01;

    private void RegisterClimateHandlers(ICoreServerAPI api)
    {
        api.Event.OnGetClimate += OnUniformClimate;
        api.Event.SaveGameLoaded += OnClimateSaveLoaded;
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnClimateRunGame);
    }

    private void OnClimateSaveLoaded()
    {
        ApplyUniformLatitude(serverApi);
    }

    private void OnClimateRunGame()
    {
        ApplyUniformLatitude(serverApi);
    }

    private static void ApplyUniformLatitude(ICoreServerAPI? api)
    {
        if (api == null || !SkyBlockWorld.IsSkyBlockWorld(api))
        {
            return;
        }

        api.World.Calendar.OnGetLatitude = _ => SkyBlockClimate.SeasonLatitude;
    }

    private void OnUniformClimate(
        ref ClimateCondition climate,
        BlockPos pos,
        EnumGetClimateMode mode,
        double totalDays)
    {
        if (serverApi == null || !SkyBlockWorld.IsSkyBlockWorld(serverApi))
        {
            return;
        }

        climate.WorldGenTemperature = SkyBlockClimate.AnnualMeanTemperatureC;
        climate.WorldgenRainfall = SkyBlockClimate.Rainfall;

        if (mode == EnumGetClimateMode.WorldGenValues)
        {
            climate.Temperature = SkyBlockClimate.AnnualMeanTemperatureC;
            climate.Rainfall = SkyBlockClimate.Rainfall;
        }
    }
}
