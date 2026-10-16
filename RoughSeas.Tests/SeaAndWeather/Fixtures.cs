using Newtonsoft.Json;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.SeaAndWeather;

public static class Fixtures
{
    public const double NorthernLatitude = 0.5;
    public const double HoursPerYear = 12 * 9 * 24;

    private static readonly Lazy<SeaWeatherConfig> config = new(() =>
        JsonConvert.DeserializeObject<SeaWeatherConfig>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "seaweather.json")))!);

    public static SeaWeatherConfig Config() => config.Value;

    public static SeaWeatherModel Model(long seed, double latitude = NorthernLatitude) =>
        new(seed, Config(), new WorldClimate(seed, new TreeAttribute(), 1024000), _ => latitude, HoursPerYear);

    public static Storm[] YearOfStorms(SeaWeatherModel model) =>
        Enumerable.Range(0, 24)
            .SelectMany(fortnight => model.Storms.Active(512000, 512000, fortnight * HoursPerYear / 24, range: 30000))
            .Distinct()
            .ToArray();
}
