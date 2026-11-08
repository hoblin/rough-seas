using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class StormFieldTests
{
    private const double X = 512000;
    private const double Z = 512000;
    private const double Hours = 2000;

    [Fact]
    public void StagesAStormAtItsPeakWhereItIsPut()
    {
        var model = Fixtures.Model(seed: 42);

        var staged = model.Storms.Stage("hurricane", X, Z, Hours, peakMps: 55, radius: 1800);

        Assert.Contains(staged, model.Storms.Active(X, Z, Hours, range: 0));
        Assert.Equal(1, staged.Strength(Hours), precision: 6);
        Assert.Equal((X, Z), staged.CentreAt(Hours + 24));
        Assert.Equal("hurricane", model.Wind(X + 150, Z, Hours).Storm?.Code);
        Assert.Equal(12, new BeaufortScale(Fixtures.Config().Beaufort).ForceAt(model.Wind(X + 150, Z, Hours).Speed).Force);
    }

    [Fact]
    public void StagesASquallThatBlows()
    {
        var model = Fixtures.Model(seed: 42);

        model.Storms.Stage("squall", X, Z, Hours, peakMps: 24, radius: null);

        var wind = model.Wind(X, Z, Hours);
        Assert.Equal("squall", wind.Storm?.Code);
        Assert.InRange(wind.Speed, 20, 30);
    }

    [Fact]
    public void ForgetsTheStagedStormsWhenCleared()
    {
        var model = Fixtures.Model(seed: 42);
        var staged = model.Storms.Stage("gale", X, Z, Hours, peakMps: null, radius: null);

        model.Storms.Unstage();

        Assert.DoesNotContain(staged, model.Storms.Active(X, Z, Hours, range: 0));
    }

    [Fact]
    public void ForecastsTheStormsOfATypeBornInRangeAhead()
    {
        var model = Fixtures.Model(seed: 42, Fixtures.TropicalLatitude);

        var brewed = model.Storms.Brewed("hurricane", X, Fixtures.TropicalZ, 0, Fixtures.HoursPerYear, range: 30000).ToArray();

        Assert.NotEmpty(brewed);
        Assert.All(brewed, storm =>
        {
            var peak = storm.BornHours + storm.LifetimeHours / 2;
            var (x, z) = storm.CentreAt(peak);
            Assert.Equal("hurricane", storm.Code);
            Assert.InRange(storm.BornHours, 0, Fixtures.HoursPerYear);
            Assert.Contains(storm, model.Storms.Active(x, z, peak, range: 0));
        });
    }
}
