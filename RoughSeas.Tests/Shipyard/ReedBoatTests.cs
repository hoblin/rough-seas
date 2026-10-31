using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Shipyard;

public class ReedBoatTests
{
    private static readonly JToken Entity = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", "reedboat.json")));
    private static readonly string ShapeText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "reedboat.json"));
    private static readonly Shape Shape = JsonConvert.DeserializeObject<Shape>(ShapeText)!;
    private static readonly FramingConfig Config = JsonConvert.DeserializeObject<FramingConfig>(Entity["behaviorConfigs"]![EntityBehaviorFraming.Code]!.ToString())!;

    private TreeAttribute? tree;

    private Framing Boat()
    {
        foreach (var stage in Config.Stages) stage.Pieces = Shape.GetElementByName(stage.Group).Children.Length;
        return new Framing(() => tree, () => { }, Config.Stages);
    }

    private Framing JustBound()
    {
        tree = new TreeAttribute();
        tree.SetInt(Config.Stages[0].Group, 1);
        return Boat();
    }

    [Fact]
    public void AReedBoatFromBeforeTheBuildIsFinishedWithHerEndsRaised()
    {
        var boat = Boat();

        Assert.True(boat.Finished(0));
        Assert.Contains("Flat1", boat.Hidden());
        Assert.Contains("Flat2", boat.Hidden());
        Assert.DoesNotContain("Tips1", boat.Hidden());
    }

    [Fact]
    public void TheFirstBindingIsALashingAcrossHerFlatBundles()
    {
        var boat = JustBound();

        Assert.False(boat.Finished(0));
        Assert.Equal("Lashing", boat.Current?.Group);
        Assert.DoesNotContain("Lashing1", boat.Hidden());
        Assert.Contains("Lashing2", boat.Hidden());
        Assert.DoesNotContain("Flat1", boat.Hidden());
        Assert.Contains("Tips1", boat.Hidden());
    }

    [Fact]
    public void SheIsLashedThenHerEndsAreBoundUpOneByOne()
    {
        var boat = JustBound();
        var order = new List<string>();
        while (boat.Current is { } stage)
        {
            if (order.LastOrDefault() != stage.Group) order.Add(stage.Group);
            if (stage.Group == "Tips" && boat.Laid(stage) == 1)
            {
                Assert.Contains("Flat1", boat.Hidden());
                Assert.DoesNotContain("Flat2", boat.Hidden());
            }
            boat.Lay(null, 0);
        }

        Assert.Equal(["Lashing", "Tips"], order);
        Assert.True(boat.Finished(0));
    }

    [Fact]
    public void HerBundlesLieAsTheyWereLaidUntilBound()
    {
        var laid = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "block", "reedbundle-two.json")))["elements"]!;
        var shape = JToken.Parse(ShapeText)["elements"]!;
        JToken Group(string name) => shape.Single(element => element.Value<string>("name") == name);
        var blank = Group("Blank")["children"]!.Concat(Group("Flat")["children"]!.SelectMany(end => end["children"]!));

        Assert.Equal(laid.Select(Draw), blank.Select(Draw));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.1, 0)]
    [InlineData(0.5, 2)]
    [InlineData(1, 3)]
    [InlineData(1.5, 3)]
    public void HerEndsSagWithHerSoak(double soaked, int sag) => Assert.Equal(sag, EntityBehaviorDroop.SagOf(soaked, 4));

    [Fact]
    public void EachEndIsDrawnAtEverySag()
    {
        var droop = Entity["behaviorConfigs"]![EntityBehaviorDroop.Code]!["ends"]!.Values<string>();

        Assert.All(droop, end => Assert.Equal(Enumerable.Range(0, 4).Select(n => $"{end}Droop{n}"),
            Shape.GetElementByName(end!).Children.Select(child => child.Name)));
    }

    private static string Draw(JToken element) => element.ToString(Formatting.None);
}
