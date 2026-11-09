using RoughSeas.Shore;

namespace RoughSeas.Tests.Shore;

public class RunUpTests
{
    private readonly RunUp runUp = ShoreFixtures.RunUp;

    [Fact]
    public void AFourMetreSeaRunsAboutAMetreUpAGentleSandyBeach() =>
        Assert.InRange(runUp.Metres(4, 9, 1.0 / 20, ShoreFixtures.Bed("sand")), 0.9, 1.05);

    [Fact]
    public void AnEightMetreStormSeaRunsAboutOneMetreEightyUpTheSameBeach() =>
        Assert.InRange(runUp.Metres(8, 12, 1.0 / 20, ShoreFixtures.Bed("sand")), 1.7, 1.9);

    [Fact]
    public void OnAGentleBeachTheRunUpNoLongerDependsOnTheSlope() =>
        Assert.Equal(runUp.Metres(4, 9, 1.0 / 30, ShoreFixtures.Bed("sand")), runUp.Metres(4, 9, 1.0 / 40, ShoreFixtures.Bed("sand")), 6);

    [Fact]
    public void ASteepRockShelfThrowsTheSeaHigherThanAGentleBeach() =>
        Assert.True(runUp.Metres(2, 8, 1.0 / 3, ShoreFixtures.Bed("stone")) > runUp.Metres(2, 8, 1.0 / 20, ShoreFixtures.Bed("sand")));

    [Fact]
    public void RoughGravelTakesTheRunUpDownFromASmoothSlopeOfTheSameSteepness() =>
        Assert.True(runUp.Metres(2, 8, 1.0 / 3, ShoreFixtures.Bed("gravel")) < runUp.Metres(2, 8, 1.0 / 3, ShoreFixtures.Bed("stone")));

    [Fact]
    public void AWallThrowsTheSeaOneWaveHeightUp() => Assert.Equal(4.7, runUp.Metres(4.7, 10, 2, ShoreFixtures.Bed("stone")), 6);

    [Fact]
    public void NoSeaRunsNothingUp() => Assert.Equal(0, runUp.Metres(0, 0, 0.1, ShoreFixtures.Bed("sand")));

    [Fact]
    public void TheSlopeRunsThroughTheCornerOfTheFirstStepTheRunUpReaches() =>
        Assert.Equal(1.125 / 3, RunUp.Slope([0.125, 0.125, 1.125, 2.125], 1), 6);

    [Fact]
    public void ABeachThatNeverRisesToTheRunUpTakesItsWholeLengthAsTheSlope() =>
        Assert.Equal(0.125 / 2, RunUp.Slope([0.125, 0.125], 1), 6);

    [Fact]
    public void ABeachRisingABlockInTenRunsUpAsASmoothOneInTenSlope() =>
        Assert.Equal(runUp.Metres(4, 9, 0.1, ShoreFixtures.Bed("sand")),
            runUp.Over(4, 9, Enumerable.Range(0, 40).Select(i => 0.125 + i / 10).ToList(), ShoreFixtures.Bed("sand")), 1);
}
