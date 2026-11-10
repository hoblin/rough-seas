using RoughSeas.Surf;

namespace RoughSeas.Tests.Surf;

public class ShoalingTests
{
    private static double Number(double period) => 4 * Math.PI * Math.PI / (Shoaling.Gravity * period * period);

    [Fact]
    public void InDeepWaterAWavesEnergyTravelsAtHalfItsSpeed() =>
        Assert.Equal(0.5 * Math.Sqrt(Shoaling.Gravity / Number(8)), Shoaling.GroupSpeed(Number(8), 200), 3);

    [Fact]
    public void InShallowWaterItTravelsAtTheSpeedOfTheDepth() =>
        Assert.Equal(Math.Sqrt(Shoaling.Gravity * 0.5), Shoaling.GroupSpeed(Number(20), 0.5), 1);

    [Fact]
    public void AWaveShortensAsTheWaterShoals() => Assert.True(Shoaling.Wavenumber(Number(8), 2) > Number(8));

    [Fact]
    public void TheIribarrenNumberGrowsWithTheBedsSlope() =>
        Assert.True(Shoaling.Iribarren(0.2, 1, Number(8)) > Shoaling.Iribarren(0.02, 1, Number(8)));
}
