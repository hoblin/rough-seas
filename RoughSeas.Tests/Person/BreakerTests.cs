using RoughSeas.Person;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Tests.Storms;

namespace RoughSeas.Tests.Person;

public class BreakerTests
{
    private static readonly BreakingConfig Config = Fixtures.PersonConfig.Breaking;
    private static readonly Breaker Crest = new(BrokeSeconds: 0, HeightMetres: 2, PeriodSeconds: 8, Celerity: 12, TowardRadians: 0);

    [Fact]
    public void ChurnsAFifthAirAsItBreaksAndAPercentAfterOnePeriod()
    {
        Assert.Equal(0.2, Crest.AirShare(Config, 0), 6);
        Assert.Equal(0.01, Crest.AirShare(Config, Crest.PeriodSeconds), 6);
        Assert.Equal(0, Crest.AirShare(Config, -1));
    }

    [Fact]
    public void RunsItsRollerAtThreeQuartersOfTheWaveSpeedAndFades()
    {
        Assert.Equal(9, Crest.RollerSpeed(Config, 0), 6);
        Assert.Equal(9 / Math.E, Crest.RollerSpeed(Config, 1), 6);
    }

    [Fact]
    public void ReachesSixTenthsOfItsHeightDown()
    {
        Assert.Equal(1.2, Crest.PlumeDepth(Config), 6);
    }

    [Fact]
    public void IsSpentOnceItsAirAndRollerHaveGone()
    {
        Assert.False(Crest.Spent(Config, 8));
        Assert.True(Crest.Spent(Config, 60));
    }
}

public class CrestRollTests
{
    private const double Period = 8;
    private const double Tick = 0.1;
    private const double Deep = 100;

    private static readonly RoughSeas.Storms.WavesConfig Config = Fixtures.Config().Waves;
    private static readonly Waves Waves = new(Config);

    private static SeaState State(double wind = 15, double height = 2) => new(wind, 0, 6, height, Period, 0, null);

    // the sum of four trains, as drawn: several local maxima to a dominant wave
    private static double Surface(double t) =>
        0.6 * Math.Sin(2 * Math.PI * t / Period) + 0.35 * Math.Sin(2 * Math.PI * t / (Period * 0.84))
        + 0.25 * Math.Sin(2 * Math.PI * t / (Period * 0.65)) + 0.15 * Math.Sin(2 * Math.PI * t / (Period * 0.5));

    private static (int Rolls, int Broke, double Highest) Watch(double seconds, double depth, Func<double> draw, SeaState? state = null)
    {
        var roll = new CrestRoll(Waves, Config);
        var (rolls, broke, highest) = (0, 0, 0.0);
        for (var t = 0.0; t < seconds; t += Tick)
        {
            var crest = roll.See(Surface(t), Tick, state ?? State(), depth, () =>
            {
                rolls++;
                return draw();
            });
            if (crest is not { } hit) continue;
            broke++;
            highest = Math.Max(highest, hit.HeightMetres);
        }
        return (rolls, broke, highest);
    }

    [Fact]
    public void RollsOnceADominantWaveNotOnceALocalCrest()
    {
        const double waves = 100;

        var (rolls, _, _) = Watch(waves * Period, Deep, () => 1);

        Assert.InRange(rolls, 0.9 * waves, 1.01 * waves);
    }

    [Fact]
    public void BreaksByTheVesselsShareAtSea()
    {
        var (_, none, _) = Watch(100 * Period, Deep, () => 1);
        var (_, all, _) = Watch(100 * Period, Deep, () => 0);

        Assert.Equal(0, none);
        Assert.True(all > 80);
    }

    [Fact]
    public void BreaksForCertainInSurfAtTheSpeedOfThatDepth()
    {
        const double depth = 0.5;
        var roll = new CrestRoll(Waves, Config);
        BrokenCrest? hit = null;
        for (var t = 0.0; t < 40 && hit == null; t += Tick) hit = roll.See(Surface(t), Tick, State(), depth, () => 1);

        Assert.NotNull(hit);
        Assert.True(hit!.Value.Surf);
        Assert.Equal(Waves.BreakerCelerity(hit.Value.HeightMetres, Period, depth), hit.Value.Celerity, 6);
        Assert.True(hit.Value.Celerity < Waves.BreakerCelerity(hit.Value.HeightMetres, Period, double.PositiveInfinity));
    }

    [Fact]
    public void MeasuresTheWaveTroughToCrest()
    {
        var (_, _, highest) = Watch(50 * Period, Deep, () => 0);

        // over the crest's own height above still water
        Assert.True(highest > 1.35, $"{highest:F2} m");
    }

    [Fact]
    public void LeavesASeaTooSmallToBreakAlone()
    {
        var (rolls, broke, _) = Watch(50 * Period, 0.1, () => 0, State(height: Config.MinHeight / 2));

        Assert.Equal(0, rolls);
        Assert.Equal(0, broke);
    }
}
