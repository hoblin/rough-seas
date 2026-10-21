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
