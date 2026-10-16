using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class SeaWeatherModelTests
{
    private static readonly (double X, double Z, double Hours)[] Samples =
        Enumerable.Range(0, 40).Select(i => (500000.0 + i * 1777, 512000.0 - i * 913, 2000.0 + i * 7.3)).ToArray();

    [Fact]
    public void ComputesTheSameSeaFromTheSameSeed()
    {
        var server = Fixtures.Model(seed: 42);
        var client = Fixtures.Model(seed: 42);

        Assert.All(Samples, sample => Assert.Equal(
            server.At(sample.X, sample.Z, sample.Hours, oceanity: 1),
            client.At(sample.X, sample.Z, sample.Hours, oceanity: 1)));
    }

    [Fact]
    public void ComputesAnotherSeaFromAnotherSeed()
    {
        var one = Fixtures.Model(seed: 42);
        var other = Fixtures.Model(seed: 43);

        Assert.Contains(Samples, sample => one.Wind(sample.X, sample.Z, sample.Hours) != other.Wind(sample.X, sample.Z, sample.Hours));
    }

    [Fact]
    public void BrewsStormsSomewhereInAYear()
    {
        var model = Fixtures.Model(seed: 42);

        var storms = Enumerable.Range(0, 12)
            .SelectMany(month => model.Storms.Active(512000, 512000, month * Fixtures.HoursPerYear / 12, range: 20000))
            .ToArray();

        Assert.NotEmpty(storms);
    }

    [Fact]
    public void BlowsFromTheSouthWestInTheNorthernWesterlies()
    {
        var model = Fixtures.Model(seed: 42);

        var calm = Samples
            .Select(sample => model.Wind(sample.X, sample.Z, sample.Hours))
            .Where(wind => wind.Presence == 0)
            .Select(wind => PrevailingWinds.FromDegrees(wind.X, wind.Z))
            .ToArray();

        Assert.NotEmpty(calm);
        Assert.All(calm, from => Assert.InRange(from, 225 - 60, 225 + 60));
    }

    [Fact]
    public void RaisesNoWavesOnLand()
    {
        var state = Fixtures.Model(seed: 42).At(512000, 512000, 2000, oceanity: 0);

        Assert.Equal(0, state.WaveHeight);
    }
}
