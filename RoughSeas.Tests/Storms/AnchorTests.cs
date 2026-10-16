using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class AnchorTests
{
    private readonly Anchor anchor = new(0, 0, 0, RodeMetres: 50, Rope: "rope-flax", HoldingNewtons: 3000);

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
        Assert.Equal(3000, anchor.Holding(depth: 10, fullScope: 5));
        Assert.Equal(1500, anchor.Holding(depth: 50.0 / 3, fullScope: 5), 6);
        Assert.Equal(0, anchor.Holding(depth: 50, fullScope: 5));
    }

    [Fact]
    public void DraggingMovesWhereItLies()
    {
        Assert.Equal(new Anchor(2, 0, -1, 50, "rope-flax", 3000), anchor.DraggedBy(2, -1));
    }
}
