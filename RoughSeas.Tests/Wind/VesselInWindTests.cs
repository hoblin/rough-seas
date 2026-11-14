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
        body.Sail!.Rig.Tacked = tacked;
        body.Sail.Hoist(sailPosition, tacked);
        return (body, body.Sail);
    }

    [Fact]
    public void WithTheWindOverHerFloatHerCrabClawDrivesHerAheadAndPressesHerToLeeward()
    {
        var (body, sail) = Outrigger(FullSail);
        var load = body.Steady(OverHerFloat, Still, Yaw, Waterline);
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
            sail.Hoist(FullSail, tacked);
            kept.Steady(wind, Still, Yaw, Waterline);
            var load = kept.Steady(wind, Still, Yaw, Waterline);
            var (fresh, freshSail) = Outrigger(FullSail, tacked);
            fresh.Steady(wind, Still, Yaw, Waterline);
            var expected = fresh.Steady(wind, Still, Yaw, Waterline);
            Assert.Equal(freshSail.Aback, sail.Aback);
            if (sail.Aback) continue;
            Assert.True(Math.Abs(expected.X - load.X) < 0.5 && Math.Abs(expected.Z - load.Z) < 0.5,
                $"{wind} tack {tacked}: kept {load.X:F1} {load.Z:F1}, fresh {expected.X:F1} {expected.Z:F1}; kept {sail.Rig.End.Describe(sail.State)}; fresh {freshSail.Rig.End.Describe(freshSail.State)}");
        }
    }

    [Fact]
    public void HoistedShiftedShuntedAndLoweredNoPointOfHerRigJumpsBetweenTicks()
    {
        var (body, sail) = Outrigger(Furled);
        var fastest = 0.0;
        HullPoint[] Drawn() =>
        [
            ..sail.Rig.End.Marks(sail.State),
            ..new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0) }.Select(corner => sail.State.Form.At(corner.Item1, corner.Item2, 0, sail.Shown))
        ];
        void Sail((double X, double Z) wind, int position, int tacked, double seconds)
        {
            sail.Hoist(position, tacked);
            for (var t = 0.0; t < seconds; t += Fixtures.TickSeconds)
            {
                var was = Drawn();
                body.Load(wind, Still, Yaw, Waterline, Fixtures.TickSeconds);
                fastest = Math.Max(fastest, Drawn().Zip(was).Max(pair => (pair.First - pair.Second).Length) / Fixtures.TickSeconds);
            }
        }

        Sail(OverHerFloat, FullSail, AsBuilt, 20);
        Sail(FromAstern, FullSail, AsBuilt, 20);
        Sail(OverHerFloat, FullSail, Shunted, 30);
        Sail(OverHerFloat, HalfSail, Shunted, 20);
        Sail(OverHerFloat, Furled, AsBuilt, 20);
        Sail(OverHerFloat, FullSail, Shunted, 20);
        Sail(OverHerLee, FullSail, Shunted, 20);
        Sail(OverHerFloat, Furled, Shunted, 20);
        Sail(OverHerFloat, HalfSail, Shunted, 0.3);
        Sail(OverHerFloat, FullSail, Shunted, 20);
        Sail(OverHerFloat, Furled, Shunted, 20);
        Sail(OverHerFloat, HalfSail, Shunted, 20);
        Sail(OverHerFloat, HalfSail, AsBuilt, 30);
        Sail(OverHerFloat, Furled, AsBuilt, 20);
        Assert.True(fastest < Fixtures.Wind.JumpMetresPerSecond, $"a point moved at {fastest:F1} m/s");
    }

    [Fact]
    public void ThroughAShuntNoLineOfHerRigIsStretchedPastItsLength()
    {
        var (body, sail) = Outrigger(Furled);
        var stretched = 0.0;
        foreach (var (position, tacked) in new[] { (FullSail, AsBuilt), (FullSail, Shunted), (Furled, Shunted), (FullSail, AsBuilt) })
        {
            sail.Hoist(position, tacked);
            for (var t = 0.0; t < 20; t += Fixtures.TickSeconds)
            {
                body.Load(OverHerFloat, Still, Yaw, Waterline, Fixtures.TickSeconds);
                var claw = (ClawState)sail.State;
                var (pose, lines) = (claw.Rig, claw.Lines);
                stretched = new[] { (pose.Masthead - claw.Bend).Length - lines.Halyard, (pose.Lead - claw.Bridle).Length - lines.Sheet }
                    .Concat(lines.Stays.Select(stay => (pose.Masthead - stay.Pin).Length - stay.Metres)).Append(stretched).Max();
            }
        }
        Assert.True(stretched < 0.01, $"a line stretched {stretched:F2} m past its length");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(3)]
    [InlineData(25)]
    public void ARigShownWhatHerHelmComputesStandsExactlyAsHers(double secondsIntoAShunt)
    {
        var (helm, sail) = Outrigger(FullSail);
        helm.Steady(OverHerFloat, Still, Yaw, Waterline);
        sail.Hoist(FullSail, Shunted);
        helm.Steady(OverHerFloat, Still, Yaw, Waterline, secondsIntoAShunt);
        var (_, shown) = Outrigger(Furled);
        shown.Show(sail.Shape(1));
        foreach (var (theirs, ours) in sail.Rig.End.Marks(sail.State).Zip(shown.Rig.End.Marks(shown.State)))
            Assert.Equal(0, (theirs - ours).Length, 6);
    }

    [Fact]
    public void HalfSetHerSailDrivesHerLessThanFullButStillAhead()
    {
        var half = Outrigger(HalfSail).Body.Steady(OverHerFloat, Still, Yaw, Waterline).Z;
        var full = Outrigger(FullSail).Body.Steady(OverHerFloat, Still, Yaw, Waterline).Z;
        Assert.InRange(half, 0, full);
        Assert.True(half < full, $"half {half:F0} N, full {full:F0} N");
    }

    [Fact]
    public void DrawingHerSailStandsOnTheSideAwayFromHerFloat()
    {
        var (body, sail) = Outrigger(FullSail);
        body.Steady(OverHerFloat, Still, Yaw, Waterline);
        Assert.True(((ClawState)sail.State).Clew.Across < 0);
    }

    [Fact]
    public void WithTheWindOverHerLeeHerSailIsTakenAbackAndDrivesHerNoWayAhead()
    {
        var (body, sail) = Outrigger(FullSail);
        var load = body.Steady(OverHerLee, Still, Yaw, Waterline);
        Assert.True(sail.Aback, sail.Rig.End.Describe(sail.State));
        Assert.True(load.Z <= 0, $"ahead {load.Z:F0} N");
        Assert.True(load.X > 0);
    }

    [Fact]
    public void ShuntedSheSailsTheOtherWayWithHerFloatStillToWindward()
    {
        var (body, sail) = Outrigger(FullSail, Shunted);
        var load = body.Steady(OverHerFloat, Still, Yaw, Waterline);
        Assert.True(load.Z < 0, $"toward her model's stern {load.Z:F0} N");
        Assert.False(sail.Aback);
        Assert.True(((ClawState)sail.State).Clew.Across < 0);
    }

    [Fact]
    public void HeadToWindHerSailDrivesNothingAndHerWindagePushesHerAstern()
    {
        var (body, sail) = Outrigger(FullSail);
        var load = body.Steady(FromAhead, Still, Yaw, Waterline);
        Assert.True(load.Z < 0, $"astern {load.Z:F0} N");
        var drive = sail.Rig.End.Ahead * sail.Load!.Points.Sum(push => push.Force.Along);
        Assert.True(drive <= 0, $"her sail drives {drive:F1} N ten degrees off the wind");
    }

    [Fact]
    public void FurledSheIsPushedOnlyDownwind()
    {
        var (body, _) = Outrigger(Furled);
        var load = body.Steady(OverHerFloat, Still, Yaw, Waterline);
        Assert.True(load.X < 0);
        Assert.Equal(0, load.Z, 3);
    }

    [Fact]
    public void HerSailSetFromHerBowTurnsHerBowAwayFromTheWind()
    {
        var (body, sail) = Outrigger(FullSail);
        var load = body.Steady(OverHerFloat, Still, Yaw, Waterline);
        Assert.True(sail.State.Centre(sail.Set, sail.Fill).Along < 0);
        Assert.True(load.NewtonMetres < 0);
    }

    [Fact]
    public void RunningHerSailDrawsAndDrivesHerAhead()
    {
        var (body, sail) = Outrigger(FullSail);
        body.Steady(FromAstern, Still, Yaw, Waterline);
        var drive = sail.Rig.End.Ahead * sail.Load!.Points.Sum(push => push.Force.Along);
        Assert.False(sail.Aback);
        Assert.True(drive > 0, $"running her sail drives {drive:F0} N: {sail.Rig.End.Describe(sail.State)}");
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
            var load = body.Load(OverHerFloat, set.Velocity, Yaw, Waterline, step);
            set.Settle(drag, new WaterOnHull(0, 0), Yaw, step, drag.Acceleration(load.X, load.Z, Yaw));
        }
        Assert.InRange(set.Velocity.Z / Breeze, 0.25, 0.4);
    }
}
