using RoughSeas.Water;

namespace RoughSeas.Tests.Water;

public class CoarseBedTests
{
    private const int Across = 256;
    private const int CoarseAcross = 48;
    private const double SeaHeight = 4;
    private const double Number = 2 * Math.PI / 120;

    private static readonly WaveTransformation Transformation = new(1.28, length => Math.Min(16, (int)Math.Ceiling(length * 0.25)), 1, 0.12, 0.45);

    private static float Bed(double worldX, double slope) => worldX >= 1000 ? -1 : (float)Math.Min((1000 - worldX) * slope + 0.875, 60);

    private static (float[] Lag, float[] Depth) Solved(int originX, double slope)
    {
        var depth = Enumerable.Range(0, Across * Across).Select(i => Bed(originX + i % Across + 0.5, slope)).ToArray();
        var coarse = new CoarseBed(0, 0, 32, CoarseAcross, Enumerable.Range(0, CoarseAcross * CoarseAcross).Select(i => Math.Max(0, Bed((i % CoarseAcross + 0.5) * 32, slope))).ToArray(), Enumerable.Repeat(SeaHeight, CoarseAcross * CoarseAcross).ToArray());
        var numbers = new[] { (Number, 0.0) };
        return (Transformation.Solve(Across, depth, numbers, null, Enumerable.Repeat(SeaHeight, depth.Length).ToArray(), 0, coarse.Entering(Transformation, numbers, null, 0, originX, 512)).Lags[0], depth);
    }

    private static double Wrapped(double angle) => angle - 2 * Math.PI * Math.Round(angle / (2 * Math.PI));

    [Theory]
    [InlineData(500, 0.02, 0.05)]
    [InlineData(700, 0.02, 0.1)]
    [InlineData(860, 0.02, 0.3)]
    [InlineData(860, 0.05, 0.2)]
    public void AMapMovedByAStepKeepsTheCrestsOverTheSameWater(int originX, double slope, double allowed)
    {
        var ((one, depth), (other, _)) = (Solved(originX, slope), Solved(originX + 32, slope));
        for (var x = 72; x < Across - 40; x++)
        {
            if (depth[128 * Across + x] > 0) Assert.InRange(Wrapped(one[128 * Across + x] - other[128 * Across + x - 32]), -allowed, allowed);
        }
    }

    [Fact]
    public void LandLeavesNoHoleInTheReferenceBesideIt()
    {
        var depth = Enumerable.Range(0, 4 * 4).Select(i => i % 4 < 2 ? 3f : 0f).ToArray();
        var bed = new CoarseBed(0, 0, 32, 4, depth, Enumerable.Repeat(1.0, 16).ToArray());
        var anchor = bed.Entering(Transformation, [(Number, 0.0)], null, 0, 0, 0).Lags[0];
        Assert.NotEqual(0, anchor(60, 64), 6);
        Assert.Equal(anchor(50, 64), anchor(60, 64), 6);
    }

    [Fact]
    public void AReefBeyondTheMapShelters()
    {
        const int across = 128;
        const int reefColumn = 13;
        var coarseDepth = Enumerable.Range(0, CoarseAcross * CoarseAcross).Select(i => Math.Abs(i % CoarseAcross - reefColumn) <= 1 ? 2f : 40f).ToArray();
        var coarse = new CoarseBed(0, 0, 32, CoarseAcross, coarseDepth, Enumerable.Repeat(SeaHeight, coarseDepth.Length).ToArray());
        var numbers = new[] { (Number, 0.0) };
        var depth = Enumerable.Repeat(40f, across * across).ToArray();
        var heights = Enumerable.Repeat(SeaHeight, depth.Length).ToArray();
        var (originX, originZ) = (reefColumn * 32 + 160, 512);
        var open = Transformation.Solve(across, depth, numbers, null, heights);
        var behind = Transformation.Solve(across, depth, numbers, null, heights, 0, coarse.Entering(Transformation, numbers, null, 0, originX, originZ));
        Assert.True(behind.Gains[0][across * across / 2 + 4] < open.Gains[0][across * across / 2 + 4] - 0.2, $"{behind.Gains[0][across * across / 2 + 4]} against {open.Gains[0][across * across / 2 + 4]}");
    }
}
