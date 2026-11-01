using RoughSeas.Storms;
using static RoughSeas.Tests.Storms.WaterFixtures;

namespace RoughSeas.Tests.Storms;

public class DrawnBreathTests
{
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
}
