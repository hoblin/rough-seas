using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Propulsion;
using RoughSeas.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Storms;

public static class Fixtures
{
    private static readonly Dictionary<string, double> VanillaWeights = new() { ["boat-raft"] = 120, ["boat-sailed"] = 1900 };

    private static readonly Lazy<StormsConfig> config = new(() => JsonConvert.DeserializeObject<StormsConfig>(Shipped("storms.json"))!);

    private static readonly Lazy<Dictionary<string, Material>> materials =
        new(() => JsonConvert.DeserializeObject<Dictionary<string, Material>>(Shipped("materials.json"))!);

    private static readonly Lazy<DragConfig> drag = new(() => JsonConvert.DeserializeObject<DragConfig>(Shipped("drag.json"))!);

    private static readonly Lazy<PropulsionConfig> propulsion = new(() => JsonConvert.DeserializeObject<PropulsionConfig>(Shipped("propulsion.json"))!);

    private static readonly Lazy<VesselMass> mass =new(() => JsonConvert.DeserializeObject<VesselMass>(Shipped("mass.json"))!);

    private static readonly Lazy<HullConfig> hullConfig = new(() =>
    {
        var config = JsonConvert.DeserializeObject<HullConfig>(Shipped("hull.json"))!;
        config.Hides = JsonConvert.DeserializeObject<HidesConfig>(Shipped("hides.json"))!;
        return config;
    });

    public static StormsConfig Config() => config.Value;

    public static VesselMass Mass => mass.Value;

    public static HullConfig HullConfig => hullConfig.Value;

    public static double FlaxNewtons => materials.Value["rope-flax"].Strength!.Value * 1000;

    public static Vessel Raft => Vessel("boat-raft");

    public static Vessel Sailboat => Vessel("boat-sailed");

    public static Seaworthiness Seaworthiness(string vessel) => RoughSeas.Storms.Seaworthiness.From(new JsonObject(SeaForces(vessel)));

    public static Seaworthiness Afloat(string vessel, double litres = 0)
    {
        var hull = Hull(vessel);
        var fill = hull.CapacityLitres > 0 ? litres / hull.CapacityLitres : 0;
        return Seaworthiness(vessel).Laden(fill, Config().Swamping).Floating(hull.Float(hull.Crewed(litres: litres), litres).FreeboardMetres);
    }

    public static Hull Hull(string vessel, string wood = "oak")
    {
        var integrity = IntegrityConfig(vessel);
        var parts = integrity["parts"]?.ToObject<VesselPart[]>() ?? [];
        foreach (var part in parts) part.Material = part.Material.Replace("{wood}", wood).Replace("{material}", wood);
        double weight = VanillaWeights.TryGetValue(vessel, out var vanilla) ? vanilla : (double)Entity(vessel)["weight"]!;
        var build = new MaterialTable(materials.Value, new NullLogger()).Build(parts, weight);
        var flotation = new Flotation((double)integrity["depthMetres"]!, (double?)integrity["waterplaneSquareMetres"] ?? 0, build.Litres);
        double capacity = vessel.StartsWith("boat-")
            ? (double?)OptionalBehaviorConfig("storms-vessels.json", vessel, "roughseas:bilge")?["capacityLitres"] ?? 0
            : (double?)Entity(vessel)["behaviorConfigs"]!["roughseas:bilge"]?["capacityLitres"] ?? 0;
        return new Hull(flotation, build.Kilograms, weight + Mass.CrewKg, capacity, new JsonObject(integrity));
    }

    public static DragConfig Drag => drag.Value;

    public static PropulsionConfig Propulsion => propulsion.Value;

    public static JToken PropulsionOf(string vessel) => vessel.StartsWith("boat-")
        ? BehaviorConfig("propulsion-vessels.json", vessel, EntityBehaviorPropulsion.Code)
        : Entity(vessel)["behaviorConfigs"]![EntityBehaviorPropulsion.Code]!;

    public static HullShape Shape(string vessel)
    {
        var hull = vessel.StartsWith("boat-")
            ? JArray.Parse(Shipped("vessels-hull-boxes.json")).Single(patch => (string?)patch["file"] == $"game:shapes/entity/nonliving/boat/{vessel[5..]}.json")["value"]!
            : Elements(Shipped(Path.Combine("shapes", (string)Entity(vessel)["client"]!["shape"]!["base"]!.ToString().Split('/').Last() + ".json")))
                .Single(element => element["attachmentpoints"]?.Any(point => (string?)point["code"] == "HullAP") == true);
        return HullShape.FromVoxels(hull["from"]!.ToObject<double[]>()!, hull["to"]!.ToObject<double[]>()!);
    }

    public static HullDrag DragOf(string vessel, Aboard aboard)
    {
        var form = HullForm.From(new JsonObject(PropulsionOf(vessel)), Drag, Shape(vessel));
        return new HullDrag(form, Hull(vessel).Float(aboard).DraftMetres, aboard.Total, WaterDensity, Drag);
    }

    public const double WaterDensity = VesselMass.SeawaterKgPerLitre * 1000;

    private static IEnumerable<JToken> Elements(string shape)
    {
        static IEnumerable<JToken> Descend(JToken? elements) =>
            elements?.SelectMany(element => Descend(element["children"]).Prepend(element)) ?? [];
        return Descend(JToken.Parse(shape)["elements"]);
    }

    public static Resistance Resistance(string vessel, string wood = "oak")
    {
        var shell = IntegrityConfig(vessel)["shell"]!.ToString().Replace("{wood}", wood).Replace("{material}", wood);
        return Shell.Of(new JsonObject(JToken.Parse(shell)))!.Resists(0, new MaterialTable(materials.Value, new NullLogger()), HullConfig, 0);
    }

    public static JToken IntegrityConfig(string vessel) => vessel.StartsWith("boat-")
        ? BehaviorConfig("vessels-vanilla-boats.json", vessel, EntityBehaviorVesselIntegrity.Code)
        : Entity(vessel)["behaviorConfigs"]![EntityBehaviorVesselIntegrity.Code]!;

    private static JToken SeaForces(string vessel) => vessel.StartsWith("boat-")
        ? BehaviorConfig("storms-vessels.json", vessel, "roughseas:seaforces")
        : Entity(vessel)["behaviorConfigs"]!["roughseas:seaforces"]!;

    private static JToken Entity(string vessel) => JToken.Parse(Shipped(Path.Combine("vessel", vessel + ".json")));

    private static Vessel Vessel(string entity)
    {
        var integrity = BehaviorConfig("vessels-vanilla-boats.json", entity, EntityBehaviorVesselIntegrity.Code);
        var forces = BehaviorConfig("storms-vessels.json", entity, "roughseas:seaforces");
        return new Vessel((double)integrity["impactCapacity"]!, (double)forces["beamMetres"]!, (double)forces["windageSquareMetres"]!);
    }

    private static JToken BehaviorConfig(string patches, string entity, string behavior) =>
        OptionalBehaviorConfig(patches, entity, behavior)!;

    private static JToken? OptionalBehaviorConfig(string patches, string entity, string behavior) =>
        JArray.Parse(Shipped(patches))
            .SingleOrDefault(patch => (string?)patch["file"] == $"game:entities/nonliving/{entity}.json" && (string?)patch["path"] == $"/behaviorConfigs/{behavior}")
            ?["value"];

    private static string Shipped(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));
}

public class NullLogger : LoggerBase
{
    protected override void LogImpl(EnumLogType logType, string format, params object[] args) { }
}

public readonly record struct Vessel(double ImpactCapacity, double BeamMetres, double WindageSquareMetres);

public sealed record Hull(Flotation Flotation, double Kilograms, double ReferenceKg, double CapacityLitres, JsonObject Config)
{
    public Aboard Crewed(double bodies = 65, double kit = 10, double containers = 0, double litres = 0, double soaked = 0) =>
        new(Kilograms + soaked, bodies, kit, containers, litres * VesselMass.SeawaterKgPerLitre, 0);

    public Afloat Float(Aboard aboard, double litres = 0) => Flotation.Float(aboard, CapacityLitres > 0 && litres >= CapacityLitres);
}
