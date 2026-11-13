using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class SquareRigTests
{
    private const double Breeze = 9;
    private const double Yaw = 0;
    private const double Waterline = 0;
    private const int FullSail = 2;
    private static readonly (double X, double Z) Still = (0, 0);

    private static (VesselInWind Body, Sail Sail) Sailboat()
    {
        var body = Fixtures.SailboatInWind();
        body.Sail!.Hoist(FullSail);
        return (body, body.Sail);
    }

    private static (double X, double Z) FromOffHerBow(double degrees)
    {
        var a = degrees * Math.PI / 180;
        return (-Breeze * Math.Sin(a), -Breeze * Math.Cos(a));
    }

    private static double OffHerBeam(Sail sail)
    {
        var square = (SquareState)sail.State;
        var yard = square.ArmStarboard - square.ArmPort;
        return Math.Atan2(Math.Abs(yard.Along), Math.Abs(yard.Across));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(135)]
    [InlineData(180)]
    public void OffTheWindHerSquareSailDrivesHerAhead(double offBow)
    {
        var (body, _) = Sailboat();
        Assert.True(body.Load(FromOffHerBow(offBow), Still, Yaw, Waterline).Z > 0);
    }

    [Fact]
    public void RunningHerYardStandsSquareAcrossHer()
    {
        var (body, sail) = Sailboat();
        body.Load(FromOffHerBow(180), Still, Yaw, Waterline);
        Assert.True(OffHerBeam(sail) < 0.1, sail.Rig.End.Describe(sail.State));
    }

    [Fact]
    public void ReachingSheBracesHerYardRoundAsFarAsHerStaysLetHer()
    {
        var (body, sail) = Sailboat();
        body.Load(FromOffHerBow(100), Still, Yaw, Waterline);
        Assert.True(OffHerBeam(sail) > 0.1, sail.Rig.End.Describe(sail.State));
        Assert.True(sail.Rig.End.Clear(sail.State));
    }

    [Fact]
    public void HeadToWindHerSquareSailDrawsNothing()
    {
        var (body, sail) = Sailboat();
        body.Load(FromOffHerBow(10), Still, Yaw, Waterline);
        Assert.Equal(0, sail.Pressure);
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(-0.6)]
    public void BracedRoundHerMastHerYardKeepsItsHeightAndItsReach(double yard)
    {
        var end = Sailboat().Sail.Rig.End;
        var (rest, braced) = ((SquareState)end.Pose(0, 0), (SquareState)end.Pose(yard, 0));
        Assert.Equal(rest.ArmPort.Height, braced.ArmPort.Height, 9);
        Assert.Equal((rest.ArmStarboard - rest.ArmPort).Length, (braced.ArmStarboard - braced.ArmPort).Length, 9);
    }
}
