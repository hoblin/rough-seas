using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Storms;

public static class Fixtures
{
    private static readonly Lazy<StormsConfig> config = new(() => JsonConvert.DeserializeObject<StormsConfig>(Shipped("storms.json"))!);

    private static readonly Lazy<Dictionary<string, Material>> materials =
        new(() => JsonConvert.DeserializeObject<Dictionary<string, Material>>(Shipped("materials.json"))!);

    public static StormsConfig Config() => config.Value;

    public static double FlaxNewtons => materials.Value["rope-flax"].Strength * 1000;

    public static Vessel Raft => Vessel("boat-raft");

    public static Vessel Sailboat => Vessel("boat-sailed");

    public static Seaworthiness Seaworthiness(string vessel) => RoughSeas.Storms.Seaworthiness.From(new JsonObject(SeaForces(vessel)));

    private static JToken SeaForces(string vessel) => vessel.StartsWith("boat-")
        ? BehaviorConfig("storms-vessels.json", vessel, "roughseas:seaforces")
        : JToken.Parse(Shipped(Path.Combine("vessel", vessel + ".json")))["behaviorConfigs"]!["roughseas:seaforces"]!;

    private static Vessel Vessel(string entity)
    {
        var integrity = BehaviorConfig("vessels-vanilla-boats.json", entity, "vesselintegrity");
        var forces = BehaviorConfig("storms-vessels.json", entity, "roughseas:seaforces");
        return new Vessel((double)integrity["impactCapacity"]!, (double)forces["beamMetres"]!, (double)forces["windageSquareMetres"]!);
    }

    private static JToken BehaviorConfig(string patches, string entity, string behavior) =>
        JArray.Parse(Shipped(patches))
            .Single(patch => (string?)patch["file"] == $"game:entities/nonliving/{entity}.json" && (string?)patch["path"] == $"/behaviorConfigs/{behavior}")
            ["value"]!;

    private static string Shipped(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));
}

public readonly record struct Vessel(double ImpactCapacity, double BeamMetres, double WindageSquareMetres);
