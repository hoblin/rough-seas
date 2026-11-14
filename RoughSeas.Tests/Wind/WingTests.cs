using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class WingTests
{
    private const double Degree = Math.PI / 180;
    private const double Abeam = Math.PI / 2;

    private static readonly Delta Claw = new(25 * Degree);

    private static readonly Rectangle SquareSail = new(0.5);

    private static Wing WingOf(string kind, Planform planform) => Wing.Of(Fixtures.Wind, kind, planform);

    private static double Lift(Wing wing, double degrees) => wing.Pressing(degrees * Degree) * Math.Cos(degrees * Degree);

    [Fact]
    public void ADeltaOfTheClawsAngleTakesPolhamusLiftSlopeAndVortexLift()
    {
        Assert.InRange(Claw.Potential, 1.30, 1.40);
        Assert.InRange(Claw.Vortex, 3.1, 3.3);
    }

    [Theory]
    [InlineData(10, 0.30)]
    [InlineData(20, 0.71)]
    [InlineData(30, 1.08)]
    [InlineData(37, 1.31)]
    [InlineData(47, 0.99)]
    public void TheClawLiftsWithinASeventhOfWentzAndKohlmansDeltaOfItsSweep(double degrees, double measured)
    {
        var lift = Lift(WingOf(CrabClaw.Kind, Claw), degrees);
        Assert.True(Math.Abs(lift / measured - 1) < 0.15, $"at {degrees}° the claw lifts {lift:F2}, the tunnel {measured:F2}");
    }

    [Theory]
    [InlineData(30, 1.25)]
    [InlineData(40, 1.70)]
    [InlineData(45, 1.72)]
    public void TheSquareSailPressesWithinAFifthOfWintersRectangleOfItsAspect(double degrees, double measured)
    {
        var normal = WingOf(SquareRig.Kind, SquareSail).Pressing(degrees * Degree);
        Assert.True(Math.Abs(normal / measured - 1) < 0.2, $"at {degrees}° the square sail presses {normal:F2}, the tunnel {measured:F2}");
    }

    [Fact]
    public void PastItsSeparationASailIsAPlatePressedAsHerHullsSide()
    {
        var wing = WingOf(CrabClaw.Kind, Claw);
        Assert.Equal(Fixtures.Wind.BluffDrag * Math.Pow(Math.Sin(70 * Degree), 2), wing.Pressing(70 * Degree), 6);
    }

    [Fact]
    public void ASquareSailSquareAcrossARunPushesStraightAheadAsAPlateAcrossTheWind()
    {
        var (drive, side) = WingOf(SquareRig.Kind, SquareSail).At(Math.PI, Abeam);
        Assert.Equal(Fixtures.Wind.BluffDrag, drive, 6);
        Assert.Equal(0, side, 6);
    }

    [Fact]
    public void EdgeOnToTheWindASailKeepsOnlyItsClothsFriction()
    {
        var (drive, side) = WingOf(SquareRig.Kind, SquareSail).At(Abeam, 0);
        Assert.Equal(0, drive, 6);
        Assert.Equal(Fixtures.Wind.ClothFriction, side, 6);
    }
}
