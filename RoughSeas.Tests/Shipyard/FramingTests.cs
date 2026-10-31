using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Shipyard;

public class FramingTests
{
    private static readonly JToken Entity = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", "barkcanoe.json")));
    private static readonly JObject Shape = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "barkcanoe.json")))!;

    private TreeAttribute? tree;
    private int changes;

    private Framing Canoe()
    {
        var config = JsonConvert.DeserializeObject<FramingConfig>(Entity["behaviorConfigs"]![EntityBehaviorFraming.Code]!.ToString())!;
        config.Measure(Shape);
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

    private static FramingStage LayUpTo(Framing canoe, System.Func<FramingStage, bool> stage, double setsAt = 0)
    {
        while (canoe.Current is { } current && !stage(current)) canoe.Lay("oak", setsAt);
        return Assert.IsType<FramingStage>(canoe.Current);
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
    public void ASheetShowsOnlyTheFlatBarkAndAFinishedCanoeAllButIt()
    {
        var canoe = Canoe().Hidden().ToList();
        var sheet = Sheet().Hidden().ToList();

        Assert.Contains(Framing.SkinGroup, sheet);
        Assert.Contains("Frame1", sheet);
        Assert.DoesNotContain(Framing.SheetGroup, sheet);
        Assert.Equal([Framing.SheetGroup], canoe);
    }

    [Fact]
    public void EachPieceShowsOnceItIsLaid()
    {
        var canoe = Sheet();
        canoe.Lay("oak", 0);

        Assert.DoesNotContain("Frame1", canoe.Hidden());
        Assert.Contains("Frame2", canoe.Hidden());
        Assert.Contains(Framing.SheetGroup, canoe.Hidden());
        Assert.DoesNotContain(Framing.SkinGroup, canoe.Hidden());
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
    public void EveryLathSheTakesNamesItsLengthAndAFiveMetreRowIsLongEnough() =>
        Assert.All(Canoe().Stages.Where(stage => stage.Work == FramingWork.Lath).SelectMany(stage => stage.Metres), metres => Assert.InRange(metres, 0.5, 5));

    [Fact]
    public void HerFrameIsTheWoodOfItsLathsAndGrowsWithThem()
    {
        var canoe = Sheet();
        canoe.Lay("maple", 0);

        Assert.Equal("maple", canoe.Wood);
        Assert.Equal(1.0 / canoe.Stages.Where(stage => stage.Work == FramingWork.Lath).Sum(stage => stage.Pieces), canoe.Framed, 6);
    }

    [Fact]
    public void TheSheathingLinesHerAsItIsLaid()
    {
        var canoe = Sheet();
        var lining = LayUpTo(canoe, stage => stage.Lines);
        canoe.Lay("oak", 0);

        Assert.Equal(1.0 / lining.Pieces, canoe.Lined, 6);
    }

    [Fact]
    public void HerHoldHoldsWaterOnlyOnceEverySeamIsPitched()
    {
        var canoe = Sheet();
        var seams = LayUpTo(canoe, stage => stage.Seals);
        for (int seam = 1; seam < seams.Pieces; seam++) canoe.Lay(null, 0);

        Assert.False(canoe.Sealed);
        canoe.Lay(null, 0);
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
    public void TheWettestRibSetsLastAndOnlyRibsSet()
    {
        var canoe = Sheet();
        LayUpTo(canoe, stage => stage.Sets, setsAt: 100);
        canoe.Lay("oak", 50);
        canoe.Lay("oak", 30);

        Assert.Equal(50, canoe.SetsAt);
    }
}
