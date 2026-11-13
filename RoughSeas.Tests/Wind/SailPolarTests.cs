using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class SailPolarTests
{
    private static SailPolar Square => new(Fixtures.Wind.Sails["square"]);

    private static SailPolar CrabClaw => new(Fixtures.Wind.Sails["crabclaw"]);

    [Fact]
    public void WithTheWindAbeamASquareSailDrivesByItsGreatestLift()
    {
        var greatestLift = Fixtures.Wind.Sails["square"].LiftAndDrag.Max(pair => pair[0]);
        Assert.Equal(greatestLift, Square.At(Math.PI / 2)!.Value.Drive, 2);
    }

    [Fact]
    public void DeadBeforeTheWindASquareSailPushesStraightAhead()
    {
        var (drive, side) = Square.At(Math.PI)!.Value;
        Assert.Equal(Fixtures.Wind.Sails["square"].LiftAndDrag.Max(pair => pair[1]), drive, 4);
        Assert.Equal(0, side, 4);
    }

    [Fact]
    public void CloserToTheWindThanItsFirstHeadingTheCrabClawLuffs()
    {
        Assert.Null(CrabClaw.At(CrabClaw.LuffsBelow - 0.01));
        Assert.NotNull(CrabClaw.At(CrabClaw.LuffsBelow));
    }

    [Fact]
    public void BetweenItsMeasuredHeadingsTheCrabClawReadsInBetween()
    {
        var headings = Fixtures.Wind.Sails["crabclaw"].Headings;
        var (a, b) = (headings[4], headings[5]);
        var middle = (a[0] + b[0]) / 2 * Math.PI / 180;
        Assert.Equal((a[1] + b[1]) / 2, CrabClaw.At(middle)!.Value.Drive, 6);
        Assert.Equal((a[2] + b[2]) / 2, CrabClaw.At(middle)!.Value.Side, 6);
    }
}
