using RoughSeas.Water;

namespace RoughSeas.Tests.Water;

public class SurfFieldTests
{
    [Fact]
    public void TheBlockBesideDeepWaterIsLandFromItsEdgeThoughItsDepthBlendsWithTheSea()
    {
        var field = new SurfField(0, 0, 2, [(0.1, 0)]);
        for (var z = 0; z < 2; z++)
        {
            field.SetDepth(0, z, 3);
            field.SetDepth(1, z, -0.125f);
        }

        Assert.True(field.DepthAt(1.2, 1) > 0);
        Assert.True(field.LandAt(1.2, 1));
        Assert.False(field.LandAt(0.9, 1));
    }

    [Theory]
    [InlineData(25.0)]
    [InlineData(-13.0)]
    [InlineData(3.0)]
    public void AWaveGivingWayToThePlaneOnesChangesPhaseOnlyAsTheTwoAdd(double lag)
    {
        Assert.Equal(0, Math.Sin(SurfField.Faded(lag, 0).Lag), 9);
        Assert.Equal(1, Math.Cos(SurfField.Faded(lag, 0).Lag), 9);
        Assert.Equal(1, SurfField.Faded(lag, 0).Share, 9);
        Assert.Equal(lag, SurfField.Faded(lag, 1).Lag, 9);
        Assert.Equal(1, SurfField.Faded(lag, 1).Share, 9);
        var (faded, share) = SurfField.Faded(lag, 0.4);
        Assert.Equal(0.4 * Math.Sin(1 + lag) + 0.6 * Math.Sin(1), share * Math.Sin(1 + faded), 9);
    }

    private const int Size = 8;
    private const float GroundHeight = 112;

    private static SurfField ShoreAlongX(float breaking)
    {
        var field = new SurfField(0, 0, Size, []);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            var water = x < Size / 2;
            field.SetDepth(x, z, water ? 2 : -0.5f);
            if (water) field.SetBreaking(x, z, breaking);
            else field.SetGroundTop(x, z, GroundHeight);
        }
        return field;
    }

    [Fact]
    public void AtTheShorelineBreakingStaysAShare()
    {
        var field = ShoreAlongX(0.5f);
        Assert.InRange(field.BreakingAt(Size / 2.0, Size / 2.0), 0, 0.5);
    }

    [Fact]
    public void OverOpenWaterBreakingIsTheCellsOwn()
    {
        var field = ShoreAlongX(0.5f);
        Assert.Equal(0.5, field.BreakingAt(1.5, Size / 2.0), 6);
    }

    [Fact]
    public void OverLandNothingBreaks()
    {
        var field = ShoreAlongX(0.5f);
        Assert.Equal(0, field.BreakingAt(Size - 1.5, Size / 2.0));
    }
}
