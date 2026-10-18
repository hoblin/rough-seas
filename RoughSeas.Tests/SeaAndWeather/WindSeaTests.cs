using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class WindSeaTests
{
    private const double Deep = 1000;

    private static readonly WavesConfig Waves = Fixtures.Config().Waves;
    private static readonly WindSea Sea = new(Waves);
    private static readonly WindSea Unbent = new(new WavesConfig { KneeMetres = 1000, CeilingMetres = 2000 });

    [Theory]
    [InlineData(20, 5000, 3, 0.62)]
    [InlineData(20, 5000, Deep, 0.71)]
    [InlineData(20, 200000, Deep, 4.2)]
    public void GrowsTheHeightsOfYoungAndVerhagensCurves(double windMps, double fetchMetres, double depthMetres, double height)
    {
        Assert.Equal(height, Sea.Grow(windMps, fetchMetres, depthMetres).Height, 1);
    }

    [Fact]
    public void GrowsHigherWithMoreFetch()
    {
        Assert.True(Sea.Grow(15, 50000, Deep).Height > Sea.Grow(15, 5000, Deep).Height);
    }

    [Fact]
    public void GrowsHigherWithMoreWind()
    {
        Assert.True(Sea.Grow(20, 50000, Deep).Height > Sea.Grow(10, 50000, Deep).Height);
    }

    [Fact]
    public void LetsShallowWaterHoldTheWavesDown()
    {
        Assert.True(Sea.Grow(20, 500000, 2).Height < 0.5 * Sea.Grow(20, 500000, Deep).Height);
    }

    [Fact]
    public void LeavesSeasBelowTheKneeAsTheCurvesGrowThem()
    {
        var below = Sea.Grow(15, 100000, Deep).Height;

        Assert.True(below < Waves.KneeMetres, $"{below} m");
        Assert.Equal(Unbent.Grow(15, 100000, Deep).Height, below);
    }

    [Fact]
    public void BendsTheHighestSeasBelowTheCeiling()
    {
        var unbent = Unbent.Grow(55, 5000000, Deep).Height;
        var hurricane = Sea.Grow(55, 5000000, Deep).Height;

        Assert.True(unbent > Waves.CeilingMetres, $"{unbent} m unbent");
        Assert.InRange(hurricane, Waves.KneeMetres, Waves.CeilingMetres);
    }

    [Fact]
    public void ShortensThePeriodInShallowWater()
    {
        Assert.True(Sea.Grow(20, 50000, 2).Period <= 9.78 * Math.Sqrt(2 / 9.81) + 1e-9);
    }

    [Fact]
    public void RaisesNoSeaWithoutWindFetchOrWater()
    {
        Assert.Equal((0, 0), Sea.Grow(0, 50000, Deep));
        Assert.Equal((0, 0), Sea.Grow(20, 0, Deep));
        Assert.Equal((0, 0), Sea.Grow(20, 50000, 0));
    }
}
