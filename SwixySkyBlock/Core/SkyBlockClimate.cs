namespace SwixySkyBlock;

/// <summary>Единый умеренный климат SkyBlock (не зависит от координат).</summary>
internal static class SkyBlockClimate
{
    /// <summary>Годовая средняя температура, °C (умеренный пояс).</summary>
    public const float AnnualMeanTemperatureC = 10f;

    /// <summary>Осадки 0..1.</summary>
    public const float Rainfall = 0.45f;

    /// <summary>Широта для сезонных колебаний (~20°C как в умеренном климате).</summary>
    public const double SeasonLatitude = 0.3;
}
