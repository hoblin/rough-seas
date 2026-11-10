using RoughSeas.Shore;

namespace RoughSeas.Tests.Shore;

public class SheetFrontTests
{
    [Fact]
    public void OnFlatGroundASheetArrivesAtItsThrownSpeed() =>
        Assert.Equal(4 / Math.Sqrt(2 * 9.81 * 1.5), SheetFront.Arrival(1.5, 0, 4), 6);

    [Fact]
    public void UpASlopeASheetArrivesLaterThanOnFlatGround() =>
        Assert.True(SheetFront.Arrival(1.5, 0.5, 4) > SheetFront.Arrival(1.5, 0, 4));

    private static SheetFront Thrown(double runUp, double period = 9) => SheetFront.Thrown(runUp, period, ShoreFixtures.Config.Sheet);

    private static int Run(SheetFront front, double rise, string bed)
    {
        var blocks = 0;
        for (SheetFront? running = front; running?.Cross(rise, ShoreFixtures.Bed(bed)) is { } next; running = next) blocks++;
        return blocks;
    }

    [Fact]
    public void TheSheetLeavesTheShorelineWithTheHeadOfItsRunUp() => Assert.Equal(1.5, Thrown(1.5).Head, 6);

    [Fact]
    public void TheSheetClimbsAStepItsHeadReachesOver() => Assert.NotNull(Thrown(1.5).Cross(1, ShoreFixtures.Bed("sand")));

    [Fact]
    public void TheSheetStopsAtTheFirstStepItsHeadCannotReachOver() => Assert.Null(Thrown(0.9).Cross(1, ShoreFixtures.Bed("sand")));

    [Fact]
    public void TheSheetLosesHeadToTheBedOnLevelGround() => Assert.True(Thrown(1).Cross(0, ShoreFixtures.Bed("sand"))!.Value.Head < 1);

    [Fact]
    public void OnLevelSandTheSheetStopsWithinTheWavesPeriod() => Assert.InRange(Run(Thrown(1), 0, "sand"), 5, 40);

    [Fact]
    public void AStormSheetRunsFartherOverLevelSandThanAnEverydayOne() =>
        Assert.True(Run(Thrown(1.8, 12), 0, "sand") > Run(Thrown(1, 9), 0, "sand"));

    [Fact]
    public void GravelSoaksTheSheetUpSoonerThanSand() => Assert.True(Run(Thrown(1), 0, "gravel") < Run(Thrown(1), 0, "sand"));

    [Fact]
    public void RockSoaksNothingUp() => Assert.Equal(0, Thrown(1).Cross(0, ShoreFixtures.Bed("stone"))!.Value.Soaked);

    [Fact]
    public void TheLensThinsAsItsFrontSlowsClimbing() => Assert.True(Thrown(3).Cross(2, ShoreFixtures.Bed("soil"))!.Value.Depth < Thrown(3).Depth);

    [Fact]
    public void OnLevelGroundTheSheetRunsNoFurtherThanItsSpeedCarriesItInOneWavePeriod() =>
        Assert.InRange(Run(Thrown(7, 8.4), 0, "soil"), 1, Math.Sqrt(2 * 9.81 * 7) * 8.4);

    [Fact]
    public void ASheetThatJustClimbsOntoABlockWetsItThoughItCannotCrossIt()
    {
        var front = Thrown(0.13, 3.1).Cross(0.125, ShoreFixtures.Bed("sand"));
        Assert.NotNull(front);
        Assert.Equal(0, front.Value.Head);
    }

    [Fact]
    public void AStoppedSheetDoesNotRunOnDownAStep() =>
        Assert.Null(Thrown(0.13, 3.1).Cross(0.125, ShoreFixtures.Bed("sand"))!.Value.Cross(-1, ShoreFixtures.Bed("sand")));
}
