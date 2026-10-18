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
        Assert.Equal(height, Sea.Grow(windMps, fetchMetres, depthMetres).Height, tolerance: 0.05);
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
    public void RaisesAYoungShortSeaUnderAWindThatJustArrived()
    {
        var young = Sea.Grow(20, 1000000, Deep, blowingHours: 0.5);
        var old = Sea.Grow(20, 1000000, Deep, blowingHours: 16);

        Assert.True(young.Height < 0.3 * old.Height, $"{young.Height:F1} m young, {old.Height:F1} m old");
        Assert.True(young.Period < 0.5 * old.Period, $"{young.Period:F1} s young, {old.Period:F1} s old");
    }

    [Fact]
    public void LetsTheShoreLimitTheSeaBeforeTimeDoes()
    {
        Assert.Equal(Sea.Grow(20, 5000, Deep), Sea.Grow(20, 5000, Deep, blowingHours: 16));
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
        Assert.True(hurricane > Waves.KneeMetres && hurricane < Waves.CeilingMetres, $"{hurricane} m");
    }

    [Fact]
    public void BendsSeasJustAboveTheKneeBelowWhatTheyWouldBe()
    {
        var unbent = Unbent.Grow(22, 2000000, Deep).Height;
        var bent = Sea.Grow(22, 2000000, Deep).Height;

        Assert.True(unbent > Waves.KneeMetres && unbent < Waves.CeilingMetres, $"{unbent} m unbent");
        Assert.True(bent > Waves.KneeMetres && bent < unbent, $"{bent} m bent from {unbent} m");
    }

    [Fact]
    public void StopsThePeriodAtTheDepthsLimit()
    {
        Assert.Equal(9.78 * Math.Sqrt(1 / 9.81), Sea.Grow(50, 1000000, 1).Period, 6);
    }

    [Fact]
    public void RaisesNoSeaWithoutWindFetchOrWater()
    {
        Assert.Equal((0, 0), Sea.Grow(0, 50000, Deep));
        Assert.Equal((0, 0), Sea.Grow(20, 0, Deep));
        Assert.Equal((0, 0), Sea.Grow(20, 50000, 0));
    }
}
