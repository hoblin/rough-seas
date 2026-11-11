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
