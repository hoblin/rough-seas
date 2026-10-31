using RoughSeas.Shipyard;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Shipyard;

public class TrunkTests
{
    private readonly HashSet<BlockPos> trunk = [];

    [Fact]
    public void ATrunkRunsFromItsFootToItsTopWhicheverOfItsBlocksIsHeld()
    {
        Grow(10, 6);

        var found = Trunk.Through(new BlockPos(0, 13, 0), trunk.Contains);

        Assert.Equal(new BlockPos(0, 10, 0), found.Foot);
        Assert.Equal(6, found.Height);
    }

    [Fact]
    public void AnotherLogOverAGapIsNotTheSameTrunk()
    {
        Grow(10, 3);
        Grow(14, 3);

        Assert.Equal(3, Trunk.Through(new BlockPos(0, 11, 0), trunk.Contains).Height);
    }

    private void Grow(int foot, int height)
    {
        for (int y = foot; y < foot + height; y++) trunk.Add(new BlockPos(0, y, 0));
    }
}
