using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class StormSurveyTests
{
    private static readonly Storm Gale = Fixtures.Model(seed: 42).Storms.Stage("gale", 512000, 512000, 2000, peakMps: null, radius: null);
    private static readonly Storm Squall = Fixtures.Model(seed: 42).Storms.Stage("squall", 512000, 512000, 2000, peakMps: null, radius: null);

    private readonly StormSurvey survey = new([new(Squall, 40, 5, 1), new(Gale, 10, 5, 1), new(Gale, 12, 10, 1)], 0, 100);

    [Fact]
    public void CountsTheHoursAStormStandsOverTheSquareOnceWhereTwoOverlap()
    {
        Assert.Equal(17, survey.CoveredHours, precision: 9);
    }

    [Fact]
    public void FindsTheLongestCalmUpToTheEndOfTheWindow()
    {
        Assert.Equal(55, survey.LongestCalmHours, precision: 9);
    }

    [Fact]
    public void ListsThePassesInOrderOfArrivalAndCountsThemByType()
    {
        Assert.Equal([10, 12, 40], survey.Passes.Select(pass => pass.FromHours));
        Assert.Equal([("gale", 2), ("squall", 1)], survey.ByType);
    }

    [Fact]
    public void CallsTheWholeWindowCalmWithoutAStorm()
    {
        var calm = new StormSurvey([], 0, 100);

        Assert.Equal(0, calm.CoveredHours);
        Assert.Equal(100, calm.LongestCalmHours);
    }
}
