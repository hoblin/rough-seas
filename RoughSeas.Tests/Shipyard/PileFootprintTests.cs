using RoughSeas.Shipyard;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Shipyard;

public class PileFootprintTests
{
    private static readonly BlockPos Pile = new(10, 5, 10);

    [Theory]
    [InlineData(2, new[] { 10, 11 })]
    [InlineData(3, new[] { 9, 10, 11 })]
    [InlineData(4, new[] { 9, 10, 11, 12 })]
    [InlineData(5, new[] { 8, 9, 10, 11, 12 })]
    public void APileCoversAsManyBlocksAsItsPiecesAreMetresLong(int metres, int[] xs)
    {
        var covered = new PileFootprint(metres, 0).Around(Pile).ToList();

        Assert.Equal(xs, covered.Select(pos => pos.X));
        Assert.All(covered, pos => Assert.Equal((5, 10), (pos.Y, pos.Z)));
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(GameMath.PIHALF, 0, -1)]
    [InlineData(GameMath.PI, -1, 0)]
    [InlineData(-GameMath.PIHALF, 0, 1)]
    public void AnEvenPileReachesTheWayItIsTurned(float angle, int x, int z)
    {
        var far = new PileFootprint(2, angle).Around(Pile).Last();

        Assert.Equal((10 + x, 10 + z), (far.X, far.Z));
    }

    [Fact]
    public void TheLongestPileReachesAsFarAsVanillasFillers()
    {
        var reach = new PileFootprint(5, 0).Around(Pile).Max(pos => Math.Abs(pos.X - Pile.X));

        Assert.Equal(PileFootprint.MaxReach, reach);
    }
}
