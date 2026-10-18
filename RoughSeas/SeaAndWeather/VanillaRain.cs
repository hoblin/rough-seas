using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.SeaAndWeather;

public class VanillaRain(SeaWeatherSystem sea, ICoreAPI api)
{
    private WeatherSystemBase weather = null!;

    public void Follow()
    {
        weather = api.ModLoader.GetModSystem<WeatherSystemBase>();
        api.Event.OnGetClimate += OnGetClimate;
    }

    private void OnGetClimate(ref ClimateCondition climate, BlockPos pos, EnumGetClimateMode mode, double totalDays)
    {
        if (mode is EnumGetClimateMode.WorldGenValues or EnumGetClimateMode.ForSuppliedDate_TemperatureOnly || weather.OverridePrecipitation != null) return;
        if (sea.Source.RainCloudness(pos.X + 0.5, pos.Z + 0.5, totalDays) is not { } cloudness) return;
        climate.Rainfall = GameMath.Clamp(cloudness - 0.5f, 0, 1);
        climate.RainCloudOverlay = GameMath.Clamp(cloudness, 0, 1);
    }
}
