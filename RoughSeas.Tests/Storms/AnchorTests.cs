using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class AnchorTests
{
    private const double FullScope = 5;
    private const double Granite = 2650;
    private const double Seawater = 1025;
    private const double Sand = 0.37;
    private const double Rock = 0.49;
    private const double Gravity = 9.81;

    private readonly Anchor anchor = new(0, 0, 0, RodeMetres: 50, Rope: "rope-flax", Kilograms: 100, BedHold: Sand);

    [Fact]
    public void LetGoInShallowWaterItPaysOutFullScope()
    {
        Assert.Equal(50, LetGo(depth: 10, rodeAboard: 210)!.Value.RodeMetres);
    }

    [Fact]
    public void LetGoTooDeepForFullScopeItPaysOutAllTheRodeAboard()
    {
        Assert.Equal(210, LetGo(depth: 60, rodeAboard: 210)!.Value.RodeMetres);
    }

    [Fact]
    public void LetGoDeeperThanTheRodeReachesItHangs()
    {
        Assert.Null(LetGo(depth: 211, rodeAboard: 210));
    }

    [Fact]
    public void TheVesselSwingsFurtherInShallowWater()
    {
        Assert.Equal(40, anchor.Reach(30), 6);
        Assert.Equal(50, anchor.Reach(0), 6);
        Assert.Equal(0, anchor.Reach(60));
    }

    [Fact]
    public void PulledAlongTheBedItHoldsTheShareOfItsDryWeightMeasuredForItsBed()
    {
        Assert.Equal(Sand * 100 * Gravity, anchor.Holding(depth: 0, Granite, Seawater), 6);
        Assert.Equal(Rock * 100 * Gravity, (anchor with { BedHold = Rock }).Holding(depth: 0, Granite, Seawater), 6);
    }

    [Fact]
    public void ARodeRisingFromItLiftsItLeastAtTheSlopeWhoseTangentIsItsFriction()
    {
        var flat = anchor.Holding(depth: 0, Granite, Seawater);
        var friction = Sand / (1 - Seawater / Granite);
        var weakest = anchor.Holding(depth: 50 * System.Math.Sin(System.Math.Atan(friction)), Granite, Seawater);

        Assert.True(weakest < anchor.Holding(depth: 10, Granite, Seawater));
        Assert.True(weakest < anchor.Holding(depth: 40, Granite, Seawater));
        Assert.True(weakest < flat);
    }

    [Fact]
    public void ARodeStraightUpLiftsItOnceItPullsWhatItWeighsUnderWater()
    {
        Assert.Equal(100 * Gravity * (1 - Seawater / Granite), anchor.Holding(depth: 50, Granite, Seawater), 6);
    }

    [Fact]
    public void AnAnchorWithNoWeightHoldsNothing()
    {
        Assert.Equal(0, (anchor with { Kilograms = 0 }).Holding(depth: 10, Granite, Seawater));
    }

    [Fact]
    public void TheShippedBedHoldsAndStoneAreReadFromTheConfig()
    {
        var mooring = Fixtures.Config().Mooring;

        Assert.Equal(Sand, mooring.SandHold);
        Assert.Equal(Rock, mooring.RockHold);
        Assert.Equal(Granite, mooring.AnchorStoneDensity);
    }

    [Fact]
    public void DraggingMovesWhereItLies()
    {
        Assert.Equal(anchor with { X = 2, Z = -1 }, anchor.DraggedBy(2, -1));
    }

    [Fact]
    public void ADraggingAnchorHoldsHerBackByItsHoldingAndNeverDrawsHerBack()
    {
        const double sailboatWithItsWater = 2400;
        const double tick = 1 / 30.0;
        var holding = Anchor.Dragging(1.0, 0, 300, sailboatWithItsWater, tick, 1);
        Assert.Equal(-300 * tick / sailboatWithItsWater, holding.Kick, 12);
        Assert.Equal(300, holding.PeakNewtons);

        Assert.Equal(-0.001, Anchor.Dragging(0.001, 0, 300, sailboatWithItsWater, tick, 1).Kick, 12);
        Assert.Equal(0, Anchor.Dragging(-0.5, 0, 300, sailboatWithItsWater, tick, 1).Kick);
    }

    private static Anchor? LetGo(double depth, double rodeAboard) =>
        Anchor.LetGo(0, -depth, 0, depth, rodeAboard, FullScope, "rope-flax", 100, Sand);
}
