using RoughSeas.Water;

namespace RoughSeas.Tests.Water;

public class PhaseFieldTests
{
    [Fact]
    public void AnOceanEnteringTheMapLeavesASeparateLakeAsItWas()
    {
        const int Across = 64;
        float[] Bed(float beyond) => Enumerable.Range(0, Across * Across).Select(i => (i % Across) switch
        {
            < 24 => (24 - i % Across) * 0.25f,
            < 32 => -1f,
            _ => beyond,
        }).ToArray();
        var (alone, besideOcean) = (PhaseField.Lag(Across, Bed(-1), 0.05, 0.01), PhaseField.Lag(Across, Bed(500), 0.05, 0.01));

        Assert.Equal(alone[32 * Across + 12], besideOcean[32 * Across + 12], 3);
    }

    private static float ShelfDepth(int worldX) => worldX < 320 ? (320 - worldX) * 0.25f : -1;

    private static float[] Shelf(int across, int startX, int step) =>
        Enumerable.Range(0, across * across).Select(i => ShelfDepth(startX + (i % across) * step + step / 2)).ToArray();

    [Theory]
    [InlineData(40, 48, 0.05, 0.0)]
    [InlineData(100, 132, 0.05, 0.01)]
    [InlineData(200, 216, 0.05, -0.02)]
    public void TwoMapsOfOneShelfAgreeWhereverTheyStartThroughTheSharedReference(int firstX, int secondX, double numberX, double numberZ)
    {
        const int Across = 64;
        var coarse = new CoarseBed(0, 0, 8, 48, Shelf(48, 0, 8), new double[48 * 48]);
        var transformation = new WaveTransformation(1.28, _ => 0, 1, 0.12, 0.45);
        var one = PhaseField.Lag(Across, Shelf(Across, firstX, 1), numberX, numberZ, coarse.Entering(transformation, [(numberX, numberZ)], null, 0, firstX, 0).Lags[0]);
        var other = PhaseField.Lag(Across, Shelf(Across, secondX, 1), numberX, numberZ, coarse.Entering(transformation, [(numberX, numberZ)], null, 0, secondX, 0).Lags[0]);

        foreach (var worldX in Enumerable.Range(secondX + 8, firstX + Across - secondX - 12))
        {
            Assert.InRange(one[32 * Across + worldX - firstX] - other[32 * Across + worldX - secondX], -0.25, 0.25);
        }
    }

    [Theory]
    [InlineData(0.05, 0.015)]
    [InlineData(-0.05, 0.006)]
    [InlineData(0.04, -0.03)]
    public void AWaveAtAShallowAngleOverEvenWaterHasNoLagAnywhere(double numberX, double numberZ)
    {
        const int Across = 96;
        var lag = PhaseField.Lag(Across, Enumerable.Repeat(500f, Across * Across).ToArray(), numberX, numberZ);
        Assert.All(lag, value => Assert.InRange(value, -0.02, 0.02));
    }

    [Fact]
    public void AShallowBodyBetweenTwoShoresHoldsOneUnbrokenPhase()
    {
        const int Across = 64;
        var depth = Enumerable.Range(0, Across * Across).Select(i => i % Across is 0 or Across - 1 ? -1f : 1f).ToArray();
        var lag = PhaseField.Lag(Across, depth, 0.05, 0);
        var number = WaveTheory.Wavenumber(0.05, 1);

        for (var x = 2; x < Across - 3; x++) Assert.Equal(number - 0.05, lag[32 * Across + x + 1] - lag[32 * Across + x], 3);
    }

    [Fact]
    public void AnObliqueWaveCrossingAShoalKeepsItsDelayBehindIt()
    {
        const int Across = 64;
        var depth = Enumerable.Range(0, Across * Across).Select(i => i % Across is >= 16 and < 26 ? 3f : 500f).ToArray();
        Assert.True(PhaseField.Lag(Across, depth, 0.05, 0.002)[32 * Across + 40] > 0.5);
    }

    [Fact]
    public void ADeepPocketBehindAShoalKeepsTheLagTheShoalGaveIt()
    {
        const int Across = 64;
        float[] Bed(int shoalFrom) => Enumerable.Range(0, Across * Across).Select(i => i % Across >= shoalFrom && i % Across < shoalFrom + 10 ? 3f : 500f).ToArray();
        var lag = PhaseField.Lag(Across, Bed(16), 0.05, 0);
        Assert.NotEqual(0, lag[32 * Across + 40], 2);
    }

    private const int Size = 64;
    private const double Number = 0.1;

    private static float[] Beach(Func<int, float> depthAt)
    {
        var depth = new float[Size * Size];
        for (var i = 0; i < depth.Length; i++) depth[i] = depthAt(i % Size);
        return depth;
    }

    private static double Angle(float[] lag, double numberX, double numberZ, int x, int z)
    {
        double At(int ax, int az) => lag[az * Size + ax] + numberX * (ax + 0.5) + numberZ * (az + 0.5);
        var (gradientX, gradientZ) = ((At(x + 1, z) - At(x - 1, z)) / 2, (At(x, z + 1) - At(x, z - 1)) / 2);
        return Math.Atan2(Math.Abs(gradientZ), Math.Abs(gradientX));
    }

    [Fact]
    public void OverDeepWaterTheWavesRunUnchanged()
    {
        var lag = PhaseField.Lag(Size, Beach(_ => 500), -Number, 0);
        Assert.All(lag, value => Assert.Equal(0, value, 3));
    }

    [Fact]
    public void OverShoalingWaterTheWavesFallBehind()
    {
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? 0 : 0.6f * x), -Number, 0);
        Assert.True(lag[Size * Size / 2 + 4] > 0);
    }

    [Fact]
    public void CrestsComingInAtAnAngleTurnTowardTheShore()
    {
        var (numberX, numberZ) = (-Number * Math.Cos(Math.PI / 4), Number * Math.Sin(Math.PI / 4));
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? 0 : 0.6f * x), numberX, numberZ);
        var offshore = Angle(lag, numberX, numberZ, 60, 32);
        var inshore = Angle(lag, numberX, numberZ, 6, 32);
        Assert.True(offshore < Math.PI / 4);
        Assert.True(inshore < offshore - 0.1);
    }

    [Fact]
    public void TheTurnFollowsSnellsLaw()
    {
        var (numberX, numberZ) = (-Number * Math.Cos(Math.PI / 4), Number * Math.Sin(Math.PI / 4));
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? 0 : 0.6f * x), numberX, numberZ);
        var depth = 0.6 * 6;
        var expected = Math.Asin(Math.Sin(Math.PI / 4) * Number / WaveTheory.Wavenumber(Number, depth));
        Assert.Equal(expected, Angle(lag, numberX, numberZ, 6, 56), 1);
    }

    [Fact]
    public void LandStaysOutOfTheField()
    {
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? -1 : 5), -Number, 0);
        Assert.Equal(0, lag[Size * 10]);
    }

    [Fact]
    public void TwoMapsReadAroundDifferentPointsAgreeWhereTheyOverlap()
    {
        const int shift = 8;
        var (numberX, numberZ) = (-Number * Math.Cos(Math.PI / 4), Number * Math.Sin(Math.PI / 4));
        var here = PhaseField.Lag(Size, Beach(x => x < 2 ? 0 : 0.6f * x), numberX, numberZ);
        var moved = PhaseField.Lag(Size, Beach(x => x + shift < 2 ? 0 : 0.6f * (x + shift)), numberX, numberZ);
        Assert.Equal(here[32 * Size + 20], moved[32 * Size + 20 - shift], 2);
    }

    [Fact]
    public void WavesRunningOutToSeaKeepRunningOutOverTheShallows()
    {
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? 0 : 0.6f * x), Number, 0);
        var gradient = Number + (lag[32 * Size + 7] - lag[32 * Size + 5]) / 2;
        Assert.True(gradient > 0);
    }

    [Fact]
    public void WavesRunningOutToSeaKeepRunningOutOverTheTreadsOfAStepShelf()
    {
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? 0 : 3f * (x / 4)), Number, 0);
        var gradient = Number + (lag[32 * Size + 7] - lag[32 * Size + 5]) / 2;
        Assert.True(gradient > 0);
    }
}
