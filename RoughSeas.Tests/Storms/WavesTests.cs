using Newtonsoft.Json;
using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class WavesTests
{
    public static readonly StormsConfig Config =
        JsonConvert.DeserializeObject<StormsConfig>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "storms.json")))!;

    private readonly Waves waves = new(Config.Waves);

    [Fact]
    public void AGaleSeaRunsLongerWavesThanABreeze()
    {
        Assert.InRange(waves.Period(1), 3, 4);
        Assert.InRange(waves.Period(5.5), 7.5, 9);
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
    public void ABreakerHitsHarderAsTheWavesGrowAndOnAWiderHull()
    {
        Assert.True(waves.BreakerEnergy(4, 2) > 4 * waves.BreakerEnergy(2, 2));
        Assert.Equal(2 * waves.BreakerEnergy(2, 1), waves.BreakerEnergy(2, 2), 6);
    }

    [Fact]
    public void ABreakerInAGaleCanSmashARaftButNotASailboat()
    {
        const double raftCapacity = 1200, sailboatCapacity = 40000;

        Assert.True(waves.BreakerEnergy(5.5, 2) > raftCapacity);
        Assert.True(waves.BreakerEnergy(5.5, 2.5) < sailboatCapacity);
    }

    [Fact]
    public void AGaleSeaPullsAMooringHarderThanTheGaleItself()
    {
        var windOnly = waves.Force(20, 0, 2.5, 6);
        var withWaves = waves.Force(20, 4, 2.5, 6);

        Assert.True(withWaves > 2 * windOnly);
    }

    [Fact]
    public void AFlaxRopeHoldsARaftInABreezeButNotForeverInAGale()
    {
        const double flaxNewtons = 11000;

        Assert.True(Waves.ChanceToExceed(waves.Force(9, 2.7, 2, 1), flaxNewtons) < 1e-6);
        Assert.InRange(Waves.ChanceToExceed(waves.Force(20, 5.5, 2, 1), flaxNewtons), 1e-3, 0.02);
    }

    [Fact]
    public void ARopeRarelyMeetsAWaveTwiceItsUsualPull()
    {
        Assert.Equal(0, Waves.ChanceToExceed(0, 1000));
        Assert.InRange(Waves.ChanceToExceed(1000, 2000), 0.01, 0.02);
        Assert.InRange(Waves.ChanceToExceed(2000, 1000), 0.3, 0.4);
    }
}
