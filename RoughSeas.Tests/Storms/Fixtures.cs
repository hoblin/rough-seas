using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Person;
using RoughSeas.Propulsion;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Vessels;
using RoughSeas.Water;
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

    private static readonly Lazy<PersonConfig> personConfig = new(() => JsonConvert.DeserializeObject<PersonConfig>(Shipped("person.json"))!);

    private static readonly Lazy<VesselMass> mass = new(() =>
    {
        var read = JsonConvert.DeserializeObject<VesselMass>(Shipped("mass.json"))!;
        read.Person = Person;
        return read;
    });

    private static readonly Lazy<HullConfig> hullConfig = new(() =>
    {
        var config = JsonConvert.DeserializeObject<HullConfig>(Shipped("hull.json"))!;
        config.Hides = JsonConvert.DeserializeObject<HidesConfig>(Shipped("hides.json"))!;
        return config;
    });

    public static StormsConfig Config() => config.Value;

    public static VesselMass Mass => mass.Value;

    public static PersonConfig PersonConfig => personConfig.Value;

    public static PlayerPerson Person => new(Body.Of(PersonConfig.Body), PersonConfig.Carried);

    public static HullConfig HullConfig => hullConfig.Value;

    public static double FlaxNewtons => materials.Value["rope-flax"].Strength!.Value * 1000;

    public static double FlaxStretchAtBreak => new MaterialTable(materials.Value, new NullLogger()).StretchAtBreak("rope-flax");

    public static double SailboatBeamMetres => BeamMetres("boat-sailed");

    public static Seaworthiness Seaworthiness(string vessel) => RoughSeas.Storms.Seaworthiness.From(new JsonObject(SeaForces(vessel)), BeamMetres(vessel), 2 * Shape(vessel).HalfLengthMetres);

    public static double BeamMetres(string vessel) => (double?)SeaForces(vessel)["beamMetres"] ?? 2 * Shape(vessel).HalfBeamMetres;

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
        return new Hull(flotation, build.Kilograms, weight + Mass.CrewKg, capacity, new JsonObject(integrity), vessel);
    }

    /// <summary>One of Shipwright's boats, as <c>patches/compat-shipwright.json</c> makes her a vessel of ours.</summary>
    public static Hull ShipwrightHull(string boat, string wood = "oak")
    {
        JToken Op(string path) => JArray.Parse(Shipped("compat-shipwright.json"))
            .Single(patch => (string?)patch["file"] == $"shipwright:entities/nonliving/{boat}.json" && (string?)patch["path"] == path)["value"]!;
        var integrity = Op($"/behaviorConfigs/{EntityBehaviorVesselIntegrity.Code}");
        var parts = integrity["parts"]!.ToObject<VesselPart[]>()!;
        foreach (var part in parts) part.Material = part.Material.Replace("{material}", wood);
        var weight = (double)Op("/weight");
        var build = new MaterialTable(materials.Value, new NullLogger()).Build(parts, weight);
        var flotation = new Flotation((double)integrity["depthMetres"]!, (double?)integrity["waterplaneSquareMetres"] ?? 0, build.Litres);
        var bilge = JArray.Parse(Shipped("compat-shipwright.json"))
            .SingleOrDefault(patch => (string?)patch["file"] == $"shipwright:entities/nonliving/{boat}.json" && (string?)patch["path"] == "/behaviorConfigs/roughseas:bilge");
        return new Hull(flotation, build.Kilograms, weight + Mass.CrewKg, (double?)bilge?["value"]?["capacityLitres"] ?? 0, new JsonObject(integrity), boat);
    }

    /// <summary>A vessel as the drawn sea meets her, with one rider and <paramref name="litres"/> in her bilge.</summary>
    public static RiddenHull Ridden(string vessel, double litres = 0)
    {
        var hull = Hull(vessel);
        var aboard = hull.Crewed(litres: litres);
        return new RiddenHull(2 * Shape(vessel).HalfLengthMetres, BeamMetres(vessel), true, hull.Float(aboard, litres).DraftMetres,
            hull.Flotation.DepthMetres, aboard.Total, AddedKg(hull), hull.Flotation.KgPerMetre);
    }

    public static double AddedKg(Hull hull)
    {
        var shape = Shape(hull.Vessel);
        double squareMetres = hull.Flotation.HasHold ? hull.Flotation.KgPerMetre / 1025 : 4 * shape.HalfLengthMetres * shape.HalfBeamMetres;
        return AddedMass.OfWaterplane(squareMetres, shape.HalfBeamMetres, 1025);
    }

    public readonly record struct Train(DrawnSea Sea, SeaSurface Surface, double Length, double Number, double Frequency, double Stokes);

    /// <summary>One train along +X, a whole step of the surface's stretch long and <paramref name="height"/> high, settled at once.</summary>
    /// <param name="stretch">The step: (1 + surface.stretch) to this power, in blocks.</param>
    public static Train OneTrain(double height, int stretch)
    {
        var shipped = Config().Surface;
        var single = new SurfaceConfig
        {
            Trains = [new SurfaceTrainConfig { AngleDegrees = 0, LengthFactor = 1, Energy = 1 }],
            Steepness = shipped.Steepness,
            MinLengthBlocks = shipped.MinLengthBlocks,
            SettleSeconds = shipped.SettleSeconds,
            TurnDegrees = shipped.TurnDegrees,
            Stretch = shipped.Stretch,
            AimSeconds = shipped.AimSeconds,
            FullFoamMetres = shipped.FullFoamMetres,
            Whitecaps = shipped.Whitecaps,
        };
        var length = Math.Pow(1 + shipped.Stretch, stretch);
        var number = 2 * Math.PI / length;
        var frequency = Math.Sqrt(9.81 * number);
        var surface = new SeaSurface(single, new Waves(Config().Waves));
        var raised = new WaveGeneration(0, Math.Sqrt(2 * Math.PI * length / 9.81));
        surface.Aim(height, 6);
        surface.Raise(raised, raised, 0);
        surface.Advance(120);
        var field = new SurfaceField();
        field.Update(new EvenSea(height), X0, Z0, RegionSize, CellBlocks);
        var amplitude = height / (2 * Math.Sqrt(2));
        return new Train(new DrawnSea(surface, field), surface, length, number, frequency, amplitude * amplitude * frequency * number);
    }

    private const int RegionSize = 512;
    private const int CellBlocks = 32;
    private const double X0 = 512031.25;
    private const double Z0 = 498877.5;

    private sealed class EvenSea(double height) : ISeaSource
    {
        public SeaSample Sample(double x, double z) => new(0, 0, 0, 0, height, 0, 0, 0, 0);

        public (double X, double Z) Wind(double x, double z) => default;

        public double StormPresence(double x, double z) => 0;

        public SeaState At(double x, double z) => default;

        public SeaCurrent CurrentAt(double x, double z) => default;

        public float? RainCloudness(double x, double z, double totalDays) => 0;

        public double RegionMps(int regionX, int regionZ) => 0;

        public WaveGeneration Generation(double x, double z, long slot) => default;
    }

    public static DragConfig Drag => drag.Value;

    public static PropulsionConfig Propulsion => propulsion.Value;

    public static JToken PropulsionOf(string vessel) => vessel.StartsWith("boat-")
        ? BehaviorConfig("propulsion-vessels.json", vessel, EntityBehaviorPropulsion.Code)
        : Entity(vessel)["behaviorConfigs"]![EntityBehaviorPropulsion.Code]!;

    public static HullShape Shape(string vessel)
    {
        if (!vessel.StartsWith("boat-"))
        {
            var box = IntegrityConfig(vessel)["hullBoxMetres"]!;
            return HullShape.FromBox(box["from"]!.ToObject<double[]>()!, box["to"]!.ToObject<double[]>()!);
        }
        var hull = JArray.Parse(Shipped("vessels-hull-boxes.json")).Single(patch => (string?)patch["file"] == $"game:shapes/entity/nonliving/boat/{vessel[5..]}.json")["value"]!;
        return HullShape.FromVoxels(hull["from"]!.ToObject<double[]>()!, hull["to"]!.ToObject<double[]>()!);
    }

    public static HullDrag DragOf(string vessel, Aboard aboard)
    {
        var attributes = vessel == "boat-sailed" ? BehaviorConfig("boats-drift.json", vessel, EntityBehaviorCurrentDrift.Code) : PropulsionOf(vessel);
        var form = HullForm.From(new JsonObject(attributes), Drag, Shape(vessel));
        return new HullDrag(form, Hull(vessel).Float(aboard).DraftMetres, aboard.Total, WaterDensity, Drag);
    }

    public const double WaterDensity = VesselMass.SeawaterKgPerLitre * KilogramsPerCubicMetre;

    private const double KilogramsPerCubicMetre = 1000;

    public static double PaddleSquareMetres => (double)JToken.Parse(Shipped(Path.Combine("tools", "paddle.json")))["attributes"]![Tool.AttributeKey]!["bladeSquareMetres"]!;

    public static RowerStation Station(string vessel, double depthMetres, bool deepWaterRefuses = true) => Station(vessel, _ => depthMetres, deepWaterRefuses);

    public static RowerStation Station(string vessel, System.Func<double, double> depthAlong, bool deepWaterRefuses = true) =>
        new(new JsonObject(PropulsionOf(vessel)), Propulsion, WaterDensity, (along, _, _) => depthAlong(along), () => deepWaterRefuses);

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


public sealed record Hull(Flotation Flotation, double Kilograms, double ReferenceKg, double CapacityLitres, JsonObject Config, string Vessel)
{
    public Aboard Crewed(double bodies = 75, double kit = 0, double containers = 0, double litres = 0, double soaked = 0) =>
        new(Kilograms + soaked, bodies, kit, containers, litres * VesselMass.SeawaterKgPerLitre, 0);

    public Afloat Float(Aboard aboard, double litres = 0) => Flotation.Float(aboard, CapacityLitres > 0 && litres >= CapacityLitres);
}
