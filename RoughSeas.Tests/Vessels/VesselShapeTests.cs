using Newtonsoft.Json.Linq;

namespace RoughSeas.Tests.Vessels;

public class VesselShapeTests
{
    public static TheoryData<string> Vessels() =>
        new(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "vessel"), "*.json").Select(Path.GetFileNameWithoutExtension)!);

    [Theory]
    [MemberData(nameof(Vessels))]
    public void HasEveryAttachmentPointItsEntityNames(string vessel)
    {
        var (entity, shape) = Load(vessel);
        var behaviors = entity["behaviorConfigs"]!;
        var named = behaviors["selectionboxes"]!["selectionBoxes"]!.Values<string>()
            .Concat(behaviors["creaturecarrier"]!["seats"]!.Select(seat => seat.Value<string>("apName")));

        Assert.Empty(named.Except(AttachmentPoints(shape)));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void DrawsEveryTextureItsCracksOverlay(string vessel)
    {
        var (entity, shape) = Load(vessel);
        var cracked = entity["behaviorConfigs"]!["vesselintegrity"]!["cracks"]!["textures"]!.Values<string>();

        Assert.Empty(cracked.Except(FaceTextures(shape)));
    }

    [Theory]
    [MemberData(nameof(Vessels))]
    public void MasksTheSeaOnlyWithAPlaneItHidesFromView(string vessel)
    {
        var (entity, shape) = Load(vessel);
        var client = entity["client"]!;
        if (!client["behaviors"]!.Any(behavior => behavior.Value<string>("code") == "hidewatersurface")) return;

        Assert.Contains(Elements(shape["elements"]!), element => element.Value<string>("name") == "hideWater" && element["children"]!.Any());
        Assert.Contains("hideWater", client["shape"]!["ignoreElements"]!.Values<string>());
    }

    private static (JToken Entity, JToken Shape) Load(string vessel)
    {
        var entity = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", vessel + ".json")));
        var name = entity["client"]!["shape"]!.Value<string>("base")!.Split('/').Last();
        return (entity, JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", name + ".json"))));
    }

    private static IEnumerable<JToken> Elements(JToken elements) =>
        elements.SelectMany(element => element["children"] is { } children ? Elements(children).Prepend(element) : [element]);

    private static IEnumerable<string> AttachmentPoints(JToken shape) =>
        Elements(shape["elements"]!).SelectMany(element => element["attachmentpoints"]?.Select(point => point.Value<string>("code")!) ?? []);

    private static IEnumerable<string> FaceTextures(JToken shape) =>
        Elements(shape["elements"]!).SelectMany(element => element["faces"]!.Children<JProperty>())
            .Select(face => face.Value["texture"]!.Value<string>()!.TrimStart('#'));
}
