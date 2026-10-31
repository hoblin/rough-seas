using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Shipyard;

public class FramingTests
{
    private static readonly JToken Entity = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", "barkcanoe.json")));
    private static readonly Shape Shape = JsonConvert.DeserializeObject<Shape>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "barkcanoe.json")))!;

    private TreeAttribute? tree;
    private int changes;

    private Framing Canoe()
    {
        var config = JsonConvert.DeserializeObject<FramingConfig>(Entity["behaviorConfigs"]![EntityBehaviorFraming.Code]!.ToString())!;
        foreach (var stage in config.Stages) stage.Pieces = Shape.GetElementByName(stage.Group).Children.Length;
        return new Framing(() => tree, () => changes++, config.Stages);
    }

    private Framing Sheet()
    {
        tree = new TreeAttribute();
        return Canoe();
    }

    private static void LayAll(Framing canoe, string wood = "oak", double setsAt = 48)
    {
        while (canoe.Current is { } stage) canoe.Lay(stage.Work == FramingWork.Lath ? wood : null, setsAt);
    }

    [Fact]
    public void ACanoeFromBeforeTheBuildIsFinished()
    {
        var canoe = Canoe();

        Assert.True(canoe.Built);
        Assert.True(canoe.Finished(0));
        Assert.True(canoe.Sealed);
        Assert.Equal(1, canoe.Framed);
    }

    [Fact]
    public void AStrippedSheetIsBareBark()
    {
        var sheet = Sheet();

        Assert.False(sheet.Finished(1000));
        Assert.Equal("Frame", sheet.Current?.Group);
        Assert.Equal(0, sheet.Framed);
        Assert.Equal(0, sheet.Lined);
        Assert.False(sheet.Sealed);
        Assert.Null(sheet.Wood);
    }

    [Fact]
    public void HerStagesGoOnInOrderPieceByPiece()
    {
        var canoe = Sheet();
        var order = new List<string>();

        while (canoe.Current is { } stage)
        {
            if (order.LastOrDefault() != stage.Group) order.Add(stage.Group);
            canoe.Lay("oak", 0);
        }

        Assert.Equal(["Frame", "Stems", "Lashing", "Sheathing", "Ribs", "Pitch"], order);
        Assert.Equal(canoe.Stages.Sum(stage => stage.Pieces), changes);
    }

    [Fact]
    public void ARowOfLogsHasLathsEnoughForHerWholeFrame()
    {
        var laths = Canoe().Stages.Where(stage => stage.Work == FramingWork.Lath).Sum(stage => stage.Pieces);
        var row = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "blocks", "wedge.json")))["attributes"]!.Value<int>("laths");

        Assert.InRange(laths, 1, row);
    }

    [Fact]
    public void HerFrameIsTheWoodOfItsLathsAndGrowsWithThem()
    {
        var canoe = Sheet();
        canoe.Lay("maple", 0);

        Assert.Equal("maple", canoe.Wood);
        Assert.InRange(canoe.Framed, 0.01, 0.1);
    }

    [Fact]
    public void TheSheathingLinesHerAsItIsLaid()
    {
        var canoe = Sheet();
        while (canoe.Current?.Lines != true) canoe.Lay("oak", 0);
        canoe.Lay("oak", 0);

        Assert.Equal(1.0 / canoe.Current!.Pieces, canoe.Lined, 6);
    }

    [Fact]
    public void HerHoldHoldsWaterOnlyOnceEverySeamIsPitched()
    {
        var canoe = Sheet();
        while (canoe.Current?.Seals != true) canoe.Lay("oak", 0);

        Assert.False(canoe.Sealed);
        LayAll(canoe);
        Assert.True(canoe.Sealed);
    }

    [Fact]
    public void SheIsFinishedOnlyOnceHerLastRibHasSet()
    {
        var canoe = Sheet();
        LayAll(canoe, setsAt: 48);

        Assert.True(canoe.Built);
        Assert.False(canoe.Finished(47));
        Assert.True(canoe.Finished(48));
    }

    [Fact]
    public void TheWettestRibSetsLast()
    {
        var canoe = Sheet();
        while (canoe.Current?.Sets != true) canoe.Lay("oak", 0);
        canoe.Lay("oak", 50);
        canoe.Lay("oak", 30);

        Assert.Equal(50, canoe.SetsAt);
    }
}
