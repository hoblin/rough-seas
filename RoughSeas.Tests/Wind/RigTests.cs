using RoughSeas.Tests.Storms;
using RoughSeas.Water;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class RigTests
{
    private const double Breeze = 9;
    private const int FullSail = 2;
    private const double Precision = 6;
    private static readonly (double X, double Z) OverHerFloat = (-Breeze, 0);

    private static Sail Drawing(int tacked)
    {
        var body = Fixtures.InWind("outrigger");
        body.Sail!.Rig.Tacked = tacked;
        body.Sail.Hoist(FullSail, tacked);
        body.Steady(OverHerFloat, (0, 0), 0, 0);
        return body.Sail;
    }

    private static CrabClaw Claw(Sail sail) => (CrabClaw)sail.Rig.End;

    private static ClawState Set(Sail sail) => (ClawState)sail.State;

    [Theory]
    [InlineData(0.4, 0)]
    [InlineData(1.3, 0.7)]
    [InlineData(-0.6, -0.9)]
    public void TurnedOnHerHalyardAndHerBoomsLashingNoSparOrLineChangesLength(double yard, double boom)
    {
        var end = Claw(Drawing(0));
        var (rest, posed) = ((ClawState)end.Pose(0, 0), (ClawState)end.Pose(yard, boom));
        var tack = end.Origin;
        Assert.Equal((end.Hoisted.Masthead - rest.Bend).Length, (end.Hoisted.Masthead - posed.Bend).Length, Precision);
        Assert.Equal((rest.Peak - tack).Length, (posed.Peak - tack).Length, Precision);
        Assert.Equal((rest.Clew - tack).Length, (posed.Clew - tack).Length, Precision);
        double Opening(ClawState state) => Math.Acos((state.Peak - tack).Unit.Dot((state.Clew - tack).Unit));
        Assert.Equal(Opening(rest), Opening(posed), Precision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WhatHerRigHandsHerHullAddsUpToTheSailsForceAndTheWeightOfHerSparsAndCloth(int tacked)
    {
        var sail = Drawing(tacked);
        var (end, state) = (Claw(sail), Set(sail));
        var (at, force) = (state.Centre(sail.Set, sail.Fill), new HullPoint(120, 0, -310));
        var load = end.Loads(state, at, force);
        var weight = new HullPoint(0, -WaveTheory.Gravity, 0);
        var rig = Fixtures.WindageOf("outrigger")["rig"]!;
        var (yard, boom, cloth) = ((double)rig["yardKilograms"]! * weight, (double)rig["boomKilograms"]! * weight,
            sail.SquareMetres * Fixtures.Wind.ClothKgPerSquareMetre * weight);
        var expected = force + yard + boom + cloth;
        var turning = at.Cross(force) + (0.5 * (state.Peak + end.Origin)).Cross(yard) + (0.5 * (state.Clew + end.Origin)).Cross(boom) + at.Cross(cloth);

        var total = load.Points.Aggregate(default(HullPoint), (sum, push) => sum + push.Force);
        var moment = load.Points.Aggregate(default(HullPoint), (sum, push) => sum + push.At.Cross(push.Force) + push.Couple);
        Assert.Equal(0, (total - expected).Length, Precision);
        Assert.Equal(0, (moment - turning).Length, Precision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DrawingHerSheetAndHalyardTakeTheSailAndHerStaysOnlyPull(int tacked)
    {
        var load = Drawing(tacked).Load!;
        Assert.True(load.SheetNewtons > 0);
        Assert.True(load.HalyardNewtons > 0);
        Assert.All(load.Stays, stay => Assert.True(stay.Newtons >= 0, $"{stay.Line} {stay.Newtons:F0} N"));
        Assert.DoesNotContain(load.Stays, stay => stay.Line == "mast bending");
    }

    [Fact]
    public void WithTheWindAbeamHerSailStandsTheSameOnEitherTack()
    {
        var (asBuilt, shunted) = (Drawing(0), Drawing(1));
        string Seen(Sail sail) => sail.Rig.End.Describe(sail.State);
        Assert.True(Math.Abs(Set(asBuilt).Peak.Height - Set(shunted).Peak.Height) < 0.01, $"{Seen(asBuilt)} | {Seen(shunted)}");
        Assert.True(Math.Abs(Set(asBuilt).Clew.Height - Set(shunted).Clew.Height) < 0.01, $"{Seen(asBuilt)} | {Seen(shunted)}");
    }

    [Fact]
    public void HerSheetAndTheBoomsWeightHoldHerBoomBelowWhereTheYardAloneWouldCarryIt()
    {
        var sail = Drawing(0);
        var rigid = (ClawState)Claw(sail).Pose(sail.State.Yard, 0);
        Assert.True(Set(sail).Clew.Height < rigid.Clew.Height,
            $"boom end {Set(sail).Clew.Height:F2} m up, carried rigid {rigid.Clew.Height:F2} m: {sail.Rig.End.Describe(sail.State)}");
    }
}
