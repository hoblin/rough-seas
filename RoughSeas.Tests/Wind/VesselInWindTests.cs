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
    private const int HalfSail = 1;
    private const int Furled = 0;
    private const int AsBuilt = 0;
    private const int Shunted = 1;
    private static readonly (double X, double Z) Still = (0, 0);
    private static readonly (double X, double Z) OverHerFloat = (-Breeze, 0);
    private static readonly (double X, double Z) OverHerLee = (Breeze, 0);
    private const double TenDegrees = Math.PI / 18;
    private static readonly (double X, double Z) FromAhead = (-Breeze * Math.Sin(TenDegrees), -Breeze * Math.Cos(TenDegrees));
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
    public void OneSailKeptThroughShiftsOfTheWindAndAShuntDrawsAsAFreshOneWould()
    {
        var (kept, sail) = Outrigger(FullSail);
        foreach (var (wind, tacked) in new[] { (OverHerFloat, AsBuilt), (FromAstern, AsBuilt), (OverHerLee, AsBuilt), (OverHerFloat, Shunted), (FromAstern, Shunted), (OverHerFloat, AsBuilt) })
        {
            sail.Rig.Tacked = tacked;
            kept.Load(wind, Still, Yaw, Waterline);
            var load = kept.Load(wind, Still, Yaw, Waterline);
            var (fresh, freshSail) = Outrigger(FullSail, tacked);
            fresh.Load(wind, Still, Yaw, Waterline);
            var expected = fresh.Load(wind, Still, Yaw, Waterline);
            Assert.Equal(freshSail.Aback, sail.Aback);
            if (sail.Aback) continue;
            Assert.True(Math.Abs(expected.X - load.X) < 0.5 && Math.Abs(expected.Z - load.Z) < 0.5,
                $"{wind} tack {tacked}: kept {load.X:F1} {load.Z:F1}, fresh {expected.X:F1} {expected.Z:F1}; kept {sail.Rig.End.Describe(sail.State)}; fresh {freshSail.Rig.End.Describe(freshSail.State)}");
        }
    }

    [Fact]
    public void HalfSetHerSailDrivesHerLessThanFullButStillAhead()
    {
        var half = Outrigger(HalfSail).Body.Load(OverHerFloat, Still, Yaw, Waterline).Z;
        var full = Outrigger(FullSail).Body.Load(OverHerFloat, Still, Yaw, Waterline).Z;
        Assert.InRange(half, 0, full);
        Assert.True(half < full, $"half {half:F0} N, full {full:F0} N");
    }

    [Fact]
    public void DrawingHerSailStandsOnTheSideAwayFromHerFloat()
    {
        var (body, sail) = Outrigger(FullSail);
        body.Load(OverHerFloat, Still, Yaw, Waterline);
        Assert.True(((ClawState)sail.State).Clew.Across < 0);
    }

    [Fact]
    public void WithTheWindOverHerLeeHerSailIsTakenAbackAndDrivesHerNoWayAhead()
    {
        var (body, sail) = Outrigger(FullSail);
        var load = body.Load(OverHerLee, Still, Yaw, Waterline);
        Assert.True(sail.Aback, sail.Rig.End.Describe(sail.State));
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
        Assert.True(((ClawState)sail.State).Clew.Across < 0);
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
        Assert.True(sail.State.Centre(sail.Set, sail.Fill).Along < 0);
        Assert.True(load.NewtonMetres < 0);
    }

    [Fact]
    public void RunningHerSailStandsAcrossHer()
    {
        var (body, sail) = Outrigger(FullSail);
        body.Load(FromAstern, Still, Yaw, Waterline);
        var normal = sail.State.Form.Normal;
        var offAhead = Math.Atan2(Math.Abs(normal.Across), Math.Abs(normal.Along));
        Assert.True(offAhead < Math.PI / 4, $"{offAhead * 180 / Math.PI:F0}° off ahead: {sail.Rig.End.Describe(sail.State)}");
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
