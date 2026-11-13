using RoughSeas.Cargo;

namespace RoughSeas.Tests.Cargo;

public class StowageTests
{
    private static readonly double[][] Row = [[0, 0, 0], [1, 0, 0], [2, 0, 0], [3, 0, 0]];

    [Fact]
    public void CountsEachSlotAsFullAsItsStackIsOfAWholeOne() =>
        Assert.Equal(0.5 * (1 + 0.5) / 2, Stowage.Fill([new Load(0, 64, 64), new Load(3, 16, 32)], 4), 9);

    [Fact]
    public void CountsAnEmptyHoldAsEmpty() =>
        Assert.Equal(0, Stowage.Fill([], 8));

    [Fact]
    public void LaysTheLargestStackNearestTheMiddleOfItsPlaces()
    {
        var laid = Stowage.Lay([new Load(0, 4, 64), new Load(1, 60, 64), new Load(2, 30, 64)], Row).ToList();

        Assert.Equal(new Stowed(1, 1), laid[0]);
        Assert.Equal(new Stowed(2, 2), laid[1]);
        Assert.Equal(new Stowed(0, 0), laid[2]);
    }

    [Fact]
    public void LaysNoMoreStacksThanItHasPlaces()
    {
        var loads = Enumerable.Range(0, 6).Select(slot => new Load(slot, slot + 1, 64)).ToList();

        Assert.Equal([5, 4, 3, 2], Stowage.Lay(loads, Row).Select(stowed => stowed.Slot));
    }
}
