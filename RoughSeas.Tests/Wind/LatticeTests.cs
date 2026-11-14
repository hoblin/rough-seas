using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class LatticeTests
{
    private static (double X, double Y)[] Delta(double apexDegrees)
    {
        var half = Math.Tan(apexDegrees / 2 * Math.PI / 180);
        return [(0, 0), (1, half), (1, -half)];
    }

    private static (double X, double Y)[] Rectangle(double aspect) => [(0, -aspect / 2), (0, aspect / 2), (1, aspect / 2), (1, -aspect / 2)];

    [Theory]
    [InlineData(25, 1.35, 3.2)]
    public void ADeltaTakesPolhamusLiftSlopeAndVortexLift(double apex, double potential, double vortex)
    {
        var lattice = Lattice.Of(Delta(apex));
        Assert.True(Math.Abs(lattice.Potential / potential - 1) < 0.15, $"Kp {lattice.Potential:F2}, Polhamus {potential}");
        Assert.True(Math.Abs(lattice.Vortex / vortex - 1) < 0.15, $"Kv {lattice.Vortex:F2}, Polhamus {vortex}");
    }

    [Theory]
    [InlineData(0.5, 0.831, 2.9)]
    [InlineData(1.0, 1.59, 2.87)]
    [InlineData(2.0, 2.59, 3.05)]
    public void ARectangleTakesTorresAndMuellersLiftSlopeAndLamarsEdgeVortexLift(double aspect, double potential, double vortex)
    {
        var lattice = Lattice.Of(Rectangle(aspect));
        Assert.True(Math.Abs(lattice.Potential / potential - 1) < 0.1, $"Kp {lattice.Potential:F2}, measured {potential}");
        Assert.True(Math.Abs(lattice.Vortex / vortex - 1) < 0.15, $"Kv {lattice.Vortex:F2}, Lamar {vortex}");
    }
}
