using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class WingTests
{
    private const double Degree = Math.PI / 180;

    private static readonly Lattice Claw = Lattice.Of([(0, 0), (1, Math.Tan(12.5 * Degree)), (1, -Math.Tan(12.5 * Degree))]);

    private static readonly Lattice SquareSail = Lattice.Of([(0, -0.25), (0, 0.25), (1, 0.25), (1, -0.25)]);

    private static Lattice Plate(double aspect) => Lattice.Of([(0, -aspect / 2), (0, aspect / 2), (1, aspect / 2), (1, -aspect / 2)]);

    private static readonly Wing Wing = Wing.Of(Fixtures.Wind);

    [Theory]
    [InlineData(10, 0.30)]
    [InlineData(20, 0.71)]
    [InlineData(30, 1.08)]
    [InlineData(37, 1.31)]
    [InlineData(47, 0.99)]
    public void ADeltaOfTheClawsAngleLiftsWithinASeventhOfWentzAndKohlmansDeltaOfItsSweep(double degrees, double measured)
    {
        var lift = Wing.Pressing(Claw, degrees * Degree) * Math.Cos(degrees * Degree);
        Assert.True(Math.Abs(lift / measured - 1) < 0.15, $"at {degrees}° it lifts {lift:F2}, the tunnel {measured:F2}");
    }

    [Theory]
    [InlineData(30, 1.25)]
    [InlineData(40, 1.70)]
    [InlineData(45, 1.72)]
    public void TheSquareSailPressesWithinAFifthOfWintersRectangleOfItsAspect(double degrees, double measured)
    {
        var normal = Wing.Pressing(SquareSail, degrees * Degree);
        Assert.True(Math.Abs(normal / measured - 1) < 0.2, $"at {degrees}° it presses {normal:F2}, the tunnel {measured:F2}");
    }

    [Theory]
    [InlineData(3.6, 45, 0.95)]
    [InlineData(6.0, 25, 0.75)]
    [InlineData(6.0, 45, 0.95)]
    public void PastItsStallAPlatePressesWithinATenthOfOrtizRivalAndWoodsPlates(double aspect, double degrees, double measured)
    {
        var normal = Wing.Pressing(Plate(aspect), degrees * Degree);
        Assert.True(Math.Abs(normal / measured - 1) < 0.1, $"aspect {aspect} at {degrees}° it presses {normal:F2}, the tunnel {measured:F2}");
    }

    [Fact]
    public void AWideWingStallsWhereAPlateOfEndlessSpanDoes() =>
        Assert.True(Wing.Pressing(Plate(50), 8 * Degree) > Wing.Pressing(Plate(50), 12 * Degree));

    [Fact]
    public void SquareAcrossTheWindASailPressesAsAPlateAcrossIt() =>
        Assert.Equal(1.11 + 0.02 * (0.5 + 2), Wing.Pressing(SquareSail, Math.PI / 2), 6);

    [Fact]
    public void EdgeOnToTheWindASailIsPressedByNothing() => Assert.Equal(0, Wing.Pressing(SquareSail, 0), 6);
}
