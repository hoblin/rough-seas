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
    private const int AsBuilt = 0;
    private const int Shunted = 1;
    private static readonly (double X, double Z) Still = (0, 0);
    private static readonly (double X, double Z) OverHerFloat = (-Breeze, 0);
    private static readonly (double X, double Z) OverHerLee = (Breeze, 0);
    private static readonly (double X, double Z) FromAhead = (-Breeze * Math.Sin(0.17), -Breeze * Math.Cos(0.17));
    private static readonly (double X, double Z) FromAstern = (0, Breeze);

    private static (VesselInWind Body, Sail Sail) Outrigger(int sailPosition, int tacked = AsBuilt)
    {
        var body = Fixtures.InWind("outrigger");
        body.Sail!.Hoist(sailPosition);
        body.Sail.Rig.Tacked = tacked;
        return (body, body.Sail);
    }

    [Fact]
    public void WithTheWindOverHerFloatHerCrabClawDrivesHerAheadAndPressesHerToLeeward()
    {
        var (body, sail) = Outrigger(FullSail);
        var load = body.Load(OverHerFloat, Still, Yaw, Waterline);
        Assert.True(load.Z > 0, $"ahead {load.Z:F0} N");
        Assert.True(load.X < 0, $"to leeward {load.X:F0} N");
        Assert.False(sail.Aback);
    }

    [Fact]
    public void DrawingHerSailStandsOnTheSideAwayFromHerFloat()
    {
        var (body, sail) = Outrigger(FullSail);
        body.Load(OverHerFloat, Still, Yaw, Waterline);
        var end = sail.Rig.End;
        Assert.True(end.Turned(end.Hoisted.Clew, sail.Turn).Across < 0);
    }

    [Fact]
    public void WithTheWindOverHerLeeHerSailIsTakenAbackAndDrivesHerNoWayAhead()
    {
        var (body, sail) = Outrigger(FullSail);
        var load = body.Load(OverHerLee, Still, Yaw, Waterline);
        Assert.True(sail.Aback);
        Assert.True(load.Z <= 0, $"ahead {load.Z:F0} N");
        Assert.True(load.X > 0);
    }

    [Fact]
    public void ShuntedSheSailsTheOtherWayWithHerFloatStillToWindward()
    {
        var (body, sail) = Outrigger(FullSail, Shunted);
        var load = body.Load(OverHerFloat, Still, Yaw, Waterline);
        Assert.True(load.Z < 0, $"toward her model's stern {load.Z:F0} N");
        Assert.False(sail.Aback);
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
        var load = body.Load(OverHerFloat, Still, Yaw, Waterline);
        Assert.True(load.X < 0);
        Assert.Equal(0, load.Z, 3);
    }

    [Fact]
    public void HerSailSetFromHerBowTurnsHerBowAwayFromTheWind()
    {
        var (body, sail) = Outrigger(FullSail);
        var load = body.Load(OverHerFloat, Still, Yaw, Waterline);
        Assert.True(sail.Form.Centre(sail.Set, sail.Turn, sail.Depth).Along < 0);
        Assert.True(load.NewtonMetres < 0);
    }

    [Fact]
    public void RunningHerSailStandsAcrossHerAsTheTunnelsSailDrewAtItsBest()
    {
        var (body, sail) = Outrigger(FullSail);
        body.Load(FromAstern, Still, Yaw, Waterline);
        var normal = sail.Form.Normal(sail.Turn);
        var offAhead = Math.Atan2(Math.Abs(normal.Across), Math.Abs(normal.Along));
        Assert.Equal(Math.Atan2(0.44, 0.95), offAhead, 1);
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
            var load = body.Load(OverHerFloat, set.Velocity, Yaw, Waterline);
            set.Settle(drag, new WaterOnHull(0, 0), Yaw, step, drag.Acceleration(load.X, load.Z, Yaw));
        }
        Assert.InRange(set.Velocity.Z / Breeze, 0.25, 0.4);
    }
}
