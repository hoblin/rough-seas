using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class DrawnWaterTests
{
    private const double X = 512031.25;
    private const double Z = 512300.5;
    private const int SeaFloor = 40;
    private const int WaterTop = 62;
    private const double StillLevel = 7 / 8.0;
    private const double StillSurface = WaterTop + StillLevel;

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

    [Fact]
    public void GivesABodyTheStillWaterWhereNoSwellIsDrawn()
    {
        var water = new DrawnWater(new Sea(0, swell: false)).WaterAt(new Column(), X, StillSurface - 0.5, Z);

        Assert.Equal(new WaterSurface(StillSurface, 0, Salt: true), water);
    }

    [Fact]
    public void GivesABodyTheDrawnSurfaceAndItsSwell()
    {
        var water = new DrawnWater(new Sea(1.5)).WaterAt(new Column(salt: false), X, StillSurface + 1, Z);

        Assert.Equal(new WaterSurface(StillSurface + 1.5, 1.5, Salt: false), water);
    }

    [Fact]
    public void KeepsABodyOverADeckOutOfTheWater()
    {
        Assert.Null(new DrawnWater(new Sea(0, swell: false)).WaterAt(new Column(solid: [WaterTop + 1]), X, WaterTop + 2.2, Z));
    }

    private sealed class Sea(double height, bool swell = true, bool local = true) : IDrawnSea
    {
        public bool Swell => swell;

        public bool SwellAt(double x, double z) => swell && local;

        public double Seconds => 0;

        public double HeightAt(double x, double z) => swell ? height : 0;

        public (double X, double Z) OrbitalVelocityAt(double x, double z, double depth = 0, double side = 0) => default;

        public (double X, double Z) HullVelocityAt(double x, double z, double lift, double draft, double yaw, double length, double beam) => default;

        public SeaPose PoseOf(double x, double z, double yaw, double length, double width) => default;
    }

    private sealed class Column(int top = WaterTop, int[]? solid = null, double level = StillLevel, bool salt = true) : IWaterColumn
    {
        public bool Salt(int y) => salt;

        public bool Water(int y) => y > SeaFloor && y <= top;

        public double Level(int y) => y == top ? level : 1;

        public bool Solid(int y) => y <= SeaFloor || (solid?.Contains(y) ?? false);
    }
}
