using RoughSeas.SeaAndWeather;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.SeaAndWeather;

public class ShowersTests
{
    private const long Seed = 1234;
    private const double Raining = 0.5;

    private readonly WorldClimate climate = new(Seed, new TreeAttribute(), Fixtures.MapSize);

    private Showers Showers() => new(Seed, Fixtures.Config().Showers, climate);

    private static double RainyShare(Showers showers, double x, double z) =>
        Enumerable.Range(0, 500).Count(hour => showers.CloudnessAt(x, z, hour * 1.7) > Raining) / 500.0;

    [Fact]
    public void RainsMoreOftenWhereTheClimateIsWetter()
    {
        var showers = Showers();
        var places = Enumerable.Range(0, 40)
            .Select(i => (x: 400000.0 + i * 5000, z: 500000.0 + i * 3100))
            .Select(place => (rainfall: climate.RainfallAt(place.x, place.z), share: RainyShare(showers, place.x, place.z)))
            .OrderBy(place => place.rainfall)
            .ToArray();
        var third = places.Length / 3;

        Assert.True(places[^1].rainfall - places[0].rainfall > 0.3, "the sampled places span too narrow a climate");
        Assert.True(places.TakeLast(third).Average(place => place.share) > places.Take(third).Average(place => place.share));
    }

    [Fact]
    public void RainsNowAndThenInATemperateClimate()
    {
        var showers = Showers();
        var temperate = Enumerable.Range(0, 200)
            .Select(i => (x: 400000.0 + i * 2900, z: 500000.0 + i * 1300))
            .First(place => climate.RainfallAt(place.x, place.z) is > 0.45 and < 0.6);

        Assert.InRange(RainyShare(showers, temperate.x, temperate.z), 0.01, 0.3);
    }
}
