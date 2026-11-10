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

    [Fact]
    public void WithoutDeepWaterTwoMapsOfOneShoreAgreeWhereverTheyStart()
    {
        const int Across = 48;
        const int Shift = 8;
        float DepthAt(int x) => x < 40 ? (40 - x) * 0.25f : -1;
        float[] Map(int start) => Enumerable.Range(0, Across * Across).Select(i => DepthAt(i % Across + start)).ToArray();
        var (one, other) = (PhaseField.Lag(Across, Map(0), 0.03, 0.01), PhaseField.Lag(Across, Map(Shift), 0.03, 0.01));

        for (var x = Shift + 4; x < 36; x++)
        {
            var (firstAt, secondAt) = (24 * Across + x, 24 * Across + x - Shift);
            var (firstPlain, secondPlain) = (0.03 * (x + 0.5) + 0.01 * 24.5, 0.03 * (x - Shift + 0.5) + 0.01 * 24.5);
            Assert.Equal(one[firstAt] + firstPlain - secondPlain - (0.03 * Shift), other[secondAt], 1);
        }
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
