using Newtonsoft.Json;
using RoughSeas.Compat.ExtremeWeather;

namespace RoughSeas.Tests.Compat;

public class ExtremeWeatherCompatTests
{
    private static readonly CompatConfig Shipped =
        JsonConvert.DeserializeObject<CompatConfig>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "extremeweather.json")))!;

    private static readonly TestedVersions Range = new() { From = "1.8.5", To = "1.9.2" };

    [Theory]
    [InlineData("1.8.5")]
    [InlineData("1.9.0")]
    [InlineData("1.9.2")]
    public void CoversTheTestedRange(string version) => Assert.True(Range.Covers(version));

    [Theory]
    [InlineData("1.8.4")]
    [InlineData("1.9.3")]
    [InlineData("2.0.0")]
    [InlineData("not a version")]
    public void LeavesOtherVersionsAlone(string version) => Assert.False(Range.Covers(version));

    [Fact]
    public void ShipsTestedAgainstOnePinnedRelease() => Assert.True(Shipped.Tested.Covers("1.8.5"));

    [Fact]
    public void SwitchesOffTheRollsOfStormsWeBring()
    {
        Assert.Equal(0L, Shipped.Settings["HurricaneEventChance"]);
        Assert.Equal(0L, Shipped.Settings["MonsoonEventChance"]);
        Assert.Equal(0L, Shipped.Settings["BlizzardEventChance"]);
    }

    [Fact]
    public void LeavesEwItsOwnSandstormsAndHeat()
    {
        Assert.DoesNotContain("SandstormEventChance", Shipped.Settings.Keys);
        Assert.DoesNotContain("HeatwaveEventChance", Shipped.Settings.Keys);
        Assert.DoesNotContain("ColdsnapEventChance", Shipped.Settings.Keys);
        Assert.DoesNotContain("HurricanesEnabled", Shipped.Settings.Keys);
    }
}
