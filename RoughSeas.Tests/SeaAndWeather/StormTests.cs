using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class StormTests
{
    private static Storm Gale(bool northern) => new()
    {
        Type = Fixtures.Config().Storms.Single(type => type.Code == "gale"),
        Shape = Fixtures.Config().Storm,
        BornHours = 100,
        LifetimeHours = 40,
        OriginX = 0,
        OriginZ = 0,
        VelocityX = 300,
        VelocityZ = 0,
        Radius = 2000,
        PeakMps = 25,
        Northern = northern
    };

    [Fact]
    public void BrewsRagesAndDiesOut()
    {
        var storm = Gale(northern: true);

        Assert.Equal(0, storm.Strength(99));
        Assert.InRange(storm.Strength(104), 0.01, 0.99);
        Assert.Equal(1, storm.Strength(120));
        Assert.Equal(0, storm.Strength(141));
    }

    [Fact]
    public void MovesAlongItsTrack()
    {
        Assert.Equal((3000, 0), Gale(northern: true).CentreAt(110));
    }

    [Fact]
    public void SpinsAnticlockwiseInTheNorth()
    {
        var east = Gale(northern: true).InfluenceAt(6000 + 800, 0, 120);

        Assert.True(east.WindZ < 0, "east of the centre the wind blows north, towards -Z");
    }

    [Fact]
    public void SpinsClockwiseInTheSouth()
    {
        var east = Gale(northern: false).InfluenceAt(6000 + 800, 0, 120);

        Assert.True(east.WindZ > 0, "east of the centre the wind blows south, towards +Z");
    }

    [Fact]
    public void LeavesTheSeaBeyondItsRadiusAlone()
    {
        Assert.Equal(default, Gale(northern: true).InfluenceAt(6000 + 2001, 0, 120));
    }
}
