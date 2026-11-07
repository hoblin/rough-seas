using Newtonsoft.Json.Linq;

namespace RoughSeas.Tests.Shipyard;

public class LathPileTests
{
    private static readonly JToken Lath = Load("resources", "lath.json");

    public static TheoryData<int> Lengths => [2, 3, 4, 5];

    [Theory]
    [MemberData(nameof(Lengths))]
    public void APileDrawsALathForEachItHolds(int metres)
    {
        var pile = Pile(metres);

        Assert.Equal(pile.Value<int>("stackingCapacity"), Shape(metres)["elements"]!.Count());
        Assert.Equal(1, pile.Value<int>("transferQuantity"));
    }

    [Theory]
    [MemberData(nameof(Lengths))]
    public void APileRisesALayerForEachLayerOfLaths(int metres)
    {
        var pile = Pile(metres);
        var perLayer = pile.Value<int>("bulkTransferQuantity");
        var elements = Shape(metres)["elements"]!.ToArray();
        var layer = elements[perLayer]["from"]![1]!.Value<double>() - elements[0]["from"]![1]!.Value<double>();

        Assert.Equal(1.0 / perLayer, pile.Value<double>("cbScaleYByLayer"), 5);
        Assert.Equal(layer / 16, pile["collisionBox"]!.Value<double>("y2"), 5);
    }

    [Theory]
    [InlineData("coracle.json")]
    [InlineData("currach.json")]
    public void AHideBoatStartsFromOneFullLayerOrTheTwoOuterLathsOfAPile(string vessel)
    {
        var start = Load("vessel", vessel)["behaviorConfigs"]!["roughseas:covering"]!["start"]!;
        var metres = start.Value<int>("metres");

        Assert.True(start.Value<int>("laths") <= Pile(metres).Value<int>("bulkTransferQuantity"));
    }

    private static JToken Pile(int metres) =>
        Lath["behaviorsByType"]![$"*-{metres}-*"]!.First(b => b.Value<string>("name") == "GroundStorable")["properties"]!;

    private static JToken Shape(int metres) => Load(Path.Combine("shapes", "block", "pile"), $"lathpile-{metres}.json");

    private static JToken Load(string folder, string file) => JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, folder, file)));
}
