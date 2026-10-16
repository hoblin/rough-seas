using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class ShoreHazardTests
{
    private readonly ShoreHazard hazard = new(Fixtures.Config().Ashore);

    [Fact]
    public void AVesselMooredAgainstTheShoreThroughAGaleIsAlmostSurelyThrownOnIt()
    {
        Assert.True(hazard.Chance(Enumerable.Repeat(4.0, 24), 1, openWaterToShore: 0) > 0.99);
    }

    [Fact]
    public void CalmWeatherThrowsNothingAshore()
    {
        Assert.Equal(0, hazard.Chance(Enumerable.Repeat(0.0, 24), 1, openWaterToShore: 0));
    }

    [Fact]
    public void OpenWaterBetweenItsSwingAndTheShoreKeepsItSafe()
    {
        var gale = Enumerable.Repeat(4.0, 24).ToArray();

        Assert.True(hazard.Chance(gale, 1, openWaterToShore: 200) < 0.01);
        Assert.True(hazard.Chance(gale, 1, openWaterToShore: 10) < hazard.Chance(gale, 1, openWaterToShore: 0));
    }

    [Fact]
    public void AVesselSwingingOverTheShoreTakesTheFullSurf()
    {
        var gale = Enumerable.Repeat(2.0, 6).ToArray();

        Assert.Equal(hazard.Chance(gale, 1, openWaterToShore: 0), hazard.Chance(gale, 1, openWaterToShore: -5), 9);
    }

    [Fact]
    public void SparseSamplesWeighForTheHoursTheyStandFor()
    {
        Assert.Equal(hazard.Chance(Enumerable.Repeat(1.0, 10), 1, 0), hazard.Chance(Enumerable.Repeat(1.0, 5), 2, 0), 6);
    }
}
