using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class SailPolarTests
{
    private static SailConfig SquareConfig => Fixtures.Wind.Sails["square"];

    private static SailPolar Square => SailPolar.Of(SquareConfig);

    private static SailPolar CrabClaw => SailPolar.Of(Fixtures.Wind.Sails["crabclaw"]);

    private const double Abeam = Math.PI / 2;

    [Fact]
    public void ASquareSailSquareAcrossARunPushesStraightAheadWithItsWholeDrag()
    {
        var (drive, side) = Square.At(Math.PI, Math.PI / 2);
        Assert.Equal(SquareConfig.LiftAndDrag[^1][1], drive, 4);
        Assert.Equal(0, side, 4);
    }

    [Fact]
    public void EdgeOnToTheWindASquareSailKeepsOnlyItsFriction()
    {
        var (drive, side) = Square.At(Abeam, 0);
        Assert.Equal(-SquareConfig.FrictionDrag * Math.Cos(Abeam), drive, 6);
        Assert.Equal(SquareConfig.FrictionDrag, side, 6);
    }

    [Fact]
    public void ASquareSailLiftsMostAtTheIncidenceItsMeasuredLiftPeaksAt()
    {
        var peak = SquareConfig.LiftAndDrag.MaxBy(pair => pair[0])!;
        var incidence = Math.Atan2(peak[1] - SquareConfig.FrictionDrag, peak[0]);
        Assert.Equal(peak[0], Square.At(Abeam, incidence).Drive, 6);
        Assert.True(Square.At(Abeam, incidence - 0.1).Drive < peak[0]);
        Assert.True(Square.At(Abeam, incidence + 0.1).Drive < peak[0]);
    }

    [Fact]
    public void ASquareSailNeverLuffsByItsHeadingAlone() => Assert.False(Square.Luffs(0));

    [Fact]
    public void CloserToTheWindThanItsFirstHeadingTheCrabClawLuffs()
    {
        var first = Fixtures.Wind.Sails["crabclaw"].Headings[0][0] * Math.PI / 180;
        Assert.True(CrabClaw.Luffs(first - 0.01));
        Assert.False(CrabClaw.Luffs(first));
    }

    [Fact]
    public void BetweenItsMeasuredHeadingsTheCrabClawReadsInBetween()
    {
        var headings = Fixtures.Wind.Sails["crabclaw"].Headings;
        var (a, b) = (headings[4], headings[5]);
        var middle = (a[0] + b[0]) / 2 * Math.PI / 180;
        Assert.Equal((a[1] + b[1]) / 2, CrabClaw.At(middle, 0).Drive, 6);
        Assert.Equal((a[2] + b[2]) / 2, CrabClaw.At(middle, 0).Side, 6);
    }
}
