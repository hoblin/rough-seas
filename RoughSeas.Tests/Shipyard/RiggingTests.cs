using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Shipyard;

public class RiggingTests
{
    private static readonly JToken Entity = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", "outrigger.json")));
    private static readonly Shape Shape = JsonConvert.DeserializeObject<Shape>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "outrigger.json")))!;
    private static readonly RiggingConfig Config = JsonConvert.DeserializeObject<RiggingConfig>(Entity["behaviorConfigs"]![EntityBehaviorRigging.Code]!.ToString())!;

    private TreeAttribute? tree;

    private Framing Outrigger()
    {
        foreach (var stage in Config.Stages) stage.Pieces = Shape.GetElementByName(stage.Group).Children.Length;
        return new Framing(() => tree, () => { }, Config.Stages);
    }

    private Framing JustJoined()
    {
        tree = new TreeAttribute();
        tree.SetInt(Config.Stages[0].Group, 1);
        return Outrigger();
    }

    private static void LayUpTo(Framing outrigger, string group)
    {
        while (outrigger.Current is { } stage && stage.Group != group) outrigger.Lay("oak", 0);
    }

    [Fact]
    public void AnOutriggerFromBeforeTheBuildIsFinishedWithHerFloatHewn()
    {
        var outrigger = Outrigger();

        Assert.True(outrigger.Finished(0));
        Assert.Contains("Blunt1", outrigger.Hidden());
        Assert.Contains("Blunt2", outrigger.Hidden());
        Assert.DoesNotContain("Ends1", outrigger.Hidden());
    }

    [Fact]
    public void HerFirstBoomIsAcrossHerOnceTheFloatJoins()
    {
        var outrigger = JustJoined();

        Assert.Equal("Booms", outrigger.Current?.Group);
        Assert.Equal(1, outrigger.Laid(outrigger.Current!));
        Assert.DoesNotContain("Booms1", outrigger.Hidden());
        Assert.Contains("Booms2", outrigger.Hidden());
    }

    [Fact]
    public void SheIsRiggedInTheOrderARealOutriggerIs()
    {
        var outrigger = JustJoined();
        var order = new List<string>();
        while (outrigger.Current is { } stage)
        {
            if (order.LastOrDefault() != stage.Group) order.Add(stage.Group);
            outrigger.Lay(null, 0);
        }

        Assert.Equal(["Booms", "Lashing", "Ends", "Mast", "SailFurled", "Stays"], order);
    }

    [Fact]
    public void HewingAnEndOfHerFloatTakesItsBluntEndAway()
    {
        var outrigger = JustJoined();
        LayUpTo(outrigger, "Ends");
        Assert.DoesNotContain("Blunt1", outrigger.Hidden());

        outrigger.Lay(null, 0);

        Assert.Contains("Blunt1", outrigger.Hidden());
        Assert.DoesNotContain("Ends1", outrigger.Hidden());
        Assert.DoesNotContain("Blunt2", outrigger.Hidden());
    }

    [Fact]
    public void ARowOfThreeLogsSplitsIntoEveryPoleSheTakesAndNoMore()
    {
        var poles = Outrigger().Stages.Where(stage => stage.Work == FramingWork.Pole).Sum(stage => stage.Pieces);
        var split = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "blocks", "wedge.json")))["attributes"]!["splits"]!
            .Single(split => split.Value<string>("item")!.StartsWith("roughseas:pole-"));

        Assert.Equal(poles, split.Value<int>("count"));
    }

    [Fact]
    public void HerPolesAreAllHerFrameWood()
    {
        var outrigger = JustJoined();
        var poles = outrigger.Stages.Where(stage => stage.Work == FramingWork.Pole).Sum(stage => stage.Pieces);

        Assert.Equal(1.0 / poles, outrigger.Framed, 6);
    }
}
