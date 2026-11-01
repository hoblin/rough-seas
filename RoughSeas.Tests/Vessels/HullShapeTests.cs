using RoughSeas.Vessels;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Vessels;

public class HullShapeTests
{
    private readonly HullShape dugout = HullShape.FromBox(-1.5, 0, 0.125, 2.5, 0.5625, 0.875);

    [Theory]
    [InlineData(-1, -0.01, HullPlace.PortBow)]
    [InlineData(-0.9, 0.45, HullPlace.StarboardBow)]
    [InlineData(1, -0.01, HullPlace.PortQuarter)]
    [InlineData(1, 0.2, HullPlace.StarboardQuarter)]
    [InlineData(0, -1, HullPlace.PortSide)]
    [InlineData(0.1, 1, HullPlace.StarboardSide)]
    public void AKnockLandsWhereTheHullMeetsWhatItHit(double along, double across, HullPlace place)
    {
        Assert.Equal(place, dugout.Toward(along, across));
    }

    [Fact]
    public void AHitLowInTheMiddleIsOnTheBottom()
    {
        Assert.Equal(HullPlace.Bottom, dugout.At(0, 0.05, 0));
        Assert.Equal(HullPlace.PortSide, dugout.At(0, 0.4, -0.3));
        Assert.Equal(HullPlace.StarboardQuarter, dugout.At(1.9, 0.4, 0.3));
    }

    [Fact]
    public void APointPastHerSideIsOutsideHer()
    {
        Assert.False(dugout.Beyond(0.3));
        Assert.True(dugout.Beyond(2));
        Assert.True(dugout.Beyond(-2));
    }

    [Fact]
    public void EveryPlaceItPointsAtIsThatPlace()
    {
        Assert.All(HullShape.Places, place =>
        {
            var (along, height, across) = dugout.PointOf(place);
            Assert.Equal(place, dugout.At(along, height, across));
        });
    }

    [Fact]
    public void TheBowFacesTheWayVanillaPaddlesAVessel()
    {
        float yaw = 0.7f;
        var (along, across) = HullShape.ToHull(Math.Sin(yaw), Math.Cos(yaw), yaw);

        Assert.Contains(dugout.Toward(along, across), new[] { HullPlace.PortBow, HullPlace.StarboardBow });
    }

    [Fact]
    public void TurnsBetweenTheWorldAndTheHull()
    {
        var (along, across) = HullShape.ToHull(0.3, -1.2, 2.1);
        var (x, z) = HullShape.ToWorld(along, across, 2.1);

        Assert.Equal(0.3, x, 6);
        Assert.Equal(-1.2, z, 6);
    }
}
