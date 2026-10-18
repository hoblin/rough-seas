using RoughSeas.Storms;

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
    public void ABreakerHitsHarderAsTheWavesGrowAndOnAWiderHull()
    {
        Assert.True(waves.BreakerEnergy(4, 2) > 4 * waves.BreakerEnergy(2, 2));
        Assert.Equal(2 * waves.BreakerEnergy(2, 1), waves.BreakerEnergy(2, 2), 6);
    }

    [Fact]
    public void ABreakerInAGaleCanSmashTheRaftButNotTheSailboat()
    {
        var (raft, sailboat) = (Fixtures.Raft, Fixtures.Sailboat);

        Assert.True(waves.BreakerEnergy(5.5, raft.BeamMetres) > raft.ImpactCapacity);
        Assert.True(waves.BreakerEnergy(5.5, sailboat.BeamMetres) < sailboat.ImpactCapacity);
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
