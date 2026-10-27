using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class DrawnBreathTests
{
    private const double X = 512031.25;
    private const double Z = 512300.5;
    private const int SeaFloor = 40;
    private const int WaterTop = 62;
    private const double StillLevel = 7 / 8.0;
    private const double StillSurface = WaterTop + StillLevel;

    private static BreathVerdict Judge(double eyeY, double swell, Column? column = null, bool drawn = true) =>
        DrawnBreath.Judge(new DrawnWater(new Sea(swell, drawn)), column ?? new Column(), X, eyeY, Z);

    [Fact]
    public void TakesTheAirOfAnEyeACrestCovers()
    {
        var verdict = Judge(StillSurface + 0.6, 1.2);

        Assert.Equal(BreathVerdict.Under, verdict);
        Assert.False(DrawnBreath.HasAir(verdict));
    }

    [Fact]
    public void GivesAirToAnEyeInTheBlockWaterThatATroughBares()
    {
        var verdict = Judge(StillSurface - 0.3, -0.8);

        Assert.Equal(BreathVerdict.Bared, verdict);
        Assert.True(DrawnBreath.HasAir(verdict));
    }

    [Fact]
    public void KeepsADiverUnderWhateverTheSwell()
    {
        Assert.Equal(BreathVerdict.Under, Judge(SeaFloor + 1.5, -2.5));
        Assert.Equal(BreathVerdict.Under, Judge(SeaFloor + 1.5, 2.5));
    }

    [Fact]
    public void LeavesAnEyeAboveTheDrawnSurfaceToTheBlocks()
    {
        var verdict = Judge(StillSurface + 0.6, 0.4);

        Assert.Equal(BreathVerdict.Above, verdict);
        Assert.Null(DrawnBreath.HasAir(verdict));
    }

    [Fact]
    public void LeavesAnEyeToTheBlocksWhereNoSwellIsDrawn()
    {
        Assert.Equal(BreathVerdict.NoSurface, Judge(StillSurface - 0.3, -0.8, drawn: false));
    }

    [Fact]
    public void LeavesAnEyeOverADeckToTheBlocks()
    {
        var deck = new Column(solid: [WaterTop + 1]);

        Assert.Equal(BreathVerdict.NoSurface, Judge(StillSurface + 1.6, 1.8, deck));
    }

    [Fact]
    public void LeavesAnEyeInAWaterloggedSolidBlockToTheBlocks()
    {
        var waterlogged = new Column(solid: [WaterTop]);

        Assert.Equal(BreathVerdict.Above, Judge(StillSurface - 0.3, -0.8, waterlogged));
    }

    [Theory]
    [InlineData(BreathVerdict.Dry)]
    [InlineData(BreathVerdict.Aboard)]
    [InlineData(BreathVerdict.NoSurface)]
    public void LeavesVanillasCheckStanding(BreathVerdict verdict)
    {
        Assert.Null(DrawnBreath.HasAir(verdict));
    }

    private sealed class Sea(double height, bool drawn) : IDrawnSea
    {
        public bool Swell => drawn;

        public bool SwellAt(double x, double z) => drawn;

        public double Seconds => 0;

        public double HeightAt(double x, double z) => drawn ? height : 0;

        public SeaPose PoseOf(double x, double z, double yaw, double length, double width) => default;
    }

    private sealed class Column(int top = WaterTop, int[]? solid = null) : IWaterColumn
    {
        public bool Water(int y) => y > SeaFloor && y <= top;

        public double Level(int y) => y == top ? StillLevel : 1;

        public bool Solid(int y) => y <= SeaFloor || (solid?.Contains(y) ?? false);
    }
}
