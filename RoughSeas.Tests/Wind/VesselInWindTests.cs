using RoughSeas.Storms;
using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class VesselInWindTests
{
    private const double Breeze = 9;
    private const double Yaw = 0;
    private const double Waterline = 0;
    private const int FullSail = 2;
    private const int Furled = 0;
    private static readonly (double X, double Z) Still = (0, 0);
    private static readonly (double X, double Z) FromPort = (Breeze, 0);
    private static readonly (double X, double Z) FromAhead = (0, -Breeze);
    private static readonly (double X, double Z) FromAstern = (0, Breeze);

    private static (VesselInWind Body, Sail Sail) Outrigger(int sailPosition)
    {
        var body = Fixtures.InWind("outrigger");
        body.Sail!.Hoist(sailPosition);
        return (body, body.Sail);
    }

    [Fact]
    public void AbeamHerCrabClawDrivesHerAheadAndPressesHerToLeeward()
    {
        var (body, _) = Outrigger(FullSail);
        var load = body.Load(FromPort, Still, Yaw, Waterline);
        Assert.True(load.Z > 0, $"ahead {load.Z:F0} N");
        Assert.True(load.X > 0, $"to leeward {load.X:F0} N");
    }

    [Fact]
    public void HeadToWindHerSailLuffsAndHerWindagePushesHerAstern()
    {
        var (body, sail) = Outrigger(FullSail);
        var load = body.Load(FromAhead, Still, Yaw, Waterline);
        Assert.True(load.Z < 0, $"astern {load.Z:F0} N");
        Assert.Equal(0, sail.Pressure);
    }

    [Fact]
    public void FurledSheIsPushedOnlyDownwind()
    {
        var (body, _) = Outrigger(Furled);
        var load = body.Load(FromPort, Still, Yaw, Waterline);
        Assert.True(load.X > 0);
        Assert.Equal(0, load.Z, 3);
    }

    [Fact]
    public void AnEasedSailTurnsHerBowUpIntoTheWind()
    {
        var (body, _) = Outrigger(FullSail);
        Assert.True(body.Load(FromPort, Still, Yaw, Waterline).NewtonMetres < 0);
    }

    [Fact]
    public void RunningHerSailStandsSquareAcrossHer()
    {
        var (body, sail) = Outrigger(FullSail);
        body.Load(FromAstern, Still, Yaw, Waterline);
        Assert.Equal(Math.PI / 2, Math.Abs(sail.Turn), 6);
    }

    [Fact]
    public void OnABeamReachSheMakesAboutAThirdOfTheWindsSpeed()
    {
        var (body, _) = Outrigger(FullSail);
        var drag = Fixtures.DragOf("outrigger", Fixtures.Hull("outrigger").Crewed());
        var set = new HullSet();
        const double step = 1 / 30.0;
        for (var t = 0.0; t < 300; t += step)
        {
            var load = body.Load(FromPort, set.Velocity, Yaw, Waterline);
            set.Settle(drag, new WaterOnHull(0, 0), Yaw, step, drag.Acceleration(load.X, load.Z, Yaw));
        }
        Assert.InRange(set.Velocity.Z / Breeze, 0.25, 0.4);
    }
}
