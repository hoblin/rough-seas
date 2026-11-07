using RoughSeas.Storms;
using static RoughSeas.Tests.Storms.WaterFixtures;

namespace RoughSeas.Tests.Storms;

public class DrawnWaterTests
{
    [Fact]
    public void LeavesThePointToTheBlocksWhileNoSwellIsDrawn()
    {
        Assert.Null(new DrawnWater(new Sea(0, swell: false)).SurfaceAt(new Column(), X, StillSurface - 0.5, Z));
    }

    [Fact]
    public void LeavesThePointToTheBlocksWhereNoSwellIsDrawnThere()
    {
        Assert.Null(new DrawnWater(new Sea(0, local: false)).SurfaceAt(new Column(), X, StillSurface + 0.135, Z));
    }

    [Fact]
    public void RaisesACrestOverAnEyeAboveTheBlockWater()
    {
        var surface = new DrawnWater(new Sea(3)).SurfaceAt(new Column(), X, StillSurface + 1.7, Z);

        Assert.Equal(StillSurface + 3, surface);
    }

    [Fact]
    public void BaresAnEyeInTheBlockWaterUnderATrough()
    {
        var surface = new DrawnWater(new Sea(-2)).SurfaceAt(new Column(), X, StillSurface - 0.5, Z);

        Assert.Equal(StillSurface - 2, surface);
        Assert.True(surface < StillSurface - 0.5);
    }

    [Fact]
    public void MeasuresADiverFromTheTopOfTheWater()
    {
        Assert.Equal(StillSurface + 1.5, new DrawnWater(new Sea(1.5)).SurfaceAt(new Column(), X, SeaFloor + 1.5, Z));
    }

    [Fact]
    public void FindsTheWaterAsFarDownAsTheCrestReaches()
    {
        var sea = new DrawnWater(new Sea(3));

        Assert.Equal(StillSurface + 3, sea.SurfaceAt(new Column(), X, StillSurface + 3.9, Z));
        Assert.Null(sea.SurfaceAt(new Column(), X, WaterTop + 2 + 3 + 0.1, Z));
    }

    [Fact]
    public void StandsTheSurfaceAtTheLevelTheWaterIsDrawnAt()
    {
        var shallows = new Column(level: 0.5);

        Assert.Equal(WaterTop + 0.5 + 1, new DrawnWater(new Sea(1)).SurfaceAt(shallows, X, WaterTop + 0.2, Z));
    }

    [Fact]
    public void BaresAnEyeInTheTopWaterBlockAboveItsDrawnLevel()
    {
        var surface = new DrawnWater(new Sea(0)).SurfaceAt(new Column(), X, WaterTop + 0.95, Z);

        Assert.Equal(StillSurface, surface);
        Assert.True(surface < WaterTop + 0.95);
    }

    [Fact]
    public void GivesTheSurfaceBetweenCrestsToAPointJustAboveTheWater()
    {
        var surface = new DrawnWater(new Sea(-1)).SurfaceAt(new Column(), X, StillSurface + 0.3, Z);

        Assert.Equal(StillSurface - 1, surface);
    }

    [Fact]
    public void KeepsAPointOverADeckDry()
    {
        var deck = new Column(solid: [WaterTop + 1]);

        Assert.Null(new DrawnWater(new Sea(3)).SurfaceAt(deck, X, StillSurface + 1.7, Z));
    }

    [Fact]
    public void KeepsAPointOverAWaterloggedDeckDry()
    {
        var deck = new Column(top: WaterTop + 1, solid: [WaterTop + 1]);

        Assert.Null(new DrawnWater(new Sea(3)).SurfaceAt(deck, X, WaterTop + 2.7, Z));
    }

    [Fact]
    public void KeepsDryGroundDry()
    {
        var ground = new Column(top: SeaFloor - 1, solid: Enumerable.Range(0, WaterTop + 1).ToArray());

        Assert.Null(new DrawnWater(new Sea(3)).SurfaceAt(ground, X, StillSurface + 1.7, Z));
    }
}
