using RoughSeas.Storms;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Storms;

public class WavesTests
{
    private readonly Waves waves = new(Fixtures.Config().Waves);

    [Fact]
    public void ASteadyPullLeavesTheWavesLessToMakeUp()
    {
        Assert.Equal(Waves.ChanceToExceed(1000, 2000), Waves.ChanceToExceed(1000, 2000, 0));
        Assert.Equal(Waves.ChanceToExceed(1000, 1500), Waves.ChanceToExceed(1000, 2000, 500), 12);
        Assert.Equal(1, Waves.ChanceToExceed(1000, 2000, 2000));
        Assert.Equal(1, Waves.ChanceToExceed(0, 2000, 2500));
        Assert.Equal(0, Waves.ChanceToExceed(0, 2000, 1500));
    }

    [Fact]
    public void FewWavesBreakInABreezeAndManyInAStorm()
    {
        Assert.InRange(waves.BreakingChance(10), 0.005, 0.02);
        Assert.InRange(waves.BreakingChance(25), 0.15, 0.3);
        Assert.Equal(1, waves.BreakingChance(80));
    }

    [Fact]
    public void AWaveBreaksInWaterShallowerThanItsHeightAndAQuarter()
    {
        Assert.True(waves.BreaksAt(depth: 2, height: 2));
        Assert.False(waves.BreaksAt(depth: 3, height: 2));
    }

    [Fact]
    public void OneWaveInSevenOverTopsTheSignificantHeight()
    {
        Assert.Equal(2, Waves.WaveHeight(2, 1 - Math.Exp(-2)), 6);
        Assert.Equal(0, Waves.WaveHeight(2, 0));
    }

    [Fact]
    public void ABreakingCrestStandsHigherThanASwellOfTheSameHeight() =>
        Assert.True(waves.Crest(2, breaking: true) > waves.Crest(2, breaking: false));

    [Fact]
    public void AWaveRunsAtItsDeepWaterSpeedInDeepWater() =>
        Assert.Equal(9.81 * 8 / (2 * Math.PI), Waves.Celerity(8, double.PositiveInfinity), 6);

    [Fact]
    public void AWaveSlowsToTheShallowWaterSpeedOverAShallowBed() =>
        Assert.Equal(Math.Sqrt(9.81 * 1), Waves.Celerity(12, 1), 1);

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
        Assert.Equal(Waves.Celerity(4, double.PositiveInfinity), waves.BreakerCelerity(8, 4, double.PositiveInfinity), 6);
    }

    [Fact]
    public void AShortBreakerSlowsOverAShallowBedAsAnyWaveOfItsPeriodDoes()
    {
        var ownPeriod = Math.Sqrt(2 * Math.PI / 0.142 / 9.81);

        Assert.Equal(Waves.Celerity(ownPeriod, 1.28), waves.BreakerCelerity(1, 9, 1.28), 6);
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
        var sailboat = Fixtures.Sailboat;

        Assert.True(waves.ShippedLitres(2, sailboat.BeamMetres) > 0);
        Assert.Equal(4 * waves.ShippedLitres(2, sailboat.BeamMetres), waves.ShippedLitres(4, sailboat.BeamMetres), 6);
    }

    [Fact]
    public void AGaleSeaPullsAMooringHarderThanTheGaleItself()
    {
        var sailboat = Fixtures.Sailboat;
        var windOnly = waves.Force(20, 0, sailboat.BeamMetres, sailboat.WindageSquareMetres);
        var withWaves = waves.Force(20, 4, sailboat.BeamMetres, sailboat.WindageSquareMetres);

        Assert.True(withWaves > 2 * windOnly);
    }

    [Fact]
    public void AFlaxRopeHoldsTheRaftInABreezeButNotForeverInAGale()
    {
        var raft = Fixtures.Raft;
        var breeze = waves.Force(9, 2.7, raft.BeamMetres, raft.WindageSquareMetres);
        var gale = waves.Force(20, 5.5, raft.BeamMetres, raft.WindageSquareMetres);

        Assert.True(Waves.ChanceToExceed(breeze, Fixtures.FlaxNewtons) < 1e-6);
        Assert.InRange(Waves.ChanceToExceed(gale, Fixtures.FlaxNewtons), 1e-3, 0.02);
    }

    [Fact]
    public void NoWaveOverloadsWhatTheSeaDoesNotPull()
    {
        Assert.Equal(0, Waves.ChanceToExceed(0, 1000));
    }

    [Fact]
    public void FewerWavesPullPastAHigherLimit()
    {
        Assert.InRange(Waves.ChanceToExceed(1000, 2000), 0.01, 0.02);
        Assert.InRange(Waves.ChanceToExceed(2000, 1000), 0.3, 0.4);
    }
}
