using Newtonsoft.Json;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.SeaAndWeather;

public static class Fixtures
{
    public const double NorthernLatitude = 0.5;
    public const double HoursPerYear = 12 * 9 * 24;

    public static SeaWeatherConfig Config() =>
        JsonConvert.DeserializeObject<SeaWeatherConfig>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "seaweather.json")))!;

    public static SeaWeatherModel Model(long seed, double latitude = NorthernLatitude) =>
        new(seed, Config(), new WorldClimate(seed, new TreeAttribute(), 1024000), _ => latitude, HoursPerYear);
}
