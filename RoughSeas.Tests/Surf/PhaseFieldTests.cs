using RoughSeas.Surf;

namespace RoughSeas.Tests.Surf;

public class PhaseFieldTests
{
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
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? 0 : 0.2f * x), -Number, 0);
        Assert.True(lag[Size * Size / 2 + 4] > 0);
    }

    [Fact]
    public void CrestsComingInAtAnAngleTurnTowardTheShore()
    {
        var (numberX, numberZ) = (-Number * Math.Cos(Math.PI / 4), Number * Math.Sin(Math.PI / 4));
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? 0 : 0.25f * x), numberX, numberZ);
        var offshore = Angle(lag, numberX, numberZ, 60, 32);
        var inshore = Angle(lag, numberX, numberZ, 6, 32);
        Assert.True(offshore < Math.PI / 4);
        Assert.True(inshore < offshore - 0.1);
    }

    [Fact]
    public void TheTurnFollowsSnellsLaw()
    {
        var (numberX, numberZ) = (-Number * Math.Cos(Math.PI / 4), Number * Math.Sin(Math.PI / 4));
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? 0 : 0.25f * x), numberX, numberZ);
        var depth = 0.25 * 6;
        var expected = Math.Asin(Math.Sin(Math.PI / 4) * Number / Shoaling.Wavenumber(Number, depth));
        Assert.Equal(expected, Angle(lag, numberX, numberZ, 6, 32), 1);
    }

    [Fact]
    public void LandStaysOutOfTheField()
    {
        var lag = PhaseField.Lag(Size, Beach(x => x < 2 ? -1 : 5), -Number, 0);
        Assert.Equal(0, lag[Size * 10]);
    }
}
