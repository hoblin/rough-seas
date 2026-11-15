using Newtonsoft.Json.Linq;
using RoughSeas.Compat.Shipwright;
using RoughSeas.Tests.Storms;

namespace RoughSeas.Tests.Compat;

public class ShipwrightCompatTests
{
    private static readonly JArray Patches = JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "compat-shipwright.json")));

    public static TheoryData<int> Operations() => [.. Enumerable.Range(0, Patches.Count)];

    public static TheoryData<string> Boats() =>
    [
        .. Patches.Where(op => op["path"]!.Value<string>() == "/weight")
            .Select(op => Path.GetFileNameWithoutExtension(op["file"]!.Value<string>()!))
    ];

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryPatchWaitsForShipwright(int at) =>
        Assert.Equal("shipwright", Patches[at]["dependsOn"]?[0]?["modid"]?.Value<string>());

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryPatchWaitsForAShipwrightWeReach(int at)
    {
        Assert.Equal(ShipwrightCompat.Condition, Patches[at]["condition"]?["when"]?.Value<string>());
        Assert.Equal("true", Patches[at]["condition"]?["isValue"]?.Value<string>());
    }

    [Fact]
    public void EveryBoatsWeightCarriesASource()
    {
        foreach (var integrity in Patches.Where(op => op["path"]!.Value<string>() == "/behaviorConfigs/roughseas:vesselintegrity"))
        {
            Assert.False(string.IsNullOrEmpty(integrity["value"]!["sources"]!["weight"]?.Value<string>()), integrity["file"]!.Value<string>());
        }
    }

    [Theory]
    [MemberData(nameof(Boats))]
    public void EveryBoatFloatsWithHerRiderAboard(string boat)
    {
        var hull = Fixtures.ShipwrightHull(boat);
        var afloat = hull.Float(hull.Crewed());

        Assert.False(afloat.Sunk);
        Assert.True(afloat.FreeboardMetres > 0);
    }
}
