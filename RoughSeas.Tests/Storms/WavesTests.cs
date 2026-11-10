using RoughSeas.Storms;
using RoughSeas.Vessels;
using RoughSeas.Water;

namespace RoughSeas.Tests.Storms;

public class WavesTests
{
    private readonly Waves waves = new(Fixtures.Config().Waves);

    [Fact]
    public void FewWavesBreakInABreezeAndManyInAStorm()
    {
        Assert.InRange(waves.BreakingChance(10), 0.005, 0.02);
        Assert.InRange(waves.BreakingChance(25), 0.15, 0.3);
        Assert.Equal(1, waves.BreakingChance(80));
    }

    [Fact]
    public void ABreakerPlungesWithinTheIribarrenBandAndSpillsOrSurgesOutsideIt()
    {
        Assert.True(waves.PlungingBreaker(1));
        Assert.False(waves.PlungingBreaker(0.1));
        Assert.False(waves.PlungingBreaker(5));
    }

    [Fact]
    public void ABreakingCrestStandsHigherThanASwellOfTheSameHeight() =>
        Assert.True(waves.Crest(2, breaking: true) > waves.Crest(2, breaking: false));

    [Fact]
    public void AWavePlungesOnASteepBedAndSpillsOnAGentleOne()
    {
        Assert.True(waves.Plunges(height: 1, period: 6, bedSlope: 0.1, depth: 1.2));
        Assert.False(waves.Plunges(height: 1, period: 6, bedSlope: 0.02, depth: 1.2));
        Assert.False(waves.Plunges(height: 1, period: 6, bedSlope: 1, depth: 1.2));
    }

    [Fact]
    public void AHullFarInsideWhereAWavePlungedMeetsOnlyItsBore()
    {
        Assert.True(waves.Plunges(height: 2, period: 8, bedSlope: 0.1, depth: 2.3));
        Assert.False(waves.Plunges(height: 2, period: 8, bedSlope: 0.1, depth: 1));
    }

    [Fact]
    public void FewBreakersPlungeAtSea()
    {
        Assert.True(waves.PlungesAtSea(0.01));
        Assert.False(waves.PlungesAtSea(0.5));
    }

    [Fact]
    public void APlungingCrestSlamsAtItsImpactSpeedWhereASpillingOnePressesItsStagnationPressure()
    {
        Assert.Equal(0.5 * 1025 * 6.283 * 12 * 12, waves.SlamPressure(10, plunging: true), 6);
        Assert.Equal(0.5 * 1025 * 10 * 10, waves.SlamPressure(10, plunging: false), 6);
    }

    [Fact]
    public void ABreakerIsAsLongAsItsHeightMakesItAndNoFasterThanTheSea()
    {
        Assert.Equal(Math.Sqrt(9.81 / 0.142 / (2 * Math.PI)), waves.BreakerCelerity(1, 9, double.PositiveInfinity), 6);
        Assert.Equal(WaveTheory.Celerity(4, double.PositiveInfinity), waves.BreakerCelerity(8, 4, double.PositiveInfinity), 6);
    }

    [Fact]
    public void AShortBreakerSlowsOverAShallowBedAsAnyWaveOfItsPeriodDoes()
    {
        var ownPeriod = Math.Sqrt(2 * Math.PI / 0.142 / 9.81);

        Assert.Equal(WaveTheory.Celerity(ownPeriod, 1.28), waves.BreakerCelerity(1, 9, 1.28), 6);
    }

    [Fact]
    public void AGaleBreakerBreaksACoraclesRibsWhileTheSailboatsPlanksHoldEvenAPlunger()
    {
        var celerity = waves.BreakerCelerity(5.5, 9, double.PositiveInfinity);

        Assert.True(waves.SlamPressure(celerity, plunging: false) > Fixtures.Resistance("coracle").Against(Blow.Slam, backed: false));
        Assert.True(waves.SlamPressure(celerity, plunging: true) < Fixtures.Resistance("boat-sailed").Against(Blow.Slam, backed: false));
    }

    [Fact]
    public void ABreakerShipsMoreWaterAsTheWavesGrow()
    {
        Assert.True(waves.ShippedLitres(2, Fixtures.SailboatBeamMetres) > 0);
        Assert.Equal(4 * waves.ShippedLitres(2, Fixtures.SailboatBeamMetres), waves.ShippedLitres(4, Fixtures.SailboatBeamMetres), 6);
    }
}
