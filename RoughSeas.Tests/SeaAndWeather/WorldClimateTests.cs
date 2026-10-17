using RoughSeas.SeaAndWeather;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.SeaAndWeather;

public class WorldClimateTests
{
    [Fact]
    public void ChangesRainfallSmoothlyAcrossClimateCells()
    {
        var climate = new WorldClimate(1234, new TreeAttribute(), Fixtures.MapSize);
        var steps = Enumerable.Range(0, 4096).Select(i => climate.RainfallAt(500000 + i, 500000)).ToArray();

        Assert.All(steps.Zip(steps.Skip(1)), pair => Assert.True(Math.Abs(pair.First - pair.Second) < 0.01));
    }
}
