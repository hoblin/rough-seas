using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class AnchorTests
{
    private const double FullScope = 5;

    private readonly Anchor anchor = new(0, 0, 0, RodeMetres: 50, Rope: "rope-flax", HoldingNewtons: 3000);

    [Fact]
    public void LetGoInShallowWaterItPaysOutFullScope()
    {
        Assert.Equal(50, LetGo(depth: 10, rodeAboard: 210)!.Value.RodeMetres);
    }

    [Fact]
    public void LetGoTooDeepForFullScopeItPaysOutAllTheRodeAboard()
    {
        Assert.Equal(210, LetGo(depth: 60, rodeAboard: 210)!.Value.RodeMetres);
    }

    [Fact]
    public void LetGoDeeperThanTheRodeReachesItHangs()
    {
        Assert.Null(LetGo(depth: 211, rodeAboard: 210));
    }

    [Fact]
    public void TheVesselSwingsFurtherInShallowWater()
    {
        Assert.Equal(40, anchor.Reach(30), 6);
        Assert.Equal(50, anchor.Reach(0), 6);
        Assert.Equal(0, anchor.Reach(60));
    }

    [Fact]
    public void ItHoldsFullyFromFiveTimesTheDepth()
    {
        Assert.Equal(3000, anchor.Holding(depth: 10, FullScope));
        Assert.Equal(1500, anchor.Holding(depth: 50.0 / 3, FullScope), 6);
        Assert.Equal(0, anchor.Holding(depth: 50, FullScope));
    }

    [Fact]
    public void OnAShoalItHoldsFully()
    {
        Assert.Equal(3000, anchor.Holding(depth: 0, FullScope));
    }

    [Fact]
    public void DraggingMovesWhereItLies()
    {
        Assert.Equal(new Anchor(2, 0, -1, 50, "rope-flax", 3000), anchor.DraggedBy(2, -1));
    }

    private static Anchor? LetGo(double depth, double rodeAboard) =>
        Anchor.LetGo(0, -depth, 0, depth, rodeAboard, FullScope, "rope-flax", 3000);
}
