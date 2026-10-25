using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Vessels;

public class VesselShapeTests
{
    private static readonly string[] SailStates = ["SailUnfurled", "SailHalf", "SailFurled"];
    private static readonly string[] Strokes = ["ready", "forwards", "backwards"];

    public static TheoryData<string> Vessels() => new(VesselNames());

    private static IEnumerable<string> VesselNames() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "vessel"), "*.json").Select(Path.GetFileNameWithoutExtension)!;

    [Theory]
    [MemberData(nameof(Vessels))]
    public void HasEverySeatAndSelectionBoxItsEntityNames(string vessel)
    {
        var (entity, _, shape) = Load(vessel);
        var behaviors = entity["behaviorConfigs"]!;
        var named = behaviors["selectionboxes"]!["selectionBoxes"]!.Values<string>()
            .Concat(behaviors["creaturecarrier"]!["seats"]!.Select(seat => seat.Value<string>("apName")));

        Assert.Empty(named.Except(AttachmentPoints(shape)));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void GivesEverySelectionBoxTheSlotAtItsIndex(string vessel)
    {
        var behaviors = Load(vessel).Entity["behaviorConfigs"]!;
        var boxes = behaviors["selectionboxes"]!["selectionBoxes"]!.Values<string>().ToList();
        var slots = behaviors["rideableaccessories"]?["wearableSlots"]?.Select(slot => slot.Value<string>("attachmentPointCode") ?? slot.Value<string>("code")).ToList();
        if (slots == null) return;

        Assert.Equal(boxes, slots);
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void KeepsASeatThatSteersFreeOfCargo(string vessel)
    {
        var behaviors = Load(vessel).Entity["behaviorConfigs"]!;
        var loadable = behaviors["rideableaccessories"]?["wearableSlots"]?
            .Where(slot => slot["forCategoryCodes"]!.Any())
            .Select(slot => slot.Value<string>("attachmentPointCode")) ?? [];

        Assert.Contains(behaviors["creaturecarrier"]!["seats"]!, seat =>
            seat.Value<bool>("controllable") && !loadable.Contains(seat.Value<string>("apName")));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void DrawsEveryTextureItsDamageMarks(string vessel)
    {
        var (entity, _, shape) = Load(vessel);
        var marked = Damage(entity).Textures;

        Assert.Empty(marked.Except(FaceTextures(shape)));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void HasAnOverlayOfItsMaterialForEveryStageOfDamage(string vessel)
    {
        var damage = Damage(Load(vessel).Entity);

        Assert.All(Enumerable.Range(1, 4), stage =>
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "damage", Path.GetFileName(damage.Overlay(stage).Path) + ".png"))));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void MapsEveryTextureItsShapeDrawsOnTheEntityAndTheItem(string vessel)
    {
        var (entity, item, shape) = Load(vessel);
        var drawn = FaceTextures(shape).Where(code => code != "null").Distinct().ToList();

        Assert.Empty(drawn.Except(TextureCodes(entity["client"]!["textures"]!).Concat(LogTextureCodes(entity))));
        Assert.Empty(drawn.Except(TextureCodes(item["textures"]!).Concat(LogTextureCodes(item))));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void TakesTheSameTexturesFromItsWoodOnTheEntityAndTheItem(string vessel)
    {
        var (entity, item, _) = Load(vessel);

        Assert.Equal(entity["attributes"]?["logTextures"]?.ToString(), item["attributes"]?["logTextures"]?.ToString());
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void MasksTheSeaWithARootPlaneThatEntityAndItemLeaveUndrawn(string vessel)
    {
        var (entity, item, shape) = Load(vessel);
        var client = entity["client"]!;
        var mask = client["behaviors"]!.FirstOrDefault(behavior => behavior.Value<string>("code") == "hidewatersurface");
        if (mask == null) return;

        Assert.Equal("hideWater/*", mask.Value<string>("hideWaterElement"));
        Assert.Contains(shape["elements"]!, element => element.Value<string>("name") == "hideWater" && element["children"]!.Any());
        Assert.Contains("hideWater", client["shape"]!["ignoreElements"]!.Values<string>());
        Assert.Contains("hideWater", item["shape"]!["ignoreElements"]!.Values<string>());
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void NamesEverySailStateVanillaSwitchesBetween(string vessel)
    {
        var (entity, _, shape) = Load(vessel);
        if (entity["attributes"]!.Value<bool?>("unfurlSails") != true) return;

        Assert.Empty(SailStates.Except(Elements(shape["elements"]!).Select(element => element.Value<string>("name"))));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void MovesItsOwnOarsOrPaddleWithEveryStrokeOfItsRower(string vessel)
    {
        var (entity, _, shape) = Load(vessel);
        var attributes = entity["attributes"]!;
        if (attributes.Value<bool?>("requiresPaddlingTool") == true) return;

        var own = entity["client"]!["animations"]!.ToDictionary(animation => animation.Value<string>("code")!);
        var animated = shape["animations"]!.ToDictionary(animation => animation.Value<string>("code")!);
        var rower = PlayerPatches().ToList();

        Assert.All(Strokes.Select(stroke => attributes["mountAnimations"]!.Value<string>(stroke)!), code =>
        {
            var registered = rower.Single(op => op.Value<string>("path") == "/client/animations/-" && op["value"]!.Value<string>("code") == code)["value"]!;
            var played = rower.Single(op => op.Value<string>("path") == "/animations/-" && op["value"]!.Value<string>("code") == code)["value"]!;
            Assert.Equal(registered.Value<float>("animationSpeed"), own[code].Value<float>("animationSpeed"));
            Assert.Equal(played.Value<int>("quantityframes"), animated[code].Value<int>("quantityframes"));
        });
    }

    [Fact]
    public void KeysEveryAxisOfWhatEachStrokeMoves()
    {
        var animations = VesselNames().SelectMany(vessel => Load(vessel).Shape["animations"] ?? new JArray())
            .Concat(PlayerPatches().Where(op => op.Value<string>("path") == "/animations/-").Select(op => op["value"]!));

        Assert.All(animations.SelectMany(animation => animation["keyframes"]!).SelectMany(keyframe => keyframe["elements"]!.Children<JProperty>()),
            element => Assert.All(new[] { "offset", "rotation" }, channel =>
            {
                var keyed = new[] { "X", "Y", "Z" }.Count(axis => element.Value[channel + axis] != null);
                Assert.True(keyed is 0 or 3, $"{element.Name} keys {keyed} of the three {channel} axes");
            }));
    }

    private static IEnumerable<JToken> PlayerPatches() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "patches"), "player-*.json")
            .SelectMany(file => JArray.Parse(File.ReadAllText(file)));

    [Theory]
    [MemberData(nameof(Vessels))]
    public void MarksEveryPlaceOfItsHullOnTheModel(string vessel)
    {
        var (entity, _, shape) = Load(vessel);
        var elements = shape.ToObject<Shape>()!.Elements;
        foreach (var element in elements) element.TrimTextureNamesAndResolveFaces();
        var integrity = entity["behaviorConfigs"]![EntityBehaviorVesselIntegrity.Code]!;
        var hull = Elements(shape["elements"]!).Single(element => element.Value<string>("name") == "Hull");
        var box = integrity["hullBox"]?.ToObject<double[]>()
            ?? hull["from"]!.ToObject<double[]>()!.Concat(hull["to"]!.ToObject<double[]>()!).ToArray();

        var marked = HullDamage.Places(elements, HullShape.FromVoxels(box[..3], box[3..]), Damage(entity).Textures)
            .Select(placed => placed.Place).Distinct();

        Assert.Equal(HullShape.Places.Order(), marked.Order());
    }

    private static (JToken Entity, JToken Item, JToken Shape) Load(string vessel)
    {
        var entity = Read("vessel", vessel);
        var name = entity["client"]!["shape"]!.Value<string>("base")!.Split('/').Last();
        return (entity, Read("items", vessel), Read("shapes", name));
    }

    private static HullDamageConfig Damage(JToken entity) =>
        entity["behaviorConfigs"]![EntityBehaviorVesselIntegrity.Code]!["damage"]!.ToObject<HullDamageConfig>()!;

    private static JToken Read(string folder, string name) =>
        JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, folder, name + ".json")));

    private static IEnumerable<JToken> Elements(JToken elements) =>
        elements.SelectMany(element => element["children"] is { } children ? Elements(children).Prepend(element) : [element]);

    private static IEnumerable<string> AttachmentPoints(JToken shape) =>
        Elements(shape["elements"]!).SelectMany(element => element["attachmentpoints"]?.Select(point => point.Value<string>("code")!) ?? []);

    private static IEnumerable<string> FaceTextures(JToken shape) =>
        Elements(shape["elements"]!).SelectMany(element => element["faces"]!.Children<JProperty>())
            .Select(face => face.Value["texture"]!.Value<string>()!.TrimStart('#'));

    private static IEnumerable<string> TextureCodes(JToken textures) => textures.Children<JProperty>().Select(texture => texture.Name);

    private static IEnumerable<string> LogTextureCodes(JToken type) => type["attributes"]?["logTextures"] is { } faces ? TextureCodes(faces) : [];
}
