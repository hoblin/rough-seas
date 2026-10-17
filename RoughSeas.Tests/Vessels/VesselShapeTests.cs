using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class VesselShapeTests
{
    private static readonly string[] SailStates = ["SailUnfurled", "SailHalf", "SailFurled"];

    public static TheoryData<string> Vessels() =>
        new(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "vessel"), "*.json").Select(Path.GetFileNameWithoutExtension)!);

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
    public void DrawsEveryTextureItsCracksOverlay(string vessel)
    {
        var (entity, _, shape) = Load(vessel);
        var cracked = entity["behaviorConfigs"]![EntityBehaviorVesselIntegrity.Code]!["cracks"]!["textures"]!.Values<string>();

        Assert.Empty(cracked.Except(FaceTextures(shape)));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void MapsEveryTextureItsShapeDrawsOnTheEntityAndTheItem(string vessel)
    {
        var (entity, item, shape) = Load(vessel);
        var drawn = FaceTextures(shape).Where(code => code != "null").Distinct().ToList();

        Assert.Empty(drawn.Except(TextureCodes(entity["client"]!["textures"]!)));
        Assert.Empty(drawn.Except(TextureCodes(item["textures"]!)));
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

    private static (JToken Entity, JToken Item, JToken Shape) Load(string vessel)
    {
        var entity = Read("vessel", vessel);
        var name = entity["client"]!["shape"]!.Value<string>("base")!.Split('/').Last();
        return (entity, Read("items", vessel), Read("shapes", name));
    }

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
}
