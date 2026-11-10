using RoughSeas.Water;

namespace RoughSeas.Tests.Water;

public class WaveTheoryTests
{
    [Fact]
    public void AWaveOfNoPeriodRunsAtNoSpeed() => Assert.Equal(0, WaveTheory.Celerity(0, 5));

    private static double Number(double period) => WaveTheory.DeepNumber(period);

    [Fact]
    public void InDeepWaterAWavesEnergyTravelsAtHalfItsSpeed() =>
        Assert.Equal(0.5 * Math.Sqrt(WaveTheory.Gravity / Number(8)), WaveTheory.GroupSpeed(Number(8), 200), 3);

    [Fact]
    public void InShallowWaterItTravelsAtTheSpeedOfTheDepth() =>
        Assert.Equal(Math.Sqrt(WaveTheory.Gravity * 0.5), WaveTheory.GroupSpeed(Number(20), 0.5), 1);

    [Fact]
    public void AWaveShortensAsTheWaterShoals() => Assert.True(WaveTheory.Wavenumber(Number(8), 2) > Number(8));

    [Fact]
    public void TheIribarrenNumberGrowsWithTheBedsSlope() =>
        Assert.True(WaveTheory.Iribarren(0.2, 1, WaveTheory.DeepLength(8)) > WaveTheory.Iribarren(0.02, 1, WaveTheory.DeepLength(8)));

    [Fact]
    public void AFlatSeaHasNoIribarrenNumberToBreakBy() => Assert.Equal(double.PositiveInfinity, WaveTheory.Iribarren(0.1, 0, 100));

    [Fact]
    public void HeightsFollowTheRayleighDistribution()
    {
        Assert.Equal(2, WaveTheory.RayleighHeight(2, 1 - Math.Exp(-2)), 6);
        Assert.Equal(0, WaveTheory.RayleighHeight(2, 0));
    }

    [Fact]
    public void ADeepWaveRunsAtThePeriodsSpeed() =>
        Assert.Equal(WaveTheory.Gravity * 8 / (2 * Math.PI), WaveTheory.Celerity(8, double.PositiveInfinity), 6);

    [Fact]
    public void AShallowWaveRunsAtTheDepthsSpeed() =>
        Assert.Equal(Math.Sqrt(WaveTheory.Gravity * 1), WaveTheory.Celerity(12, 1), 1);

    [Fact]
    public void ASeasSignificantHeightIsFourStandardDeviations() =>
        Assert.Equal(4.0, WaveTheory.SignificantHeight(2.0), 6);

    [Fact]
    public void ABoreHasNoMeanHeightAndTheEnergyOfASine()
    {
        const int Samples = 100000;
        double mean = 0, variance = 0;
        for (var i = 0; i < Samples; i++)
        {
            var bore = WaveTheory.Bore(i * 2 * Math.PI / Samples, 0.1);
            mean += bore / Samples;
            variance += bore * bore / Samples;
        }
        Assert.Equal(0, mean, 3);
        Assert.Equal(0.5, variance, 3);
    }

    [Fact]
    public void InDeepWaterTheOrbitsFadeAsTheExponential() =>
        Assert.Equal(Math.Exp(0.1 * 0.5), WaveTheory.OrbitalFade(0.1, double.PositiveInfinity, 0.5, 0), 6);

    [Fact]
    public void ToDeepWaterTheShallowFormulaMeetsTheDeepOne() =>
        Assert.Equal(WaveTheory.OrbitalFade(0.1, double.PositiveInfinity, 0, 1), WaveTheory.OrbitalFade(0.1, 150, 0, 1), 3);

    [Fact]
    public void InShallowWaterTheWaterMovesFasterForTheSameWave() =>
        Assert.Equal(1 / Math.Tanh(0.3), WaveTheory.OrbitalFade(0.1, 3, 0, 0), 6);

    [Fact]
    public void ABoresFrontKeepsToItsLimits()
    {
        Assert.Equal(0.02, WaveTheory.BoreFront(0.001, 0.1, 0.2));
        Assert.Equal(0.5, WaveTheory.BoreFront(100, 1, 0.2));
    }
}
