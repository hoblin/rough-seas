using RoughSeas.Water;

namespace RoughSeas.Tests.Water;

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

    private static double[] Slope(double perBlock, int blocks = 200) => Enumerable.Range(1, blocks).Select(i => i * perBlock).ToArray();

    private static double Climbed(SheetFront front, double[] profile, string bed)
    {
        var climbed = 0.0;
        SheetFront? running = front;
        for (var i = 0; i < profile.Length; i++)
        {
            if (running?.Cross(profile[i] - (i > 0 ? profile[i - 1] : 0), ShoreFixtures.Bed(bed)) is not { } next) break;
            (running, climbed) = (next, profile[i]);
        }
        return climbed;
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.1)]
    [InlineData(0.143)]
    public void ASheetThrownUpASlopeStopsAtItsRunUpWithItsFrictionSpent(double slope)
    {
        var profile = Slope(slope);
        var front = SheetFront.Thrown(1.5, 9, ShoreFixtures.Config.Sheet, profile, ShoreFixtures.Bed("soil"));
        Assert.InRange(Climbed(front, profile, "soil"), 1.5 - slope, 1.5 + 3 * slope);
    }

    [Fact]
    public void AHigherRunUpNeverThrowsAWeakerSheetUpAShoreThatEndsBelowIt()
    {
        var profile = Slope(0.05, 20);
        var heads = new[] { 0.5, 0.9, 1.0, 1.1, 1.5, 3 }.Select(runUp => SheetFront.Thrown(runUp, 9, ShoreFixtures.Config.Sheet, profile, ShoreFixtures.Bed("soil")).Head).ToArray();
        for (var i = 1; i < heads.Length; i++) Assert.True(heads[i] >= heads[i - 1] - 0.01, $"{heads[i]} after {heads[i - 1]}");
    }

    [Fact]
    public void AShoreThatNeverRisesToTheRunUpKeepsTheFreeThrow() =>
        Assert.Equal(Thrown(1.5).Head, SheetFront.Thrown(1.5, 9, ShoreFixtures.Config.Sheet, new double[48], ShoreFixtures.Bed("sand")).Head, 6);

    [Fact]
    public void AStoppedSheetDoesNotRunOnDownAStep() =>
        Assert.Null(Thrown(0.13, 3.1).Cross(0.125, ShoreFixtures.Bed("sand"))!.Value.Cross(-1, ShoreFixtures.Bed("sand")));
}
