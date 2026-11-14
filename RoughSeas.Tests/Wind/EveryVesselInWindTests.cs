using Newtonsoft.Json.Linq;
using RoughSeas.Storms;
using RoughSeas.Tests.Storms;
using RoughSeas.Wind;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Wind;

public class EveryVesselInWindTests
{
    private const double Breeze = 9;
    private static readonly string[] Sides = ["client", "server"];
    private static readonly string[] PatchFiles = ["storms-vessels.json", "boats-drift.json", "wind-vessels.json", "compat-shipwright.json"];

    private static readonly string[] AtSea = [EntityBehaviorSeaForces.Code, EntityBehaviorCurrentDrift.Code, EntityBehaviorWindage.Code];

    private sealed record Vessel(string Name, Dictionary<string, HashSet<string>> Behaviours, JToken? Windage);

    private static readonly Lazy<Vessel[]> All = new(() => [.. Ours(), .. Patched()]);

    public static TheoryData<string> Vessels() => [.. All.Value.Select(vessel => vessel.Name)];

    private static IEnumerable<Vessel> Ours() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "vessel"), "*.json").Select(file =>
        {
            var entity = JObject.Parse(File.ReadAllText(file));
            var behaviours = Sides.ToDictionary(side => side, side => entity[side]!["behaviors"]!.Select(b => b.Value<string>("code")!).ToHashSet());
            return new Vessel(Path.GetFileNameWithoutExtension(file), behaviours, entity["behaviorConfigs"]![EntityBehaviorWindage.Code]);
        });

    private static IEnumerable<Vessel> Patched()
    {
        var ops = PatchFiles.SelectMany(file => JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file)))).ToList();
        return ops.Where(op => op.Value<string>("path")!.EndsWith("/behaviors/-") && AtSea.Contains(op["value"]!.Value<string>("code")))
            .Select(op => op.Value<string>("file")!).Distinct()
            .Select(file => new Vessel(file,
                Sides.ToDictionary(side => side, side => ops.Where(op => op.Value<string>("file") == file && op.Value<string>("path") == $"/{side}/behaviors/-")
                    .Select(op => op["value"]!.Value<string>("code")!).ToHashSet()),
                ops.FirstOrDefault(op => op.Value<string>("file") == file && op.Value<string>("path") == $"/behaviorConfigs/{EntityBehaviorWindage.Code}")?["value"]));
    }

    private static Vessel Named(string name) => All.Value.Single(vessel => vessel.Name == name);

    [Theory]
    [MemberData(nameof(Vessels))]
    public void TheWavesTheCurrentAndTheWindAllMoveHer(string name)
    {
        var vessel = Named(name);
        foreach (var side in Sides)
            Assert.All(AtSea, code => Assert.Contains(code, vessel.Behaviours[side]));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void AWindOnHerBeamPushesHerToLeeward(string name)
    {
        var windage = Named(name).Windage;
        Assert.NotNull(windage);
        Assert.True(windage!.Value<double>("windageSquareMetres") > 0, "her side above the water is measured");
        Assert.True(windage.Value<double>("frontSquareMetres") > 0, "her front above the water is measured");

        var load = VesselInWind.From(new JsonObject(windage), Fixtures.Wind).Steady((Breeze, 0), (0, 0), 0, 0);

        Assert.True(load.X > 0, $"to leeward {load.X:F0} N");
    }
}
