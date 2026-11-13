using RoughSeas.Cargo;

namespace RoughSeas.Tests.Cargo;

public class StowageTests
{
    private static readonly double[][] Row = [[0, 0, 0], [1, 0, 0], [2, 0, 0], [3, 0, 0]];

    [Fact]
    public void CountsEachSlotAsFullAsItsStackIsOfAWholeOne() =>
        Assert.Equal((1 + 0.5) / 4, Stowage.Fill([new Lading(0, 64, 64), new Lading(3, 16, 32)], 4), 9);

    [Fact]
    public void CountsAnEmptyHoldAsEmpty() =>
        Assert.Equal(0, Stowage.Fill([], 8));

    [Fact]
    public void LaysTheLargestStackNearestTheMiddleOfItsPlaces()
    {
        var laid = Stowage.Lay([new Lading(5, 4, 64), new Lading(6, 60, 64), new Lading(7, 30, 64)], Row).ToList();

        Assert.Equal([new Stowed(6, 1), new Stowed(7, 2), new Stowed(5, 0)], laid);
    }

    [Fact]
    public void LaysNoMoreStacksThanItHasPlaces()
    {
        var lading = Enumerable.Range(0, 6).Select(slot => new Lading(slot, slot + 1, 64)).ToList();

        Assert.Equal([5, 4, 3, 2], Stowage.Lay(lading, Row).Select(stowed => stowed.Slot));
    }
}
