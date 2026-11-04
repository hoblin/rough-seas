using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ResistanceTests
{
    private static readonly Resistance Hull = new(FrameJoules: 30000, ShellPoint: 400, ShellCut: 100);
    private static readonly Resistance Framed = new(FrameJoules: 400, ShellPoint: double.PositiveInfinity, ShellCut: 100, Framed: new(40, 100, 0.4, 0));
    private static readonly Resistance Bark = new(FrameJoules: 400, ShellPoint: 20, ShellCut: 20, Framed: new(40, 60, 0.2, 20));

    [Fact]
    public void TheWholeFrameTakesABroadLoadWhateverTheShell()
    {
        Assert.Equal(30000, new Resistance(FrameJoules: 30000, ShellPoint: 400, ShellCut: 100).Against(Blow.Broad, backed: true));
    }

    [Fact]
    public void ABroadBlowTheShellCrushesAwayLeavesTheFrameUnloaded()
    {
        var bundle = new Resistance(FrameJoules: 30000, ShellPoint: double.PositiveInfinity, ShellCut: double.PositiveInfinity, CushionJoules: 20000);

        Assert.Equal(0, bundle.Severity(15000, Blow.Broad, backed: false, strength: 1));
        Assert.Equal(0.5, bundle.Severity(35000, Blow.Broad, backed: false, strength: 1));
    }

    [Fact]
    public void AWeakerPieceCrushesLessAndBindsLess()
    {
        var bundle = new Resistance(FrameJoules: 30000, ShellPoint: double.PositiveInfinity, ShellCut: double.PositiveInfinity, CushionJoules: 20000);

        Assert.Equal(1, bundle.Severity(25000, Blow.Broad, backed: false, strength: 0.5), 9);
    }

    [Fact]
    public void OnlyABroadBlowIsCrushedAway()
    {
        var bundle = new Resistance(FrameJoules: 30000, ShellPoint: 400, ShellCut: 100, CushionJoules: 20000);

        Assert.Equal(2, bundle.Severity(800, Blow.Point, backed: false, strength: 1));
    }

    [Fact]
    public void ABlowOnTheShellOpensAtMostThePlaceItLands()
    {
        Assert.Equal(1, Hull.Loss(3, Blow.Point, byPlace: true, backed: true));
        Assert.Equal(1, Hull.Loss(3, Blow.Cut, byPlace: true, backed: true));
        Assert.Equal(0.4, Hull.Loss(0.4, Blow.Point, byPlace: true, backed: true));
    }

    [Fact]
    public void ABlowOnTheShellOfAHullReckonedWholeTakesOnePlacesShareOfIt()
    {
        Assert.Equal(1.0 / HullShape.Places.Length, Hull.Loss(3, Blow.Cut, byPlace: false, backed: true), 9);
    }

    [Fact]
    public void ABroadLoadWeighsOnTheWholeHullAlike()
    {
        Assert.Equal(1.2, Hull.Loss(1.2, Blow.Broad, byPlace: false, backed: true));
    }

    [Fact]
    public void OnlyABroadLoadBeyondTheFrameBreaksItAtOnce()
    {
        Assert.True(Hull.BreaksFrame(Blow.Broad, 1.2, backed: false));
        Assert.False(Hull.BreaksFrame(Blow.Broad, 0.9, backed: false));
        Assert.False(Hull.BreaksFrame(Blow.Point, 5, backed: false));
    }

    [Fact]
    public void ABroadBlowOnAFramedHullMeetsOnlyItsShellAndTheMembersWhereItLands()
    {
        Assert.Equal(40, Framed.Against(Blow.Broad, backed: true));
        Assert.Equal(100, Framed.Against(Blow.Broad, backed: false));
        Assert.Equal(0.5, Framed.Severity(20, Blow.Broad, backed: true, strength: 1));
    }

    [Fact]
    public void ABroadBlowLandsOverAFrameMemberByTheMembersShareOfTheHull()
    {
        Assert.True(Framed.Backed(HullPlace.Bottom, 0.39, Blow.Broad));
        Assert.False(Framed.Backed(HullPlace.PortSide, 0.41, Blow.Broad));
    }

    [Fact]
    public void TheMemberBehindBreaksJustAsTheModelDrawsItBroken()
    {
        Assert.Equal(HullModel.FrameBroken, Framed.Loss(1, Blow.Broad, byPlace: true, backed: true));
        Assert.Equal(1, Framed.Loss(3, Blow.Broad, byPlace: true, backed: true));
    }

    [Fact]
    public void AKnockThatBreaksNothingLeavesAHideAndItsLathsWhole() =>
        Assert.Equal(0, Framed.Loss(0.9, Blow.Broad, byPlace: true, backed: false));

    [Fact]
    public void AKnockThatOpensBarkCracksItThoughTheRibsHold()
    {
        Assert.Equal(HullModel.FrameBroken / 2, Bark.Loss(0.5, Blow.Broad, byPlace: true, backed: true));
        Assert.Equal(0, Bark.Loss(0.4, Blow.Broad, byPlace: true, backed: true));
    }

    [Fact]
    public void OnlyWhatAStoveInPlaceLeavesOverReachesTheWholeFrame()
    {
        Assert.False(Framed.BreaksFrame(Blow.Broad, 11.9, backed: true));
        Assert.True(Framed.BreaksFrame(Blow.Broad, 12.5, backed: true));
        Assert.False(Framed.BreaksFrame(Blow.Broad, 5.9, backed: false));
        Assert.True(Framed.BreaksFrame(Blow.Broad, 6, backed: false));
    }

    [Fact]
    public void ABreakerBelowThePlankingsStrengthLeavesItWhole() =>
        Assert.Equal(0, Hull.Loss(0.9, Blow.Slam, byPlace: true, backed: true));

    [Fact]
    public void ABreakerBeyondThePlankingsStrengthStovesInThePlaceItSlams()
    {
        Assert.Equal(1, Hull.Loss(1.3, Blow.Slam, byPlace: true, backed: true));
        Assert.Equal(1.0 / HullShape.Places.Length, Hull.Loss(1.3, Blow.Slam, byPlace: false, backed: true), 9);
        Assert.False(Hull.BreaksFrame(Blow.Slam, 5, backed: false));
    }

    [Fact]
    public void AStripBendsUntilItsMomentReachesWhatItsSectionHolds()
    {
        Assert.Equal(2 * 60e6 * 0.02 * 0.02 / (0.3 * 0.3), Resistance.Bending(60e6, 0.02, 0.3, 1.0 / 12), 3);
        Assert.Equal(60e6 * 0.02 * 0.02 / (3 * 0.3 * 0.3), Resistance.Bending(60e6, 0.02, 0.3, 1.0 / 2), 3);
    }

    [Fact]
    public void HolingTakesTheWorkOfFractureAroundTheHoleThroughTheShell()
    {
        double radius = 0.03;

        Assert.Equal(75000 * 2 * Math.PI * radius * 0.02, Resistance.Holing(75000, 0.02, Math.PI * radius * radius), 6);
    }

    [Fact]
    public void AThickerShellTakesMoreToHole()
    {
        Assert.Equal(2 * Resistance.Holing(75000, 0.02, 0.0025), Resistance.Holing(75000, 0.04, 0.0025), 6);
    }
}
