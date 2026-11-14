using Newtonsoft.Json.Linq;
using RoughSeas.Compat.Shipwright;

namespace RoughSeas.Tests.Compat;

public class ShipwrightCompatTests
{
    private static readonly JArray Patches = JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "compat-shipwright.json")));

    public static TheoryData<int> Operations() => [.. Enumerable.Range(0, Patches.Count)];

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryPatchWaitsForShipwright(int at) =>
        Assert.Equal("shipwright", Patches[at]["dependsOn"]?[0]?["modid"]?.Value<string>());

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryPatchWaitsForATestedShipwright(int at)
    {
        Assert.Equal(ShipwrightCompat.Condition, Patches[at]["condition"]?["when"]?.Value<string>());
        Assert.Equal("true", Patches[at]["condition"]?["isValue"]?.Value<string>());
    }

    [Fact]
    public void EveryBoatGetsItsWeightFromARealCraft()
    {
        foreach (var integrity in Patches.Where(op => op["path"]!.Value<string>() == "/behaviorConfigs/roughseas:vesselintegrity"))
        {
            Assert.False(string.IsNullOrEmpty(integrity["value"]!["sources"]!["weight"]?.Value<string>()), integrity["file"]!.Value<string>());
        }
    }
}
