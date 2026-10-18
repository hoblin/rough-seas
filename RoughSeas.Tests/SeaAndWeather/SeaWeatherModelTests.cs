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

        Assert.All(Samples.Concat(InsideStorms()), sample =>
        {
            Assert.Equal(Model.Wind(sample.X, sample.Z, sample.Hours), other.Wind(sample.X, sample.Z, sample.Hours));
            Assert.Equal(Model.RainCloudness(sample.X, sample.Z, sample.Hours), other.RainCloudness(sample.X, sample.Z, sample.Hours));
        });
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
    public void BlowsAGaleInsideAGale()
    {
        var gale = Storms.First(storm => storm.Code == "gale" && storm.PeakMps >= 22);
        var hours = gale.BornHours + gale.LifetimeHours / 2;
        var (x, z) = gale.CentreAt(hours);

        var inside = Model.Wind(x + gale.Radius * 0.3, z, hours);
        var beyond = Model.Wind(x + gale.Radius * 3, z, hours);

        Assert.Equal("gale", inside.Storm?.Code);
        Assert.InRange(new BeaufortScale(Fixtures.Config().Beaufort).ForceAt(inside.Speed).Force, 8, 12);
        Assert.True(inside.Speed > beyond.Speed, $"{inside.Speed:F1} m/s inside, {beyond.Speed:F1} m/s beyond");
    }

    [Fact]
    public void BlowsBrieflyWhereASquallHasJustArrived()
    {
        var squall = Storms.First(storm => storm.Code == "squall");
        var hours = squall.BornHours + squall.LifetimeHours / 2;
        var (x, z) = squall.CentreAt(hours);
        var speed = Math.Sqrt(squall.VelocityX * squall.VelocityX + squall.VelocityZ * squall.VelocityZ);
        var (aheadX, aheadZ) = (x + squall.VelocityX / speed * squall.Radius * 0.3, z + squall.VelocityZ / speed * squall.Radius * 0.3);

        Assert.True(Model.BlowingHours(aheadX, aheadZ, hours, Model.Wind(aheadX, aheadZ, hours).Speed) < 2);
    }

    [Fact]
    public void BlowsLongWhereOnlyTheBackgroundWindBlows()
    {
        var calm = Samples.Where(sample => Model.Wind(sample.X, sample.Z, sample.Hours).Presence == 0).ToArray();

        Assert.True(calm.Average(sample => Model.BlowingHours(sample.X, sample.Z, sample.Hours, Model.Wind(sample.X, sample.Z, sample.Hours).Speed)) > 4);
    }

    [Fact]
    public void BlowsHarderOverTheOpenSeaThanOverLand()
    {
        var basins = Fixtures.Basins(seed: 42);
        var calm = Samples
            .SelectMany(sample => Enumerable.Range(0, 30).Select(i => (X: sample.X + i * 211, sample.Z, sample.Hours)))
            .Select(at => (basin: basins.At(at.X, at.Z), wind: Model.Wind(at.X, at.Z, at.Hours)))
            .Where(at => at.wind.Presence == 0)
            .ToArray();
        var land = calm.Where(at => !at.basin.IsSea).Select(at => at.wind.Speed).ToArray();
        var openSea = calm.Where(at => at.basin.ShoreBlocks >= Fixtures.Config().Background.SeaRampBlocks).Select(at => at.wind.Speed).ToArray();

        Assert.NotEmpty(land);
        Assert.NotEmpty(openSea);
        Assert.InRange(land.Average(), 2, 6);
        Assert.InRange(openSea.Average(), 5, 9);
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
    public void BrewsHurricanesOnlyOverTheSea()
    {
        var basins = Fixtures.Basins(seed: 42);
        var hurricanes = Storms.Where(storm => storm.Code == "hurricane").ToArray();
        double CellCentre(double origin, int cellSize) => (Math.Floor(origin / cellSize) + 0.5) * cellSize;

        Assert.NotEmpty(hurricanes);
        Assert.All(hurricanes, hurricane => Assert.True(basins.IsSea(
            CellCentre(hurricane.OriginX, hurricane.Type.CellSize), CellCentre(hurricane.OriginZ, hurricane.Type.CellSize))));
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
