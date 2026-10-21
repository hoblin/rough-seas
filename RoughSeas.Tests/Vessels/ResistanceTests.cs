using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ResistanceTests
{
    private readonly Resistance resistance = new(FrameJoules: 30000, PlaceJoules: 400, CutShare: 0.25);

    [Fact]
    public void TheWholeFrameTakesABroadLoad()
    {
        Assert.Equal(30000, resistance.Against(Blow.Broad));
    }

    [Fact]
    public void OnlyTheShellWhereItLandsTakesAPointBlow()
    {
        Assert.Equal(400, resistance.Against(Blow.Point));
    }

    [Fact]
    public void AnEdgeSpendsItsEnergyOnTheCutAlone()
    {
        Assert.Equal(100, resistance.Against(Blow.Cut));
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
