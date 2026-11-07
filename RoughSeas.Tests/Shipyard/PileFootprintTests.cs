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

    [Fact]
    public void APileTurnedAQuarterLiesAlongTheOtherAxis()
    {
        var covered = new PileFootprint(2, GameMath.PIHALF).Around(Pile).ToList();

        Assert.Equal([10, 9], covered.Select(pos => pos.Z));
        Assert.All(covered, pos => Assert.Equal(10, pos.X));
    }

    [Fact]
    public void APileTurnedAboutReachesTheOtherWay()
    {
        Assert.Equal([10, 9], new PileFootprint(2, GameMath.PI).Around(Pile).Select(pos => pos.X));
    }

    [Fact]
    public void NoPileReachesFurtherThanVanillasFillersDo()
    {
        var reach = new PileFootprint(5, 0).Around(Pile).Max(pos => Math.Abs(pos.X - Pile.X));

        Assert.Equal(PileFootprint.MaxReach, reach);
    }
}
