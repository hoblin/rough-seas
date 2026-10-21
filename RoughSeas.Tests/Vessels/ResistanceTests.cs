using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ResistanceTests
{
    [Fact]
    public void TheWholeFrameTakesABroadLoadWhateverTheShell()
    {
        Assert.Equal(30000, new Resistance(FrameJoules: 30000, ShellPoint: 400, ShellCut: 100).Against(Blow.Broad, backed: true));
    }

    [Fact]
    public void ABlowOnTheShellOpensAtMostThePlaceItLands()
    {
        Assert.Equal(1, Resistance.Loss(3, Blow.Point, byPlace: true));
        Assert.Equal(1, Resistance.Loss(3, Blow.Cut, byPlace: true));
        Assert.Equal(0.4, Resistance.Loss(0.4, Blow.Point, byPlace: true));
    }

    [Fact]
    public void ABlowOnTheShellOfAHullReckonedWholeTakesOnePlacesShareOfIt()
    {
        Assert.Equal(1.0 / HullShape.Places.Length, Resistance.Loss(3, Blow.Cut, byPlace: false), 9);
    }

    [Fact]
    public void ABroadLoadWeighsOnTheWholeHullAlike()
    {
        Assert.Equal(1.2, Resistance.Loss(1.2, Blow.Broad, byPlace: false));
    }

    [Fact]
    public void OnlyABroadLoadBeyondTheFrameBreaksItAtOnce()
    {
        Assert.True(Resistance.BreaksFrame(Blow.Broad, Resistance.Loss(1.2, Blow.Broad, byPlace: true)));
        Assert.False(Resistance.BreaksFrame(Blow.Broad, Resistance.Loss(0.9, Blow.Broad, byPlace: true)));
        Assert.False(Resistance.BreaksFrame(Blow.Point, Resistance.Loss(5, Blow.Point, byPlace: true)));
    }

    [Fact]
    public void ABreakerBelowThePlankingsStrengthLeavesItWhole() =>
        Assert.Equal(0, Resistance.Loss(0.9, Blow.Slam, byPlace: true));

    [Fact]
    public void ABreakerBeyondThePlankingsStrengthStovesInThePlaceItSlams()
    {
        Assert.Equal(1, Resistance.Loss(1.3, Blow.Slam, byPlace: true));
        Assert.Equal(1.0 / HullShape.Places.Length, Resistance.Loss(1.3, Blow.Slam, byPlace: false), 9);
        Assert.False(Resistance.BreaksFrame(Blow.Slam, Resistance.Loss(5, Blow.Slam, byPlace: true)));
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
