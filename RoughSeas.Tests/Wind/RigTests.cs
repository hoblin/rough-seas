using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class RigTests
{
    private const double Breeze = 9;
    private const int FullSail = 2;
    private static readonly (double X, double Z) OverHerFloat = (-Breeze, 0);

    private static Sail Drawing(int tacked)
    {
        var body = Fixtures.InWind("outrigger");
        body.Sail!.Hoist(FullSail);
        body.Sail.Rig.Tacked = tacked;
        body.Load(OverHerFloat, (0, 0), 0, 0);
        return body.Sail;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WhatHerRigHandsHerHullAddsUpToTheForceAndMomentOfHerSail(int tacked)
    {
        var sail = Drawing(tacked);
        var end = sail.Rig.End;
        var turn = sail.Turn;
        var (at, force) = (sail.Form.Centre(sail.Set, turn, sail.Depth), new HullPoint(120, 0, -310));
        var load = end.Loads(at, force, turn);

        var total = load.Points.Aggregate(default(HullPoint), (sum, push) => sum + push.Force);
        var moment = load.Points.Aggregate(default(HullPoint), (sum, push) => sum + push.At.Cross(push.Force) + push.Couple);
        Assert.Equal(0, (total - force).Length, 6);
        Assert.Equal(0, (moment - at.Cross(force)).Length, 6);
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
}
