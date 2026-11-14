using RoughSeas.Lines;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Lines;

public class ChainTests
{
    private const double Flax = 1480;
    private const double Diameter = 0.0187;
    private const double Gravity = 9.81;
    private static readonly double Breaking = 11000 * Math.Pow(Diameter / 0.012, 2);
    private const double Stretch = 0.1;

    private static Chain Strung(double chord, double rest, int segments)
    {
        var points = Enumerable.Range(0, segments + 1).Select(k => new Vec3d(chord * k / segments, 0, 0)).ToArray();
        return new Chain(points, rest, Diameter, new Fibre(Flax, 11000, 0.012, Stretch));
    }

    private static double Sag(Chain chain, double chord, double seconds, double step)
    {
        for (var t = 0.0; t < seconds; t += step) chain.Step(new Vec3d(0, 0, 0), new Vec3d(chord, 0, 0), Gravity, step);
        return -chain.Points.Min(point => point.Y);
    }

    [Fact]
    public void ATautLineHangsOnlyAsMuchAsItsOwnWeightStretchesIt()
    {
        const double chord = 4.55;
        var weight = Flax * Math.PI * Diameter * Diameter / 4 * Gravity;
        var stiffness = Breaking / Stretch;
        var expected = Math.Cbrt(3 * weight * Math.Pow(chord, 4) / (64 * stiffness));
        var sag = Sag(Strung(chord, chord, 9), chord, 20, 1 / 60.0);
        Assert.True(Math.Abs(sag / expected - 1) < 0.25, $"sags {sag:F3} m, its weight stretches it to {expected:F3} m");
    }

    [Fact]
    public void ASlackLineHangsAsAChainOfItsLength()
    {
        const double chord = 3;
        const double rest = 3.6;
        var expected = Math.Sqrt(3 * chord * (rest - chord) / 8);
        var sag = Sag(Strung(chord, rest, 8), chord, 20, 1 / 60.0);
        Assert.True(Math.Abs(sag / expected - 1) < 0.2, $"sags {sag:F2} m, a chain of its length {expected:F2} m");
    }

    [Theory]
    [InlineData(1 / 144.0)]
    [InlineData(1 / 20.0)]
    public void ItHangsAlikeAtAnyFrameRate(double step)
    {
        const double chord = 4.55;
        var chain = Strung(chord, chord, 9);
        Sag(chain, chord, 20, step);
        var heights = new List<double>();
        for (var t = 0.0; t < 1; t += step)
        {
            chain.Step(new Vec3d(0, 0, 0), new Vec3d(chord, 0, 0), Gravity, step);
            heights.Add(-chain.Points[chain.Points.Length / 2].Y);
        }
        var still = Sag(Strung(chord, chord, 9), chord, 20, 1 / 60.0);
        Assert.True(Math.Abs(heights.Average() - still) < 0.01, $"at {1 / step:F0} ticks a second it hangs {heights.Average():F3} m, at 60 {still:F3} m");
    }
}
