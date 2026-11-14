using Newtonsoft.Json;
using RoughSeas.Compat;
using RoughSeas.Compat.ExtremeWeather;

namespace RoughSeas.Tests.Compat;

public class ExtremeWeatherCompatTests
{
    private static readonly CompatConfig Shipped =
        JsonConvert.DeserializeObject<CompatConfig>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "extremeweather.json")))!;

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
