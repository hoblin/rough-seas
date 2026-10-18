using System.Globalization;
using Newtonsoft.Json;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace RoughSeas.Tests.SeaAndWeather;

public static class Fixtures
{
    public const double NorthernLatitude = 0.5;
    public const double HoursPerYear = 12 * 9 * 24;

    private static readonly Lazy<SeaWeatherConfig> config = new(() =>
        JsonConvert.DeserializeObject<SeaWeatherConfig>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "seaweather.json")))!);

    public static SeaWeatherConfig Config() => config.Value;

    public const int MapSize = 1024000;

    public static SeaWeatherModel Model(long seed, double latitude = NorthernLatitude) => Model(seed, _ => latitude);

    public static SeaWeatherModel Model(long seed, Func<double, double> latitude) =>
        new(seed, Config(), new WorldClimate(seed, new TreeAttribute(), MapSize), Basins(seed), latitude, HoursPerYear);

    public static OceanBasins Basins(long seed, string landcover = "0.3") =>
        new(Ocean(seed, landcover), new OceanCalibration(Config().Ocean), Config().Currents);

    public static GeneratedOcean Ocean(long seed, string landcover = "0.3") =>
        new(() => GenMaps.GetOceanMapGen(seed + 1873, float.Parse(landcover, CultureInfo.InvariantCulture), TerraGenConfig.oceanMapScale, 1,
            [new XZ(MapSize / 2 / TerraGenConfig.oceanMapScale, MapSize / 2 / TerraGenConfig.oceanMapScale)], false));

    public static Storm[] YearOfStorms(SeaWeatherModel model) =>
        Enumerable.Range(0, 24)
            .SelectMany(fortnight => model.Storms.Active(512000, 512000, fortnight * HoursPerYear / 24, range: 30000))
            .Distinct()
            .ToArray();
}
