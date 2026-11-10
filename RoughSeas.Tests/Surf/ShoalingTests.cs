using RoughSeas.Surf;

namespace RoughSeas.Tests.Surf;

public class ShoalingTests
{
    private static double Number(double period) => 4 * Math.PI * Math.PI / (Shoaling.Gravity * period * period);

    [Fact]
    public void AWaveKeepsItsHeightInDeepWater() => Assert.Equal(1, Shoaling.Factor(Number(8), 200), 3);

    [Fact]
    public void AWaveDipsALittleOverIntermediateDepthBeforeItGrows() => Assert.True(Shoaling.Factor(Number(10), 30) < 1);

    [Fact]
    public void InShallowWaterAWaveGrowsByGreensLaw() =>
        Assert.Equal(Math.Pow(2.0 / 0.5, 0.25), Shoaling.Factor(Number(12), 0.5) / Shoaling.Factor(Number(12), 2), 1);

    [Fact]
    public void AWaveShortensAsTheWaterShoals() => Assert.True(Shoaling.Wavenumber(Number(8), 2) > Number(8));

    [Fact]
    public void AWaveComingStraightInIsNotSpreadByRefraction() => Assert.Equal(1, Shoaling.Refraction(Number(8), 2, 1), 9);

    [Fact]
    public void AWaveComingInAtAnAngleSpreadsAsItTurnsAndLowers() => Assert.True(Shoaling.Refraction(Number(8), 2, Math.Cos(Math.PI / 4)) < 1);

    [Fact]
    public void DeepWaterDoesNotRefractAWaveEvenRunningAlongTheContours() => Assert.Equal(1, Shoaling.Refraction(Number(8), 200, 0.0001), 9);

    [Fact]
    public void ThereIsNoJumpBetweenAWaveRunningJustShorewardAndJustSeaward() =>
        Assert.Equal(Shoaling.Refraction(Number(8), 4, 0.01), Shoaling.Refraction(Number(8), 4, -0.01), 9);

    [Fact]
    public void NoDepthRefractsNothing() => Assert.Equal(1, Shoaling.Refraction(Number(8), 0, 0.5));

    [Fact]
    public void TheIribarrenNumberGrowsWithTheBedsSlope() =>
        Assert.True(Shoaling.Iribarren(0.2, 1, Number(8)) > Shoaling.Iribarren(0.02, 1, Number(8)));
}
