using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class BreakerTests
{
    private static readonly BreakingConfig Config = Fixtures.Config().Body.Breaking;
    private static readonly Breaker Crest = new(BrokeSeconds: 0, HeightMetres: 2, PeriodSeconds: 8, Celerity: 12, TowardRadians: 0);

    [Fact]
    public void ChurnsTheMostAirAsItBreaksAndAPercentAfterOnePeriod()
    {
        Assert.Equal(Config.Aeration, Crest.AirShare(Config, 0), 6);
        Assert.Equal(Config.AerationAfterPeriod, Crest.AirShare(Config, Crest.PeriodSeconds), 6);
        Assert.Equal(0, Crest.AirShare(Config, -1));
    }

    [Fact]
    public void RunsItsRollerAtThreeQuartersOfTheWaveSpeedAndFades()
    {
        Assert.Equal(Config.RollerSpeedRatio * Crest.Celerity, Crest.RollerSpeed(Config, 0), 6);
        Assert.True(Crest.RollerSpeed(Config, 3 * Config.RollerSeconds) < 0.05 * Crest.RollerSpeed(Config, 0));
    }

    [Fact]
    public void ChurnsTheWaterAboutAsDeepAsTheWaveIsHigh()
    {
        Assert.InRange(Crest.PlumeDepth(Config), 0.7 * Crest.HeightMetres, 0.9 * Crest.HeightMetres);
    }

    [Fact]
    public void IsSpentOnceItsAirAndRollerHaveFaded()
    {
        Assert.False(Crest.Spent(Config, Crest.PeriodSeconds));
        Assert.True(Crest.Spent(Config, 3 * Crest.PeriodSeconds));
    }

    [Fact]
    public void SeesEachCrestOnceAsItPasses()
    {
        var watch = new CrestWatch();
        var crests = new List<double>();
        for (var tick = 0; tick < 60 * 40; tick++)
        {
            if (watch.Passed(0.8 * Math.Sin(2 * Math.PI * tick / 60 / 8)) is { } crest) crests.Add(crest);
        }

        Assert.Equal(5, crests.Count);
        Assert.All(crests, crest => Assert.Equal(0.8, crest, 2));
    }

    [Fact]
    public void SeesNoCrestInATroughThatTurns()
    {
        var watch = new CrestWatch();
        double?[] seen = [watch.Passed(-0.5), watch.Passed(-0.3), watch.Passed(-0.4)];

        Assert.All(seen, Assert.Null);
    }
}
