using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class SeaWeatherModelTests
{
    private static readonly (double X, double Z, double Hours)[] Samples =
        Enumerable.Range(0, 40).Select(i => (500000.0 + i * 1777, 512000.0 - i * 913, 2000.0 + i * 7.3)).ToArray();

    private static readonly SeaWeatherModel Model = Fixtures.Model(seed: 42);
    private static readonly Storm[] Storms = Fixtures.YearOfStorms(Model);

    private static IEnumerable<(double X, double Z, double Hours)> InsideStorms() =>
        Storms.Select(storm =>
        {
            var hours = storm.BornHours + storm.LifetimeHours / 2;
            var (x, z) = storm.CentreAt(hours);
            return (x + storm.Radius * 0.3, z, hours);
        });

    [Fact]
    public void ComputesTheSameSeaFromTheSameSeed()
    {
        var other = Fixtures.Model(seed: 42);

        Assert.All(Samples.Concat(InsideStorms()), sample => Assert.Equal(
            Model.At(sample.X, sample.Z, sample.Hours, oceanity: 1),
            other.At(sample.X, sample.Z, sample.Hours, oceanity: 1)));
    }

    [Fact]
    public void ComputesTheSameCurrentsFromTheSameSeed()
    {
        var other = Fixtures.Model(seed: 42);

        Assert.All(Samples, sample => Assert.Equal(Model.Current(sample.X, sample.Z), other.Current(sample.X, sample.Z)));
    }

    [Fact]
    public void ComputesAnotherSeaFromAnotherSeed()
    {
        var other = Fixtures.Model(seed: 43);

        Assert.Contains(Samples, sample => Model.Wind(sample.X, sample.Z, sample.Hours) != other.Wind(sample.X, sample.Z, sample.Hours));
    }

    [Fact]
    public void RaisesTheSeaInsideAStorm()
    {
        var gale = Storms.First(storm => storm.Code == "gale" && storm.PeakMps >= 22);
        var hours = gale.BornHours + gale.LifetimeHours / 2;
        var (x, z) = gale.CentreAt(hours);

        var inside = Model.At(x + gale.Radius * 0.3, z, hours, oceanity: 1);
        var beyond = Model.At(x + gale.Radius * 3, z, hours, oceanity: 1);

        Assert.Equal("gale", inside.StormCode);
        Assert.InRange(inside.Beaufort, 8, 12);
        Assert.True(inside.WaveHeight > beyond.WaveHeight, $"waves {inside.WaveHeight} m inside, {beyond.WaveHeight} m beyond");
    }

    [Fact]
    public void FindsTheSameStormsUpCloseAsFromAfar()
    {
        Assert.All(Storms, storm =>
        {
            var hours = storm.BornHours + storm.LifetimeHours / 2;
            var (x, z) = storm.CentreAt(hours);
            Assert.Contains(storm, Model.Storms.Near(x + storm.Radius * 0.9, z, hours));
        });
    }

    [Fact]
    public void BrewsHurricanesOnlyInTheirSeason()
    {
        var hurricanes = Storms.Where(storm => storm.Code == "hurricane").ToArray();
        var monthlyWeight = Fixtures.Config().Storms.Single(type => type.Code == "hurricane").MonthlyWeight;

        Assert.NotEmpty(hurricanes);
        Assert.All(hurricanes, hurricane =>
            Assert.True(monthlyWeight[(int)(hurricane.BornHours / Fixtures.HoursPerYear % 1 * 12)] > 0));
    }

    [Fact]
    public void BlowsFromTheSouthWestInTheNorthernWesterlies()
    {
        var veer = Fixtures.Config().Background.VeerDegrees;
        var calm = Samples
            .Select(sample => Model.Wind(sample.X, sample.Z, sample.Hours))
            .Where(wind => wind.Presence == 0)
            .ToArray();

        Assert.NotEmpty(calm);
        Assert.All(calm, wind => Assert.InRange(Compass.FromDegrees(wind.X, wind.Z), 225 - veer, 225 + veer));
        Assert.True(calm.Average(wind => wind.X) > 0, "towards east");
        Assert.True(calm.Average(wind => wind.Z) < 0, "towards north");
    }

    [Fact]
    public void RaisesNoWavesOnLand()
    {
        Assert.Equal(0, Model.At(512000, 512000, 2000, oceanity: 0).WaveHeight);
    }

    [Fact]
    public void RainsHardInsideASquall()
    {
        var squall = Storms.First(storm => storm.Code == "squall");
        var hours = squall.BornHours + squall.LifetimeHours / 2;
        var (x, z) = squall.CentreAt(hours);

        Assert.True(Model.RainCloudness(x, z, hours) - 0.5 > 0.5 * squall.Type.Rain);
    }
}
