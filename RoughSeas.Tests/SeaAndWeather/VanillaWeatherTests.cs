using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class VanillaWeatherTests
{
    private const double RainCloudBrightness = 0.55;

    private static readonly VanillaConfig Vanilla = Fixtures.Config().Vanilla;
    private static readonly SeaWeatherModel Model = Fixtures.Model(seed: 42);
    private static readonly Storm Squall = Fixtures.YearOfStorms(Model).First(storm => storm.Code == "squall");

    private static double CloudBrightnessAt(double radii)
    {
        var hours = Squall.BornHours + Squall.LifetimeHours / 2;
        var (x, z) = Squall.CentreAt(hours);
        var presence = Model.StormsAt(x + Squall.Radius * radii, z, hours).Presence;
        return VanillaWeather.CloudBrightness(RainCloudBrightness, presence, Vanilla);
    }

    [Fact]
    public void LeavesAShowersCloudAsVanillaDrawsIt()
    {
        Assert.Equal(RainCloudBrightness, VanillaWeather.CloudBrightness(RainCloudBrightness, stormPresence: 0, Vanilla));
    }

    [Fact]
    public void DarkensAStormsCloudsBeyondVanillasRainCloud()
    {
        Assert.True(CloudBrightnessAt(0.2) < RainCloudBrightness - 0.2, $"brightness {CloudBrightnessAt(0.2)} near the centre");
    }

    [Fact]
    public void DarkensAStormsCloudsFromItsRimInward()
    {
        Assert.True(CloudBrightnessAt(0.2) < CloudBrightnessAt(0.6), $"{CloudBrightnessAt(0.2)} near the centre, {CloudBrightnessAt(0.6)} out");
        Assert.True(CloudBrightnessAt(0.6) < CloudBrightnessAt(0.95), $"{CloudBrightnessAt(0.6)} out, {CloudBrightnessAt(0.95)} at the rim");
    }
}
