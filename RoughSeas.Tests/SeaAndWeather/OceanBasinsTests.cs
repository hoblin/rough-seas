using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class OceanBasinsTests
{
    private static readonly OceanBasins Basins = Fixtures.Basins(seed: 42);

    private static readonly (double X, double Z)[] Samples =
        Enumerable.Range(0, 200).Select(i => (500000.0 + i * 1777 % 30000, 512000.0 - i * 913 % 30000)).ToArray();

    [Fact]
    public void MapsTheSameBasinsFromTheSameSeed()
    {
        var other = Fixtures.Basins(seed: 42);

        Assert.All(Samples, sample => Assert.Equal(Basins.At(sample.X, sample.Z), other.At(sample.X, sample.Z)));
    }

    [Fact]
    public void FindsBothSeaAndLand()
    {
        Assert.Contains(Samples, sample => Basins.At(sample.X, sample.Z).IsSea);
        Assert.Contains(Samples, sample => !Basins.At(sample.X, sample.Z).IsSea);
    }

    [Fact]
    public void MeasuresTheSameShoresFromNeighbouringWaters()
    {
        var (x, z) = Samples.First(sample => Basins.At(sample.X, sample.Z) is { IsSea: true, WestBlocks: > 64, EastBlocks: > 64 });
        var here = Basins.At(x, z);
        var east = Basins.At(x + 32, z);

        Assert.Equal(here.WestBlocks + 32, east.WestBlocks);
        Assert.Equal(here.EastBlocks - 32, east.EastBlocks);
    }

    [Fact]
    public void SeesNoSeaWhereTheWorldIsAllLand()
    {
        var land = Fixtures.Basins(seed: 42, landcover: "1");

        Assert.DoesNotContain(Samples, sample => land.At(sample.X, sample.Z).IsSea);
    }
}
